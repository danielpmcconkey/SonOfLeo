module Tests.Integrated.CrossDomainOrchestration.StagingIngestionRules

open System
open System.IO
open System.Text.Json
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.IngestionContracts
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module JE = Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration

(* Two kinds of test live here. The file, source and post-result tests go through the Ingestion routes, which commit:
   their setup commits too, and a finally deletes what they made (staged entries first, then journal entries, then
   rules, sources and accounts). Everything else runs inside a rolled-back transaction. Staged entries are from
   TestCreditCardCo unless a test says otherwise, and carry a fresh tag in their description and reference. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private headerIdOf (entry: StageEntry) = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

let private statusOf (entry: StageEntry) = entry |> stageEntryHeader |> StageEntryHeader.currentStatus

let private lineOfType (lineType: JournalEntryLineType) (entry: StageEntry) =
    entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = lineType)

let private refetch (context: Context.Context) (entry: StageEntry) = entry |> headerIdOf |> fetchByStageEntryHeaderId context

let private route (verb: string) (payload: string) = routeUiCommandForTesting "Ingestion" verb [] payload

// =========================================================================
// Files
// =========================================================================

let private root = Path.Combine(Path.GetTempPath(), "sonofleo-staging-rules")
let private importDir = Path.Combine(root, "import")
let private processedDir = Path.Combine(root, "processed")

let private jsonString (s: string) = JsonSerializer.Serialize s

let private jsonOption (s: string option) = s |> Option.map jsonString |> Option.defaultValue "null"

/// One record: each property written with the key and the (already JSON) value given, in order.
let private record (properties: (string * string) list) =
    properties |> List.map (fun (key, value) -> $"{jsonString key}:{value}") |> String.concat "," |> sprintf "{%s}"

/// The properties of one record under their documented names.
let private properties
    (group: string) (date: LocalDate) (amount: decimal) (lineType: string) (accountCode: string option)
    (description: string) (source: string) (reference: string) (memo: string option) =
    [ "baseStageEntryGroupId", jsonString group
      "entryDate", jsonString (date.ToString("yyyy-MM-dd", null))
      "amount", amount.ToString(Globalization.CultureInfo.InvariantCulture)
      "entryType", jsonString lineType
      "accountCode", jsonOption accountCode
      "description", jsonString description
      "fiSource", jsonString source
      "fiReference", jsonString reference
      "memo", jsonOption memo ]

/// A balanced two-record group from TestCreditCardCo: a Debit to F-2230 and a Credit with no account.
let private validGroup (group: string) (tag: string) (amount: decimal) =
    [ properties group (Calendar.today ()) amount "Debit" (Some "F-2230") $"Group {group} {tag}" "TestCreditCardCo"
          $"{group}-{tag}" (Some "debit leg")
      properties group (Calendar.today ()) amount "Credit" None $"Group {group} {tag}" "TestCreditCardCo"
          $"{group}-{tag}" None ]
    |> List.map record

let private ingestInput (input: IngestRawFileToStageInput) =
    input |> Json.toJson |> Result.bind (route "IngestRawFileToStage")

let private ingest (fileName: string) =
    { fileName = fileName; importDir = importDir; processedDir = processedDir } |> ingestInput

/// Every staged entry whose source file is the path, read outside any transaction.
let private stagedFrom (path: string) =
    path |> SourceFile.create |> Result.bind (fetchAllByFile (fresh ()) None)

/// Writes the rows to a fresh file in the import directory and runs the test with its name and path. Afterwards it
/// deletes every staged entry that came from the file, and the file from both directories.
let private withFile (rows: string list) (test: string -> string -> unit) =
    let fileName = $"stg-{Guid.NewGuid():N}.jsonl"
    let path = Path.Combine(importDir, fileName)
    Directory.CreateDirectory importDir |> ignore
    Directory.CreateDirectory processedDir |> ignore
    File.WriteAllLines(path, rows)
    try
        test fileName path
    finally
        let cleaned =
            stagedFrom path
            |> Result.bind (fun entries ->
                entries
                |> List.map (fun e -> e |> headerIdOf |> Some |> Cleanup.cleanUpStageEntryHeaderId)
                |> convertListOfResultsToResultsList)
        File.Delete path
        Directory.GetFiles(processedDir, $"*-{fileName}") |> Array.iter File.Delete
        match cleaned with
        | Ok _ -> ()
        | Error e -> failwith (e.ToMessage())

// =========================================================================
// Sources
// =========================================================================

let private createSourceThroughRoute (payload: string) =
    payload |> route "CreateIngestionSource" |> Result.bind Json.fromJson<IngestionSourceReturn>

let private namePayload (name: string) = ({ name = name } : CreateNewIngestionSourceInput) |> Json.toJson |> orFail

let private allSourceIds () =
    executeReaderQuery (fresh () |> Context.getDatabaseTransaction) "select unique_id from ingestion.source" [] (RowReader.getUuid "unique_id") Ok
        AnyQuantityIsAcceptable
    |> Result.map Set.ofList

let private cleanUpSource (id: Guid) =
    id |> IngestionSourceId.fromGuid |> Some |> Cleanup.cleanUpIngestionSourceId |> orFail

/// A name of the length, starting with a fresh tag.
let private nameOfLength (length: int) =
    let tag = newTag ()
    tag + String('n', length - tag.Length)

// =========================================================================
// Staged entries, rules and payments inside one transaction
// =========================================================================

let private noHeaderUpdates (headerId: StageEntryHeaderId) : StageEntryHeader.StageEntryHeaderFieldUpdates =
    { headerIdToUpdate = headerId
      sourceFileUpdate = NoChange
      entryDateUpdate = NoChange
      descriptionUpdate = NoChange
      ingestionSourceUpdate = NoChange
      fiReferenceUpdate = NoChange
      journalEntryHeaderIdUpdate = NoChange
      statusUpdate = NoChange }

let private noLineUpdates (lineId: StageEntryLineId) : StageEntryLine.StageEntryLineFieldUpdates =
    { lineIdToUpdate = lineId
      amountUpdate = NoChange
      entryTypeUpdate = NoChange
      accountIdUpdate = NoChange
      memoUpdate = NoChange
      journalEntryLineIdUpdate = NoChange }

type private Scenario(fixture: TestDataFixture, initialContext: Context.Context) =
    // one staged entry cannot hold two status transitions at one instant (REQ-STG-4.1.2), so every operation that
    // changes a status runs after advance ()
    let mutable context = initialContext
    let cardSource =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestCreditCardCo")

    member _.Context = context
    member _.advance () = context <- context |> TestContext.updateInitiationInstant
    member _.card = cardSource
    member _.accountIdOf (code: string) =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    /// A staged entry from the source, dated the date, with the lines (amount, line type, account code), that has
    /// been moved from Ingested through each status in the path, one transition 10 microseconds after the last.
    member _.staged (source: IngestionSource.IngestionSource) (reference: string) (date: LocalDate)
                    (lines: (decimal * string * string option) list) (path: string list) =
        let start = Clock.now ()
        let transitions =
            ("Ingested" :: path)
            |> List.pairwise
            |> List.mapi (fun i (from, into) ->
                (Some from, into, start.Plus(Duration.FromTicks(100L * int64 (i + 1))), "Operator"))
        createStageEntryForTest context "/tmp/staging-rules-test.dat" $"Staging rules {reference}" reference source date
            (lines |> List.map (fun (amount, lineType, code) -> (amount, lineType, code, None, None)))
            ((None, "Ingested", start, "StageIngestion") :: transitions)

    /// A 100.00 TestCreditCardCo entry dated today: a Debit to F-2230 and a Credit to F-1280, or with no accounts.
    member this.cardEntry (reference: string) (withAccounts: bool) (path: string list) =
        this.staged cardSource reference (Calendar.today ())
            [ (100.00M, "Debit", (if withAccounts then Some "F-2230" else None))
              (100.00M, "Credit", (if withAccounts then Some "F-1280" else None)) ]
            path

    /// Fixture agreement A's next Instance, with one 100.00 Invoice on its leg paid by a Payment on the line.
    member _.payOn (lineId: StageEntryLineId) =
        result {
            let cashFlow = fixture.Data.cashFlow
            let date = cashFlow.nextInstanceDateA
            let! amount = Money.fromDecimal 100.00M
            let payments =
                [ (CashFlowComponent.Staged lineId, ({ money = amount } : CashFlowComponent.PaymentAmount), None, None, None) ]
            let! _ =
                InstanceOrchestration.createInstanceCompositeAndSaveToDb context cashFlow.agreementAId date
                    [ (cashFlow.legAId, None, ({ localDate = date } : CashFlowComponent.InvoiceDate),
                       ({ localDate = date.PlusDays(30) } : CashFlowComponent.DueDate),
                       ({ money = amount } : CashFlowComponent.InvoiceAmount), CashFlowComponent.InvoiceReceived, None, None,
                       payments) ]
            return ()
        }

    member _.update (headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates) (lineUpdates: StageEntryLine.StageEntryLineFieldUpdates list) =
        updateStageEntry context headerUpdates lineUpdates [] []

let private rolledBack (fixture: TestDataFixture) action (body: Scenario -> Result<unit, IAppError>) =
    runCommandRouteAndAutoRollback action (fun context -> body (Scenario(fixture, context))) |> railroadWrapper

let private committed (fixture: TestDataFixture) action (body: Scenario -> Result<'a, IAppError>) =
    runCommandRouteAndAutoCompleteTransaction action (fun context -> body (Scenario(fixture, context)))

let private postThroughRoute (isShadow: bool) =
    ({ isShadow = isShadow } : PostStageEntriesInput)
    |> Json.toJson
    |> Result.bind (route "PostStageEntries")
    |> Result.bind Json.fromJson<PostStageEntriesFullResult>

let private debitsOn (code: string) (rows: TrialBalanceReturnRow list) =
    rows |> List.filter (fun row -> row.accountCode = code) |> List.sumBy _.totalDebits

/// The one journal entry dated the date whose description is the staged entry's.
let private postedFrom (context: Context.Context) (entry: StageEntry) =
    result {
        let header = entry |> stageEntryHeader
        let date = header |> StageEntryHeader.entryDate
        let! onDate = JE.fetchByDateRange context date date
        return
            onDate
            |> List.filter (fun je -> je |> JE.header |> JournalEntryHeader.description = (header |> StageEntryHeader.description))
            |> List.exactlyOne
    }

[<Collection("SharedTestData")>]
type StagingIngestionRulesTests(fixture: TestDataFixture) =

    let accountIdOf (code: string) =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    let card =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestCreditCardCo")

    // =========================================================================
    // REQ-STG-1.17 — property names and trimming
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-1.17 a file whose records use the property names baseStageEntryGroupId, entryDate, amount, entryType, accountCode, description, fiSource, fiReference and memo is ingested with records sharing a baseStageEntryGroupId in one staged entry and every other value stored in its field`` () =
        let tag = newTag ()
        let date = Calendar.today ()
        let rows =
            [ properties "G1" date 12.34M "Debit" (Some "F-2230") $"First {tag}" "TestCreditCardCo" $"R1-{tag}" (Some "first debit")
              properties "G2" date 56.78M "Debit" (Some "F-5300") $"Second {tag}" "TestCreditCardCo" $"R2-{tag}" None
              properties "G1" date 12.34M "Credit" None $"First {tag}" "TestCreditCardCo" $"R1-{tag}" (Some "first credit")
              properties "G2" date 56.78M "Credit" (Some "F-1280") $"Second {tag}" "TestCreditCardCo" $"R2-{tag}" (Some "second credit") ]
            |> List.map record
        withFile rows (fun fileName path ->
            result {
                let! _ = ingest fileName
                let! staged = stagedFrom path
                Assert.Equal(2, staged.Length)
                let byReference (reference: string) =
                    staged |> List.find (fun e -> e |> stageEntryHeader |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value = reference)
                let fieldsOf (entry: StageEntry) =
                    let header = entry |> stageEntryHeader
                    header |> StageEntryHeader.entryDate,
                    header |> StageEntryHeader.description |> JournalEntryDescription.value,
                    header |> StageEntryHeader.ingestionSource |> IngestionSource.ingestionSourceId
                let linesOf (entry: StageEntry) =
                    entry
                    |> seLines
                    |> List.map (fun l ->
                        l |> StageEntryLine.lineType,
                        l |> StageEntryLine.amount |> Money.amount,
                        l |> StageEntryLine.accountId,
                        l |> StageEntryLine.memo |> Option.map JournalEntryLineMemo.value)
                    |> Set.ofList
                let first = byReference $"R1-{tag}"
                let second = byReference $"R2-{tag}"
                let cardId = card |> IngestionSource.ingestionSourceId
                Assert.Equal((date, $"First {tag}", cardId), fieldsOf first)
                Assert.Equal((date, $"Second {tag}", cardId), fieldsOf second)
                Assert.Equal<Set<_>>(
                    set [ (Debit, 12.34M, Some(accountIdOf "F-2230"), Some "first debit"); (Credit, 12.34M, None, Some "first credit") ],
                    linesOf first)
                Assert.Equal<Set<_>>(
                    set [ (Debit, 56.78M, Some(accountIdOf "F-5300"), None); (Credit, 56.78M, Some(accountIdOf "F-1280"), Some "second credit") ],
                    linesOf second)
            }
            |> railroadWrapper)

    [<Theory>]
    [<InlineData("group_id")>]
    [<InlineData("entry_date")>]
    [<InlineData("line_type")>]
    [<InlineData("fi_source")>]
    [<InlineData("fi_reference")>]
    member _.``REQ-STG-1.17 for each of group_id, entry_date, line_type, fi_source and fi_reference, a file whose records spell that required property by its column name instead is rejected and nothing is ingested`` (column: string) =
        let tag = newTag ()
        let documented =
            match column with
            | "group_id" -> "baseStageEntryGroupId"
            | "entry_date" -> "entryDate"
            | "line_type" -> "entryType"
            | "fi_source" -> "fiSource"
            | _ -> "fiReference"
        let misspelt =
            [ properties "G1" (Calendar.today ()) 10.00M "Debit" (Some "F-2230") $"Misspelt {tag}" "TestCreditCardCo" $"M-{tag}" None
              properties "G1" (Calendar.today ()) 10.00M "Credit" (Some "F-1280") $"Misspelt {tag}" "TestCreditCardCo" $"M-{tag}" None ]
            |> List.map (List.map (fun (key, value) -> (if key = documented then column else key), value) >> record)
        // a valid group alongside, so a partial ingestion would leave something behind
        withFile (misspelt @ validGroup "G2" tag 20.00M) (fun fileName path ->
            let attempt = ingest fileName
            Assert.True(attempt |> Result.isError)
            Assert.Empty(stagedFrom path |> orFail))

    [<Theory>]
    [<InlineData("baseStageEntryGroupId")>]
    [<InlineData("entryType")>]
    [<InlineData("accountCode")>]
    [<InlineData("description")>]
    [<InlineData("fiSource")>]
    [<InlineData("fiReference")>]
    [<InlineData("memo")>]
    member _.``REQ-STG-1.17 for each text property (baseStageEntryGroupId, entryType, accountCode, description, fiSource, fiReference, memo), a value padded with spaces is treated as its trimmed value: stored trimmed, grouped, parsed or resolved as if trimmed, and accepted when only the padding takes it past its maximum length`` (property: string) =
        let tag = newTag ()
        let before (s: string) = "   " + s
        let after (s: string) = s + "   "
        let group = tag + String('g', 36 - tag.Length)
        let description = tag + String('d', 1000 - tag.Length)
        let reference = tag + String('r', 100 - tag.Length)
        let memo = tag + String('m', 1000 - tag.Length)
        let recordWith (pad: string -> string) (lineType: string) (accountCode: string option) =
            let p (name: string) (value: string) = if name = property then pad value else value
            properties (p "baseStageEntryGroupId" group) (Calendar.today ()) 25.00M (p "entryType" lineType)
                (accountCode |> Option.map (p "accountCode")) (p "description" description) (p "fiSource" "TestCreditCardCo")
                (p "fiReference" reference) (Some(p "memo" memo))
            |> record
        // the two records are padded on different sides, so grouping must compare trimmed values
        let rows = [ recordWith before "Debit" (Some "F-2230"); recordWith after "Credit" (Some "F-1280") ]
        withFile rows (fun fileName path ->
            result {
                let! _ = ingest fileName
                let! staged = stagedFrom path
                let entry = Assert.Single(staged)
                let header = entry |> stageEntryHeader
                Assert.Equal(2, entry |> seLines |> List.length)
                Assert.Equal<string>(description, header |> StageEntryHeader.description |> JournalEntryDescription.value)
                Assert.Equal<string>(reference, header |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value)
                Assert.Equal(card |> IngestionSource.ingestionSourceId, header |> StageEntryHeader.ingestionSource |> IngestionSource.ingestionSourceId)
                Assert.Equal(Some(accountIdOf "F-2230"), entry |> lineOfType Debit |> StageEntryLine.accountId)
                Assert.Equal(Some(accountIdOf "F-1280"), entry |> lineOfType Credit |> StageEntryLine.accountId)
                Assert.All(entry |> seLines, fun line ->
                    Assert.Equal(Some memo, line |> StageEntryLine.memo |> Option.map JournalEntryLineMemo.value))
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-STG-2.25, 2.26, 2.27, 3.15 — ingestion sources
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-2.25 each ingestion source created by name receives a system-generated UUID, distinct from every other source's`` () =
        let made = ResizeArray<Guid>()
        try
            result {
                let! before = allSourceIds ()
                let! first = namePayload (nameOfLength 20) |> createSourceThroughRoute
                made.Add first.ingestionSourceId
                let! second = namePayload (nameOfLength 20) |> createSourceThroughRoute
                made.Add second.ingestionSourceId
                Assert.NotEqual(Guid.Empty, first.ingestionSourceId)
                Assert.NotEqual(Guid.Empty, second.ingestionSourceId)
                Assert.NotEqual(first.ingestionSourceId, second.ingestionSourceId)
                Assert.DoesNotContain(first.ingestionSourceId, before)
                Assert.DoesNotContain(second.ingestionSourceId, before)
            }
            |> railroadWrapper
        finally
            made |> Seq.iter cleanUpSource

    [<Theory>]
    [<InlineData("null")>]
    [<InlineData("empty")>]
    [<InlineData("whitespace only")>]
    member _.``REQ-STG-2.26 for each of null, empty and whitespace only, creating an ingestion source with that name is rejected with a typed error and no source is stored`` (kind: string) =
        let payload =
            match kind with
            | "null" -> """{"name":null}"""
            | "empty" -> namePayload ""
            | _ -> namePayload "    "
        let before = allSourceIds () |> orFail
        let attempt = createSourceThroughRoute payload
        let after = allSourceIds () |> orFail
        match attempt with
        | Ok made -> cleanUpSource made.ingestionSourceId
        | Error _ -> ()
        Assert.True(attempt |> Result.isError)
        Assert.Equal<Set<Guid>>(before, after)

    [<Fact>]
    member _.``REQ-STG-2.26 an ingestion source name of 100 characters is stored, and one of 101 is rejected with a typed error and not stored`` () =
        let made = ResizeArray<Guid>()
        try
            result {
                let atLimit = nameOfLength 100
                let! stored = namePayload atLimit |> createSourceThroughRoute
                made.Add stored.ingestionSourceId
                Assert.Equal<string>(atLimit, stored.name)
                let! name = atLimit |> JournalRefFinancialInstitution.create
                let! readBack = name |> IngestionSource.fetchByName (fresh ())
                Assert.Equal(stored.ingestionSourceId, readBack |> IngestionSource.ingestionSourceId |> IngestionSourceId.value)
                let! before = allSourceIds ()
                let attempt = namePayload (nameOfLength 101) |> createSourceThroughRoute
                attempt |> Result.iter (fun tooLong -> made.Add tooLong.ingestionSourceId)
                let! after = allSourceIds ()
                Assert.True(attempt |> Result.isError)
                Assert.Equal<Set<Guid>>(before, after)
            }
            |> railroadWrapper
        finally
            made |> Seq.iter cleanUpSource

    [<Fact>]
    member _.``REQ-STG-2.26 an ingestion source name padded with spaces is stored trimmed, and one of 100 characters plus padding is accepted`` () =
        let made = ResizeArray<Guid>()
        try
            result {
                let short = nameOfLength 30
                let! padded = namePayload ("  " + short + "  ") |> createSourceThroughRoute
                made.Add padded.ingestionSourceId
                Assert.Equal<string>(short, padded.name)
                let! shortName = short |> JournalRefFinancialInstitution.create
                let! readBack = shortName |> IngestionSource.fetchByName (fresh ())
                Assert.Equal(padded.ingestionSourceId, readBack |> IngestionSource.ingestionSourceId |> IngestionSourceId.value)
                let atLimit = nameOfLength 100
                let! paddedAtLimit = namePayload (" " + atLimit + "    ") |> createSourceThroughRoute
                made.Add paddedAtLimit.ingestionSourceId
                Assert.Equal<string>(atLimit, paddedAtLimit.name)
            }
            |> railroadWrapper
        finally
            made |> Seq.iter cleanUpSource

    [<Fact>]
    member _.``REQ-STG-2.26 a record whose fiSource equals an ingestion source's name is staged with that source's ID`` () =
        let tag = newTag ()
        let sourceName = nameOfLength 40
        let source = namePayload sourceName |> createSourceThroughRoute |> orFail
        try
            let rows =
                [ properties "G1" (Calendar.today ()) 15.00M "Debit" (Some "F-2230") $"New source {tag}" sourceName $"N-{tag}" None
                  properties "G1" (Calendar.today ()) 15.00M "Credit" (Some "F-1280") $"New source {tag}" sourceName $"N-{tag}" None ]
                |> List.map record
            withFile rows (fun fileName path ->
                result {
                    let! _ = ingest fileName
                    let! staged = stagedFrom path
                    let entry = Assert.Single(staged)
                    Assert.Equal(
                        source.ingestionSourceId,
                        entry |> stageEntryHeader |> StageEntryHeader.ingestionSource |> IngestionSource.ingestionSourceId |> IngestionSourceId.value)
                }
                |> railroadWrapper)
        finally
            cleanUpSource source.ingestionSourceId

    [<Fact>]
    member _.``REQ-STG-2.26 batch post writes the ingestion source's name as the financial institution on the external reference of each journal entry posted from that source`` () =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let tag = newTag ()
                let sourceName = nameOfLength 40
                let! name = sourceName |> JournalRefFinancialInstitution.create
                let! source = name |> createNewSource s.Context
                let! first =
                    s.staged source $"P1-{tag}" (Calendar.today ()) [ (40.00M, "Debit", Some "F-2230"); (40.00M, "Credit", Some "F-1280") ] [ "Classified" ]
                let! second =
                    s.staged source $"P2-{tag}" (Calendar.today ()) [ (45.00M, "Debit", Some "F-5300"); (45.00M, "Credit", Some "F-1280") ] [ "Classified" ]
                s.advance ()
                do! post s.Context
                [ first; second ] |> List.ofSeq |> List.iter (fun entry ->
                    let journalEntry = postedFrom s.Context entry |> orFail
                    let reference = journalEntry |> JE.externalReferences |> List.exactlyOne
                    Assert.Equal<string>(
                        sourceName,
                        reference |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value))
            })

    [<Fact>]
    member _.``REQ-STG-2.27 REQ-STG-3.15 an ingestion source created by name reads back with that name, a system-generated ID, and created-at and modified-at both equal to the creating operation's instant`` () =
        rolledBack fixture IngestNewSource (fun s ->
            result {
                let sourceName = nameOfLength 40
                let! name = sourceName |> JournalRefFinancialInstitution.create
                let! created = name |> createNewSource s.Context
                let! readBack = name |> IngestionSource.fetchByName s.Context
                let instant = s.Context |> Context.getInitiationInstant
                Assert.Equal<string>(sourceName, readBack |> IngestionSource.name |> JournalRefFinancialInstitution.value)
                Assert.NotEqual(Guid.Empty, readBack |> IngestionSource.ingestionSourceId |> IngestionSourceId.value)
                Assert.Equal(created |> IngestionSource.ingestionSourceId, readBack |> IngestionSource.ingestionSourceId)
                Assert.Equal(instant, readBack |> IngestionSource.createdAt)
                Assert.Equal(instant, readBack |> IngestionSource.modifiedAt)
            })

    // =========================================================================
    // REQ-STG-3.11, 3.13, 3.14 — the ingestion request
    // =========================================================================

    [<Theory>]
    [<InlineData("missing import directory")>]
    [<InlineData("missing processed directory")>]
    [<InlineData("file absent")>]
    member _.``REQ-STG-3.11 for each of a missing import directory, a missing processed directory and a file absent from the import directory, the ingestion request fails with a typed error and nothing is ingested`` (case: string) =
        let tag = newTag ()
        let absent = Path.Combine(root, $"absent-{tag}")
        withFile (validGroup "G1" tag 30.00M) (fun fileName path ->
            let input : IngestRawFileToStageInput =
                match case with
                | "missing import directory" -> { fileName = fileName; importDir = absent; processedDir = processedDir }
                | "missing processed directory" -> { fileName = fileName; importDir = importDir; processedDir = absent }
                | _ -> { fileName = $"absent-{tag}.jsonl"; importDir = importDir; processedDir = processedDir }
            let attempt = ingestInput input
            Assert.True(attempt |> Result.isError)
            Assert.Empty(stagedFrom path |> orFail)
            Assert.Empty(stagedFrom (Path.Combine(input.importDir, input.fileName)) |> orFail))

    [<Fact>]
    member _.``REQ-STG-3.13 ingestion returns every staged entry it created and no other, each with its header, all of its lines and its status transitions`` () =
        let tag = newTag ()
        withFile (validGroup "G1" tag 31.00M @ validGroup "G2" tag 32.00M) (fun fileName path ->
            result {
                let! returned = ingest fileName |> Result.bind Json.fromJson<StageEntryReturn list>
                let! staged = stagedFrom path
                Assert.Equal(2, staged.Length)
                Assert.Equal<Set<Guid>>(
                    staged |> List.map (headerIdOf >> StageEntryHeaderId.value) |> Set.ofList,
                    returned |> List.map _.stageEntryHeader.stageEntryHeaderId |> Set.ofList)
                staged |> List.ofSeq |> List.iter (fun entry ->
                    let id = entry |> headerIdOf |> StageEntryHeaderId.value
                    let matching = returned |> List.find (fun r -> r.stageEntryHeader.stageEntryHeaderId = id)
                    let header = entry |> stageEntryHeader
                    Assert.Equal<string>(header |> StageEntryHeader.description |> JournalEntryDescription.value, matching.stageEntryHeader.description)
                    Assert.Equal<string>(header |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value, matching.stageEntryHeader.fiReference)
                    Assert.Equal<Set<Guid>>(
                        entry |> seLines |> List.map (StageEntryLine.stageEntryLineId >> StageEntryLineId.value) |> Set.ofList,
                        matching.lines |> List.map _.stageEntryLineId |> Set.ofList)
                    Assert.NotEmpty(matching.statusTransitions)
                    Assert.Equal<Set<Guid>>(
                        entry
                        |> statusTransitions
                        |> List.map (StageEntryStatusTransition.stageEntryStatusTransitionId >> StageEntryStatusTransitionId.value)
                        |> Set.ofList,
                        matching.statusTransitions |> List.map _.stageEntryStatusTransitionId |> Set.ofList))
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-STG-3.14 ingesting a file with a line an active account rule matches and a group whose source and reference match an existing staged entry leaves every new entry Ingested, the matched line with a null account, and nothing flagged duplicate`` () =
        let tag = newTag ()
        let reference = $"Seen-{tag}"
        let rule, existing =
            committed fixture IngestRawEntries (fun s ->
                result {
                    let! name = $"Staging rules {tag}" |> ClassificationRuleName.create
                    let! pattern = tag |> StringSearchPattern.create
                    let! ruleGroups = [ ("And", [ FieldMatch.Description pattern ], None) ] |> createClassificationRuleGroupListForTest
                    let! rule =
                        ClassificationOrchestration.createNewClassificationRule s.Context name
                            (ClassificationClaimant.Account(s.accountIdOf "F-5300")) 100 ruleGroups
                    let! existing = s.cardEntry reference true [ "Classified" ]
                    return rule, existing
                })
            |> orFail
        try
            let rows =
                [ properties "G1" (Calendar.today ()) 33.00M "Debit" None $"Matched by rule {tag}" "TestCreditCardCo" $"Fresh-{tag}" None
                  properties "G1" (Calendar.today ()) 33.00M "Credit" (Some "F-1280") $"Matched by rule {tag}" "TestCreditCardCo" $"Fresh-{tag}" None
                  properties "G2" (Calendar.today ()) 34.00M "Debit" (Some "F-2230") $"Seen before {tag}" "TestCreditCardCo" reference None
                  properties "G2" (Calendar.today ()) 34.00M "Credit" (Some "F-1280") $"Seen before {tag}" "TestCreditCardCo" reference None ]
                |> List.map record
            withFile rows (fun fileName path ->
                result {
                    let! _ = ingest fileName
                    let! staged = stagedFrom path
                    Assert.Equal(2, staged.Length)
                    Assert.All(staged, fun entry -> Assert.Equal(Some StagedEntryStatus.Ingested, entry |> statusOf))
                    let matched = staged |> List.find (fun e -> e |> seLines |> List.exists (fun l -> l |> StageEntryLine.amount |> Money.amount = 33.00M))
                    let seen = staged |> List.find (fun e -> e |> seLines |> List.exists (fun l -> l |> StageEntryLine.amount |> Money.amount = 34.00M))
                    let matchedLine = matched |> lineOfType Debit
                    Assert.Equal(None, matchedLine |> StageEntryLine.accountId)
                    // the rule and the reference do bite when their own operations run
                    runCommandRouteAndAutoRollback IngestDeduplicateStageEntries (fun context ->
                        result {
                            let later = context |> TestContext.updateInitiationInstant
                            let! _ = classifyAccounts later
                            let! line = matchedLine |> StageEntryLine.stageEntryLineId |> StageEntryLine.fetchById later
                            Assert.Equal(Some(accountIdOf "F-5300"), line |> StageEntryLine.accountId)
                            let evenLater = later |> TestContext.updateInitiationInstant
                            let! _ = deduplicateStagedEntries evenLater
                            let! seenAfter = refetch evenLater seen
                            Assert.Equal(Some StagedEntryStatus.Duplicate, seenAfter |> statusOf)
                        })
                    |> railroadWrapper
                }
                |> railroadWrapper)
        finally
            [ Cleanup.cleanUpStageEntryHeaderId (Some(existing |> headerIdOf))
              Cleanup.cleanUpClassificationRuleId (Some(rule |> ClassificationRule.classificationRuleId)) ]
            |> List.iter orFail

    // =========================================================================
    // REQ-STG-4.1.1 — current status is the latest audit record's
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-4.1.1 after transitions to Classified and then Reviewed, the entry's current status is Reviewed, the to-status of its latest audit record, and an audit record added later becomes its current status on the next read`` () =
        rolledBack fixture IngestUpdateStageEntry (fun s ->
            result {
                let! entry = s.cardEntry $"Status-{newTag ()}" true [ "Classified"; "Reviewed" ]
                let! readBack = refetch s.Context entry
                let latest = readBack |> statusTransitions |> List.maxBy StageEntryStatusTransition.instant
                Assert.Equal(Some StagedEntryStatus.Reviewed, readBack |> statusOf)
                Assert.Equal(StagedEntryStatus.Reviewed, latest |> StageEntryStatusTransition.toStatus)
                let! _ =
                    createStageEntryStatusTransitionForTest s.Context (entry |> headerIdOf) (Some "Reviewed") "Ignored"
                        ((latest |> StageEntryStatusTransition.instant).Plus(Duration.FromMilliseconds(5L))) "Operator"
                let! afterward = refetch s.Context entry
                Assert.Equal(Some StagedEntryStatus.Ignored, afterward |> statusOf)
            })

    [<Fact>]
    member _.``REQ-STG-4.1.1 a staged entry's current status is the to-status of its audit record with the latest instant, including when a record with an earlier instant is added after it`` () =
        rolledBack fixture IngestUpdateStageEntry (fun s ->
            result {
                let! entry = s.cardEntry $"Status-{newTag ()}" true [ "Classified"; "Reviewed" ]
                let transitions = entry |> statusTransitions |> List.sortBy StageEntryStatusTransition.instant
                let reviewedAt = transitions |> List.last |> StageEntryStatusTransition.instant
                let classifiedAt = transitions |> List.item (transitions.Length - 2) |> StageEntryStatusTransition.instant
                // between the Classified and Reviewed records, written after both
                let between = classifiedAt.Plus(Duration.FromTicks((reviewedAt - classifiedAt).BclCompatibleTicks / 2L))
                let! _ =
                    createStageEntryStatusTransitionForTest s.Context (entry |> headerIdOf) (Some "Classified") "Ignored" between "Operator"
                let! readBack = refetch s.Context entry
                Assert.Equal(Some StagedEntryStatus.Reviewed, readBack |> statusOf)
            })

    // =========================================================================
    // REQ-STG-5.11 — what classification returns
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-5.11 classification returns its run ID, one outcome per line it evaluated, and every staged entry that is Ingested, Classified, NoMatch, Conflict or Reviewed after the run, and none that is Duplicate, Ignored or Posted`` () =
        rolledBack fixture ClassifyAccounts (fun s ->
            result {
                let tag = newTag ()
                let entryIn (status: string) (withAccounts: bool) (path: string list) = s.cardEntry $"{status}-{tag}" withAccounts path
                let! ingestedCoded = entryIn "IngestedCoded" true []
                let! ingestedUncoded = entryIn "IngestedUncoded" false []
                let! classified = entryIn "Classified" true [ "Classified" ]
                let! noMatch = entryIn "NoMatch" false [ "NoMatch" ]
                let! conflict = entryIn "Conflict" false [ "Conflict" ]
                let! reviewed = entryIn "Reviewed" true [ "Classified"; "Reviewed" ]
                let! duplicate = entryIn "Duplicate" false [ "Duplicate" ]
                let! ignored = entryIn "Ignored" false [ "Ignored" ]
                let! posted = entryIn "Posted" true [ "Classified"; "Posted" ]
                s.advance ()
                let! run = classifyAccounts s.Context
                let! expected =
                    [ StagedEntryStatus.Ingested; StagedEntryStatus.Classified; StagedEntryStatus.NoMatch; StagedEntryStatus.Conflict; StagedEntryStatus.Reviewed ]
                    |> fetchByStatusList s.Context
                let returnedIds = run.stagedEntries |> List.map headerIdOf |> Set.ofList
                Assert.NotEqual(Guid.Empty, run.runId |> ClassificationRunId.value)
                Assert.Equal<Set<StageEntryHeaderId>>(expected |> List.map headerIdOf |> Set.ofList, returnedIds)
                [ ingestedCoded; ingestedUncoded; classified; noMatch; conflict; reviewed ] |> List.ofSeq |> List.iter (fun entry ->
                    Assert.Contains(entry |> headerIdOf, returnedIds))
                [ duplicate; ignored; posted ] |> List.ofSeq |> List.iter (fun entry ->
                    Assert.DoesNotContain(entry |> headerIdOf, returnedIds))
                let outcomesOn (lineId: StageEntryLineId) =
                    run.classificationResults |> List.filter (fun r -> r.candidate.lineIdOfCandidate = lineId) |> List.length
                // the uncoded lines of the entries the run took up are evaluated once each; no other line of these is
                [ ingestedUncoded; noMatch; conflict ] |> List.ofSeq |> List.iter (fun entry ->
                    for line in entry |> seLines do
                        Assert.Equal(1, outcomesOn (line |> StageEntryLine.stageEntryLineId)))
                [ ingestedCoded; classified; reviewed; duplicate; ignored; posted ] |> List.ofSeq |> List.iter (fun entry ->
                    for line in entry |> seLines do
                        Assert.Equal(0, outcomesOn (line |> StageEntryLine.stageEntryLineId)))
            })

    // =========================================================================
    // REQ-STG-6.3.1, 6.3.2 — manual updates that name the wrong line or change nothing
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.3.1 a manual update naming a line of a different staged entry is rejected with a typed error naming both the entry and the line, and neither entry changes`` () =
        rolledBack fixture IngestUpdateStageEntry (fun s ->
            result {
                let tag = newTag ()
                let! target = s.cardEntry $"Target-{tag}" true [ "Classified" ]
                let! other = s.cardEntry $"Other-{tag}" true [ "Classified" ]
                s.advance ()
                let! targetBefore = refetch s.Context target
                let! otherBefore = refetch s.Context other
                let otherLine = other |> lineOfType Debit |> StageEntryLine.stageEntryLineId
                let! memo = "moved" |> JournalEntryLineMemo.create
                let attempt = s.update (target |> headerIdOf |> noHeaderUpdates) [ { noLineUpdates otherLine with memoUpdate = SetTo(Some memo) } ]
                let namesBoth =
                    match attempt with
                    | Error (AsError (IngestionUpdateStageEntryLinesMustMatchHeader(entryUuid, lineUuid))) ->
                        entryUuid = (target |> headerIdOf |> StageEntryHeaderId.value) && lineUuid = (otherLine |> StageEntryLineId.value)
                    | _ -> false
                Assert.True(namesBoth)
                let! targetAfter = refetch s.Context target
                let! otherAfter = refetch s.Context other
                Assert.Equal(targetBefore, targetAfter)
                Assert.Equal(otherBefore, otherAfter)
            })

    [<Fact>]
    member _.``REQ-STG-6.3.2 a manual update naming no fields is rejected with a typed error and nothing is written`` () =
        rolledBack fixture IngestUpdateStageEntry (fun s ->
            result {
                let! entry = s.staged s.card $"NoOp-{newTag ()}" (Calendar.today ())
                                 [ (60.00M, "Debit", Some "F-2230"); (60.00M, "Credit", Some "F-1280") ] [ "Classified" ]
                s.advance ()
                let! before = refetch s.Context entry
                let attempt = s.update (entry |> headerIdOf |> noHeaderUpdates) []
                let refused =
                    match attempt with
                    | Error (AsError IngestionUpdateStageEntryNoOp) -> true
                    | _ -> false
                Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                let! after = refetch s.Context entry
                Assert.Equal(before, after)
            })

    [<Fact>]
    member _.``REQ-STG-6.3.2 a manual update that sets only the status, to the entry's current status, succeeds and writes nothing: no new status transition and no change to the entry or its lines`` () =
        rolledBack fixture IngestUpdateStageEntry (fun s ->
            result {
                let! entry = s.cardEntry $"SameStatus-{newTag ()}" true [ "Classified" ]
                s.advance ()
                let! before = refetch s.Context entry
                let! _ = s.update { (entry |> headerIdOf |> noHeaderUpdates) with statusUpdate = SetTo StagedEntryStatus.Classified } []
                let! after = refetch s.Context entry
                Assert.Equal(before |> statusTransitions |> List.length, after |> statusTransitions |> List.length)
                Assert.Equal(before, after)
            })

    // =========================================================================
    // REQ-STG-6.7 — an entry with a paid line is never Duplicate or Ignored
    // =========================================================================

    [<Theory>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-STG-6.7 for each of Duplicate and Ignored, a manual update moving an entry with a line referenced by a Payment to that status is rejected with a typed error and the entry keeps its status`` (status: string) =
        rolledBack fixture IngestUpdateStageEntry (fun s ->
            result {
                let! entry = s.cardEntry $"Paid-{newTag ()}" true [ "Classified" ]
                do! s.payOn (entry |> lineOfType Debit |> StageEntryLine.stageEntryLineId)
                s.advance ()
                let! target = status |> StagedEntryStatus.fromString
                let attempt = s.update { (entry |> headerIdOf |> noHeaderUpdates) with statusUpdate = SetTo target } []
                Assert.True(attempt |> Result.isError)
                let! after = refetch s.Context entry
                Assert.Equal(Some StagedEntryStatus.Classified, after |> statusOf)
            })

    [<Fact>]
    member _.``REQ-STG-6.7 deduplication leaves an entry that would be a duplicate but has a line referenced by a Payment at its status, and lists it in its result as not flagged because of the Payment`` () =
        rolledBack fixture IngestDeduplicateStageEntries (fun s ->
            result {
                let reference = $"Repeat-{newTag ()}"
                let! _original = s.cardEntry reference true [ "Classified" ]
                s.advance ()
                let! paid = s.cardEntry reference true []
                do! s.payOn (paid |> lineOfType Debit |> StageEntryLine.stageEntryLineId)
                s.advance ()
                let! unpaid = s.cardEntry reference true []
                s.advance ()
                let! remaining = deduplicateStagedEntries s.Context
                let! paidAfter = refetch s.Context paid
                let! unpaidAfter = refetch s.Context unpaid
                // a repeat without a Payment is flagged, so the pass did find these repeats
                Assert.Equal(Some StagedEntryStatus.Duplicate, unpaidAfter |> statusOf)
                Assert.Equal(Some StagedEntryStatus.Ingested, paidAfter |> statusOf)
                Assert.Contains(paid |> headerIdOf, remaining.ingested |> List.map headerIdOf)
            })

    // =========================================================================
    // REQ-STG-7.5.1 — what deduplication returns
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-7.5.1 deduplication returns every staged entry that is Ingested after the pass and no entry of any other status, including those it just flagged Duplicate`` () =
        rolledBack fixture IngestDeduplicateStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! original = s.cardEntry $"Original-{tag}" true []
                s.advance ()
                let! repeat = s.cardEntry $"Original-{tag}" true []
                let! classified = s.cardEntry $"Classified-{tag}" true [ "Classified" ]
                s.advance ()
                let! remaining = deduplicateStagedEntries s.Context
                let! ingested = [ StagedEntryStatus.Ingested ] |> fetchByStatusList s.Context
                let! repeatAfter = refetch s.Context repeat
                let returnedIds = remaining.ingested |> List.map headerIdOf |> Set.ofList
                Assert.Equal(Some StagedEntryStatus.Duplicate, repeatAfter |> statusOf)
                Assert.Equal<Set<StageEntryHeaderId>>(ingested |> List.map headerIdOf |> Set.ofList, returnedIds)
                Assert.Contains(original |> headerIdOf, returnedIds)
                Assert.DoesNotContain(repeat |> headerIdOf, returnedIds)
                Assert.DoesNotContain(classified |> headerIdOf, returnedIds)
                Assert.All(remaining.ingested, fun entry -> Assert.Equal(Some StagedEntryStatus.Ingested, entry |> statusOf))
            })

    // =========================================================================
    // REQ-STG-8.5, 9.11 — the post results
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-8.5 shadow post's after trial balance includes a staged line dated the day it runs and excludes one dated the next day, and its before trial balance likewise excludes a ledger entry dated the next day`` () =
        let tag = newTag ()
        let today = Calendar.today ()
        let tomorrow = today.PlusDays(1)
        let newCode () = "Z" + Guid.NewGuid().ToString("N").Substring(0, 7).ToUpperInvariant()
        let accounts = ResizeArray<AccountId>()
        let staged = ResizeArray<StageEntryHeaderId>()
        let entries = ResizeArray<JournalEntryHeaderId>()
        try
            result {
                let account (accountType: string) =
                    let code = newCode ()
                    runCommandRouteAndAutoCompleteTransaction AccountCreate (fun context ->
                        createTestAccountFromPrimitives context code $"Staging rules {code}" accountType ((Calendar.today ()).PlusYears(-1)) None None None None)
                    |> Result.map (fun (_, id) -> accounts.Add id; code, id)
                let! debitCode, debitId = account "Expense"
                let! creditCode, creditId = account "Liability"
                let! _ =
                    committed fixture IngestRawEntries (fun s ->
                        result {
                            let! onToday = s.staged s.card $"Today-{tag}" today [ (11.11M, "Debit", Some debitCode); (11.11M, "Credit", Some creditCode) ] [ "Classified" ]
                            let! onTomorrow = s.staged s.card $"Tomorrow-{tag}" tomorrow [ (22.22M, "Debit", Some debitCode); (22.22M, "Credit", Some creditCode) ] [ "Classified" ]
                            staged.Add(onToday |> headerIdOf)
                            staged.Add(onTomorrow |> headerIdOf)
                            return ()
                        })
                let! first = postThroughRoute true
                Assert.Equal(0.00M, first.trialBalanceBefore |> debitsOn debitCode)
                Assert.Equal(11.11M, first.trialBalanceAfter |> debitsOn debitCode)
                let! _, entryId =
                    runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew (fun context ->
                        createTestJournalEntryFromPrimitives context $"Staging rules tomorrow {tag}" None tomorrow
                            [ (debitId, 33.33M, "Debit", None); (creditId, 33.33M, "Credit", None) ] [] [])
                entries.Add entryId
                let! second = postThroughRoute true
                Assert.Equal(0.00M, second.trialBalanceBefore |> debitsOn debitCode)
                Assert.Equal(11.11M, second.trialBalanceAfter |> debitsOn debitCode)
            }
            |> railroadWrapper
        finally
            [ yield! staged |> Seq.map (Some >> Cleanup.cleanUpStageEntryHeaderId)
              yield! entries |> Seq.map (Some >> Cleanup.cleanUpJournalEntryId)
              yield! accounts |> Seq.map (Some >> Cleanup.cleanUpAccountId) ]
            |> List.iter orFail

    [<Fact>]
    member _.``REQ-STG-8.5 shadow post's result states that the post was rolled back`` () =
        let result = postThroughRoute true |> orFail
        Assert.True(result.wasRolledBack)

    [<Fact>]
    member _.``REQ-STG-9.11 batch post returns before and after trial balances equal to those shadow post returns for the same entries, and states that the post was not rolled back`` () =
        let tag = newTag ()
        let staged = ResizeArray<StageEntryHeaderId>()
        let entries = ResizeArray<JournalEntryHeaderId>()
        try
            result {
                let! _ =
                    committed fixture IngestRawEntries (fun s ->
                        result {
                            let! first = s.staged s.card $"Batch1-{tag}" (Calendar.today ()) [ (70.00M, "Debit", Some "F-2230"); (70.00M, "Credit", Some "F-1280") ] [ "Classified" ]
                            let! second = s.staged s.card $"Batch2-{tag}" (Calendar.today ()) [ (80.00M, "Debit", Some "F-5300"); (80.00M, "Credit", Some "F-1280") ] [ "Classified"; "Reviewed" ]
                            staged.Add(first |> headerIdOf)
                            staged.Add(second |> headerIdOf)
                            return ()
                        })
                let! shadow = postThroughRoute true
                let! batch = postThroughRoute false
                let! posted =
                    staged
                    |> Seq.map (fetchByStageEntryHeaderId (fresh ()))
                    |> List.ofSeq
                    |> convertListOfResultsToResultsList
                posted |> List.iter (fun e -> e |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId |> Option.iter entries.Add)
                Assert.Equal(2, entries.Count)
                Assert.NotEqual<TrialBalanceReturnRow list>(shadow.trialBalanceBefore, shadow.trialBalanceAfter)
                Assert.Equal<TrialBalanceReturnRow list>(shadow.trialBalanceBefore, batch.trialBalanceBefore)
                Assert.Equal<TrialBalanceReturnRow list>(shadow.trialBalanceAfter, batch.trialBalanceAfter)
                Assert.False(batch.wasRolledBack)
            }
            |> railroadWrapper
        finally
            // staged entries first: posting links each staged header and line to the journal entry it made
            [ yield! staged |> Seq.map (Some >> Cleanup.cleanUpStageEntryHeaderId)
              yield! entries |> Seq.map (Some >> Cleanup.cleanUpJournalEntryId) ]
            |> List.iter orFail

    // =========================================================================
    // REQ-STG-9.10 — posting records what it produced
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.10 batch post records on each staged entry the ID of the journal entry it produced`` () =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! first = s.cardEntry $"Produced1-{tag}" true [ "Classified" ]
                let! second = s.staged s.card $"Produced2-{tag}" (Calendar.today ()) [ (55.00M, "Debit", Some "F-5300"); (55.00M, "Credit", Some "F-1280") ] [ "Classified"; "Reviewed" ]
                s.advance ()
                do! post s.Context
                [ first; second ] |> List.ofSeq |> List.iter (fun entry ->
                    let readBack = refetch s.Context entry |> orFail
                    let journalEntry = postedFrom s.Context entry |> orFail
                    Assert.Equal(
                        Some(journalEntry |> JE.header |> JournalEntryHeader.journalEntryHeaderId),
                        readBack |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId))
            })

    [<Fact>]
    member _.``REQ-STG-9.10 for an entry whose lines differ in account, line type or amount, each staged line records the journal entry line with the same account, line type and amount, including when the staged lines are not in the order the journal entry lines are created in`` () =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let! entry =
                    s.staged s.card $"Pairing-{newTag ()}" (Calendar.today ())
                        [ (30.00M, "Credit", Some "F-1280"); (10.00M, "Debit", Some "F-2230"); (20.00M, "Debit", Some "F-5300") ]
                        [ "Classified" ]
                s.advance ()
                do! post s.Context
                let! readBack = refetch s.Context entry
                let! journalEntry = postedFrom s.Context entry
                let journalLines = journalEntry |> JE.jeLines
                Assert.Equal(3, readBack |> seLines |> List.length)
                readBack |> seLines |> List.ofSeq |> List.iter (fun line ->
                    let paired =
                        journalLines |> List.filter (fun j -> Some(j |> JournalEntryLine.journalEntryLineId) = (line |> StageEntryLine.journalEntryLineId))
                    let journalLine = Assert.Single(paired)
                    Assert.Equal(line |> StageEntryLine.accountId, Some(journalLine |> JournalEntryLine.accountId))
                    Assert.Equal(line |> StageEntryLine.lineType, journalLine |> JournalEntryLine.lineType)
                    Assert.Equal(line |> StageEntryLine.amount, journalLine |> JournalEntryLine.amount))
            })

    [<Fact>]
    member _.``REQ-STG-9.10 for an entry with two lines of the same account, line type and amount, each of those staged lines records a different journal entry line`` () =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let! entry =
                    s.staged s.card $"Twins-{newTag ()}" (Calendar.today ())
                        [ (50.00M, "Debit", Some "F-2230"); (50.00M, "Debit", Some "F-2230"); (100.00M, "Credit", Some "F-1280") ]
                        [ "Classified" ]
                s.advance ()
                do! post s.Context
                let! readBack = refetch s.Context entry
                let! journalEntry = postedFrom s.Context entry
                let journalLineIds = journalEntry |> JE.jeLines |> List.map JournalEntryLine.journalEntryLineId |> Set.ofList
                let twins = readBack |> seLines |> List.filter (fun l -> l |> StageEntryLine.lineType = Debit)
                let recorded = twins |> List.choose StageEntryLine.journalEntryLineId
                Assert.Equal(2, recorded.Length)
                Assert.Equal(2, recorded |> List.distinct |> List.length)
                Assert.All(recorded, fun id -> Assert.Contains(id, journalLineIds))
            })

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-STG-7.5.1 REQ-STG-6.7 for each of Ingested, Classified, NoMatch and Conflict, a paid repeat that deduplication declines to flag is in the result's declined list and keeps its status`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-STG-7.5.1 deduplication's declined list holds no repeat without a Payment, which is flagged Duplicate instead, no paid Reviewed repeat, which dedup never flags, and no entry that is not a repeat`` () =
        Assert.Fail "Not yet implemented"
