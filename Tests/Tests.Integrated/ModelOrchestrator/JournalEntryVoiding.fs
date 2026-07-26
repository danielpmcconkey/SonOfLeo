namespace Tests.Integrated.ModelOrchestrator

open System
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open ModelOrchestrator
open ModelOrchestrator.JournalEntries.JournalEntry
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.Rollback
open Utilities.ResultHelper
open Xunit
open Tests.Integrated
open Model.Audit
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
        let envelopeCreate = AuditEnvelope.create JournalEntryPostNew
        let envelopeVoid = AuditEnvelope.create JournalEntryVoid
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _, jeId =
                        createTestJournalEntryFromPrimitives
                            "JE create for void 4.3"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelopeCreate
                            tran
                    let! voided = jeId |> voidJournalEntry tran envelopeVoid None commentText
                    Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.4 voidJournalEntryOrchestration attaches a reason comment to the voided entry``() =
        let envelopeCreate = AuditEnvelope.create JournalEntryPostNew
        let envelopeVoid = AuditEnvelope.create JournalEntryVoid
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! je, jeId =
                        createTestJournalEntryFromPrimitives
                            "JE create for void 4.4"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelopeCreate
                            tran
                    Assert.Equal(0, je |> comments |> List.length) // just confirming that it's zero at the satrt
                    let! voided = jeId |> voidJournalEntry tran envelopeVoid None commentText
                    let comments = voided |> comments
                    Assert.Equal(1, comments |> List.length)
                    let comment = comments |> List.head
                    Assert.Equal(
                        commentText |> CommentText.value,
                        comment |> JournalEntryComment.commentText |> CommentText.value
                    )
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.5 voidJournalEntryOrchestration rejects void when fiscal period is closed``() =
        let envelopeCreate = AuditEnvelope.create JournalEntryPostNew
        let envelopeVoid = AuditEnvelope.create JournalEntryVoid
        let today = Calendar.today()
        let sevenMonthsAgo = today.PlusMonths(-7)
        let monthF = sevenMonthsAgo.Month.ToString("D2")
        let periodKeyStr = $"{sevenMonthsAgo.Year}-{monthF}"
        withRollback(fun tran ->
            let railroad =
                result {
                    // create Fiscal Period as open so you can add an entry into it
                    let! periodKey = periodKeyStr |> FiscalPeriodKey.fromString
                    let! fp = FiscalPeriodCreation.constructNewAndSaveToDb periodKey envelopeCreate tran
                    let fpId = fp |> fiscalPeriodId
                    // add the JE into that FP
                    let! _, jeId =
                        createTestJournalEntryFromPrimitives
                            "JE create for void 4.5"
                            None
                            sevenMonthsAgo
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelopeCreate
                            tran
                    // close the FP
                    let! _ = closeFiscalPeriod fpId envelopeCreate tran
                    // try to void
                    let voidedResult = jeId |> voidJournalEntry tran envelopeVoid None commentText
                    do!
                        match voidedResult with
                        | Error(JournalEntryVoidingFiscalPeriodIsClosed _) -> Ok()
                        | Error e -> Error e
                        | Ok _ -> Error(TestingError "Expected failure; got success")
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.6 REQ-SYS-6.1 voidJournalEntryOrchestration rejects void on already-voided entry``() =
        withRollback(fun tran ->
            let envelope = AuditEnvelope.create JournalEntryVoid
            let voidedResult = fixture.Data.voidedJeId |> voidJournalEntry tran envelope None commentText
            match voidedResult with
            | Error(JournalEntryVoidingNoOp _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail("Expected failure; got success"))

    [<Fact>]
    member _.``REQ-JE-4.3 voidJournalEntryOrchestration returns error for nonexistent entry id``() =
        // guards the railway itself: the fetch failure must propagate as an
        // Error, not escape the orchestrator as an exception
        withRollback(fun tran ->
            let envelope = AuditEnvelope.create JournalEntryVoid
            let badId = Guid.NewGuid() |> JournalEntryHeaderId.fromGuid
            let voidedResult = badId |> voidJournalEntry tran envelope None commentText
            match voidedResult with
            | Error(DalResultantRowsDidntMatchExpectation _) -> () // todo: surface a better error message
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail("Expected failure; got success"))
