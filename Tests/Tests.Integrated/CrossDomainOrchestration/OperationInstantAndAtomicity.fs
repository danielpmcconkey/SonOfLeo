module Tests.Integrated.CrossDomainOrchestration.OperationInstantAndAtomicity

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.AuditEnvelope
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.DataIngestion
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

(* The system clock is the only clock there is, and it advances between reads: it is kept to the microsecond, and
   every database write takes longer than that. So a row stamped from a fresh clock read is later than the instant
   the operation read when it began, and "equal to the initiation instant" can only hold if the row took that
   instant. The instant tests run in a rolled-back transaction and take the instant from the context. The atomicity
   tests go through the routes, which commit; a finally deletes what they made. *)

let private newTag () = "t" + Guid.NewGuid().ToString("N").Substring(0, 10)

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private fresh () = Context.create NoTransaction FetchOnly

let private instantOf (context: Context.Context) = context |> Context.getInitiationInstant

let private accountIdOf (fixture: TestDataFixture) (code: string) =
    fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

let private card (fixture: TestDataFixture) =
    fixture.Data.ingestionSources
    |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestCreditCardCo")

/// A staged entry from TestCreditCardCo dated the date, with accounts on both 100.00 lines, taken to the statuses.
let private stagedEntry (fixture: TestDataFixture) (context: Context.Context) (reference: string) (date: LocalDate) (path: string list) =
    let start = Clock.now ()
    let transitions =
        ("Ingested" :: path)
        |> List.pairwise
        |> List.mapi (fun i (from, into) -> (Some from, into, start.Plus(Duration.FromTicks(100L * int64 (i + 1))), "Operator"))
    createStageEntryForTest context "/tmp/instant-and-atomicity-test.dat" $"Instant and atomicity {reference}" reference
        (card fixture) date
        [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
        ((None, "Ingested", start, "StageIngestion") :: transitions)

let private sendJe (verb: string) (payload: 'a) = payload |> Json.toJson |> Result.bind (routeUiCommandForTesting "JournalEntry" verb [])

let private describedToday (description: string) =
    JE.fetchByDateRange (fresh ()) (Calendar.today ()) (Calendar.today ())
    |> orFail
    |> List.filter (fun e -> e |> JE.header |> JournalEntryHeader.description |> JournalEntryDescription.value = description)

let private postStagedThroughRoute () =
    ({ isShadow = false } : PostStageEntriesInput)
    |> Json.toJson
    |> Result.bind (routeUiCommandForTesting "Ingestion" "PostStageEntries" [])

[<Collection("SharedTestData")>]
type OperationInstantAndAtomicityTests(fixture: TestDataFixture) =

    let accountIdOf = accountIdOf fixture

    let lines =
        [ (accountIdOf "F-2230", 30.00M, "Debit", Some "first debit")
          (accountIdOf "F-5300", 20.00M, "Debit", None)
          (accountIdOf "F-1280", 50.00M, "Credit", None) ]

    // =========================================================================
    // REQ-SYS-3.3 — updates stamp modified-at with the operation's instant
    // =========================================================================

    [<Theory>]
    [<InlineData("amending a comment's text")>]
    [<InlineData("re-pointing a comment's secondary journal entry")>]
    [<InlineData("voiding a journal entry")>]
    [<InlineData("re-pointing a payment agreement link")>]
    member _.``REQ-SYS-3.3 for each update (amending a comment's text, re-pointing a comment's secondary journal entry, voiding a journal entry, re-pointing a payment agreement link), under a clock that advances on every read, the record's modified-at is set to the operation's initiation instant and its created-at is unchanged`` (update: string) =
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun creating ->
            result {
                let tag = newTag ()
                let! target, _ = createTestJournalEntryFromPrimitives creating $"Target {tag}" None (Calendar.today ()) lines [] []
                let! other, _ = createTestJournalEntryFromPrimitives creating $"Other {tag}" None (Calendar.today ()) lines [] []
                let targetId = target |> JE.header |> JournalEntryHeader.journalEntryHeaderId
                let otherId = other |> JE.header |> JournalEntryHeader.journalEntryHeaderId
                let! text = $"note {tag}" |> CommentText.create
                let! note = JournalEntryCommentOrchestration.constructNewAndPersist creating targetId None text
                let noteId = note |> JournalEntryComment.journalEntryCommentId
                let! legLine = stagedEntry fixture creating $"Link {tag}" (Calendar.today ()) [ "Classified" ]
                let lineId =
                    legLine |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit) |> StageEntryLine.stageEntryLineId
                let! link = CashFlowOps.constructNewPaymentAgreementLinkAndPersist creating fixture.Data.cashFlow.legAId lineId
                let linkId = link |> PaymentAgreementLink.paymentAgreementLinkId
                let updating = creating |> TestContext.updateInitiationInstant
                let! createdAt, modifiedAt =
                    match update with
                    | "amending a comment's text" ->
                        result {
                            let! amended = "amended" |> CommentText.create
                            let! _ = JournalEntryCommentOrchestration.updateComment updating noteId (SetTo amended) NoChange
                            let! read = noteId |> JournalEntryComment.fetchById updating
                            return read |> JournalEntryComment.createdAt, read |> JournalEntryComment.modifiedAt
                        }
                    | "re-pointing a comment's secondary journal entry" ->
                        result {
                            let! _ = JournalEntryCommentOrchestration.updateComment updating noteId NoChange (SetTo(Some otherId))
                            let! read = noteId |> JournalEntryComment.fetchById updating
                            return read |> JournalEntryComment.createdAt, read |> JournalEntryComment.modifiedAt
                        }
                    | "voiding a journal entry" ->
                        result {
                            let! reason = "voided for the instant test" |> CommentText.create
                            let! _ = targetId |> JournalEntryVoiding.voidJournalEntry updating None reason
                            let! read = targetId |> JournalEntryHeader.fetchById updating
                            return read |> JournalEntryHeader.createdAt, read |> JournalEntryHeader.modifiedAt
                        }
                    | _ ->
                        result {
                            let! read =
                                PaymentAgreementLink.update updating
                                    { linkIdToUpdate = linkId; paymentAgreementIdUpdate = SetTo fixture.Data.cashFlow.legBId }
                            return read |> PaymentAgreementLink.createdAt, read |> PaymentAgreementLink.modifiedAt
                        }
                Assert.Equal(instantOf creating, createdAt)
                Assert.Equal(instantOf updating, modifiedAt)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.3 a rejected update leaves the record's modified-at unchanged`` () =
        runCommandRouteAndAutoRollback JournalEntryUpdateComment (fun creating ->
            result {
                let tag = newTag ()
                let! target, targetId = createTestJournalEntryFromPrimitives creating $"Target {tag}" None (Calendar.today ()) lines [] []
                let! text = $"note {tag}" |> CommentText.create
                let! note = JournalEntryCommentOrchestration.constructNewAndPersist creating targetId None text
                let noteId = note |> JournalEntryComment.journalEntryCommentId
                let updating = creating |> TestContext.updateInitiationInstant
                // a comment can't name its own primary as its secondary (REQ-JE-1.53)
                let attempt = JournalEntryCommentOrchestration.updateComment updating noteId NoChange (SetTo(Some targetId))
                Assert.True(attempt |> Result.isError)
                let! read = noteId |> JournalEntryComment.fetchById updating
                Assert.Equal(instantOf creating, read |> JournalEntryComment.modifiedAt)
                Assert.Equal(None, read |> JournalEntryComment.secondaryJournalEntryId)
                ignore target
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-SYS-3.4 — one operation, one instant
    // =========================================================================

    [<Fact>]
    member _.``REQ-SYS-3.4 posting a journal entry with several lines, an external reference and a comment, under a clock that advances on every read, gives every row it writes a created-at and modified-at equal to the operation's single initiation instant`` () =
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let tag = newTag ()
                let! _, id =
                    createTestJournalEntryFromPrimitives context $"Instants {tag}" None (Calendar.today ()) lines
                        [ ("Instant FI", $"Ref {tag}") ] [ (None, $"note {tag}") ]
                let! read = id |> JE.fetchById context
                let instant = instantOf context
                let stamps =
                    [ yield (read |> JE.header |> JournalEntryHeader.createdAt, read |> JE.header |> JournalEntryHeader.modifiedAt)
                      for line in read |> JE.jeLines do
                          yield (line |> JournalEntryLine.createdAt, line |> JournalEntryLine.modifiedAt)
                      for reference in read |> JE.externalReferences do
                          yield (reference |> JournalEntryExternalReference.createdAt, reference |> JournalEntryExternalReference.modifiedAt)
                      for note in read |> JE.comments do
                          yield (note |> JournalEntryComment.createdAt, note |> JournalEntryComment.modifiedAt) ]
                Assert.Equal(1 + 3 + 1 + 1, stamps.Length)
                Assert.All(stamps, fun (created, modified) ->
                    Assert.Equal(instant, created)
                    Assert.Equal(instant, modified))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.4 a batch post of several staged entries, under a clock that advances on every read, writes every journal entry's created-at and modified-at and every status transition's timestamp as the one initiation instant of the operation`` () =
        runCommandRouteAndAutoRollback IngestPostStageEntries (fun staging ->
            result {
                let tag = newTag ()
                let! first = stagedEntry fixture staging $"Batch1-{tag}" (Calendar.today ()) [ "Classified" ]
                let! second = stagedEntry fixture staging $"Batch2-{tag}" (Calendar.today ()) [ "Classified"; "Reviewed" ]
                let posting = staging |> TestContext.updateInitiationInstant
                do! post posting
                let instant = instantOf posting
                [ first; second ]
                |> List.iter (fun entry ->
                    let read = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId |> fetchByStageEntryHeaderId posting |> orFail
                    let postedTransition =
                        read |> statusTransitions |> List.filter (fun t -> t |> StageEntryStatusTransition.toStatus = StagedEntryStatus.Posted)
                    Assert.Equal(instant, (Assert.Single(postedTransition)) |> StageEntryStatusTransition.instant)
                    let journalEntryId = read |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId |> Option.get
                    let journalEntry = journalEntryId |> JE.fetchById posting |> orFail
                    let header = journalEntry |> JE.header
                    Assert.Equal(instant, header |> JournalEntryHeader.createdAt)
                    Assert.Equal(instant, header |> JournalEntryHeader.modifiedAt)
                    Assert.All(journalEntry |> JE.jeLines, fun line ->
                        Assert.Equal(instant, line |> JournalEntryLine.createdAt)
                        Assert.Equal(instant, line |> JournalEntryLine.modifiedAt)))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.4 every operation run through the interface's command runner carries an auditable action identifying that operation, and two different operations carry different actions`` () =
        let actionOf (action: 'a) =
            runCommandRouteAndAutoRollback action (fun context -> Ok(context.loggingContext.envelope |> AuditEnvelope.action))
            |> orFail
        let posting = actionOf JournalEntryPostNew
        let voiding = actionOf JournalEntryVoid
        Assert.Equal(box JournalEntryPostNew, box posting)
        Assert.Equal(box JournalEntryVoid, box voiding)
        Assert.NotEqual(box posting, box voiding)

    // =========================================================================
    // REQ-SYS-8.1 — an operation's writes land together or not at all
    // =========================================================================

    [<Theory>]
    [<InlineData("posting a journal entry")>]
    [<InlineData("a batch post")>]
    member _.``REQ-SYS-8.1 for each of posting a journal entry whose last comment names a secondary journal entry that doesn't exist, and a batch post whose last staged entry is dated in a closed fiscal period, the request fails with that step's typed error after earlier writes were issued and none of its writes are in the database`` (operation: string) =
        let tag = newTag ()
        let staged = ResizeArray<StageEntryHeaderId>()
        try
            match operation with
            | "posting a journal entry" ->
                let missing = Guid.NewGuid()
                let input : JournalEntryInput =
                    { header = { description = $"Half written {tag}"; source = None; entryDate = Calendar.today () }
                      lines =
                        [ { accountCode = "F-2230"; amount = 10.00M; lineType = "Debit"; memo = None }
                          { accountCode = "F-1280"; amount = 10.00M; lineType = "Credit"; memo = None } ]
                      externalReferences = [ { financialInstitution = "Atomicity FI"; referenceText = $"Ref {tag}" } ]
                      comments =
                        [ { secondaryJournalEntryId = None; commentText = "fine" }
                          { secondaryJournalEntryId = Some missing; commentText = "points nowhere" } ] }
                let attempt = input |> sendJe "PostNew"
                let rightError =
                    match attempt with
                    | Error (AsError (JournalEntryCommentSecondaryJeHeaderIdNotFound id)) -> id = missing
                    | _ -> false
                Assert.True(rightError)
                Assert.Empty(describedToday $"Half written {tag}")
            | _ ->
                let closedDate = (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)
                let good, bad =
                    runCommandRouteAndAutoCompleteTransaction IngestRawEntries (fun context ->
                        result {
                            // Reviewed entries are posted before Classified ones, so the closed-period entry comes last
                            let! good = stagedEntry fixture context $"Good-{tag}" (Calendar.today ()) [ "Classified"; "Reviewed" ]
                            let! bad = stagedEntry fixture context $"Closed-{tag}" closedDate [ "Classified" ]
                            return good, bad
                        })
                    |> orFail
                [ good; bad ] |> List.iter (fun e -> staged.Add(e |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId))
                let attempt = postStagedThroughRoute ()
                let rightError =
                    match attempt with
                    | Error (AsError (JournalEntryHeaderEntryDateInvalid _)) -> true
                    | _ -> false
                Assert.True(rightError)
                Assert.Empty(describedToday $"Instant and atomicity Good-{tag}")
                let goodAfter = good |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId |> fetchByStageEntryHeaderId (fresh ()) |> orFail
                Assert.Equal(Some StagedEntryStatus.Reviewed, goodAfter |> stageEntryHeader |> StageEntryHeader.currentStatus)
                Assert.Equal(None, goodAfter |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId)
        finally
            staged |> Seq.iter (Some >> Cleanup.cleanUpStageEntryHeaderId >> orFail)

    [<Fact>]
    member _.``REQ-SYS-8.1 an operation run through the interface's command runner that writes and then raises an exception leaves none of its writes in the database`` () =
        let description = $"Written then thrown {newTag ()}"
        let outcome =
            try
                runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew (fun context ->
                    result {
                        let! _ = createTestJournalEntryFromPrimitives context description None (Calendar.today ()) lines [] []
                        return raise (InvalidOperationException "raised after the write")
                    })
                |> Result.map ignore
            with ex -> Error(TestError.TestingError ex.Message :> IAppError)
        let written = describedToday description
        written |> List.iter (fun e -> e |> JE.header |> JournalEntryHeader.journalEntryHeaderId |> Some |> Cleanup.cleanUpJournalEntryId |> orFail)
        Assert.True(outcome |> Result.isError)
        Assert.Empty(written)

    [<Theory>]
    [<InlineData("posting a journal entry")>]
    [<InlineData("a batch post")>]
    member _.``REQ-SYS-8.1 for each of posting a journal entry with several comments and a batch post of several staged entries, when every step is valid, all of the operation's writes are in the database`` (operation: string) =
        let tag = newTag ()
        let staged = ResizeArray<StageEntryHeaderId>()
        let entries = ResizeArray<JournalEntryHeaderId>()
        try
            match operation with
            | "posting a journal entry" ->
                let input : JournalEntryInput =
                    { header = { description = $"Whole {tag}"; source = None; entryDate = Calendar.today () }
                      lines =
                        [ { accountCode = "F-2230"; amount = 10.00M; lineType = "Debit"; memo = None }
                          { accountCode = "F-1280"; amount = 10.00M; lineType = "Credit"; memo = None } ]
                      externalReferences = [ { financialInstitution = "Atomicity FI"; referenceText = $"Ref {tag}" } ]
                      comments =
                        [ { secondaryJournalEntryId = None; commentText = "first" }
                          { secondaryJournalEntryId = None; commentText = "second" } ] }
                let posted = input |> sendJe "PostNew" |> Result.bind Json.fromJson<JournalEntryReturn> |> orFail
                entries.Add(posted.header.id |> JournalEntryHeaderId.fromGuid)
                let read = posted.header.id |> JournalEntryHeaderId.fromGuid |> JE.fetchById (fresh ()) |> orFail
                Assert.Equal(2, read |> JE.jeLines |> List.length)
                Assert.Equal(1, read |> JE.externalReferences |> List.length)
                Assert.Equal(2, read |> JE.comments |> List.length)
            | _ ->
                let first, second =
                    runCommandRouteAndAutoCompleteTransaction IngestRawEntries (fun context ->
                        result {
                            let! first = stagedEntry fixture context $"Whole1-{tag}" (Calendar.today ()) [ "Classified" ]
                            let! second = stagedEntry fixture context $"Whole2-{tag}" (Calendar.today ()) [ "Classified"; "Reviewed" ]
                            return first, second
                        })
                    |> orFail
                [ first; second ] |> List.iter (fun e -> staged.Add(e |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId))
                postStagedThroughRoute () |> orFail |> ignore
                staged
                |> Seq.iter (fun id ->
                    let read = id |> fetchByStageEntryHeaderId (fresh ()) |> orFail
                    let journalEntryId = read |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId
                    journalEntryId |> Option.iter entries.Add
                    Assert.Equal(Some StagedEntryStatus.Posted, read |> stageEntryHeader |> StageEntryHeader.currentStatus)
                    let journalEntry = journalEntryId |> Option.get |> JE.fetchById (fresh ()) |> orFail
                    Assert.Equal(2, journalEntry |> JE.jeLines |> List.length)
                    Assert.Equal(1, journalEntry |> JE.externalReferences |> List.length))
        finally
            // staged entries first: posting links each staged header and line to the journal entry it made
            staged |> Seq.iter (Some >> Cleanup.cleanUpStageEntryHeaderId >> orFail)
            entries |> Seq.iter (Some >> Cleanup.cleanUpJournalEntryId >> orFail)
