module Tests.Integrated.CrossDomainOrchestration.HoldingsAsOf

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type HoldingsAsOfTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-8.1 the accounts in holdings as of a date are exactly the fixture accounts active on that date with a snapshot on or before it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 REQ-POS-8.2 an account whose latest snapshot on or before the date is weeks older than the date is included with that older snapshot's date and lines`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 a snapshot dated after the as-of date is ignored in favour of the account's latest one on or before it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 an account whose active end is before the date is excluded although it has snapshots on or before the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 REQ-POS-4.7 an account is included as of its active end date and excluded as of the day after`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 an account active on the date with no snapshot on or before it is excluded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.1 an account whose latest snapshot has no lines is included with no lines`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.2 each included account carries its name, institution, account group, tax treatment, owners' names, linked ledger account's code and name, and its snapshot's date, provenance and contribution basis, as the fixture holds them`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.3 each line carries its Security's name and ticker, the name of its value in each of the seven dimensions or nothing, its Holding's basis method, and its quantity, price, market value and cost basis as recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-8.4 accounts are ordered by name and the lines within each account by Security name`` () =
        Assert.Fail "Not yet implemented"
