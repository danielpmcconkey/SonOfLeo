module Business.CrossDomainOrchestration.JournalEntryVoiding

open App.DataAccessLayer.DalError
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError

let private confirmJournalEntryIdIsReal
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    match journalEntryHeaderId |> JournalEntryHeader.fetchById context with
    | Ok _ -> Ok ()
    | Error e ->
        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
        then Error (JournalEntryHeaderIdDoesntExist(journalEntryHeaderId |> JournalEntryHeaderId.value))
        else Error e

let private confirmFiscalPeriodIsStillOpenBeforeVoiding
    (context: Context.Context)
    (journalEntryHeader: JournalEntryHeader.JournalEntryHeader)
    : Result<unit, IAppError> =
    let entryDate = journalEntryHeader |> JournalEntryHeader.entryDate
    let fiscalPeriodId = entryDate |> EntryDate.fiscalPeriodId
    result {
        let! fiscalPeriod =
            match fiscalPeriodId |> FiscalPeriod.fetchById context with
            | Ok x -> Ok x
            | Error e ->
                if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                then
                    let entryDatePrim = entryDate |> EntryDate.entryDate
                    let uuid = fiscalPeriodId |> FiscalPeriodId.value
                    Error (JournalEntryVoidingCannotFetchFiscalPeriod(entryDatePrim, uuid))
                else Error e
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

let private voidById
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    let uuid = journalEntryHeaderId |> JournalEntryHeaderId.value
    let now = context |> Context.getInitiationInstant
    let parameters =
        [ { name = "@modified"; value = DbInstant(now) }
          { name = "@newValue"; value = DbInstant(now) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    let queryStatement =
        $"""
        UPDATE ledger.journal_entry
        set
            modified_at = @modified
            , voided_at = @newValue
        WHERE unique_id = @unique_id
        and voided_at is null
        ;
    """
    result {
        let! je =
            match journalEntryHeaderId |> JournalEntryHeader.fetchById context with
            | Ok x -> Ok x
            | Error e ->
                if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                then Error (JournalEntryHeaderIdDoesntExist uuid)
                else Error e
        do! je |> confirmFiscalPeriodIsStillOpenBeforeVoiding context
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }

let private insertReason
    (context: Context.Context)
    (primaryJournalEntryId: JournalEntryHeaderId)
    (secondaryJournalEntryId: JournalEntryHeaderId option)
    (commentText: CommentText)
    : Result<unit, IAppError> =
    JournalEntryCommentOrchestration.constructNewAndPersist
        context
        primaryJournalEntryId
        secondaryJournalEntryId
        commentText
    |> Result.map ignore

let voidJournalEntry
    (context: Context.Context)
    (secondaryJournalEntryIdForComment: JournalEntryHeaderId option)
    (commentText: CommentText)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<JournalEntry, IAppError> =
    result {
        do! journalEntryHeaderId |> confirmJournalEntryIdIsReal context // validate here so the error message is helpful
        do! insertReason context journalEntryHeaderId secondaryJournalEntryIdForComment commentText
        do!
            journalEntryHeaderId
            |> voidById context
            |> function
                | Ok y -> Ok y
                | Error e ->
                    if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalNoOp
                    then Error (JournalEntryVoidingNoOp(journalEntryHeaderId |> JournalEntryHeaderId.value))
                    else Error e
        return! journalEntryHeaderId |> JournalEntryOrchestration.fetchById context
    }
