namespace Tests.Integrated.ModelOrchestrator

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

    let makeRawRow groupIdStr (date: NodaTime.LocalDate) descStr sourceStr refStr amount lineTypeStr codeStr memoStr =
        result {
            let! groupId = groupIdStr |> BaseStageEntryGroupId.create
            let! desc = descStr |> JournalEntryDescription.create
            let! source = sourceStr |> JournalRefFinancialInstitution.create
            let! ref = refStr |> JournalExternalReferenceText.create
            let! money = amount |> Money.fromDecimal
            let! lt = lineTypeStr |> JournalEntryLineType.fromString
            let! code = codeStr |> convertOptionToDesiredTypeWithFallibleConverter AccountCode.create
            let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
            return {
                baseStageEntryGroupId = groupId
                entryDate = date
                description = desc
                fiSource = source
                fiReference = ref
                amount = money
                entryType = lt
                accountCode = code
                memo = memo }
        }

    let buildTestRows () =
        let today = Calendar.today()
        [
            makeRawRow "grp-001" (today.PlusDays(-3)) "DD DoorDash Order 8431927" "TestBank" "REF-DD-001" 32.47M "Debit" None None
            makeRawRow "grp-001" (today.PlusDays(-3)) "DD DoorDash Order 8431927" "TestBank" "REF-DD-001" 32.47M "Credit" (Some "F-1270") None
            makeRawRow "grp-002" (today.PlusDays(-2)) "MARATHON PETRO 7218 ANYTOWN US" "TestBank" "REF-GAS-001" 48.12M "Debit" None None
            makeRawRow "grp-002" (today.PlusDays(-2)) "MARATHON PETRO 7218 ANYTOWN US" "TestBank" "REF-GAS-001" 48.12M "Credit" (Some "F-1270") None
            makeRawRow "grp-003" (today.PlusDays(-2)) "HARRIS TEETER 0381 ANYTOWN US" "TestBank" "REF-GROC-001" 127.83M "Debit" None None
            makeRawRow "grp-003" (today.PlusDays(-2)) "HARRIS TEETER 0381 ANYTOWN US" "TestBank" "REF-GROC-001" 127.83M "Credit" (Some "F-1270") None
            makeRawRow "grp-004" (today.PlusDays(-1)) "SPECTRUM SOUTHEAST 800-892-2253" "TestBank" "REF-CABLE-001" 79.99M "Debit" None None
            makeRawRow "grp-004" (today.PlusDays(-1)) "SPECTRUM SOUTHEAST 800-892-2253" "TestBank" "REF-CABLE-001" 79.99M "Credit" (Some "F-1270") None
            makeRawRow "grp-005" (today.PlusDays(-1)) "TOTALLY UNKNOWN MERCHANT NOWHERE" "TestSavings" "REF-UNK-001" 15.00M "Debit" None None
            makeRawRow "grp-005" (today.PlusDays(-1)) "TOTALLY UNKNOWN MERCHANT NOWHERE" "TestSavings" "REF-UNK-001" 15.00M "Credit" (Some "F-1270") None
            makeRawRow "grp-006" today "ALLSTATE INS AUTOPAY" "TestBank" "REF-INS-001" 142.50M "Debit" None None
            makeRawRow "grp-006" today "ALLSTATE INS AUTOPAY" "TestBank" "REF-INS-001" 142.50M "Credit" (Some "F-1270") None
            makeRawRow "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 800.00M "Debit" (Some "F-1270") (Some "Net pay to checking")
            makeRawRow "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 312.50M "Credit" (Some "F-5300") (Some "Federal withholding")
            makeRawRow "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 187.50M "Credit" (Some "F-5350") (Some "State withholding")
            makeRawRow "grp-007" (today.PlusDays(-3)) "PAYROLL DEPOSIT ACME CORP" "TestBank" "REF-PAY-001" 300.00M "Credit" (Some "F-5650") (Some "401k contribution")
            makeRawRow "grp-008" (today.PlusDays(-2)) "Fixture JE with reference" "TestBank" "TXN-001" 65.00M "Debit" None None
            makeRawRow "grp-008" (today.PlusDays(-2)) "Fixture JE with reference" "TestBank" "TXN-001" 65.00M "Credit" (Some "F-1270") None
            makeRawRow "grp-009" (today.PlusDays(-2)) "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Debit" None None
            makeRawRow "grp-009" (today.PlusDays(-2)) "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Credit" (Some "F-1270") None
            makeRawRow "grp-010" today "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Debit" None None
            makeRawRow "grp-010" today "DD DoorDash Order 9917223" "TestBank" "REF-DD-002" 28.93M "Credit" (Some "F-1270") None
        ] |> convertListOfResultsToResultsList

    let runPipeline context =
        result {
            let! sourceFile = "/tmp/stg-test-checking.jsonl" |> SourceFile.create
            let! rows = buildTestRows ()
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
                Assert.Equal(10, fullResult.stagedEntries |> List.length)
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
                Assert.True((entry |> lines |> List.length) >= 2)
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
                Assert.Equal(Some "F-1270", debitLine |> StageEntryLine.accountCode |> Option.map AccountCode.value)
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
                let! multiRow1 = StageTestData.makeRawRow "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 800.00M "Debit" (Some "F-1270") (Some "Net pay to checking")
                let! multiRow2 = StageTestData.makeRawRow "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 312.50M "Credit" (Some "F-5300") (Some "Federal withholding")
                let! multiRow3 = StageTestData.makeRawRow "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 187.50M "Credit" (Some "F-5350") (Some "State withholding")
                let! multiRow4 = StageTestData.makeRawRow "grp-multi" today "Multi leg event" "TestBank" "REF-MULTI-001" 300.00M "Credit" (Some "F-5650") (Some "401k contribution")
                let! otherRow1 = StageTestData.makeRawRow "grp-other" today "Separate event" "TestBank" "REF-OTHER-001" 40.00M "Debit" (Some "F-5350") None
                let! otherRow2 = StageTestData.makeRawRow "grp-other" today "Separate event" "TestBank" "REF-OTHER-001" 40.00M "Credit" (Some "F-1270") None
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
                let! firstRow1 = StageTestData.makeRawRow "grp-reused" today "First file event" "TestBank" "REF-REUSED-001" 25.00M "Debit" (Some "F-5350") None
                let! firstRow2 = StageTestData.makeRawRow "grp-reused" today "First file event" "TestBank" "REF-REUSED-001" 25.00M "Credit" (Some "F-1270") None
                let! firstResult = [ firstRow1; firstRow2 ] |> ingestRawToStageThenDeduplicateAndClassify context firstFile
                let contextForSecondFile = context |> Context.updateInitiationInstant
                let! secondFile = "/tmp/test-grouping-file-two.jsonl" |> SourceFile.create
                let! secondRow1 = StageTestData.makeRawRow "grp-reused" today "Second file event" "TestBank" "REF-REUSED-002" 61.00M "Debit" (Some "F-5350") None
                let! secondRow2 = StageTestData.makeRawRow "grp-reused" today "Second file event" "TestBank" "REF-REUSED-002" 61.00M "Credit" (Some "F-1270") None
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

    [<Fact>]
    member _.``REQ-STG-1.13 ingestRaw rejects group with inconsistent entry_date across records`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-inconsistent.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow "grp-inc" today "Inconsistent" "TestBank" "REF-INC-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-inc" (today.PlusDays(1)) "Inconsistent" "TestBank" "REF-INC-001" 100.00M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    | Error (IngestionBaseStageGroupIdDistinctDataViolation _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
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
                let! row = StageTestData.makeRawRow "grp-one" today "Single record" "TestBank" "REF-ONE-001" 100.00M "Debit" (Some "F-5350") None
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
                let! row1 = StageTestData.makeRawRow "grp-imb" today "Imbalanced" "TestBank" "REF-IMB-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-imb" today "Imbalanced" "TestBank" "REF-IMB-001" 99.99M "Credit" (Some "F-1270") None
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
                let! validRow1 = StageTestData.makeRawRow "grp-ok" today "Valid group" "TestBank" "REF-OK-001" 100.00M "Debit" (Some "F-5350") None
                let! validRow2 = StageTestData.makeRawRow "grp-ok" today "Valid group" "TestBank" "REF-OK-001" 100.00M "Credit" (Some "F-1270") None
                let! badRow = StageTestData.makeRawRow "grp-bad" today "Bad group" "TestBank" "REF-BAD-001" 50.00M "Debit" (Some "F-5350") None
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
                let! row1 = StageTestData.makeRawRow "grp-src" today "Bad source" "NonExistentBank" "REF-SRC-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-src" today "Bad source" "NonExistentBank" "REF-SRC-001" 100.00M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    (* This asserts a leak, not a design. An unresolvable ingestion source name reaches the
                       caller as a raw row-count error from the data access layer instead of a
                       domain error, because the lookup does not re-brand it the way
                       FiscalPeriod.fetchIdByKey does. The exact case is asserted so that
                       fixing the leak in Src turns this red rather than leaving it silently
                       agreeing with the wrong thing. *)
                    | Error (DalResultantRowsDidntMatchExpectation _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-3.7 — Non-null account_code that doesn't exist rejects file
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-3.7 ingestRaw rejects file when account_code does not resolve to existing account`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! sourceFile = "/tmp/test-bad-code.jsonl" |> SourceFile.create
                let! row1 = StageTestData.makeRawRow "grp-code" today "Bad code" "TestBank" "REF-CODE-001" 100.00M "Debit" (Some "BOGUS-9999") None
                let! row2 = StageTestData.makeRawRow "grp-code" today "Bad code" "TestBank" "REF-CODE-001" 100.00M "Credit" (Some "F-1270") None
                return!
                    match [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile with
                    (* This asserts a leak, not a design. An unresolvable account code reaches the
                       caller as a raw row-count error from the data access layer instead of a
                       domain error, because the lookup does not re-brand it the way
                       FiscalPeriod.fetchIdByKey does. The exact case is asserted so that
                       fixing the leak in Src turns this red rather than leaving it silently
                       agreeing with the wrong thing. *)
                    | Error (DalResultantRowsDidntMatchExpectation _) -> Ok ()
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
                let! row1 = StageTestData.makeRawRow "grp-err" today "Typed error" "TestBank" "REF-ERR-001" 100.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-err" today "Typed error" "TestBank" "REF-ERR-001" 50.00M "Credit" (Some "F-1270") None
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
                    |> List.filter (fun se -> se |> stageEntryHeader |> StageEntryHeader.status = Duplicate)
                    |> List.length
                Assert.Equal(1, dupCount)
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
                let! row1 = StageTestData.makeRawRow "grp-vd" today "Voided ref test" "VoidedEntryBank" "VOIDED-REF-001" 75.00M "Debit" (Some "F-5650") None
                let! row2 = StageTestData.makeRawRow "grp-vd" today "Voided ref test" "VoidedEntryBank" "VOIDED-REF-001" 75.00M "Credit" (Some "F-1270") None
                let! fullResult = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
                let entry = fullResult.stagedEntries |> List.head
                Assert.NotEqual(Duplicate, StageTestData.latestStatus entry)
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
                Assert.Equal(Some "F-1270", creditLine |> StageEntryLine.accountCode |> Option.map AccountCode.value)
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
                let! row1 = StageTestData.makeRawRow "grp-ign" today "Ignored entry" "TestBank" "REF-IGNORED-001" 30.00M "Debit" (Some "F-5350") None
                let! row2 = StageTestData.makeRawRow "grp-ign" today "Ignored entry" "TestBank" "REF-IGNORED-001" 30.00M "Credit" (Some "F-1270") None
                let! firstResult = [ row1; row2 ] |> ingestRawToStageThenDeduplicateAndClassify context sourceFile1
                let firstEntry = firstResult.stagedEntries |> List.head
                let headerId = firstEntry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let contextForIgnore = context |> Context.updateInitiationInstant
                let! _ = headerId |> Model.DataIngestion.StageEntryHeader.updateStatus contextForIgnore Ignored
                let contextForReimport = contextForIgnore |> Context.updateInitiationInstant
                let! sourceFile2 = "/tmp/test-ignored-reimport.jsonl" |> SourceFile.create
                let! row3 = StageTestData.makeRawRow "grp-ign2" today "Reimport of ignored" "TestBank" "REF-IGNORED-001" 30.00M "Debit" (Some "F-5350") None
                let! row4 = StageTestData.makeRawRow "grp-ign2" today "Reimport of ignored" "TestBank" "REF-IGNORED-001" 30.00M "Credit" (Some "F-1270") None
                let! secondResult = [ row3; row4 ] |> ingestRawToStageThenDeduplicateAndClassify contextForReimport sourceFile2
                Assert.NotEmpty(secondResult.newDuplicates)
            })
        |> railroadWrapper
