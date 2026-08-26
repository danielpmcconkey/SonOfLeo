module Model.CashFlow.Agreement

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime

type Agreement = private {
    agreementID: AgreementId
    agreementName: AgreementName
    flow: Flow
    cadence: Cadence
    counterparty: Counterparty
    startDate: LocalDate
    endDate: LocalDate option
    memo: AgreementMemo option
}
    
