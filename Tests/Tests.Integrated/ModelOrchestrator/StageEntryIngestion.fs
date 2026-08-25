namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.BaseStageRaw
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


module StageTestData =

    let makeRawRow context groupIdStr (date: NodaTime.LocalDate) descStr sourceStr refStr amount lineTypeStr codeStr memoStr =
        result {
            let! groupId = groupIdStr |> BaseStageEntryGroupId.create
            let! desc = descStr |> JournalEntryDescription.create
            let! source = sourceStr |> JournalRefFinancialInstitution.create
            let! ref = refStr |> JournalExternalReferenceText.create
            let! money = amount |> Money.fromDecimal
            let! lt = lineTypeStr |> JournalEntryLineType.fromString
            let! accountId = codeStr |> ``convert AccountCodeString Option to AccountId Option`` context
            let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
            return {
                baseStageEntryGroupId = groupId
                entryDate = date
                description = desc
                fiSource = source
                fiReference = ref
                amount = money
                entryType = lt
                accountId = accountId
                memo = memo }
        }

    let buildTestRows (context: Context.Context) =
        let today = Calendar.today()
        [
            makeRawRow context "grp-001" (today.PlusDays(-3)) "DD DoorDash Order 8431927" "TestBank" "REF-DD-001" 32.47M "Debit" None None
            makeRawRow context "grp-001" (today.PlusDays(-3)) "DD DoorDash Order 8431927" "TestBank" "REF-DD-001" 32.47M "Credit" (Some "F-1270") None
            makeRawRow context "grp-002" (today.PlusDays(-2)) "MARATHON PETRO 7218 ANYTOWN US" "TestBank" "REF-GAS-001" 48.12M "Debit" None None
            makeRawRow context "grp-002" (today.PlusDays(-2)) "MARATHON PETRO 7218 ANYTOWN US" "TestBank" "REF-GAS-001" 48.12M "Credit" (Some "F-1270") None
            makeRawRow context "grp-003" (today.PlusDays(-2)) "HARRIS TEETER 0381 ANYTOWN US" "TestBank" "REF-GROC-001" 127.83M "Debit" None None
            makeRawRow context "grp-003" (today.PlusDays(-2)) "HARRIS TEETER 0381 ANYTOWN US" "TestBank" "REF-GROC-001" 127.83M "Credit" (Some "F-1270") None
            makeRawRow context "grp-004" (today.PlusDays(-1)) "SPECTRUM SOUTHEAST 800-892-2253" "TestBank" "REF-CABLE-001" 79.99M "Debit" None None
            makeRawRow context "grp-004" (today.PlusDays(-1)) "SPECTRUM SOUTHEAST 800-892-2253" "TestBank" "REF-CABLE-001" 79.99M "Credit" (Some "F-1270") None
            makeRawRow context "grp-005" (today.PlusDays(-1)) "TOTALLY UNKNOWN MERCHANT NOWHERE" "TestSavings" "REF-UNK-001" 15.00M "Debit" None None
            makeRawRow context "grp-005" (today.PlusDays(-1)) "TOTALLY UNKNOWN MERCHANT NOWHERE" "TestSavings" "REF-UNK-001" 15.00M "Credit" (Some "F-1270") None
            makeRawRow context "grp-006" today "ALLSTATE INS AUTOPAY" "TestBank" "REF-INS-001" 142.50M "Debit" None None
            makeRawRow context "grp-006" today "ALLSTATE INS AUTOPAY" "TestBank" "REF-INS-001" 142.50M "Credit" (Some "F-1270") None
            makeRawRow context "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 800.00M "Debit" (Some "F-1270") (Some "Net pay to checking")
            makeRawRow context "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 312.50M "Credit" (Some "F-5300") (Some "Federal withholding")
            makeRawRow context "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 187.50M "Credit" (Some "F-5350") (Some "State withholding")
            makeRawRow context "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 300.00M "Credit" (Some "F-5650") (Some "401k contribution")
            makeRawRow context "grp-008" (today.PlusDays(-2)) "Fixture JE with reference" "TestBank" "TXN-001" 65.00M "Debit" None None
            makeRawRow context "grp-008" (today.PlusDays(-2)) "Fixture JE with reference" "TestBank" "TXN-001" 65.00M "Credit" (Some "F-1270") None
            makeRawRow context "grp-009" (today.PlusDays(-2)) "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Debit" None None
            makeRawRow context "grp-009" (today.PlusDays(-2)) "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Credit" (Some "F-1270") None
            makeRawRow context "grp-010" today "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Debit" None None
            makeRawRow context "grp-010" today "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Credit" (Some "F-1270") None
            // grp-011 — the only group whose lines both arrive with a null account code. The
            // TestSplitBank rules discriminate on line type, so the two lines resolve to two
            // different accounts. Both halves matter: a classifier that stopped after the first
            // null line leaves the Credit null, and one that assigned per entry rather than per
            // line puts the same code on both.
            makeRawRow context "grp-011" (today.PlusDays(-1)) "SPLIT TRANSFER UNKNOWN BOTH SIDES" "TestSplitBank" "REF-SPLIT-001" 75.00M "Debit" None None
            makeRawRow context "grp-011" (today.PlusDays(-1)) "SPLIT TRANSFER UNKNOWN BOTH SIDES" "TestSplitBank" "REF-SPLIT-001" 75.00M "Credit" None None
            // grp-012 — both lines arrive null against MixedOutcomeBank, whose rules are two
            // tied Debit-only rules and nothing for Credit. The debit line conflicts and the
            // credit line matches nothing, which is the only place in the fixture where one
            // entry produces both outcomes.
            makeRawRow context "grp-012" (today.PlusDays(-1)) "MIXED OUTCOME BOTH SIDES NULL" "MixedOutcomeBank" "REF-MIXED-001" 60.00M "Debit" None None
            makeRawRow context "grp-012" (today.PlusDays(-1)) "MIXED OUTCOME BOTH SIDES NULL" "MixedOutcomeBank" "REF-MIXED-001" 60.00M "Credit" None None
        ] |> convertListOfResultsToResultsList

    let runPipeline context =
        result {
            let! sourceFile = "/tmp/stg-test-checking.jsonl" |> SourceFile.create
            let! rows = buildTestRows context
            return! rows |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
        }

    let findByDescription desc (entries: StageEntry list) =
        entries |> List.find (fun se ->
            se |> stageEntryHeader |> StageEntryHeader.description |> JournalEntryDescription.value = desc)

    let latestStatus (entry: StageEntry) =
        entry |> statusTransitions
        |> List.sortByDescending (fun t -> t |> StageEntryStatusTransition.instant)
        |> List.head
        |> StageEntryStatusTransition.toStatus

    /// How many times the dedup pass has flagged this entry. Latest status cannot answer
    /// that on its own — an entry already sitting at Duplicate looks identical whether the
    /// next pass skipped it or flagged it a second time.
    let duplicateTransitionCount (entry: StageEntry) =
        entry |> statusTransitions
        |> List.filter (fun t -> t |> StageEntryStatusTransition.toStatus = StagedEntryStatus.Duplicate)
        |> List.length


[<Collection("SharedTestData")>]
type StageEntryIngestionTests(fixture: TestDataFixture) =

    let today = Calendar.today()


    // =========================================================================
    // REQ-STG-3.1 REQ-STG-3.4 REQ-STG-3.9 — Ingestion happy path
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-3.1 REQ-STG-3.4 REQ-STG-3.9 ingestRawToStageThenDeduplicateAndClassify happy path creates entries with correct fields`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                Assert.Equal(12, fullResult.stagedEntries |> List.length)
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let transitions = entry |> statusTransitions
                Assert.NotEmpty(transitions)
                let initialTransition =
                    transitions |> List.sortBy (fun t -> t |> StageEntryStatusTransition.instant) |> List.head
                Assert.Equal(Ingested, initialTransition |> StageEntryStatusTransition.toStatus)
                Assert.True(initialTransition |> StageEntryStatusTransition.fromStatus |> Option.isNone)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-2.2 through 2.7, 2.9 — Staged entry field population
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-2.2 REQ-STG-2.3 REQ-STG-2.4 REQ-STG-2.5 REQ-STG-2.6 REQ-STG-2.7 REQ-STG-2.9 ingested entry has correct header fields`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let header = entry |> stageEntryHeader
                Assert.Equal(today.PlusDays(-3), header |> StageEntryHeader.entryDate)
                Assert.Equal("DD DoorDash Order 8431927", header |> StageEntryHeader.description |> JournalEntryDescription.value)
                Assert.Equal("TestBank", header |> StageEntryHeader.ingestionSource |> IngestionSource.name |> JournalRefFinancialInstitution.value)
                Assert.Equal("REF-DD-001", header |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value)
                Assert.Equal("/tmp/stg-test-checking.jsonl", header |> StageEntryHeader.sourceFile |> SourceFile.value)
                (* grp-001 is built from exactly two raw rows, so it owes exactly two lines.
                   A floor would tolerate the line duplication it is meant to catch. *)
                Assert.Equal(2, entry |> lines |> List.length)
                (* REQ-STG-2.7: status is no longer a column, so "cannot be null" means the
                   derived value is present and agrees with the entry's own latest transition.
                   The header reads it through the audit CTE and the transition list is
                   fetched separately, so the two only agree if the derivation is right. *)
                Assert.Equal<StagedEntryStatus option>(
                    Some(entry |> StageTestData.latestStatus),
                    header |> StageEntryHeader.currentStatus)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-2.12 through 2.17 — Staged line field population
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-2.12 REQ-STG-2.13 REQ-STG-2.14 REQ-STG-2.15 REQ-STG-2.16 REQ-STG-2.17 ingested entry has correct line fields`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "PAYROLL DEPOSIT ACME CORP"
                let debitLine =
                    entry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                Assert.Equal(800.00M, debitLine |> StageEntryLine.amount |> Money.amount)
                Assert.Equal(Debit, debitLine |> StageEntryLine.lineType)
                let! codeStr =
                    debitLine
                    |> StageEntryLine.accountId
                    |> ``convert AccountId Option to AccountCodeString Option`` context
                Assert.Equal(Some "F-1270", codeStr)
                Assert.Equal(Some "Net pay to checking", debitLine |> StageEntryLine.memo |> Option.map JournalEntryLineMemo.value)
                // parser-assigned lines have no classification_rule_id
                Assert.True(debitLine |> StageEntryLine.classificationRuleId |> Option.isNone)
                // 2.17 — balanced
                let totalDebits =
                    entry |> lines
                    |> List.filter (fun l -> l |> StageEntryLine.lineType = Debit)
                    |> List.sumBy (fun l -> l |> StageEntryLine.amount |> Money.amount)
                let totalCredits =
                    entry |> lines
                    |> List.filter (fun l -> l |> StageEntryLine.lineType = Credit)
                    |> List.sumBy (fun l -> l |> StageEntryLine.amount |> Money.amount)
                Assert.Equal(totalDebits, totalCredits)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-2.20 through 2.23 — Audit record field population
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-2.20 REQ-STG-2.21 REQ-STG-2.22 REQ-STG-2.23 ingested entry has correct audit record`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "DD DoorDash Order 8431927"
                let initialTransition =
                    entry |> statusTransitions
                    |> List.sortBy (fun t -> t |> StageEntryStatusTransition.instant)
                    |> List.head
                Assert.True(initialTransition |> StageEntryStatusTransition.fromStatus |> Option.isNone)
                Assert.Equal(Ingested, initialTransition |> StageEntryStatusTransition.toStatus)
                Assert.Equal(StageIngestion, initialTransition |> StageEntryStatusTransition.stageStatusChangeMechanism)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-1.3 — group_id associates records into one economic event
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.3 records sharing a group_id become one staged entry and a distinct group_id becomes another`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-grouping.jsonl" |> SourceFile.create
                let! multiRow1 = StageTestData.makeRawRow context "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 800.00M "Debit" (Some "F-1270") (Some "Net pay to checking")
                let! multiRow2 = StageTestData.makeRawRow context "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 312.50M "Credit" (Some "F-5300") (Some "Federal withholding")
                let! multiRow3 = StageTestData.makeRawRow context "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 187.50M "Credit" (Some "F-5350") (Some "State withholding")
                let! multiRow4 = StageTestData.makeRawRow context "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 300.00M "Credit" (Some "F-5650") (Some "401k contribution")
                let! otherRow1 = StageTestData.makeRawRow context "grp-other" today "Separate event" "TestBank" "REF-OTHER-001" 40.00M "Debit" (Some "F-5350") None
                let! otherRow2 = StageTestData.makeRawRow context "grp-other" today "Separate event" "TestBank" "REF-OTHER-001" 40.00M "Credit" (Some "F-1270") None
                let! fullResult =
                    [ multiRow1; multiRow2; multiRow3; multiRow4; otherRow1; otherRow2 ]
                    |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
                Assert.Equal(2, fullResult.stagedEntries |> List.length)
                let multi = fullResult.stagedEntries |> StageTestData.findByDescription "Multi leg event"
                Assert.Equal(4, multi |> lines |> List.length)
                let other = fullResult.stagedEntries |> StageTestData.findByDescription "Separate event"
                Assert.Equal(2, other |> lines |> List.length)
            })
        |> railroadWrapper

    (* "within a single file" is the operative clause: group_id is the parser's local
       association mechanism and is not globally unique, so the same value reappearing in
       a later file associates nothing. *)
    [<Fact>]
    member _.``REQ-STG-1.3 the same group_id in a second file produces a separate staged entry`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! firstFile = "/tmp/test-grouping-file-one.jsonl" |> SourceFile.create
                let! firstRow1 = StageTestData.makeRawRow context "grp-reused" today "First file event" "TestBank" "REF-REUSED-001" 25.00M "Debit" (Some "F-5350") None
                let! firstRow2 = StageTestData.makeRawRow context "grp-reused" today "First file event" "TestBank" "REF-REUSED-001" 25.00M "Credit" (Some "F-1270") None
                let! firstResult = [ firstRow1; firstRow2 ] |> ingestRawToStageThenDeduplicateAndClassify context firstFile
                let contextForSecondFile = context |> Context.updateInitiationInstant
                let! secondFile = "/tmp/test-grouping-file-two.jsonl" |> SourceFile.create
                let! secondRow1 = StageTestData.makeRawRow context "grp-reused" today "Second file event" "TestBank" "REF-REUSED-002" 61.00M "Debit" (Some "F-5350") None
                let! secondRow2 = StageTestData.makeRawRow context "grp-reused" today "Second file event" "TestBank" "REF-REUSED-002" 61.00M "Credit" (Some "F-1270") None
                let! secondResult =
                    [ secondRow1; secondRow2 ] |> ingestRawToStageThenDeduplicateAndClassify contextForSecondFile secondFile
                let firstId = firstResult.stagedEntries |> List.exactlyOne |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let secondId = secondResult.stagedEntries |> List.exactlyOne |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                Assert.True(firstId <> secondId, "A reused group_id in a later file must not join the earlier entry")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-1.13 — Group with inconsistent header fields
    // =========================================================================

    (* The requirement names four fields a group must agree on. Testing one of them leaves the
       other three free to drift: a parser emitting two descriptions under one group_id is a
       different defect from one emitting two dates, and only the date case was covered. Each
       row below differs from the first record in exactly one field, and the alternate fi_source
       is a source that really exists so the failure cannot be a source-resolution error wearing
       the right name. *)
    [<Theory>]
    [<InlineData("entryDate")>]
    [<InlineData("description")>]
    [<InlineData("fiSource")>]
    [<InlineData("fiReference")>]
    member _.``REQ-STG-1.13 ingestRaw rejects a group whose records disagree on any one of the four fields the group must share``
        (field: string)
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-inconsistent.jsonl" |> SourceFile.create
                let secondDate = if field = "entryDate" then today.PlusDays(1) else today
                let secondDescription = if field = "description" then "Inconsistent the second" else "Inconsistent"
                let secondSource = if field = "fiSource" then "TestSavings" else "TestBank"
                let secondReference = if field = "fiReference" then "REF-INC-002" else "REF-INC-001"
                let! row1 = StageTestData.makeRawRow context "grp-inc" today "Inconsistent" "TestBank" "REF-INC-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow context "grp-inc" secondDate secondDescription secondSource secondReference 100.00M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionBaseStageGroupIdDistinctDataViolation _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error when {field} differed within the group: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError $"Expected failure; got success when {field} differed within the group")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-1.14 — Single-record group
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.14 ingestRaw rejects group with only one record`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-single.jsonl" |> SourceFile.create
                let! row = StageTestData.makeRawRow context "grp-one" today "Single record" "TestBank" "REF-ONE-001" 100.00M "Debit" (Some "F-5350") None
                return!
                    match [ row ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionStageEntryInsufficientLines _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-1.15 — Imbalanced debit/credit within group
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.15 ingestRaw rejects group with imbalanced debits and credits`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-imbalanced.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-imb" today "Imbalanced" "TestBank" "REF-IMB-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow context "grp-imb" today "Imbalanced" "TestBank" "REF-IMB-001" 99.99M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionStageEntryDebitCreditMismatch _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-3.3 REQ-STG-3.10 — All-or-nothing ingestion
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-3.3 REQ-STG-3.10 one invalid group in a multi-group file rejects entire file`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-all-or-nothing.jsonl" |> SourceFile.create
                let! validRow1 = StageTestData.makeRawRow context "grp-ok" today "Valid group" "TestBank" "REF-OK-001" 100.00M "Debit" (Some "F-5350") None
                let! validRow2 = StageTestData.makeRawRow context "grp-ok" today "Valid group" "TestBank" "REF-OK-001" 100.00M "Credit" (Some "F-1270") None
                let! badRow = StageTestData.makeRawRow context "grp-bad" today "Bad group" "TestBank" "REF-BAD-001" 50.00M "Debit" (Some "F-5350") None
                return!
                    match [ validRow1; validRow2; badRow ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionStageEntryInsufficientLines _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-3.6 — Unknown fi_source rejects file
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-3.6 ingestRaw rejects file when fi_source does not resolve to a known source`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-bad-source.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-src" today "Bad source" "NonExistentBank" "REF-SRC-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow context "grp-src" today "Bad source" "NonExistentBank" "REF-SRC-001" 100.00M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionSourceNameNotFound _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-3.2 — Validation rejects with typed error
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-3.2 validation failure returns typed error identifying the violation`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-typed-error.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-err" today "Typed error" "TestBank" "REF-ERR-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow context "grp-err" today "Typed error" "TestBank" "REF-ERR-001" 50.00M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionStageEntryDebitCreditMismatch (d, c)) ->
                        Assert.Equal(100.00M, d)
                        Assert.Equal(50.00M, c)
                        Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-7.1 REQ-STG-7.2 — Stage-vs-stage dedup
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-7.1 REQ-STG-7.2 dedup flags entry with same source and fi_reference as existing staged entry`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-009 and grp-010 share source+ref; one should be flagged as duplicate
                let ddEntries = fullResult.stagedEntries |> List.filter (fun se ->
                    se |> stageEntryHeader |> StageEntryHeader.description |> JournalEntryDescription.value = "DD DoorDash Order 9917223")
                Assert.Equal(2, ddEntries |> List.length)
                let dupCount =
                    ddEntries
                    |> List.filter (fun se -> se |> stageEntryHeader |> StageEntryHeader.currentStatus = Some Duplicate)
                    |> List.length
                Assert.Equal(1, dupCount)
            })
        |> railroadWrapper


    [<Fact>]
    member _.``REQ-STG-7.2 a dedup pass leaves a Reviewed entry with no Duplicate transition while flagging the Ingested entry that shares its source and fi reference`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                (* Asserting only that the Reviewed entry survives would be satisfied by a
                   dedup pass that flags nothing at all, so the same run has to flag the
                   entry that is still Ingested. *)
                let! sourceFile1 = "/tmp/test-reviewed-first.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-rev" today "Reviewed dedup subject" "TestBank" "REF-REVIEWED-001" 61.00M "Debit" (Some "F-5650") None
                let! row2 = StageTestData.makeRawRow context "grp-rev" today "Reviewed dedup subject" "TestBank" "REF-REVIEWED-001" 61.00M "Credit" (Some "F-1270") None
                let! firstResult = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile1
                let firstEntry = firstResult.stagedEntries |> List.exactlyOne
                let firstHeaderId = firstEntry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

                (* The operator reviews it. Classified -> Reviewed is the transition that puts
                   it beyond the dedup engine's authority. *)
                let contextForReview = context |> Context.updateInitiationInstant
                do! firstHeaderId
                    |> StageEntryHeader.updateHeaderStatus contextForReview Reviewed Operator

                let contextForReimport = contextForReview |> Context.updateInitiationInstant
                let! sourceFile2 = "/tmp/test-reviewed-second.jsonl" |> SourceFile.create
                let! row3 = StageTestData.makeRawRow context "grp-rev2" today "Reviewed dedup rerun" "TestBank" "REF-REVIEWED-001" 61.00M "Debit" (Some "F-5650") None
                let! row4 = StageTestData.makeRawRow context "grp-rev2" today "Reviewed dedup rerun" "TestBank" "REF-REVIEWED-001" 61.00M "Credit" (Some "F-1270") None
                let! secondResult = [ row3; row4 ] |> ingestRawToStageThenDeduplicateAndClassify contextForReimport sourceFile2

                let secondEntry = secondResult.stagedEntries |> List.exactlyOne
                Assert.Equal(Duplicate, StageTestData.latestStatus secondEntry)

                let! reviewedAfter = firstHeaderId |> fetchByStageEntryHeaderId contextForReimport
                (* Status alone would still read Reviewed if the pass had flagged it and the
                   status write had then failed. The audit trail is what rules that out. *)
                Assert.Equal(0, StageTestData.duplicateTransitionCount reviewedAfter)
                Assert.Equal(Reviewed, StageTestData.latestStatus reviewedAfter)
            })
        |> railroadWrapper



    [<Fact>]
    member _.``REQ-STG-7.2 a dedup pass leaves a Posted entry with no Duplicate transition while flagging the Ingested entry that shares its source and fi reference`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                (* Asserting only that the Posted entry survives would be satisfied by a dedup
                   pass that flags nothing at all, so the same run has to flag the entry that
                   is still Ingested.

                   The entry is posted for real rather than moved to Posted by hand, and that
                   is load-bearing. A hand-forced Posted entry that arrived first sits at
                   ordinal 1 of its source-and-reference partition, and the dedup query passes
                   over ordinal 1 on that ground alone — the status exclusion never gets a
                   chance to matter, so the test would stay green with the exclusion deleted.
                   Posting writes a journal entry carrying this entry's own source and
                   fi_reference, which makes the ledger arm of the match true for it. From
                   that point the status exclusion is the only thing standing between it and
                   being re-flagged on every pass that follows. *)
                let! sourceFile1 = "/tmp/test-posted-first.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-post" today "Posted dedup subject" "TestBank" "REF-POSTED-001" 63.00M "Debit" (Some "F-5650") None
                let! row2 = StageTestData.makeRawRow context "grp-post" today "Posted dedup subject" "TestBank" "REF-POSTED-001" 63.00M "Credit" (Some "F-1270") None
                let! firstResult = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile1
                let firstEntry = firstResult.stagedEntries |> List.exactlyOne
                let firstHeaderId = firstEntry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

                (* Both lines arrived coded, so classification left it Classified — the only
                   status the entry can legally reach Posted from, per the permitted
                   transition table in spec section 4. *)
                Assert.Equal(Classified, StageTestData.latestStatus firstEntry)
                Assert.Equal(0, StageTestData.duplicateTransitionCount firstEntry)
                System.Threading.Thread.Sleep(10)
                let contextForPost = context |> Context.updateInitiationInstant
                (* Batch post rather than postStageEntry: postStageEntry writes the journal
                   entry but leaves the status alone, and this test needs both halves — the
                   ledger row that makes the entry matchable and the Posted status that is
                   supposed to protect it. The entry ingested above is the only postable one
                   in this rolled-back transaction. *)
                do! ModelOrchestrator.StageEntryOrchestration.post contextForPost

                System.Threading.Thread.Sleep(10)
                let contextForReimport = contextForPost |> Context.updateInitiationInstant
                let! sourceFile2 = "/tmp/test-posted-second.jsonl" |> SourceFile.create
                let! row3 = StageTestData.makeRawRow context "grp-post2" today "Posted dedup rerun" "TestBank" "REF-POSTED-001" 63.00M "Debit" (Some "F-5650") None
                let! row4 = StageTestData.makeRawRow context "grp-post2" today "Posted dedup rerun" "TestBank" "REF-POSTED-001" 63.00M "Credit" (Some "F-1270") None
                let! secondResult = [ row3; row4 ] |> ingestRawToStageThenDeduplicateAndClassify contextForReimport sourceFile2

                let secondEntry = secondResult.stagedEntries |> List.exactlyOne
                Assert.Equal(Duplicate, StageTestData.latestStatus secondEntry)

                let! postedAfter = firstHeaderId |> fetchByStageEntryHeaderId contextForReimport
                Assert.Equal(0, StageTestData.duplicateTransitionCount postedAfter)
                Assert.Equal(Posted, StageTestData.latestStatus postedAfter)
            })
        |> railroadWrapper


    [<Fact>]
    member _.``REQ-STG-7.2 a second dedup pass over an entry already flagged Duplicate leaves it holding exactly one Duplicate transition and still flags the newly arrived entry sharing its key`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                (* Three separate ingests rather than one batch of three. Inside a single batch
                   every entry earns its Ingested transition at the same instant, so the dedup
                   query's ordinal falls through to a uuid tiebreak and which member of a pair
                   gets flagged is not deterministic. Separate passes make arrival order decide
                   it, which is what lets this test name the entry it is talking about. *)
                let ingestOne label groupId ctx =
                    result {
                        let! sourceFile = $"/tmp/test-redup-{label}.jsonl" |> SourceFile.create
                        let! debit = StageTestData.makeRawRow ctx groupId today $"Duplicate dedup {label}" "TestBank" "REF-REDUP-001" 44.00M "Debit" (Some "F-5650") None
                        let! credit = StageTestData.makeRawRow ctx groupId today $"Duplicate dedup {label}" "TestBank" "REF-REDUP-001" 44.00M "Credit" (Some "F-1270") None
                        let! ingested = [ debit; credit ] |> ingestRawToStageThenDeduplicateAndClassify ctx sourceFile
                        return ingested.stagedEntries |> List.exactlyOne
                    }

                let! first = ingestOne "first" "grp-redup1" context
                Assert.Equal(0, StageTestData.duplicateTransitionCount first)

                System.Threading.Thread.Sleep(10)
                let contextSecond = context |> Context.updateInitiationInstant
                let! second = ingestOne "second" "grp-redup2" contextSecond
                Assert.Equal(Duplicate, StageTestData.latestStatus second)
                let secondHeaderId = second |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

                System.Threading.Thread.Sleep(10)
                let contextThird = contextSecond |> Context.updateInitiationInstant
                let! third = ingestOne "third" "grp-redup3" contextThird
                Assert.Equal(Duplicate, StageTestData.latestStatus third)

                (* Duplicate -> Duplicate is absent from the permitted transition table in
                   spec section 4, so a pass
                   that failed to exclude the already-flagged entry would either abort the run
                   or grow its audit trail. The run reaching this line rules out the first; the
                   count rules out the second. *)
                let! secondAfter = secondHeaderId |> fetchByStageEntryHeaderId contextThird
                Assert.Equal(1, StageTestData.duplicateTransitionCount secondAfter)
            })
        |> railroadWrapper


    [<Fact>]
    member _.``REQ-STG-7.2 entries sharing only source or only fi reference gain no Duplicate transition in a pass that flags the pair sharing both`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                (* The pair sharing both keys rides along in the same batch so that "the pass
                   flagged nothing" cannot be what makes the four half-match assertions pass.
                   Every fi_reference here is unique to this test, so nothing matches a ledger
                   external reference and REQ-STG-7.3 stays out of it. *)
                let! sourceFile = "/tmp/test-partial-key.jsonl" |> SourceFile.create
                let makeEntry groupId desc source fiRef =
                    [ StageTestData.makeRawRow context groupId today desc source fiRef 21.00M "Debit" (Some "F-5650") None
                      StageTestData.makeRawRow context groupId today desc source fiRef 21.00M "Credit" (Some "F-1270") None ]
                let! rows =
                    [ makeEntry "grp-pk1" "Partial key same source one" "TestBank" "REF-PARTIAL-A1"
                      makeEntry "grp-pk2" "Partial key same source two" "TestBank" "REF-PARTIAL-A2"
                      makeEntry "grp-pk3" "Partial key same reference one" "TestBank" "REF-PARTIAL-B"
                      makeEntry "grp-pk4" "Partial key same reference two" "TestSavings" "REF-PARTIAL-B"
                      makeEntry "grp-pk5" "Partial key both shared one" "TestBank" "REF-PARTIAL-C"
                      makeEntry "grp-pk6" "Partial key both shared two" "TestBank" "REF-PARTIAL-C" ]
                    |> List.concat
                    |> convertListOfResultsToResultsList
                let! fullResult = rows |> ingestRawToStageThenDeduplicateAndClassify context sourceFile

                let entryNamed desc = fullResult.stagedEntries |> StageTestData.findByDescription desc
                [ "Partial key same source one"
                  "Partial key same source two"
                  "Partial key same reference one"
                  "Partial key same reference two" ]
                |> List.iter (fun desc ->
                    Assert.Equal(0, entryNamed desc |> StageTestData.duplicateTransitionCount))

                (* Which member of the shared pair gets flagged is decided by a uuid tiebreak
                   within the batch, so the claim is that exactly one of the two was. *)
                let flaggedInSharedPair =
                    [ "Partial key both shared one"; "Partial key both shared two" ]
                    |> List.map entryNamed
                    |> List.filter (fun e -> e |> StageTestData.duplicateTransitionCount = 1)
                    |> List.length
                Assert.Equal(1, flaggedInSharedPair)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-7.3 — Stage-vs-ledger dedup
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-7.3 dedup flags entry matching posted JE external reference`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-008 has fi_reference TXN-001 matching jeWithRef's ext ref
                let dupEntry = fullResult.stagedEntries |> StageTestData.findByDescription "Fixture JE with reference"
                Assert.Equal(Duplicate, StageTestData.latestStatus dupEntry)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-7.3 dedup does not flag entry matching voided JE external reference`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-voided-ref.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-vd" today "Voided ref test" "VoidedEntryBank" "VOIDED-REF-001" 75.00M "Debit" (Some "F-5650") None
                let! row2 = StageTestData.makeRawRow context "grp-vd" today "Voided ref test" "VoidedEntryBank" "VOIDED-REF-001" 75.00M "Credit" (Some "F-1270") None
                let! fullResult = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
                let entry = fullResult.stagedEntries |> List.head
                Assert.NotEqual(Duplicate, StageTestData.latestStatus entry)
            })
        |> railroadWrapper



    [<Fact>]
    member _.``REQ-STG-7.3 a staged entry matching a non-voided JE's external reference on financial_institution alone, or on reference alone, gains no Duplicate transition in a pass that flags the entry matching both`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                (* The fixture's non-voided "Fixture JE with reference" carries the external
                   reference TestBank / TXN-001. Each of the three entries below sits alone in
                   its own source-and-reference partition, so nothing here can be flagged by
                   the stage-vs-stage rule and anything flagged was flagged by the ledger
                   comparison. *)
                let! sourceFile = "/tmp/test-ledger-partial.jsonl" |> SourceFile.create
                let makeEntry groupId desc source fiRef =
                    [ StageTestData.makeRawRow context groupId today desc source fiRef 37.00M "Debit" (Some "F-5650") None
                      StageTestData.makeRawRow context groupId today desc source fiRef 37.00M "Credit" (Some "F-1270") None ]
                let! rows =
                    [ makeEntry "grp-lp1" "Ledger partial source only" "TestBank" "REF-LEDGER-NOT-TXN-001"
                      makeEntry "grp-lp2" "Ledger partial reference only" "TestSavings" "TXN-001"
                      makeEntry "grp-lp3" "Ledger partial both" "TestBank" "TXN-001" ]
                    |> List.concat
                    |> convertListOfResultsToResultsList
                let! fullResult = rows |> ingestRawToStageThenDeduplicateAndClassify context sourceFile

                let entryNamed desc = fullResult.stagedEntries |> StageTestData.findByDescription desc
                Assert.Equal(0, entryNamed "Ledger partial source only" |> StageTestData.duplicateTransitionCount)
                Assert.Equal(0, entryNamed "Ledger partial reference only" |> StageTestData.duplicateTransitionCount)
                Assert.Equal(1, entryNamed "Ledger partial both" |> StageTestData.duplicateTransitionCount)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-7.5 — Duplicate flag does not alter lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-7.5 flagging as duplicate does not alter lines or account assignments`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-008 is a ledger dup — its lines should still have their original codes
                let dupEntry = fullResult.stagedEntries |> StageTestData.findByDescription "Fixture JE with reference"
                Assert.Equal(Duplicate, StageTestData.latestStatus dupEntry)
                let creditLine =
                    dupEntry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let! codeStr =
                    creditLine
                    |> StageEntryLine.accountId
                    |> ``convert AccountId Option to AccountCodeString Option`` context
                Assert.Equal(Some "F-1270", codeStr)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-4.5 — Ignored entries count as dedup matches
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-4.5 dedup treats Ignored entries as matches`` () =
        // REQ-STG-4.5 says Ignored entries must be treated as dedup matches.
        // The dedup query's WHERE clause excludes Duplicate/Posted/Ignored from being
        // candidates (they're already handled), but the LEFT JOIN to in_stage_already
        // does NOT exclude Ignored — so a new entry matching an Ignored entry's
        // source+ref will be flagged as duplicate. We test this by verifying the
        // dedup query's behavior: the query itself is tested in the production code.
        // Here we verify the spec's intent via the fixture's dedup flow.
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile1 = "/tmp/test-ignored-setup.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow context "grp-ign" today "Ignored entry" "TestBank" "REF-IGNORED-001" 30.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow context "grp-ign" today "Ignored entry" "TestBank" "REF-IGNORED-001" 30.00M "Credit" (Some "F-1270") None
                let! firstResult = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile1
                let firstEntry = firstResult.stagedEntries |> List.head
                let headerId = firstEntry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let contextForIgnore = context |> Context.updateInitiationInstant
                do! headerId |> Model.DataIngestion.StageEntryHeader.updateHeaderStatus contextForIgnore Ignored Operator
                let contextForReimport = contextForIgnore |> Context.updateInitiationInstant
                let! sourceFile2 = "/tmp/test-ignored-reimport.jsonl" |> SourceFile.create
                let! row3 = StageTestData.makeRawRow context "grp-ign2" today "Reimport of ignored" "TestBank" "REF-IGNORED-001" 30.00M "Debit" (Some "F-5350") None
                let! row4 = StageTestData.makeRawRow context "grp-ign2" today "Reimport of ignored" "TestBank" "REF-IGNORED-001" 30.00M "Credit" (Some "F-1270") None
                let! secondResult = [ row3; row4 ] |> ingestRawToStageThenDeduplicateAndClassify contextForReimport sourceFile2
                Assert.NotEmpty(secondResult.newDuplicates)
            })
        |> railroadWrapper
