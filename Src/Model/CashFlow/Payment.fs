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

let private transactionPointerFromColumns
    (journalEntryHeaderUuid: Guid option)
    (stageEntryHeaderUuid: Guid option)
    : Result<TransactionPointer, AppError> =
    // Once a payment is posted to the ledger, it carries both ids (the stage_entry_header_id it originated
    // from is preserved, not cleared) — journal_entry_header_id wins whenever it's present. Only "neither
    // id is set" is an error.
    match journalEntryHeaderUuid, stageEntryHeaderUuid with
    | Some journalEntryHeaderUuid, _ ->
        journalEntryHeaderUuid |> JournalEntryHeaderId.fromGuid |> CashFlowComponent.Posted |> Ok
    | None, Some stageEntryHeaderUuid ->
        stageEntryHeaderUuid |> StageEntryHeaderId.fromGuid |> CashFlowComponent.Staged |> Ok
    | None, None ->
        Error(
            CashflowInvalidPaymentTransactionPointerRow
                "neither journal_entry_header_id nor stage_entry_header_id was set; at least one must be set.")

(*
Payment.amount and Payment.postedToLedgerDate are not columns on cashflow.payment; they're derived at read time via a
select that joins to the journal_entry/journal_entry_line and staged_entry/staged_entry_line tables, pinned by
payment_agreement's debit/credit account and master_agreement's flow_direction so exactly one line matches per payment
(no aggregation — see CompoundedLearnings for why summing was deliberately rejected). fetchGenericRead below owns that
join; reconstitute only ever sees the already-computed "amount" and "posted_to_ledger_date" columns.
*)
let private reconstitute raw =
    result {
        let (uuid,
             invoiceUuid,
             journalEntryHeaderUuid,
             stageEntryHeaderUuid,
             amountDec,
             postedToFiDate,
             postedToLedgerDate,
             memoStr,
             createdAt,
             modifiedAt) =
            raw
        let paymentId = uuid |> PaymentId.fromGuid
        let invoiceId = invoiceUuid |> InvoiceId.fromGuid
        let! transactionPointer = transactionPointerFromColumns journalEntryHeaderUuid stageEntryHeaderUuid
        let! amount =
            match amountDec with
            | Some d -> d |> Money.fromDecimal
            | None ->
                Error(
                    CashflowInvalidPaymentAmountRow
                        "the computed amount column was null; no matching journal_entry_line/staged_entry_line was \
                         found for this payment's flow direction and account.")
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter PaymentMemo.create
        return
            create
                paymentId
                invoiceId
                transactionPointer
                amount
                postedToFiDate
                postedToLedgerDate
                memo
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "invoice_id"),
    (row |> RowReader.getUuidOption "journal_entry_header_id"),
    (row |> RowReader.getUuidOption "stage_entry_header_id"),
    (row |> RowReader.getNumericOption "amount"),
    (row |> RowReader.getDateOption "posted_to_fi_date"),
    (row |> RowReader.getDateOption "posted_to_ledger_date"),
    (row |> RowReader.getStringOption "memo"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let readRowsFromDb
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Payment list, AppError> =
    let from = "cashflow.payment pmt"
    let query = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchGenericRead
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Payment list, AppError> =
    let select = """
        pmt.unique_id, pmt.invoice_id, pmt.journal_entry_header_id, pmt.stage_entry_header_id, 
        case when je.unique_id is not null then jel.amount else sel.amount end as amount,
        pmt.posted_to_fi_date, je.entry_date as posted_to_ledger_date, pmt.memo, pmt.created_at, 
        pmt.modified_at
        """
    let join =
        [
            "left join cashflow.invoice inv on pmt.invoice_id = inv.unique_id"
            "left join cashflow.payment_agreement pa on inv.payment_agreement_id = pa.unique_id"
            "left join cashflow.master_agreement ma on pa.master_agreement_id = ma.unique_id"
            "left join ledger.journal_entry je on pmt.journal_entry_header_id = je.unique_id"
            """
            left join ledger.journal_entry_line jel
                on je.unique_id = jel.journal_entry_id
                and (case 
                        when ma.flow_direction = 'Income' then jel.account_id = pa.credit_account and jel.line_type = 'Credit'
                        when ma.flow_direction = 'Outgo' then jel.account_id = pa.debit_account and jel.line_type = 'Debit'
                    end)
            """
            "left join ingestion.staged_entry se on pmt.stage_entry_header_id = se.unique_id"
            """
            left join ingestion.staged_entry_line sel 
                on se.unique_id = sel.entry_id
                and (case 
                        when ma.flow_direction = 'Income' then sel.account_id = pa.credit_account and sel.line_type = 'Credit'
                        when ma.flow_direction = 'Outgo' then sel.account_id = pa.debit_account and sel.line_type = 'Debit'
                    end)
            """
        ]
    readRowsFromDb context None select (Some join) predicate limit None None parameters expectedRows
