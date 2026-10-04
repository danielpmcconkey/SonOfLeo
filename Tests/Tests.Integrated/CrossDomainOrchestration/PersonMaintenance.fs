module Tests.Integrated.CrossDomainOrchestration.PersonMaintenance

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PersonMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-PER-2.1 creating a Person from a name and a birthdate stores both, and the list returns that Person with that name and birthdate`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-1.3 creating a Person whose name exactly matches an existing Person's is rejected with a typed error naming the name, and no second Person is stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-1.3 REQ-SYS-1.1 creating a Person whose name is an existing Person's name wrapped in whitespace is rejected as a duplicate naming the trimmed name`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-1.3 a Person whose name differs from an existing Person's only by letter case is created alongside it`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-1.5 REQ-SYS-3.4 a birthdate on the calendar date of the operation's initiation instant is accepted, and one the day after is rejected with a typed error naming the date`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 updating a Person addressed by its current name changes its name and birthdate, and its old name then matches no Person`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-PER-1.3 renaming a Person to another Person's name is rejected with a typed error naming the name, and neither Person changes`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.2 REQ-PER-1.5 updating a Person's birthdate to the day after the current date is rejected with a typed error naming the date, and the stored birthdate is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.3 listing Persons returns every fixture Person with its name and birthdate, ordered by name, and no other Person`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-PER-2.4 updating a Person by a name that matches no Person fails with a typed error naming that name`` () =
        Assert.Fail "Not yet implemented"
