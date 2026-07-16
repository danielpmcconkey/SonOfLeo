module ModelOrchestrator.JournalEntryVoiding

open System
open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.JournalEntryPrimitives
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.Journaling.JournalEntryHeader
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open Utilities.ResultCE
open Utilities.DAL

let private voidById // REQ-JE-4.3
        (transaction: DbTransaction option)
        (auditEnvelope: AuditEnvelope)
        (journalEntryId: Guid)
        : Result<JournalEntryHeader, AppError> = 
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
        let! je = journalEntryId |> fetchById transaction
        let fp = je |> entryDate |> EntryDate.fiscalPeriod
        do! if fp |> FiscalPeriod.isOpen = false then Error "Cannot void a Journal Entry in a closed period" else Ok() // REQ-JE-4.5
        let! _ = executeNonQuery query parameters ExactlyOne transaction // REQ-JE-4.6
        return! journalEntryId |> fetchById transaction
    }

let private insertReason  // REQ-JE-4.4
        (transaction: DbTransaction option)
        (auditEnvelope: AuditEnvelope)
        (reason: JournalEntryCommentPrimitives)
        (journalEntryId: Guid)
        : Result<unit, AppError> =
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
        (reason: JournalEntryCommentPrimitives) // REQ-JE-4.4
        (journalEntryId: Guid)
        : Result<JournalEntry, AppError> =

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
