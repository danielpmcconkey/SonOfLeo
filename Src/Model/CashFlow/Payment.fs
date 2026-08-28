module Model.CashFlow.Payment

open System
open Model
open Model.CashFlow.CashFlowComponent
open Model.DataIngestion
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

type Payment = private {
    paymentId: PaymentId
    invoiceId: InvoiceId
    transactionPointer: TransactionPointer
    amount: Money // not separately tracked in the database; here for read convenience
    postedToFiDate: LocalDate option // the date the payment hit the actual external account
    postedToLedgerDate: LocalDate option // not separately tracked in the database; here for read convenience
    memo: PaymentMemo option
    createdAt: Instant
    modifiedAt: Instant
}

type PaymentFieldUpdates = {
    paymentIdToUpdate: PaymentId
    transactionPointerUpdate: FieldUpdate<TransactionPointer>
    postedToFiDateUpdate: FieldUpdate<LocalDate option>
    memoUpdate: FieldUpdate<PaymentMemo option>
}

let paymentId p = p.paymentId
let invoiceId p = p.invoiceId
let transactionPointer p = p.transactionPointer
let amount p = p.amount
let postedToFiDate p = p.postedToFiDate
let postedToLedgerDate p = p.postedToLedgerDate
let memo p = p.memo
let createdAt p = p.createdAt
let modifiedAt p = p.modifiedAt

let create
    (paymentId: PaymentId)
    (invoiceId: InvoiceId)
    (transactionPointer: TransactionPointer)
    (amount: Money)
    (postedToFiDate: LocalDate option)
    (postedToLedgerDate: LocalDate option)
    (memo: PaymentMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Payment =
    { paymentId = paymentId
      invoiceId = invoiceId
      transactionPointer = transactionPointer
      amount = amount
      postedToFiDate = postedToFiDate
      postedToLedgerDate = postedToLedgerDate
      memo = memo
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private transactionPointerToColumns (transactionPointer: TransactionPointer) : Guid option * Guid option =
    match transactionPointer with
    | CashFlowComponent.Posted journalEntryHeaderId -> (journalEntryHeaderId |> JournalEntryHeaderId.value |> Some), None
    | CashFlowComponent.Staged stageEntryHeaderId -> None, (stageEntryHeaderId |> StageEntryHeaderId.value |> Some)

let insertNewToDb
    (context: Context.Context)
    (payment: Payment)
    : Result<unit, AppError> =
    result {
        let query =
            """
            insert into cashflow.payment(
	            unique_id, invoice_id, journal_entry_header_id, stage_entry_header_id, posted_to_fi_date, memo,
                created_at, modified_at)
            values (
	            @unique_id, @invoice_id, @journal_entry_header_id, @stage_entry_header_id, @posted_to_fi_date, @memo,
                @created_at, @modified_at);"""
        let uuid = payment.paymentId |> PaymentId.value
        let invoiceUuid = payment.invoiceId |> InvoiceId.value
        let journalEntryHeaderUuid, stageEntryHeaderUuid = payment.transactionPointer |> transactionPointerToColumns
        let memo = payment.memo |> Option.map PaymentMemo.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@invoice_id"; value = UniqueId(invoiceUuid) }
              { name = "@journal_entry_header_id"; value = NullableUniqueId(journalEntryHeaderUuid) }
              { name = "@stage_entry_header_id"; value = NullableUniqueId(stageEntryHeaderUuid) }
              { name = "@posted_to_fi_date"; value = NullableDbLocalDate(payment.postedToFiDate) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@created_at"; value = DbInstant(payment.createdAt) }
              { name = "@modified_at"; value = DbInstant(payment.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }

(*

Note to future me...

Payment.amount is derived. It's just complicated. The read query will need to join both the journal entry tables (header
and line) and the stage entry tables (header and line). Then, some sort of case statement that, when the je header id is
not null, source from the right je line. When the je header id is null, source from the right se line.

We're gonna have to do some gymnastics to ensure that we don't double the result set because a single JE or SE has at
least 2 lines. I don't know if that means trying to join on the account ID in the line or, aggregate sum of all debit or
credit lines, depending on flow direction. This needs discussion.
*)
