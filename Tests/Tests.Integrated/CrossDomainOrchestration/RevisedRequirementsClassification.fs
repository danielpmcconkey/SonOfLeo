module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsClassification

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type RevisedRequirementsClassificationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CR-1.5 REQ-CR-4.3 creating a classification rule whose claimant is a payment agreement given by name stores the rule claiming that payment agreement and no account`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-1.5 REQ-CR-4.3 creating a classification rule whose payment agreement claimant name matches no payment agreement fails with a typed error naming the name, and no rule is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-1.14 for each of the Source, Description and Memo field matches, a pattern matches the same text in its own case and does not match it in the other case`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-1.14 for each of the Source, Description and Memo field matches, a pattern is satisfied by a value containing it mid-string, and the same pattern anchored with ^ and $ is not`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-3.4 when exactly one active rule matches and its claimant is a payment agreement, the outcome is OneMatch carrying that payment agreement's ID, the rule's ID and its priority`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-5.3 for each of the payment agreement claimant filter and the claimant type filter (account, payment agreement), fetching rules returns every rule that meets it and no rule that doesn't`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-5.3 the payment agreement claimant filter given only part of a payment agreement's name, or the full name in the wrong case, returns no rule`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-5.3 for each of the name filter and the source pattern filter, a partial value in the wrong case matches no rule and the same partial value in the right case matches the rule`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-5.5 a fetched rule identifies an account claimant by the account's code and name, and a payment agreement claimant by the payment agreement's name`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-6.1 for each claimant switch (account to payment agreement, payment agreement to account), updating a rule's claimant stores the new claimant and clears the old one, leaving exactly one`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CR-6.3 updating a rule's claimant to a payment agreement name that matches no payment agreement fails with a typed error naming the name, and the rule keeps its claimant`` () =
        Assert.Fail "not implemented"
