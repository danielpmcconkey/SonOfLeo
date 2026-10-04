module Tests.Integrated.CrossDomainOrchestration.InvestmentAccountMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InvestmentAccountMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.3 creating an Investment Account stores its name, institution, account group, tax treatment, owners, active begin, active end and ledger link, and the list returns each of them with the linked account's code and name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.1 creating an Investment Account whose name exactly matches an existing one's is rejected with a typed error naming the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.1 an Investment Account whose name differs from an existing one's only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.5 REQ-PER-2.4 creating an Investment Account with an owner name that matches no Person fails with a typed error naming that name, and no account is stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.5 an Investment Account given the same Person twice among its owners is rejected with a typed error naming the Person`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.5 an Investment Account given no owners is rejected with a typed error naming the account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.6 for each of TaxDeferred, Roth and Hsa, an Investment Account with two owners is rejected with a typed error naming the account and its tax treatment`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.6 a Taxable Investment Account with two owners is created, and the list shows both owners`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.8 for each of an Asset account of a subtype other than Investment and an account of a type other than Asset, linking it to an Investment Account fails with a typed error naming the code and what is wrong`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-4.9 linking an Investment Account to a ledger account already linked to another Investment Account is rejected with a typed error naming the code and that other account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 updating an Investment Account addressed by its current name changes its name, institution, account group, active begin, active end and ledger link, and the list shows every new value`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 an update that clears an Investment Account's active end and ledger link leaves it with neither`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 REQ-POS-5.4 an update giving owners replaces the whole set: an account owned by two Persons and updated to one of them and a third is owned by exactly those two`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update adding a second owner to a Roth account is rejected with a typed error naming the account and its tax treatment, and the owners are unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update changing a jointly owned Taxable account to Roth without changing its owners is rejected with a typed error naming the account and Roth`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update changing a jointly owned Taxable account to Roth and reducing its owners to one in the same operation succeeds`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.6 an update changing a single-owner Roth account to Taxable and adding a second owner in the same operation succeeds`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.4 REQ-POS-4.5 an update giving an empty owner set is rejected with a typed error naming the account, and the owners are unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.3 changing the tax treatment of a Taxable account holding two Securities with basis methods to TaxDeferred is rejected with a typed error naming both Securities`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.3 changing the tax treatment of a Roth account holding two Securities to Taxable is rejected with a typed error naming both Securities`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.3 changing the tax treatment of a TaxDeferred account with Holdings to Roth succeeds, since its Holdings carry no basis method under either`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period so that snapshots fall before the new begin and after the new end is rejected with a typed error naming the earliest and latest offending snapshot dates`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.4 narrowing an account's active period to exactly its first and last snapshot dates succeeds`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 updating an Investment Account by a name that matches no account fails with a typed not-found error naming Investment Account and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.3 listing Investment Accounts returns every fixture account ordered by name, each with its institution, account group, tax treatment, owners' names, linked account's code and name, active begin and active end, and no other account`` () =
        Assert.Fail "Not yet implemented"
