namespace Tests.Integrated.ModelOrchestrator

open Logger.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type JournalEntryCommentOrchestrationTests(fixture: TestDataFixture) =

    (* REQ-JE-1.56 is about moving a link that already exists, so both tests need a comment
       that already carries one. Every fixture comment is staged with a null secondary, so
       each test builds its own starting state through constructNewAndSaveToDb — a different
       function from the one under test, so a bug in updateComment cannot fake the starting
       state that same test then asserts against. *)
    let createCommentLinkedToSecondary context textString secondaryJournalEntryId =
        result {
            let! commentText = textString |> CommentText.create
            return!
                JournalEntryCommentOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    (Some secondaryJournalEntryId)
                    commentText
        }

    [<Fact>]
    member _.``REQ-JE-1.56 updateComment repoints the secondary JE link at a different entry``() =
        runFuncAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! comment =
                    fixture.Data.jeWithRefId
                    |> createCommentLinkedToSecondary context "REQ-JE-1.56 repoint"
                Assert.Equal(Some fixture.Data.jeWithRefId, comment |> JournalEntryComment.secondaryJournalEntryId)
                let! repointed =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        (comment |> JournalEntryComment.journalEntryCommentId)
                        NoChange
                        (SetTo(Some fixture.Data.jeWithLinesRefsAndCommentsId))
                Assert.Equal(
                    Some fixture.Data.jeWithLinesRefsAndCommentsId,
                    repointed |> JournalEntryComment.secondaryJournalEntryId
                )
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.56 updateComment clears the secondary JE link to no entry``() =
        runFuncAndAutoRollback JournalEntryUpdateComment (fun context ->
            result {
                let! comment =
                    fixture.Data.jeWithRefId
                    |> createCommentLinkedToSecondary context "REQ-JE-1.56 clear"
                Assert.Equal(Some fixture.Data.jeWithRefId, comment |> JournalEntryComment.secondaryJournalEntryId)
                let! cleared =
                    JournalEntryCommentOrchestration.updateComment
                        context
                        (comment |> JournalEntryComment.journalEntryCommentId)
                        NoChange
                        (SetTo None)
                Assert.Equal(None, cleared |> JournalEntryComment.secondaryJournalEntryId)
                return ()
            })
        |> railroadWrapper
