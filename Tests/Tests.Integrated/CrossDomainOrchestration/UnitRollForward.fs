module Tests.Integrated.CrossDomainOrchestration.UnitRollForward

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type UnitRollForwardTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-14.1 rolling forward with the first date equal to the second, and with the first date later than the second, is each rejected with a typed error naming the account and both dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.1 rolling forward from a date with no snapshot of the account, and to a date with no snapshot of the account, including a date on which only another account has a snapshot, is each rejected with a typed error naming the account and that date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 the balancing account's roll-forward between its two snapshots gives every Security its start quantity, units in, units out, expected quantity and end quantity as derived by hand from the fixture, with a difference of zero on every row`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 activity dated on the first snapshot date and the day before it is not counted, and activity dated the day after the first snapshot date is counted`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 activity dated on the second snapshot date and the day before it is counted, and activity dated the day after the second snapshot date is not counted`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 a Security on the second snapshot and not the first has a row with start quantity 0, and a Security on the first and not the second has a row with end quantity 0`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 a Security on neither snapshot that a units-moving activity in the window names has a row with start and end quantity 0, its units in and out as the activities give, and the hand-derived difference`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 REQ-POS-12.2 every units-in activity's quantity is totalled under units in and every units-out activity's quantity under units out, each against the Security it names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 a Security named in the window only by a Dividend, Interest or CapitalGainDistribution, and on neither snapshot, has no row`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 a Contribution naming no Security leaves every row's units in unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 rows for Securities whose snapshot lines and activities were recorded out of name order are returned ordered by Security name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 REQ-POS-14.3 the non-balancing account's roll-forward returns, as data and not as an error, the hand-derived non-zero difference for each Security whose activity is missing`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-14.2 a Security whose units out exceed its start quantity plus units in has the negative expected quantity derived by hand`` () =
        Assert.Fail "Not yet implemented"
