module Tests.Integrated.CrossDomainOrchestration.PreLedgerBalances

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PreLedgerBalancesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-15.1 REQ-POS-15.4 recording a pre-ledger balance for an Asset account and one for a Liability account stores each and returns each as stored with its account code, date and balance, marked as not replacing one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.1 a pre-ledger balance of 0.00 and one of -15.00 are each recorded and listed exactly as recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.1 for each of an Equity, a Revenue and an Expense account, recording a pre-ledger balance is rejected with a typed error naming the account code and its type`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.1 REQ-POS-15.4 recording a pre-ledger balance for an account and date that already has one replaces its balance, returns it marked as replacing one, and listing gives only the new balance for that account and date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.2 a pre-ledger balance dated the day before the earliest fiscal period's start date is accepted, and one dated on that start date is rejected with a typed error naming the account code and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.3 a pre-ledger balance for a ledger account linked to an Investment Account is rejected with a typed error naming the account code and that Investment Account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.3 a pre-ledger balance for a Property's asset account is rejected with a typed error naming the account code and that Property, and one for a Property's mortgage account is accepted`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.4 one operation naming the same account and date twice is rejected with a typed error naming the account code and the date, and neither balance is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.5 deleting the pre-ledger balance for an account and date removes it and leaves the account's balances on other dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.5 deleting for an account and date with no pre-ledger balance fails with a typed not-found error naming the account code and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.6 listing pre-ledger balances for one account between two dates returns every balance of that account dated in the range, both ends included, in date order, each with the account's code and name, and none outside the range or of another account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.6 listing pre-ledger balances for every account between two dates, recorded out of account-code and date order, returns every balance in the range and none outside it, ordered by account code and then balance date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-15.6 listing pre-ledger balances with the end date the day before the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"
