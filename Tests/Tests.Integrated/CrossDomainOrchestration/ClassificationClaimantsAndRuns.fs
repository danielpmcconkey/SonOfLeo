module Tests.Integrated.CrossDomainOrchestration.ClassificationClaimantsAndRuns

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type ClassificationClaimantsAndRunsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CR-1.23 a rule created with an account claimant reads back with claimant type 'AccountClaimant', and one created with a payment agreement claimant reads back with 'PaymentAgreementClaimant'`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-1.24 for each of both claimants set and neither set, reading a stored rule in that state by ID, by name or by filter fails with a typed error naming the rule's ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-1.25 for each place a LineType field match can sit (the first chain of the only group, the second chain of an Or group, a later group), the rule counts as constraining line type, and a rule whose field matches are all on other fields does not`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-3.7 an account classification run reports NoMatch for a line matched only by an active payment-agreement-claimant rule, and a payment-agreement run reports NoMatch for a line matched only by an active account-claimant rule`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-3.7 when an account rule and a payment-agreement rule of equal priority both match a line, the account run reports OneMatch carrying the account rule and the payment-agreement run reports OneMatch carrying the payment-agreement rule`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-3.8 a line that already has an account gets the same OneMatch, carrying the same account rule, as an otherwise identical line with no account`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-3.8 a line that is already linked gets the same OneMatch, carrying the same payment-agreement rule, as an otherwise identical unlinked line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-5.6 for each of an account code, a payment agreement name and a claimant type that resolves to nothing, a rule filter naming it fails with a typed error naming it`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.1 two classification runs return distinct, non-empty run IDs, and each run's match rows carry the ID that run returned`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.2 a line matched by a winning rule and a losing rule records one match row per rule, each with a system-generated ID, the run ID, the staged line ID, the rule ID and the run's Instant`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.2 a line on which two rules tie records exactly one match row per tied rule`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.2 a line matched by exactly one rule records exactly one match row`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.2 a line no rule matches records no match row`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.2 a run over several candidate lines records match rows for each line's own matching rules and no others`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.3 recording a match row for a (run, staged line, rule) combination already recorded leaves exactly one row for that combination`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.3 a rule that matches a line through more than one of its rule groups records one match row for that line in that run`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.5 fetching a run's match rows returns every row of that run and none of another run's, each with the rule's name, claimant and priority`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.5 fetching a run's match rows after the rule was renamed, re-pointed to another claimant and re-prioritised returns the rule's current name, claimant and priority, not those at run time`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.5 a run's match rows come back ordered by staged line, rows on the same line ordered by priority, and rows sharing line and priority ordered by rule name`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-8.5 fetching the match rows of a run ID that has none returns an empty list`` () =
        Assert.Fail "not implemented"
