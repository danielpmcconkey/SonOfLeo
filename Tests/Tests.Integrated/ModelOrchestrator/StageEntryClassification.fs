namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryStatusTransition
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent


[<Collection("SharedTestData")>]
type StageEntryClassificationTests(fixture: TestDataFixture) =


    // =========================================================================
    // REQ-STG-5.1 — Classification runs against Ingested entries
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.1 classifyStagedEntries processes entries with status Ingested`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                Assert.NotEmpty(fullResult.classificationResults)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.3 — Non-null account_code lines not modified
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.3 classification does not modify lines with non-null account_code`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-001 has a parser-assigned credit line (F-1270) — should not be touched
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let creditLine =
                    entry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                Assert.Equal(Some "F-1270", creditLine |> StageEntryLine.accountCode |> Option.map AccountCode.value)
                Assert.True(creditLine |> StageEntryLine.classificationRuleId |> Option.isNone)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.4 — Single match assigns code and records rule ID
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.4 single rule match assigns account code and classification_rule_id to line`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-001 DoorDash: debit line should be classified to F-5350 with a rule ID
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let debitLine =
                    entry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                Assert.Equal(Some "F-5350", debitLine |> StageEntryLine.accountCode |> Option.map AccountCode.value)
                Assert.True(debitLine |> StageEntryLine.classificationRuleId |> Option.isSome)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.5 — Multiple matches with clear priority winner
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.5 multiple rule matches with clear priority winner assigns winner`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-001 DoorDash matches both generic TestBank (1000→F-5300) and DoorDash (100→F-5350)
                let debitResults =
                    fullResult.classificationResults
                    |> List.filter (fun cr ->
                        cr.candidate.lineType = Debit
                        && cr.candidate.description |> JournalEntryDescription.value = "DD DoorDash Order 8431927")
                Assert.NotEmpty(debitResults)
                match (debitResults |> List.head).outcome with
                | ManyMatchesClearWinner (winner, _) ->
                    Assert.Equal("F-5350", winner.code |> AccountCode.value)
                | other -> Assert.Fail $"Expected ManyMatchesClearWinner but got {other}"
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.6 — Multiple matches with tied priority sets Conflict
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.6 tied priority rule matches set entry status to Conflict`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-006 ALLSTATE: two rules at priority 500 → Conflict
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "ALLSTATE INS AUTOPAY"
                Assert.Equal(Conflict, StageTestData.latestStatus entry)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.7 — No rule match sets NoMatch
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.7 no rule match sets entry status to NoMatch`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-005: TestSavings source has no rules → NoMatch
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "TOTALLY UNKNOWN MERCHANT NOWHERE"
                Assert.Equal(StagedEntryStatus.NoMatch, StageTestData.latestStatus entry)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.8 — Fully parser-assigned entries skip classification
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.8 entry with all lines parser-assigned transitions to Classified with no MatchCandidates`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-007 payroll: all 4 lines parser-assigned → Classified, no classification results
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "PAYROLL DEPOSIT ACME CORP"
                Assert.Equal(Classified, StageTestData.latestStatus entry)
                let entryHeaderId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let classResultsForEntry =
                    fullResult.classificationResults
                    |> List.filter (fun cr -> cr.candidate.headerIdOfCandidate = entryHeaderId)
                Assert.Empty(classResultsForEntry)
            })
        |> railroadWrapper
