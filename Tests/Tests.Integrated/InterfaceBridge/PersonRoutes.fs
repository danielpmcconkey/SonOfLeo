module Tests.Integrated.InterfaceBridge.PersonRoutes

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PersonRoutesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-PER-2.1 a Person Create payload creates the Person, and the return carries its name and birthdate`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-1.4 a Person Create payload with no birthdate is rejected with a typed error naming the missing birthdate, and no Person is created`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 a Person Update payload naming a new name and birthdate changes both, and the return carries the new values`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-SYS-6.1 a Person Update payload naming no field is rejected with a typed no-change error, and the Person is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-SYS-6.1 a Person Update payload changing the name and re-sending the stored birthdate succeeds, changing the name and leaving the birthdate as stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-PER-1.1 a Person Update payload with a whitespace-only name is rejected with a typed empty-name error, and the stored name is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.3 the Person List route returns every Person with its name and birthdate, ordered by name`` () =
        Assert.Fail "Not yet implemented"
