module Business.FinancialServices.CashFlow.CashFlowComponent

open System
open NodaTime
open App.Utility.IAppError
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow.CashFlowError

type MasterAgreementId = private MasterAgreementId of Guid
module MasterAgreementId =
    let create () : MasterAgreementId = MasterAgreementId(Guid.NewGuid())
    let fromGuid g = MasterAgreementId g
    let value (MasterAgreementId g) : Guid = g

type InstanceId = private InstanceId of Guid
module InstanceId =
    let create () : InstanceId = InstanceId(Guid.NewGuid())
    let fromGuid g = InstanceId g
    let value (InstanceId g) : Guid = g

type PaymentId = private PaymentId of Guid
module PaymentId =
    let create () : PaymentId = PaymentId(Guid.NewGuid())
    let fromGuid g = PaymentId g
    let value (PaymentId g) : Guid = g

type PaymentAgreementId = private PaymentAgreementId of Guid
module PaymentAgreementId =
    let create () : PaymentAgreementId = PaymentAgreementId(Guid.NewGuid())
    let fromGuid g = PaymentAgreementId g
    let value (PaymentAgreementId g) : Guid = g

type PaymentAgreementLinkId = private PaymentAgreementLinkId of Guid
module PaymentAgreementLinkId =
    let create () : PaymentAgreementLinkId = PaymentAgreementLinkId(Guid.NewGuid())
    let fromGuid g = PaymentAgreementLinkId g
    let value (PaymentAgreementLinkId g) : Guid = g

type InvoiceId = private InvoiceId of Guid
module InvoiceId =
    let create () : InvoiceId = InvoiceId(Guid.NewGuid())
    let fromGuid g = InvoiceId g
    let value (InvoiceId g) : Guid = g

type DebitAccount = DebitAccount of AccountId
type CreditAccount = CreditAccount of AccountId

type FlowDirection =
    | Income
    | Outgo
    
module FlowDirection =
    let fromString str =
        match str with
        | "Income" -> Ok Income
        | "Outgo" -> Ok Outgo
        | _ -> error (CashflowInvalidFlowDirection str)
    let toString fd =
        match fd with
        | Income -> "Income"
        | Outgo -> "Outgo"

type InvoiceState =
    | InvoiceGenerated
    | InvoiceSent
    | InvoiceExpected
    | InvoiceReceived

module InvoiceState =
    let isValidFlowDirectionInvoiceStateCombination
        (flowDirection: FlowDirection)
        (invoiceState: InvoiceState)
        : bool =
        let validWith =
            match flowDirection with
            | Income -> [InvoiceGenerated; InvoiceSent]
            | Outgo -> [InvoiceExpected; InvoiceReceived]
        validWith |> List.contains invoiceState
    let fromString str =
        match str with
        | "InvoiceGenerated" -> Ok InvoiceGenerated
        | "InvoiceSent" -> Ok InvoiceSent
        | "InvoiceExpected" -> Ok InvoiceExpected
        | "InvoiceReceived" -> Ok InvoiceReceived
        | _ -> error (CashflowInvalidInvoiceState str)
    let toString state =
        match state with
        | InvoiceGenerated -> "InvoiceGenerated"
        | InvoiceSent -> "InvoiceSent"
        | InvoiceExpected -> "InvoiceExpected"
        | InvoiceReceived -> "InvoiceReceived"

type PaymentState =
    | NotYetPaid
    | PartiallyPaid
    | FullyPaid

module PaymentState =
    let fromString str =
        match str with
        | "NotYetPaid" -> Ok NotYetPaid
        | "PartiallyPaid" -> Ok PartiallyPaid
        | "FullyPaid" -> Ok FullyPaid
        | _ -> error (CashflowInvalidPaymentState str)
    let toString state =
        match state with
        | NotYetPaid -> "NotYetPaid"
        | PartiallyPaid -> "PartiallyPaid"
        | FullyPaid -> "FullyPaid"

type PostedState =
    | NotHandled
    | PartiallyPosted
    | PostedToLedger

module PostedState =
    let fromString str =
        match str with
        | "NotHandled" -> Ok NotHandled
        | "PartiallyPosted" -> Ok PartiallyPosted
        | "PostedToLedger" -> Ok PostedToLedger
        | _ -> error (CashflowInvalidPostedState str)
    let toString state =
        match state with
        | NotHandled -> "NotHandled"
        | PartiallyPosted -> "PartiallyPosted"
        | PostedToLedger -> "PostedToLedger"

type BlockerNote = private BlockerNote of string

module BlockerNote =
    let maxLength = 500
    let value (BlockerNote an) = an
    let create (raw: string) : Result<BlockerNote, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowBlockerNoteIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowBlockerNoteTooLong(raw, maxLength))
        else
            Ok(BlockerNote trimmed)

type Blocker =
    | NoFunds
    | Irresponsible
    | NeedsDecision of BlockerNote
    | Other of BlockerNote

module Blocker =
    let toString b =
        match b with
        | NoFunds -> "NoFunds"
        | Irresponsible -> "Irresponsible"
        | NeedsDecision note -> $"NeedsDecision: {note}"
        | Other note -> $"Other: {note}"
    
type InvoiceLifeCycleState = {
    invoiceState: InvoiceState
    paymentState: PaymentState
    postedState: PostedState
    blocker: Blocker option
}

type AgreementName = private AgreementName of string

module AgreementName =
    let maxLength = 100
    let value (AgreementName an) = an 
    let create (raw: string) : Result<AgreementName, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowAgreementNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowAgreementNameTooLong(raw, maxLength))
        else
            Ok(AgreementName trimmed)

type PaymentAgreementName = private PaymentAgreementName of string

module PaymentAgreementName =
    let maxLength = 250
    let value (PaymentAgreementName pan) = pan
    let create (raw: string) : Result<PaymentAgreementName, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowPaymentAgreementNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowPaymentAgreementNameTooLong(raw, maxLength))
        else
            Ok(PaymentAgreementName trimmed)

type Counterparty = private Counterparty of string

module Counterparty =
    let maxLength = 250
    let value (Counterparty cp) = cp
    let create (raw: string) : Result<Counterparty, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowCounterpartyIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowCounterpartyTooLong(raw, maxLength))
        else
            Ok(Counterparty trimmed)

type ExternalInvoiceId = private ExternalInvoiceId of string

module ExternalInvoiceId =
    let maxLength = 100
    let value (ExternalInvoiceId eid) = eid
    let create (raw: string) : Result<ExternalInvoiceId, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowExternalInvoiceIdIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowExternalInvoiceIdTooLong(raw, maxLength))
        else
            Ok(ExternalInvoiceId trimmed)

type AgreementMemo = private AgreementMemo of string

module AgreementMemo =
    let maxLength = 2000
    let value (AgreementMemo cp) = cp
    let create (raw: string) : Result<AgreementMemo, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowAgreementMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowAgreementMemoTooLong(raw, maxLength))
        else
            Ok(AgreementMemo trimmed)

type PaymentAgreementMemo = private PaymentAgreementMemo of string

module PaymentAgreementMemo =
    let maxLength = 2000
    let value (PaymentAgreementMemo cp) = cp
    let create (raw: string) : Result<PaymentAgreementMemo, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowPaymentAgreementMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowPaymentAgreementMemoTooLong(raw, maxLength))
        else
            Ok(PaymentAgreementMemo trimmed)

type InvoiceMemo = private InvoiceMemo of string

module InvoiceMemo =
    let maxLength = 2000
    let value (InvoiceMemo cp) = cp
    let create (raw: string) : Result<InvoiceMemo, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowInvoiceMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowInvoiceMemoTooLong(raw, maxLength))
        else
            Ok(InvoiceMemo trimmed)

type PaymentMemo = private PaymentMemo of string

module PaymentMemo =
    let maxLength = 2000
    let value (PaymentMemo cp) = cp
    let create (raw: string) : Result<PaymentMemo, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowPaymentMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowPaymentMemoTooLong(raw, maxLength))
        else
            Ok(PaymentMemo trimmed)

type TransactionPointer =
    | Posted of JournalEntryLineId
    | Staged of StageEntryLineId

type ProjectionHorizonInDays = private {days: int}

module ProjectionHorizonInDays =
    let min = 1
    let max = 365
    let value (h: ProjectionHorizonInDays) : int = h.days
    let create (raw: int) : Result<ProjectionHorizonInDays, IAppError> =
        match raw with
        | x when x > max -> Error(CashflowProjectionHorizonInDaysExceededMax(raw, max))
        | x when x < min -> Error(CashflowProjectionHorizonInDaysBelowMin(raw, min))
        | _ -> Ok({days = raw})

type DaysDueAfterInvoiceDate = private {daysAfter: int}

module DaysDueAfterInvoiceDate =
    let min = 0
    let max = 365
    let value (d: DaysDueAfterInvoiceDate) : int = d.daysAfter
    let create (raw: int) : Result<DaysDueAfterInvoiceDate, IAppError> =
        match raw with
        | x when x > max -> Error(CashflowDaysDueAfterInvoiceDateExceededMax(raw, max))
        | x when x < min -> Error(CashflowDaysDueAfterInvoiceDateBelowMin(raw, min))
        | _ -> Ok({daysAfter = raw})

type InvoiceDate = { localDate: LocalDate }
type DueDate = { localDate: LocalDate }
type PostedToFiDate = { localDate: LocalDate }
type PostedToLedgerDate = { localDate: LocalDate }
type InvoiceAmount = { money: Money.Money }
type PaymentAmount = { money: Money.Money }

type InvoiceDecisionOutcome =
    | PaymentCreated of StageEntryLineId
    | ManyCandidateEntries of StageEntryLineId list
    | Overpayment

type InvoiceDecision = {
    invoiceId: InvoiceId
    outcome: InvoiceDecisionOutcome
}

type PaymentPostingTransition = {
    paymentId: PaymentId
    agreementName: AgreementName
    invoiceAmount: InvoiceAmount
    journalEntryLineId: JournalEntryLineId
}

type ProjectedInvoice = {
    invoiceId: InvoiceId
    agreementName: AgreementName
    direction: FlowDirection
    dueDate: DueDate
    amount: InvoiceAmount
}

type ProjectedAccount = {
    accountId: AccountId
    accountCode: AccountCode
    accountName: AccountName
    currentBalance: Money.Money
    knownInflows: Money.Money
    knownOutflows: Money.Money
    projectedLow: Money.Money
    invoices: ProjectedInvoice list
}

type BillToChase = {
    instanceId: InstanceId
    agreementName: AgreementName
    paymentAgreementName: PaymentAgreementName
    instanceDate: LocalDate
    cadenceType: Cadence.CadenceType
}

type CashFlowProjection = {
    accounts: ProjectedAccount list
    billsToChase: BillToChase list
}
