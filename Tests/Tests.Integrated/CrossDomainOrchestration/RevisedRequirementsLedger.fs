module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsLedger

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type RevisedRequirementsLedgerTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-4.1 deactivating an account with no active end supplied sets its active end to the Eastern calendar date of the audit envelope's system instant`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-2.11 posting a journal entry whose last comment names a secondary journal entry that doesn't exist fails and stores none of its header, lines, external references or earlier comments`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-2.11 posting a journal entry with comments stores every comment and returns each with its generated ID and timestamp`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.1 a fetched journal entry returns every comment whose primary is that entry and not a comment on which it is only the secondary`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.4 fetching an account's lines with the non-voided restriction returns every line of its non-voided entries and no line of its voided entries`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.4 fetching an account's lines without the non-voided restriction returns every line of both its voided and non-voided entries`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.5 for each of a source FI alone and a reference value alone, a lookup by external reference returns every entry carrying a reference that matches the value given and no other entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-3.5 a lookup given a source FI and a reference value returns an entry whose single external reference matches both, and not an entry that matches the FI on one reference and the value on another`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-4.4 voiding a journal entry with a reason that names a secondary journal entry stores the reason as a comment whose primary is the voided entry and whose secondary is the one named`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-4.4 voiding a journal entry with a reason that names a secondary journal entry that doesn't exist fails with the secondary-not-found error, the entry is not voided, and no reason comment is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-JE-4.4 voiding an already-voided journal entry with a valid reason fails and stores no reason comment`` () =
        Assert.Fail "not implemented"
