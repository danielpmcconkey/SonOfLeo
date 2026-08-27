module Model.CashFlow.PaymentAgreement

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime

type PaymentAgreement = {
    paymentAgreementId: PaymentAgreementId
    masterAgreementID: MasterAgreementId
    debitAccount: DebitAccount
    creditAccount: CreditAccount
    expectedAmount: Money option
    memo: TransactionMemo option
    createdAt: Instant
    modifiedAt: Instant
}

