namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility.Result
open Xunit
open Tests.Helpers
open System
open Business.CrossDomainOrchestration.FetchFilters
open Tests.Helpers.Railroad

[<Collection("SharedTestData")>]
type AccountActivityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-3.12 fetchFiltered with no filters set returns one row per fixture journal entry line and one line-less row per account without lines``() =
        let lineRows =
            fixture.Data.journalEntryLines
            |> List.map (fun l -> l |> JournalEntryLine.accountId, Some(l |> JournalEntryLine.journalEntryLineId))
        let accountsWithLines = lineRows |> List.map fst |> Set.ofList
        let lineLessRows =
            fixture.Data.accounts
            |> List.map Account.accountId
            |> List.filter (fun id -> accountsWithLines |> Set.contains id |> not)
            |> List.map (fun id -> id, None)
        let expectedRows = lineRows @ lineLessRows
        let filter:AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = None
              amount = None
              description = None
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities ->
            let returnedRows = activities |> List.map (fun a -> a.accountId, a.activityDetail |> Option.map _.lineId)
            Assert.Equal<Set<AccountId * JournalEntryLineId option>>(expectedRows |> Set.ofList, returnedRows |> Set.ofList)
            Assert.Equal(expectedRows |> List.length, returnedRows |> List.length)
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.12 activity detail carries its parent entry's date, description, source, and voided-at``() =
        (* "Fixture JE with reference" is the one fixture entry created with a source, so it
           is the only one that can prove the source enrichment is populated rather than
           merely present as a null. *)
        let expectedEntry =
            fixture.Data.journalEntries
            |> List.find(fun je ->
                je
                |> JournalEntryOrchestration.header
                |> JournalEntryHeader.description
                |> JournalEntryDescription.value = "Fixture JE with reference")
        let expectedHeader = expectedEntry |> JournalEntryOrchestration.header
        let expectedLineId =
            expectedEntry |> JournalEntryOrchestration.jeLines |> List.head |> JournalEntryLine.journalEntryLineId
        let filter: AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = None
              amount = None
              description = None
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        result {
            let! activities = AccountActivity.fetchFiltered context filter None
            let detail =
                activities
                |> List.choose(fun a -> a.activityDetail)
                |> List.find(fun d -> d.lineId = expectedLineId)
            Assert.Equal(
                expectedHeader |> JournalEntryHeader.entryDate |> EntryDate.entryDate,
                detail.entryDate)
            Assert.Equal(
                expectedHeader |> JournalEntryHeader.description |> JournalEntryDescription.value,
                detail.journalEntryDescription |> JournalEntryDescription.value)
            Assert.Equal<string option>(
                expectedHeader |> JournalEntryHeader.source |> Option.map JournalEntrySource.value,
                detail.journalEntrySource |> Option.map JournalEntrySource.value)
            Assert.Equal<NodaTime.Instant option>(
                expectedHeader |> JournalEntryHeader.voidedAt,
                detail.journalEntryVoidedAt)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.12.2 fetchFiltered with unVoidedOnly excludes voided entries``() =
        let unVoidedJournalEntries =
            fixture.Data.journalEntries
            |> List.filter(fun je -> je |> JournalEntryOrchestration.header |> JournalEntryHeader.voidedAt |> Option.isNone)
        let unVoidedLines = unVoidedJournalEntries |> List.collect(fun je -> je |> JournalEntryOrchestration.jeLines)
        let accounts = fixture.Data.accounts
        let expectedCountTotal =
            accounts
            |> List.sumBy(fun account ->
                let lineCount =
                    unVoidedLines
                    |> List.filter(fun line -> line |> JournalEntryLine.accountId = (account |> Account.accountId))
                    |> List.length
                max 1 lineCount) // this picks up the "naked" account with no lines
        let filter:AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = None
              amount = None
              description = None
              unVoidedOnly = true }
        let voidedLineIds =
            fixture.Data.journalEntries
            |> List.filter(fun je -> je |> JournalEntryOrchestration.header |> JournalEntryHeader.voidedAt |> Option.isSome)
            |> List.collect(fun je -> je |> JournalEntryOrchestration.jeLines)
            |> List.map JournalEntryLine.journalEntryLineId
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities ->
            Assert.Equal(expectedCountTotal, activities |> List.length)
            (* A filter that let one voided entry through while dropping one live entry keeps the
               count intact, so the count alone cannot see it. Void exclusion is a ledger
               invariant; assert it on the rows themselves. *)
            Assert.NotEmpty voidedLineIds
            let returnedDetails = activities |> List.choose(fun activity -> activity.activityDetail)
            Assert.All(returnedDetails, fun detail -> Assert.True(detail.journalEntryVoidedAt |> Option.isNone))
            Assert.Empty(
                returnedDetails
                |> List.filter(fun detail -> voidedLineIds |> List.contains detail.lineId))
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.12.3 fetchFiltered returns no-activity row for account with no lines``() =
        let accountId = fixture.Data.assets1000Id
        let linesAtAccount =
            fixture.Data.journalEntryLines
            |> List.filter(fun jel -> jel |> JournalEntryLine.accountId = accountId)
        Assert.True(linesAtAccount |> List.isEmpty) // make sure you picked an empty account
        let filter:AccountActivityFilter =
            { accountId = Some accountId
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = None
              amount = None
              description = None
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities ->
            Assert.Equal(1, activities |> List.length)
            let activity = activities |> List.head
            Assert.True(activity.activityDetail |> Option.isNone)
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.12.1 REQ-AC-3.12.2 fetchFiltered by amount returns exactly the lines of that amount, voided entries' lines included``() =
        (* The flag is off, so the population is every fixture line, voided entries' included. *)
        let allLines = fixture.Data.journalEntryLines
        let targetAmountDecimal =
            allLines
            |> List.countBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
            |> List.maxBy snd
            |> fst
        let expectedLineIds =
            allLines
            |> List.filter(fun l -> l |> JournalEntryLine.amount |> Money.amount = targetAmountDecimal)
            |> List.map JournalEntryLine.journalEntryLineId
        let targetAmount =
            targetAmountDecimal |> Money.fromDecimal |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let filter:AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = None
              amount = Some targetAmount
              description = None
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities ->
            let returnedLineIds = activities |> List.choose (fun a -> a.activityDetail |> Option.map _.lineId)
            Assert.Equal<Set<JournalEntryLineId>>(expectedLineIds |> Set.ofList, returnedLineIds |> Set.ofList)
            Assert.Equal(expectedLineIds |> List.length, activities |> List.length)
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.12.1 fetchFiltered by description returns only matching lines``() =
        let targetDescriptionStringFull =
            fixture.Data.jeWithUniqueDescription
            |> JournalEntryOrchestration.header
            |> JournalEntryHeader.description
            |> JournalEntryDescription.value
        // the description is a like match, so we want to take a substring
        let targetDescriptionLength = targetDescriptionStringFull |> String.length
        let targetDescriptionString = targetDescriptionStringFull.Substring(2, targetDescriptionLength - 4)
        let numLines = fixture.Data.jeWithUniqueDescription |> JournalEntryOrchestration.jeLines |> List.length
        let numMatchingEntries =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let full = je |> JournalEntryOrchestration.header |> JournalEntryHeader.description |> JournalEntryDescription.value
                full.Contains(targetDescriptionString))
            |> List.length
        let expectedCount = numMatchingEntries * numLines // the report surfaces all lines whose entry matches
        let targetDescription =
            targetDescriptionString
            |> JournalEntryDescription.create
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let filter:AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = None
              amount = None
              description = Some targetDescription
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities ->
            let withDetail = activities |> List.filter(fun a -> a.activityDetail |> Option.isSome)
            Assert.Equal(expectedCount, withDetail |> List.length)
            for activity in withDetail do
                let detail = activity.activityDetail |> Option.get
                Assert.Equal(targetDescriptionStringFull, detail.journalEntryDescription |> JournalEntryDescription.value)
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.12.1 AccountActivity.fetchFiltered by journalEntryId returns only lines for that entry``() =
        let targetId = fixture.Data.basicJeId
        let expectedLineCount =
            fixture.Data.journalEntryLines
            |> List.filter(fun l -> l |> JournalEntryLine.journalEntryHeaderId = targetId)
            |> List.length
        Assert.True(expectedLineCount > 0, "Fixture basicJe should have lines")
        let filter:AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = Some targetId
              amount = None
              description = None
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities ->
            let withDetail = activities |> List.filter(fun a -> a.activityDetail |> Option.isSome)
            Assert.Equal(expectedLineCount, withDetail |> List.length)
            for activity in withDetail do
                let detail = activity.activityDetail |> Option.get
                Assert.Equal(targetId, detail.journalEntryHeaderId)
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.12.1 fetchFiltered by journalEntryId with nonexistent id returns no activity rows``() =
        let bogusId = Guid.NewGuid() |> JournalEntryHeaderId.fromGuid
        let filter:AccountActivityFilter =
            { accountId = None
              temporalFilter = None
              source = None
              accountType = None
              accountSubtype = None
              accountParentId = None
              journalEntryId = Some bogusId
              amount = None
              description = None
              unVoidedOnly = false }
        let context = Context.create NoTransaction FetchOnly
        let result = AccountActivity.fetchFiltered context filter None
        match result with
        | Ok activities -> Assert.Empty(activities)
        | Error e -> Assert.Fail(e.ToMessage())

    // =========================================================================
    // Plan defects 8–17 (2026-09-27)
    // =========================================================================

    [<Theory>]
    [<InlineData("%")>]
    [<InlineData("_")>]
    [<InlineData(@"\")>]
    member _.``REQ-SYS-1.4 account activity description filter returns every record containing search text with a literal %, _ or \ and no record lacking it`` (special: string) =
        Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoRollback FetchOnly (fun context ->
            result {
                let case = LiteralSearch.case "sys14activity" special
                let post description =
                    EntityFunctions.createTestJournalEntryFromPrimitives
                        context description None (App.Utility.Calendar.today())
                        [ (fixture.Data.food5350Id, 10.00M, "Debit", None)
                          (fixture.Data.moneyMarket1270Id, 10.00M, "Credit", None) ]
                        [] []
                let! _ = post case.containing
                let! _ = post case.decoy
                let! description = case.search |> JournalEntryDescription.create
                let filter: AccountActivityFilter =
                    { accountId = None
                      temporalFilter = None
                      source = None
                      accountType = None
                      accountSubtype = None
                      accountParentId = None
                      journalEntryId = None
                      amount = None
                      description = Some description
                      unVoidedOnly = false }
                let! activities = AccountActivity.fetchFiltered context filter None
                Assert.All(activities, fun a -> Assert.True(a.activityDetail.IsSome, "an activity row without a line"))
                let descriptions =
                    activities
                    |> List.choose _.activityDetail
                    |> List.map (fun d -> d.journalEntryDescription |> JournalEntryDescription.value)
                Assert.Contains(case.containing, descriptions)
                Assert.DoesNotContain(case.decoy, descriptions)
                Assert.All(descriptions, fun d -> Assert.Contains(case.search, d))
            })
        |> railroadWrapper
