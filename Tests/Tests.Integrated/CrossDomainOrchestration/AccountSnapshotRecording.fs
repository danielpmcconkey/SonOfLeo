module Tests.Integrated.CrossDomainOrchestration.AccountSnapshotRecording

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type AccountSnapshotRecordingTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-6.1 REQ-POS-7.3 recording a new snapshot stores its date, provenance, contribution basis and every line, and returns it as stored, marked as not a replacement`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.1 a snapshot with no lines is recorded, and fetching it gives back no lines`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.1 REQ-POS-7.1 one operation recording snapshots for two accounts stores both`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.1 one operation naming the same account and date twice is rejected with a typed error naming the account and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.2 REQ-POS-7.3 recording a date that already has a snapshot replaces its provenance, contribution basis and every line, so a line present before and absent from the new snapshot is gone, and returns it marked as a replacement`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.2 re-recording a date with figures identical to the stored snapshot succeeds and is marked as a replacement, not rejected as a no-op`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.7 a line's six-decimal quantity and price, its market value and its cost basis are fetched back exactly as recorded, and a line recorded with no cost basis is fetched back with none`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.3 a snapshot dated the day before the account's active begin, and one dated the day after its active end, are each rejected with a typed error naming the account and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.3 snapshots dated on the account's active begin and on its active end are each accepted`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.3 REQ-SYS-3.4 a snapshot dated on the calendar date of the operation's initiation instant is accepted, and one dated the day after is rejected with a typed error naming the account and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.4 a contribution basis on a Roth account's snapshot is stored, and for each of Taxable, TaxDeferred and Hsa, one on that account's snapshot is rejected with a typed error naming the account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.5 a line naming a Security of which the account has no Holding fails with a typed error naming the account and the Security, and no Holding is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.5 a line naming a Security held only in a different account fails with a typed error naming the account and the Security`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-6.6 a snapshot naming one Security on two lines is rejected with a typed error naming the Security`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 recording a snapshot for an account name that matches no account fails with a typed not-found error naming Investment Account and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.4 deleting the snapshot for an account and date removes it and its lines, and leaves the account's other snapshots`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.4 deleting for an account and date with no snapshot fails with a typed not-found error naming the account and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.5 fetching an account and date returns that snapshot's provenance, contribution basis and lines as recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-7.5 listing an account's snapshot dates between two dates returns every snapshot date of that account in the range, both ends included, in date order with its provenance, and no date outside the range or of another account`` () =
        Assert.Fail "Not yet implemented"
