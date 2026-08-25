namespace Tests.Integrated.ModelOrchestrator

open DataAccessLayer.DbTransaction
open Logger.Audit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Utilities.AppError
open Utilities.ResultHelper
open Xunit
open Tests.Helpers
open Model
open ModelOrchestrator
open System
open ModelOrchestrator.FetchFilters
open Tests.Helpers.Railroad

[<Collection("SharedTestData")>]
type AccountActivityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by account returns all activity with no filters set``() =
        let expectedCountDetails = fixture.Data.totalJournalEntryLines
        let expectedCountTotal = expectedCountDetails + fixture.Data.totalAccountsWithNoLines
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
            Assert.Equal(expectedCountTotal, activities |> List.length)
            let withDetail = activities |> List.filter(fun a -> a.activityDetail |> Option.isSome)
            Assert.Equal(expectedCountDetails, withDetail |> List.length)
            let detail = (withDetail |> List.head).activityDetail |> Option.get
            let descriptionText = detail.journalEntryDescription |> JournalEntryDescription.value
            Assert.False(String.IsNullOrWhiteSpace descriptionText)
            Assert.NotEqual(Guid.Empty, detail.journalEntryHeaderId |> JournalEntryHeaderId.value)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 activity detail carries its parent entry's date, description, source, and voided-at``() =
        (* "Fixture JE with reference" is the one fixture entry created with a source, so it
           is the only one that can prove the source enrichment is populated rather than
           merely present as a null. *)
        let expectedEntry =
            fixture.Data.journalEntries
            |> List.find(fun je ->
                je
                |> JournalEntry.header
                |> JournalEntryHeader.description
                |> JournalEntryDescription.value = "Fixture JE with reference")
        let expectedHeader = expectedEntry |> JournalEntry.header
        let expectedLineId =
            expectedEntry |> JournalEntry.lines |> List.head |> JournalEntryLine.journalEntryLineId
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
    member _.``REQ-JE-3.9.1 fetchFiltered with unVoidedOnly excludes voided entries``() =
        let unVoidedJournalEntries =
            fixture.Data.journalEntries
            |> List.filter(fun je -> je |> JournalEntry.header |> JournalEntryHeader.voidedAt |> Option.isNone)
        let unVoidedLines = unVoidedJournalEntries |> List.collect(fun je -> je |> JournalEntry.lines)
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
            |> List.filter(fun je -> je |> JournalEntry.header |> JournalEntryHeader.voidedAt |> Option.isSome)
            |> List.collect(fun je -> je |> JournalEntry.lines)
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
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered returns no-activity row for account with no lines``() =
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
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by amount returns only matching lines``() =
        let nonVoidedLines =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                je |> JournalEntry.header |> JournalEntryHeader.voidedAt |> Option.isNone)
            |> List.collect JournalEntry.lines
        let targetAmountDecimal =
            nonVoidedLines
            |> List.countBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
            |> List.maxBy snd
            |> fst
        let expectedCount =
            nonVoidedLines
            |> List.filter(fun l -> l |> JournalEntryLine.amount |> Money.amount = targetAmountDecimal)
            |> List.length
        let targetAmount =
            targetAmountDecimal |> Money.fromDecimal |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
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
            let withDetail = activities |> List.filter(fun a -> a.activityDetail |> Option.isSome)
            Assert.Equal(expectedCount, withDetail |> List.length)
            for activity in withDetail do
                let detail = activity.activityDetail |> Option.get
                Assert.Equal(targetAmountDecimal, detail.amount |> Money.amount)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9.3 fetchFiltered sort by entry date — ascending and descending are mutual reverses``() =
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
        let extractDates (activities:AccountActivity.AccountActivity list) =
            activities
            |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            |> List.map (fun a -> (a.activityDetail |> Option.get).entryDate)
        result {
            let! activitiesAsc = AccountActivity.fetchFiltered context filter (Some FetchSort.EntryDateAsc)
            let! activitiesDesc = AccountActivity.fetchFiltered context filter (Some FetchSort.EntryDateDesc)
            let datesAsc = extractDates activitiesAsc
            let datesDesc = extractDates activitiesDesc
            Assert.True(
                datesAsc |> List.pairwise |> List.exists (fun (a, b) -> a <> b),
                "All dates are identical — sort order cannot be verified")
            Assert.True(
                (datesAsc |> List.rev) = datesDesc,
                "Descending sort should be the reverse of ascending sort")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.9.3 fetchFiltered sort by account code — ascending and descending are mutual reverses``() =
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
        let extractCodes (activities:AccountActivity.AccountActivity list) =
            activities |> List.map (fun a -> a.accountCode |> AccountCode.value)
        result {
            let! activitiesAsc = AccountActivity.fetchFiltered context filter (Some FetchSort.AccountCodeAsc)
            let! activitiesDesc = AccountActivity.fetchFiltered context filter (Some FetchSort.AccountCodeDesc)
            let codesAsc = extractCodes activitiesAsc
            let codesDesc = extractCodes activitiesDesc
            Assert.True(
                codesAsc |> List.pairwise |> List.exists (fun (a, b) -> a <> b),
                "All codes are identical — sort order cannot be verified")
            Assert.True(
                (codesAsc |> List.rev) = codesDesc,
                "Descending sort should be the reverse of ascending sort")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.9.3 fetchFiltered sort by amount — ascending and descending are mutual reverses``() =
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
        let extractAmounts (activities:AccountActivity.AccountActivity list) =
            activities
            |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            |> List.map (fun a -> (a.activityDetail |> Option.get).amount |> Money.amount)
        result {
            let! activitiesAsc = AccountActivity.fetchFiltered context filter (Some AmountAsc)
            let! activitiesDesc = AccountActivity.fetchFiltered context filter (Some AmountDesc)
            let amountsAsc = extractAmounts activitiesAsc
            let amountsDesc = extractAmounts activitiesDesc
            Assert.True(
                amountsAsc |> List.pairwise |> List.exists (fun (a, b) -> a <> b),
                "All amounts are identical — sort order cannot be verified")
            Assert.True(
                (amountsAsc |> List.rev) = amountsDesc,
                "Descending sort should be the reverse of ascending sort")
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by description returns only matching lines``() =
        let targetDescriptionStringFull =
            fixture.Data.jeWithUniqueDescription
            |> JournalEntry.header
            |> JournalEntryHeader.description
            |> JournalEntryDescription.value
        // the description is a like match, so we want to take a substring
        let targetDescriptionLength = targetDescriptionStringFull |> String.length
        let targetDescriptionString = targetDescriptionStringFull.Substring(2, targetDescriptionLength - 4)
        let numLines = fixture.Data.jeWithUniqueDescription |> JournalEntry.lines |> List.length
        let numMatchingEntries =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let full = je |> JournalEntry.header |> JournalEntryHeader.description |> JournalEntryDescription.value
                full.Contains(targetDescriptionString))
            |> List.length
        let expectedCount = numMatchingEntries * numLines // the report surfaces all lines whose entry matches
        let targetDescription =
            targetDescriptionString
            |> JournalEntryDescription.create
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
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
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 AccountActivity.fetchFiltered by journalEntryId returns only lines for that entry``() =
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
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by journalEntryId with nonexistent id returns no activity rows``() =
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
        | Error e -> Assert.Fail(AppError.toMessage e)
