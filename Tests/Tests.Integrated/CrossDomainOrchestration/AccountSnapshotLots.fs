module Tests.Integrated.CrossDomainOrchestration.AccountSnapshotLots

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type AccountSnapshotLotsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-6.9 REQ-POS-7.3 a Taxable account's snapshot whose lines carry lots is recorded, and both the returned snapshot and a later fetch give each line's lots with their acquired date, quantity and reported cost basis exactly as supplied`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.9 for each of TaxDeferred, Roth and Hsa, a snapshot whose line carries one lot is rejected with a typed error naming the account and the security, and the account's stored snapshot for that date is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.10 a Taxable account's snapshot with lots on one line and none on another is recorded, and the line recorded without lots is fetched back with no lots`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.10 a Taxable account's later snapshot recorded with no lots for a line whose earlier snapshot carried lots is fetched back with no lots, while the earlier snapshot still has its lots`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.12 recording a snapshot whose line's lots sum to 0.000001 less than its quantity fails with the lot-sum error and the account's stored snapshot for that date keeps its lines and lots unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.13 two lots of one line identical in acquired date, quantity and cost basis are both recorded and both fetched back`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.13 a line's lots supplied out of acquired-date and quantity order are fetched back in exactly the order supplied`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.2 re-recording a snapshot whose line carried lots, with different lots on that line, leaves exactly the new lots on it in the order supplied and none of the old`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.2 re-recording a snapshot whose line carried lots, with that line carrying none, leaves the line with no lots`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.5 fetching the fixture's Taxable snapshot gives each line's lots exactly as the fixture recorded them, including two identical lots in the order supplied`` () =
        Assert.Fail "Not yet implemented"
