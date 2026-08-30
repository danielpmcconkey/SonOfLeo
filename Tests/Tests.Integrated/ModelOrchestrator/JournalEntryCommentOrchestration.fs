namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.Ledger
open ModelOrchestrator
open ModelOrchestrator.JournalEntries
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.SadPath
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type JournalEntryCommentOrchestrationTests(fixture: TestDataFixture) =
    
    [<Fact>]
    member _.``REQ-JE-1.56 updateComment repoints the secondary JE link at a different entry``() =
        let comment =
            fixture.Data.sharedCommentJe2
            |> JournalEntry.comments
            |> List.head
        let commentId = comment |> JournalEntryComment.journalEntryCommentId
        let repointedJeId = fixture.Data.jeWithLinesRefsAndCommentsId
        let expected = Some repointedJeId
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! repointed =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        commentId
                        NoChange
                        (SetTo(Some repointedJeId))
                let actual = repointed |> JournalEntryComment.secondaryJournalEntryId
                Assert.Equal( expected, actual )
                return () } )
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.56 updateComment clears the secondary JE link to no entry``() =
        let comment =
            fixture.Data.sharedCommentJe2
            |> JournalEntry.comments
            |> List.head
        let commentId = comment |> JournalEntryComment.journalEntryCommentId
        let expected = None
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! cleared =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        commentId
                        NoChange
                        (SetTo(None))
                let actual = cleared |> JournalEntryComment.secondaryJournalEntryId
                Assert.Equal(expected, actual)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.7 updateComment rejects no-op when both fields are NoChange``() =
        let comment =
            fixture.Data.sharedCommentJe2
            |> JournalEntry.comments
            |> List.head
        let commentId = comment |> JournalEntryComment.journalEntryCommentId
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun context ->
            let result =
                JournalEntryCommentOrchestration.updateComment
                    context
                    commentId
                    NoChange
                    NoChange
            isCorrectErrorEmpty result JournalEntryCommentUpdateNoOp None)
        |> railroadWrapper
