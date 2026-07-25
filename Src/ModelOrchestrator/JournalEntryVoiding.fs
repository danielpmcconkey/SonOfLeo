module ModelOrchestrator.JournalEntryVoiding

open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Utilities.AppError
open Utilities.ResultHelper
open Utilities.DAL

let private confirmFiscalPeriodIsStillOpenBeforeVoiding
    (transaction: DbTransaction option)
    (journalEntryHeader: JournalEntryHeader)
    : Result<unit, AppError> =
    let entryDate = journalEntryHeader |> JournalEntryHeader.entryDate
    let fiscalPeriodId = entryDate |> EntryDate.fiscalPeriodId
    result {
        let! fiscalPeriod =
            match fiscalPeriodId |> FiscalPeriod.fetchById transaction with
            | Ok x -> Ok x
            | Error(DalResultantRowsDidntMatchExpectation _) ->
                Error(
                    JournalEntryVoidingCannotFetchFiscalPeriod(
                        entryDate |> EntryDate.entryDate,
                        fiscalPeriodId |> FiscalPeriodId.value
                    )
                )
            | Error e -> Error e
        return!
            match fiscalPeriod |> FiscalPeriod.isOpen with
            | true -> Ok()
            | false ->
                Error(
                    JournalEntryVoidingFiscalPeriodIsClosed(
                        entryDate |> EntryDate.entryDate,
                        fiscalPeriodId |> FiscalPeriodId.value
                    )
                )
    }

let private voidById // REQ-JE-4.3
    (transaction: DbTransaction option)
    (auditEnvelope: AuditEnvelope)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, AppError> =
    let uuid = journalEntryHeaderId |> JournalEntryHeaderId.value
    let parameters =
        [ { name = "@modified"; value = DbInstant(AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3
          { name = "@newValue"; value = DbInstant(AuditEnvelope.instant auditEnvelope) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    let query =
        $"""
        UPDATE ledger.journal_entry
        set
            modified_at = @modified -- REQ-SYS-3.3
            , voided_at = @newValue
        WHERE unique_id = @unique_id
        and voided_at is null -- REQ-JE-4.6
        ;
    """
    result {
        let! je = journalEntryHeaderId |> JournalEntryHeader.fetchById transaction
        do! je |> confirmFiscalPeriodIsStillOpenBeforeVoiding transaction // REQ-JE-4.5
        do! executeNonQuery query parameters ExactlyOne transaction // REQ-JE-4.6
    }

let private insertReason // REQ-JE-4.4
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    (auditEnvelope: AuditEnvelope)
    (transaction: DbTransaction option)
    : Result<unit, AppError> =
    JournalEntryCommentOrchestration.constructNewAndSaveToDb
        primaryJournalEntryId
        secondaryJournalEntryId
        commentText
        auditEnvelope
        transaction
    |> Result.map ignore

let voidJournalEntry // REQ-JE-4.3
    (auditEnvelope: AuditEnvelope)
    (secondaryJournalEntryIdForComment: JournalEntryHeaderId option)
    (commentText: CommentText)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<JournalEntry, AppError> =
    let transaction = createDbTransaction() |> Result.defaultWith(fun e -> failwith(AppError.toMessage e)) // if this fails, nothing can proceed
    let railRoad =
        result {
            do!
                insertReason
                    journalEntryHeaderId
                    secondaryJournalEntryIdForComment
                    commentText
                    auditEnvelope
                    (Some transaction) // REQ-JE-4.4
            do!
                journalEntryHeaderId
                |> voidById (Some transaction) auditEnvelope
                |> function
                    | Ok y -> Ok y
                    | Error(DalResultantRowsDidntMatchExpectation(_, 0)) ->
                        Error(JournalEntryVoidingNoOp(journalEntryHeaderId |> JournalEntryHeaderId.value))
                    | Error(DalResultantRowsDidntMatchExpectation(expected, actual)) ->
                        Error(DalResultantRowsDidntMatchExpectation(expected, actual))
                    | Error e -> Error e
            return! journalEntryHeaderId |> JournalEntry.fetchById(Some transaction)
        }
    match railRoad with
    | Error e ->
        transaction
        |> rollbackDbTransactionAndDisposeConnection
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        Error e
    | Ok je ->
        transaction
        |> commitDbTransactionAndDisposeConnection
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        Ok je
