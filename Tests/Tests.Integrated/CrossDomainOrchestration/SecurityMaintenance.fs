module Tests.Integrated.CrossDomainOrchestration.SecurityMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type SecurityMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-POS-11.1 creating a Dimension Value stores it in its dimension, and listing that dimension returns it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-2.3 creating a Dimension Value whose name exactly matches another in the same dimension is rejected with a typed error naming the dimension and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-2.3 a Dimension Value may take a name already used in a different dimension`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-2.3 a Dimension Value whose name differs from another in its dimension only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.1 renaming a Dimension Value addressed by dimension and current name changes its name, and a Security referencing it lists the new name in that dimension`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.1 REQ-POS-2.3 renaming a Dimension Value to a name already used in its dimension is rejected with a typed error naming the dimension and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.1 listing one dimension's values returns every fixture value of that dimension ordered by name, and no value of any other dimension`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 renaming a Dimension Value by a name that exists only in another dimension fails with a typed not-found error naming Dimension Value and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 creating a Security with a name, a ticker and a value in each of the seven dimensions stores all of them, and the list returns each one`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-3.3 REQ-POS-3.4 a Security with no ticker and no Dimension Values is created, and the list shows it with no ticker and nothing in each of the seven dimensions`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-3.2 creating a Security whose name exactly matches an existing Security's is rejected with a typed error naming the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-3.2 a Security whose name differs from an existing one's only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-3.3 creating a Security whose ticker exactly matches another Security's is rejected with a typed error naming the ticker`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-3.3 a Security whose ticker differs from another's only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-3.4 a Security given two values of the same dimension is rejected with a typed error naming the dimension`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 REQ-POS-11.2 a Security given a Dimension Value name that exists in another dimension but not in the dimension given fails with a typed not-found error naming Dimension Value and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 updating a Security addressed by its current name changes its name and ticker and sets one dimension's value, leaving its values in the other six dimensions unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 an update that clears a Security's ticker and one dimension's value leaves it with no ticker and nothing in that dimension, and its other dimensions unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 REQ-POS-3.2 renaming a Security to another Security's name is rejected with a typed error naming the name, and neither Security changes`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.9 updating a Security by a name that matches no Security fails with a typed not-found error naming Security and the name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-POS-11.2 listing Securities returns every fixture Security ordered by name, each with its ticker and the name of its value in each of the seven dimensions or nothing, and no other Security`` () =
        Assert.Fail "Not yet implemented"
