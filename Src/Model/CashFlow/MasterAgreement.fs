module Model.CashFlow.MasterAgreement

open Model.CashFlow.CashFlowComponent
open Model.CashFlow.PaymentAgreement
open NodaTime



type Flow = {
    direction: FlowDirection
    expectedTransactions: PaymentAgreement list
}

type MasterAgreement = private {
    agreementID: MasterAgreementId
    agreementName: AgreementName
    flow: Flow
    cadence: Cadence
    counterparty: Counterparty
    startDate: LocalDate
    endDate: LocalDate option
    memo: AgreementMemo option
    createdAt: Instant
    modifiedAt: Instant
}

