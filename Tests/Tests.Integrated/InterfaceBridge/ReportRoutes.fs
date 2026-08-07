namespace Tests.Integrated.InterfaceBridge

open Xunit

[<Collection("SharedTestData")>]
type ReportRoutesTests(fixture: Tests.Helpers.TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-2.2 data-only mode returns boundary-type rows with expected field types``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-2.3 report mode writes an HTML file and returns the file path``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-2.4 date interpolation appends yyyy-MM-dd to filename before extension``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-NGUI-4.5 unknown report name fails with typed error``() =
        Assert.Fail "not implemented"
