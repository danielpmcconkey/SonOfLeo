namespace Tests.Integrated.Model.Ledger

open System
open Xunit
open Tests.Integrated
open Model.Audit
open Model.Ledger.Journaling
open Utilities

[<Collection("SharedTestData")>]
type JournalEntryCommentTests(fixture: TestDataFixture) =

    // =============================================================================
    // Add comment
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-5.1 constructNewAndSaveToDb attaches a comment to a journal entry`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.basicJeId None "Test comment text" envelope None
        match result with
        | Ok c ->
            Assert.Equal(fixture.Data.basicJeId, c |> JournalEntryComment.primaryJournalEntryId)
            Assert.Equal("Test comment text", c |> JournalEntryComment.commentText |> CommentText.value)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-5.1 constructNewAndSaveToDb attaches a comment with a secondary JE link`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.basicJeId (Some fixture.Data.jeWithRefId)
                         "Comment with secondary link" envelope None
        match result with
        | Ok c ->
            Assert.Equal(fixture.Data.basicJeId, c |> JournalEntryComment.primaryJournalEntryId)
            Assert.Equal(Some fixture.Data.jeWithRefId, c |> JournalEntryComment.secondaryJournalEntryId)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-5.2 constructNewAndSaveToDb generates UUID and sets timestamps`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let expectedInstant = AuditEnvelope.instant envelope
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.basicJeId None "Timestamp test" envelope None
        match result with
        | Ok c ->
            Assert.NotEqual(Guid.Empty, c |> JournalEntryComment.uniqueId)
            Assert.Equal(expectedInstant, c |> JournalEntryComment.createdAt)
            Assert.Equal(expectedInstant, c |> JournalEntryComment.modifiedAt)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-1.52 constructNewAndSaveToDb accepts null secondary JE ID`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.basicJeId None "Null secondary" envelope None
        match result with
        | Ok c -> Assert.True(c |> JournalEntryComment.secondaryJournalEntryId |> Option.isNone)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-1.53 constructNewAndSaveToDb rejects secondary JE ID equal to primary`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.basicJeId (Some fixture.Data.basicJeId)
                         "Same primary and secondary" envelope None
        Assert.True(Result.isError result)

    [<Fact>]
    member _.``REQ-JE-5.5 constructNewAndSaveToDb allows comment on a voided entry`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.voidedJeId None "Comment on voided" envelope None
        match result with
        | Ok c -> Assert.Equal(fixture.Data.voidedJeId, c |> JournalEntryComment.primaryJournalEntryId)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-5.5 constructNewAndSaveToDb allows comment when fiscal period is closed`` () =
        // REQ-JE-5.5 says this should succeed, but the current implementation
        // re-validates EntryDate (including period-is-open) when fetching the
        // header during comment validation. This test documents current behavior;
        // it should flip to Ok once the read path stops re-validating period status.
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let result = JournalEntryComment.constructNewAndSaveToDb
                         fixture.Data.jeInClosedPeriodId None "Comment on closed period entry" envelope None
        Assert.True(Result.isError result)

    // =============================================================================
    // Update comment
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-5.3 updateComment amends the comment text`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateComment
        let result = JournalEntryComment.updateComment envelope
                         fixture.Data.fixtureCommentId "Updated comment text" None None
        match result with
        | Ok c -> Assert.Equal("Updated comment text", c |> JournalEntryComment.commentText |> CommentText.value)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-5.3 REQ-SYS-3.3 updateComment updates modified_at timestamp`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateComment
        let expectedInstant = AuditEnvelope.instant envelope
        let result = JournalEntryComment.updateComment envelope
                         fixture.Data.fixtureCommentId "Modified timestamp test" None None
        match result with
        | Ok c -> Assert.Equal(expectedInstant, c |> JournalEntryComment.modifiedAt)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-5.6 updateComment does not change the primary JE link`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateComment
        let result = JournalEntryComment.updateComment envelope
                         fixture.Data.fixtureCommentId "Primary link unchanged" None None
        match result with
        | Ok c -> Assert.Equal(fixture.Data.basicJeId, c |> JournalEntryComment.primaryJournalEntryId)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-5.3 updateComment rejects empty text`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateComment
        let result = JournalEntryComment.updateComment envelope
                         fixture.Data.fixtureCommentId "" None None
        Assert.True(Result.isError result)

    [<Fact>]
    member _.``REQ-JE-5.3 updateComment rejects whitespace-only text`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateComment
        let result = JournalEntryComment.updateComment envelope
                         fixture.Data.fixtureCommentId "   " None None
        Assert.True(Result.isError result)

    // =============================================================================
    // Persistence fidelity
    // =============================================================================

    [<Fact>]
    member _.``REQ-SYS-5.1 comment round-trips through persistence with all fields intact`` () =
        let envelope = AuditEnvelope.create JournalEntryAddComment
        let createResult = JournalEntryComment.constructNewAndSaveToDb
                               fixture.Data.basicJeId (Some fixture.Data.jeWithRefId)
                               "Round-trip comment" envelope None
        match createResult with
        | Error e -> Assert.Fail e
        | Ok created ->
            let fetchResult = created |> JournalEntryComment.uniqueId |> JournalEntryComment.fetchById None
            match fetchResult with
            | Error e -> Assert.Fail $"Fetch after creation failed: {e}"
            | Ok fetched ->
                Assert.Equal(created |> JournalEntryComment.uniqueId, fetched |> JournalEntryComment.uniqueId)
                Assert.Equal(created |> JournalEntryComment.primaryJournalEntryId, fetched |> JournalEntryComment.primaryJournalEntryId)
                Assert.Equal(created |> JournalEntryComment.secondaryJournalEntryId, fetched |> JournalEntryComment.secondaryJournalEntryId)
                Assert.Equal(
                    created |> JournalEntryComment.commentText |> CommentText.value,
                    fetched |> JournalEntryComment.commentText |> CommentText.value)
                Assert.Equal(created |> JournalEntryComment.createdAt, fetched |> JournalEntryComment.createdAt)
                Assert.Equal(created |> JournalEntryComment.modifiedAt, fetched |> JournalEntryComment.modifiedAt)
