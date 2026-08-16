namespace Tests.Integrated.Model.Ledger

open System

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open Tests.Helpers.Railroad
open Utilities.AppError
open Tests.Helpers.SadPath
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Xunit
open Tests.Helpers
open Model.Ledger.Journaling

[<Collection("SharedTestData")>]
type JournalEntryCommentTests(fixture: TestDataFixture) =

    // todo: need to move all of the tests that require orchestration to the orchestration files and see if we have duplicate tests. This goes for all domains
    [<Fact>]
    member _.``REQ-JE-5.1 constructNewAndSaveToDb attaches a comment to a journal entry``() =
        let commentText =
            "Test comment text"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    None
                    commentText
            match result with
            | Ok c ->
                Assert.Equal(fixture.Data.basicJeId, c |> JournalEntryComment.primaryJournalEntryId)
                Assert.Equal(commentText, c |> JournalEntryComment.commentText)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.1 constructNewAndSaveToDb attaches a comment with a secondary JE link``() =
        let commentText =
            "Comment with secondary link"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    (Some fixture.Data.jeWithRefId)
                    commentText
            match result with
            | Ok c ->
                Assert.Equal(fixture.Data.basicJeId, c |> JournalEntryComment.primaryJournalEntryId)
                Assert.Equal(Some fixture.Data.jeWithRefId, c |> JournalEntryComment.secondaryJournalEntryId)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.2 constructNewAndSaveToDb generates UUID and sets timestamps``() =
        let commentText =
            "Comment with secondary link"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let expectedInstant = context |> Context.getInitiationInstant
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    None
                    commentText
            match result with
            | Ok c ->
                Assert.NotEqual(
                    Guid.Empty,
                    c |> JournalEntryComment.journalEntryCommentId |> JournalEntryCommentId.value
                )
                Assert.Equal(expectedInstant, c |> JournalEntryComment.createdAt)
                Assert.Equal(expectedInstant, c |> JournalEntryComment.modifiedAt)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.52 constructNewAndSaveToDb accepts null secondary JE ID``() =
        let commentText =
            "Null secondary"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    None
                    commentText
            match result with
            | Ok c ->
                Assert.True(c |> JournalEntryComment.secondaryJournalEntryId |> Option.isNone)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.53 constructNewAndSaveToDb rejects secondary JE ID equal to primary``() =
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let commentText =
                "Same primary and secondary"
                |> CommentText.create
                |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    (Some fixture.Data.basicJeId)
                    commentText
            isCorrectError result JournalEntryCommentPrimaryAndSecondaryIdsAreSame None)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.5 constructNewAndSaveToDb allows comment on a voided entry``() =
        let commentText =
            "Comment on voided"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.voidedJeId
                    None
                    commentText
            match result with
            | Ok c ->
                Assert.Equal(fixture.Data.voidedJeId, c |> JournalEntryComment.primaryJournalEntryId)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.5 constructNewAndSaveToDb allows comment when fiscal period is closed``() =
        let commentText =
            "Comment on closed period entry"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let result =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.jeInClosedPeriodId
                    None
                    commentText
            match result with
            | Ok c ->
                Assert.Equal(fixture.Data.jeInClosedPeriodId, c |> JournalEntryComment.primaryJournalEntryId)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.3 updateComment amends the comment text``() =
        let expected = "Updated comment text"
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! textUpdate = expected |> CommentText.create |> Result.map SetTo
                let secondaryIdUpdate = NoChange
                let! updatedComment =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        fixture.Data.fixtureCommentId
                        textUpdate
                        secondaryIdUpdate
                Assert.Equal(expected, updatedComment |> JournalEntryComment.commentText |> CommentText.value)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.3 REQ-SYS-3.3 updateComment updates modified_at timestamp``() =
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! textUpdate = "Modified timestamp test" |> CommentText.create |> Result.map SetTo
                let secondaryIdUpdate = NoChange
                let! updatedComment =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        fixture.Data.fixtureCommentId
                        textUpdate
                        secondaryIdUpdate
                Assert.Equal(context |> Context.getInitiationInstant, updatedComment |> JournalEntryComment.modifiedAt)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.6 updateComment does not change the primary JE link``() =
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! textUpdate = "Primary link unchanged" |> CommentText.create |> Result.map SetTo
                let secondaryIdUpdate = NoChange
                let! updatedComment =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        fixture.Data.fixtureCommentId
                        textUpdate
                        secondaryIdUpdate
                Assert.Equal(fixture.Data.basicJeId, updatedComment |> JournalEntryComment.primaryJournalEntryId)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-5.1 comment round-trips through persistence with all fields intact``() =
        let commentText =
            "Round-trip comment"
            |> CommentText.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        runCommandRouteAndAutoRollback JournalEntryAddComment (fun context ->
            let createResult =
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    (Some fixture.Data.jeWithRefId)
                    commentText
            match createResult with
            | Error e -> Error e
            | Ok created ->
                // fetch inside the same contextsaction — the create is never committed
                let fetchResult =
                    created |> JournalEntryComment.journalEntryCommentId |> JournalEntryComment.fetchById context
                match fetchResult with
                | Error e -> Assert.Fail $"Fetch after creation failed: {e}"
                | Ok fetched ->
                    Assert.Equal(
                        created |> JournalEntryComment.journalEntryCommentId,
                        fetched |> JournalEntryComment.journalEntryCommentId
                    )
                    Assert.Equal(
                        created |> JournalEntryComment.primaryJournalEntryId,
                        fetched |> JournalEntryComment.primaryJournalEntryId
                    )
                    Assert.Equal(
                        created |> JournalEntryComment.secondaryJournalEntryId,
                        fetched |> JournalEntryComment.secondaryJournalEntryId
                    )
                    Assert.Equal(
                        created |> JournalEntryComment.commentText |> CommentText.value,
                        fetched |> JournalEntryComment.commentText |> CommentText.value
                    )
                    Assert.Equal(created |> JournalEntryComment.createdAt, fetched |> JournalEntryComment.createdAt)
                    Assert.Equal(created |> JournalEntryComment.modifiedAt, fetched |> JournalEntryComment.modifiedAt)
                Ok())
        |> railroadWrapper
