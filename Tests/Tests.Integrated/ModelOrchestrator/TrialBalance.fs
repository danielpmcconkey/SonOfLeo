namespace Tests.Integrated.ModelOrchestrator

open Xunit

[<Collection("SharedTestData")>]
type TrialBalanceTests(fixture: Tests.Helpers.TestDataFixture) =

    [<Fact>]
    member _.``REQ-RPT-1.2 trial balance includes inactive accounts and accounts with no journal entry activity``() =
        // fetch only, so no roll back woes
        // actually check your test fixtures ahead to confirm you're pulling the right credit, debit, and net balances
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.4 leaf account row reflects only its own balance with no roll-up``() =
        // fetch only, so no roll back woes
        // actually check your test fixtures ahead to confirm you're pulling the right credit, debit, and net balances
        // this is kind of a bullshit test, but I'm allowing it. By the end of this, we'll have several tests that test balance math. They'll all fail or pass together
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.5 parent account row includes its own values plus recursive child roll-up``() =
        // fetch only, so no roll back woes
        // actually check your test fixtures ahead to confirm you're pulling the right credit, debit, and net balances
        // this is kind of a bullshit test, but I'm allowing it. By the end of this, we'll have several tests that test balance math. They'll all fail or pass together
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.6 result list is sorted by account code``() =
        // fetch only, so no roll back woes
        // don't check balances here. We've got enough of that
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.7 top-level accounts have generation 0 and children increment by 1 per level``() =
        // fetch only, so no roll back woes
        // don't check balances here. We've got enough of that
        // add an L3 and L4 account test fixture and actually test that they're in the right level
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.8 voided journal entries contribute zero to the trial balance``() =
        // I'm vetoing this test. Find whatever test already checks that the account balance function removes voids and add REQ-RPT-1.8 to its test name 
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.9 entries dated after the as-of date are excluded``() =
        // fetch only, so no roll back woes
        // only check the balance on accounts with post-dated entries
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.10 debit-normal net equals debits minus credits and credit-normal net equals credits minus debits``() =
        // I'm voiding this for the same reason I voided REQ-RPT-1.8 . Add REQ-RPT-1.10 the existing tests
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.11 account with no qualifying activity appears with zero credits debits and net``() =
        // still worth checking here to confirm I didn't remove anything in my nesting + flattening workflow
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-RPT-1.12 empty explicit account filter returns typed AppError``() =
        // I have withdrawn this requirement. Test can be deleted.
        Assert.Fail "not implemented"
