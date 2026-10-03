module Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

open System
open NodaTime
open App.Utility.FieldUpdate

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

type PaymentPostingTransitionReturn = {
    paymentId: Guid
    agreementName: string
    invoiceAmount: decimal
    journalEntryLineId: Guid
}

type ProjectedInvoiceReturn = {
    invoiceId: Guid
    agreementName: string
    direction: string
    dueDate: LocalDate
    amount: decimal
    outstandingAmount: decimal
}

type ProjectedAccountReturn = {
    accountCode: string
    accountName: string
    currentBalance: decimal
    knownInflows: decimal
    knownOutflows: decimal
    projectedLow: decimal
    invoices: ProjectedInvoiceReturn list
}

type BillToChaseReturn = {
    instanceId: Guid
    agreementName: string
    paymentAgreementName: string
    instanceDate: LocalDate
    cadenceType: CadenceTypeContract
}

type CashFlowProjectionReturn = {
    accounts: ProjectedAccountReturn list
    billsToChase: BillToChaseReturn list
}

// ****************************************
// Input
// ****************************************

type CreateUpcomingInstancesInput = { projectionHorizonInDays: int }

type ProjectCashFlowInput = { projectionHorizonInDays: int }

// a payment names its line only: its amount is the line's (REQ-CF-6.5)
type CreatePaymentFieldsInput = {
    transactionPointer: TransactionPointerContract
    postedToFiDate: LocalDate option
    postedToLedgerDate: LocalDate option
    memo: string option
}

// an invoice names only its invoice state and blocker: payment state, posted state and the instance's is-fulfilled are
// always derived from the payments, so no contract carries them
type NewInvoiceFieldsInput = {
    paymentAgreementName: string
    externalInvoiceId: string option
    invoiceDate: LocalDate
    dueDate: LocalDate
    amount: decimal
    invoiceState: string
    blocker: BlockerContract option
    memo: string option
    payments: CreatePaymentFieldsInput list
}

type CreateInstanceInput = {
    masterAgreementName: string
    instanceDate: LocalDate
    invoices: NewInvoiceFieldsInput list
}

type CreateInvoiceInput = {
    instanceId: Guid
    invoice: NewInvoiceFieldsInput
}

type UpdateInvoiceInput = {
    invoiceId: Guid
    externalInvoiceIdUpdate: FieldUpdate<string option>
    invoiceDateUpdate: FieldUpdate<LocalDate>
    dueDateUpdate: FieldUpdate<LocalDate>
    amountUpdate: FieldUpdate<decimal>
    invoiceStateUpdate: FieldUpdate<string>
    blockerUpdate: FieldUpdate<BlockerContract option>
    memoUpdate: FieldUpdate<string option>
}

type CreatePaymentInput = {
    invoiceId: Guid
    payment: CreatePaymentFieldsInput
}

type DeletePaymentInput = { paymentId: Guid }

type FetchAgreementSummaryInput = { agreementName: string }

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

/// UpdatePaymentAgreementInput names an existing Payment Agreement of the agreement being updated, by name, and the
/// fields to change on it. Accounts are given by account code.
type UpdatePaymentAgreementInput = {
    paymentAgreementName: string
    paymentAgreementNameUpdate: FieldUpdate<string>
    debitAccountCodeUpdate: FieldUpdate<string>
    creditAccountCodeUpdate: FieldUpdate<string>
    expectedAmountUpdate: FieldUpdate<decimal option>
    daysDueAfterInvoiceDateUpdate: FieldUpdate<int option>
    memoUpdate: FieldUpdate<string option>
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
    paymentAgreementUpdates: UpdatePaymentAgreementInput list
    newPaymentAgreements: CreatePaymentAgreementFieldsInput list
}
