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
open Business.FinancialServices.CashFlow

let private confirmJournalEntryIdIsReal
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    journalEntryHeaderId |> JournalEntryHeader.fetchById context
    |> whenNoRows (JournalEntryHeaderIdDoesntExist(journalEntryHeaderId |> JournalEntryHeaderId.value))
    |> Result.map ignore

let private confirmFiscalPeriodIsStillOpenBeforeVoiding
    (context: Context.Context)
    (journalEntryHeader: JournalEntryHeader.JournalEntryHeader)
    : Result<unit, IAppError> =
    let entryDate = journalEntryHeader |> JournalEntryHeader.entryDate
    let fiscalPeriodId = entryDate |> EntryDate.fiscalPeriodId
    result {
        let! fiscalPeriod =
            let entryDatePrim = entryDate |> EntryDate.entryDate
            let uuid = fiscalPeriodId |> FiscalPeriodId.value
            fiscalPeriodId |> FiscalPeriod.fetchById context
            |> whenNoRows (JournalEntryVoidingCannotFetchFiscalPeriod(entryDatePrim, uuid))
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
            journalEntryHeaderId |> JournalEntryHeader.fetchById context
            |> whenNoRows (JournalEntryHeaderIdDoesntExist uuid)
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

/// A void is refused while any Payment points at one of the entry's lines: voiding it would leave the Invoice paid and
/// posted on cash the ledger no longer records (REQ-JE-4.14). It runs before anything is written, the reason comment
/// included. Nothing here touches staging (REQ-JE-4.13).
let private confirmNoPaymentReferencesEntry
    (context: Context.Context)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<unit, IAppError> =
    result {
        let! lines = journalEntryHeaderId |> JournalEntryLine.fetchByJournalEntryHeaderId context
        let! payments = lines |> List.map JournalEntryLine.journalEntryLineId |> Payment.fetchByJournalEntryLineIdList context
        return!
            match payments with
            | [] -> Ok ()
            | _ ->
                let paymentUuids = payments |> List.map (Payment.paymentId >> CashFlowComponent.PaymentId.value)
                CashFlowError.error(
                    CashFlowError.CashflowPaymentsReferenceEntryBeingVoided(
                        paymentUuids, journalEntryHeaderId |> JournalEntryHeaderId.value))
    }

let voidJournalEntry
    (context: Context.Context)
    (secondaryJournalEntryIdForComment: JournalEntryHeaderId option)
    (commentText: CommentText)
    (journalEntryHeaderId: JournalEntryHeaderId)
    : Result<JournalEntry, IAppError> =
    result {
        do! journalEntryHeaderId |> confirmJournalEntryIdIsReal context // validate here so the error message is helpful
        do! journalEntryHeaderId |> confirmNoPaymentReferencesEntry context
        do! insertReason context journalEntryHeaderId secondaryJournalEntryIdForComment commentText
        do!
            journalEntryHeaderId
            |> voidById context
            |> whenNoRows (JournalEntryVoidingNoOp(journalEntryHeaderId |> JournalEntryHeaderId.value))
        return! journalEntryHeaderId |> JournalEntryOrchestration.fetchById context
    }
