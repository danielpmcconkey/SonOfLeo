namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
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
    // REQ-STG-5.3 — A parser-assigned account is not overridden
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.3 a parser-assigned account survives classification even though the rule that classified its sibling line matches the same entry description`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-001 arrives with a parser-assigned credit line and a null debit line
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let lineOfType lt = entry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = lt)
                (* Classification matches on the entry's description, which both lines share.
                   The debit sibling carrying a rule id is what proves a rule was available to
                   this entry -- without it, an untouched credit line is equally well explained
                   by the classifier having found nothing to match. *)
                Assert.True(lineOfType Debit |> StageEntryLine.classificationRuleId |> Option.isSome)
                Assert.Equal(Some fixture.Data.moneyMarket1270Id, lineOfType Credit |> StageEntryLine.accountId)
                Assert.True(lineOfType Credit |> StageEntryLine.classificationRuleId |> Option.isNone)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-5.4 — Single match assigns the account and records which rule did it
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.4 a line that arrives with no account takes the account of the single rule that matched it, and records that rule's id`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-001 DoorDash: the debit line arrives null and only the DoorDash rule matches it
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let debitLine =
                    entry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                (* Naming the rule is the point: a classifier that lands the right account while
                   stamping some other rule's id breaks the provenance link and nothing else
                   in the suite would notice. *)
                let ruleNameOf r = r |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value
                let doorDashRule =
                    fixture.Data.classificationRules
                    |> List.find (fun r -> ruleNameOf r = "Source = TestBank && Desc = DoorDash then 5350")
                Assert.Equal(Some fixture.Data.food5350Id, debitLine |> StageEntryLine.accountId)
                Assert.Equal(
                    Some (doorDashRule |> ClassificationRule.classificationRuleId),
                    debitLine |> StageEntryLine.classificationRuleId)
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
                    let! codeStr =
                        winner.accountId
                        |> ``convert AccountId to AccountCodeString`` context
                    Assert.Equal("F-5350", codeStr)
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


    // =========================================================================
    // Every line whose account code is null gets evaluated, not just the first
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.2 an entry whose two lines both arrive with no account and match different rules has each line assigned its own rule's account`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-011 is the only staged group whose lines both arrive null. The TestSplitBank
                // rules discriminate on line type, so the two lines must resolve differently: a
                // classifier that stopped after the first null line leaves the Credit null, and one
                // that assigned per entry rather than per line puts F-5350 on both.
                let entry =
                    fullResult.stagedEntries
                    |> StageTestData.findByDescription "SPLIT TRANSFER UNKNOWN BOTH SIDES"
                let idOfLineType lt =
                    entry
                    |> lines
                    |> List.find (fun l -> l |> StageEntryLine.lineType = lt)
                    |> StageEntryLine.accountId
                Assert.Equal(Some fixture.Data.food5350Id, idOfLineType Debit)
                Assert.Equal(Some fixture.Data.entertainment5650Id, idOfLineType Credit)
            })
        |> railroadWrapper
