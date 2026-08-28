module Model.CashFlow.PaymentAgreement

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime

type PaymentAgreement = private {
    paymentAgreementId: PaymentAgreementId
    masterAgreementID: MasterAgreementId
    debitAccount: DebitAccount
    creditAccount: CreditAccount
    expectedAmount: Money option
    memo: PaymentAgreementMemo option
    createdAt: Instant
    modifiedAt: Instant
}

let paymentAgreementId p = p.paymentAgreementId
let masterAgreementID p = p.masterAgreementID
let debitAccount p = p.debitAccount
let creditAccount p = p.creditAccount
let expectedAmount p = p.expectedAmount
let memo p = p.memo
let createdAt p = p.createdAt
let modifiedAt p = p.modifiedAt

let create
    (paymentAgreementId: PaymentAgreementId)
    (masterAgreementID: MasterAgreementId)
    (debitAccount: DebitAccount)
    (creditAccount: CreditAccount)
    (expectedAmount: Money option)
    (memo: PaymentAgreementMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : PaymentAgreement =
    { paymentAgreementId = paymentAgreementId
      masterAgreementID = masterAgreementID
      debitAccount = debitAccount
      creditAccount = creditAccount
      expectedAmount = expectedAmount
      memo = memo
      createdAt = createdAt
      modifiedAt = modifiedAt }

