module Tests.Integrated.CrossDomainOrchestration.InvestmentWealthHistory

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InvestmentWealthHistoryTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-9.1 an end date earlier than the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.2 a range beginning mid-month and ending on a month-end gives exactly one point per month-end from the end of the begin month to the end date, in date order`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.2 a range beginning and ending inside one month, before its last day, gives no points`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.1 REQ-RPT-9.2 a range whose begin and end are the same month-end gives exactly that one point`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.2 a range spanning February gives a point on its last day, the 29th in a leap year and the 28th otherwise`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by account, each point's per-account totals and grand total equal the hand-summed market values of the holdings as of that month-end`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by account group, accounts whose labels match exactly share one total and accounts whose labels differ only by letter case are totalled separately`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.2 grouped by tax treatment, each point gives one total per tax treatment present, equal to the hand-summed market values of that treatment's accounts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.3 grouped by owners, a jointly owned account is totalled under its complete owner set and not under either owner's single-owner group`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.3 for each of the seven dimensions, grouped by it, each line is totalled under its Security's value in that dimension and lines whose Security has none are totalled under unassigned`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.4 a range before the earliest snapshot and before the ledger's first fiscal period succeeds with a zero-total point for each month-end`` () =
        Assert.Fail "Not yet implemented"
