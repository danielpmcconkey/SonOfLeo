module Model.CashFlow.Payment

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime

type Payment = {
    paymentId: PaymentId
    invoiceId: InvoiceId
    transactionPointer: TransactionPointer
    amount: Money // not separately tracked in the database; here for read convenience
    postedToFiDate: LocalDate option // the date the payment hit the actual external account
    postedToLedgerDate: LocalDate option // not separately tracked in the database; here for read convenience
    memo: TransactionMemo option
    createdAt: Instant
    modifiedAt: Instant
}
