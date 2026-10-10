module Tests.Integrated.CrossDomainOrchestration.InvestmentActivityRecording

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InvestmentActivityRecordingTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-12.1 REQ-POS-13.5 listing the fixture's activity account across its recorded range gives every activity, one of each of the fifteen kinds among them, with its date, kind, description, source, Security, quantity, price and amount exactly as the fixture recorded it, including activities with no source, no Security, no quantity and no price, which list back with each absent`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.5 recording an activity naming a Security of which the account has no Holding fails with a typed error naming the account and the Security, and no Holding is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.5 recording an activity naming a Security held only in a different account fails with a typed error naming the account and the Security`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.8 activities dated the day before the account's active begin and the day after its active end are each rejected with a typed error naming the account and the date, and activities dated on its active begin and on its active end are accepted`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.1 REQ-SYS-3.4 a range ending on the calendar date of the operation's initiation instant with an activity dated on it is accepted, and a range ending the day after is rejected with a typed error naming the account and the range`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.2 recording a range replaces every activity of the account dated within it with those supplied, and leaves the account's activities dated the day before and the day after the range untouched`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.2 recording an empty list for a range removes every activity of the account dated within it and leaves the activities outside the range`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.2 recording a range for one account leaves another account's activities on the same dates untouched`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.2 REQ-POS-13.4 re-recording a range with activities identical to those stored succeeds with removed equal to recorded, and listing gives each activity once, not twice`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.4 recording two ranges returns for each its account and range, the number removed equal to the hand-counted activities that were dated in it, and the number recorded equal to the number supplied`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.9 two activities of one account on one date identical in every field are both recorded and both listed`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-12.9 activities of one account on one date are listed in the order supplied, not sorted by kind, description or amount`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.5 listing an account's activities supplied out of date order between two dates returns every activity of that account dated in the range, both ends included, ordered by date, and none dated outside the range or belonging to another account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.5 listing an account's activities with the end date the day before the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"
