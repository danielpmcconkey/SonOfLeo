module Model.CashFlow.CashFlowComponent

open System
open Model
open Model.Ledger.Accounts.AccountComponent
open Utilities.AppError

type AgreementId = private AgreementId of Guid
module AgreementId =
    let create () : AgreementId = AgreementId(Guid.NewGuid())
    let fromGuid g = AgreementId g
    let value (AgreementId g) : Guid = g

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

type TransactionMemo = private TransactionMemo of string

module TransactionMemo =
    let maxLength = 250
    let value (TransactionMemo cp) = cp
    let create (raw: string) : Result<TransactionMemo, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowTransactionMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowTransactionMemoTooLong(raw, maxLength))
        else
            Ok(TransactionMemo trimmed)

type ExpectedTransaction = {
    debitAccount: DebitAccount
    creditAccount: CreditAccount
    expectedAmount: Money option
    memo: TransactionMemo option
}

type Flow = {
    direction: FlowDirection
    expectedTransactions: ExpectedTransaction list
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
    let fromInt i =
        if i > 0 && i < 32 then Ok (DateInMonthNumber i) else Error (CashflowInvalidDateInMonthNumber i)
        
type WeekInMonthNumber = private WeekInMonthNumber of int

module WeekInMonthNumber =
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
    let maxLength = 250
    let value (AgreementMemo cp) = cp
    let create (raw: string) : Result<AgreementMemo, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(CashflowAgreementMemoIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(CashflowAgreementMemoTooLong(raw, maxLength))
        else
            Ok(AgreementMemo trimmed)
