module Tests.Isolated.Model.CashFlow.MasterAgreementDataStates

open System
open App.Utility.IAppError
open Business.General
open Business.General.BizGeneralError
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowError
open NodaTime
open Xunit

let private rejectedWith (isExpected: IAppError -> bool) (r: Result<'a, IAppError>) =
    match r with
    | Error e -> isExpected e
    | Ok _ -> false

let private cashFlowError (isExpected: CashFlowError -> bool) (e: IAppError) =
    match e with
    | AsError (x: CashFlowError) -> isExpected x
    | _ -> false

let private generalError (isExpected: BizGeneralError -> bool) (e: IAppError) =
    match e with
    | AsError (x: BizGeneralError) -> isExpected x
    | _ -> false

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private fitsCadence (cadenceType: Cadence.CadenceType) (date: LocalDate) =
    Cadence.create cadenceType { nextInstance = date }

// =========================================================================
// REQ-CF-2.4, 2.5, 2.18, 2.19 — agreement name and counterparty
// =========================================================================

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData(" \t  ")>]
let ``REQ-CF-2.4 for each of the empty string, a single space and a string of spaces and tabs, an agreement name is rejected with a typed error`` (text: string) =
    let rejected =
        AgreementName.create text
        |> rejectedWith (cashFlowError (function CashflowAgreementNameIsEmpty raw -> raw = text | _ -> false))
    Assert.True(rejected)

[<Fact>]
let ``REQ-CF-2.5 an agreement name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed error`` () =
    let hundred = String('n', 100)
    let accepted = AgreementName.create hundred |> Result.map AgreementName.value
    let rejected =
        AgreementName.create (hundred + "n")
        |> rejectedWith (cashFlowError (function CashflowAgreementNameTooLong (_, 100) -> true | _ -> false))
    Assert.Equal(Ok hundred, accepted)
    Assert.True(rejected)

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData(" \t  ")>]
let ``REQ-CF-2.18 for each of the empty string, a single space and a string of spaces and tabs, a counterparty is rejected with a typed error`` (text: string) =
    let rejected =
        Counterparty.create text
        |> rejectedWith (cashFlowError (function CashflowCounterpartyIsEmpty raw -> raw = text | _ -> false))
    Assert.True(rejected)

[<Fact>]
let ``REQ-CF-2.19 a counterparty of exactly 250 characters is accepted and one of 251 characters is rejected with a typed error`` () =
    let limit = String('c', 250)
    let accepted = Counterparty.create limit |> Result.map Counterparty.value
    let rejected =
        Counterparty.create (limit + "c")
        |> rejectedWith (cashFlowError (function CashflowCounterpartyTooLong (_, 250) -> true | _ -> false))
    Assert.Equal(Ok limit, accepted)
    Assert.True(rejected)

// =========================================================================
// REQ-CF-2.7, 2.13, 2.14, 2.15, 2.16 — the values a cadence and a direction are built from
// =========================================================================

[<Fact>]
let ``REQ-CF-2.7 for each of "Income" and "Outgo" the flow direction string converts to that direction, and for each of "income", "Sideways" and the empty string it is rejected with a typed error`` () =
    let converted = [ "Income"; "Outgo" ] |> List.map (FlowDirection.fromString >> orFail)
    let rejections =
        [ "income"; "Sideways"; "" ]
        |> List.map (fun raw ->
            FlowDirection.fromString raw
            |> rejectedWith (cashFlowError (function CashflowInvalidFlowDirection s -> s = raw | _ -> false)))
    Assert.Equal<FlowDirection list>([ Income; Outgo ], converted)
    Assert.DoesNotContain(false, rejections)

[<Fact>]
let ``REQ-CF-2.13 for each of the seven week-day names the string converts to that week day, and for each of "monday", "Funday" and the empty string it is rejected with a typed error`` () =
    let names = [ "Sunday"; "Monday"; "Tuesday"; "Wednesday"; "Thursday"; "Friday"; "Saturday" ]
    let converted = names |> List.map (Cadence.WeekDay.fromString >> orFail)
    let rejections =
        [ "monday"; "Funday"; "" ]
        |> List.map (fun raw ->
            Cadence.WeekDay.fromString raw
            |> rejectedWith (generalError (function InvalidWeekDay s -> s = raw | _ -> false)))
    Assert.Equal<Cadence.WeekDay list>(
        [ Cadence.Sunday; Cadence.Monday; Cadence.Tuesday; Cadence.Wednesday; Cadence.Thursday; Cadence.Friday
          Cadence.Saturday ],
        converted)
    Assert.DoesNotContain(false, rejections)

[<Fact>]
let ``REQ-CF-2.14 for each of the twelve month names the string converts to that month, and for each of "january", "Smarch" and the empty string it is rejected with a typed error`` () =
    let names =
        [ "January"; "February"; "March"; "April"; "May"; "June"; "July"; "August"; "September"; "October"
          "November"; "December" ]
    let converted = names |> List.map (Cadence.Month.fromString >> orFail >> Cadence.Month.toMonthNum)
    let rejections =
        [ "january"; "Smarch"; "" ]
        |> List.map (fun raw ->
            Cadence.Month.fromString raw
            |> rejectedWith (generalError (function InvalidMonth s -> s = raw | _ -> false)))
    Assert.Equal<int list>([ 1 .. 12 ], converted)
    Assert.DoesNotContain(false, rejections)

[<Fact>]
let ``REQ-CF-2.15 for each of 1 and 28 a date-in-month number is accepted, and for each of 0 and 29 it is rejected with a typed error`` () =
    let accepted = [ 1; 28 ] |> List.map (Cadence.DateInMonthNumber.fromInt >> orFail >> Cadence.DateInMonthNumber.value)
    let rejections =
        [ 0; 29 ]
        |> List.map (fun raw ->
            Cadence.DateInMonthNumber.fromInt raw
            |> rejectedWith (generalError (function InvalidDateInMonthNumber i -> i = raw | _ -> false)))
    Assert.Equal<int list>([ 1; 28 ], accepted)
    Assert.DoesNotContain(false, rejections)

[<Fact>]
let ``REQ-CF-2.16 for each of 1 and 4 a week-in-month number is accepted, and for each of 0 and 5 it is rejected with a typed error`` () =
    let accepted = [ 1; 4 ] |> List.map (Cadence.WeekInMonthNumber.fromInt >> orFail >> Cadence.WeekInMonthNumber.value)
    let rejections =
        [ 0; 5 ]
        |> List.map (fun raw ->
            Cadence.WeekInMonthNumber.fromInt raw
            |> rejectedWith (generalError (function InvalidWeekInMonthNumber i -> i = raw | _ -> false)))
    Assert.Equal<int list>([ 1; 4 ], accepted)
    Assert.DoesNotContain(false, rejections)

// =========================================================================
// REQ-CF-2.25 — the next-instance date fits the cadence
// =========================================================================

(* October 2026 starts on a Thursday: Monday the 5th, the second Tuesday is the 13th, the last day the 31st. 2028 is a
   leap year, so the last day of its February is the 29th and the 28th is not. *)
[<Theory>]
[<InlineData("Weekly")>]
[<InlineData("EveryOtherWeek")>]
[<InlineData("MonthlyDateInMonth")>]
[<InlineData("MonthlyNthWeekDay")>]
[<InlineData("MonthlyLast")>]
[<InlineData("Annually")>]
[<InlineData("AnnuallyNthWeekDay")>]
[<InlineData("AnnuallyFebruaryLast")>]
let ``REQ-CF-2.25 for each of Weekly, EveryOtherWeek, Monthly date-in-month, Monthly nth-weekday, Monthly Last, Annually date-in-month, Annually nth-weekday and Annually (February, Last), a next-instance date that fits the rule is accepted and one that does not is rejected with a typed error naming that date and the rule`` (cadence: string) =
    let fifteenth = 15 |> Cadence.DateInMonthNumber.fromInt |> orFail
    let first = 1 |> Cadence.DateInMonthNumber.fromInt |> orFail
    let second = 2 |> Cadence.WeekInMonthNumber.fromInt |> orFail
    let cadenceType, fits, doesNotFit, namesDateAndRule =
        match cadence with
        | "Weekly" ->
            Cadence.Weekly Cadence.Monday, LocalDate(2026, 10, 5), LocalDate(2026, 10, 6),
            (function CadenceDateNotOnWeekDay (d, rule) -> d = LocalDate(2026, 10, 6) && rule = "Monday" | _ -> false)
        | "EveryOtherWeek" ->
            Cadence.EveryOtherWeek Cadence.Monday, LocalDate(2026, 10, 5), LocalDate(2026, 10, 6),
            (function CadenceDateNotOnWeekDay (d, rule) -> d = LocalDate(2026, 10, 6) && rule = "Monday" | _ -> false)
        | "MonthlyDateInMonth" ->
            Cadence.Monthly(Cadence.DateInMonth fifteenth), LocalDate(2026, 10, 15), LocalDate(2026, 10, 16),
            (function CadenceDateNotOnDateInMonth (d, 15) -> d = LocalDate(2026, 10, 16) | _ -> false)
        | "MonthlyNthWeekDay" ->
            Cadence.Monthly(Cadence.NthWeekDay(second, Cadence.Tuesday)), LocalDate(2026, 10, 13), LocalDate(2026, 10, 20),
            (function
             | CadenceDateNotNthWeekDayInMonth (d, 2, rule) -> d = LocalDate(2026, 10, 20) && rule = "Tuesday"
             | _ -> false)
        | "MonthlyLast" ->
            Cadence.Monthly Cadence.Last, LocalDate(2026, 10, 31), LocalDate(2026, 10, 30),
            (function CadenceDateNotLastDayOfMonth d -> d = LocalDate(2026, 10, 30) | _ -> false)
        | "Annually" ->
            Cadence.Annually(Cadence.March, Cadence.DateInMonth first), LocalDate(2027, 3, 1), LocalDate(2027, 3, 2),
            (function
             | CadenceDateNotOnAnnualDate (d, monthDay, month) ->
                 d = LocalDate(2027, 3, 2) && monthDay = "day 1" && month = "March"
             | _ -> false)
        | "AnnuallyNthWeekDay" ->
            Cadence.Annually(Cadence.October, Cadence.NthWeekDay(second, Cadence.Tuesday)), LocalDate(2026, 10, 13),
            LocalDate(2026, 10, 20),
            (function
             | CadenceDateNotOnAnnualDate (d, monthDay, month) ->
                 d = LocalDate(2026, 10, 20) && monthDay = "Tuesday number 2" && month = "October"
             | _ -> false)
        | "AnnuallyFebruaryLast" ->
            Cadence.Annually(Cadence.February, Cadence.Last), LocalDate(2028, 2, 29), LocalDate(2028, 2, 28),
            (function
             | CadenceDateNotOnAnnualDate (d, monthDay, month) ->
                 d = LocalDate(2028, 2, 28) && monthDay = "the last day" && month = "February"
             | _ -> false)
        | other -> failwith $"no cadence {other}"
    let accepted = fitsCadence cadenceType fits |> Result.isOk
    let rejected = fitsCadence cadenceType doesNotFit |> rejectedWith (generalError namesDateAndRule)
    Assert.True(accepted)
    Assert.True(rejected)

(* The second Tuesday of October 2027 is the 12th (the 1st is a Friday). *)
[<Theory>]
[<InlineData("AnnuallyNthWeekDay")>]
[<InlineData("AnnuallyFebruaryLast")>]
let ``REQ-CF-4.8 for each of Annually nth-weekday and Annually (February, Last), the cadence date following an on-rule date is the rule's date a year on, from 28 February 2027 to 29 February 2028 for the last-day rule`` (cadence: string) =
    let second = 2 |> Cadence.WeekInMonthNumber.fromInt |> orFail
    let cadenceType, from, expected =
        match cadence with
        | "AnnuallyNthWeekDay" ->
            Cadence.Annually(Cadence.October, Cadence.NthWeekDay(second, Cadence.Tuesday)), LocalDate(2026, 10, 13),
            LocalDate(2027, 10, 12)
        | "AnnuallyFebruaryLast" -> Cadence.Annually(Cadence.February, Cadence.Last), LocalDate(2027, 2, 28), LocalDate(2028, 2, 29)
        | other -> failwith $"no cadence {other}"
    Assert.Equal(expected, Cadence.determineNextDateFromPrior from cadenceType)

[<Fact>]
let ``REQ-CF-2.25 a Monthly Last cadence accepts 28 February in a common year and 29 February in a leap year, and rejects 28 February in a leap year`` () =
    let last = Cadence.Monthly Cadence.Last
    let accepted = [ LocalDate(2027, 2, 28); LocalDate(2028, 2, 29) ] |> List.map (fitsCadence last >> Result.isOk)
    let rejected =
        fitsCadence last (LocalDate(2028, 2, 28))
        |> rejectedWith (generalError (function CadenceDateNotLastDayOfMonth d -> d = LocalDate(2028, 2, 28) | _ -> false))
    Assert.Equal<bool list>([ true; true ], accepted)
    Assert.True(rejected)

[<Fact>]
let ``REQ-CF-2.25 an Annually cadence rejects a next-instance date on the right day of the wrong month with a typed error naming the date and the rule`` () =
    let first = 1 |> Cadence.DateInMonthNumber.fromInt |> orFail
    let rejected =
        fitsCadence (Cadence.Annually(Cadence.March, Cadence.DateInMonth first)) (LocalDate(2027, 4, 1))
        |> rejectedWith (generalError (function
            | CadenceDateNotOnAnnualDate (d, monthDay, month) -> d = LocalDate(2027, 4, 1) && monthDay = "day 1" && month = "March"
            | _ -> false))
    Assert.True(rejected)

[<Fact>]
let ``REQ-CF-2.25 a Daily cadence accepts a next-instance date on every day of a week that spans a month end, including the 29th, 30th and 31st`` () =
    let week = [ 0 .. 6 ] |> List.map (fun i -> LocalDate(2026, 10, 28).PlusDays(i))
    let refusals =
        week
        |> List.choose (fun d ->
            match fitsCadence Cadence.Daily d with
            | Ok _ -> None
            | Error e -> Some $"{d}: {e.DomainName}.{e.CaseName}")
    Assert.Contains(LocalDate(2026, 10, 31), week)
    Assert.Empty(refusals)

// =========================================================================
// REQ-CF-2.22 — Master Agreement memo
// =========================================================================

/// Fails unless `attempt` was refused with an error `isExpected` accepts, naming whatever came back instead.
let private expectRefusal (isExpected: IAppError -> bool) (attempt: Result<'a, IAppError>) =
    match attempt with
    | Error e when isExpected e -> ()
    | Error e -> Assert.Fail $"Wrong error. {e.DomainName}.{e.CaseName}: {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Fact>]
let ``REQ-CF-2.22 a Master Agreement memo of exactly 2000 characters is accepted and one of 2001 characters is rejected with a typed error naming the limit`` () =
    let atLimit = String('m', 2000)
    Assert.Equal(Ok atLimit, AgreementMemo.create atLimit |> Result.map AgreementMemo.value)
    AgreementMemo.create (atLimit + "m")
    |> expectRefusal (function
        | AsError (CashflowAgreementMemoTooLong(raw, limit)) -> raw = atLimit + "m" && limit = 2000
        | _ -> false)

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData(" \t  ")>]
let ``REQ-CF-2.22 for each of the empty string, a single space and a string of spaces and tabs, a Master Agreement memo is rejected with a typed error`` (text: string) =
    AgreementMemo.create text
    |> expectRefusal (function
        | AsError (CashflowAgreementMemoIsEmpty raw) -> raw = text
        | _ -> false)
