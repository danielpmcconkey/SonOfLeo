namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
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

    let fetchOnDate context (date: NodaTime.LocalDate) = fetchByDateRange context date date

    let headerOf journalEntry = journalEntry |> header
    let linesOf journalEntry = journalEntry |> lines
    let externalReferencesOf journalEntry = journalEntry |> externalReferences

    let lineCount journalEntry = journalEntry |> lines |> List.length


[<Collection("SharedTestData")>]
type StageEntryPostingTests(fixture: TestDataFixture) =

    (* Posting one staged entry and then finding what the ledger made of it needs a locator,
       and the locator can never be a field the calling test asserts on — searching by a value
       and then asserting the row carries that value proves only that the filter works. So
       there are two locators, and each test takes the one that does not overlap its own
       requirement: the header-mapping test owns description and entry date, the external
       reference test owns the reference, and neither may look itself up. The requirements are
       named in prose rather than by ID because the traceability audit greps whole files, and
       an ID in a comment reads to it as a test annotation. *)
    static let postStagedEntry context entry =
        result {
            let! jeSource =
                Some "Data ingestion import"
                |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
            do! entry |> postStageEntry context jeSource
        }

    static let postThenFetchByExternalReference context entry =
        result {
            do! entry |> postStagedEntry context
            let staged = entry |> stageEntryHeader
            let fi = staged |> StageEntryHeader.ingestionSource |> IngestionSource.name
            let fiReference = staged |> StageEntryHeader.fiReference
            let! posted = PostedJournalEntry.fetchByFiReference context fi fiReference
            Assert.Equal(1, posted |> List.length)
            return posted |> List.head
        }

    static let postThenFetchByDateAndDescription context entry =
        result {
            do! entry |> postStagedEntry context
            let staged = entry |> stageEntryHeader
            let description = staged |> StageEntryHeader.description
            let! onThatDate = staged |> StageEntryHeader.entryDate |> PostedJournalEntry.fetchOnDate context
            let matching =
                onThatDate
                |> List.filter (fun journalEntry ->
                    journalEntry |> PostedJournalEntry.headerOf |> JournalEntryHeader.description = description)
            Assert.Equal(1, matching |> List.length)
            return matching |> List.head
        }


    // =========================================================================
    // REQ-STG-8.2 REQ-STG-9.2 — Posting runs the real JE domain validation
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.2 REQ-STG-9.2 shadow post fails when staged entry is in closed fiscal period`` () =
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
                    | Error (JournalEntryHeaderEntryDateInvalid _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure posting to closed period; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-8.3 — Shadow post returns before and after trial balance, and the delta is real
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.3 the difference between the two trial balances is the staged amount`` () =
        runCommandRouteAndAutoRollback IngestShadowPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                (* What the shadow post is about to move is derived here from the staged rows
                   themselves, by the same rule fetchAllForPosting applies but without calling
                   it: an entry posts when its latest status is Classified or Reviewed and
                   every one of its lines carries an account code. *)
                let postableLines =
                    fullResult.stagedEntries
                    |> List.filter (fun entry ->
                        let status = StageTestData.latestStatus entry
                        (status = Classified || status = Reviewed)
                        && entry
                           |> lines
                           |> List.forall (fun line -> line |> StageEntryLine.accountCode |> Option.isSome))
                    |> List.collect lines
                (* A trial balance rolls child balances up into their parents, so only a leaf
                   account's movement equals its own postings. *)
                let isLeaf accountCode =
                    let accountId =
                        fixture.Data.accounts
                        |> List.find (fun account -> account |> Account.code = accountCode)
                        |> Account.accountId
                    fixture.Data.accounts
                    |> List.forall (fun account -> account |> Account.parentId <> Some accountId)
                let stagedAmountFor lineType accountCode =
                    postableLines
                    |> List.filter (fun line ->
                        line |> StageEntryLine.accountCode = Some accountCode
                        && line |> StageEntryLine.lineType = lineType)
                    |> List.sumBy (fun line -> line |> StageEntryLine.amount |> Money.amount)
                let accountCodesReceivingPostings =
                    postableLines
                    |> List.choose (fun line -> line |> StageEntryLine.accountCode)
                    |> List.distinct
                    |> List.filter isLeaf
                Assert.NotEmpty(accountCodesReceivingPostings)
                let asOf = Calendar.today()
                let! trialBalanceBefore = fetchTrialBalanceData context asOf
                do! ModelOrchestrator.StageEntryOrchestration.post context
                let! trialBalanceAfter = fetchTrialBalanceData context asOf
                let rowFor accountCode (rows: TrialBalanceRowFlattened list) =
                    rows |> List.find (fun row -> row.accountCode = accountCode)
                accountCodesReceivingPostings
                |> List.iter (fun accountCode ->
                    let expectedDebits = accountCode |> stagedAmountFor Debit
                    let expectedCredits = accountCode |> stagedAmountFor Credit
                    (* A zero here would make the two assertions below hold for a shadow post
                       that moved nothing at all. *)
                    Assert.True(
                        expectedDebits + expectedCredits > 0M,
                        $"Nothing is staged against {accountCode |> AccountCode.value}, so its delta proves nothing.")
                    let before = trialBalanceBefore |> rowFor accountCode
                    let after = trialBalanceAfter |> rowFor accountCode
                    Assert.Equal(
                        expectedDebits,
                        (after.totalDebits |> Money.amount) - (before.totalDebits |> Money.amount))
                    Assert.Equal(
                        expectedCredits,
                        (after.totalCredits |> Money.amount) - (before.totalCredits |> Money.amount)))
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-1.3 REQ-STG-9.2 — one group produces one journal entry, built by the domain model
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.3 REQ-STG-9.2 a four-record group posts as a single journal entry carrying all four lines`` () =
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
    // REQ-STG-9.1 — Batch post happy path
    // =========================================================================

    [<Fact>]
    (* Deliberately toothless. This proves only that a full batch round trip does not blow
       up; what the resulting journal entries contain is asserted by the tests below. *)
    member _.``REQ-STG-9.1 batch post happy path`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! _ = StageTestData.runPipeline context
                do! ModelOrchestrator.StageEntryOrchestration.post context
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
                let staged = entry |> stageEntryHeader
                let! posted = entry |> postThenFetchByExternalReference context
                let postedHeader = posted |> PostedJournalEntry.headerOf
                Assert.Equal(
                    staged |> StageEntryHeader.description |> JournalEntryDescription.value,
                    postedHeader |> JournalEntryHeader.description |> JournalEntryDescription.value)
                Assert.Equal(
                    staged |> StageEntryHeader.entryDate,
                    postedHeader |> JournalEntryHeader.entryDate |> EntryDate.entryDate)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.4 — Account code to ID resolution at posting time, and the uncoded line
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.4 posted JE lines carry the resolved account, amount, line type, and memo of each staged line`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                (* The payroll group is the only fixture group whose lines differ from each
                   other in account, amount, and memo all at once, so a mapping that crosses
                   two lines cannot survive it. *)
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "PAYROLL DEPOSIT ACME CORP"
                (* The account ID is resolved from the fixture's own chart of accounts rather
                   than from the lookup the poster uses, so a broken resolution cannot agree
                   with itself. *)
                let accountIdOfCode code =
                    fixture.Data.accounts
                    |> List.find (fun account -> account |> Account.code = code)
                    |> Account.accountId
                    |> AccountId.value
                let expected =
                    entry
                    |> lines
                    |> List.map (fun line ->
                        line |> StageEntryLine.accountCode |> Option.get |> accountIdOfCode,
                        line |> StageEntryLine.amount |> Money.amount,
                        line |> StageEntryLine.lineType,
                        line |> StageEntryLine.memo |> Option.map JournalEntryLineMemo.value)
                    |> List.sort
                let! posted = entry |> postThenFetchByExternalReference context
                let actual =
                    posted
                    |> PostedJournalEntry.linesOf
                    |> List.map (fun line ->
                        line |> JournalEntryLine.accountId |> AccountId.value,
                        line |> JournalEntryLine.amount |> Money.amount,
                        line |> JournalEntryLine.lineType,
                        line |> JournalEntryLine.memo |> Option.map JournalEntryLineMemo.value)
                    |> List.sort
                Assert.Equal<_ list>(expected, actual)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-9.4 batch post fails loudly when a postable entry carries an uncoded line`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let lineId = entry |> lines |> List.head |> StageEntryLine.stageEntryLineId
                (* Strip the code the classifier assigned. The entry stays Classified and so
                   stays postable: this is exactly the broken upstream invariant the
                   requirement describes, and posting has to say so rather than quietly skip
                   the entry. An invalid non-null code cannot be staged at all, the chart of
                   accounts being FK-constrained, so a null is the only reachable shape of
                   this failure. *)
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
                        accountCodeUpdate = Utilities.FieldUpdate.FieldUpdate.SetTo None
                        memoUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange
                        classificationRuleIdUpdate = Utilities.FieldUpdate.FieldUpdate.NoChange } ]
                let! uncoded = updateStageEntry context headerUpdates lineUpdates
                // the entry has to still be postable, or the post below would prove nothing
                Assert.Equal(Classified, StageTestData.latestStatus uncoded)
                Assert.True(
                    uncoded |> lines |> List.exists (fun line -> line |> StageEntryLine.accountCode |> Option.isNone),
                    "The line under test must be uncoded for this test to mean anything.")
                return!
                    match ModelOrchestrator.StageEntryOrchestration.post context with
                    | Error (IngestionPostingNoneAccountCode _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-9.5 — External reference constructed from source + fi_reference
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.5 posted JE carries one external reference built from the staged source name and fi_reference`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "SPECTRUM SOUTHEAST 800-892-2253"
                let staged = entry |> stageEntryHeader
                let! posted = entry |> postThenFetchByDateAndDescription context
                let externalReferences = posted |> PostedJournalEntry.externalReferencesOf
                Assert.Equal(1, externalReferences |> List.length)
                let externalReference = externalReferences |> List.head
                Assert.Equal(
                    staged |> StageEntryHeader.ingestionSource |> IngestionSource.name |> JournalRefFinancialInstitution.value,
                    externalReference
                    |> JournalEntryExternalReference.financialInstitution
                    |> JournalRefFinancialInstitution.value)
                Assert.Equal(
                    staged |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value,
                    externalReference
                    |> JournalEntryExternalReference.referenceText
                    |> JournalExternalReferenceText.value)
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


    (* The all-or-nothing batch post requirement of spec section 9 has no test at this layer,
       deliberately. Atomicity is a property of the transaction, and every orchestrator test
       runs inside one that is rolled back regardless of the outcome, so a partially-posted
       batch and a fully-rolled-back one leave identical evidence in here. It is tested at the
       route, where the transaction is committed or discarded for real:
       Tests.Integrated.InterfaceBridge.IngestionRoutes. *)


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
