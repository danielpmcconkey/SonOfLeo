module Model.CashFlow.CashFlowComponent

open System
open Model.DataIngestion
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError

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

type PaymentState =
    | NotYetPaid
    | PartiallyPaid
    | FullyPaid

type PostedState =
    | NotHandled
    | IngestedToStage
    | PostedToLedger

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

type WeekDay =
    | Sunday
    | Monday
    | Tuesday
    | Wednesday
    | Thursday
    | Friday
    | Saturday

module WeekDay =
    let fromString str =
        match str with
        | "Sunday" -> Ok Sunday
        | "Monday" -> Ok Monday
        | "Tuesday" -> Ok Tuesday
        | "Wednesday" -> Ok Wednesday
        | "Thursday" -> Ok Thursday
        | "Friday" -> Ok Friday
        | "Saturday" -> Ok Saturday
        | _ -> Error (CashflowInvalidWeekDay str)
    let toString str =
        match str with
        | Sunday -> "Sunday"
        | Monday -> "Monday"
        | Tuesday -> "Tuesday"
        | Wednesday -> "Wednesday"
        | Thursday -> "Thursday"
        | Friday -> "Friday"
        | Saturday -> "Saturday"

type Month =
    | January
    | February
    | March
    | April
    | May
    | June
    | July
    | August
    | September
    | October
    | November
    | December

module Month =
    let toString m =
        match m with
        | January -> "January"
        | February -> "February"
        | March -> "March"
        | April -> "April"
        | May -> "May"
        | June -> "June"
        | July -> "July"
        | August -> "August"
        | September -> "September"
        | October -> "October"
        | November -> "November"
        | December -> "December"
    let fromString str =
        match str with
        | "January" -> Ok January
        | "February" -> Ok February
        | "March" -> Ok March
        | "April" -> Ok April
        | "May" -> Ok May
        | "June" -> Ok June
        | "July" -> Ok July
        | "August" -> Ok August
        | "September" -> Ok September
        | "October" -> Ok October
        | "November" -> Ok November
        | "December" -> Ok December
        | _ -> Error (CashflowInvalidMonth str)
    let toMonthNum m =        
        match m with
        | January -> 1
        | February -> 2
        | March -> 3
        | April -> 4
        | May -> 5
        | June -> 6
        | July -> 7
        | August -> 8
        | September -> 9
        | October -> 10
        | November -> 11
        | December -> 12
    let toAbbreviation m =
        match m with
        | January -> "Jan"
        | February -> "Feb"
        | March -> "Mar"
        | April -> "Apr"
        | May -> "May"
        | June -> "Jun"
        | July -> "Jul"
        | August -> "Aug"
        | September -> "Sep"
        | October -> "Oct"
        | November -> "Nov"
        | December -> "Dec"

type DateInMonthNumber = private DateInMonthNumber of int

module DateInMonthNumber =
    let value (DateInMonthNumber i) = i
    let fromInt i =
        if i > 0 && i < 32 then Ok (DateInMonthNumber i) else Error (CashflowInvalidDateInMonthNumber i)

type WeekInMonthNumber = private WeekInMonthNumber of int

module WeekInMonthNumber =
    let value (WeekInMonthNumber i) = i
    let fromInt i =
        if i > 0 && i < 6 then Ok (WeekInMonthNumber i) else Error (CashflowInvalidWeekInMonthNumber i)

type MonthDay =
    | DateInMonth of DateInMonthNumber
    | NthWeekDay of WeekInMonthNumber * WeekDay
    | Last

type Cadence =
    | Daily
    | Weekly of WeekDay
    | EveryOtherWeek of WeekDay
    | Monthly of MonthDay
    | Annually of Month * MonthDay

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
