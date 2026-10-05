module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsLedger

open System
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.General
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

(* Plan item 30: the clauses of revised Accounts and Journal Entry requirements that no earlier test reached.
   The account test runs in a transaction that rolls back. The journal entry tests go through the JournalEntry routes,
   which commit: each posts its own entries with a fresh tag in the description (and in its external references),
   and a finally deletes them, newest first, then any account the test made. *)

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private send (verb: string) (payload: 'a) = payload |> Json.toJson |> Result.bind (routeUiCommandForTesting "JournalEntry" verb [])

let private entryInput (description: string) (debitCode: string) (references: (string * string) list) (comments: JournalEntryCommentInput list) : JournalEntryInput =
    { header = { description = description; source = Some "Revised requirements test"; entryDate = Calendar.today () }
      lines =
        [ { accountCode = debitCode; amount = 25.00M; lineType = "Debit"; memo = None }
          { accountCode = "F-1280"; amount = 25.00M; lineType = "Credit"; memo = None } ]
      externalReferences = references |> List.map (fun (fi, text) -> { financialInstitution = fi; referenceText = text })
      comments = comments }

let private comment (secondary: Guid option) (text: string) : JournalEntryCommentInput =
    { secondaryJournalEntryId = secondary; commentText = text }

let private fetchById (id: Guid) =
    ({ id = id } : JournalEntryFetchByIdInput) |> send "FetchById" |> Result.bind Json.fromJson<JournalEntryReturn>

let private fetchByReference (fi: string option) (reference: string option) =
    ({ fi = fi; reference = reference } : JournalEntryFetchByExternalReferenceInput)
    |> send "FetchByExternalReference"
    |> Result.bind Json.fromJson<JournalEntryReturn list>

let private describedToday (description: string) =
    let today = Calendar.today ()
    ({ beginDate = today; endDateInclusive = today } : JournalEntryFetchByDateRangeInput)
    |> send "FetchByDateRange"
    |> Result.bind Json.fromJson<JournalEntryReturn list>
    |> orFail
    |> List.filter (fun e -> e.header.description = description)

let private voidWith (id: Guid) (reason: JournalEntryCommentInput) =
    ({ id = id; reason = reason } : JournalEntryVoidInput) |> send "Void" |> Result.bind Json.fromJson<JournalEntryReturn>

/// Runs the test with a function that posts an entry and records it for deletion, and a function that makes a fresh
/// committed asset account and records it. Entries go newest first (a comment naming an older entry as its secondary
/// goes before that entry does), then the accounts.
let private withEntries (test: (JournalEntryInput -> Result<JournalEntryReturn, IAppError>) -> (unit -> string) -> unit) =
    let made = ResizeArray<Guid>()
    let accounts = ResizeArray<AccountId>()
    let post (input: JournalEntryInput) =
        input |> send "PostNew" |> Result.bind Json.fromJson<JournalEntryReturn>
        |> Result.map (fun e -> made.Add e.header.id; e)
    let newAccount () =
        let code = "Z" + Guid.NewGuid().ToString("N").Substring(0, 7).ToUpperInvariant()
        runCommandRouteAndAutoCompleteTransaction AccountCreate (fun context ->
            createTestAccountFromPrimitives context code $"Revised requirements {code}" "Asset" ((Calendar.today ()).PlusYears(-1))
                None None None None)
        |> Result.map (fun (_, id) -> accounts.Add id)
        |> orFail
        code
    try
        test post newAccount
    finally
        [ for id in made |> Seq.rev -> id |> JournalEntryHeaderId.fromGuid |> Some |> Cleanup.cleanUpJournalEntryId
          for id in accounts |> Seq.rev -> Cleanup.cleanUpAccountId (Some id) ]
        |> List.iter orFail

let private ids (entries: JournalEntryReturn list) = entries |> List.map (fun e -> e.header.id) |> Set.ofList

[<Collection("SharedTestData")>]
type RevisedRequirementsLedgerTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-AC-4.1 — the default active end
    // =========================================================================

    [<Fact>]
    member _.``REQ-AC-4.1 deactivating an account with no active end supplied sets its active end to the Eastern calendar date of the audit envelope's system instant`` () =
        runCommandRouteAndAutoRollback AccountDeactivate (fun context ->
            result {
                let code = "Z" + Guid.NewGuid().ToString("N").Substring(0, 7).ToUpperInvariant()
                let! account, _ =
                    createTestAccountFromPrimitives context code $"Default end {code}" "Asset" ((Calendar.today ()).PlusYears(-1))
                        None None None None
                let! deactivated = account |> AccountDeactivation.deactivateAccount context None
                let eastern = DateTimeZoneProviders.Tzdb.["America/New_York"]
                let expected = (context |> Context.getInitiationInstant).InZone(eastern).Date
                Assert.Equal(Some expected, deactivated |> Account.activityPeriod |> ActivityPeriod.activeEnd)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-JE-2.11 — comments are saved atomically with the entry
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-2.11 REQ-JE-2.12 REQ-JE-2.15 posting a journal entry whose last comment names a secondary journal entry that doesn't exist fails and stores none of its header, lines, external references or earlier comments`` () =
        withEntries (fun post _ ->
            let tag = newTag ()
            let missing = Guid.NewGuid()
            let attempt =
                post (entryInput $"Half posted {tag}" "F-2230" [ ($"FI {tag}", $"ref {tag}") ]
                          [ comment None $"first {tag}"; comment None $"second {tag}"; comment (Some missing) $"last {tag}" ])
            let refused =
                match attempt with
                | Error (AsError (JournalEntryCommentSecondaryJeHeaderIdNotFound id)) -> id = missing
                | _ -> false
            Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
            Assert.Empty(describedToday $"Half posted {tag}")
            Assert.Empty(fetchByReference (Some $"FI {tag}") None |> orFail))

    [<Fact>]
    member _.``REQ-JE-2.11 posting a journal entry with comments stores every comment and returns each with its generated ID and timestamp`` () =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let! posted = post (entryInput $"Commented {tag}" "F-2230" [] [ comment None $"one {tag}"; comment None $"two {tag}" ])
                let! stored = fetchById posted.header.id
                Assert.Equal(2, posted.comments.Length)
                Assert.All(posted.comments, fun c ->
                    Assert.NotEqual(Guid.Empty, c.id)
                    Assert.Equal(posted.header.createdAt, c.createdAt)
                    Assert.Equal(posted.header.createdAt, c.modifiedAt))
                let shape (e: JournalEntryReturn) = e.comments |> List.map (fun c -> c.id, c.commentText, c.createdAt) |> Set.ofList
                Assert.Equal<Set<Guid * string * Instant>>(shape posted, shape stored)
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-JE-3.1 — only the entry's own comments come back
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-3.1 a fetched journal entry returns every comment whose primary is that entry and not a comment on which it is only the secondary`` () =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let! first = post (entryInput $"Primary {tag}" "F-2230" [] [ comment None $"own one {tag}"; comment None $"own two {tag}" ])
                let! _ = post (entryInput $"Pointing {tag}" "F-2230" [] [ comment (Some first.header.id) $"names first {tag}" ])
                let! stored = fetchById first.header.id
                Assert.Equal<Set<string>>(set [ $"own one {tag}"; $"own two {tag}" ], stored.comments |> List.map (fun c -> c.commentText) |> Set.ofList)
                Assert.All(stored.comments, fun c -> Assert.Equal(first.header.id, c.primaryJournalEntryId))
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-JE-3.4 — lines by account, with and without the non-voided restriction
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-3.4 fetching an account's lines with the non-voided restriction returns every line of its non-voided entries and no line of its voided entries`` () =
        withEntries (fun post newAccount ->
            result {
                let tag = newTag ()
                let code = newAccount ()
                let! live = post (entryInput $"Live {tag}" code [] [])
                let! voided = post (entryInput $"Voided {tag}" code [] [])
                let! _ = voidWith voided.header.id (comment None $"void {tag}")
                let lineOn (e: JournalEntryReturn) = e.lines |> List.filter (fun l -> l.accountCode = code) |> List.map (fun l -> l.id)
                let! lines =
                    ({ accountCode = code; nonVoidedOnly = true } : JournalEntryFetchLinesByAccountInput)
                    |> send "FetchLinesByAccount"
                    |> Result.bind Json.fromJson<JournalEntryLineReturn list>
                Assert.Equal<Set<Guid>>(Set.ofList (lineOn live), lines |> List.map (fun l -> l.id) |> Set.ofList)
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-JE-3.4 fetching an account's lines without the non-voided restriction returns every line of both its voided and non-voided entries`` () =
        withEntries (fun post newAccount ->
            result {
                let tag = newTag ()
                let code = newAccount ()
                let! live = post (entryInput $"Live {tag}" code [] [])
                let! voided = post (entryInput $"Voided {tag}" code [] [])
                let! _ = voidWith voided.header.id (comment None $"void {tag}")
                let lineOn (e: JournalEntryReturn) = e.lines |> List.filter (fun l -> l.accountCode = code) |> List.map (fun l -> l.id)
                let! lines =
                    ({ accountCode = code; nonVoidedOnly = false } : JournalEntryFetchLinesByAccountInput)
                    |> send "FetchLinesByAccount"
                    |> Result.bind Json.fromJson<JournalEntryLineReturn list>
                Assert.Equal<Set<Guid>>(Set.ofList (lineOn live @ lineOn voided), lines |> List.map (fun l -> l.id) |> Set.ofList)
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-JE-3.5 — lookup by FI, by reference, or by both on one reference
    // =========================================================================

    [<Theory>]
    [<InlineData("source FI")>]
    [<InlineData("reference value")>]
    member _.``REQ-JE-3.5 for each of a source FI alone and a reference value alone, a lookup by external reference returns every entry carrying a reference that matches the value given and no other entry`` (given: string) =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let fi, otherFi, reference, otherReference = $"FI {tag}", $"Other FI {tag}", $"ref {tag}", $"other ref {tag}"
                let! both = post (entryInput $"Both {tag}" "F-2230" [ (fi, reference) ] [])
                let! fiOnly = post (entryInput $"FI only {tag}" "F-2230" [ (fi, otherReference) ] [])
                let! referenceOnly = post (entryInput $"Reference only {tag}" "F-2230" [ (otherFi, reference) ] [])
                let! found, expected =
                    match given with
                    | "source FI" -> fetchByReference (Some fi) None |> Result.map (fun f -> f, [ both; fiOnly ])
                    | _ -> fetchByReference None (Some reference) |> Result.map (fun f -> f, [ both; referenceOnly ])
                Assert.Equal<Set<Guid>>(ids expected, ids found)
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-JE-3.5 a lookup given a source FI and a reference value returns an entry whose single external reference matches both, and not an entry that matches the FI on one reference and the value on another`` () =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let fi, reference = $"FI {tag}", $"ref {tag}"
                let! matching = post (entryInput $"One reference {tag}" "F-2230" [ (fi, reference) ] [])
                let! _ = post (entryInput $"Split {tag}" "F-2230" [ (fi, $"other ref {tag}"); ($"Other FI {tag}", reference) ] [])
                let! found = fetchByReference (Some fi) (Some reference)
                Assert.Equal<Set<Guid>>(set [ matching.header.id ], ids found)
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-JE-4.4 — the void reason, its secondary, and atomicity
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-4.4 voiding a journal entry with a reason that names a secondary journal entry stores the reason as a comment whose primary is the voided entry and whose secondary is the one named`` () =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let! wrong = post (entryInput $"Wrong {tag}" "F-2230" [] [])
                let! replacement = post (entryInput $"Replacement {tag}" "F-2230" [] [])
                let! _ = voidWith wrong.header.id (comment (Some replacement.header.id) $"replaced {tag}")
                let! stored = fetchById wrong.header.id
                let reason = stored.comments |> List.filter (fun c -> c.commentText = $"replaced {tag}") |> Assert.Single
                Assert.Equal(wrong.header.id, reason.primaryJournalEntryId)
                Assert.Equal(Some replacement.header.id, reason.secondaryJournalEntryId)
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-JE-4.4 voiding a journal entry with a reason that names a secondary journal entry that doesn't exist fails with the secondary-not-found error, the entry is not voided, and no reason comment is stored`` () =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let missing = Guid.NewGuid()
                let! entry = post (entryInput $"Kept {tag}" "F-2230" [] [])
                let attempt = voidWith entry.header.id (comment (Some missing) $"dangling {tag}")
                let refused =
                    match attempt with
                    | Error (AsError (JournalEntryCommentSecondaryJeHeaderIdNotFound id)) -> id = missing
                    | _ -> false
                Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                let! stored = fetchById entry.header.id
                Assert.Equal(None, stored.header.voidedAt)
                Assert.Empty(stored.comments)
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-JE-4.4 voiding an already-voided journal entry with a valid reason fails and stores no reason comment`` () =
        withEntries (fun post _ ->
            result {
                let tag = newTag ()
                let! entry = post (entryInput $"Voided twice {tag}" "F-2230" [] [])
                let! _ = voidWith entry.header.id (comment None $"first void {tag}")
                let attempt = voidWith entry.header.id (comment None $"second void {tag}")
                let refused =
                    match attempt with
                    | Error (AsError (JournalEntryVoidingNoOp id)) -> id = entry.header.id
                    | _ -> false
                Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                let! stored = fetchById entry.header.id
                Assert.Equal<string list>([ $"first void {tag}" ], stored.comments |> List.map (fun c -> c.commentText))
            }
            |> railroadWrapper)
