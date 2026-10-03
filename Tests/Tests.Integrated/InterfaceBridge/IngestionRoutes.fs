module Tests.Integrated.InterfaceBridge.IngestionRoutes

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open System.IO
open App.DataAccessLayer.DbTransaction
open App.Operation.AuditEnvelope
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
(* JournalEntry first, StageEntryOrchestration second: both expose `lines`, and the staged
   side is what the bulk of this file reads. The ledger-side name used here is
   `fetchByReference`, which only the JournalEntry module defines. *)
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open App.Utility.FieldUpdate
open App.Utility.Json.Json
open App.Utility.Result
open Xunit
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.BizFinServError

open Ui.InterfaceBridge.InterfaceContracts.IngestionContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

/// What the single ingest route used to return, rebuilt from the three routes an operator now runs in its place.
type IngestionPipelineReturn =
    { stagedEntries: StageEntryReturn list
      newDuplicates: StageEntryReturn list }


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
        |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))

    /// Writes a one-defect file, asserts the route rejects it with the exact error, cleans up.
    /// The route's rejection of a file: every failing record, in file order. Fails the test on any other outcome.
    static let rejectionOf fileName : DataIngestionError.IngestionRejectedRecord list =
        match routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName) with
        | Error (AsError (DataIngestionError.IngestionFileRejected (filePath, records))) ->
            Assert.Equal(Path.Combine(importDir, fileName), filePath)
            records
        | Ok _ -> failwith "Expected the file to be rejected; it was staged. It may have been moved to the processed directory."
        | Error e -> failwith $"Expected IngestionFileRejected; got {e.DomainName}.{e.CaseName}: {e.ToMessage()}"

    (* A one-defect file. The defect may be on more than one record (the amount theory repeats the amount on both rows),
       so every record the rejection names must carry the expected error. *)
    static let assertRouteRejects fileName rows expectedError =
        try
            writeImportFile fileName rows
            let records = rejectionOf fileName
            Assert.NotEmpty records
            for record in records do
                Assert.Equal(expectedError, record.error.CaseName)
        finally
            deleteImportFile fileName

    (* ---------------------------------------------------------------------
       FetchStageEntryFiltered — the route that carried a dead column reference
       through two audits because nothing ever called it.
       --------------------------------------------------------------------- *)

    static let noFilterInput: StageEntryFetchFilterInput =
        { stageEntryHeaderId = None
          sourceFile = None
          temporalFilter = None
          description = None
          ingestionSource = None
          fiReference = None
          status = None
          stageEntryLineId = None
          amount = None
          lineType = None
          accountCode = None
          memo = None
          journalEntryHeaderId = None
          journalEntryLineId = None }

    static let fetchFilteredThroughRoute filter =
        result {
            let! payload =
                { StageEntryFetchFilteredInput.filter = filter; sort = None }
                |> toJson<StageEntryFetchFilteredInput>
            let! resultPayload = routeUiCommandForTesting "Ingestion" "FetchStageEntryFiltered" [] payload
            return! fromJson<StageEntryReturn list> resultPayload
        }

    /// Runs a file through ingestion the way an operator now does: the ingest route, then the dedup route, then
    /// account classification, which used to be one route. Hands back the file's entries as they stand afterwards
    /// and the ones dedup flagged. Every route commits, so every caller owns the staged entries that come back and
    /// must clean them up.
    static let ingestThroughRoute fileName rows =
        writeImportFile fileName rows
        result {
            let! ingestedPayload =
                routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName)
            let! ingested = fromJson<StageEntryReturn list> ingestedPayload
            let! _ = routeUiCommandForTesting "Ingestion" "DeduplicateStageEntries" [] ""
            let! _ = routeUiCommandForTesting "Classification" "ClassifyAccounts" [] ""
            let! stagedEntries =
                match ingested |> List.tryHead with
                | None -> Ok []
                | Some entry ->
                    fetchFilteredThroughRoute { noFilterInput with sourceFile = Some entry.stageEntryHeader.sourceFile }
            return
                { stagedEntries = stagedEntries
                  newDuplicates =
                    stagedEntries |> List.filter (fun entry -> entry.stageEntryHeader.status = Some "Duplicate") }
        }

    static let headerIdsToCleanUp (fullResult: IngestionPipelineReturn) =
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

    /// Two groups whose every line names its account outright, so the accounts a filter has to
    /// resolve are the ones these rows were written with rather than whatever the classifier
    /// would have picked.
    static let twoAccountedGroups =
        [ rawRow "grp-route-fetch-a" today "Route fetch first group" "TestBank" "REF-ROUTE-FETCH-001" "18.00" "Debit" (Some "F-5300") None
          rawRow "grp-route-fetch-a" today "Route fetch first group" "TestBank" "REF-ROUTE-FETCH-001" "18.00" "Credit" (Some "F-1270") None
          rawRow "grp-route-fetch-b" today "Route fetch second group" "TestBank" "REF-ROUTE-FETCH-002" "25.00" "Debit" (Some "F-5650") None
          rawRow "grp-route-fetch-b" today "Route fetch second group" "TestBank" "REF-ROUTE-FETCH-002" "25.00" "Credit" (Some "F-1270") None ]



    static let stagedFrom (sourceFilePath: string) =
        fetchFilteredThroughRoute { noFilterInput with sourceFile = Some sourceFilePath }

    (* REQ-STG-3.2.1 files. Each is written, rejected, and deleted; the rejection is compared as (lines, group_id, error
       case) in file order, and nothing from the file may be staged or moved. *)
    static let assertRejectsExactly fileName (rows: string list) expected =
        try
            writeImportFile fileName rows
            let records = rejectionOf fileName
            let actual = records |> List.map (fun r -> r.lineNumbers, r.groupId, r.error.CaseName)
            Assert.Equal<(int list * string option * string) list>(expected, actual)
            Assert.True(File.Exists(Path.Combine(importDir, fileName)), "the rejected file was moved")
            match stagedFrom (Path.Combine(importDir, fileName)) with
            | Ok staged -> Assert.Empty staged
            | Error e -> failwith (e.ToMessage())
        finally
            deleteImportFile fileName

    static let validRow groupId lineType =
        rawRow groupId today $"Valid {groupId}" "TestBank" $"REF-{groupId}" "25.00" lineType (Some "F-5350") None

    static let headerIdsOf (entries: StageEntryReturn list) =
        entries |> List.map (fun entry -> entry.stageEntryHeader.stageEntryHeaderId |> StageEntryHeaderId.fromGuid |> Some)

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
                Assert.Equal(Some "Classified", firstGroup.stageEntryHeader.status)
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
            | Error e -> failwith (e.ToMessage())

    [<Theory>]
    [<InlineData("entryDate", "not-a-date", "JsonDeserializationFailed")>]
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

    (* The empty-string case in the theory above is a format failure. This is an existence
       failure: a well-formed code that names no account. It can only be reached from the
       route, because the boundary converter resolves the code to an account ID and every
       layer below it receives the ID. *)
    [<Fact>]
    member _.``REQ-STG-3.7 IngestRawFileToStage rejects the file when an account code does not resolve to an existing account`` () =
        let rows =
            [ rawRow "grp-route-badcode" today "Route bad account code" "TestBank" "REF-ROUTE-BADCODE" "100.00" "Debit" (Some "BOGUS-9999") None
              rawRow "grp-route-badcode" today "Route bad account code" "TestBank" "REF-ROUTE-BADCODE" "100.00" "Credit" (Some "F-1270") None ]
        assertRouteRejects "ingestion-route-bad-account-code.jsonl" rows "AccountCodeDoesntMatchAccountId"

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
                      memo = NoChange }
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
                          status = SetTo { newStatus = "Reviewed" }
                          lines = [ overrideCodeWith "F-5650" ]
                          linesToAdd = []
                          lineIdsToRemove = [] }
                Assert.Equal(Some "F-5650", afterReview |> codeOf)
                Assert.Equal(Some "Reviewed", afterReview.stageEntryHeader.status)
                (* REQ-STG-6.2: the system validates the result but does not infer status from
                   the operator's changes. A line-only override must leave the entry where the
                   operator put it. *)
                let! afterSecondOverride =
                    updateThroughRoute
                        { stageEntryHeaderId = headerId
                          sourceFileUpdate = NoChange
                          entryDate = NoChange
                          description = NoChange
                          ingestionSource = NoChange
                          fiReference = NoChange
                          status = NoChange
                          lines = [ overrideCodeWith "F-5350" ]
                          linesToAdd = []
                          lineIdsToRemove = [] }
                Assert.Equal(Some "F-5350", afterSecondOverride |> codeOf)
                Assert.Equal(Some "Reviewed", afterSecondOverride.stageEntryHeader.status)
                // both edits are durable outside the transaction the route managed
                let! refetched = refetchStageEntry headerId
                Assert.Equal(Reviewed, refetched |> latestStatusOf)
                let refetchedAccountId =
                    refetched
                    |> seLines
                    |> List.find (fun line ->
                        line |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value = debitLine.stageEntryLineId)
                    |> StageEntryLine.accountId
                Assert.Equal(Some fixture.Data.food5350Id, refetchedAccountId)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

    (* No orchestrator-level test can carry either of these two: down there every test already
       runs inside a rolled-back transaction, so a shadow post and a real one leave identical
       evidence. This one asks the question from outside — the route has returned and committed
       nothing, so any ledger row it wrote is gone, and staging is exactly where the operator
       left it. *)
    [<Fact>]
    member _.``REQ-STG-8.1 REQ-STG-8.4 PostStageEntries shadow route returns trial balances and leaves ledger and staging untouched`` () =
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
            | Error e -> failwith (e.ToMessage())

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
            // staged entries first: posting links each staged header and line back to the journal entry it made
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())
            match cleanUpJournalEntryList journalEntryIdsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

    (* The all-or-nothing claim has two halves. That a poisoned batch fails is visible anywhere;
       that *none* of it lands is visible only out here, after the route has decided whether to
       commit. The two good groups are the point of the test — they would post cleanly on their
       own, so finding them absent is what proves the batch was atomic rather than merely
       unlucky. *)
    [<Fact>]
    member _.``REQ-STG-9.8 PostStageEntries real route commits no entry when one fails domain validation`` () =
        let fileName = "ingestion-route-atomic-post.jsonl"
        let referenceOne = "REF-ROUTE-ATOMIC-001"
        let referenceTwo = "REF-ROUTE-ATOMIC-002"
        let poisonReference = "REF-ROUTE-ATOMIC-POISON"
        let mutable idsToCleanUp = []
        let mutable journalEntryIdsToCleanUp = []
        try
            result {
                // the closed fiscal period is the cheapest way to make one entry fail JE validation
                let closedPeriodDate =
                    (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate)
                        .PlusDays(14)
                        .ToString("yyyy-MM-dd", null)
                let poisonGroup =
                    [ rawRow "grp-route-poison" closedPeriodDate "Route post closed period group" "TestBank" poisonReference "9.00" "Debit" (Some "F-5300") None
                      rawRow "grp-route-poison" closedPeriodDate "Route post closed period group" "TestBank" poisonReference "9.00" "Credit" (Some "F-1270") None ]
                let! ingested =
                    twoValidGroups referenceOne referenceTwo @ poisonGroup |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                Assert.Equal(3, ingested.stagedEntries |> List.length)
                do! isCorrectError (postThroughRoute false) JournalEntryHeaderEntryDateInvalid None
                let context = Context.create NoTransaction FetchOnly
                let! financialInstitution = "TestBank" |> JournalRefFinancialInstitution.create
                let! postedPerReference =
                    [ referenceOne; referenceTwo; poisonReference ]
                    |> List.map (fun reference ->
                        result {
                            let! referenceText = reference |> JournalExternalReferenceText.create
                            return! fetchByReference context (Some financialInstitution) (Some referenceText)
                        })
                    |> convertListOfResultsToResultsList
                let posted = postedPerReference |> List.concat
                // captured before the assertion so a failing test still cleans up what it found
                journalEntryIdsToCleanUp <-
                    posted |> List.map (header >> JournalEntryHeader.journalEntryHeaderId >> Some)
                Assert.Empty(posted)
                // and the staged entries are still sitting there postable, none of them marked Posted
                let! refetched =
                    ingested.stagedEntries
                    |> List.map (fun entry -> refetchStageEntry entry.stageEntryHeader.stageEntryHeaderId)
                    |> convertListOfResultsToResultsList
                refetched |> List.iter (fun entry -> Assert.Equal(Classified, entry |> latestStatusOf))
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())
            match cleanUpJournalEntryList journalEntryIdsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

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
            | Error e -> failwith (e.ToMessage())
    [<Fact>]
    member _.``REQ-STG-10.1 REQ-STG-10.6 FetchStageEntryFiltered route returns the staged entry with its lines and status transitions intact`` () =
        let fileName = "fetch-filtered-composition.jsonl"
        let mutable idsToCleanUp = []
        try
            result {
                let! ingested = twoAccountedGroups |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let staged =
                    ingested.stagedEntries
                    |> List.find (fun entry -> entry.stageEntryHeader.fiReference = "REF-ROUTE-FETCH-001")

                let! fetched =
                    fetchFilteredThroughRoute { noFilterInput with fiReference = Some "REF-ROUTE-FETCH-001" }
                Assert.Equal(1, fetched |> List.length)
                let returned = fetched |> List.head

                Assert.Equal(staged.stageEntryHeader.stageEntryHeaderId, returned.stageEntryHeader.stageEntryHeaderId)
                Assert.Equal("Route fetch first group", returned.stageEntryHeader.description)
                Assert.Equal("TestBank", returned.stageEntryHeader.ingestionSource)
                Assert.Equal(staged.stageEntryHeader.entryDate, returned.stageEntryHeader.entryDate)
                Assert.Equal(staged.stageEntryHeader.status, returned.stageEntryHeader.status)

                Assert.Equal(2, returned.lines |> List.length)
                Assert.Equal<decimal list>(
                    staged.lines |> List.map (fun line -> line.amount) |> List.sort,
                    returned.lines |> List.map (fun line -> line.amount) |> List.sort)
                Assert.Equal<string list>(
                    [ "Credit"; "Debit" ],
                    returned.lines |> List.map (fun line -> line.lineType) |> List.sort)

                (* The transition history is what REQ-STG-10.6 exists for: without it the caller
                   has to make a second round trip to learn how the entry reached its status. *)
                Assert.NotEmpty returned.statusTransitions
                Assert.Equal<Guid list>(
                    staged.statusTransitions
                    |> List.map (fun transition -> transition.stageEntryStatusTransitionId)
                    |> List.sort,
                    returned.statusTransitions
                    |> List.map (fun transition -> transition.stageEntryStatusTransitionId)
                    |> List.sort)
                let returnedStatus = returned.stageEntryHeader.status
                Assert.True(
                    returnedStatus.IsSome,
                    "the returned entry carries no current status, so REQ-STG-10.6's trail cannot be checked against it")
                Assert.Contains(
                    returnedStatus.Value,
                    returned.statusTransitions |> List.map (fun transition -> transition.toStatus))
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

    [<Fact>]
    member _.``REQ-STG-10.2 FetchStageEntryFiltered route resolves an account code to the account whose lines it returns`` () =
        (* The account filter arrives at the route as a code string and reaches the query as an
           id, so this is the only layer where that conversion is exercised at all. Filtering by
           each group's own expense account in turn is what separates a working conversion from
           one that resolves every code to the same account, or ignores the filter outright. *)
        let fileName = "fetch-filtered-account-code.jsonl"
        let mutable idsToCleanUp = []
        try
            result {
                let! ingested = twoAccountedGroups |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let idFor reference =
                    ingested.stagedEntries
                    |> List.find (fun entry -> entry.stageEntryHeader.fiReference = reference)
                    |> fun entry -> entry.stageEntryHeader.stageEntryHeaderId
                let firstGroupId = idFor "REF-ROUTE-FETCH-001"
                let secondGroupId = idFor "REF-ROUTE-FETCH-002"

                let! byFirstAccount = fetchFilteredThroughRoute { noFilterInput with accountCode = Some "F-5300" }
                Assert.Equal(1, byFirstAccount |> List.length)
                Assert.Equal(firstGroupId, (byFirstAccount |> List.head).stageEntryHeader.stageEntryHeaderId)

                let! bySecondAccount = fetchFilteredThroughRoute { noFilterInput with accountCode = Some "F-5650" }
                Assert.Equal(1, bySecondAccount |> List.length)
                Assert.Equal(secondGroupId, (bySecondAccount |> List.head).stageEntryHeader.stageEntryHeaderId)

                (* Both groups credit F-1270, so the shared account has to bring back both — a
                   conversion that quietly resolved to a single row would fail here. *)
                let! bySharedAccount = fetchFilteredThroughRoute { noFilterInput with accountCode = Some "F-1270" }
                Assert.Equal<Guid list>(
                    [ firstGroupId; secondGroupId ] |> List.sort,
                    bySharedAccount
                    |> List.map (fun entry -> entry.stageEntryHeader.stageEntryHeaderId)
                    |> List.sort)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

    [<Fact>]
    member _.``REQ-STG-10.7 FetchStageEntryFiltered route rejects an account code matching no ledger account rather than returning an empty list`` () =
        (* The conversion fails at the boundary, before the query is built, so there is nothing
           to stage and nothing to clean up. The point of the requirement is the distinction
           this asserts: a code naming no account is a caller error, and reporting it as an
           empty result would be indistinguishable from a filter that simply matched nothing. *)
        result {
            do!
                isCorrectError
                    (fetchFilteredThroughRoute { noFilterInput with accountCode = Some "BOGUS-9999" })
                    AccountCodeDoesntMatchAccountId
                    (Some "An unresolvable account code was reported as matching nothing instead of as an error.")
            return ()
        }
        |> railroadWrapper

    // =========================================================================
    // REQ-STG-3.12 — the file moves to the processed directory once its entries are staged
    // =========================================================================

    (* A file name with a directory in it lands in a subdirectory of the import directory, and its processed name
       then points into a subdirectory of the processed directory that doesn't exist, so the move is refused after
       the entries have committed. *)
    [<Fact>]
    member _.``REQ-STG-3.12 when the entries commit but the file cannot be moved, the ingestion fails with the staged-but-not-moved error naming the file and telling the operator to move it by hand, the entries stay staged, and the file stays in the import directory`` () =
        let subdirectory = "move-refused"
        let fileName = $"{subdirectory}/ingestion-route-move-fails.jsonl"
        let importPath = Path.Combine(importDir, fileName)
        let mutable idsToCleanUp = []
        try
            Directory.CreateDirectory(Path.Combine(importDir, subdirectory)) |> ignore
            writeImportFile fileName
                [ rawRow "grp-move-a" today "Move fails group" "TestBank" "REF-MOVE-001" "9.00" "Debit" (Some "F-5300") None
                  rawRow "grp-move-a" today "Move fails group" "TestBank" "REF-MOVE-001" "9.00" "Credit" (Some "F-1270") None ]
            result {
                let ingested = routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName)
                let! staged = stagedFrom importPath
                idsToCleanUp <- staged |> headerIdsOf
                let () =
                  match ingested with
                  | Error (AsError (DataIngestionError.IngestionStagedButFileNotMoved(filePath, _, _) as e)) ->
                      Assert.Equal(importPath, filePath)
                      Assert.Contains("by hand", (e :> IAppError).ToMessage())
                  | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                  | Ok _ -> Assert.Fail "Expected the move to fail; the route succeeded"
                Assert.Equal(1, staged |> List.length)
                Assert.True(File.Exists importPath)
            }
            |> railroadWrapper
        finally
            File.Delete importPath
            Directory.Delete(Path.Combine(importDir, subdirectory))
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())


    [<Fact>]
    member _.``REQ-STG-3.12 a successful ingestion leaves its entries staged and the file only in the processed directory, named with its original name prefixed by the ingestion timestamp as yyyy-MM-dd.HHmmss.fff-`` () =
        let fileName = "ingestion-route-moved-after-commit.jsonl"
        let importPath = Path.Combine(importDir, fileName)
        let mutable idsToCleanUp = []
        try
            writeImportFile fileName
                [ rawRow "grp-moved-a" today "Moved after commit group" "TestBank" "REF-MOVED-001" "5.00" "Debit" (Some "F-5300") None
                  rawRow "grp-moved-a" today "Moved after commit group" "TestBank" "REF-MOVED-001" "5.00" "Credit" (Some "F-1270") None ]
            result {
                let stampOf (instant: NodaTime.Instant) = instant |> Clock.instantToString "yyyy-MM-dd.HHmmss.fff"
                let before = Clock.now() |> stampOf
                let! _ = routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [] (ingestPayload fileName)
                let after = Clock.now() |> stampOf
                let! staged = stagedFrom importPath
                idsToCleanUp <- staged |> headerIdsOf
                Assert.Equal(1, staged |> List.length)
                Assert.False(File.Exists importPath)
                let processed = Directory.GetFiles(processedDir, $"*-{fileName}") |> Array.map Path.GetFileName
                let processedName = Assert.Single(processed)
                let pattern = $"""^(\d{{4}}-\d{{2}}-\d{{2}}\.\d{{6}}\.\d{{3}})-{Text.RegularExpressions.Regex.Escape fileName}$"""
                let matched = Text.RegularExpressions.Regex.Match(processedName, pattern)
                Assert.True(matched.Success, $"processed name {processedName} does not carry a yyyy-MM-dd.HHmmss.fff- prefix")
                let stamp = matched.Groups[1].Value
                Assert.InRange(stamp, before, after)
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())


    (* The contract has no mechanism field, so the serializer drops one a caller adds. The payload is written by hand to
       carry it anyway, the way an old saved payload would. *)
    [<Theory>]
    [<InlineData("Classifier")>]
    [<InlineData("Deduplicator")>]
    member _.``REQ-STG-6.2.1 a manual update payload naming change mechanism Classifier or Deduplicator still records its status change as Operator`` (claimedMechanism: string) =
        let fileName = $"ingestion-route-mechanism-{claimedMechanism}.jsonl"
        let description = "Route ingest first group"
        let mutable idsToCleanUp = []
        try
            result {
                let! ingested =
                    twoValidGroups $"REF-ROUTE-MECH-{claimedMechanism}-1" $"REF-ROUTE-MECH-{claimedMechanism}-2"
                    |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let headerId =
                    ingested.stagedEntries
                    |> List.find (fun entry -> entry.stageEntryHeader.description = description)
                    |> _.stageEntryHeader.stageEntryHeaderId
                let! contractPayload =
                    { stageEntryHeaderId = headerId
                      sourceFileUpdate = NoChange
                      entryDate = NoChange
                      description = NoChange
                      ingestionSource = NoChange
                      fiReference = NoChange
                      status = SetTo { newStatus = "Ignored" }
                      lines = []
                      linesToAdd = []
                      lineIdsToRemove = [] }
                    |> toJson<UpdateStageEntryInput>
                let statusField = "\"newStatus\":\"Ignored\""
                Assert.Contains(statusField, contractPayload)
                let payload =
                    contractPayload.Replace(
                        statusField, $"{statusField},\"stageStatusChangeMechanism\":\"{claimedMechanism}\"")
                let! _ = routeUiCommandForTesting "Ingestion" "UpdateStageEntry" [] payload
                let! refetched = refetchStageEntry headerId
                let latest =
                    refetched
                    |> statusTransitions
                    |> List.maxBy StageEntryStatusTransition.instant
                Assert.Equal(Ignored, latest |> StageEntryStatusTransition.toStatus)
                Assert.Equal(Operator, latest |> StageEntryStatusTransition.stageStatusChangeMechanism)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())

    [<Fact>]
    member _.``REQ-STG-3.2 REQ-STG-3.2.1 a file mixing valid and invalid records is rejected with one error listing every failing record, and only those, by line number, group_id and violation`` () =
        let rows =
            [ validRow "grp-mix-a" "Debit"
              validRow "grp-mix-a" "Credit"
              rawRow "grp-mix-b" today "Bad type" "TestBank" "REF-MIX-B" "25.00" "Sideways" (Some "F-5350") None
              validRow "grp-mix-b" "Credit"
              rawRow "grp-mix-c" today "Bad amount" "TestBank" "REF-MIX-C" "32.475" "Debit" (Some "F-5350") None
              rawRow "grp-mix-c" today "Bad amount" "TestBank" "REF-MIX-C" "32.47" "Credit" (Some "F-5350") None ]
        assertRejectsExactly
            "ingestion-route-mixed.jsonl"
            rows
            [ [ 3 ], Some "grp-mix-b", "JournalEntryLineTypeInvalid"
              [ 5 ], Some "grp-mix-c", "MoneyFailedToConvertImproperPrecision" ]

    [<Fact>]
    member _.``REQ-STG-3.2.1 a line that is not valid JSON is reported by its line number alongside every other failing record, not instead of them`` () =
        let rows =
            [ "{ this line is not json"
              validRow "grp-json-a" "Debit"
              validRow "grp-json-a" "Credit"
              rawRow "grp-json-b" today "Bad type" "TestBank" "REF-JSON-B" "25.00" "Sideways" (Some "F-5350") None
              validRow "grp-json-b" "Credit" ]
        assertRejectsExactly
            "ingestion-route-bad-json.jsonl"
            rows
            [ [ 1 ], None, "JsonDeserializationFailed"
              [ 4 ], Some "grp-json-b", "JournalEntryLineTypeInvalid" ]

    [<Fact>]
    member _.``REQ-STG-3.2.1 line numbers in a rejection count blank lines, so each matches the line an editor shows`` () =
        let rows =
            [ ""
              validRow "grp-blank-a" "Debit"
              "   "
              validRow "grp-blank-a" "Credit"
              ""
              rawRow "grp-blank-b" today "Bad type" "TestBank" "REF-BLANK-B" "25.00" "Sideways" (Some "F-5350") None
              validRow "grp-blank-b" "Credit" ]
        assertRejectsExactly
            "ingestion-route-blank-lines.jsonl"
            rows
            [ [ 6 ], Some "grp-blank-b", "JournalEntryLineTypeInvalid" ]

    [<Fact>]
    member _.``REQ-STG-3.2.1 a group that fails a group-level check is reported with its group_id and the check it failed, alongside every failing record in the same file`` () =
        let rows =
            [ rawRow "grp-level-a" today "Unbalanced" "TestBank" "REF-LEVEL-A" "100.00" "Debit" (Some "F-5350") None
              rawRow "grp-level-a" today "Unbalanced" "TestBank" "REF-LEVEL-A" "99.99" "Credit" (Some "F-5350") None
              rawRow "grp-level-b" today "Bad type" "TestBank" "REF-LEVEL-B" "25.00" "Sideways" (Some "F-5350") None
              validRow "grp-level-b" "Credit" ]
        assertRejectsExactly
            "ingestion-route-group-level.jsonl"
            rows
            [ [ 1; 2 ], Some "grp-level-a", "IngestionStageEntryDebitCreditMismatch"
              [ 3 ], Some "grp-level-b", "JournalEntryLineTypeInvalid" ]

    (* Like the shadow post route test above, this asks from outside: the route has returned, so whatever it posted
       must already be gone. *)
    [<Fact>]
    member _.``REQ-RPT-4.4 REQ-RPT-4.6 the shadow reconciliation route returns reconciliation rows and leaves ledger and staging untouched`` () =
        let fileName = "ingestion-route-shadow-reconcile.jsonl"
        let reference = "REF-ROUTE-SHADOW-RECON-001"
        let rows =
            [ rawRow "grp-route-shadow-recon" today "Route shadow reconcile" "TestBank" reference "25.00" "Debit" (Some "F-5650") None
              rawRow "grp-route-shadow-recon" today "Route shadow reconcile" "TestBank" reference "25.00" "Credit" (Some "F-1270") None ]
        let reconciliationPayload =
            { ReconciliationInput.rows = [ { accountCode = "F-5650"; externalBalance = 0.00M; asOf = Calendar.today() } ] }
            |> toJson<ReconciliationInput>
            |> Result.defaultWith (fun (e: IAppError) -> failwith(e.ToMessage()))
        let ledgerBalanceIn payload =
            payload
            |> fromJson<ReconciliationReturnRow list>
            |> Result.map (fun returned -> (returned |> List.exactlyOne).ledgerBalance)
        let plainLedgerBalance () =
            routeReportingCommandForTesting "Reconciliation" [] reconciliationPayload |> Result.bind ledgerBalanceIn
        let mutable idsToCleanUp = []
        try
            result {
                let! ingested = rows |> ingestThroughRoute fileName
                idsToCleanUp <- ingested |> headerIdsToCleanUp
                let context = Context.create NoTransaction FetchOnly
                (* F-5650 is a debit-normal leaf, so posting moves it by exactly its postable debits minus credits,
                   whatever else is staged. *)
                let! postable = fetchAllForPosting context
                let expectedMovement =
                    postable
                    |> List.collect seLines
                    |> List.filter (fun line -> line |> StageEntryLine.accountId = Some fixture.Data.entertainment5650Id)
                    |> List.sumBy (fun line ->
                        let amount = line |> StageEntryLine.amount |> Money.amount
                        if line |> StageEntryLine.lineType = Debit then amount else -amount)
                Assert.True(expectedMovement >= 25.00M, "The entry this test staged is not postable.")
                let! before = plainLedgerBalance ()
                let! shadow =
                    routeUiCommandForTesting "Ingestion" "ShadowReconcile" [] reconciliationPayload
                    |> Result.bind ledgerBalanceIn
                let! after = plainLedgerBalance ()
                Assert.Equal(before + expectedMovement, shadow)
                Assert.Equal(before, after)
                let! financialInstitution = "TestBank" |> JournalRefFinancialInstitution.create
                let! referenceText = reference |> JournalExternalReferenceText.create
                let! posted = fetchByReference context (Some financialInstitution) (Some referenceText)
                Assert.Empty(posted)
                let! refetched = refetchStageEntry (ingested.stagedEntries |> List.head).stageEntryHeader.stageEntryHeaderId
                Assert.Equal(Classified, refetched |> latestStatusOf)
                return ()
            }
            |> railroadWrapper
        finally
            deleteImportFile fileName
            match cleanUpStageEntryHeaderIdList idsToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (e.ToMessage())
