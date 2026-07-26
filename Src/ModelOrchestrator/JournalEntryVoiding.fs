module ModelOrchestrator.JournalEntryVoiding

open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery
open Context.Context

let private confirmFiscalPeriodIsStillOpenBeforeVoiding
    (context: Context)
    (journalEntryHeader: JournalEntryHeader)
    : Result<unit, AppError> =
    let entryDate = journalEntryHeader |> JournalEntryHeader.entryDate
    let fiscalPeriodId = entryDate |> EntryDate.fiscalPeriodId
    result {
        let! fiscalPeriod =
            match fiscalPeriodId |> FiscalPeriod.fetchById context with
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
    (context: Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, AppError> =
    let uuid = journalEntryHeaderId |> JournalEntryHeaderId.value
    let now = context |> getInitiationInstant
    let parameters =
        [ { name = "@modified"; value = DbInstant(now) } // REQ-SYS-3.3
          { name = "@newValue"; value = DbInstant(now) }
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
        let! je = journalEntryHeaderId |> JournalEntryHeader.fetchById context
        do! je |> confirmFiscalPeriodIsStillOpenBeforeVoiding context // REQ-JE-4.5
        do! executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne // REQ-JE-4.6
    }

let private insertReason // REQ-JE-4.4
    (context: Context)
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    : Result<unit, AppError> =
    JournalEntryCommentOrchestration.constructNewAndSaveToDb
        context
        primaryJournalEntryId
        secondaryJournalEntryId
        commentText
    |> Result.map ignore

let voidJournalEntry // REQ-JE-4.3
    (context: Context)
    (secondaryJournalEntryIdForComment: JournalEntryHeaderId option)
    (commentText: CommentText)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<JournalEntry, AppError> =
    result {
        do! insertReason context journalEntryHeaderId secondaryJournalEntryIdForComment commentText // REQ-JE-4.4
        do!
            journalEntryHeaderId
            |> voidById context
            |> function
                | Ok y -> Ok y
                | Error(DalResultantRowsDidntMatchExpectation(_, 0)) ->
                    Error(JournalEntryVoidingNoOp(journalEntryHeaderId |> JournalEntryHeaderId.value))
                | Error(DalResultantRowsDidntMatchExpectation(expected, actual)) ->
                    Error(DalResultantRowsDidntMatchExpectation(expected, actual))
                | Error e -> Error e
        return! journalEntryHeaderId |> JournalEntry.fetchById context
    }
