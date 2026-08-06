module InterfaceBridge.Routes.JournalEntryRoutes

open Context.Context
open DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters
open InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters
open InterfaceBridge.InterfaceContracts.JournalContracts
open InterfaceBridge.Json
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntryVoiding
open InterfaceBridge.CommandRoute
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.FieldUpdate.FieldUpdate
open Utilities.ResultHelper
open Logger.Audit

let private postNew payload _ : Result<string, AppError> =
    runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew (fun context ->
        result {
            let! input = Json.fromJson<JournalEntryInput> payload
            let! description = input.header.description |> JournalEntryDescription.create
            let! source = input.header.source |> ``convert JeSourceString Option to JeSource Option``
            let! entryDate = input.header.entryDate |> EntryDate.create context
            let! lines =
                input.lines |> ``convert [JournalEntryLineInput list] to [JournalEntryLinePrimitives list]`` context
            let! references =
                input.externalReferences
                |> ``convert [JournalEntryExternalReferenceInput list] to [JournalEntryExternalReferencePrimitives list]``
            let! comments =
                input.comments
                |> ``convert [JournalEntryCommentInput list] to [JournalEntryCommentPrimitives list]``
            let! newJournalEntry =
                JournalEntry.constructNewAndSaveToDb context description source entryDate lines references comments
            let! returnVal = ``convert JournalEntry to JournalEntryReturn`` context newJournalEntry
            return! Json.toJson<JournalEntryReturn> returnVal
        })

let private fetchById payload _ =
    let context = create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<JournalEntryFetchByIdInput> payload
        let! journalEntry = input.id |> JournalEntryHeaderId.fromGuid |> JournalEntry.fetchById context
        let! returnVal = ``convert JournalEntry to JournalEntryReturn`` context journalEntry
        return! Json.toJson<JournalEntryReturn> returnVal
    }

let private fetchByPeriod payload _ =
    let context = create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<JournalEntryFetchByPeriodInput> payload
        let! fiscalPeriod = input.periodKey |> ``convert [FiscalPeriodKeyString] to FiscalPeriod`` context
        let! model = fiscalPeriod |> JournalEntry.fetchByPeriod context
        let! returnVal = model |> ``convert JournalEntry list to JournalEntryReturn list`` context
        return! Json.toJson<JournalEntryReturn list> returnVal
    }

let private fetchLinesByAccount payload _ =
    let context = create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<JournalEntryFetchLinesByAccountInput> payload
        let! id = input.accountCode |> ``convert AccountCodeString to Id`` context
        let! model = id |> JournalEntryLine.fetchByAccountId context input.nonVoidedOnly
        let! returnVal = model |> ``convert JournalEntryLine list to JournalEntryLineReturn list`` context
        return! Json.toJson<JournalEntryLineReturn list> returnVal
    }

let private fetchByExternalReference payload _ =
    let context = create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<JournalEntryFetchByExternalReferenceInput> payload
        let! fi = input.fi |> convertOptionToDesiredTypeWithFallibleConverter JournalRefFinancialInstitution.create
        let! reference =
            input.reference
            |> convertOptionToDesiredTypeWithFallibleConverter JournalExternalReferenceText.create
        let! model = JournalEntry.fetchByReference context fi reference
        let! returnVal = model |> ``convert JournalEntry list to JournalEntryReturn list`` context
        return! Json.toJson<JournalEntryReturn list> returnVal
    }

let private fetchByDateRange payload _ =
    let context = create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<JournalEntryFetchByDateRangeInput> payload
        let! model = JournalEntry.fetchByDateRange context input.beginDate input.endDateInclusive
        let! returnVal = model |> ``convert JournalEntry list to JournalEntryReturn list`` context
        return! Json.toJson<JournalEntryReturn list> returnVal
    }

let private voidJe payload _ =
    runCommandRouteAndAutoCompleteTransaction JournalEntryVoid (fun context ->
        result {
            let! input = Json.fromJson<JournalEntryVoidInput> payload
            let headerId = input.id |> JournalEntryHeaderId.fromGuid
            let! secondaryJournalEntryIdForComment, commentText =
                input.reason |> ``convert [JournalEntryCommentInput] to [JournalEntryCommentPrimitives]``
            let! model = headerId |> voidJournalEntry context secondaryJournalEntryIdForComment commentText
            let! returnVal = ``convert JournalEntry to JournalEntryReturn`` context model
            return! Json.toJson<JournalEntryReturn> returnVal
        })

let private updateExternalReference payload _ =
    let context = create NoTransaction JournalEntryUpdateExternalReference
    result {
        let! input = Json.fromJson<JournalEntryUpdateExternalReferenceInput> payload
        let referenceId = input.id |> JournalEntryExternalReferenceId.fromGuid
        let! fi = input.fi |> convertOptionToDesiredTypeWithFallibleConverter JournalRefFinancialInstitution.create
        let fiFieldUpdate =
            match fi with
            | None -> NoChange
            | Some x -> SetTo x
        let! reference =
            input.reference
            |> convertOptionToDesiredTypeWithFallibleConverter JournalExternalReferenceText.create
        let referenceFieldUpdate = // todo: think about creating a primitive option -> field update with fallible converter
            match reference with
            | None -> NoChange
            | Some x -> SetTo x
        let! model =
            referenceId
            |> JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                context
                fiFieldUpdate
                referenceFieldUpdate
        let returnVal = ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn`` model
        return! Json.toJson<JournalEntryExternalReferenceReturn> returnVal
    }

let private addExternalReference payload _ =
    let context = create NoTransaction JournalEntryAddExternalReference
    result {
        let! input = Json.fromJson<JournalEntryAddExternalReferenceInput> payload
        let headerId = input.journalEntryId |> JournalEntryHeaderId.fromGuid
        let! fi = input.reference.financialInstitution |> JournalRefFinancialInstitution.create
        let! reference = input.reference.referenceText |> JournalExternalReferenceText.create
        let! model = JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb context headerId fi reference
        let returnVal = ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn`` model
        return! Json.toJson<JournalEntryExternalReferenceReturn> returnVal
    }

let private addComment payload _ =
    let context = create NoTransaction JournalEntryAddComment
    result {
        let! input = Json.fromJson<JournalEntryAddCommentInput> payload
        let headerId = input.journalEntryId |> JournalEntryHeaderId.fromGuid
        let secondaryJournalEntryId =
            input.comment.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.fromGuid
        let! commentText = input.comment.commentText |> CommentText.create
        let! model =
            JournalEntryCommentOrchestration.constructNewAndSaveToDb
                context
                headerId
                secondaryJournalEntryId
                commentText
        let returnVal = ``convert JournalEntryComment to JournalEntryCommentReturn`` model
        return! Json.toJson<JournalEntryCommentReturn> returnVal
    }

let private updateComment payload _ =
    let context = create NoTransaction JournalEntryUpdateComment
    result {
        let! input = Json.fromJson<JournalEntryUpdateCommentInput> payload
        let journalEntryCommentId = input.id |> JournalEntryCommentId.fromGuid
        let secondaryJournalEntryId =
            input.secondaryJournalEntryId
            |> convertFieldUpdateOptionToNewTypeOption JournalEntryHeaderId.fromGuid
        let! commentText = input.commentText |> convertFieldUpdateToNewTypeFallible CommentText.create
        let! model =
            JournalEntryCommentOrchestration.updateComment
                context
                journalEntryCommentId
                commentText
                secondaryJournalEntryId
        let returnVal = ``convert JournalEntryComment to JournalEntryCommentReturn`` model
        return! Json.toJson<JournalEntryCommentReturn> returnVal
    }

let journalEntryDomainCommandRoutes =
    [ { domain = "JournalEntry"
        verb = "PostNew"
        description = "Create a complete Journal Entry with all related objects (lines, references, comments)."
        inputContract = typeof<JournalEntryInput>.Name
        outputContract = typeof<JournalEntryReturn>.Name
        handler = postNew }
      { domain = "JournalEntry"
        verb = "FetchById"
        description = "Retrieve a complete Journal Entry based on its unique ID in the database."
        inputContract = typeof<JournalEntryFetchByIdInput>.Name
        outputContract = typeof<JournalEntryReturn>.Name
        handler = fetchById }
      { domain = "JournalEntry"
        verb = "FetchByPeriod"
        description = "Retrieve all Journal Entries (and related objects) for a given Fiscal Period."
        inputContract = typeof<JournalEntryFetchByPeriodInput>.Name
        outputContract = typeof<JournalEntryReturn list>.Name
        handler = fetchByPeriod }
      { domain = "JournalEntry"
        verb = "FetchLinesByAccount"
        description = "Retrieve all Journal Entry Lines for a given Account."
        inputContract = typeof<JournalEntryFetchLinesByAccountInput>.Name
        outputContract = typeof<JournalEntryLineReturn list>.Name
        handler = fetchLinesByAccount }
      { domain = "JournalEntry"
        verb = "FetchByExternalReference"
        description =
          "Retrieve all Journal Entries (and related objects) matching a specific External Account Reference (FI and / or reference). You must specify at least one."
        inputContract = typeof<JournalEntryFetchByExternalReferenceInput>.Name
        outputContract = typeof<JournalEntryReturn list>.Name
        handler = fetchByExternalReference }
      { domain = "JournalEntry"
        verb = "FetchByDateRange"
        description =
          "Retrieve all Journal Entries (and related objects) whose entry date falls between begin and end (inclusive) dates."
        inputContract = typeof<JournalEntryFetchByDateRangeInput>.Name
        outputContract = typeof<JournalEntryReturn list>.Name
        handler = fetchByDateRange }
      { domain = "JournalEntry"
        verb = "Void"
        description =
          "Void a Journal Entry by setting its “voided at” Instant to the system run time (requires a reason comment)"
        inputContract = typeof<JournalEntryVoidInput>.Name
        outputContract = typeof<JournalEntryReturn>.Name
        handler = voidJe }
      { domain = "JournalEntry"
        verb = "UpdateExternalReference"
        description = "Update an existing Journal Entry External Reference"
        inputContract = typeof<JournalEntryUpdateExternalReferenceInput>.Name
        outputContract = typeof<JournalEntryExternalReferenceReturn>.Name
        handler = updateExternalReference }
      { domain = "JournalEntry"
        verb = "AddExternalReference"
        description = "Add a new External Reference to an existing Journal Entry"
        inputContract = typeof<JournalEntryAddExternalReferenceInput>.Name
        outputContract = typeof<JournalEntryExternalReferenceReturn>.Name
        handler = addExternalReference }
      { domain = "JournalEntry"
        verb = "AddComment"
        description = "Add a new Comment to an existing Journal Entry"
        inputContract = typeof<JournalEntryAddCommentInput>.Name
        outputContract = typeof<JournalEntryCommentReturn>.Name
        handler = addComment }
      { domain = "JournalEntry"
        verb = "UpdateComment"
        description = "Update an existing Journal Entry Comment"
        inputContract = typeof<JournalEntryUpdateCommentInput>.Name
        outputContract = typeof<JournalEntryCommentReturn>.Name
        handler = updateComment } ]
