module Tests.Integrated.CrossDomainOrchestration.NetWorthHistory

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type NetWorthHistoryTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-10.1 net worth history with the end date the day before the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.2 a range beginning mid-month and ending on a month-end gives exactly one point per month-end from the end of the begin month to the end date, in date order`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.2 a range beginning on a month-end includes a point on that begin date, and one ending mid-month includes no point for that last month`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.2 a range beginning and ending inside one month, before its last day, gives no points`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.2 a range from the last pre-ledger month-end to the first fiscal-period month-end gives two points: the first marked pre-ledger and the second not, each with its seven hand-derived totals and its absent components`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.3 a range whose month-ends run past the last fiscal period fails with a typed error naming the earliest month-end that is neither within a fiscal period nor pre-ledger`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-10.3 a range whose month-ends include one in a gap between two fiscal periods fails with a typed error naming that month-end`` () =
        Assert.Fail "Not yet implemented"
