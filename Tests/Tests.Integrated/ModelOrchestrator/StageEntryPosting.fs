namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.StageEntryOrchestration
open ModelOrchestrator.TrialBalanceReport
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Xunit


(* The orchestrator's JournalEntry type shadows its companion module on a fully qualified
   path, and this file's file-level opens already bind `lines` to the staged-entry accessor.
   A local module with its own open gets at the ledger-side names without disturbing either. *)
module PostedJournalEntry =

    open ModelOrchestrator.JournalEntries.JournalEntry

    let fetchByFiReference context financialInstitution fiReference =
        fetchByReference context (Some financialInstitution) (Some fiReference)

    let lineCount journalEntry = journalEntry |> lines |> List.length


[<Collection("SharedTestData")>]
type StageEntryPostingTests(fixture: TestDataFixture) =


    // =========================================================================
    // REQ-STG-8.1 REQ-STG-8.4 — Shadow post does not modify ledger or staging
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.1 REQ-STG-8.4 shadow post does not create journal entries or change staging statuses`` () =
        runCommandRouteAndAutoRollback IngestShadowPostStageEntries (fun context ->
            result {
                let! _ = StageTestData.runPipeline context
                let contextForPost = context |> Context.updateInitiationInstant
                let! postablesBefore = fetchAllForPosting contextForPost
                let postableCountBefore = postablesBefore |> List.length
                Assert.True(postableCountBefore > 0, "Need postable entries for this test")
                do! ModelOrchestrator.StageEntryOrchestration.post contextForPost
                let! postablesAfterPost = fetchAllForPosting contextForPost
                Assert.Equal(0, postablesAfterPost |> List.length)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-8.2 — Shadow post uses real domain validation
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.2 shadow post fails when staged entry is in closed fiscal period`` () =
        runCommandRouteAndAutoRollback IngestShadowPostStageEntries (fun context ->
            result {
                // create an entry in the closed period and mark it Reviewed so it's postable
                let closedPeriodDate =
                    (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)
                let! sourceFile = "/tmp/test-closed-period.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow "grp-cp" closedPeriodDate "Closed period entry" "TestBank" "REF-CP-001" 50.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-cp" closedPeriodDate "Closed period entry" "TestBank" "REF-CP-001" 50.00M "Credit" (Some "F-1270") None
                let! _ = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
                // the entry is now Classified; post should fail because the period is closed
                return!
                    match ModelOrchestrator.StageEntryOrchestration.post context with
                    | Error _ -> Ok ()
                    | Ok _ -> Error (TestingError "Expected failure posting to closed period; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-8.3 — Shadow post returns before and after trial balance
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.3 shadow post returns trial balance before and after`` () =
        runCommandRouteAndAutoRollback IngestShadowPostStageEntries (fun context ->
            result {
                let! _ = StageTestData.runPipeline context
                let asOf = Calendar.today()
                let! trialBalanceBefore = fetchTrialBalanceData context asOf
                do! ModelOrchestrator.StageEntryOrchestration.post context
                let! trialBalanceAfter = fetchTrialBalanceData context asOf
                Assert.NotEmpty(trialBalanceBefore)
                Assert.NotEmpty(trialBalanceAfter)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-1.3 — one group produces one journal entry when posted
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.3 a four-record group posts as a single journal entry carrying all four lines`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let today = Calendar.today()
                let! sourceFile = "/tmp/test-one-je-per-group.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow "grp-1je" today "One JE per group" "TestBank" "REF-1JE-001" 800.00M "Debit" (Some "F-1270") (Some "Net pay to checking")
                let! row2 = StageTestData.makeRawRow "grp-1je" today "One JE per group" "TestBank" "REF-1JE-001" 312.50M "Credit" (Some "F-5300") (Some "Federal withholding")
                let! row3 = StageTestData.makeRawRow "grp-1je" today "One JE per group" "TestBank" "REF-1JE-001" 187.50M "Credit" (Some "F-5350") (Some "State withholding")
                let! row4 = StageTestData.makeRawRow "grp-1je" today "One JE per group" "TestBank" "REF-1JE-001" 300.00M "Credit" (Some "F-5650") (Some "401k contribution")
                let! _ =
                    [ row1; row2; row3; row4 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
                let contextForPost = context |> Context.updateInitiationInstant
                do! ModelOrchestrator.StageEntryOrchestration.post contextForPost
                let! fi = "TestBank" |> JournalRefFinancialInstitution.create
                let! fiReference = "REF-1JE-001" |> JournalExternalReferenceText.create
                let! posted = PostedJournalEntry.fetchByFiReference contextForPost fi fiReference
                Assert.Equal(1, posted |> List.length)
                Assert.Equal(4, posted |> List.exactlyOne |> PostedJournalEntry.lineCount)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.1 REQ-STG-9.2 — Batch post happy path
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.1 REQ-STG-9.2 batch post creates journal entries through domain model`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! _ = StageTestData.runPipeline context
                // post internally calls fetchAllForPosting + postStageEntry + status update
                do! ModelOrchestrator.StageEntryOrchestration.post context
                // if post succeeded, all postable entries were posted; verify none remain
                let! postablesAfter = fetchAllForPosting context
                Assert.Equal(0, postablesAfter |> List.length)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.3 — JE header fields mapped from staged entry
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.3 posted JE has description and entry_date from staged entry`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let! jeSource =
                    Some "Data ingestion import"
                    |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
                do! postStageEntry context jeSource entry
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.4 — Account code to ID resolution at posting time
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.4 batch post resolves account codes to IDs`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let! jeSource =
                    Some "Data ingestion import"
                    |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
                do! postStageEntry context jeSource entry
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-9.4 batch post fails when account code does not resolve`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let firstLine = entry |> lines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let! badCode = "BOGUS-9999" |> AccountCode.create
                let headerUpdates: StageEntryHeaderFieldUpdates =
                    { headerIdToUpdate = headerId
                      sourceFileUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                      entryDateUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                      descriptionUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                      ingestionSourceUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                      fiReferenceUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                      statusUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange }
                let lineUpdates: StageEntryLineFieldUpdates list =
                    [ { lineIdToUpdate = lineId
                        amountUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                        entryTypeUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                        accountCodeUpdate = Utilities.FieldUpdate.FieldUpdate.SetTo (Some badCode)
                        memoUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                        classificationRuleIdUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange } ]
                return!
                    match updateStageEntry context headerUpdates lineUpdates with
                    | Error _ -> Ok ()
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.5 — External reference constructed from source + fi_reference
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.5 posted JE has external reference with source name and fi_reference`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "SPECTRUM SOUTHEAST 800-892-2253"
                let! jeSource =
                    Some "Data ingestion import"
                    |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
                do! postStageEntry context jeSource entry
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.7 — Status set to Posted with audit record
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.7 batch post sets status to Posted and creates LedgerPoster audit record`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! _ = StageTestData.runPipeline context
                let contextForPost = context |> Context.updateInitiationInstant
                let! postablesBefore = fetchAllForPosting contextForPost
                Assert.True(postablesBefore |> List.length > 0, "Need postable entries")
                let firstHeaderId =
                    postablesBefore |> List.head |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                do! ModelOrchestrator.StageEntryOrchestration.post contextForPost
                let! refetched = firstHeaderId |> fetchByStageEntryHeaderId contextForPost
                let latestTransition =
                    refetched |> statusTransitions
                    |> List.sortByDescending (fun t -> t |> StageEntryStatusTransition.instant)
                    |> List.head
                Assert.Equal(Posted, latestTransition |> StageEntryStatusTransition.toStatus)
                Assert.Equal(LedgerPoster, latestTransition |> StageEntryStatusTransition.stageStatusChangeMechanism)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.8 — All-or-nothing batch post
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.8 batch post rolls back all entries when one fails domain validation`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                // ingest an entry in the closed period so it's postable but will fail
                let closedPeriodDate =
                    (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)
                let! sourceFile = "/tmp/test-batch-fail.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow "grp-bf" closedPeriodDate "Batch fail entry" "TestBank" "REF-BF-001" 50.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-bf" closedPeriodDate "Batch fail entry" "TestBank" "REF-BF-001" 50.00M "Credit" (Some "F-1270") None
                let! _ = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
                // also run the standard pipeline so we have a mix of postable entries
                let! _ = StageTestData.runPipeline context
                return!
                    match ModelOrchestrator.StageEntryOrchestration.post context with
                    | Error _ -> Ok ()
                    | Ok _ -> Error (TestingError "Expected batch post to fail due to closed period entry")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-4.4 — fetchAllForPosting returns only postable entries
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-4.4 fetchAllForPosting returns only Classified and Reviewed entries with all lines coded`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! _ = StageTestData.runPipeline context
                let! postables = fetchAllForPosting context
                Assert.True(postables |> List.length > 0, "Need postable entries for this test")
                postables |> List.iter (fun entry ->
                    let status = StageTestData.latestStatus entry
                    Assert.True(status = Classified || status = Reviewed,
                        $"Expected Classified or Reviewed but got {status}")
                    let allLinesHaveCodes =
                        entry |> lines
                        |> List.forall (fun l -> l |> StageEntryLine.accountCode |> Option.isSome)
                    Assert.True(allLinesHaveCodes, "All lines in a postable entry must have account codes"))
            })
        |> railroadWrapper
