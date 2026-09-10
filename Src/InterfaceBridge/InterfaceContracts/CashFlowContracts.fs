module InterfaceBridge.InterfaceContracts.CashFlowContracts

open System
open InterfaceBridge.InterfaceContracts.IngestionContracts
open NodaTime
open Utilities.FieldUpdate

// ****************************************
// Bi-directional contracts
// ****************************************

type BlockerContract =
    | NoFunds
    | Irresponsible
    | NeedsDecision of string
    | Other of string

type InvoiceLifeCycleStateContract = {
    invoiceState: string
    paymentState: string
    postedState: string
    blocker: BlockerContract option
}

type TransactionPointerContract =
    | Posted of Guid
    | Staged of Guid

type MonthDayContract =
    | DateInMonth of int
    | NthWeekDay of int * string
    | Last

type CadenceTypeContract =
    | Daily
    | Weekly of string
    | EveryOtherWeek of string
    | Monthly of MonthDayContract
    | Annually of string * MonthDayContract

type CadenceContract = {
    cadenceType: CadenceTypeContract
    nextInstance: LocalDate
}

// ****************************************
// Return
// ****************************************

type PaymentReturn = {
    paymentId: Guid
    invoiceId: Guid
    transactionPointer: TransactionPointerContract
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
    invoiceLifeCycleState: InvoiceLifeCycleStateContract
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

type MasterAgreementReturn = {
    agreementId: Guid
    agreementName: string
    direction: string
    cadence: CadenceContract
    counterparty: string
    activeBegin: LocalDate
    activeEnd: LocalDate option
    memo: string option
    createdAt: Instant
    modifiedAt: Instant
}

type PaymentAgreementReturn = {
    paymentAgreementId: Guid
    masterAgreementName: string
    paymentAgreementName: string
    debitAccountCode: string
    creditAccountCode: string
    expectedAmount: decimal option
    daysDueAfterInvoiceDate: int option
    memo: string option
    createdAt: Instant
    modifiedAt: Instant
}

type AgreementReturn = {
    masterAgreement: MasterAgreementReturn
    paymentAgreements: PaymentAgreementReturn list
    instances: InstanceReturn list
    invoices: InvoiceReturn list
    payments: PaymentReturn list
}

type PaymentAgreementLinkReturn = {
    paymentAgreementLinkId: Guid
    paymentAgreementName: string
    stageEntryLineId: Guid
    createdAt: Instant
    modifiedAt: Instant
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

type CreatePaymentFieldsInput = {
    transactionPointer: TransactionPointerContract
    amount: decimal
    postedToFiDate: LocalDate option
    postedToLedgerDate: LocalDate option
    memo: string option
}

type CreateInvoiceFieldsInput = {
    paymentAgreementName: string
    externalInvoiceId: string option
    invoiceDate: LocalDate
    dueDate: LocalDate
    amount: decimal
    invoiceLifeCycleState: InvoiceLifeCycleStateContract
    memo: string option
    payments: CreatePaymentFieldsInput list
}

type CreateInstanceInput = {
    masterAgreementName: string
    instanceDate: LocalDate
    isFulfilled: bool
    invoices: CreateInvoiceFieldsInput list
}

type CreateInvoiceInput = {
    instanceId: Guid
    invoice: CreateInvoiceFieldsInput
}

type UpdateInvoiceInput = {
    invoiceId: Guid
    externalInvoiceIdUpdate: FieldUpdate<string option>
    invoiceDateUpdate: FieldUpdate<LocalDate>
    dueDateUpdate: FieldUpdate<LocalDate>
    amountUpdate: FieldUpdate<decimal>
    invoiceStateUpdate: FieldUpdate<string>
    paymentStateUpdate: FieldUpdate<string>
    postedStateUpdate: FieldUpdate<string>
    blockerUpdate: FieldUpdate<BlockerContract option>
    memoUpdate: FieldUpdate<string option>
}

type CreatePaymentInput = {
    invoiceId: Guid
    payment: CreatePaymentFieldsInput
}

type CreatePaymentAgreementLinkInput = {
    paymentAgreementName: string
    stageEntryLineId: Guid
}

type UpdatePaymentAgreementLinkInput = {
    paymentAgreementLinkId: Guid
    paymentAgreementNameUpdate: FieldUpdate<string>
}

type DeletePaymentAgreementLinkInput = { paymentAgreementLinkId: Guid }

type CreatePaymentAgreementFieldsInput = {
    paymentAgreementName: string
    debitAccountCode: string
    creditAccountCode: string
    expectedAmount: decimal option
    daysDueAfterInvoiceDate: int option
    memo: string option
}

type CreateAgreementInput = {
    agreementName: string
    direction: string
    cadence: CadenceContract
    counterparty: string
    activeBegin: LocalDate
    activeEnd: LocalDate option
    memo: string option
    paymentAgreements: CreatePaymentAgreementFieldsInput list
}

type UpdateAgreementInput = {
    agreementName: string
    agreementNameUpdate: FieldUpdate<string>
    directionUpdate: FieldUpdate<string>
    cadenceUpdate: FieldUpdate<CadenceContract>
    counterpartyUpdate: FieldUpdate<string>
    activeBeginUpdate: FieldUpdate<LocalDate>
    activeEndUpdate: FieldUpdate<LocalDate option>
    memoUpdate: FieldUpdate<string option>
}
