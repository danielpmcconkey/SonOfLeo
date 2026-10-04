module Tests.Integrated.CrossDomainOrchestration.HoldingMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type HoldingMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.5 creating a Holding in a Taxable account with AverageCost stores it, and listing that account's Holdings shows the Security with AverageCost`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 creating a Holding in a Roth account with no basis method stores it, and the list shows it with no basis method`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.1 creating a second Holding of the same Security in the same account is rejected with a typed error naming the account and the Security`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.1 a Security already held in one account can be held in a second account`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.2 creating a Holding with no basis method in a Taxable account is rejected with a typed error naming the account, the Security and Taxable`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-5.2 creating a Holding with a basis method in a Roth account is rejected with a typed error naming the account, the Security and Roth`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 REQ-POS-5.2 changing a Taxable account's Holding from AverageCost to SpecificLot stores SpecificLot`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 REQ-POS-5.2 removing the basis method of a Taxable account's Holding is rejected with a typed error naming the account, the Security and Taxable, and the basis method is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 creating a Holding naming a Security that matches no Security fails with a typed not-found error naming Security and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 creating a Holding naming an Investment Account that matches no account fails with a typed not-found error naming Investment Account and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 listing Holdings with no account filter returns every fixture Holding ordered by account name then Security name, each with its basis method, and no other Holding`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.5 listing Holdings limited to one account returns every Holding of that account and none of any other account`` () =
        Assert.Fail "Not yet implemented"
