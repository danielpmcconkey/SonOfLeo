module Tests.Isolated.Model.Positions.BalanceAccounts

open NodaTime
open Business.CrossDomainOrchestration
open Xunit

[<Fact>]
let ``REQ-POS-16.1 REQ-SYS-1.1 for each of an empty string and a whitespace-only string, a Balance Account name is rejected with a typed empty-name error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-16.1 a Balance Account name of exactly 100 characters is accepted and one of 101 characters is rejected with a typed too-long error`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-16.1 REQ-SYS-1.1 a Balance Account name with leading and trailing whitespace holds the trimmed text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-16.2 each of Cash and Debt parses to a distinct kind that writes back as the same text, and 'cash' and 'Loan' are rejected with a typed error carrying the text`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-16.3 a Balance Account institution of an empty string or whitespace only is rejected with a typed empty error, one of 101 characters with a typed too-long error, and one of 100 characters is accepted`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-16.6 a Balance Account's balance on a date is the amount of its latest Balance dated on or before that date, and a Balance dated after it is ignored`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-POS-16.6 a Balance Account whose only Balance is dated after the date has no balance on it, and one whose latest Balance is 0.00 has a balance of 0.00`` () =
    Assert.Fail "Not yet implemented"

[<Fact>]
let ``REQ-RPT-8.1 when no fiscal period exists, net worth accepts any date`` () =
    for date in [ LocalDate(1990, 1, 1); LocalDate(2025, 12, 31); LocalDate(2099, 12, 31) ] do
        match NetWorth.isWithinFiscalPeriod [] date with
        | Ok withinOne -> Assert.False(withinOne, $"{date} is within no fiscal period")
        | Error e -> Assert.Fail $"{date} was rejected: {e.ToMessage()}"

