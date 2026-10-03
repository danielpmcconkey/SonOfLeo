module Business.CrossDomainOrchestration.JournalEntryVoiding

open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.CashFlow
open Business.CrossDomainOrchestration.JournalEntryOrchestration

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
    result {
        let! je =
            journalEntryHeaderId |> JournalEntryHeader.fetchById context
            |> whenNoRows (JournalEntryHeaderIdDoesntExist uuid)
        do! je |> confirmFiscalPeriodIsStillOpenBeforeVoiding context
        do! if je |> JournalEntryHeader.voidedAt |> Option.isNone then Ok () else Error(JournalEntryVoidingNoOp uuid)
        do!
            JournalEntryHeader.update context journalEntryHeaderId
                (SetTo(Some(context |> Context.getInitiationInstant)))
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
        do! journalEntryHeaderId |> voidById context
        return! journalEntryHeaderId |> JournalEntryOrchestration.fetchById context
    }
