module Model.CashFlow.Instance

open Model.CashFlow.CashFlowComponent
open NodaTime

type Instance = private {
    instanceId: InstanceId
    masterAgreementID: MasterAgreementId
    instanceDate: LocalDate
    isFulfilled: bool
    createdAt: Instant
    modifiedAt: Instant
}

