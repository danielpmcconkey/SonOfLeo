module Tests.Integrated.CrossDomainOrchestration.BalanceAccountMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type BalanceAccountMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-16.7 REQ-POS-16.4 creating a Balance Account stores its name, kind, institution and owners, and the list returns each of them with its owners' names`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.1 creating a Balance Account whose name exactly matches an existing one's is rejected with a typed error naming the name, and no second Balance Account is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.1 a Balance Account whose name differs from an existing one's only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.4 a Balance Account given no owners is rejected with a typed error naming the Balance Account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.4 a Balance Account given the same Person twice among its owners is rejected with a typed error naming the Person`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.4 REQ-PER-2.4 a Balance Account with an owner name that matches no Person fails with a typed error naming that name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.7 updating a Balance Account addressed by its current name changes its name, institution and owners, and the list shows every new value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.7 an update giving a Balance Account's owners replaces the set whole: an owner left out is gone and a new owner is present`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.2 a Debt Balance Account updated in its name, institution and owners is still of kind Debt, and its Balances keep their amounts`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.7 listing Balance Accounts returns every fixture Balance Account ordered by name, from a fixture set whose stored order differs from it, with its kind, institution and owners' names, and no other`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 updating a Balance Account by a name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 recording a Balance for a Balance Account name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name, and nothing is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.5 REQ-POS-16.8 recording a Balance for a Cash and one for a Debt Balance Account stores each and returns each as stored with its Balance Account, date and amount, marked as not replacing one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.5 a Balance of 0.00 and one of -15.00 are each recorded and listed exactly as recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.5 a Balance dated the operation's current date is recorded, and one dated the day after is rejected with a typed error naming the Balance Account and the date, and is not recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.8 recording a Balance for a Balance Account and date that already has one replaces its amount, returns it marked as replacing one, and listing gives only the new amount for that date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.8 one operation naming the same Balance Account and date twice is rejected with a typed error naming both, and neither Balance is recorded`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.9 deleting the Balance for a Balance Account and date removes it and leaves the account's Balances on other dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.9 deleting for a Balance Account and date with no Balance fails with a typed not-found error naming the Balance Account and the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.9 listing a Balance Account's Balances between two dates returns every Balance of that account dated in the range, both ends included, in date order, and none outside the range or of another account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.9 listing a Balance Account's Balances with the end date the day before the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.10 deleting a Balance Account with no Balance and no Property naming it removes it with its owners, and the list no longer returns it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.10 deleting a Balance Account that has a Balance is rejected with a typed error naming the Balance Account, and it and its Balances remain`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.10 deleting a Balance Account that is a Property's mortgage is rejected with a typed error naming the Balance Account and the Property, and it remains`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-16.10 REQ-POS-11.9 deleting a Balance Account by a name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name`` () =
        Assert.Fail "Not yet implemented"
