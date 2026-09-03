module Model.CashFlow.CashFlowComponent

open System
open Model.Ledger.AccountComponent
open Model.Ledger.JournalEntryComponent
open NodaTime
open Utilities.AppError
open Model.DataIngestion.StageEntryComponent

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
        | _ -> Error (CashflowInvalidFlowDirection str)
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
        | _ -> Error (CashflowInvalidInvoiceState str)
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
        | _ -> Error (CashflowInvalidPaymentState str)
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
        | _ -> Error (CashflowInvalidPostedState str)
    let toString state =
        match state with
        | NotHandled -> "NotHandled"
        | PartiallyPosted -> "PartiallyPosted"
        | PostedToLedger -> "PostedToLedger"

type BlockerNote = private BlockerNote of string

module BlockerNote =
    let maxLength = 500
    let value (BlockerNote an) = an // required because BlockerNote is a private string
    let create (raw: string) : Result<BlockerNote, AppError> =
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
    let value (AgreementName an) = an // required because AgreementName is a private string
    let create (raw: string) : Result<AgreementName, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowAgreementNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowAgreementNameTooLong(raw, maxLength))
        else
            Ok(AgreementName trimmed)

type Counterparty = private Counterparty of string

module Counterparty =
    let maxLength = 250
    let value (Counterparty cp) = cp
    let create (raw: string) : Result<Counterparty, AppError> =
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
    let create (raw: string) : Result<ExternalInvoiceId, AppError> =
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
    let create (raw: string) : Result<AgreementMemo, AppError> =
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
    let create (raw: string) : Result<PaymentAgreementMemo, AppError> =
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
    let create (raw: string) : Result<InvoiceMemo, AppError> =
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
    let create (raw: string) : Result<PaymentMemo, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowPaymentMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowPaymentMemoTooLong(raw, maxLength))
        else
            Ok(PaymentMemo trimmed)

type TransactionPointer =
    | Posted of JournalEntryHeaderId
    | Staged of StageEntryHeaderId

type ProjectionHorizonInDays = private {days: int}

module ProjectionHorizonInDays =
    let min = 1
    let max = 365
    let value (h: ProjectionHorizonInDays) : int = h.days
    let create (raw: int) : Result<ProjectionHorizonInDays, AppError> =
        match raw with
        // todo: Simian create the right app errors 
        | x when x > max -> Error(MoneyFailedToConvertExceededMax(raw, max))
        | x when x < min -> Error(MoneyFailedToConvertBelowMin(raw, min))
        | _ -> Ok({days = raw})

type InvoiceDate = { localDate: LocalDate }
type DueDate = { localDate: LocalDate }
type PostedToFiDate = { localDate: LocalDate }
type PostedToLedgerDate = { localDate: LocalDate }
type InvoiceAmount = { money: Model.Money }
type PaymentAmount = { money: Model.Money }
