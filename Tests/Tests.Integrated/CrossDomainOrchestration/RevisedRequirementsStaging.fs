module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsStaging

open System
open System.IO
open System.Text.Json
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Ui.InterfaceBridge.InterfaceContracts.IngestionContracts
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module JE = Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration

(* Plan item 30: the clauses of revised DataIngestion requirements that no earlier test reached.
   The file tests go through the Ingestion routes, which commit: each writes its own file to a scratch import directory
   and a finally deletes every staged entry from it and the file itself. Everything else runs inside a transaction that
   rolls back. Staged entries are from TestCreditCardCo and carry a fresh tag in their description and reference; the
   context's instant is advanced before every operation that changes a status, because one staged entry cannot hold two
   status transitions at one instant (REQ-STG-4.1.2). *)

let private fresh () = Context.create NoTransaction FetchOnly

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private headerIdOf (entry: StageEntry) = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

let private statusOf (entry: StageEntry) = entry |> stageEntryHeader |> StageEntryHeader.currentStatus

let private accountsOf (entry: StageEntry) = entry |> seLines |> List.map StageEntryLine.accountId

// =========================================================================
// Files
// =========================================================================

let private root = Path.Combine(Path.GetTempPath(), "sonofleo-revised-staging")
let private importDir = Path.Combine(root, "import")
let private processedDir = Path.Combine(root, "processed")

let private jsonString (s: string) = JsonSerializer.Serialize s

/// One record of a TestCreditCardCo group dated today, with no account and no memo.
let private record (group: string) (amount: string) (lineType: string) (description: string) (reference: string) =
    [ "baseStageEntryGroupId", jsonString group
      "entryDate", jsonString ((Calendar.today ()).ToString("yyyy-MM-dd", null))
      "amount", amount
      "entryType", jsonString lineType
      "accountCode", "null"
      "description", jsonString description
      "fiSource", jsonString "TestCreditCardCo"
      "fiReference", jsonString reference
      "memo", "null" ]
    |> List.map (fun (key, value) -> $"{jsonString key}:{value}")
    |> String.concat ","
    |> sprintf "{%s}"

/// A balanced Debit and Credit pair with the group ID and the amount (written as the JSON number given).
let private pair (group: string) (amount: string) (tag: string) =
    [ record group amount "Debit" $"Revised staging {tag}" $"{tag}-{group.Length}"
      record group amount "Credit" $"Revised staging {tag}" $"{tag}-{group.Length}" ]

let private ingest (fileName: string) =
    ({ fileName = fileName; importDir = importDir; processedDir = processedDir } : IngestRawFileToStageInput)
    |> Json.toJson
    |> Result.bind (routeUiCommandForTesting "Ingestion" "IngestRawFileToStage" [])

let private stagedFrom (path: string) =
    path |> SourceFile.create |> Result.bind (fetchAllByFile (fresh ()) None)

let private withFile (rows: string list) (test: string -> string -> unit) =
    let fileName = $"rev-{Guid.NewGuid():N}.jsonl"
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
                entries |> List.map (headerIdOf >> Some >> Cleanup.cleanUpStageEntryHeaderId) |> convertListOfResultsToResultsList)
        File.Delete path
        Directory.GetFiles(processedDir, $"*-{fileName}") |> Array.iter File.Delete
        cleaned |> orFail |> ignore

// =========================================================================
// Staged entries, rules and posting inside one transaction
// =========================================================================

let private noFilter : StageEntryFetchFilter =
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
      accountId = None
      memo = None
      journalEntryHeaderId = None
      journalEntryLineId = None }

type private Scenario(fixture: TestDataFixture, initialContext: Context.Context) =
    let mutable context = initialContext
    let cardSource =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestCreditCardCo")
    let idOf (code: string) =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    member _.Context = context
    member _.advance () = context <- context |> TestContext.updateInitiationInstant
    member _.accountIdOf code = idOf code

    /// A 100.00 TestCreditCardCo entry with the description, reference and date, a Debit and a Credit on F-2230 and
    /// F-1280 or on no account, moved from Ingested through each status in the path.
    member _.staged (sourceFile: string) (description: string) (reference: string) (date: LocalDate) (withAccounts: bool) (path: string list) =
        let start = Clock.now ()
        let transitions =
            ("Ingested" :: path)
            |> List.pairwise
            |> List.mapi (fun i (from, into) -> (Some from, into, start.Plus(Duration.FromTicks(100L * int64 (i + 1))), "Operator"))
        createStageEntryForTest context sourceFile description reference cardSource date
            [ (100.00M, "Debit", (if withAccounts then Some "F-2230" else None), None, None)
              (100.00M, "Credit", (if withAccounts then Some "F-1280" else None), None, None) ]
            ((None, "Ingested", start, "StageIngestion") :: transitions)

    member this.entry (tag: string) (withAccounts: bool) (path: string list) =
        this.staged "/tmp/revised-staging-test.dat" $"Revised staging {tag}" tag (Calendar.today ()) withAccounts path

    member _.refetch (entry: StageEntry) = entry |> headerIdOf |> fetchByStageEntryHeaderId context

    /// A rule claiming any line whose entry description matches the text.
    member _.rule (claimant: ClassificationClaimant) (priority: int) (text: string) =
        result {
            let! name = $"Revised staging rule {Guid.NewGuid():N}" |> ClassificationRuleName.create
            let! pattern = text |> StringSearchPattern.create
            let! groups = [ ("And", [ FieldMatch.Description pattern ], None) ] |> createClassificationRuleGroupListForTest
            return! ClassificationOrchestration.createNewClassificationRule context name claimant priority groups
        }

    /// An Outgo agreement with one leg on F-2230 and F-1280; returns the leg's ID.
    member _.leg () =
        result {
            let name = $"Revised staging {Guid.NewGuid():N}"
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Revised staging counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = $"{name} leg" |> PaymentAgreementName.create
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first)) { nextInstance = LocalDate(2049, 3, 1) }
                    counterparty activityPeriod None
                    [ (legName, DebitAccount(idOf "F-2230"), CreditAccount(idOf "F-1280"), None, None, None) ]
            return agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
        }

    member this.accountRun () = this.advance (); classifyAccounts context
    member this.dedup () = this.advance (); deduplicateStagedEntries context
    member this.post () = this.advance (); post context

let private rolledBack (fixture: TestDataFixture) action (body: Scenario -> Result<unit, IAppError>) =
    runCommandRouteAndAutoRollback action (fun context -> body (Scenario(fixture, context))) |> railroadWrapper

let private statusNamed (name: string) = name |> StagedEntryStatus.fromString |> orFail

[<Collection("SharedTestData")>]
type RevisedRequirementsStagingTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-STG-1.4, 1.6 — group_id length and amount precision in the file
    // =========================================================================

    [<Theory>]
    [<InlineData(36)>]
    [<InlineData(37)>]
    member _.``REQ-STG-1.4 for each group_id length (36 characters accepted, 37 characters rejected with a typed error naming the field), ingesting a file with that group_id gives that outcome`` (length: int) =
        let tag = newTag ()
        let group = tag + String('g', length - tag.Length)
        withFile (pair group "25.00" tag) (fun fileName path ->
            let outcome = ingest fileName
            let staged = stagedFrom path |> orFail
            if length = 36 then
                Assert.True(outcome |> Result.isOk, $"%A{outcome |> Result.mapError (fun e -> e.ToMessage())}")
                Assert.Equal(1, staged.Length)
            else
                let refused =
                    (* a file with invalid records is refused as a whole; each record carries its own typed error *)
                    match outcome with
                    | Error (AsError (IngestionFileRejected (_, records))) ->
                        not records.IsEmpty
                        && records |> List.forall (fun r ->
                            match r.error with
                            | AsError (IngestionBaseStageEntryGroupIdTooLong (value, 36)) -> value = group
                            | _ -> false)
                    | _ -> false
                Assert.True(refused, $"%A{outcome |> Result.mapError (fun e -> e.ToMessage())}")
                Assert.Empty(staged))

    [<Theory>]
    [<InlineData("12", 12.00)>]
    [<InlineData("12.5", 12.50)>]
    member _.``REQ-STG-1.6 for each amount with no decimal places and with one decimal place, a record carrying it is staged with that amount`` (written: string, expected: float) =
        let tag = newTag ()
        withFile (pair "G1" written tag) (fun fileName path ->
            let outcome = ingest fileName
            Assert.True(outcome |> Result.isOk, $"%A{outcome |> Result.mapError (fun e -> e.ToMessage())}")
            let staged = stagedFrom path |> orFail |> Assert.Single
            Assert.All(staged |> seLines, fun l -> Assert.Equal(decimal expected, l |> StageEntryLine.amount |> Money.amount)))

    // =========================================================================
    // REQ-STG-2.6 — source_file length
    // =========================================================================

    [<Theory>]
    [<InlineData(150)>]
    [<InlineData(151)>]
    member _.``REQ-STG-2.6 for each source_file length (150 characters accepted, 151 characters rejected with a typed error and nothing stored), creating a staged entry with that source file gives that outcome`` (length: int) =
        rolledBack fixture IngestRawEntries (fun s ->
            result {
                let tag = newTag ()
                let prefix = $"/tmp/{tag}/"
                let sourceFile = prefix + String('f', length - prefix.Length - 4) + ".dat"
                let attempt = s.staged sourceFile $"Revised staging {tag}" tag (Calendar.today ()) true []
                let! stored = sourceFile.Substring(0, 150) |> SourceFile.create |> Result.bind (fetchAllByFile s.Context None)
                if length = 150 then
                    Assert.True(attempt |> Result.isOk, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                    Assert.Equal(1, stored.Length)
                else
                    let refused =
                        match attempt with
                        | Error (AsError (IngestionSourceFileTooLong (value, 150))) -> value = sourceFile
                        | _ -> false
                    Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                    Assert.Empty(stored)
            })

    // =========================================================================
    // REQ-STG-5.1, 5.2 — which entries and which rules a classification run uses
    // =========================================================================

    [<Theory>]
    [<InlineData("Ingested")>]
    [<InlineData("NoMatch")>]
    [<InlineData("Conflict")>]
    [<InlineData("Classified")>]
    [<InlineData("Reviewed")>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    [<InlineData("Posted")>]
    member _.``REQ-STG-5.1 for each status, a classification run assigns a matching account rule's account to an entry whose status is Ingested, NoMatch or Conflict, and leaves an entry with any other status unchanged`` (status: string) =
        rolledBack fixture ClassifyAccounts (fun s ->
            result {
                let tag = newTag ()
                let path =
                    match status with
                    | "Ingested" -> []
                    | "Reviewed" -> [ "Classified"; "Reviewed" ]
                    | "Posted" -> [ "Classified"; "Reviewed"; "Posted" ]
                    | other -> [ other ]
                let! entry = s.entry tag false path
                let! _ = s.rule (ClassificationClaimant.Account(s.accountIdOf "F-5300")) 1 tag
                let! _ = s.accountRun ()
                let! after = s.refetch entry
                if [ "Ingested"; "NoMatch"; "Conflict" ] |> List.contains status then
                    Assert.Equal<AccountId option list>([ Some(s.accountIdOf "F-5300"); Some(s.accountIdOf "F-5300") ], accountsOf after)
                    Assert.Equal(Some(statusNamed "Classified"), statusOf after)
                else
                    Assert.Equal<AccountId option list>([ None; None ], accountsOf after)
                    Assert.Equal(Some(statusNamed status), statusOf after)
            })

    [<Fact>]
    member _.``REQ-STG-5.2 a classification run leaves a staged line unassigned when the only rule that matches it claims a payment agreement`` () =
        rolledBack fixture ClassifyAccounts (fun s ->
            result {
                let tag = newTag ()
                let! entry = s.entry tag false []
                let! legId = s.leg ()
                let! _ = s.rule (ClassificationClaimant.PaymentAgreement legId) 1 tag
                let! _ = s.accountRun ()
                let! after = s.refetch entry
                Assert.Equal<AccountId option list>([ None; None ], accountsOf after)
            })

    [<Fact>]
    member _.``REQ-STG-5.2 when a payment agreement rule with a lower priority value and an account rule both match a staged line, a classification run assigns the account rule's account`` () =
        rolledBack fixture ClassifyAccounts (fun s ->
            result {
                let tag = newTag ()
                let! entry = s.entry tag false []
                let! legId = s.leg ()
                let! _ = s.rule (ClassificationClaimant.PaymentAgreement legId) 1 tag
                let! _ = s.rule (ClassificationClaimant.Account(s.accountIdOf "F-5300")) 50 tag
                let! _ = s.accountRun ()
                let! after = s.refetch entry
                Assert.Equal<AccountId option list>([ Some(s.accountIdOf "F-5300"); Some(s.accountIdOf "F-5300") ], accountsOf after)
                Assert.Equal(Some(statusNamed "Classified"), statusOf after)
            })

    // =========================================================================
    // REQ-STG-7.2 — which entries dedup flags, and which is the original
    // =========================================================================

    [<Theory>]
    [<InlineData("Classified")>]
    [<InlineData("NoMatch")>]
    [<InlineData("Conflict")>]
    member _.``REQ-STG-7.2 for each of the statuses Classified, NoMatch and Conflict, a later entry with that status sharing the original's source and FI reference is flagged Duplicate`` (status: string) =
        rolledBack fixture IngestDeduplicateStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! original = s.staged "/tmp/revised-staging-test.dat" $"Original {tag}" tag (Calendar.today ()) false []
                s.advance ()
                let! repeat = s.staged "/tmp/revised-staging-test.dat" $"Repeat {tag}" tag (Calendar.today ()) false [ status ]
                let! _ = s.dedup ()
                let! originalAfter = s.refetch original
                let! repeatAfter = s.refetch repeat
                Assert.Equal(Some(statusNamed "Ingested"), statusOf originalAfter)
                Assert.Equal(Some(statusNamed "Duplicate"), statusOf repeatAfter)
            })

    [<Fact>]
    member _.``REQ-STG-7.2 when two entries share a source and FI reference, the first-ingested is the original and the later-ingested is flagged Duplicate even when the later-ingested has the earlier entry date`` () =
        rolledBack fixture IngestDeduplicateStageEntries (fun s ->
            result {
                let tag = newTag ()
                let today = Calendar.today ()
                let! first = s.staged "/tmp/revised-staging-test.dat" $"First {tag}" tag today false []
                s.advance ()
                let! later = s.staged "/tmp/revised-staging-test.dat" $"Later {tag}" tag (today.PlusDays(-10)) false []
                let! _ = s.dedup ()
                let! firstAfter = s.refetch first
                let! laterAfter = s.refetch later
                Assert.Equal(Some(statusNamed "Ingested"), statusOf firstAfter)
                Assert.Equal(Some(statusNamed "Duplicate"), statusOf laterAfter)
            })

    [<Fact>]
    member _.``REQ-STG-7.2 an Ignored original keeps its status, and a later Ingested entry sharing its source and FI reference is flagged Duplicate`` () =
        rolledBack fixture IngestDeduplicateStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! original = s.staged "/tmp/revised-staging-test.dat" $"Ignored original {tag}" tag (Calendar.today ()) false [ "Ignored" ]
                s.advance ()
                let! repeat = s.staged "/tmp/revised-staging-test.dat" $"Repeat {tag}" tag (Calendar.today ()) false []
                let! _ = s.dedup ()
                let! originalAfter = s.refetch original
                let! repeatAfter = s.refetch repeat
                Assert.Equal(Some(statusNamed "Ignored"), statusOf originalAfter)
                Assert.Equal(Some(statusNamed "Duplicate"), statusOf repeatAfter)
            })

    // =========================================================================
    // REQ-STG-9.3, 10.2, 10.3 — posted entries
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-9.3 a journal entry posted from a staged entry, fetched back after posting, carries no comments`` () =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! entry = s.entry tag true [ "Classified"; "Reviewed" ]
                do! s.post ()
                let! after = s.refetch entry
                let! journalEntryId =
                    after |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId
                    |> Option.map Ok |> Option.defaultValue (Error(Tests.Helpers.TestError.TestingError "not posted" :> IAppError))
                let! posted = journalEntryId |> JE.fetchById s.Context
                Assert.Equal<string>($"Revised staging {tag}", posted |> JE.header |> JournalEntryHeader.description |> JournalEntryDescription.value)
                Assert.Empty(posted |> JE.comments)
            })

    [<Theory>]
    [<InlineData("journal entry ID")>]
    [<InlineData("journal entry line ID")>]
    member _.``REQ-STG-10.2 for each of the journal entry ID filter and the journal entry line ID filter, fetching staged entries returns the posted entry that produced that journal entry and no other entry`` (filterCase: string) =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! first = s.entry $"{tag}a" true [ "Classified"; "Reviewed" ]
                let! _ = s.entry $"{tag}b" true [ "Classified"; "Reviewed" ]
                do! s.post ()
                let! firstAfter = s.refetch first
                let filter =
                    match filterCase with
                    | "journal entry ID" -> { noFilter with journalEntryHeaderId = firstAfter |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId }
                    | _ -> { noFilter with journalEntryLineId = firstAfter |> seLines |> List.head |> StageEntryLine.journalEntryLineId }
                Assert.True(filter.journalEntryHeaderId.IsSome || filter.journalEntryLineId.IsSome)
                let! found = fetchFiltered s.Context None filter
                Assert.Equal<StageEntryHeaderId list>([ headerIdOf first ], found |> List.map headerIdOf)
            })

    [<Fact>]
    member _.``REQ-STG-10.3 fetching staged entries by the journal entry line ID of one of a posted entry's lines returns that entry with all of its lines`` () =
        rolledBack fixture IngestPostStageEntries (fun s ->
            result {
                let tag = newTag ()
                let! entry = s.entry tag true [ "Classified"; "Reviewed" ]
                do! s.post ()
                let! after = s.refetch entry
                let creditLine = after |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let! found = fetchFiltered s.Context None { noFilter with journalEntryLineId = creditLine |> StageEntryLine.journalEntryLineId }
                let returned = found |> Assert.Single
                Assert.Equal<Set<StageEntryLineId>>(
                    after |> seLines |> List.map StageEntryLine.stageEntryLineId |> Set.ofList,
                    returned |> seLines |> List.map StageEntryLine.stageEntryLineId |> Set.ofList)
            })
