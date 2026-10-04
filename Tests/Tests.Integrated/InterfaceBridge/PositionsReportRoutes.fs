module Tests.Integrated.InterfaceBridge.PositionsReportRoutes

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PositionsReportRoutesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-8.6 the NetWorth route in data-only mode returns the same rows and totals as the net worth computation for the same date, each ledger account's name beside its code`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-2.3 the NetWorth route in report mode writes a new HTML file and returns its fully qualified path`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-2.4 the NetWorth report file with date interpolation is the base directory and file name followed by a hyphen, the as-of date as yyyy-MM-dd, and .html`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-8.6 REQ-RPT-3.1 the rendered net worth header shows the report title and the as-of date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.5 the InvestmentWealthHistory route in data-only mode returns the same points and group totals as the wealth history computation for the same range and grouping`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-2.3 the InvestmentWealthHistory route in report mode writes a new HTML file and returns its fully qualified path`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-2.4 the InvestmentWealthHistory report file with date interpolation is the base directory and file name followed by a hyphen, the begin and end dates as yyyy-MM-dd_yyyy-MM-dd, and .html`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history shows the report title and the begin and end dates in its header, and a table with one row per month-end and one column per group value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-RPT-9.1 an InvestmentWealthHistory payload naming a grouping outside account, account group, tax treatment, owners and the seven dimensions is rejected with a typed error naming the text given`` () =
        Assert.Fail "Not yet implemented"
