module Tests.Integrated.CrossDomainOrchestration.InvestmentActivityCoverage

open Xunit
open Tests.Helpers

[<Collection("SharedTestData")>]
type InvestmentActivityCoverageTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-13.6 recording one range for an account with no activity coverage leaves exactly one coverage row for that account, from the range's begin date to its end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that overlaps the account's one covered range leaves exactly one coverage row, from the earlier of the two begin dates to the later of the two end dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 for each of a range beginning the day after the account's covered range ends and a range ending the day before it begins, recording it leaves exactly one coverage row, from the earlier begin date to the later end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that reaches from inside one covered range of the account to inside a later one leaves exactly one coverage row, from the first's begin date to the second's end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that begins two days after the account's covered range ends, leaving one uncovered day between them, adds a second coverage row for exactly that range and leaves the first row's begin and end dates unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range lying wholly inside the account's covered range leaves exactly one coverage row, with the covered range's begin and end dates unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 REQ-POS-13.3 two adjoining ranges for one account in one operation, supplied later range first, leave exactly one coverage row, from the earlier range's begin date to the later range's end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range supplied with no activities, for an account with no activity coverage, leaves exactly one coverage row, from the range's begin date to its end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording ranges for two accounts in one operation leaves each account with exactly one coverage row, matching its own range`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that overlaps or adjoins another account's covered range leaves the other account's coverage rows exactly as they were and merges nothing across accounts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.6 REQ-POS-13.1 a recording of ranges for two accounts that is rejected because one account's two ranges overlap leaves both accounts' coverage rows exactly as they were before`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for an account with no coverage between two dates, the uncovered stretches are exactly one, from the begin date to the end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for two dates lying wholly inside one covered range, the uncovered stretches are an empty list`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for two dates enclosing two covered ranges, the uncovered stretches are exactly the stretch from the begin date to the day before the first range, the stretch between the two ranges, and the stretch from the day after the second range to the end date, in that order`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for a single uncovered day between two covered ranges, the uncovered stretches are exactly one, whose begin and end dates are both that day`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for coverage ending on the begin date and starting on the end date, the uncovered stretches are exactly one, from the day after the begin date to the day before the end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for coverage ending the day before the begin date and starting the day after the end date, the uncovered stretches are exactly one, from the begin date to the end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for an uncovered day given as both the begin and the end date, the uncovered stretches are exactly one, whose begin and end dates are both that day`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 for an account with no coverage between two dates that another account covers in full, the uncovered stretches are exactly one, from the begin date to the end date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-13.7 listing uncovered stretches with the end date the day before the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"
