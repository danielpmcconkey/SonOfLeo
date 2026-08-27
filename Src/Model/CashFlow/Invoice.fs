module Model.CashFlow.Invoice

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime

type Invoice = {
    invoiceId: InvoiceId
    instanceId: InstanceId
    paymentAgreementId: PaymentAgreementId
    invoiceDate: LocalDate
    dueDate: LocalDate
    amount: Money
    invoiceLifeCycleState: InvoiceLifeCycleState
    memo: TransactionMemo option
    createdAt: Instant
    modifiedAt: Instant
}
