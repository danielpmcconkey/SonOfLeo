module InterfaceBridge.Routes.JournalEntryRoutes

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters
open InterfaceBridge.BoundaryConverters.JournalEntryFieldConverters
open InterfaceBridge.InterfaceContracts.JournalContracts
open InterfaceBridge.Json
open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntryVoiding
open InterfaceBridge.CommandRoute
open Utilities.FieldUpdate
open Utilities.FieldUpdate.FieldUpdate
open Utilities.ResultHelper

let private postNew payload _ =
    let envelope = AuditEnvelope.create JournalEntryPostNew
    result {
        let! input = Json.fromJson<JournalEntryInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! description = input.header.description |> JournalEntryDescription.create
        let! source = input.header.source |> ``convert JeSourceString Option to JeSource Option``
        let! entryDate = input.header.entryDate |> EntryDate.create None
        let! lines = input.lines |> ``convert [JournalEntryLineInput list] to [JournalEntryLinePrimitives list]``
        let! references = input.externalReferences |> ``convert [JournalEntryExternalReferenceInput list] to [JournalEntryExternalReferencePrimitives list]``
        let! comments = input.comments |> ``convert [JournalEntryCommentInput list] to [JournalEntryCommentPrimitives list]``
        let! newJournalEntry = JournalEntry.constructNewAndSaveToDb description source entryDate lines references comments envelope
        let! returnVal = ``convert JournalEntry to JournalEntryReturn`` newJournalEntry
        return! Json.toJson<JournalEntryReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private fetchById payload _ =
    result {
        let! input = Json.fromJson<JournalEntryFetchByIdInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! journalEntry = input.id |> JournalEntryHeaderId.fromGuid |> JournalEntry.fetchById None
        let! returnVal = ``convert JournalEntry to JournalEntryReturn`` journalEntry
        return! Json.toJson<JournalEntryReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private fetchByPeriod payload _ = // REQ-JE-3.3
    result {
        let! input = Json.fromJson<JournalEntryFetchByPeriodInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! fiscalPeriod = input.periodKey |> ``convert [FiscalPeriodKeyString] to FiscalPeriod`` None
        let! model = fiscalPeriod |> JournalEntry.fetchByPeriod None
        let! returnVal = model |> ``convert JournalEntry list to JournalEntryReturn list``
        return! Json.toJson<JournalEntryReturn list> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private fetchLinesByAccount payload _ = // REQ-JE-3.4
    result {
        let! input = Json.fromJson<JournalEntryFetchLinesByAccountInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! id = input.accountCode |> ``convert AccountCodeString to Id``
        let! model = id |> JournalEntryLine.fetchByAccountId None input.nonVoidedOnly
        let! returnVal = model |> ``convert JournalEntryLine list to JournalEntryLineReturn list``
        return! Json.toJson<JournalEntryLineReturn list> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5 

let private fetchByExternalReference payload _ =
    result {
        let! input = Json.fromJson<JournalEntryFetchByExternalReferenceInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! fi = input.fi |> convertOptionToDesiredTypeWithFallibleConverter JournalRefFinancialInstitution.create
        let! reference = input.reference |> convertOptionToDesiredTypeWithFallibleConverter JournalExternalReferenceText.create
        let! model = JournalEntry.fetchByReference None fi reference
        let! returnVal = model |> ``convert JournalEntry list to JournalEntryReturn list``
        return! Json.toJson<JournalEntryReturn list> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private fetchByDateRange payload _ =
    result {
        let! input = Json.fromJson<JournalEntryFetchByDateRangeInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! model = JournalEntry.fetchByDateRange None input.beginDate input.endDateInclusive
        let! returnVal = model |> ``convert JournalEntry list to JournalEntryReturn list``
        return! Json.toJson<JournalEntryReturn list> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private voidJe payload _ =
    let envelope = AuditEnvelope.create JournalEntryVoid
    result {
        let! input = Json.fromJson<JournalEntryVoidInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let headerId = input.id |> JournalEntryHeaderId.fromGuid
        let! secondaryJournalEntryIdForComment, commentText = input.reason |> ``convert [JournalEntryCommentInput] to [JournalEntryCommentPrimitives]``
        let! model = headerId |> voidJournalEntry envelope secondaryJournalEntryIdForComment commentText
        let! returnVal = ``convert JournalEntry to JournalEntryReturn`` model
        return! Json.toJson<JournalEntryReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private updateExternalReference payload _ =
    let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
    result {
        let! input = Json.fromJson<JournalEntryUpdateExternalReferenceInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let referenceId = input.id |> JournalEntryExternalReferenceId.fromGuid
        let! fi = input.fi |> convertOptionToDesiredTypeWithFallibleConverter JournalRefFinancialInstitution.create
        let fiFieldUpdate = 
            match fi with
            | None ->  NoChange
            | Some x -> SetTo x
        let! reference = input.reference |> convertOptionToDesiredTypeWithFallibleConverter JournalExternalReferenceText.create
        let referenceFieldUpdate = // todo: think about creating a primitive option -> field update with fallible converter
            match reference with
            | None ->  NoChange
            | Some x -> SetTo x
        let! model = referenceId |> JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText None envelope fiFieldUpdate referenceFieldUpdate 
        let returnVal = ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn`` model
        return! Json.toJson<JournalEntryExternalReferenceReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private addExternalReference payload _ = // REQ-JE-4.10
    let envelope = AuditEnvelope.create JournalEntryAddExternalReference
    result {
        let! input = Json.fromJson<JournalEntryAddExternalReferenceInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let headerId = input.journalEntryId |> JournalEntryHeaderId.fromGuid
        let! fi = input.reference.financialInstitution |> JournalRefFinancialInstitution.create
        let! reference = input.reference.referenceText |> JournalExternalReferenceText.create
        let! model = JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                         headerId fi reference envelope None
        let returnVal = ``convert JournalEntryExternalReference to JournalEntryExternalReferenceReturn`` model
        return! Json.toJson<JournalEntryExternalReferenceReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private addComment payload _ =
    let envelope = AuditEnvelope.create JournalEntryAddComment
    result {
        let! input = Json.fromJson<JournalEntryAddCommentInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let headerId = input.journalEntryId |> JournalEntryHeaderId.fromGuid
        let secondaryJournalEntryId = input.comment.secondaryJournalEntryId |> Option.map JournalEntryHeaderId.fromGuid
        let! commentText = input.comment.commentText |> CommentText.create
        let! model = JournalEntryCommentOrchestration.constructNewAndSaveToDb
                         headerId secondaryJournalEntryId commentText envelope None
        let returnVal = ``convert JournalEntryComment to JournalEntryCommentReturn`` model
        return! Json.toJson<JournalEntryCommentReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let private updateComment payload _ =
    let envelope = AuditEnvelope.create JournalEntryUpdateComment
    result {
        let! input = Json.fromJson<JournalEntryUpdateCommentInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let journalEntryCommentId = input.id |> JournalEntryCommentId.fromGuid
        let secondaryJournalEntryId =
            input.secondaryJournalEntryId
            |> convertFieldUpdateOptionToNewTypeOption JournalEntryHeaderId.fromGuid
        let! commentText =
            input.commentText
            |> convertFieldUpdateToNewTypeFallible CommentText.create
        let! model = JournalEntryCommentOrchestration.updateComment envelope journalEntryCommentId commentText secondaryJournalEntryId None
        let returnVal = ``convert JournalEntryComment to JournalEntryCommentReturn`` model
        return! Json.toJson<JournalEntryCommentReturn> returnVal } // REQ-NGUI-2.4, REQ-NGUI-3.5
    
let journalEntryDomainCommandRoutes = [
    { domain = "JournalEntry"; verb = "PostNew"; description = "Create a complete Journal Entry with all related objects (lines, references, comments)."
      inputType = typeof<JournalEntryInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  postNew } 
    { domain = "JournalEntry"; verb = "FetchById"; description = "Retrieve a complete Journal Entry based on its unique ID in the database."
      inputType = typeof<JournalEntryFetchByIdInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  fetchById } 
    { domain = "JournalEntry"; verb = "FetchByPeriod"; description = "Retrieve all Journal Entries (and related objects) for a given Fiscal Period."
      inputType = typeof<JournalEntryFetchByPeriodInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByPeriod } 
    { domain = "JournalEntry"; verb = "FetchLinesByAccount"; description = "Retrieve all Journal Entry Lines for a given Account."
      inputType = typeof<JournalEntryFetchLinesByAccountInput>.Name; outputType = typeof<JournalEntryLineReturn list>.Name; handler =  fetchLinesByAccount } 
    { domain = "JournalEntry"; verb = "FetchByExternalReference"; description = "Retrieve all Journal Entries (and related objects) matching a specific External Account Reference (FI and / or reference). You must specify at least one."
      inputType = typeof<JournalEntryFetchByExternalReferenceInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByExternalReference }
    { domain = "JournalEntry"; verb = "FetchByDateRange"; description = "Retrieve all Journal Entries (and related objects) whose entry date falls between begin and end (inclusive) dates."
      inputType = typeof<JournalEntryFetchByDateRangeInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByDateRange } 
    { domain = "JournalEntry"; verb = "Void"; description = "Void a Journal Entry by setting its “voided at” Instant to the system run time (requires a reason comment)"
      inputType = typeof<JournalEntryVoidInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  voidJe } 
    { domain = "JournalEntry"; verb = "UpdateExternalReference"; description = "Update an existing Journal Entry External Reference"
      inputType = typeof<JournalEntryUpdateExternalReferenceInput>.Name; outputType = typeof<JournalEntryExternalReferenceReturn>.Name; handler =  updateExternalReference } 
    { domain = "JournalEntry"; verb = "AddExternalReference"; description = "Add a new External Reference to an existing Journal Entry"
      inputType = typeof<JournalEntryAddExternalReferenceInput>.Name; outputType = typeof<JournalEntryExternalReferenceReturn>.Name; handler =  addExternalReference } 
    { domain = "JournalEntry"; verb = "AddComment"; description = "Add a new Comment to an existing Journal Entry"
      inputType = typeof<JournalEntryAddCommentInput>.Name; outputType = typeof<JournalEntryCommentReturn>.Name; handler =  addComment } 
    { domain = "JournalEntry"; verb = "UpdateComment"; description = "Update an existing Journal Entry Comment"
      inputType = typeof<JournalEntryUpdateCommentInput>.Name; outputType = typeof<JournalEntryCommentReturn>.Name; handler =  updateComment } ]