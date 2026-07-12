namespace Tests.Integrated.ModelOrchestrator

open System
open Xunit
open Tests.Integrated
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.FiscalPeriods
open Model.LookupCache
open ModelOrchestrator.JournalEntries
open ModelOrchestrator.JournalEntries.JournalEntryCreationAndConstruction
open ModelOrchestrator.JournalEntryFetching
open Utilities

[<Collection("SharedTestData")>]
type JournalEntryFetchingTests(fixture: TestDataFixture) =

    // =============================================================================
    // Fetch by ID
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns the correct journal entry`` () =
        let result = fixture.Data.basicJeId |> fetchById
        match result with
        | Ok je ->
            Assert.Equal(fixture.Data.basicJeId, je |> header |> JournalEntryHeader.uniqueId)
            Assert.Equal("Fixture basic JE", je |> header |> JournalEntryHeader.description |> JournalEntryDescription.value)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns error for nonexistent ID`` () =
        let bogusId = Guid.NewGuid()
        let result = bogusId |> fetchById
        Assert.True(Result.isError result)

    [<Fact>]
    member _.``REQ-JE-3.1 fetchById returns header, lines, external references, and comments`` () =
        let result = fixture.Data.jeWithRefId |> fetchById
        match result with
        | Ok je ->
            Assert.NotNull(je |> header)
            Assert.True(je |> lines |> List.length >= 2)
            Assert.True(je |> externalReferences |> List.length >= 1)
        | Error e -> Assert.Fail e

    // =============================================================================
    // Fetch by period
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns all entries for a given fiscal period`` () =
        let today = Calendar.today()
        let monthF = today.Month.ToString("D2")
        let periodKey = $"{today.Year}-{monthF}"
        let result = fiscalPeriodKeyToId.fetch periodKey
        match result with
        | Error e -> Assert.Fail $"Could not find period for today: {e}"
        | Ok periodId ->
            let fetchResult = periodId |> FiscalPeriodId.fromGuid |> fetchByPeriod
            match fetchResult with
            | Ok entries -> Assert.True(entries |> List.length >= 1)
            | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns empty list for period with no entries`` () =
        let today = Calendar.today()
        let farDate = today.PlusMonths(4)
        let monthF = farDate.Month.ToString("D2")
        let periodKey = $"{farDate.Year}-{monthF}"
        let result = fiscalPeriodKeyToId.fetch periodKey
        match result with
        | Error e -> Assert.Fail $"Could not find far period: {e}"
        | Ok periodId ->
            let fetchResult = periodId |> FiscalPeriodId.fromGuid |> fetchByPeriod
            match fetchResult with
            | Ok entries -> Assert.Equal(0, entries |> List.length)
            | Error e -> Assert.Fail e

    // =============================================================================
    // Fetch by external reference
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns entries matching source FI and reference value`` () =
        let result = fetchByReference (Some "TestBank") (Some "TXN-001")
        match result with
        | Ok entries ->
            let entryIds = entries |> List.map (fun je -> je |> header |> JournalEntryHeader.uniqueId)
            Assert.Contains(fixture.Data.jeWithRefId, entryIds)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared`` () =
        let result = fetchByReference (Some "SharedBank") (Some "F-SHARED-001")
        match result with
        | Ok entries ->
            let entryIds = entries |> List.map (fun je -> je |> header |> JournalEntryHeader.uniqueId)
            Assert.Contains(fixture.Data.sharedRefJe1Id, entryIds)
            Assert.Contains(fixture.Data.sharedRefJe2Id, entryIds)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns empty list for nonexistent reference`` () =
        let result = fetchByReference (Some "NoSuchBank") (Some "NO-SUCH-REF")
        match result with
        | Ok entries -> Assert.Equal(0, entries |> List.length)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.8 fetchByReference with FI only returns all entries for that FI`` () =
        let result = fetchByReference (Some "TestBank") None
        match result with
        | Ok entries ->
            let entryIds = entries |> List.map (fun je -> je |> header |> JournalEntryHeader.uniqueId)
            Assert.Contains(fixture.Data.jeWithRefId, entryIds)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.8 fetchByReference with FI only returns multiple entries when FI is shared`` () =
        let result = fetchByReference (Some "SharedBank") None
        match result with
        | Ok entries ->
            let entryIds = entries |> List.map (fun je -> je |> header |> JournalEntryHeader.uniqueId)
            Assert.Contains(fixture.Data.sharedRefJe1Id, entryIds)
            Assert.Contains(fixture.Data.sharedRefJe2Id, entryIds)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-3.8 fetchByReference with both parameters None returns Error`` () =
        let result = fetchByReference None None
        Assert.True(Result.isError result)

    // =============================================================================
    // Fetch by date range
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns entries within inclusive date range`` () =
        let today = Calendar.today()
        let result = fetchByDateRange today today
        match result with
        | Ok entries -> Assert.True(entries |> List.length >= 1)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns empty list when no entries in range`` () =
        let farDate = NodaTime.LocalDate(2050, 1, 1)
        let result = fetchByDateRange farDate farDate
        match result with
        | Ok entries -> Assert.Equal(0, entries |> List.length)
        | Error e -> Assert.Fail e
