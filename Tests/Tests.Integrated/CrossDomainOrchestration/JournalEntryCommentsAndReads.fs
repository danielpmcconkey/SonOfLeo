module Tests.Integrated.CrossDomainOrchestration.JournalEntryCommentsAndReads

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type JournalEntryCommentsAndReadsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-2.15 a journal entry posted with two comments, one naming an existing secondary journal entry, stores both with the new entry as their primary and the named secondary on the one that names it, and returns both on the entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-2.15 a journal entry posted with no comments is stored and returned with no comments`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-2.15 for each invalid comment (whitespace-only text, text of 2001 characters, a secondary ID that matches no journal entry), posting the entry fails with the typed error for that fault (the not-found error naming the secondary for the last) and nothing is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.1.1 for each of the fetch by fiscal period, by date range, by external reference and by source FI, a voided and an active entry that both match the query are both returned, the voided one with its voided-at set and the active one without`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.1.1 fetching a voided journal entry by ID returns it with its voided-at set`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.5.1 a journal entry lookup by external reference given neither a source FI nor a reference value fails with a typed error`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.7.1 a journal entry fetch by date range whose start date is after its end date fails with a typed error`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.7 a journal entry fetch by date range whose start date equals its end date returns every entry dated that day and none dated the day before or after`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-5.8 creating a comment whose primary journal entry ID matches no journal entry fails with a typed error naming the primary, and no comment is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-5.8 creating a comment whose secondary journal entry ID matches no journal entry fails with a typed error naming the secondary, and no comment is stored`` () =
        Assert.Fail "not implemented"
