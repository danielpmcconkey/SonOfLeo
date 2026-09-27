namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.SadPath
open App.Utility.IAppError
open Tests.Helpers.TestError
open App.Utility.FieldUpdate
open App.Utility.Result
open Xunit
open Business.FinancialServices.Ledger.LedgerError

[<Collection("SharedTestData")>]
type JournalEntryCommentOrchestrationTests(fixture: TestDataFixture) =
    
    [<Fact>]
    member _.``REQ-JE-1.56 updateComment repoints the secondary JE link at a different entry``() =
        let comment =
            fixture.Data.sharedCommentJe2
            |> JournalEntryOrchestration.comments
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
            |> JournalEntryOrchestration.comments
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
            |> JournalEntryOrchestration.comments
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

    // =========================================================================
    // Plan defects 8–17 (2026-09-27)
    // =========================================================================

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a journal entry comment by an ID no comment holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 re-pointing a comment's secondary journal entry to an ID no journal entry holds fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a comment whose secondary journal entry does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"
