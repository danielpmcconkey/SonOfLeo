namespace Tests.Integrated.ModelOrchestrator

open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Utilities.AppError
open Xunit
open Tests.Integrated
open ModelOrchestrator.AccountActivity
open System
open ModelOrchestrator.FetchFilters

[<Collection("SharedTestData")>]
type AccountActivityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by account returns all activity with no filters set``() =
        let expectedCountDetails = fixture.Data.totalJournalEntryLines
        let expectedCountTotal = expectedCountDetails + fixture.Data.totalAccountsWithNoLines
        let filter =
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
        let result = fetchFiltered None filter None
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
    member _.``REQ-JE-3.9 fetchFiltered with unVoidedOnly excludes voided entries``() =
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
        let filter =
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
        let result = fetchFiltered None filter None
        match result with
        | Ok activities -> Assert.Equal(expectedCountTotal, activities |> List.length)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered returns no-activity row for account with no lines``() =
        let accountId = fixture.Data.assets1000Id
        let linesAtAccount =
            fixture.Data.journalEntryLines
            |> List.filter(fun jel -> jel |> JournalEntryLine.accountId = accountId)
        Assert.True(linesAtAccount |> List.isEmpty) // make sure you picked an empty account
        let filter =
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
        let result = fetchFiltered None filter None
        match result with
        | Ok activities ->
            Assert.Equal(1, activities |> List.length)
            let activity = activities |> List.head
            Assert.True(activity.activityDetail |> Option.isNone)
        | Error e -> Assert.Fail(AppError.toMessage e)
