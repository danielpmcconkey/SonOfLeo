module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsStaging

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type RevisedRequirementsStagingTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-STG-1.4 for each group_id length (36 characters accepted, 37 characters rejected with a typed error naming the field), ingesting a file with that group_id gives that outcome`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-1.6 for each amount with no decimal places and with one decimal place, a record carrying it is staged with that amount`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-2.6 for each source_file length (150 characters accepted, 151 characters rejected with a typed error and nothing stored), creating a staged entry with that source file gives that outcome`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-5.1 for each status, a classification run assigns a matching account rule's account to an entry whose status is Ingested, NoMatch or Conflict, and leaves an entry with any other status unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-5.2 a classification run leaves a staged line unassigned when the only rule that matches it claims a payment agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-5.2 when a payment agreement rule with a lower priority value and an account rule both match a staged line, a classification run assigns the account rule's account`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-7.2 for each of the statuses Classified, NoMatch and Conflict, a later entry with that status sharing the original's source and FI reference is flagged Duplicate`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-7.2 when two entries share a source and FI reference, the first-ingested is the original and the later-ingested is flagged Duplicate even when the later-ingested has the earlier entry date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-7.2 an Ignored original keeps its status, and a later Ingested entry sharing its source and FI reference is flagged Duplicate`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-9.3 a journal entry posted from a staged entry, fetched back after posting, carries no comments`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-10.2 for each of the journal entry ID filter and the journal entry line ID filter, fetching staged entries returns the posted entry that produced that journal entry and no other entry`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-STG-10.3 fetching staged entries by the journal entry line ID of one of a posted entry's lines returns that entry with all of its lines`` () =
        Assert.Fail "not implemented"
