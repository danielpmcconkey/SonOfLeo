module Tests.Isolated.Model.CashFlow.MasterAgreementDataStates

open Xunit

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData(" \t  ")>]
let ``REQ-CF-2.4 for each of the empty string, a single space and a string of spaces and tabs, an agreement name is rejected with a typed error`` (text:string) =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.5 an agreement name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.7 for each of "Income" and "Outgo" the flow direction string converts to that direction, and for each of "income", "Sideways" and the empty string it is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.13 for each of the seven week-day names the string converts to that week day, and for each of "monday", "Funday" and the empty string it is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.14 for each of the twelve month names the string converts to that month, and for each of "january", "Smarch" and the empty string it is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.15 for each of 1 and 28 a date-in-month number is accepted, and for each of 0 and 29 it is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.16 for each of 1 and 4 a week-in-month number is accepted, and for each of 0 and 5 it is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Theory>]
[<InlineData("")>]
[<InlineData(" ")>]
[<InlineData(" \t  ")>]
let ``REQ-CF-2.18 for each of the empty string, a single space and a string of spaces and tabs, a counterparty is rejected with a typed error`` (text:string) =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.19 a counterparty of exactly 250 characters is accepted and one of 251 characters is rejected with a typed error`` () =
    Assert.Fail "not implemented"

[<Theory>]
[<InlineData("Weekly")>]
[<InlineData("EveryOtherWeek")>]
[<InlineData("MonthlyDateInMonth")>]
[<InlineData("MonthlyNthWeekDay")>]
[<InlineData("MonthlyLast")>]
[<InlineData("Annually")>]
let ``REQ-CF-2.25 for each of Weekly, EveryOtherWeek, Monthly date-in-month, Monthly nth-weekday, Monthly Last and Annually, a next-instance date that fits the rule is accepted and one that does not is rejected with a typed error naming that date and the rule`` (cadence:string) =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.25 a Monthly Last cadence accepts 28 February in a common year and 29 February in a leap year, and rejects 28 February in a leap year`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.25 an Annually cadence rejects a next-instance date on the right day of the wrong month with a typed error naming the date and the rule`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-CF-2.25 a Daily cadence accepts a next-instance date on every day of a week that spans a month end, including the 29th, 30th and 31st`` () =
    Assert.Fail "not implemented"
