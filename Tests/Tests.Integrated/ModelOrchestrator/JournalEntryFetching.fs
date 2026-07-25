namespace Tests.Integrated.ModelOrchestrator

open System
open InterfaceBridge.InterfaceContracts.JournalContracts
open InterfaceBridge.Json.Json
open Tests.Integrated.InterfaceBridge._routeResolver
open Utilities.ResultHelper
open Xunit
open Tests.Integrated
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.FiscalPeriods
open Model.LookupCache
open ModelOrchestrator.JournalEntries.JournalEntry
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryFetchingTests(fixture: TestDataFixture) =
    
    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns the correct journal entry`` () =
        let idToCheck = fixture.Data.basicJeId
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je -> je |> header |> JournalEntryHeader.journalEntryHeaderId = idToCheck)
            |> List.head
            |> header |> JournalEntryHeader.description |> JournalEntryDescription.value
        let result = idToCheck |> fetchById None
        match result with
        | Ok je ->
            Assert.Equal(idToCheck, je |> header |> JournalEntryHeader.journalEntryHeaderId)
            Assert.Equal(expected, je |> header |> JournalEntryHeader.description |> JournalEntryDescription.value)
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns error for nonexistent ID`` () =
        let bogusId = Guid.NewGuid() |> JournalEntryHeaderId.fromGuid
        let result = bogusId |> fetchById None
        Assert.True(Result.isError result)
        
    [<Fact>]
    member _.``REQ-JE-3.1 fetchById returns header, lines, external references, and comments`` () =
        let expectedLinesCount = fixture.Data.jeWithLinesRefsAndComments |> lines |> List.length
        let expectedRefsCount = fixture.Data.jeWithLinesRefsAndComments |> externalReferences |> List.length
        let expectedCommentsCount = fixture.Data.jeWithLinesRefsAndComments |> comments |> List.length
        let result = fixture.Data.jeWithLinesRefsAndCommentsId |> fetchById None
        match result with
        | Ok je ->
            Assert.NotNull(je |> header)
            Assert.Equal(expectedLinesCount, je |> lines |> List.length)
            Assert.Equal(expectedRefsCount, je |> externalReferences |> List.length)
            Assert.Equal(expectedCommentsCount, je |> comments |> List.length)
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns all entries for a given fiscal period`` () =
        let today = Calendar.today()
        let monthF = today.Month.ToString("D2")
        let periodKey = $"{today.Year}-{monthF}"
        let railroad = result {
            let! fpUuid = periodKey |> fiscalPeriodKeyToId.fetch
            let fpId = fpUuid |> FiscalPeriodId.fromGuid
            let expected =
                fixture.Data.journalEntries
                |> List.filter(fun x -> x |> header |> JournalEntryHeader.entryDate |> EntryDate.fiscalPeriodId = fpId)
                |> List.length
            let! fp = fpId |> FiscalPeriod.fetchById None 
            let! fetchList = fp |> fetchByPeriod None
            let actual = fetchList |> List.length
            Assert.Equal(expected, actual)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns empty list for period with no entries`` () =
        let today = Calendar.today()
        let farDate = today.PlusMonths(4)
        let monthF = farDate.Month.ToString("D2")
        let periodKey = $"{farDate.Year}-{monthF}"
        let railroad = result {
            let! uuid = periodKey |> fiscalPeriodKeyToId.fetch
            let! fp = uuid |> FiscalPeriodId.fromGuid |> FiscalPeriod.fetchById None
            let! entries = fp |> fetchByPeriod None
            Assert.Equal(0, entries |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns entries matching source FI and reference value`` () =
        let fiStr = "TestBank"
        let refStr = "TXN-001"
        let railroad = result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let expected =
                fixture.Data.journalEntryExternalReferences
                |> List.filter(fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi &&
                    jer |> JournalEntryExternalReference.referenceText = refText )
                |> List.length
            let! fetched = fetchByReference None (Some fi) (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            let firstEntry = fetched |> List.head
            let fetchedReferences = firstEntry |> externalReferences
            let matchedCount =
                fetchedReferences 
                |> List.filter(fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi &&
                    jer |> JournalEntryExternalReference.referenceText = refText )
                |> List.length
            Assert.True(matchedCount > 0)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared`` () =
        let fiStr = "TestBank"
        let refStr = "F-SHARED-001"
        let railroad = result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let expected =
                fixture.Data.journalEntryExternalReferences
                |> List.filter(fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi &&
                    jer |> JournalEntryExternalReference.referenceText = refText )
                |> List.length
            let! fetched = fetchByReference None (Some fi) (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns empty list for nonexistent reference`` () =
        let fiStr = "Bogus"
        let refStr = "Nada"
        let expected = 0
        let railroad = result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let! fetched = fetchByReference None (Some fi) (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.8 fetchByReference with FI only returns all entries for that FI`` () =
        let fiStr = "TestBank"
        let railroad = result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let expected =
                fixture.Data.journalEntryExternalReferences
                |> List.filter(fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi  )
                |> List.length
            let! fetched = fetchByReference None (Some fi) None
            Assert.Equal(expected, fetched |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.8 fetchByReference with reference text only only returns all entries for that reference text`` () =
        let refStr = "TXN-001"
        let railroad = result {
            let! refText = refStr |> JournalExternalReferenceText.create
            let expected =
                fixture.Data.journalEntryExternalReferences
                |> List.filter(fun jer ->
                    jer |> JournalEntryExternalReference.referenceText = refText )
                |> List.length
            let! fetched = fetchByReference None None (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-3.8 fetchByReference with both parameters None returns Error`` () =
        match fetchByReference None None None with
        | Error (JournalEntryFetchByReferenceBothArgumentsNull _) -> () 
        | Error e -> Assert.Fail (AppError.toMessage e)
        | Ok _ -> Assert.Fail "Expected failure; got success"
        
    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns entries within inclusive date range`` () =
        let today = Calendar.today()
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let entryDate = je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                entryDate >= today && entryDate <= today )
            |> List.length
        let result = fetchByDateRange None today today
        match result with
        | Ok entries -> Assert.Equal(expected, entries |> List.length)
        | Error e -> Assert.Fail (AppError.toMessage e)
        
    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns empty list when no entries in range`` () =
        let farDate = NodaTime.LocalDate(2050, 1, 1)
        let result = fetchByDateRange None farDate farDate
        match result with
        | Ok entries -> Assert.Equal(0, entries |> List.length)
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route returns exit code 1 for nonexistent ID`` () = // todo refactor to use ModelOrchestrator instead of command routes
        let railroad = result {
            let! payload = { JournalEntryFetchByIdInput.id = Guid.NewGuid() } |> toJson<JournalEntryFetchByIdInput>
            do! match routeUiCommandForTesting "JournalEntry" "FetchById" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (DalResultantRowsDidntMatchExpectation _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
