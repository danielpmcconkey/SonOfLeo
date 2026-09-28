module Tests.Integrated.CrossDomainOrchestration.JournalEntryCommentsAndReads

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

(* Every test goes through the JournalEntry routes, which commit. Each posts its own entries with a fresh tag in the
   description, and a finally deletes them (their comments, references and lines go with them). "Nothing is stored"
   is checked by looking for the tagged description on the entry date: an entry's lines, references and comments
   can't exist without its header. *)

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private send (verb: string) (payload: 'a) = payload |> Json.toJson |> Result.bind (routeUiCommandForTesting "JournalEntry" verb [])

let private entryInput (description: string) (date: LocalDate) (reference: (string * string) option) (comments: JournalEntryCommentInput list) : JournalEntryInput =
    { header = { description = description; source = Some "Comments and reads test"; entryDate = date }
      lines =
        [ { accountCode = "F-2230"; amount = 25.00M; lineType = "Debit"; memo = None }
          { accountCode = "F-1280"; amount = 25.00M; lineType = "Credit"; memo = Some "credit leg" } ]
      externalReferences =
        reference |> Option.map (fun (fi, text) -> { financialInstitution = fi; referenceText = text }) |> Option.toList
      comments = comments }

let private comment (secondary: Guid option) (text: string) : JournalEntryCommentInput =
    { secondaryJournalEntryId = secondary; commentText = text }

let private fetchByDateRange (beginDate: LocalDate) (endDate: LocalDate) =
    ({ beginDate = beginDate; endDateInclusive = endDate } : JournalEntryFetchByDateRangeInput)
    |> send "FetchByDateRange"
    |> Result.bind Json.fromJson<JournalEntryReturn list>

let private describedOn (date: LocalDate) (description: string) =
    fetchByDateRange date date |> orFail |> List.filter (fun e -> e.header.description = description)

[<Collection("SharedTestData")>]
type JournalEntryCommentsAndReadsTests(fixture: TestDataFixture) =

    let today = Calendar.today ()

    (* Runs the test with a function that posts an entry and records it for deletion. Entries are deleted newest
       first, so a comment naming an older entry as its secondary goes before that entry does. *)
    let withEntries (test: (JournalEntryInput -> Result<JournalEntryReturn, IAppError>) -> unit) =
        let made = ResizeArray<Guid>()
        let post (input: JournalEntryInput) =
            input |> send "PostNew" |> Result.bind Json.fromJson<JournalEntryReturn>
            |> Result.map (fun e -> made.Add e.header.id; e)
        try
            test post
        finally
            made
            |> Seq.rev
            |> Seq.map (JournalEntryHeaderId.fromGuid >> Some >> Cleanup.cleanUpJournalEntryId)
            |> List.ofSeq
            |> List.iter orFail

    let voidEntry (id: Guid) =
        ({ id = id; reason = comment None "Comments and reads test void" } : JournalEntryVoidInput)
        |> send "Void"
        |> Result.bind Json.fromJson<JournalEntryReturn>

    // =========================================================================
    // REQ-JE-2.15 — comments supplied when posting
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-2.15 a journal entry posted with two comments, one naming an existing secondary journal entry, stores both with the new entry as their primary and the named secondary on the one that names it, and returns both on the entry`` () =
        withEntries (fun post ->
            result {
                let tag = newTag ()
                let! earlier = post (entryInput $"Earlier {tag}" today None [])
                let! posted =
                    post (entryInput $"Commented {tag}" today None
                              [ comment None $"plain {tag}"; comment (Some earlier.header.id) $"pointing {tag}" ])
                let! stored =
                    ({ id = posted.header.id } : JournalEntryFetchByIdInput) |> send "FetchById" |> Result.bind Json.fromJson<JournalEntryReturn>
                let shape (e: JournalEntryReturn) =
                    e.comments |> List.map (fun c -> c.commentText, c.primaryJournalEntryId, c.secondaryJournalEntryId) |> Set.ofList
                let expected =
                    set [ ($"plain {tag}", posted.header.id, None); ($"pointing {tag}", posted.header.id, Some earlier.header.id) ]
                Assert.Equal<Set<string * Guid * Guid option>>(expected, shape posted)
                Assert.Equal<Set<string * Guid * Guid option>>(expected, shape stored)
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-JE-2.15 a journal entry posted with no comments is stored and returned with no comments`` () =
        withEntries (fun post ->
            result {
                let tag = newTag ()
                let! posted = post (entryInput $"Uncommented {tag}" today None [])
                let! stored =
                    ({ id = posted.header.id } : JournalEntryFetchByIdInput) |> send "FetchById" |> Result.bind Json.fromJson<JournalEntryReturn>
                Assert.Empty(posted.comments)
                Assert.Equal<string>($"Uncommented {tag}", stored.header.description)
                Assert.Empty(stored.comments)
            }
            |> railroadWrapper)

    [<Theory>]
    [<InlineData("whitespace-only text")>]
    [<InlineData("text of 2001 characters")>]
    [<InlineData("a secondary ID that matches no journal entry")>]
    member _.``REQ-JE-2.15 for each invalid comment (whitespace-only text, text of 2001 characters, a secondary ID that matches no journal entry), posting the entry fails with the typed error for that fault (the not-found error naming the secondary for the last) and nothing is stored`` (fault: string) =
        withEntries (fun post ->
            let tag = newTag ()
            let missing = Guid.NewGuid()
            let bad =
                match fault with
                | "whitespace-only text" -> comment None "    "
                | "text of 2001 characters" -> comment None (String('c', 2001))
                | _ -> comment (Some missing) $"dangling {tag}"
            let attempt = post (entryInput $"Refused {tag}" today None [ comment None $"fine {tag}"; bad ])
            let rightFault =
                match fault, attempt with
                | "whitespace-only text", Error (AsError (JournalEntryCommentIsEmpty _)) -> true
                | "text of 2001 characters", Error (AsError (JournalEntryCommentTooLong _)) -> true
                | "a secondary ID that matches no journal entry", Error (AsError (JournalEntryCommentSecondaryJeHeaderIdNotFound id)) -> id = missing
                | _ -> false
            Assert.True(rightFault, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
            Assert.Empty(describedOn today $"Refused {tag}"))

    // =========================================================================
    // REQ-JE-3.1.1 — reads return voided entries
    // =========================================================================

    [<Theory>]
    [<InlineData("fiscal period")>]
    [<InlineData("date range")>]
    [<InlineData("external reference")>]
    [<InlineData("source FI")>]
    member _.``REQ-JE-3.1.1 for each of the fetch by fiscal period, by date range, by external reference and by source FI, a voided and an active entry that both match the query are both returned, the voided one with its voided-at set and the active one without`` (read: string) =
        withEntries (fun post ->
            result {
                let tag = newTag ()
                let fi = $"FI {tag}"
                let! voided = post (entryInput $"Voided {tag}" today (Some(fi, $"Ref {tag}")) [])
                let! active = post (entryInput $"Active {tag}" today (Some(fi, $"Ref {tag}")) [])
                let! _ = voidEntry voided.header.id
                let! fetched =
                    match read with
                    | "fiscal period" ->
                        ({ periodKey = today.ToString("yyyy-MM", null) } : JournalEntryFetchByPeriodInput)
                        |> send "FetchByPeriod" |> Result.bind Json.fromJson<JournalEntryReturn list>
                    | "date range" -> fetchByDateRange today today
                    | "external reference" ->
                        ({ fi = Some fi; reference = Some $"Ref {tag}" } : JournalEntryFetchByExternalReferenceInput)
                        |> send "FetchByExternalReference" |> Result.bind Json.fromJson<JournalEntryReturn list>
                    | _ ->
                        ({ fi = Some fi; reference = None } : JournalEntryFetchByExternalReferenceInput)
                        |> send "FetchByExternalReference" |> Result.bind Json.fromJson<JournalEntryReturn list>
                let find (id: Guid) = fetched |> List.filter (fun e -> e.header.id = id)
                let voidedRead = Assert.Single(find voided.header.id)
                let activeRead = Assert.Single(find active.header.id)
                Assert.True(voidedRead.header.voidedAt.IsSome)
                Assert.True(activeRead.header.voidedAt.IsNone)
            }
            |> railroadWrapper)

    [<Fact>]
    member _.``REQ-JE-3.1.1 fetching a voided journal entry by ID returns it with its voided-at set`` () =
        withEntries (fun post ->
            result {
                let! entry = post (entryInput $"Voided by ID {newTag ()}" today None [])
                let! _ = voidEntry entry.header.id
                let! fetched =
                    ({ id = entry.header.id } : JournalEntryFetchByIdInput) |> send "FetchById" |> Result.bind Json.fromJson<JournalEntryReturn>
                Assert.Equal(entry.header.id, fetched.header.id)
                Assert.True(fetched.header.voidedAt.IsSome)
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-JE-3.5.1, 3.7, 3.7.1 — lookups that can't run, and the one-day range
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-3.5.1 a journal entry lookup by external reference given neither a source FI nor a reference value fails with a typed error`` () =
        let attempt = ({ fi = None; reference = None } : JournalEntryFetchByExternalReferenceInput) |> send "FetchByExternalReference"
        let typed =
            match attempt with
            | Error (AsError JournalEntryFetchByReferenceBothArgumentsNull) -> true
            | _ -> false
        Assert.True(typed)

    [<Fact>]
    member _.``REQ-JE-3.7.1 a journal entry fetch by date range whose start date is after its end date fails with a typed error`` () =
        let typed =
            match fetchByDateRange (today.PlusDays(1)) today with
            | Error (AsError (JournalEntryFetchByDateRangeBeginAfterEnd(b, e))) -> b = today.PlusDays(1) && e = today
            | _ -> false
        Assert.True(typed)

    [<Fact>]
    member _.``REQ-JE-3.7 a journal entry fetch by date range whose start date equals its end date returns every entry dated that day and none dated the day before or after`` () =
        withEntries (fun post ->
            result {
                let tag = newTag ()
                let day = today.PlusDays(-1)
                let! before = post (entryInput $"Before {tag}" (day.PlusDays(-1)) None [])
                let! onDay1 = post (entryInput $"On day one {tag}" day None [])
                let! onDay2 = post (entryInput $"On day two {tag}" day None [])
                let! after = post (entryInput $"After {tag}" (day.PlusDays(1)) None [])
                let! fetched = fetchByDateRange day day
                let ids = fetched |> List.map _.header.id |> Set.ofList
                Assert.Contains(onDay1.header.id, ids)
                Assert.Contains(onDay2.header.id, ids)
                Assert.DoesNotContain(before.header.id, ids)
                Assert.DoesNotContain(after.header.id, ids)
                Assert.All(fetched, fun e -> Assert.Equal(day, e.header.entryDate))
            }
            |> railroadWrapper)

    // =========================================================================
    // REQ-JE-5.8 — a comment's journal entries must exist
    // =========================================================================

    [<Fact>]
    member _.``REQ-JE-5.8 creating a comment whose primary journal entry ID matches no journal entry fails with a typed error naming the primary, and no comment is stored`` () =
        let missing = Guid.NewGuid()
        let text = $"Orphan comment {newTag ()}"
        let attempt = ({ journalEntryId = missing; comment = comment None text } : JournalEntryAddCommentInput) |> send "AddComment"
        let namesPrimary =
            match attempt with
            | Error (AsError (JournalEntryCommentPrimaryJeHeaderIdNotFound id)) -> id = missing
            | _ -> false
        Assert.True(namesPrimary)
        let stored =
            JournalEntryComment.fetchByJournalEntryId (Context.create NoTransaction FetchOnly) (JournalEntryHeaderId.fromGuid missing)
            |> orFail
        Assert.Empty(stored)

    [<Fact>]
    member _.``REQ-JE-5.8 creating a comment whose secondary journal entry ID matches no journal entry fails with a typed error naming the secondary, and no comment is stored`` () =
        withEntries (fun post ->
            result {
                let! entry = post (entryInput $"Commented later {newTag ()}" today None [])
                let missing = Guid.NewGuid()
                let attempt =
                    ({ journalEntryId = entry.header.id; comment = comment (Some missing) "points nowhere" } : JournalEntryAddCommentInput)
                    |> send "AddComment"
                let namesSecondary =
                    match attempt with
                    | Error (AsError (JournalEntryCommentSecondaryJeHeaderIdNotFound id)) -> id = missing
                    | _ -> false
                Assert.True(namesSecondary)
                let! stored =
                    ({ id = entry.header.id } : JournalEntryFetchByIdInput) |> send "FetchById" |> Result.bind Json.fromJson<JournalEntryReturn>
                Assert.Empty(stored.comments)
            }
            |> railroadWrapper)
