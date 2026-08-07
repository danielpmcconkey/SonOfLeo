namespace Tests.Integrated.ModelOrchestrator

open System
open Logger.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open ModelOrchestrator
open ModelOrchestrator.JournalEntries.JournalEntry
open Tests.Helpers.EntityFunctions
open Tests.Helpers.RouteResolver
open Tests.Helpers.Railroad
open Utilities.ResultHelper
open Xunit
open Tests.Helpers
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntryVoiding
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryVoidingTests(fixture: TestDataFixture) =

    let commentText =
        "Voiding for test"
        |> CommentText.create
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.3 voidJournalEntryOrchestration sets voided_at on the entry``() =
        let today = Calendar.today()
        runFuncAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! _, jeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create for void 4.3"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                let! voided = jeId |> voidJournalEntry context None commentText
                Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.4 voidJournalEntryOrchestration attaches a reason comment to the voided entry``() =
        let today = Calendar.today()
        runFuncAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! je, jeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create for void 4.4"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                Assert.Equal(0, je |> comments |> List.length) // just confirming that it's zero at the satrt
                let! voided = jeId |> voidJournalEntry context None commentText
                let comments = voided |> comments
                Assert.Equal(1, comments |> List.length)
                let comment = comments |> List.head
                Assert.Equal(
                    commentText |> CommentText.value,
                    comment |> JournalEntryComment.commentText |> CommentText.value
                )
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.5 voidJournalEntryOrchestration rejects void when fiscal period is closed``() =
        let today = Calendar.today()
        let sevenMonthsAgo = today.PlusMonths(-7)
        let monthF = sevenMonthsAgo.Month.ToString("D2")
        let periodKeyStr = $"{sevenMonthsAgo.Year}-{monthF}"
        runFuncAndAutoRollback JournalEntryVoid (fun context ->
            result {
                // create Fiscal Period as open so you can add an entry into it
                let! periodKey = periodKeyStr |> FiscalPeriodKey.fromString
                let! fp = periodKey |> FiscalPeriodCreation.constructNewAndSaveToDb context
                let fpId = fp |> fiscalPeriodId
                // add the JE into that FP
                let! _, jeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create for void 4.5"
                        None
                        sevenMonthsAgo
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                // close the FP
                let! _ = fpId |> closeFiscalPeriod context
                // try to void
                let voidedResult = jeId |> voidJournalEntry context None commentText
                do!
                    match voidedResult with
                    | Error(JournalEntryVoidingFiscalPeriodIsClosed _) -> Ok()
                    | Error e -> Error(TestingError $"Wrong error message. {AppError.toMessage e}")
                    | Ok _ -> Error(TestingError "Expected failure; got success")
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.6 voidJournalEntryOrchestration rejects void on already-voided entry``() =
        runFuncAndAutoRollback JournalEntryVoid (fun context ->
            let voidedResult = fixture.Data.voidedJeId |> voidJournalEntry context None commentText
            match voidedResult with
            | Error(JournalEntryVoidingNoOp _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error message. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError "Expected failure; got success"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.3 voidJournalEntryOrchestration returns error for nonexistent entry id``() =
        // guards the railway itself: the fetch failure must propagate as an
        // Error, not escape the orchestrator as an exception
        runFuncAndAutoRollback JournalEntryVoid (fun context ->
            let badId = Guid.NewGuid() |> JournalEntryHeaderId.fromGuid
            let voidedResult = badId |> voidJournalEntry context None commentText
            match voidedResult with
            | Error(JournalEntryHeaderIdDoesntExist _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error message. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError "Expected failure; got success"))
        |> railroadWrapper
