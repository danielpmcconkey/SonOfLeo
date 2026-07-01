namespace Tests.Integrated.ModelOrchestrator

open System
open Xunit
open Tests.Integrated
open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.FiscalPeriods
open Model.LookupCache
open Model.Ledger.JournalEntryPrimitives
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
            Assert.Equal("Fixture basic JE", je |> header |> JournalEntryHeader.description |> Description.value)
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
            let fetchResult = periodId |> fetchByPeriod
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
            let fetchResult = periodId |> fetchByPeriod
            match fetchResult with
            | Ok entries -> Assert.Equal(0, entries |> List.length)
            | Error e -> Assert.Fail e

    // =============================================================================
    // Fetch by external reference
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns entries matching source FI and reference value`` () =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let prims : JournalEntryPrimitives =
            { header =
                { description = "FetchByRef target"; source = None; entryDate = Calendar.today(); voidedAt = None }
              lines =
                [ { accountId = fixture.Data.mortgage2210Id; amount = 15.00M; lineType = "Debit"; memo = None }
                  { accountId = fixture.Data.food5350Id; amount = 15.00M; lineType = "Credit"; memo = None } ]
              externalReferences = [ { financialInstitution = "FetchRefBank"; referenceText = "FREF-001" } ]
              comments = [] }
        match prims |> orchestrateCreation envelope with
        | Error e -> Assert.Fail $"Setup failed: {e}"
        | Ok _ ->
            let result = fetchByReference "FetchRefBank" "FREF-001"
            match result with
            | Ok entries ->
                Assert.True(entries |> List.length >= 1)
                let matchingEntry = entries |> List.find (fun je ->
                    je |> header |> JournalEntryHeader.description |> Description.value = "FetchByRef target")
                Assert.Equal("FetchByRef target", matchingEntry |> header |> JournalEntryHeader.description |> Description.value)
            | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared`` () =
        let envelope1 = AuditEnvelope.create JournalEntryPostNew
        let prims1 : JournalEntryPrimitives =
            { header =
                { description = "Shared ref entry 1"; source = None; entryDate = Calendar.today(); voidedAt = None }
              lines =
                [ { accountId = fixture.Data.mortgage2210Id; amount = 10.00M; lineType = "Debit"; memo = None }
                  { accountId = fixture.Data.food5350Id; amount = 10.00M; lineType = "Credit"; memo = None } ]
              externalReferences = [ { financialInstitution = "SharedBank"; referenceText = "SHARED-001" } ]
              comments = [] }
        let envelope2 = AuditEnvelope.create JournalEntryPostNew
        let prims2 : JournalEntryPrimitives =
            { header =
                { description = "Shared ref entry 2"; source = None; entryDate = Calendar.today(); voidedAt = None }
              lines =
                [ { accountId = fixture.Data.mortgage2210Id; amount = 20.00M; lineType = "Debit"; memo = None }
                  { accountId = fixture.Data.food5350Id; amount = 20.00M; lineType = "Credit"; memo = None } ]
              externalReferences = [ { financialInstitution = "SharedBank"; referenceText = "SHARED-001" } ]
              comments = [] }
        match prims1 |> orchestrateCreation envelope1, prims2 |> orchestrateCreation envelope2 with
        | Ok _, Ok _ ->
            let fetchResult = fetchByReference "SharedBank" "SHARED-001"
            match fetchResult with
            | Ok entries -> Assert.True(entries |> List.length >= 2)
            | Error e -> Assert.Fail e
        | Error e, _ -> Assert.Fail $"First creation failed: {e}"
        | _, Error e -> Assert.Fail $"Second creation failed: {e}"

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns empty list for nonexistent reference`` () =
        let result = fetchByReference "NoSuchBank" "NO-SUCH-REF"
        match result with
        | Ok entries -> Assert.Equal(0, entries |> List.length)
        | Error e -> Assert.Fail e
