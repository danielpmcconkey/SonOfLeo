module SonOfLeoCli.JournalEntryRoutes

open Model
open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open ModelOrchestrator
open Model.Ledger.JournalEntryPrimitives
open Model.UI
open ModelOrchestrator.JournalEntryVoiding
open Utilities
open Utilities.ResultCE
open InterfaceContractTypes
    
// ****************************************
// CONVERSION FUNCTIONS
// ****************************************

let private convertJournalEntryHeaderInputToPrimitives
    (input: JournalEntryHeaderInput)
    : JournalEntryHeaderPrimitives = {
            description = input.description
            source = input.source
            entryDate = input.entryDate
            voidedAt = None } // creating a new JE that's been pre-voided is against the rules

let private convertJournalEntryLineInputToPrimitives
        (input: JournalEntryLineInput)
        : Result<JournalEntryLinePrimitives, string> =
    result {
        let! accountId =
            input.accountCode
            |> LookupCache.accountCodeToId.fetch // REQ-JE-2.3, REQ-JE-2.4
            |> Result.mapError(fun e -> $"Provided Account Code of {input.accountCode} didn't match any recorded Accounts in the database. Further details: {e}")
        return {
                accountId = accountId
                amount = input.amount
                lineType = input.lineType
                memo = input.memo }}

let private convertJournalEntryExternalReferenceInputToPrimitives
    (input: JournalEntryExternalReferenceInput)
    : JournalEntryExternalReferencePrimitives = {
                financialInstitution = input.financialInstitution 
                referenceText = input.referenceText }

let private convertJournalEntryCommentInputToPrimitives
    (input: JournalEntryCommentInput)
    : JournalEntryCommentPrimitives = {
                secondaryJournalEntryId = input.secondaryJournalEntryId
                commentText = input.commentText }
    
let private convertJournalEntryInputToPrimitives
    (input: JournalEntryInput)
    : Result<JournalEntryPrimitives, string> =
    result {
        let! lines =
            input.lines
            |> List.map(fun x -> x |> convertJournalEntryLineInputToPrimitives)
            |> ListHelper.listOfResultsToResultsList
        return {
                header = input.header |> convertJournalEntryHeaderInputToPrimitives
                lines = lines
                externalReferences = input.externalReferences |> List.map(fun x -> x |> convertJournalEntryExternalReferenceInputToPrimitives)
                comments = input.comments |> List.map(fun x -> x |> convertJournalEntryCommentInputToPrimitives) } }

let private convertJournalEntryLineToReturn
    (model: JournalEntryLine)
    : Result<JournalEntryLineReturn, string> =
        result {
            let! accountCode =
                    let accountId = model |> JournalEntryLine.accountId
                    accountId
                    |> LookupCache.accountIdToCode.fetch
                    |> Result.mapError(fun e -> $"Returned Account ID of {accountId} didn't match any recorded Accounts in the database. Further details: {e}")
            return {
                id = model |> JournalEntryLine.uniqueId
                accountCode = accountCode
                amount = model |> JournalEntryLine.amount |> MoneyModule.amount
                lineType = model |> JournalEntryLine.lineType |> JournalEntryLineType.toString
                memo = model |> JournalEntryLine.memo |> Option.map(fun x -> x |> LineMemo.value)
                createdAt = model |> JournalEntryLine.createdAt
                modifiedAt = model |> JournalEntryLine.modifiedAt } }

let private convertJournalEntryHeaderToReturn
    (model: JournalEntryHeader)
    : JournalEntryHeaderReturn = {
        id = model |> JournalEntryHeader.uniqueId
        description = model |> JournalEntryHeader.description |> Description.value
        source = model |> JournalEntryHeader.source |> Option.map(fun x -> x |> Source.value)
        entryDate = model |> JournalEntryHeader.entryDate |> EntryDate.entryDate
        voidedAt = model |> JournalEntryHeader.voidedAt
        createdAt = model |> JournalEntryHeader.createdAt
        modifiedAt = model |> JournalEntryHeader.modifiedAt }

let private convertJournalEntryExternalReferenceToReturn
    (model: JournalEntryExternalReference)
    : JournalEntryExternalReferenceReturn = {
        id = model |> JournalEntryExternalReference.uniqueId
        financialInstitution = model |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
        referenceText = model |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
        createdAt = model |> JournalEntryExternalReference.createdAt
        modifiedAt = model |> JournalEntryExternalReference.modifiedAt }

let private convertJournalEntryCommentToReturn
    (model: JournalEntryComment)
    : JournalEntryCommentReturn = {
        id = model |> JournalEntryComment.uniqueId
        secondaryJournalEntryId = model |> JournalEntryComment.secondaryJournalEntryId
        commentText = model |> JournalEntryComment.commentText |> CommentText.value
        createdAt = model |> JournalEntryComment.createdAt
        modifiedAt = model |> JournalEntryComment.modifiedAt }

let private convertJournalEntryToReturn
    (model: JournalEntry)
    : Result<JournalEntryReturn, string> =
    result {
        let! lines =
            model
            |> lines
            |> List.map(fun x -> x |> convertJournalEntryLineToReturn)
            |> ListHelper.listOfResultsToResultsList
        return {
        header = model |> header |> convertJournalEntryHeaderToReturn
        lines = lines
        externalReferences = model |> externalReferences |> List.map(fun x -> x |> convertJournalEntryExternalReferenceToReturn)
        comments = model |> comments |> List.map(fun x -> x |> convertJournalEntryCommentToReturn) } }
    
// ****************************************
// ROUTING FUNCTIONS
// ****************************************


let private postNew payload _ =
    result {
        let! input = Json.fromJson<JournalEntryInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let! primitives = input |> convertJournalEntryInputToPrimitives
        let! model = primitives |> orchestrateCreation envelope
        let! returnVal = convertJournalEntryToReturn model
        return! Json.toJson<JournalEntryReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private fetchById payload _ =
    result {
        let! input = Json.fromJson<JournalEntryFetchByIdInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! model = input.id |> JournalEntryFetching.fetchById
        let! returnVal = convertJournalEntryToReturn model
        return! Json.toJson<JournalEntryReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private fetchByPeriod payload _ = // REQ-JE-3.3
    result {
        let! input = Json.fromJson<JournalEntryFetchByPeriodInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! id =
            input.periodKey
            |> LookupCache.fiscalPeriodKeyToId.fetch
            |> Result.mapError(fun e -> $"Period key provided didn't match any recorded Fiscal Periods in the database. Further details: {e}")
        let! model = id |> JournalEntryFetching.fetchByPeriod
        let! returnVal = model |> List.map(fun x -> x |> convertJournalEntryToReturn) |> ListHelper.listOfResultsToResultsList
        return! Json.toJson<JournalEntryReturn list> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private fetchLinesByAccount payload _ = // REQ-JE-3.4
    result {
        let! input = Json.fromJson<JournalEntryFetchLinesByAccountInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! id =
            input.accountCode
            |> LookupCache.accountCodeToId.fetch
            |> Result.mapError(fun e -> $"Account Code provided didn't match any recorded Accounts in the database. Further details: {e}")
        let! model = id |> JournalEntryLine.fetchByAccountId None input.nonVoidedOnly
        let! returnVal = model |> List.map(fun x -> x |> convertJournalEntryLineToReturn) |> ListHelper.listOfResultsToResultsList
        return! Json.toJson<JournalEntryLineReturn list> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private fetchByExternalReference payload _ =
    result {
        let! input = Json.fromJson<JournalEntryFetchByExternalReferenceInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! model = JournalEntryFetching.fetchByReference input.fi input.reference
        let! returnVal = model |> List.map(fun x -> x |> convertJournalEntryToReturn) |> ListHelper.listOfResultsToResultsList
        return! Json.toJson<JournalEntryReturn list> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private voidJe payload _ =
    result {
        let! input = Json.fromJson<JournalEntryVoidInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create JournalEntryVoid
        let reason = input.reason |> convertJournalEntryCommentInputToPrimitives
        let! model = voidJournalEntryOrchestration envelope reason input.id
        let! returnVal = convertJournalEntryToReturn model
        return! Json.toJson<JournalEntryReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private updateExternalReference payload _ =
    result {
        let! input = Json.fromJson<JournalEntryUpdateExternalReferenceInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
        let! model = JournalEntryExternalReference.updateFiAndReferenceText envelope input.id input.fi input.reference None
        let returnVal = convertJournalEntryExternalReferenceToReturn model
        return! Json.toJson<JournalEntryExternalReferenceReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private addExternalReference payload _ = // REQ-JE-4.10
    result {
        let! input = Json.fromJson<JournalEntryAddExternalReferenceInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let! model = JournalEntryExternalReference.constructNewAndSaveToDb
                         input.journalEntryId
                         input.reference.financialInstitution
                         input.reference.referenceText
                         envelope
                         None
        let returnVal = convertJournalEntryExternalReferenceToReturn model
        return! Json.toJson<JournalEntryExternalReferenceReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private addComment payload _ =
    result {
        let! input = Json.fromJson<JournalEntryAddCommentInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let! model = JournalEntryComment.constructNewAndSaveToDb
                         input.journalEntryId
                         input.comment.secondaryJournalEntryId
                         input.comment.commentText
                         envelope
                         None
        let returnVal = convertJournalEntryCommentToReturn model
        return! Json.toJson<JournalEntryCommentReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
let private updateComment payload _ =
    result {
        let! input = Json.fromJson<JournalEntryUpdateCommentInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create JournalEntryUpdateComment
        let! model = JournalEntryComment.updateComment envelope input.id input.commentText input.secondaryJournalEntryId None
        let returnVal = convertJournalEntryCommentToReturn model
        return! Json.toJson<JournalEntryCommentReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
    
let journalEntryDomainCommandRoutes = [
    { domain = "JournalEntry"; verb = "PostNew"; description = "Create a complete Journal Entry with all related objects (lines, references, comments)."
      inputType = typeof<JournalEntryInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  postNew } 
    { domain = "JournalEntry"; verb = "FetchById"; description = "Retrieve a complete Journal Entry based on its unique ID in the database."
      inputType = typeof<JournalEntryFetchByIdInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  fetchById } 
    { domain = "JournalEntry"; verb = "FetchByPeriod"; description = "Retrieve all Journal Entries (and related objects) for a given Fiscal Period."
      inputType = typeof<JournalEntryFetchByPeriodInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByPeriod } 
    { domain = "JournalEntry"; verb = "FetchLinesByAccount"; description = "Retrieve all Journal Entry Lines for a given Account."
      inputType = typeof<JournalEntryFetchLinesByAccountInput>.Name; outputType = typeof<JournalEntryLineReturn list>.Name; handler =  fetchLinesByAccount } 
    { domain = "JournalEntry"; verb = "FetchByExternalReference"; description = "Retrieve all Journal Entries (and related objects) matching a specific External Account Reference (FI and reference)"
      inputType = typeof<JournalEntryFetchByExternalReferenceInput>.Name; outputType = typeof<JournalEntryReturn list>.Name; handler =  fetchByExternalReference } 
    { domain = "JournalEntry"; verb = "Void"; description = "Void a Journal Entry by setting its “voided at” Instant to the system run time (requires a reason comment)"
      inputType = typeof<JournalEntryVoidInput>.Name; outputType = typeof<JournalEntryReturn>.Name; handler =  voidJe } 
    { domain = "JournalEntry"; verb = "UpdateExternalReference"; description = "Update an existing Journal Entry External Reference"
      inputType = typeof<JournalEntryUpdateExternalReferenceInput>.Name; outputType = typeof<JournalEntryExternalReferenceReturn>.Name; handler =  updateExternalReference } 
    { domain = "JournalEntry"; verb = "AddExternalReference"; description = "Add a new External Reference to an existing Journal Entry"
      inputType = typeof<JournalEntryAddExternalReferenceInput>.Name; outputType = typeof<JournalEntryExternalReferenceReturn>.Name; handler =  addExternalReference } 
    { domain = "JournalEntry"; verb = "AddComment"; description = "Add a new Comment to an existing Journal Entry"
      inputType = typeof<JournalEntryAddCommentInput>.Name; outputType = typeof<JournalEntryCommentReturn>.Name; handler =  addComment } 
    { domain = "JournalEntry"; verb = "UpdateComment"; description = "Update an existing Journal Entry Comment"
      inputType = typeof<JournalEntryUpdateCommentInput>.Name; outputType = typeof<JournalEntryCommentReturn>.Name; handler =  updateComment } 

]