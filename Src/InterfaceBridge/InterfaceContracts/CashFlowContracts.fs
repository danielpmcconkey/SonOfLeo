module InterfaceBridge.InterfaceContracts.CashFlowContracts

open System
open InterfaceBridge.InterfaceContracts.IngestionContracts
open NodaTime

// ****************************************
// Return
// ****************************************

type BlockerReturn =
    | NoFunds
    | Irresponsible
    | NeedsDecision of string
    | Other of string

type InvoiceLifeCycleStateReturn = {
    invoiceState: string
    paymentState: string
    postedState: string
    blocker: BlockerReturn option
}

type TransactionPointerReturn =
    | Posted of Guid
    | Staged of Guid

type PaymentReturn = {
    paymentId: Guid
    invoiceId: Guid
    transactionPointer: TransactionPointerReturn
    amount: decimal
    postedToFiDate: LocalDate option
    postedToLedgerDate: LocalDate option
    memo: string option
    createdAt: Instant
    modifiedAt: Instant
}

type InvoiceReturn = {
    invoiceId: Guid
    instanceId: Guid
    paymentAgreementName: string
    externalInvoiceId: string option
    invoiceDate: LocalDate
    dueDate: LocalDate
    amount: decimal
    invoiceLifeCycleState: InvoiceLifeCycleStateReturn
    memo: string option
    createdAt: Instant
    modifiedAt: Instant
}

type InvoiceCompositeReturn = {
    invoice: InvoiceReturn
    payments: PaymentReturn list
}

type InstanceReturn = {
    instanceId: Guid
    masterAgreementName: string
    instanceDate: LocalDate
    isFulfilled: bool
    createdAt: Instant
    modifiedAt: Instant
}

type InstanceCompositeReturn = {
    instance: InstanceReturn
    invoiceComposites: InvoiceCompositeReturn list
}

type PaymentAgreementDecisionReturn = {
    stageEntryLineId: Guid
    paymentAgreementName: string option
    ruleIds: Guid list
    outcome: string
}

type InvoiceDecisionOutcomeReturn =
    | PaymentCreated of Guid
    | ManyCandidateEntries of Guid list
    | Overpayment

type InvoiceDecisionReturn = {
    invoiceId: Guid
    outcome: InvoiceDecisionOutcomeReturn
}

type PaymentAgreementClassificationResultReturn = {
    runId: Guid
    classificationResults: ClassificationResultReturn list
    decisionLog: PaymentAgreementDecisionReturn list
    invoiceDecisionLog: InvoiceDecisionReturn list
    openInstances: InstanceCompositeReturn list
}

// ****************************************
// Input
// ****************************************

type CreateUpcomingInstancesInput = { projectionHorizonInDays: int }
