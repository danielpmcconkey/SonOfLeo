module Tests.Integrated.CrossDomainOrchestration.SweepBehaviour

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type SweepBehaviourTests(fixture: TestDataFixture) =

    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(365)>]
    member _.``REQ-CF-7.1 REQ-CF-7.3 for each of 1 and 365, a sweep with that horizon gives a Daily agreement whose next-instance date is today an Instance on today plus the horizon and none on the day after`` (horizon: int) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement whose end date is before today gets no Instance, even for missed cadence dates on or before that end date, while an active agreement in the same sweep does`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement whose end date is today and whose next-instance date is today gets an Instance dated today`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement whose start date is today and whose next-instance date is today gets an Instance dated today`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement with no end date whose start date has passed gets Instances through the horizon end`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.3 REQ-CF-7.6 an agreement whose next-instance date is in the past gets an Instance on exactly the cadence dates from that date through the horizon end, missed ones included, and none before it even when on or after its start date`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Weekly")>]
    [<InlineData("MonthlyDateInMonth")>]
    [<InlineData("MonthlyNthWeekDay")>]
    [<InlineData("MonthlyLast")>]
    [<InlineData("Annually")>]
    member _.``REQ-CF-7.3 for each of Weekly, Monthly date-in-month, Monthly nth-weekday, Monthly Last and Annually, the sweep creates an Instance on exactly the dates the rule yields from the next-instance date through the horizon end`` (cadence: string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.4 an EveryOtherWeek agreement whose start date is on the off week of its next-instance date gets Instances every 14 days from the next-instance date through the horizon end, and none on the weeks between`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.6 REQ-CF-4.8 after the sweep, a Weekly agreement's next-instance date is its first cadence date after the horizon end, not the day after the horizon end`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.7 REQ-CF-7.12 a second sweep with the same horizon on the same day creates no Instance or Invoice and leaves every Instance, Invoice, Payment and next-instance date exactly as the first left it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.7 a sweep with a longer horizon after a shorter one on the same day creates exactly the cadence dates after the shorter horizon end through the longer one`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.8 for each Instance the sweep creates, each leg with an expected amount and a days-due value gets one Invoice for the expected amount, dated the Instance date and due that many days later`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.8 an Instance that existed before the sweep gets no Invoice from it, while an Instance the same run creates for the same agreement gets one for each leg with an expected amount and a days-due value`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.9 a leg with no expected amount gets no Invoice on an Instance the sweep creates, while a sibling leg with both values does`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.9 a leg with no days-due value gets no Invoice on an Instance the sweep creates, while a sibling leg with both values does`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.10 in one sweep, every Invoice created for an Income agreement is InvoiceGenerated and every one for an Outgo agreement is InvoiceExpected`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.14 the sweep returns every Instance that is unfulfilled after it runs, with its Invoices and their Payments, including ones it did not create, and no fulfilled Instance`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.15 when an Instance fails to be created after other agreements' Instances and Invoices were already created in the run, none of them remains and no next-instance date has moved`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.15 when an Invoice fails to be created after other Instances and Invoices were already created in the run, none of them remains and no next-instance date has moved`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-7.16 an agreement whose end date falls inside the horizon, on a day that is not a cadence date, gets an Instance on its last cadence date before the end date and none after it`` () =
        Assert.Fail "not implemented"
