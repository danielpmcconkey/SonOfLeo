namespace Tests.Integrated.ModelOrchestrator

open System
open Xunit
open Tests.Integrated
open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.JournalEntryPrimitives
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open ModelOrchestrator.JournalEntryVoiding
open Utilities

[<Collection("SharedTestData")>]
type JournalEntryVoidingTests(fixture: TestDataFixture) =

    let voidReason = { secondaryJournalEntryId = None; commentText = "Voiding for test" }

    // =============================================================================
    // Void — happy path
    //
    // Each test consumes its own fixture void victim. The orchestrator commits,
    // and the victim's voided end-state is by design — no cleanup.
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-4.3 voidJournalEntryOrchestration sets voided_at on the entry`` () =
        let envelope = AuditEnvelope.create JournalEntryVoid
        let result = voidJournalEntryOrchestration envelope voidReason fixture.Data.voidVictim1Id
        match result with
        | Ok voided -> Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-4.4 voidJournalEntryOrchestration attaches a reason comment to the voided entry`` () =
        let envelope = AuditEnvelope.create JournalEntryVoid
        let reason = { secondaryJournalEntryId = None; commentText = "This is the void reason" }
        let result = voidJournalEntryOrchestration envelope reason fixture.Data.voidVictim2Id
        match result with
        | Ok voided ->
            let voidComments = voided |> comments
            let hasReason = voidComments |> List.exists (fun c ->
                c |> JournalEntryComment.commentText |> CommentText.value = "This is the void reason")
            Assert.True(hasReason)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-4.3 REQ-JE-4.4 voidJournalEntryOrchestration returns full aggregate with void marker and comment`` () =
        let envelope = AuditEnvelope.create JournalEntryVoid
        let result = voidJournalEntryOrchestration envelope voidReason fixture.Data.voidVictim3Id
        match result with
        | Ok voided ->
            Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
            Assert.True(voided |> lines |> List.length >= 2)
            Assert.True(voided |> comments |> List.length >= 1)
        | Error e -> Assert.Fail e

    // =============================================================================
    // Void — rejections
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-4.5 voidJournalEntryOrchestration rejects void when fiscal period is closed`` () =
        let envelope = AuditEnvelope.create JournalEntryVoid
        let result = voidJournalEntryOrchestration envelope voidReason fixture.Data.jeInClosedPeriodId
        Assert.True(Result.isError result)

    [<Fact>]
    member _.``REQ-JE-4.6 REQ-SYS-6.1 voidJournalEntryOrchestration rejects void on already-voided entry`` () =
        let envelope = AuditEnvelope.create JournalEntryVoid
        let result = voidJournalEntryOrchestration envelope voidReason fixture.Data.voidedJeId
        Assert.True(Result.isError result)

    [<Fact>]
    member _.``REQ-JE-4.4 voidJournalEntryOrchestration rejects void with empty reason`` () =
        // wiring test only — component-level rejection coverage lives in
        // Tests.Isolated (REQ-JE-1.54). Orchestrator error path commits nothing —
        // the fixture JE is untouched.
        let envelope = AuditEnvelope.create JournalEntryVoid
        let emptyReason = { secondaryJournalEntryId = None; commentText = "" }
        let result = voidJournalEntryOrchestration envelope emptyReason fixture.Data.basicJeId
        Assert.True(Result.isError result)

    // =============================================================================
    // Void — balance exclusion
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-4.7 voided entry lines are excluded from account balance computation`` () =
        let linesWithVoided = JournalEntryLine.fetchByAccountId None false fixture.Data.entertainment5650Id
        let linesWithoutVoided = JournalEntryLine.fetchByAccountId None true fixture.Data.entertainment5650Id
        match linesWithVoided, linesWithoutVoided with
        | Ok allLines, Ok nonVoidedLines ->
            Assert.True((allLines |> List.length) > (nonVoidedLines |> List.length))
        | Error e, _ -> Assert.Fail $"Fetch all lines failed: {e}"
        | _, Error e -> Assert.Fail $"Fetch non-voided lines failed: {e}"
