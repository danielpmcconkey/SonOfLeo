namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.StageEntryComponent
open Model.Ledger
open ModelOrchestrator.FetchFilters
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.ResultHelper
open Xunit
open Model.Ledger.JournalEntryComponent


[<Collection("SharedTestData")>]
type StageEntryFetchingTests(fixture: TestDataFixture) =

    (* Staged entries are transient by nature and cannot live in the shared fixture: grp-009
       and grp-010 deliberately share REF-DD-002 so the dedup pass has something to catch, and
       a permanent copy of that pair would make every later pipeline run flag duplicates
       against it. So this suite stages its own data inside the rollback, the way the other
       four StageEntry suites do. *)

    static let today = Calendar.today()

    (* The twelve rows StageTestData stages all carry one source file and all fall within
       today-3..today. Neither the source-file filter nor the fiscal-period filter can prove
       it excludes anything against data that uniform, so this second group supplies the
       counterexample: a different file, a date two months back, and an fi_reference that
       collides with nothing in the main batch. *)
    static let otherSourceFilePath = "/tmp/stg-test-other-source.jsonl"
    static let otherBatchDate = today.PlusMonths(-2)

    static let stageOtherBatch context =
        result {
            let! sourceFile = otherSourceFilePath |> SourceFile.create
            let! rows =
                [ StageTestData.makeRawRow
                      context "grp-other-001" otherBatchDate "PRIOR PERIOD UTILITY PAYMENT"
                      "TestBank" "REF-OTHER-001" 91.40M "Debit" None None
                  StageTestData.makeRawRow
                      context "grp-other-001" otherBatchDate "PRIOR PERIOD UTILITY PAYMENT"
                      "TestBank" "REF-OTHER-001" 91.40M "Credit" (Some "F-1270") None ]
                |> convertListOfResultsToResultsList
            return! rows |> ingestRawToStageThenDeduplicateAndClassify context sourceFile
        }

    /// Stages both batches and hands back every entry now in the stage, so expected values
    /// are derived from entities this test put there rather than from the fetch under test.
    static let stageAll context =
        result {
            let! main = StageTestData.runPipeline context
            let! other = stageOtherBatch context
            return main.stagedEntries @ other.stagedEntries
        }

    static let noFilter: StageEntryFetchFilter =
        { stageEntryHeaderId = None
          sourceFile = None
          temporalFilter = None
          description = None
          ingestionSource = None
          fiReference = None
          status = None
          stageEntryLineId = None
          amount = None
          lineType = None
          accountId = None
          memo = None
          classificationRuleId = None }

    static let idOf entry = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
    static let idsOf entries = entries |> List.map idOf |> List.sort
    static let descriptionOf entry =
        entry |> stageEntryHeader |> StageEntryHeader.description |> JournalEntryDescription.value
    static let sourceNameOf entry =
        entry
        |> stageEntryHeader
        |> StageEntryHeader.ingestionSource
        |> IngestionSource.name
        |> JournalRefFinancialInstitution.value
    static let entryDateOf entry = entry |> stageEntryHeader |> StageEntryHeader.entryDate
    static let sourceFileOf entry =
        entry |> stageEntryHeader |> StageEntryHeader.sourceFile |> SourceFile.value
    static let fiReferenceOf entry =
        entry |> stageEntryHeader |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value
    static let statusOf entry = entry |> stageEntryHeader |> StageEntryHeader.currentStatus

    /// The where clause keys off the latest row in staged_entry_audit, not the status column
    /// on the header, so the status filter's expected set is derived the same way.
    static let latestTransitionStatusOf entry =
        entry
        |> statusTransitions
        |> List.sortByDescending StageEntryStatusTransition.instant
        |> List.head
        |> StageEntryStatusTransition.toStatus

    static let findByDescription desc entries = entries |> StageTestData.findByDescription desc


    // =========================================================================
    // REQ-STG-10.1 — the unfiltered query
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-10.1 fetchFiltered with every filter omitted returns every staged entry in the table, matched by id``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let! fetched = noFilter |> fetchFiltered context None
                Assert.Equal(staged |> List.length, fetched |> List.length)
                Assert.Equal<StageEntryHeaderId list>(staged |> idsOf, fetched |> idsOf)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-10.2 — one criterion at a time
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by stage entry id returns exactly the one entry bearing that id`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let target = staged |> findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let! fetched =
                    { noFilter with stageEntryHeaderId = Some(target |> idOf) } |> fetchFiltered context None
                Assert.Equal(1, fetched |> List.length)
                Assert.Equal(target |> idOf, fetched |> List.head |> idOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by source file returns every entry staged from that file and no entry staged from another``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let! mainSourceFile = "/tmp/stg-test-checking.jsonl" |> SourceFile.create
                let expected =
                    staged
                    |> List.filter(fun e -> e |> sourceFileOf = "/tmp/stg-test-checking.jsonl")
                let! fetched = { noFilter with sourceFile = Some mainSourceFile } |> fetchFiltered context None
                Assert.Equal(expected |> List.length, fetched |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                (* The other batch is the only thing that makes the exclusion half meaningful,
                   so prove it was staged and prove it stayed out. *)
                let otherIds = staged |> List.except expected |> idsOf
                Assert.NotEmpty otherIds
                Assert.Empty(fetched |> idsOf |> List.filter(fun id -> otherIds |> List.contains id))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by date range returns every entry dated inside the range, both endpoints included, and no entry dated outside it``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let beginDate = today.PlusDays(-2)
                let endInclusive = today.PlusDays(-1)
                let expected =
                    staged
                    |> List.filter(fun e ->
                        let d = e |> entryDateOf
                        d >= beginDate && d <= endInclusive)
                let! fetched =
                    { noFilter with
                        temporalFilter = Some(DateRange { beginDate = beginDate; endInclusive = endInclusive }) }
                    |> fetchFiltered context None
                Assert.Equal(expected |> List.length, fetched |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                (* Endpoint inclusivity is only proved if entries actually sit on both ends. *)
                Assert.Contains(beginDate, expected |> List.map entryDateOf)
                Assert.Contains(endInclusive, expected |> List.map entryDateOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by fiscal period returns every entry whose date falls in that period and no entry from an adjacent one``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let period =
                    fixture.Data.fiscalPeriods
                    |> List.find(fun fp ->
                        otherBatchDate >= (fp |> FiscalPeriod.startDate)
                        && otherBatchDate <= (fp |> FiscalPeriod.endDate))
                let expected =
                    staged
                    |> List.filter(fun e ->
                        let d = e |> entryDateOf
                        d >= (period |> FiscalPeriod.startDate) && d <= (period |> FiscalPeriod.endDate))
                let! fetched =
                    { noFilter with
                        temporalFilter = Some(FiscalPeriodIdentifier(period |> FiscalPeriod.fiscalPeriodId)) }
                    |> fetchFiltered context None
                Assert.Equal(expected |> List.length, fetched |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by description returns every entry carrying that description and no entry carrying another``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                (* grp-009 and grp-010 share this description, so a filter that stopped at the
                   first hit returns one entry where two are owed. *)
                let targetDescription = "DD DoorDash Order 9917223"
                let expected = staged |> List.filter(fun e -> e |> descriptionOf = targetDescription)
                let! description = targetDescription |> JournalEntryDescription.create
                let! fetched = { noFilter with description = Some description } |> fetchFiltered context None
                Assert.Equal(2, expected |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.All(fetched, fun e -> Assert.Equal(targetDescription, e |> descriptionOf))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by a description fragment returns every entry whose description contains it and no entry that does not``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                (* "ANYTOWN" sits inside two different full descriptions, so a filter still
                   doing exact match returns nothing and a filter ignoring the fragment
                   returns everything. Only partial match returns these two. *)
                let fragment = "ANYTOWN"
                let expected = staged |> List.filter(fun e -> (e |> descriptionOf).Contains fragment)
                Assert.Equal(2, expected |> List.length)
                Assert.Equal(2, expected |> List.map descriptionOf |> List.distinct |> List.length)
                let! description = fragment |> JournalEntryDescription.create
                let! fetched = { noFilter with description = Some description } |> fetchFiltered context None
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.All(fetched, fun e -> Assert.Contains(fragment, e |> descriptionOf))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 the description filter matches case-exactly: an upper-case fragment returns the entries carrying it in that case and returns nothing once its case is altered``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let fragment = "ANYTOWN"
                (* Asserting only that the lower-cased fragment returns nothing would be
                   satisfied by a filter that always returns nothing, so the same fragment in
                   its own case has to come back populated in the same test. *)
                let! matching = fragment |> JournalEntryDescription.create
                let! fetchedMatching = { noFilter with description = Some matching } |> fetchFiltered context None
                let expected = staged |> List.filter(fun e -> (e |> descriptionOf).Contains fragment)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetchedMatching |> idsOf)
                Assert.NotEmpty fetchedMatching
                let! lowered = fragment.ToLowerInvariant() |> JournalEntryDescription.create
                let! fetchedLowered = { noFilter with description = Some lowered } |> fetchFiltered context None
                Assert.Empty fetchedLowered
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by ingestion source returns every entry from that institution and no entry from another``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let expected = staged |> List.filter(fun e -> e |> sourceNameOf = "TestSavings")
                let! source = "TestSavings" |> JournalRefFinancialInstitution.create
                let! fetched = { noFilter with ingestionSource = Some source } |> fetchFiltered context None
                Assert.Equal(expected |> List.length, fetched |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.All(fetched, fun e -> Assert.Equal("TestSavings", e |> sourceNameOf))
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by fi reference returns every entry carrying that reference and no entry carrying another``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let targetReference = "REF-DD-002"
                let expected =
                    staged
                    |> List.filter(fun e -> e |> fiReferenceOf = targetReference)
                let! reference = targetReference |> JournalExternalReferenceText.create
                let! fetched = { noFilter with fiReference = Some reference } |> fetchFiltered context None
                Assert.Equal(2, expected |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by status returns every entry currently in that status and no entry in another``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                (* Which statuses the pipeline produces is the classifier's business, not this
                   test's, so the target is whichever status has the largest non-total share. *)
                let targetStatus =
                    staged
                    |> List.countBy latestTransitionStatusOf
                    |> List.filter(fun (_, count) -> count < (staged |> List.length))
                    |> List.sortByDescending snd
                    |> List.head
                    |> fst
                let expected = staged |> List.filter(fun e -> e |> latestTransitionStatusOf = targetStatus)
                let! fetched = { noFilter with status = Some targetStatus } |> fetchFiltered context None
                Assert.Equal(expected |> List.length, fetched |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.All(fetched, fun e -> Assert.Equal(targetStatus, e |> latestTransitionStatusOf))
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by stage line id returns exactly the one entry owning that line`` () =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let target = staged |> findByDescription "PAYROLL DEPOSIT ACME CORP"
                let targetLine = target |> lines |> List.head
                let! fetched =
                    { noFilter with stageEntryLineId = Some(targetLine |> StageEntryLine.stageEntryLineId) }
                    |> fetchFiltered context None
                Assert.Equal(1, fetched |> List.length)
                Assert.Equal(target |> idOf, fetched |> List.head |> idOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by amount returns every entry having a line at that amount and no entry whose lines are all other amounts``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let! targetAmount = 312.50M |> Money.fromDecimal
                let expected =
                    staged
                    |> List.filter(fun e -> e |> lines |> List.exists(fun l -> l |> StageEntryLine.amount = targetAmount))
                let! fetched = { noFilter with amount = Some targetAmount } |> fetchFiltered context None
                Assert.Equal(1, expected |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by line type paired with amount returns the entry owning the credit leg and returns nothing for the debit``
        ()
        =
        (* Double entry puts at least one Debit and one Credit on every staged entry, so a line
           type filter on its own returns the whole table whatever value it is given — it would
           pass just as green if the implementation dropped the field entirely. Pairing it with
           an amount carried by exactly one leg is what makes it falsifiable: if line type were
           ignored, the Debit case below would come back holding grp-007. *)
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let! targetAmount = 312.50M |> Money.fromDecimal
                let expected =
                    staged
                    |> List.filter(fun e -> e |> lines |> List.exists(fun l -> l |> StageEntryLine.amount = targetAmount))
                let! creditMatches =
                    { noFilter with amount = Some targetAmount; lineType = Some Credit } |> fetchFiltered context None
                let! debitMatches =
                    { noFilter with amount = Some targetAmount; lineType = Some Debit } |> fetchFiltered context None
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, creditMatches |> idsOf)
                Assert.Empty debitMatches
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by account returns every entry having a line assigned that account and no entry without one``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let targetAccountId = fixture.Data.food5350Id
                let expected =
                    staged
                    |> List.filter(fun e ->
                        e |> lines |> List.exists(fun l -> l |> StageEntryLine.accountId = Some targetAccountId))
                let! fetched = { noFilter with accountId = Some targetAccountId } |> fetchFiltered context None
                Assert.NotEmpty expected
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by memo returns every entry having a line carrying that memo and no entry without one``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let targetMemoText = "Federal withholding"
                let expected =
                    staged
                    |> List.filter(fun e ->
                        e
                        |> lines
                        |> List.exists(fun l ->
                            l
                            |> StageEntryLine.memo
                            |> Option.map JournalEntryLineMemo.value = Some targetMemoText))
                let! memo = targetMemoText |> JournalEntryLineMemo.create
                let! fetched = { noFilter with memo = Some memo } |> fetchFiltered context None
                Assert.Equal(1, expected |> List.length)
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered by classification rule id returns every entry having a line classified by that rule and no entry classified by another``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                (* Which rule wins which line is the classifier's business; take whichever rule
                   the pipeline actually stamped onto the fewest entries so the exclusion half
                   has something to exclude. *)
                let targetRuleId =
                    staged
                    |> List.collect(fun e -> e |> lines |> List.choose StageEntryLine.classificationRuleId)
                    |> List.distinct
                    |> List.head
                let expected =
                    staged
                    |> List.filter(fun e ->
                        e
                        |> lines
                        |> List.exists(fun l -> l |> StageEntryLine.classificationRuleId = Some targetRuleId))
                let! fetched = { noFilter with classificationRuleId = Some targetRuleId } |> fetchFiltered context None
                Assert.NotEmpty expected
                Assert.Equal<StageEntryHeaderId list>(expected |> idsOf, fetched |> idsOf)
                Assert.NotEmpty(staged |> List.except expected)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.2 fetchFiltered given both a status and an ingestion source returns only the entries satisfying both, not the union``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let targetStatus =
                    staged
                    |> List.countBy latestTransitionStatusOf
                    |> List.filter(fun (_, count) -> count < (staged |> List.length))
                    |> List.sortByDescending snd
                    |> List.head
                    |> fst
                let byStatus = staged |> List.filter(fun e -> e |> latestTransitionStatusOf = targetStatus)
                let bySource = staged |> List.filter(fun e -> e |> sourceNameOf = "TestBank")
                let intersection =
                    byStatus |> List.filter(fun e -> bySource |> List.exists(fun other -> idOf other = idOf e))
                let unionCount =
                    (byStatus @ bySource) |> List.map idOf |> List.distinct |> List.length
                let! source = "TestBank" |> JournalRefFinancialInstitution.create
                let! fetched =
                    { noFilter with status = Some targetStatus; ingestionSource = Some source }
                    |> fetchFiltered context None
                (* Without a strict subset on both sides the conjunction claim proves nothing. *)
                Assert.True(intersection |> List.length < unionCount)
                Assert.NotEmpty intersection
                Assert.Equal<StageEntryHeaderId list>(intersection |> idsOf, fetched |> idsOf)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-10.3 — a line-level match returns the whole entry
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-10.3 fetchFiltered by an amount matching one line of a four-line entry returns that entry with all four of its lines``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let payroll = staged |> findByDescription "PAYROLL DEPOSIT ACME CORP"
                let! targetAmount = 312.50M |> Money.fromDecimal
                (* Exactly one of the payroll entry's four legs carries this amount. *)
                Assert.Equal(
                    1,
                    payroll |> lines |> List.filter(fun l -> l |> StageEntryLine.amount = targetAmount) |> List.length)
                let! fetched = { noFilter with amount = Some targetAmount } |> fetchFiltered context None
                Assert.Equal(1, fetched |> List.length)
                let returned = fetched |> List.head
                Assert.Equal(payroll |> idOf, returned |> idOf)
                Assert.Equal(4, returned |> lines |> List.length)
                Assert.Equal<Money list>(
                    payroll |> lines |> List.map StageEntryLine.amount |> List.sort,
                    returned |> lines |> List.map StageEntryLine.amount |> List.sort)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-10.4 — sorting
    // =========================================================================

    (* The order by clauses carry no tiebreaker and the staged data ties on every one of the
       four keys, so which of two equal-keyed entries comes first is not the query's promise to
       keep. The sequence of key values is, and it is also collation-independent: whatever
       ordering the database considers ascending, descending has to be its reverse. *)
    [<Theory>]
    [<InlineData("entryDate")>]
    [<InlineData("ingestionSource")>]
    [<InlineData("status")>]
    [<InlineData("description")>]
    member _.``REQ-STG-10.4 fetchFiltered sorted ascending and sorted descending return the same entries with exactly reversed key sequences``
        (key: string)
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! _ = stageAll context
                let ascSort, descSort, keyOf =
                    match key with
                    | "entryDate" ->
                        EntryDateAsc, EntryDateDesc, (fun e -> (e |> entryDateOf).ToString("yyyy-MM-dd", null))
                    | "ingestionSource" -> FiAsc, FiDesc, sourceNameOf
                    | "status" -> StatusAsc, StatusDesc, (fun e -> e |> latestTransitionStatusOf |> StagedEntryStatus.toString)
                    | "description" -> DescriptionAsc, DescriptionDesc, descriptionOf
                    | other -> failwith $"Unhandled sort key {other}"
                let! ascending = noFilter |> fetchFiltered context (Some ascSort)
                let! descending = noFilter |> fetchFiltered context (Some descSort)
                Assert.Equal<StageEntryHeaderId list>(ascending |> idsOf, descending |> idsOf)
                Assert.Equal<string list>(
                    ascending |> List.map keyOf |> List.rev,
                    descending |> List.map keyOf)
                (* A palindromic key sequence would satisfy the reversal above without the sort
                   having run at all, so require the two directions to actually differ. *)
                Assert.NotEqual<string list>(ascending |> List.map keyOf, descending |> List.map keyOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-10.4 fetchFiltered sorted by entry date ascending places no entry before one dated earlier`` () =
        (* Dates are the one sort key with an ordering the test can assert directly, without
           having to agree with the database about collation. *)
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let! fetched = noFilter |> fetchFiltered context (Some EntryDateAsc)
                let returnedDates = fetched |> List.map entryDateOf
                Assert.Equal<NodaTime.LocalDate list>(returnedDates |> List.sort, returnedDates)
                Assert.Equal(staged |> List.length, fetched |> List.length)
                (* Sorted output only means something if the input was not already in order. *)
                Assert.NotEqual<NodaTime.LocalDate list>(staged |> List.map entryDateOf, returnedDates)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-10.5 — no match is an empty list, not an error
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-10.5 fetchFiltered with a description no staged entry carries returns an empty list rather than an error``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let absentDescription = "NO STAGED ENTRY CARRIES THIS DESCRIPTION"
                Assert.DoesNotContain(absentDescription, staged |> List.map descriptionOf)
                let! description = absentDescription |> JournalEntryDescription.create
                let! fetched = { noFilter with description = Some description } |> fetchFiltered context None
                Assert.Empty fetched
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-10.6 — full composition
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-10.6 an entry returned by fetchFiltered carries its header fields, all of its lines, and all of its status transitions``
        ()
        =
        runCommandRouteAndAutoRollback IngestRawEntries (fun context ->
            result {
                let! staged = stageAll context
                let payroll = staged |> findByDescription "PAYROLL DEPOSIT ACME CORP"
                let! fetched =
                    { noFilter with stageEntryHeaderId = Some(payroll |> idOf) } |> fetchFiltered context None
                Assert.Equal(1, fetched |> List.length)
                let returned = fetched |> List.head

                Assert.Equal(payroll |> descriptionOf, returned |> descriptionOf)
                Assert.Equal(payroll |> entryDateOf, returned |> entryDateOf)
                Assert.Equal(payroll |> sourceNameOf, returned |> sourceNameOf)
                Assert.Equal<StagedEntryStatus option>(payroll |> statusOf, returned |> statusOf)
                Assert.Equal(payroll |> fiReferenceOf, returned |> fiReferenceOf)
                Assert.Equal(payroll |> sourceFileOf, returned |> sourceFileOf)

                Assert.Equal<StageEntryLineId list>(
                    payroll |> lines |> List.map StageEntryLine.stageEntryLineId |> List.sort,
                    returned |> lines |> List.map StageEntryLine.stageEntryLineId |> List.sort)
                Assert.Equal<Money list>(
                    payroll |> lines |> List.map StageEntryLine.amount |> List.sort,
                    returned |> lines |> List.map StageEntryLine.amount |> List.sort)

                Assert.NotEmpty(returned |> statusTransitions)
                Assert.Equal<StageEntryStatusTransitionId list>(
                    payroll
                    |> statusTransitions
                    |> List.map StageEntryStatusTransition.stageEntryStatusTransitionId
                    |> List.sort,
                    returned
                    |> statusTransitions
                    |> List.map StageEntryStatusTransition.stageEntryStatusTransitionId
                    |> List.sort)
            })
        |> railroadWrapper
