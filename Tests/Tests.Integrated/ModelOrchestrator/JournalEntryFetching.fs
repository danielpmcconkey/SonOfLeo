namespace Tests.Integrated.ModelOrchestrator

open System
open DataAccessLayer.DbTransaction
open InterfaceBridge.InterfaceContracts.JournalContracts
open Utilities.Json.Json
open Logger.Audit
open Tests.Helpers.RouteResolver
open Tests.Helpers.Railroad
open Utilities.ResultHelper
open Xunit
open Tests.Helpers
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Model.Ledger.FiscalPeriods
open Model.LookupCache
open ModelOrchestrator.JournalEntries.JournalEntry
open Utilities
open Utilities.AppError
open Tests.Helpers.SadPath


[<Collection("SharedTestData")>]
type JournalEntryFetchingTests(fixture: TestDataFixture) =

    (* fetchByReference returns distinct journal entries, so an expectation has to count
       entries and not the reference rows pointing at them. One entry can carry several
       matching references. The fixture currently gives each entry at most one per
       institution-and-text pair, which made the two quantities agree by luck rather than by
       construction -- add a second matching reference to any entry and a row count starts
       expecting an entry that fetchByReference is right not to return twice. *)
    let distinctEntryCountMatching predicate =
        fixture.Data.journalEntryExternalReferences
        |> List.filter predicate
        |> List.map JournalEntryExternalReference.journalEntryHeaderId
        |> List.distinct
        |> List.length

    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns the correct journal entry``() =
        let context = Context.create NoTransaction FetchOnly
        let idToCheck = fixture.Data.basicJeId
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je -> je |> header |> JournalEntryHeader.journalEntryHeaderId = idToCheck)
            |> List.head
            |> header
            |> JournalEntryHeader.description
            |> JournalEntryDescription.value
        let result = idToCheck |> fetchById context
        match result with
        | Ok je ->
            Assert.Equal(idToCheck, je |> header |> JournalEntryHeader.journalEntryHeaderId)
            Assert.Equal(expected, je |> header |> JournalEntryHeader.description |> JournalEntryDescription.value)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns error for nonexistent ID``() =
        let context = Context.create NoTransaction FetchOnly
        let bogusId = Guid.NewGuid() |> JournalEntryHeaderId.fromGuid
        isCorrectError (bogusId |> fetchById context) JournalEntryHeaderIdDoesntExist None
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.1 fetchById returns the entry's own lines, external references, and comments, identified individually and not merely counted``() =
        let context = Context.create NoTransaction FetchOnly
        (* Counting alone cannot tell the right children from the wrong ones: an entry returned
           with the right number of lines, every one of them belonging to some other entry,
           would satisfy a count. Identity is what the requirement is actually claiming. *)
        let expectedEntry = fixture.Data.jeWithLinesRefsAndComments
        let lineIdsOf je = je |> lines |> List.map JournalEntryLine.journalEntryLineId |> List.sort
        let refTextsOf je =
            je |> externalReferences
            |> List.map (JournalEntryExternalReference.referenceText >> JournalExternalReferenceText.value)
            |> List.sort
        let commentTextsOf je =
            je |> comments
            |> List.map (JournalEntryComment.commentText >> CommentText.value)
            |> List.sort
        let result = fixture.Data.jeWithLinesRefsAndCommentsId |> fetchById context
        match result with
        | Ok je ->
            Assert.NotNull(je |> header)
            Assert.NotEmpty(expectedEntry |> lineIdsOf)
            Assert.NotEmpty(expectedEntry |> refTextsOf)
            Assert.NotEmpty(expectedEntry |> commentTextsOf)
            Assert.Equal<JournalEntryLineId list>(expectedEntry |> lineIdsOf, je |> lineIdsOf)
            Assert.Equal<string list>(expectedEntry |> refTextsOf, je |> refTextsOf)
            Assert.Equal<string list>(expectedEntry |> commentTextsOf, je |> commentTextsOf)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns all entries for a given fiscal period``() =
        let today = Calendar.today()
        let monthF = today.Month.ToString("D2")
        let periodKey = $"{today.Year}-{monthF}"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fpUuid = periodKey |> fiscalPeriodKeyToId.fetch context
            let fpId = fpUuid |> FiscalPeriodId.fromGuid
            let expected =
                fixture.Data.journalEntries
                |> List.filter(fun x -> x |> header |> JournalEntryHeader.entryDate |> EntryDate.fiscalPeriodId = fpId)
                |> List.length
            let! fp = fpId |> FiscalPeriod.fetchById context
            let! fetchList = fp |> fetchByPeriod context
            let actual = fetchList |> List.length
            Assert.Equal(expected, actual)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns empty list for period with no entries``() =
        let today = Calendar.today()
        let farDate = today.PlusMonths(4)
        let monthF = farDate.Month.ToString("D2")
        let periodKey = $"{farDate.Year}-{monthF}"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! uuid = periodKey |> fiscalPeriodKeyToId.fetch context
            let! fp = uuid |> FiscalPeriodId.fromGuid |> FiscalPeriod.fetchById context
            let! entries = fp |> fetchByPeriod context
            Assert.Equal(0, entries |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns entries matching source FI and reference value``() =
        let fiStr = "TestBank"
        let refStr = "TXN-001"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let expected =
                distinctEntryCountMatching (fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi
                    && jer |> JournalEntryExternalReference.referenceText = refText)
            let! fetched = fetchByReference context (Some fi) (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            Assert.True(
                fetched
                |> List.forall(fun fetchedEntry ->
                    fetchedEntry
                    |> externalReferences
                    |> List.exists(fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi
                    && jer |> JournalEntryExternalReference.referenceText = refText)
                    ), "Every fetched entry must carry a reference matching the search criteria")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared``() =
        let fiStr = "TestBank"
        let refStr = "F-SHARED-001"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let expected =
                distinctEntryCountMatching (fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi
                    && jer |> JournalEntryExternalReference.referenceText = refText)
            let! fetched = fetchByReference context (Some fi) (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference returns empty list for nonexistent reference``() =
        let fiStr = "Bogus"
        let refStr = "Nada"
        let expected = 0
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let! fetched = fetchByReference context (Some fi) (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.8 fetchByReference with FI only returns all entries for that FI``() =
        let fiStr = "TestBank"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let expected =
                distinctEntryCountMatching (fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi)
            let! fetched = fetchByReference context (Some fi) None
            Assert.Equal(expected, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.8 fetchByReference with reference text only only returns all entries that match``() =
        let refStr = "TXN-001"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! refText = refStr |> JournalExternalReferenceText.create
            let expected =
                distinctEntryCountMatching (fun jer ->
                    jer |> JournalEntryExternalReference.referenceText = refText)
            let! fetched = fetchByReference context None (Some refText)
            Assert.Equal(expected, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 REQ-JE-3.8 fetchByReference with both parameters None returns Error``() =
        let context = Context.create NoTransaction FetchOnly
        match fetchByReference context None None with
        | Error(JournalEntryFetchByReferenceBothArgumentsNull) -> ()
        | Error e -> Assert.Fail(AppError.toMessage e)
        | Ok _ -> Assert.Fail "Expected failure; got success"

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns entries within inclusive date range``() =
        let context = Context.create NoTransaction FetchOnly
        let today = Calendar.today()
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let entryDate = je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                entryDate >= today && entryDate <= today)
            |> List.length
        let result = fetchByDateRange context today today
        match result with
        | Ok entries -> Assert.Equal(expected, entries |> List.length)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns empty list when no entries in range``() =
        let context = Context.create NoTransaction FetchOnly
        let farDate = NodaTime.LocalDate(2050, 1, 1)
        let result = fetchByDateRange context farDate farDate
        match result with
        | Ok entries -> Assert.Equal(0, entries |> List.length)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route returns exit code 1 for nonexistent ID``() = // todo refactor to use ModelOrchestrator instead of command routes
        result {
            let! payload = { JournalEntryFetchByIdInput.id = Guid.NewGuid() } |> toJson<JournalEntryFetchByIdInput>
            do!
                isCorrectError
                    (routeUiCommandForTesting "JournalEntry" "FetchById" [] payload)
                    JournalEntryHeaderIdDoesntExist
                    None
            return ()
        }
        |> railroadWrapper
