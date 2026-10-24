module Tests.Integrated.CrossDomainOrchestration.PropertyMortgageBalanceAccounts

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PropertyMortgageBalanceAccountsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-9.9 REQ-POS-11.6 creating a Property with two mortgage Balance Accounts stores both, and the list returns their names in name order`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.9 a Property given a Cash Balance Account as a mortgage is rejected with a typed error naming the Balance Account and that it is not a Debt, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.9 REQ-POS-11.9 a Property given a mortgage Balance Account name that matches no Balance Account fails with a typed not-found error naming Balance Account and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.9 a Property given the same mortgage Balance Account twice is rejected with a typed error naming it, and no Property is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.9 a Balance Account already a mortgage of one Property, given as a mortgage of another, is rejected with a typed error naming the Balance Account and the Property already linked`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-9.9 REQ-POS-11.6 an update giving a Property's mortgage Balance Accounts as the one it already has plus a new one leaves it linked to both`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.6 an update giving a Property's mortgage Balance Accounts replaces the set whole: one left out is no longer its mortgage and a new one is`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.11 deleting a Property with mortgage Balance Accounts and no Valuation removes its links with it, after which each former mortgage Balance Account can be named as another Property's mortgage`` () =
        Assert.Fail "Not yet implemented"

