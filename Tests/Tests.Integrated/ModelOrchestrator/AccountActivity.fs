namespace Tests.Integrated.ModelOrchestrator

open Model.Ledger.Accounts.AccountComponent
open Xunit
open Tests.Integrated
open ModelOrchestrator.AccountActivity

[<Collection("SharedTestData")>]
type AccountActivityTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered by account returns enriched activity with entry-level fields`` () =
        let filter = {
            accountId = Some (fixture.Data.mortgage2210Id |> AccountId.value)
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
            Assert.True(activities |> List.length >= 1)
            let withDetail = activities |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            Assert.True(withDetail |> List.length >= 1)
            let detail = (withDetail |> List.head).activityDetail |> Option.get
            Assert.True(detail.journalEntryDescription.Length > 0)
            Assert.NotEqual(System.Guid.Empty, detail.journalEntryId)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered with unVoidedOnly excludes voided entries`` () =
        let filterAll = {
            accountId = Some (fixture.Data.entertainment5650Id |> AccountId.value)
            temporalFilter = None
            source = None
            accountType = None
            accountSubtype = None
            accountParentId = None
            journalEntryId = None
            amount = None
            description = None
            unVoidedOnly = false }
        let filterUnvoided = { filterAll with unVoidedOnly = true }
        let allResult = fetchFiltered None filterAll None
        let unvoidedResult = fetchFiltered None filterUnvoided None
        match allResult, unvoidedResult with
        | Ok allActivities, Ok unvoidedActivities ->
            let allDetails = allActivities |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            let unvoidedDetails = unvoidedActivities |> List.filter (fun a -> a.activityDetail |> Option.isSome)
            Assert.True((allDetails |> List.length) > (unvoidedDetails |> List.length))
        | Error e, _ -> Assert.Fail e
        | _, Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.9 fetchFiltered returns no-activity row for account with no lines`` () =
        let filter = {
            accountId = Some (fixture.Data.assets1000Id |> AccountId.value)
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
        | Error e -> Assert.Fail e
