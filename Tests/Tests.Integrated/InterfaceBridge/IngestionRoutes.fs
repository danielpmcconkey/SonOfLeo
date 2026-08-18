module Tests.Integrated.InterfaceBridge.IngestionRoutes

open System
open System.IO
open DataAccessLayer.DbTransaction
open InterfaceBridge.InterfaceContracts.IngestionContracts
open InterfaceBridge.InterfaceContracts.ReportsContracts
open Logger.Audit
open Model.DataIngestion
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
(* JournalEntry first, StageEntryOrchestration second: both expose `lines`, and the staged
   side is what the bulk of this file reads. The ledger-side name used here is
   `fetchByReference`, which only the JournalEntry module defines. *)
open ModelOrchestrator.JournalEntries.JournalEntry
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open Utilities
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.Json.Json
open Utilities.ResultHelper
open Xunit


[<Collection("SharedTestData")>]
type IngestionRouteTests(fixture: TestDataFixture) =

    (* The route reads a real file off disk and, on success, moves it into the processed
       directory under a timestamped name. These are container-local scratch directories;
       each test deletes the file it wrote, from both, in its finally. *)
    static let testRoot = Path.Combine(Path.GetTempPath(), "sonofleo-route-tests")
    static let importDir = Path.Combine(testRoot, "import")
    static let processedDir = Path.Combine(testRoot, "processed")

    static let today = Calendar.today().ToString("yyyy-MM-dd", null)

    static let quotedOrNull =
        function
        | Some(s: string) -> $"\"{s}\""
        | None -> "null"

    /// One line of the base staging format, as a parser would emit it.
    static let rawRow groupId entryDate description fiSource fiReference amount lineType accountCode memo =
        $"""{{"baseStageEntryGroupId":"%s{groupId}","entryDate":"%s{entryDate}","description":"%s{description}","fiSource":"%s{fiSource}","fiReference":"%s{fiReference}","amount":%s{amount},"entryType":"%s{lineType}","accountCode":%s{quotedOrNull accountCode},"memo":%s{quotedOrNull memo}}}"""

    (* An InlineData attribute cannot hold a 1001-character literal, so over-length rows
       carry the sentinel "tooLong" and it is expanded here to one character past the
       field's documented maximum. The expansion is derived from the maximum rather than
       hard-coded so the row proves where the boundary actually sits. *)
    static let maxLengthOf =
        function
        | "description"
        | "memo" -> 1000
        | "fiSource"
        | "fiReference" -> 100
        | other -> failwith $"No maximum length is defined for field {other}."

    static let writeImportFile fileName (rows: string list) =
        Directory.CreateDirectory importDir |> ignore
        Directory.CreateDirectory processedDir |> ignore
        File.WriteAllLines(Path.Combine(importDir, fileName), rows)

    static let deleteImportFile fileName =
        File.Delete(Path.Combine(importDir, fileName))
        if Directory.Exists processedDir then
            Directory.GetFiles(processedDir, $"*-{fileName}") |> Array.iter File.Delete

    static let ingestPayload fileName =
        { IngestRawFileToStageInput.fileName = fileName
          importDir = importDir
          processedDir = processedDir }
        |> toJson<IngestRawFileToStageInput>
        |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

    /// Writes a one-defect file, asserts the route rejects it with the exact error, cleans up.
    static let assertRouteRejects fileName rows expectedError =
        try
            writeImportFile fileName rows
            result {
                do!
                    isCorrectErrorString
                        (routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName))
                        expectedError
                        (Some "The file may have been moved to the processed directory.")
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName

    /// Runs a file through the ingestion route and hands back the parsed result. The route
    /// commits, so every caller owns the staged entries that come back and must clean them up.
    static let ingestThroughRoute fileName rows =
        writeImportFile fileName rows
        result {
            let! resultPayload =
                routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName)
            return! fromJson<IngestionFullResultReturn> resultPayload
        }

    static let headerIdsToCleanUp (fullResult: IngestionFullResultReturn) =
        fullResult.stagedEntries
        |> List.map (fun entry -> entry.stageEntryHeader.stageEntryHeaderId |> StageEntryHeaderId.fromGuid |> Some)

    static let postThroughRoute isShadow =
        result {
            let! payload = { PostStageEntriesInput.isShadow = isShadow } |> toJson<PostStageEntriesInput>
            let! resultPayload = routeUiCommandForTesting "Ingestion" "PostStageEntries" [] payload
            return! fromJson<PostStageEntriesFullResult> resultPayload
        }

    /// Reads a staged entry back through the fetch path the route wrote to, so assertions
    /// about persisted state are made outside whatever transaction the route managed.
    static let refetchStageEntry (headerIdGuid: Guid) =
        let context = Context.create NoTransaction FetchOnly
        headerIdGuid
        |> StageEntryHeaderId.fromGuid
        |> fetchByStageEntryHeaderId context

    static let latestStatusOf (entry: StageEntry) =
        entry
        |> statusTransitions
        |> List.sortByDescending (fun t -> t |> StageEntryStatusTransition.instant)
        |> List.head
        |> StageEntryStatusTransition.toStatus

    /// Two balanced groups, both fully classifiable against the fixture's TestBank rules.
    static let twoValidGroups referenceOne referenceTwo =
        [ rawRow "grp-route-a" today "Route ingest first group" "TestBank" referenceOne "42.10" "Debit" None None
          rawRow "grp-route-a" today "Route ingest first group" "TestBank" referenceOne "42.10" "Credit" (Some "F-1270") None
          rawRow "grp-route-b" today "Route ingest second group" "TestBank" referenceTwo "18.00" "Debit" (Some "F-5300") None
          rawRow "grp-route-b" today "Route ingest second group" "TestBank" referenceTwo "18.00" "Credit" (Some "F-1270") None ]


    [<Fact>]
    member _.``REQ-STG-3.1 IngestRawFileToStage route ingests valid file and returns result`` () =
        let fileName = "ingestion-route-happy-path.jsonl"
        let mutable idsToCleanUp = []
        try
            result {
                let! fullResult =
                    twoValidGroups "REF-ROUTE-INGEST-001" "REF-ROUTE-INGEST-002" |> ingestThroughRoute fileName
                idsToCleanUp <- fullResult |> headerIdsToCleanUp
                Assert.Equal(2, fullResult.stagedEntries |> List.length)
                Assert.Empty(fullResult.newDuplicates)
                let firstGroup =
                    fullResult.stagedEntries
                    |> List.find (fun entry -> entry.stageEntryHeader.description = "Route ingest first group")
                Assert.Equal("TestBank", firstGroup.stageEntryHeader.ingestionSource)
                Assert.Equal("REF-ROUTE-INGEST-001", firstGroup.stageEntryHeader.fiReference)
                Assert.Equal(2, firstGroup.lines |> List.length)
                Assert.Equal("Classified", firstGroup.stageEntryHeader.status)
                (* The route's contract includes relocating the file it consumed, so a caller
                   can tell an ingested file from one still waiting. *)
                Assert.False(File.Exists(Path.Combine(importDir, fileName)))
                Assert.NotEmpty(Directory.GetFiles(processedDir, $"*-{fileName}"))
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

    [<Theory>]
    [<InlineData("entryDate", "not-a-date", "InterfaceBridgeFailedJsonDeserialization")>]
    [<InlineData("amount", "32.475", "MoneyFailedToConvertImproperPrecision")>]
    [<InlineData("amount", "19999999999.99", "MoneyFailedToConvertExceededMax")>]
    [<InlineData("entryType", "Sideways", "JournalEntryLineTypeInvalid")>]
    [<InlineData("accountCode", "", "AccountCodeIsEmpty")>]
    [<InlineData("description", "", "JournalEntryDescriptionIsEmpty")>]
    [<InlineData("description", "tooLong", "JournalEntryDescriptionTooLong")>]
    [<InlineData("fiSource", "", "JournalRefFinancialInstitutionIsEmpty")>]
    [<InlineData("fiSource", "tooLong", "JournalRefFinancialInstitutionTooLong")>]
    [<InlineData("fiReference", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData("fiReference", "tooLong", "JournalEntryReferenceTextTooLong")>]
    [<InlineData("memo", "", "JournalEntryLineMemoIsEmpty")>]
    [<InlineData("memo", "tooLong", "JournalEntryLineMemoTooLong")>]
    member _.``REQ-STG-1.5 REQ-STG-1.6 REQ-STG-1.7 REQ-STG-1.8 REQ-STG-1.9 REQ-STG-1.10 REQ-STG-1.11 REQ-STG-1.12 IngestRawFileToStage validates input as valid types``
        (field: string, value: string, expectedError: string)
        =
        let valueToUse =
            if value = "tooLong" then String.replicate (maxLengthOf field + 1) "x" else value
        let entryDateToUse = if field = "entryDate" then valueToUse else today
        let descriptionToUse = if field = "description" then valueToUse else "Route validation test entry"
        let fiSourceToUse = if field = "fiSource" then valueToUse else "TestBank"
        let fiReferenceToUse = if field = "fiReference" then valueToUse else "REF-ROUTE-VALIDATION"
        let amountToUse = if field = "amount" then valueToUse else "32.47"
        let entryTypeToUse = if field = "entryType" then valueToUse else "Debit"
        let accountCodeToUse = if field = "accountCode" then Some valueToUse else None
        let memoToUse = if field = "memo" then Some valueToUse else None
        (* Header fields and the amount are repeated on both rows so the group stays
           internally consistent and balanced; only the defect under test is wrong. Line
           fields are fixed on the second row, so a line defect lands on the first alone. *)
        let rows =
            [ rawRow
                  "grp-route-validation"
                  entryDateToUse
                  descriptionToUse
                  fiSourceToUse
                  fiReferenceToUse
                  amountToUse
                  entryTypeToUse
                  accountCodeToUse
                  memoToUse
              rawRow
                  "grp-route-validation"
                  entryDateToUse
                  descriptionToUse
                  fiSourceToUse
                  fiReferenceToUse
                  amountToUse
                  "Credit"
                  (Some "F-1270")
                  None ]
        assertRouteRejects $"ingestion-route-{expectedError}.jsonl" rows expectedError

    (* The route rejects an update whose header is entirely NoChange, so neither call below
       can be a line-only edit. See BdsNotes/finding-2026-08-18-update-route-header-required.md. *)
    [<Fact>]
    member _.``REQ-STG-6.1 REQ-STG-6.2 UpdateStageEntry route happy path`` () =
        let fileName = "ingestion-route-update.jsonl"
        let description = "Route ingest first group"
        let mutable idsToCleanUp = []
        try
            result {
                let! ingested =
                    twoValidGroups "REF-ROUTE-UPDATE-001" "REF-ROUTE-UPDATE-002" |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let toUpdate =
                    ingested.stagedEntries
                    |> List.find (fun entry -> entry.stageEntryHeader.description = description)
                let headerId = toUpdate.stageEntryHeader.stageEntryHeaderId
                let debitLine = toUpdate.lines |> List.find (fun line -> line.lineType = "Debit")
                (* The classifier assigned this line's code. REQ-STG-6.1 says the operator
                   overrides it regardless of which layer put it there. *)
                Assert.Equal(Some "F-5300", debitLine.accountCode)
                let overrideCodeWith newCode : UpdateStageEntryLineInput =
                    { stageEntryLineId = debitLine.stageEntryLineId
                      amount = NoChange
                      lineType = NoChange
                      accountCode = SetTo (Some newCode)
                      memo = NoChange
                      classificationRuleId = NoChange }
                let updateThroughRoute (input: UpdateStageEntryInput) =
                    result {
                        let! payload = input |> toJson<UpdateStageEntryInput>
                        let! resultPayload = routeUiCommandForTesting "Ingestion" "UpdateStageEntry" [] payload
                        return! fromJson<StageEntryReturn> resultPayload
                    }
                let codeOf (entry: StageEntryReturn) =
                    entry.lines
                    |> List.find (fun line -> line.stageEntryLineId = debitLine.stageEntryLineId)
                    |> _.accountCode
                // the ordinary review flow: override the code and declare the entry reviewed
                let! afterReview =
                    updateThroughRoute
                        { stageEntryHeaderId = headerId
                          sourceFileUpdate = NoChange
                          entryDate = NoChange
                          description = NoChange
                          ingestionSource = NoChange
                          fiReference = NoChange
                          status = SetTo "Reviewed"
                          lines = [ overrideCodeWith "F-5650" ] }
                Assert.Equal(Some "F-5650", afterReview |> codeOf)
                Assert.Equal("Reviewed", afterReview.stageEntryHeader.status)
                (* REQ-STG-6.2: the system validates the result but does not infer status from
                   the operator's changes. A second override with status left alone must not
                   move the entry anywhere. The description is re-set to the value it already
                   holds purely to give the header something to update. *)
                let! afterSecondOverride =
                    updateThroughRoute
                        { stageEntryHeaderId = headerId
                          sourceFileUpdate = NoChange
                          entryDate = NoChange
                          description = SetTo description
                          ingestionSource = NoChange
                          fiReference = NoChange
                          status = NoChange
                          lines = [ overrideCodeWith "F-5350" ] }
                Assert.Equal(Some "F-5350", afterSecondOverride |> codeOf)
                Assert.Equal("Reviewed", afterSecondOverride.stageEntryHeader.status)
                // both edits are durable outside the transaction the route managed
                let! refetched = refetchStageEntry headerId
                Assert.Equal(Reviewed, refetched |> latestStatusOf)
                let refetchedCode =
                    refetched
                    |> lines
                    |> List.find (fun line ->
                        line |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value = debitLine.stageEntryLineId)
                    |> StageEntryLine.accountCode
                    |> Option.map AccountCode.value
                Assert.Equal(Some "F-5350", refetchedCode)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

    (* The orchestrator-level shadow post test observes from inside the very transaction that
       is the feature, so it cannot tell a shadow post from a real one. This one asks the
       question from outside: the route has returned and committed nothing, so any ledger row
       it wrote is gone. *)
    [<Fact>]
    member _.``REQ-STG-8.1 PostStageEntries shadow route returns trial balances and wasRolledBack true`` () =
        let fileName = "ingestion-route-shadow-post.jsonl"
        let referenceOne = "REF-ROUTE-SHADOW-001"
        let referenceTwo = "REF-ROUTE-SHADOW-002"
        let mutable idsToCleanUp = []
        try
            result {
                let! ingested = twoValidGroups referenceOne referenceTwo |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let! postResult = postThroughRoute true
                Assert.True(postResult.wasRolledBack, "The shadow route must report that it rolled back")
                Assert.NotEmpty(postResult.trialBalanceBefore)
                Assert.NotEmpty(postResult.trialBalanceAfter)
                (* Both snapshots are taken inside the rolled-back transaction, so the after
                   must already carry the two groups' debits — an unmoved balance means
                   nothing was posted to measure. Both debit legs classify to F-5300. *)
                let debitsFor code (rows: TrialBalanceReturnRow list) =
                    rows |> List.find (fun row -> row.accountCode = code) |> _.totalDebits
                Assert.Equal(
                    (postResult.trialBalanceBefore |> debitsFor "F-5300") + 60.10M,
                    postResult.trialBalanceAfter |> debitsFor "F-5300")
                let context = Context.create NoTransaction FetchOnly
                let! financialInstitution = "TestBank" |> JournalRefFinancialInstitution.create
                let! firstReference = referenceOne |> JournalExternalReferenceText.create
                let! secondReference = referenceTwo |> JournalExternalReferenceText.create
                let! firstPosted = fetchByReference context (Some financialInstitution) (Some firstReference)
                let! secondPosted = fetchByReference context (Some financialInstitution) (Some secondReference)
                Assert.Empty(firstPosted)
                Assert.Empty(secondPosted)
                // and staging is untouched: the entries are still sitting there postable
                let! refetched = refetchStageEntry (ingested.stagedEntries |> List.head).stageEntryHeader.stageEntryHeaderId
                Assert.Equal(Classified, refetched |> latestStatusOf)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-STG-9.1 PostStageEntries real route posts entries and returns wasRolledBack false`` () =
        let fileName = "ingestion-route-real-post.jsonl"
        let referenceOne = "REF-ROUTE-REALPOST-001"
        let referenceTwo = "REF-ROUTE-REALPOST-002"
        let mutable idsToCleanUp = []
        let mutable journalEntryIdsToCleanUp = []
        try
            result {
                let! ingested = twoValidGroups referenceOne referenceTwo |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let! postResult = postThroughRoute false
                Assert.False(postResult.wasRolledBack, "The real route must report that it committed")
                let context = Context.create NoTransaction FetchOnly
                let! financialInstitution = "TestBank" |> JournalRefFinancialInstitution.create
                let! firstReference = referenceOne |> JournalExternalReferenceText.create
                let! secondReference = referenceTwo |> JournalExternalReferenceText.create
                let! firstPosted = fetchByReference context (Some financialInstitution) (Some firstReference)
                let! secondPosted = fetchByReference context (Some financialInstitution) (Some secondReference)
                journalEntryIdsToCleanUp <-
                    firstPosted @ secondPosted
                    |> List.map (header >> JournalEntryHeader.journalEntryHeaderId >> Some)
                Assert.Equal(1, firstPosted |> List.length)
                Assert.Equal(1, secondPosted |> List.length)
                let! refetched = refetchStageEntry (ingested.stagedEntries |> List.head).stageEntryHeader.stageEntryHeaderId
                Assert.Equal(Posted, refetched |> latestStatusOf)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            // journal entries first: they are the rows the post created, and nothing depends on them
            match cleanUpJournalEntryList journalEntryIdsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-STG-2.4 CreateIngestionSource route happy path`` () =
        let sourceName = "RouteTestCreditUnion"
        let mutable idToCleanUp = None
        try
            result {
                let! payload = { CreateNewIngestionSourceInput.name = sourceName } |> toJson<CreateNewIngestionSourceInput>
                let! resultPayload = routeUiCommandForTesting "Ingestion" "CreateIngestionSource" [] payload
                let! returned = fromJson<IngestionSourceReturn> resultPayload
                idToCleanUp <- returned.ingestionSourceId |> IngestionSourceId.fromGuid |> Some
                Assert.Equal(sourceName, returned.name)
                Assert.NotEqual(Guid.Empty, returned.ingestionSourceId)
                (* A staged entry's source_id must point at a row in ingestion.source, so the
                   created source has to be resolvable by the same lookup ingestion uses. *)
                let context = Context.create NoTransaction FetchOnly
                let! name = sourceName |> JournalRefFinancialInstitution.create
                let! fetched = name |> IngestionSource.fetchByName context
                Assert.Equal(returned.ingestionSourceId, fetched |> IngestionSource.ingestionSourceId |> IngestionSourceId.value)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpIngestionSourceId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)
