module Tests.Integrated.CrossDomainOrchestration.AccountCreateActivityBalance

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
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
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.AccountContracts
module Shared = Ui.InterfaceBridge.InterfaceContracts.SharedContracts

(* Every test builds its own accounts, with codes "Z" plus seven random hex digits, active since 1 January 2026, and
   its own journal entries, dated inside the fixture's open fiscal periods. The account routes read and write outside
   any test transaction, so the setup commits; a finally deletes the journal entries, then the accounts, children
   before parents. Activity and balance results cover the whole ledger, so tests either pick their own rows out of
   the result or compare a filtered result with the unfiltered one. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private newCode () = "Z" + Guid.NewGuid().ToString("N").Substring(0, 7).ToUpperInvariant()

let private send (verb: string) (input: 'a) = input |> Json.toJson |> Result.bind (routeUiCommandForTesting "Account" verb [])

let private accountIdsWithCode (code: string) =
    executeReaderQuery (fresh () |> Context.getDatabaseTransaction) "select unique_id from ledger.account where code = @code"
        [ { name = "@code"; value = CharString code } ] (RowReader.getUuid "unique_id") Ok AnyQuantityIsAcceptable

let private noFilter : Contracts.AccountActivityFilterInput =
    { accountCode = None
      temporalFilter = None
      source = None
      accountType = None
      accountSubtype = None
      accountParentCode = None
      journalEntryId = None
      amount = None
      description = None
      unVoidedOnly = false }

let private activity (filter: Contracts.AccountActivityFilterInput) (sort: FetchSort option) =
    ({ filter = filter; sort = sort } : Contracts.AccountActivityFetchInput)
    |> send "FetchActivity"
    |> Result.bind Json.fromJson<Contracts.AccountActivityReturn list>

let private balances (codes: string list) (asOf: LocalDate option) =
    ({ codes = codes; asOf = asOf } : Contracts.AccountBalanceFetchByAccountListInput)
    |> send "FetchBalances"
    |> Result.bind Json.fromJson<Contracts.AccountBalanceReturn list>

let private balanceOf (code: string) (asOf: LocalDate option) =
    balances [ code ] asOf |> Result.map List.exactlyOne

/// A row's identity: its account and, when it represents a line, that line.
let private keyOf (row: Contracts.AccountActivityReturn) = row.accountCode, row.activityDetail |> Option.map _.lineId

let private keysOf (rows: Contracts.AccountActivityReturn list) = rows |> List.map keyOf |> Set.ofList

let private lineIdsOf (entry: JournalEntry) (accountId: AccountId) =
    entry
    |> JournalEntryOrchestration.jeLines
    |> List.filter (fun l -> l |> JournalEntryLine.accountId = accountId)
    |> List.map (JournalEntryLine.journalEntryLineId >> JournalEntryLineId.value)

let private entryIdOf (entry: JournalEntry) =
    entry |> JournalEntryOrchestration.header |> JournalEntryHeader.journalEntryHeaderId

let private detailOf (row: Contracts.AccountActivityReturn) = row.activityDetail

type private Period = { key: string; startDate: LocalDate; endDate: LocalDate }

/// Builds accounts and journal entries, each committed, and remembers them for clean-up.
type private Ledger(fixture: TestDataFixture) =
    let accounts = ResizeArray<AccountId>()
    let routeCodes = ResizeArray<string>()
    let entries = ResizeArray<JournalEntryHeaderId>()
    let openPeriods =
        fixture.Data.openFiscalPeriodIds
        |> List.map (fun id ->
            id
            |> FiscalPeriod.fetchById (fresh ())
            |> Result.map (fun p ->
                { key = p |> FiscalPeriod.periodKey |> FiscalPeriodKey.value
                  startDate = p |> FiscalPeriod.startDate
                  endDate = p |> FiscalPeriod.endDate })
            |> Result.defaultWith (fun e -> failwith (e.ToMessage())))
        |> List.sortBy _.startDate
    let today = Calendar.today ()

    member _.Accounts = accounts |> List.ofSeq
    member _.RouteCodes = routeCodes |> List.ofSeq
    member _.Entries = entries |> List.ofSeq

    /// The open period holding today.
    member _.current = openPeriods |> List.find (fun p -> p.startDate <= today && today <= p.endDate)

    /// Another open period, before the current one when there is one.
    member this.other =
        let current = this.current
        match openPeriods |> List.filter (fun p -> p.endDate < current.startDate) |> List.tryLast with
        | Some p -> p
        | None -> openPeriods |> List.find (fun p -> p.startDate > current.endDate)

    /// The latest open period.
    member _.latest = openPeriods |> List.last

    /// Remembers a code the test will create an account under through a route.
    member _.routeCode (code: string) = routeCodes.Add code; code

    /// Creates an account; returns its code and ID.
    member _.account (accountType: string) (subtype: string option) (parent: AccountId option) (reference: string option) =
        let code = newCode ()
        runCommandRouteAndAutoCompleteTransaction AccountCreate (fun context ->
            createTestAccountFromPrimitives context code $"Account test {code}" accountType (LocalDate(2026, 1, 1)) None
                subtype parent reference)
        |> Result.map (fun (_, id) -> accounts.Add id; code, id)

    /// Posts a journal entry with the lines (account, amount, line type, memo).
    member _.entry (date: LocalDate) (source: string option) (description: string) (lines: (AccountId * decimal * string * string option) list) =
        runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew (fun context ->
            createTestJournalEntryFromPrimitives context description source date lines [] [])
        |> Result.map (fun (entry, id) -> entries.Add id; entry)

    /// Voids the entry; returns it as stored afterwards.
    member _.voidEntry (entry: JournalEntry) =
        runCommandRouteAndAutoCompleteTransaction JournalEntryVoid (fun context ->
            result {
                let! reason = "Account test void" |> CommentText.create
                return! JournalEntryVoiding.voidJournalEntry context None reason (entryIdOf entry)
            })

/// The shared shape for the activity tests: a parent P with a child C, a grandchild G and a child N with no lines, a
/// Liability L, and four entries. J1 and J3 are dated in the current period, J2 and J4 in the other. J3 is voided.
type private Tree =
    { tag: string
      p: string * AccountId
      c: string * AccountId
      g: string * AccountId
      n: string * AccountId
      l: string * AccountId
      dateA: LocalDate
      dateB: LocalDate
      j1: JournalEntry
      j2: JournalEntry
      j3: JournalEntry
      j4: JournalEntry
      j5: JournalEntryOrchestration.JournalEntry }

let private tree (ledger: Ledger) =
    result {
        let tag = Guid.NewGuid().ToString("N").Substring(0, 8)
        let! p = ledger.account "Asset" (Some "Cash") None (Some "ref-p")
        let! c = ledger.account "Asset" (Some "FixedAsset") (Some(snd p)) None
        let! g = ledger.account "Asset" (Some "Investment") (Some(snd c)) (Some "ref-g")
        let! n = ledger.account "Asset" (Some "FixedAsset") (Some(snd p)) (Some "ref-n")
        let! l = ledger.account "Liability" (Some "CurrentLiability") None None
        let dateA = ledger.current.startDate.PlusDays(1)
        let dateB = ledger.other.startDate.PlusDays(1)
        let! j1 = ledger.entry dateA (Some $"Src{tag}") $"Rent paid {tag}" [ (snd p, 100.00M, "Debit", Some "p memo"); (snd l, 100.00M, "Credit", None) ]
        let! j2 = ledger.entry dateB (Some $"Src{tag}x") $"RENT PAID {tag}" [ (snd c, 40.00M, "Debit", None); (snd l, 40.00M, "Credit", None) ]
        let! j3 = ledger.entry dateA (Some $"Void{tag}") $"Transfer {tag}" [ (snd g, 25.00M, "Debit", Some "g memo"); (snd p, 25.00M, "Credit", None) ]
        let! j4 = ledger.entry dateB (Some $"Src{tag}") $"Top up {tag}" [ (snd p, 10.00M, "Debit", None); (snd l, 10.00M, "Credit", None) ]
        let! j5 = ledger.entry dateB None $"Grandchild {tag}" [ (snd g, 7.00M, "Debit", None); (snd l, 7.00M, "Credit", None) ]
        let! voided = ledger.voidEntry j3
        return
            { tag = tag; p = p; c = c; g = g; n = n; l = l; dateA = dateA; dateB = dateB
              j1 = j1; j2 = j2; j3 = voided; j4 = j4; j5 = j5 }
    }

[<Collection("SharedTestData")>]
type AccountCreateActivityBalanceTests(fixture: TestDataFixture) =

    (* Runs the test with a Ledger that commits what it builds, then deletes it all. *)
    let withLedger (test: Ledger -> Result<unit, IAppError>) =
        let ledger = Ledger(fixture)
        let cleanUpFailures = ResizeArray<string>()
        try
            test ledger |> railroadWrapper
        finally
            let routeIds =
                ledger.RouteCodes
                |> List.collect (fun code ->
                    match accountIdsWithCode code with
                    | Ok ids -> ids |> List.map (AccountId.fromGuid >> Ok)
                    | Error e -> [ Error e ])
            [ for id in ledger.Entries do yield Cleanup.cleanUpJournalEntryId (Some id)
              for id in routeIds |> List.rev do yield id |> Result.bind (Some >> Cleanup.cleanUpAccountId)
              for id in ledger.Accounts |> List.rev do yield Cleanup.cleanUpAccountId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

    let createInput (code: string) : Contracts.AccountCreateInput =
        { code = code
          name = $"Account test {code}"
          accountTypeSt = "Asset"
          activeBegin = LocalDate(2026, 1, 1)
          activeEnd = None
          subType = None
          parentCode = None
          reference = None }

    let fetchByCode (code: string) =
        ({ code = code } : Contracts.AccountFetchByCodeInput) |> send "FetchByCode" |> Result.bind Json.fromJson<Contracts.AccountReturn>

    // =========================================================================
    // REQ-AC-2.22, 2.23 — create at the interface
    // =========================================================================

    [<Fact>]
    member _.``REQ-AC-2.22 a create-account payload whose parent is given by the code of an existing Account stores the new Account with that Account as its parent`` () =
        withLedger (fun ledger ->
            result {
                let! parentCode, parentId = ledger.account "Asset" None None None
                let code = ledger.routeCode (newCode ())
                let! _ = { createInput code with parentCode = Some parentCode } |> send "Create"
                let! childIds = accountIdsWithCode code
                let! stored = childIds |> List.exactlyOne |> AccountId.fromGuid |> Account.fetchById (fresh ())
                Assert.Equal(Some parentId, stored |> Account.parentId)
            })

    [<Fact>]
    member _.``REQ-AC-2.22 a create-account payload whose parent code matches no Account is rejected with a typed error naming the code, and nothing is stored`` () =
        withLedger (fun ledger ->
            result {
                let code = ledger.routeCode (newCode ())
                let missing = newCode ()
                let attempt = { createInput code with parentCode = Some missing } |> send "Create"
                let namesIt =
                    match attempt with
                    | Error (AsError (LedgerError.AccountParentCodeInvalid c)) -> c = missing
                    | _ -> false
                let! stored = accountIdsWithCode code
                Assert.True(namesIt)
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-AC-2.23 a create-account payload with an active end stores the Account with that active end`` () =
        withLedger (fun ledger ->
            result {
                let code = ledger.routeCode (newCode ())
                let! _ = { createInput code with activeEnd = Some(LocalDate(2027, 6, 30)) } |> send "Create"
                let! stored = fetchByCode code
                Assert.Equal(Some(LocalDate(2027, 6, 30)), stored.activeEnd)
            })

    [<Fact>]
    member _.``REQ-AC-2.23 a create-account payload with no active end stores the Account with a null active end`` () =
        withLedger (fun ledger ->
            result {
                let code = ledger.routeCode (newCode ())
                let! _ = createInput code |> send "Create"
                let! stored = fetchByCode code
                Assert.Equal(None, stored.activeEnd)
            })

    // =========================================================================
    // REQ-AC-3.11 — codes that match nothing
    // =========================================================================

    [<Theory>]
    [<InlineData("FetchByCode")>]
    [<InlineData("UpdateName")>]
    [<InlineData("Deactivate")>]
    [<InlineData("FetchActivity")>]
    [<InlineData("FetchBalances")>]
    member _.``REQ-AC-3.11 for each of retrieve, update, deactivate, activity and balance, an operation naming an account code that matches no Account fails with a typed error naming the code`` (operation: string) =
        let missing = newCode ()
        let attempt =
            match operation with
            | "FetchByCode" -> ({ code = missing } : Contracts.AccountFetchByCodeInput) |> send operation
            | "UpdateName" -> ({ code = missing; newName = "A new name" } : Contracts.AccountUpdateNameInput) |> send operation
            | "Deactivate" -> ({ code = missing; activeEnd = Some(LocalDate(2027, 6, 30)) } : Contracts.AccountDeactivationInput) |> send operation
            | "FetchActivity" -> activity { noFilter with accountCode = Some missing } None |> Result.map ignore |> Result.map string
            | _ -> balances [ missing ] None |> Result.map ignore |> Result.map string
        let namesIt =
            match attempt with
            | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId c)) -> c = missing
            | _ -> false
        Assert.True(namesIt)

    [<Fact>]
    member _.``REQ-AC-3.11 a balance query whose code list mixes existing codes with one that matches no Account fails with a typed error naming that code and returns no balances`` () =
        withLedger (fun ledger ->
            result {
                let! first, _ = ledger.account "Asset" None None None
                let! second, _ = ledger.account "Liability" None None None
                let missing = newCode ()
                let attempt = balances [ first; missing; second ] None
                let namesIt =
                    match attempt with
                    | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId c)) -> c = missing
                    | _ -> false
                Assert.True(namesIt)
            })

    [<Theory>]
    [<InlineData("account code")>]
    [<InlineData("parent account code")>]
    member _.``REQ-AC-3.11 an activity query whose account code or parent account code filter matches no Account fails with a typed error naming the code`` (field: string) =
        let missing = newCode ()
        let filter =
            if field = "account code" then { noFilter with accountCode = Some missing }
            else { noFilter with accountParentCode = Some missing }
        let namesIt =
            match activity filter None with
            | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId c))
            | Error (AsError (LedgerError.AccountParentCodeInvalid c)) -> c = missing
            | _ -> false
        Assert.True(namesIt)

    // =========================================================================
    // REQ-AC-3.12 — what an activity row carries
    // =========================================================================

    [<Fact>]
    member _.``REQ-AC-3.12 an activity row for a journal entry line carries the account's code, name, type, subtype, parent code and external reference, and the line's ID, amount, line type, memo, created and modified instants, and its journal entry's ID, entry date, description, source and voided-at instant, each as stored`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let gCode, gId = t.g
                let line = t.j3 |> JournalEntryOrchestration.jeLines |> List.find (fun l -> l |> JournalEntryLine.accountId = gId)
                let header = t.j3 |> JournalEntryOrchestration.header
                let! rows = activity { noFilter with accountCode = Some gCode } None
                let row =
                    rows |> List.find (fun r -> r.activityDetail |> Option.exists (fun d -> d.lineId = (line |> JournalEntryLine.journalEntryLineId |> JournalEntryLineId.value)))
                let detail = row.activityDetail.Value
                Assert.Equal<string>(gCode, row.accountCode)
                Assert.Equal<string>($"Account test {gCode}", row.accountName)
                Assert.Equal<string>("Asset", row.accountType)
                Assert.Equal(Some "Investment", row.accountSubtype)
                Assert.Equal(Some(fst t.c), row.accountParentCode)
                Assert.Equal(Some "ref-g", row.accountExternalRef)
                Assert.Equal(25.00M, detail.amount)
                Assert.Equal<string>("Debit", detail.lineType)
                Assert.Equal(Some "g memo", detail.lineMemo)
                Assert.Equal(line |> JournalEntryLine.createdAt, detail.lineCreatedAt)
                Assert.Equal(line |> JournalEntryLine.modifiedAt, detail.lineModifiedAt)
                Assert.Equal(header |> JournalEntryHeader.journalEntryHeaderId |> JournalEntryHeaderId.value, detail.journalEntryId)
                Assert.Equal(t.dateA, detail.entryDate)
                Assert.Equal<string>($"Transfer {t.tag}", detail.journalEntryDescription)
                Assert.Equal(Some $"Void{t.tag}", detail.journalEntrySource)
                Assert.True((header |> JournalEntryHeader.voidedAt).IsSome)
                Assert.Equal(header |> JournalEntryHeader.voidedAt, detail.journalEntryVoidedAt)
            })

    [<Fact>]
    member _.``REQ-AC-3.12 an Account with several journal entry lines yields exactly one activity row per line and no rows for other Accounts' lines`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let pCode, pId = t.p
                let pLines = [ t.j1; t.j3; t.j4 ] |> List.collect (fun e -> lineIdsOf e pId)
                let! rows = activity noFilter None
                let pRowLines = rows |> List.filter (fun r -> r.accountCode = pCode) |> List.map (fun r -> r.activityDetail |> Option.map _.lineId)
                let elsewhere =
                    rows |> List.filter (fun r -> r.accountCode <> pCode && r.activityDetail |> Option.exists (fun d -> pLines |> List.contains d.lineId))
                Assert.Equal<Guid option list>(pLines |> List.map Some |> List.sort, pRowLines |> List.sort)
                Assert.Empty(elsewhere)
            })

    // =========================================================================
    // REQ-AC-3.12.1 — activity filters
    // =========================================================================

    [<Theory>]
    [<InlineData("account code")>]
    [<InlineData("fiscal period key")>]
    [<InlineData("entry date range")>]
    [<InlineData("source")>]
    [<InlineData("account type")>]
    [<InlineData("account subtype")>]
    [<InlineData("parent account code")>]
    [<InlineData("journal entry ID")>]
    [<InlineData("line amount")>]
    [<InlineData("description")>]
    [<InlineData("unvoided-only")>]
    member _.``REQ-AC-3.12.1 for each activity filter (account code, fiscal period key, entry date range, source, account type, account subtype, parent account code, journal entry ID, line amount, description, unvoided-only), a query with only that filter, over rows that include some that fail it, returns exactly the rows that satisfy it`` (filterName: string) =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let period = ledger.current
                let j1Id = entryIdOf t.j1 |> JournalEntryHeaderId.value
                let onLine (test: Contracts.AccountActivityDetailReturn -> bool) (r: Contracts.AccountActivityReturn) =
                    r.activityDetail |> Option.exists test
                let filter, satisfies =
                    match filterName with
                    | "account code" -> { noFilter with accountCode = Some(fst t.p) }, (fun (r: Contracts.AccountActivityReturn) -> r.accountCode = fst t.p)
                    | "fiscal period key" ->
                        { noFilter with temporalFilter = Some(Shared.PeriodKey period.key) },
                        onLine (fun d -> period.startDate <= d.entryDate && d.entryDate <= period.endDate)
                    | "entry date range" ->
                        { noFilter with temporalFilter = Some(Shared.DateRange { beginDate = t.dateA; endInclusive = t.dateA }) },
                        onLine (fun d -> d.entryDate = t.dateA)
                    | "source" -> { noFilter with source = Some $"Src{t.tag}" }, onLine (fun d -> d.journalEntrySource = Some $"Src{t.tag}")
                    | "account type" -> { noFilter with accountType = Some "Liability" }, (fun r -> r.accountType = "Liability")
                    | "account subtype" -> { noFilter with accountSubtype = Some "FixedAsset" }, (fun r -> r.accountSubtype = Some "FixedAsset")
                    | "parent account code" -> { noFilter with accountParentCode = Some(fst t.p) }, (fun r -> r.accountParentCode = Some(fst t.p))
                    | "journal entry ID" -> { noFilter with journalEntryId = Some j1Id }, onLine (fun d -> d.journalEntryId = j1Id)
                    | "line amount" -> { noFilter with amount = Some 40.00M }, onLine (fun d -> d.amount = 40.00M)
                    | "description" ->
                        { noFilter with description = Some $"paid {t.tag}" },
                        onLine (fun d -> d.journalEntryDescription.Contains($"paid {t.tag}", StringComparison.Ordinal))
                    | _ -> { noFilter with unVoidedOnly = true }, (fun r -> r.activityDetail |> Option.forall (fun d -> d.journalEntryVoidedAt.IsNone))
                let! everything = activity noFilter None
                let! filtered = activity filter None
                let expected = everything |> List.filter satisfies |> keysOf
                Assert.NotEmpty(expected)
                Assert.True(everything |> List.exists (satisfies >> not))
                Assert.Equal<Set<string * Guid option>>(expected, filtered |> keysOf)
                Assert.Equal(filtered.Length, filtered |> keysOf |> Set.count)
            })

    [<Fact>]
    member _.``REQ-AC-3.12.1 an entry date range filter returns lines dated on its first and last days and excludes lines dated the day before and the day after`` () =
        withLedger (fun ledger ->
            result {
                let! code, id = ledger.account "Asset" None None None
                let! counter, counterId = ledger.account "Liability" None None None
                let first = ledger.other.startDate.PlusDays(1)
                let last = first.PlusDays(2)
                let! entries =
                    [ first.PlusDays(-1); first; last; last.PlusDays(1) ]
                    |> List.map (fun d -> ledger.entry d None "Date range test" [ (id, 10.00M, "Debit", None); (counterId, 10.00M, "Credit", None) ])
                    |> convertListOfResultsToResultsList
                let! rows =
                    activity { noFilter with accountCode = Some code; temporalFilter = Some(Shared.DateRange { beginDate = first; endInclusive = last }) } None
                let returned = rows |> List.choose detailOf |> List.map _.journalEntryId |> Set.ofList
                Assert.Equal<Set<Guid>>(set [ entries[1] |> entryIdOf |> JournalEntryHeaderId.value; entries[2] |> entryIdOf |> JournalEntryHeaderId.value ], returned)
            })

    [<Fact>]
    member _.``REQ-AC-3.12.1 a description filter matches a case-sensitive substring: it returns a line whose description contains the text and not one that contains it in another case`` () =
        withLedger (fun ledger ->
            result {
                let! code, id = ledger.account "Asset" None None None
                let! _, counterId = ledger.account "Liability" None None None
                let tag = Guid.NewGuid().ToString("N").Substring(0, 8).ToLowerInvariant()
                let date = ledger.current.startDate
                let lines = [ (id, 10.00M, "Debit", None); (counterId, 10.00M, "Credit", None) ]
                let! lower = ledger.entry date None $"Monthly rent {tag} due" lines
                let! _ = ledger.entry date None $"MONTHLY RENT {tag.ToUpperInvariant()} DUE" lines
                let! rows = activity { noFilter with accountCode = Some code; description = Some $"rent {tag}" } None
                let returned = rows |> List.choose detailOf |> List.map _.journalEntryId
                Assert.Equal<Guid list>([ lower |> entryIdOf |> JournalEntryHeaderId.value ], returned)
            })

    [<Fact>]
    member _.``REQ-AC-3.12.1 a source filter returns lines whose source equals it and not lines whose source merely contains it or differs in case`` () =
        withLedger (fun ledger ->
            result {
                let! code, id = ledger.account "Asset" None None None
                let! _, counterId = ledger.account "Liability" None None None
                let source = "Src" + Guid.NewGuid().ToString("N").Substring(0, 8).ToLowerInvariant()
                let date = ledger.current.startDate
                let lines = [ (id, 10.00M, "Debit", None); (counterId, 10.00M, "Credit", None) ]
                let! exact = ledger.entry date (Some source) "Source test" lines
                let! _ = ledger.entry date (Some(source + " extra")) "Source test" lines
                let! _ = ledger.entry date (Some(source.ToUpperInvariant())) "Source test" lines
                let! rows = activity { noFilter with accountCode = Some code; source = Some source } None
                let returned = rows |> List.choose detailOf |> List.map _.journalEntryId
                Assert.Equal<Guid list>([ exact |> entryIdOf |> JournalEntryHeaderId.value ], returned)
            })

    [<Fact>]
    member _.``REQ-AC-3.12.1 filters given together return exactly the rows that satisfy all of them, excluding rows that satisfy all but one`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let pCode = fst t.p
                let onAccount (r: Contracts.AccountActivityReturn) = r.accountCode = pCode
                let onDate (r: Contracts.AccountActivityReturn) = r.activityDetail |> Option.exists (fun d -> d.entryDate = t.dateA)
                let! everything = activity noFilter None
                let! filtered =
                    activity { noFilter with accountCode = Some pCode; temporalFilter = Some(Shared.DateRange { beginDate = t.dateA; endInclusive = t.dateA }) } None
                let expected = everything |> List.filter (fun r -> onAccount r && onDate r) |> keysOf
                Assert.Equal(2, expected.Count)
                Assert.True(everything |> List.exists (fun r -> onAccount r && not (onDate r)))
                Assert.True(everything |> List.exists (fun r -> onDate r && not (onAccount r)))
                Assert.Equal<Set<string * Guid option>>(expected, filtered |> keysOf)
            })

    // =========================================================================
    // REQ-AC-3.12.2 — voided lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-AC-3.12.2 without the unvoided-only flag, a line of a voided journal entry is returned carrying its voided-at instant`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let voidedLines = lineIdsOf t.j3 (snd t.g) @ lineIdsOf t.j3 (snd t.p)
                let! rows = activity noFilter None
                let returned = rows |> List.choose detailOf |> List.filter (fun d -> voidedLines |> List.contains d.lineId)
                Assert.Equal(2, returned.Length)
                Assert.All(returned, fun d -> Assert.Equal(t.j3 |> JournalEntryOrchestration.header |> JournalEntryHeader.voidedAt, d.journalEntryVoidedAt))
                Assert.All(returned, fun d -> Assert.True(d.journalEntryVoidedAt.IsSome))
            })

    [<Fact>]
    member _.``REQ-AC-3.12.2 with the unvoided-only flag set, lines of voided journal entries are omitted and lines of unvoided entries are still returned`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let voidedLines = lineIdsOf t.j3 (snd t.g) @ lineIdsOf t.j3 (snd t.p)
                let unvoidedLines = lineIdsOf t.j1 (snd t.p) @ lineIdsOf t.j5 (snd t.g)
                let! rows = activity { noFilter with unVoidedOnly = true } None
                let returned = rows |> List.choose detailOf |> List.map _.lineId |> Set.ofList
                Assert.Empty(Set.intersect returned (Set.ofList voidedLines))
                Assert.True(Set.isSubset (Set.ofList unvoidedLines) returned)
            })

    // =========================================================================
    // REQ-AC-3.12.3 — accounts with no lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-AC-3.12.3 an Account with no journal entry lines is returned exactly once, carrying its account fields with no line detail, both with no filters and with only account-level filters or the unvoided-only flag applied`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let nCode = fst t.n
                let accountLevel =
                    { noFilter with accountCode = Some nCode; accountType = Some "Asset"; accountSubtype = Some "FixedAsset"; accountParentCode = Some(fst t.p) }
                let! queries =
                    [ noFilter; accountLevel; { noFilter with unVoidedOnly = true } ]
                    |> List.map (fun f -> activity f None)
                    |> convertListOfResultsToResultsList
                queries |> List.iter (fun rows ->
                    let row = rows |> List.filter (fun r -> r.accountCode = nCode) |> List.exactlyOne
                    Assert.Equal(
                        ({ accountCode = nCode
                           accountName = $"Account test {nCode}"
                           accountType = "Asset"
                           accountSubtype = Some "FixedAsset"
                           accountParentCode = Some(fst t.p)
                           accountExternalRef = Some "ref-n"
                           activityDetail = None } : Contracts.AccountActivityReturn),
                        row))
            })

    [<Theory>]
    [<InlineData("fiscal period")>]
    [<InlineData("date range")>]
    [<InlineData("source")>]
    [<InlineData("journal entry ID")>]
    [<InlineData("amount")>]
    [<InlineData("description")>]
    member _.``REQ-AC-3.12.3 for each filter on journal entry properties (fiscal period, date range, source, journal entry ID, amount, description), an Account with no lines is not returned when that filter is applied`` (filterName: string) =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let filter =
                    match filterName with
                    | "fiscal period" -> { noFilter with temporalFilter = Some(Shared.PeriodKey ledger.current.key) }
                    | "date range" -> { noFilter with temporalFilter = Some(Shared.DateRange { beginDate = min t.dateA t.dateB; endInclusive = max t.dateA t.dateB }) }
                    | "source" -> { noFilter with source = Some $"Src{t.tag}" }
                    | "journal entry ID" -> { noFilter with journalEntryId = Some(entryIdOf t.j1 |> JournalEntryHeaderId.value) }
                    | "amount" -> { noFilter with amount = Some 100.00M }
                    | _ -> { noFilter with description = Some t.tag }
                let! rows = activity filter None
                Assert.NotEmpty(rows)
                Assert.DoesNotContain(rows, fun r -> r.accountCode = fst t.n)
                Assert.DoesNotContain(rows, fun r -> r.activityDetail.IsNone)
            })

    // =========================================================================
    // REQ-AC-3.12.4 — sorting
    // =========================================================================

    [<Theory>]
    [<InlineData("AccountCodeAsc")>]
    [<InlineData("AccountCodeDesc")>]
    [<InlineData("EntryDateAsc")>]
    [<InlineData("EntryDateDesc")>]
    [<InlineData("AmountAsc")>]
    [<InlineData("AmountDesc")>]
    member _.``REQ-AC-3.12.4 for each of account code, entry date and amount, ascending and descending, activity rows stored in an order matching neither direction come back in that sort order`` (sortName: string) =
        withLedger (fun ledger ->
            result {
                let! middle, middleId = ledger.account "Asset" None None None
                let! low, lowId = ledger.account "Asset" None None None
                let! high, highId = ledger.account "Asset" None None None
                let tag = Guid.NewGuid().ToString("N").Substring(0, 8)
                let start = ledger.other.startDate
                (* Entries go in with dates 2nd, 1st, 3rd and amounts 50, 70, 30, so neither the dates nor the amounts
                   are stored in either order. *)
                let! _ = ledger.entry (start.PlusDays(2)) None $"Sort {tag}" [ (middleId, 50.00M, "Debit", None); (lowId, 50.00M, "Credit", None) ]
                let! _ = ledger.entry (start.PlusDays(1)) None $"Sort {tag}" [ (highId, 70.00M, "Debit", None); (middleId, 70.00M, "Credit", None) ]
                let! _ = ledger.entry (start.PlusDays(3)) None $"Sort {tag}" [ (lowId, 30.00M, "Debit", None); (highId, 30.00M, "Credit", None) ]
                let sort, key, descending =
                    match sortName with
                    | "AccountCodeAsc" -> FetchSort.AccountCodeAsc, (fun (r: Contracts.AccountActivityReturn) -> r.accountCode :> IComparable), false
                    | "AccountCodeDesc" -> FetchSort.AccountCodeDesc, (fun r -> r.accountCode :> IComparable), true
                    | "EntryDateAsc" -> FetchSort.EntryDateAsc, (fun r -> r.activityDetail.Value.entryDate :> IComparable), false
                    | "EntryDateDesc" -> FetchSort.EntryDateDesc, (fun r -> r.activityDetail.Value.entryDate :> IComparable), true
                    | "AmountAsc" -> FetchSort.AmountAsc, (fun r -> r.activityDetail.Value.amount :> IComparable), false
                    | _ -> FetchSort.AmountDesc, (fun r -> r.activityDetail.Value.amount :> IComparable), true
                let! rows = activity { noFilter with description = Some $"Sort {tag}" } (Some sort)
                let keys = rows |> List.map key
                let inOrder =
                    keys |> List.pairwise |> List.forall (fun (a, b) -> if descending then a.CompareTo(b) >= 0 else a.CompareTo(b) <= 0)
                Assert.Equal(6, rows.Length)
                Assert.Equal<Set<string>>(set [ middle; low; high ], rows |> List.map _.accountCode |> Set.ofList)
                Assert.True(inOrder, String.Join(", ", keys))
            })

    // =========================================================================
    // REQ-AC-3.13 — balances
    // =========================================================================

    [<Fact>]
    member _.``REQ-AC-3.13 a balance query for several account codes returns exactly one result per code given`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let codes = [ fst t.p; fst t.l; fst t.n ]
                let! results = balances codes None
                Assert.Equal<string list>(codes |> List.sort, results |> List.map _.accountCode |> List.sort)
            })

    [<Fact>]
    member _.``REQ-AC-3.13 a balance query for a debit-normal Account returns its code, name, total debits and total credits equal to the sums of its debit and credit lines, and a net balance of debits minus credits`` () =
        withLedger (fun ledger ->
            result {
                let! asset, assetId = ledger.account "Asset" None None None
                let! _, liabilityId = ledger.account "Liability" None None None
                let date = ledger.current.startDate
                let! _ = ledger.entry date None "Balance test" [ (assetId, 100.00M, "Debit", None); (liabilityId, 100.00M, "Credit", None) ]
                let! _ = ledger.entry date None "Balance test" [ (liabilityId, 35.00M, "Debit", None); (assetId, 35.00M, "Credit", None) ]
                let! balance = balanceOf asset None
                Assert.Equal(
                    ({ accountCode = asset; accountName = $"Account test {asset}"; totalDebits = 100.00M; totalCredits = 35.00M; netBalance = 65.00M }
                     : Contracts.AccountBalanceReturn),
                    balance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13 a balance query for a credit-normal Account returns its code, name, total debits and total credits equal to the sums of its debit and credit lines, and a net balance of credits minus debits`` () =
        withLedger (fun ledger ->
            result {
                let! _, assetId = ledger.account "Asset" None None None
                let! liability, liabilityId = ledger.account "Liability" None None None
                let date = ledger.current.startDate
                let! _ = ledger.entry date None "Balance test" [ (assetId, 100.00M, "Debit", None); (liabilityId, 100.00M, "Credit", None) ]
                let! _ = ledger.entry date None "Balance test" [ (liabilityId, 35.00M, "Debit", None); (assetId, 35.00M, "Credit", None) ]
                let! balance = balanceOf liability None
                Assert.Equal(
                    ({ accountCode = liability; accountName = $"Account test {liability}"; totalDebits = 35.00M; totalCredits = 100.00M; netBalance = 65.00M }
                     : Contracts.AccountBalanceReturn),
                    balance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13.1 an Account's totals and balance equal the sums of its unvoided lines alone when it also has lines of voided journal entries`` () =
        withLedger (fun ledger ->
            result {
                let! asset, assetId = ledger.account "Asset" None None None
                let! _, liabilityId = ledger.account "Liability" None None None
                let date = ledger.current.startDate
                let! _ = ledger.entry date None "Balance test" [ (assetId, 100.00M, "Debit", None); (liabilityId, 100.00M, "Credit", None) ]
                let! _ = ledger.entry date None "Balance test" [ (liabilityId, 35.00M, "Debit", None); (assetId, 35.00M, "Credit", None) ]
                let! debitToVoid = ledger.entry date None "Balance test" [ (assetId, 1000.00M, "Debit", None); (liabilityId, 1000.00M, "Credit", None) ]
                let! creditToVoid = ledger.entry date None "Balance test" [ (liabilityId, 500.00M, "Debit", None); (assetId, 500.00M, "Credit", None) ]
                let! _ = ledger.voidEntry debitToVoid
                let! _ = ledger.voidEntry creditToVoid
                let! balance = balanceOf asset None
                Assert.Equal(100.00M, balance.totalDebits)
                Assert.Equal(35.00M, balance.totalCredits)
                Assert.Equal(65.00M, balance.netBalance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13.1 with an as-of date, a line dated on that date counts and a line dated the day after does not`` () =
        withLedger (fun ledger ->
            result {
                let! asset, assetId = ledger.account "Asset" None None None
                let! _, liabilityId = ledger.account "Liability" None None None
                let asOf = ledger.other.startDate.PlusDays(1)
                let! _ = ledger.entry asOf None "As-of test" [ (assetId, 10.00M, "Debit", None); (liabilityId, 10.00M, "Credit", None) ]
                let! _ = ledger.entry (asOf.PlusDays(1)) None "As-of test" [ (assetId, 20.00M, "Debit", None); (liabilityId, 20.00M, "Credit", None) ]
                let! balance = balanceOf asset (Some asOf)
                Assert.Equal(10.00M, balance.totalDebits)
                Assert.Equal(10.00M, balance.netBalance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13.1 without an as-of date, a balance counts every unvoided line whatever its entry date`` () =
        withLedger (fun ledger ->
            result {
                let! asset, assetId = ledger.account "Asset" None None None
                let! _, liabilityId = ledger.account "Liability" None None None
                let dates = [ ledger.other.startDate; ledger.current.startDate; ledger.latest.endDate ]
                let! _ =
                    dates
                    |> List.map (fun d -> ledger.entry d None "No as-of test" [ (assetId, 10.00M, "Debit", None); (liabilityId, 10.00M, "Credit", None) ])
                    |> convertListOfResultsToResultsList
                let! balance = balanceOf asset None
                Assert.True(ledger.latest.endDate > Calendar.today ())
                Assert.Equal(30.00M, balance.totalDebits)
                Assert.Equal(30.00M, balance.netBalance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13.1 an Account whose only lines are voided or dated after the as-of date has zero total debits, zero total credits and a zero balance`` () =
        withLedger (fun ledger ->
            result {
                let! asset, assetId = ledger.account "Asset" None None None
                let! _, liabilityId = ledger.account "Liability" None None None
                let asOf = ledger.other.startDate.PlusDays(1)
                let! toVoid = ledger.entry asOf None "Zero test" [ (assetId, 10.00M, "Debit", None); (liabilityId, 10.00M, "Credit", None) ]
                let! _ = ledger.entry (asOf.PlusDays(1)) None "Zero test" [ (liabilityId, 20.00M, "Debit", None); (assetId, 20.00M, "Credit", None) ]
                let! _ = ledger.voidEntry toVoid
                let! balance = balanceOf asset (Some asOf)
                Assert.Equal(
                    ({ accountCode = asset; accountName = $"Account test {asset}"; totalDebits = 0.00M; totalCredits = 0.00M; netBalance = 0.00M }
                     : Contracts.AccountBalanceReturn),
                    balance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13.2 a parent Account's balance counts only its own lines, not those of its children or deeper descendants`` () =
        withLedger (fun ledger ->
            result {
                let! t = tree ledger
                let! parent = balanceOf (fst t.p) None
                let! child = balanceOf (fst t.c) None
                let! grandchild = balanceOf (fst t.g) None
                Assert.Equal(110.00M, parent.totalDebits)
                Assert.Equal(0.00M, parent.totalCredits)
                Assert.Equal(110.00M, parent.netBalance)
                Assert.Equal(40.00M, child.netBalance)
                Assert.Equal(7.00M, grandchild.netBalance)
            })

    [<Fact>]
    member _.``REQ-AC-3.13.3 a balance query with an empty list of account codes fails with a typed error`` () =
        let attempt = balances [] None
        Assert.True(attempt |> Result.isError)
