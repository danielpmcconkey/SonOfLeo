namespace Tests.Integrated.ModelOrchestrator

open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Xunit
open Tests.Integrated
open ModelOrchestrator.AccountActivity
open System
open ModelOrchestrator.FetchFilters

[<Collection("SharedTestData")>]
type AccountActivityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by account returns all activity with no filters set`` () =
        let expectedCountDetails = fixture.Data.totalJournalEntryLines
        let expectedCountTotal = expectedCountDetails + fixture.Data.totalAccountsWithNoLines
        let filter = {
            accountId = None
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
            let withDetail = activities |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            Assert.Equal(expectedCountDetails, withDetail |> List.length)
            let detail = (withDetail |> List.head).activityDetail
                         |> Option.get
            let descriptionText =  detail.journalEntryDescription |> JournalEntryDescription.value
            Assert.False(String.IsNullOrWhiteSpace descriptionText )
            Assert.NotEqual(Guid.Empty, detail.journalEntryHeaderId |> JournalEntryHeaderId.value)
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered with unVoidedOnly excludes voided entries`` () =
        // todo: we do this better in the AccountRoutes tests. move that test here and delete this one
        let expectedCount = 14
        let filterUnvoided = {
            accountId = None
            temporalFilter = None
            source = None
            accountType = None
            accountSubtype = None
            accountParentId = None
            journalEntryId = None
            amount = None
            description = None
            unVoidedOnly = true }
        let unvoidedResult = fetchFiltered None filterUnvoided None
        match unvoidedResult with
        | Ok unvoidedActivities ->
            let unvoidedDetails = unvoidedActivities |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            Assert.Equal(expectedCount, unvoidedDetails |> List.length)
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered returns no-activity row for account with no lines`` () =
        // todo: this test is ill-conceived. the account activity filter *should* return a row. The test should be that it does put an empty account in the result set
        let filter = {
            accountId = Some (fixture.Data.assets1000Id)
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
        | Error e -> Assert.Fail (AppError.toMessage e)
