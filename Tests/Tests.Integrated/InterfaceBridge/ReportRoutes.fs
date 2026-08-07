namespace Tests.Integrated.InterfaceBridge

open Xunit

[<Collection("SharedTestData")>]
type ReportRoutesTests(fixture: Tests.Helpers.TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-2.2 data-only mode returns boundary-type rows with expected field types``() =
        // use routeReportingCommandForTesting
        // fetch only, so no roll back woes
        // actually check your test fixtures ahead to confirm you're pulling the right credit, debit, and net balances
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-2.3 report mode writes an HTML file and returns the file path``() =
        // use routeReportingCommandForTesting
        // fetch only, so no roll back woes
        // have your input payload route output to /tmp/son-of-leo-test-output with a filename unique to this test
        // also make sure that as-of is relevant maybe always have it be Calendar.today() plus 1 month?
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-2.4 date interpolation appends yyyy-MM-dd to filename before extension``() =
        // use routeReportingCommandForTesting
        // fetch only, so no roll back woes
        // have your input payload route output to /tmp/son-of-leo-test-output with a filename unique to this test
        // also make sure that as-of is relevant maybe always have it be Calendar.today() plus 1 month?
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-NGUI-4.5 unknown report name fails with typed error``() =
        // use routeReportingCommandForTesting
        // fetch only, so no roll back woes
        Assert.Fail "not implemented"
