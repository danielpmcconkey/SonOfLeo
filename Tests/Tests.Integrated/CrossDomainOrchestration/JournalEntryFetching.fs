namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open App.DataAccessLayer
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open App.DataAccessLayer.DbTransaction
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts
open App.Utility.Json.Json
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Tests.Helpers.RouteResolver
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open Tests.Helpers
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Business.FinancialServices.Ledger.LedgerError


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
        | Error e -> Assert.Fail(e.ToMessage())

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
        let lineIdsOf je = je |> jeLines |> List.map JournalEntryLine.journalEntryLineId |> List.sort
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
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-JE-3.3 fetchByPeriod returns all entries for a given fiscal period``() =
        let today = Calendar.today()
        let monthF = today.Month.ToString("D2")
        let periodKey = $"{today.Year}-{monthF}"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fpUuid = periodKey |> Business.FinancialServices.Ledger.FiscalPeriod.keyToId.fetch (context |> Context.getDatabaseTransaction)
            let fpId = fpUuid |> FiscalPeriodId.fromGuid
            let expected =
                fixture.Data.journalEntries
                |> List.filter(fun x -> x |> header |> JournalEntryHeader.entryDate |> EntryDate.fiscalPeriodId = fpId)
            let! fp = fpId |> FiscalPeriod.fetchById context
            let! fetchList = fp |> fetchByPeriod context
            Assert.Equal(expected |> List.length, fetchList |> List.length)
            (* Entries from the wrong period in the right quantity satisfy a count. *)
            Assert.Equal<JournalEntryHeaderId list>(
                expected |> List.map(header >> JournalEntryHeader.journalEntryHeaderId) |> List.sort,
                fetchList |> List.map(header >> JournalEntryHeader.journalEntryHeaderId) |> List.sort)
            Assert.All(
                fetchList,
                fun je -> Assert.Equal(fpId, je |> header |> JournalEntryHeader.entryDate |> EntryDate.fiscalPeriodId))
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
            let! uuid = periodKey |> Business.FinancialServices.Ledger.FiscalPeriod.keyToId.fetch (context |> Context.getDatabaseTransaction)
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
    member _.``REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns exactly the entries that share the reference``() =
        let fiStr = "TestBank"
        let refStr = "F-SHARED-001"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let! refText = refStr |> JournalExternalReferenceText.create
            let expectedIds =
                fixture.Data.journalEntryExternalReferences
                |> List.filter (fun jer ->
                    jer |> JournalEntryExternalReference.financialInstitution = fi
                    && jer |> JournalEntryExternalReference.referenceText = refText)
                |> List.map JournalEntryExternalReference.journalEntryHeaderId
                |> Set.ofList
            Assert.True(expectedIds.Count > 1, "the fixture must share this reference across entries")
            let! fetched = fetchByReference context (Some fi) (Some refText)
            Assert.Equal<Set<JournalEntryHeaderId>>(expectedIds, fetched |> List.map (header >> JournalEntryHeader.journalEntryHeaderId) |> Set.ofList)
            Assert.Equal(expectedIds.Count, fetched |> List.length)
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
    member _.``REQ-JE-3.5 fetchByReference with FI only returns exactly the entries carrying a reference from that FI``() =
        let fiStr = "TestBank"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fi = fiStr |> JournalRefFinancialInstitution.create
            let expectedIds =
                fixture.Data.journalEntryExternalReferences
                |> List.filter (fun jer -> jer |> JournalEntryExternalReference.financialInstitution = fi)
                |> List.map JournalEntryExternalReference.journalEntryHeaderId
                |> Set.ofList
            Assert.NotEmpty(expectedIds)
            let! fetched = fetchByReference context (Some fi) None
            Assert.Equal<Set<JournalEntryHeaderId>>(expectedIds, fetched |> List.map (header >> JournalEntryHeader.journalEntryHeaderId) |> Set.ofList)
            Assert.Equal(expectedIds.Count, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 fetchByReference with reference text only returns exactly the entries carrying that reference text``() =
        let refStr = "TXN-001"
        let context = Context.create NoTransaction FetchOnly
        result {
            let! refText = refStr |> JournalExternalReferenceText.create
            let expectedIds =
                fixture.Data.journalEntryExternalReferences
                |> List.filter (fun jer -> jer |> JournalEntryExternalReference.referenceText = refText)
                |> List.map JournalEntryExternalReference.journalEntryHeaderId
                |> Set.ofList
            Assert.NotEmpty(expectedIds)
            let! fetched = fetchByReference context None (Some refText)
            Assert.Equal<Set<JournalEntryHeaderId>>(expectedIds, fetched |> List.map (header >> JournalEntryHeader.journalEntryHeaderId) |> Set.ofList)
            Assert.Equal(expectedIds.Count, fetched |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5.1 fetchByReference with both parameters None returns Error``() =
        let context = Context.create NoTransaction FetchOnly
        match fetchByReference context None None with
        | Error (AsError (JournalEntryFetchByReferenceBothArgumentsNull)) -> ()
        | Error e -> Assert.Fail(e.ToMessage())
        | Ok _ -> Assert.Fail "Expected failure; got success"

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange over a range of several days returns exactly the entries dated on its begin date, between, and on its end date, and none from the day before it or the day after`` () =
        (* In a rolled-back transaction: one entry on each of the day before the range, its begin date, a day between,
           its end date and the day after. The fixture's own entries in the range are expected too. *)
        let p = fixture.Data.positions
        let today = Calendar.today ()
        let beginDate, endDate = today.PlusDays(-6), today.PlusDays(-3)
        Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! posted =
                    [ beginDate.PlusDays(-1); beginDate; beginDate.PlusDays(1); endDate; endDate.PlusDays(1) ]
                    |> List.map (fun date ->
                        EntityFunctions.createTestJournalEntryFromPrimitives
                            context $"Date range test {date}" None date
                            [ (p.positionsCash1275Id, 10.00M, "Debit", None); (p.positionsEquity3040Id, 10.00M, "Credit", None) ] [] []
                        |> Result.map (fun (_, id) -> date, id))
                    |> convertListOfResultsToResultsList
                let inRange (date: NodaTime.LocalDate) = date >= beginDate && date <= endDate
                let expected =
                    (fixture.Data.journalEntries
                     |> List.filter (fun je -> je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate |> inRange)
                     |> List.map (header >> JournalEntryHeader.journalEntryHeaderId))
                    @ (posted |> List.filter (fst >> inRange) |> List.map snd)
                    |> List.sort
                let! fetched = fetchByDateRange context beginDate endDate
                Assert.Equal(3, posted |> List.filter (fst >> inRange) |> List.length)
                Assert.Equal<JournalEntryHeaderId list>(
                    expected, fetched |> List.map (header >> JournalEntryHeader.journalEntryHeaderId) |> List.sort)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns entries within inclusive date range``() =
        let context = Context.create NoTransaction FetchOnly
        let today = Calendar.today()
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let entryDate = je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                entryDate >= today && entryDate <= today)
        let result = fetchByDateRange context today today
        match result with
        | Ok entries ->
            Assert.Equal(expected |> List.length, entries |> List.length)
            (* Entries dated outside the range, in the right quantity, satisfy a count. The
               range is a single day here, so every returned entry must sit exactly on it. *)
            Assert.Equal<JournalEntryHeaderId list>(
                expected |> List.map(header >> JournalEntryHeader.journalEntryHeaderId) |> List.sort,
                entries |> List.map(header >> JournalEntryHeader.journalEntryHeaderId) |> List.sort)
            Assert.All(
                entries,
                fun je -> Assert.Equal(today, je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate))
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-JE-3.7 fetchByDateRange returns empty list when no entries in range``() =
        let context = Context.create NoTransaction FetchOnly
        let farDate = NodaTime.LocalDate(2050, 1, 1)
        let result = fetchByDateRange context farDate farDate
        match result with
        | Ok entries -> Assert.Equal(0, entries |> List.length)
        | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route returns exit code 1 for nonexistent ID``() = // todo refactor to use Business.FinancialServices instead of command routes
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

    [<Fact>]
    member _.``REQ-JE-3.5 a lookup by source FI returns the entries whose source FI equals it exactly, and no entry whose source FI differs only in letter case or merely contains it`` () =
        // In a rolled-back transaction: one entry from each of three institutions, all with the same reference value.
        let p = fixture.Data.positions
        Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let entry fi =
                    EntityFunctions.createTestJournalEntryFromPrimitives
                        context $"Exact FI lookup {fi}" None p.ledgerEntryDate
                        [ (p.positionsCash1275Id, 10.00M, "Debit", None); (p.positionsEquity3040Id, 10.00M, "Credit", None) ]
                        [ fi, "EXACT-FI-REF" ] []
                    |> Result.map snd
                let! exactId = entry "Exact Example Bank"
                let! _ = entry "exact example bank"
                let! _ = entry "Exact Example Bank Online"
                let! fi = "Exact Example Bank" |> JournalRefFinancialInstitution.create
                let! fetched = fetchByReference context (Some fi) None
                Assert.Equal<JournalEntryHeaderId list>(
                    [ exactId ], fetched |> List.map (header >> JournalEntryHeader.journalEntryHeaderId))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 a lookup by reference value returns the entries whose reference value equals it exactly, and no entry whose reference value differs only in letter case or merely contains it`` () =
        // In a rolled-back transaction: three entries from one institution, each with its own reference value.
        let p = fixture.Data.positions
        Ui.InterfaceBridge.CommandRoute.runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let entry reference =
                    EntityFunctions.createTestJournalEntryFromPrimitives
                        context $"Exact reference lookup {reference}" None p.ledgerEntryDate
                        [ (p.positionsCash1275Id, 10.00M, "Debit", None); (p.positionsEquity3040Id, 10.00M, "Credit", None) ]
                        [ "Exact Reference Example Bank", reference ] []
                    |> Result.map snd
                let! exactId = entry "Exact-Ref-077"
                let! _ = entry "EXACT-REF-077"
                let! _ = entry "Exact-Ref-0770"
                let! reference = "Exact-Ref-077" |> JournalExternalReferenceText.create
                let! fetched = fetchByReference context None (Some reference)
                Assert.Equal<JournalEntryHeaderId list>(
                    [ exactId ], fetched |> List.map (header >> JournalEntryHeader.journalEntryHeaderId))
            })
        |> railroadWrapper
