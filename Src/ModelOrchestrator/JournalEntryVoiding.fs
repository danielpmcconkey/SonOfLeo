module ModelOrchestrator.JournalEntryVoiding

open System
open Model.Audit
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryHeader
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open Utilities.ResultCE
open Utilities.DAL

let private voidById // REQ-JE-4.3
        (transaction: DbTransaction option)
        (auditEnvelope: AuditEnvelope)
        (journalEntryId: Guid)
        : Result<JournalEntryHeader, string> = 
    let parameters = [
            { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
            { name = "@newValue"; value = DbInstant (AuditEnvelope.instant auditEnvelope) }
            { name = "@unique_id"; value = UniqueId journalEntryId };
        ]
    let query = $"""
        UPDATE ledger.journal_entry
        set
            modified_at = @modified -- REQ-SYS-3.3
            , voided_at = @newValue
        WHERE unique_id = @unique_id
        and voided_at is null -- REQ-JE-4.6
        ;
    """
    result {
        let! _ =  validateFiscalPeriodIsOpen journalEntryId transaction // REQ-JE-4.5
        let! _ = executeNonQuery query parameters ExactlyOne transaction // REQ-JE-4.6
        return! journalEntryId |> fetchById transaction
    }

let private insertReason 
        (transaction: DbTransaction option)
        (auditEnvelope: AuditEnvelope)
        (reason: JournalEntryCommentPrimitives)
        (journalEntryId: Guid)
        : Result<unit, string> =
    let result = JournalEntryComment.constructNewAndSaveToDb
                            journalEntryId
                            reason.secondaryJournalEntryId
                            reason.commentText
                            auditEnvelope
                            transaction
    match result with
    | Error e -> Error e
    | _ -> Ok ()

let voidJournalEntryOrchestration // REQ-JE-4.3
        (auditEnvelope: AuditEnvelope)
        (reason: JournalEntryCommentPrimitives)
        (journalEntryId: Guid)
        : Result<JournalEntry, string> =

    let transaction = createDbTransaction() |> Result.defaultWith failwith // if this fails, nothing can proceed
    let railRoad = result {
        do! insertReason (Some transaction) auditEnvelope reason journalEntryId
        let! newHeader = journalEntryId |> voidById (Some transaction) auditEnvelope
        let! validLines = journalEntryId |> JournalEntryLine.fetchByJournalEntryId (Some transaction)
        let! validReferences = journalEntryId |> JournalEntryExternalReference.fetchByJournalEntryId (Some transaction)
        let! validComments = journalEntryId |> JournalEntryComment.fetchByJournalEntryId (Some transaction)
        return! constructFromPreValidatedComponents newHeader validLines validReferences validComments
    }
    match railRoad with
    | Error e ->
        transaction |> rollbackDbTransactionAndDisposeConnection |> Result.defaultWith failwith
        Error e
    | Ok je ->
        transaction |> commitDbTransactionAndDisposeConnection |> Result.defaultWith failwith
        Ok je
