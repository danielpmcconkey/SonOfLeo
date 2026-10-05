module Tests.Integrated.CrossDomainOrchestration.OperationInstantAndAtomicity

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.DalError
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
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
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

/// One record of the kind, created under the creating context. Gives back a read of its stored created-at and modified-at,
/// and an update of it (for an Account Snapshot or a Valuation, recording the same date again).
let private createdRecord (fixture: TestDataFixture) (kind: string) (creating: Context.Context) =
    let p = fixture.Data.positions
    let stamps (createdAt: 'r -> Instant) (modifiedAt: 'r -> Instant) (read: Result<'r, IAppError>) =
        read |> Result.map (fun r -> createdAt r, modifiedAt r)
    let ignoreResult (r: Result<'a, IAppError>) = r |> Result.map ignore
    result {
        let! samId = PositionsLookups.personIdOf creating PositionsFixture.sam
        let! jordanCustodialId = PositionsLookups.investmentAccountIdOf creating PositionsFixture.jordanCustodial
        let! rentalId = PositionsLookups.propertyIdOf creating PositionsFixture.rental
        match kind with
        | "Person" ->
            let! person =
                PositionsValues.toPersonName "Instant Example"
                |> fun name -> PersonOrchestration.constructNewAndPersist creating name (LocalDate(1990, 6, 1))
            let personId = person |> Business.General.Person.personId
            let read context =
                personId
                |> Business.General.Person.fetchById context
                |> stamps Business.General.Person.createdAt Business.General.Person.modifiedAt
            let update context =
                PersonOrchestration.updatePerson
                    context
                    { personIdToUpdate = personId
                      personNameUpdate = SetTo(PositionsValues.toPersonName "Instant Example Renamed")
                      birthdateUpdate = NoChange }
                |> ignoreResult
            return read, update
        | "Dimension Value" ->
            let! dimensionValue =
                DimensionValueOrchestration.constructNewAndPersist creating Sector (PositionsValues.toDimensionValueName "Instant Sector")
            let dimensionValueId = dimensionValue |> DimensionValue.dimensionValueId
            let read context =
                dimensionValueId |> DimensionValue.fetchById context |> stamps DimensionValue.createdAt DimensionValue.modifiedAt
            let update context =
                DimensionValueOrchestration.renameDimensionValue
                    context
                    { dimensionValueIdToUpdate = dimensionValueId
                      dimensionValueNameUpdate = SetTo(PositionsValues.toDimensionValueName "Instant Sector Renamed") }
                |> ignoreResult
            return read, update
        | "Security" ->
            let! security =
                SecurityOrchestration.constructNewAndPersist creating (PositionsValues.toSecurityName "Instant Example Fund") None []
            let securityId = security |> Security.securityId
            let read context = securityId |> Security.fetchById context |> stamps Security.createdAt Security.modifiedAt
            let update context =
                SecurityOrchestration.updateSecurity context securityId NoChange (SetTo(Some(PositionsValues.toTicker "INSTX"))) []
                |> ignoreResult
            return read, update
        | "Investment Account" ->
            let! account =
                InvestmentAccountOrchestration.constructNewAndPersist
                    creating
                    (PositionsValues.toAccountName "Instant Brokerage")
                    (PositionsValues.toInstitution "Example Brokerage")
                    (PositionsValues.toAccountGroup "Brokerage")
                    Taxable
                    [ samId ]
                    (PositionsValues.toActivityPeriod p.accountsActiveBegin None)
                    None
            let accountId = account |> InvestmentAccount.investmentAccountId
            let read context =
                accountId |> InvestmentAccount.fetchById context |> stamps InvestmentAccount.createdAt InvestmentAccount.modifiedAt
            let update context =
                InvestmentAccountOrchestration.updateInvestmentAccount
                    context
                    { investmentAccountIdToUpdate = accountId
                      investmentAccountNameUpdate = NoChange
                      institutionUpdate = SetTo(PositionsValues.toInstitution "Example Discount Brokerage")
                      accountGroupUpdate = NoChange
                      taxTreatmentUpdate = NoChange
                      ownersUpdate = NoChange
                      activityPeriodUpdate = NoChange
                      ledgerAccountIdUpdate = NoChange }
                |> ignoreResult
            return read, update
        | "Holding" ->
            let! securityId = PositionsLookups.securityIdOf creating PositionsFixture.totalMarket
            let! holding = HoldingOrchestration.constructNewAndPersist creating jordanCustodialId securityId (Some AverageCost)
            let holdingId = holding |> Holding.holdingId
            let read context = holdingId |> Holding.fetchById context |> stamps Holding.createdAt Holding.modifiedAt
            let update context =
                HoldingOrchestration.changeHoldingBasisMethod context jordanCustodialId securityId (Some SpecificLot) |> ignoreResult
            return read, update
        | "Account Snapshot" ->
            let! _ = AccountSnapshotOrchestration.recordSnapshots creating [ jordanCustodialId, p.d2, Reported, None, [] ]
            let read context =
                AccountSnapshotHeader.fetchByInvestmentAccountAndDate context jordanCustodialId p.d2
                |> Result.map Option.get
                |> stamps AccountSnapshotHeader.createdAt AccountSnapshotHeader.modifiedAt
            let update context =
                AccountSnapshotOrchestration.recordSnapshots context [ jordanCustodialId, p.d2, Imported, None, [] ] |> ignoreResult
            return read, update
        | "Property" ->
            let! property =
                RealEstateOrchestration.constructNewAndPersist
                    creating
                    (PositionsValues.toPropertyName "Instant Cabin")
                    Rental
                    [ samId ]
                    (PositionsValues.toOwnedPeriod p.rentalAcquired None)
                    (PositionsValues.toPurchaseBasis 90000.00M)
                    []
                    []
            let propertyId = property |> Property.propertyId
            let read context = propertyId |> Property.fetchById context |> stamps Property.createdAt Property.modifiedAt
            let update context =
                RealEstateOrchestration.updateProperty
                    context
                    { propertyIdToUpdate = propertyId
                      propertyNameUpdate = NoChange
                      propertyUseUpdate = NoChange
                      ownedPeriodUpdate = NoChange
                      purchaseBasisUpdate = SetTo(PositionsValues.toPurchaseBasis 95000.00M)
                      assetAccountIdsUpdate = NoChange
                      ownersUpdate = NoChange
                      mortgageAccountIdsUpdate = NoChange }
                |> ignoreResult
            return read, update
        | "Valuation" ->
            let! valuation =
                RealEstateOrchestration.recordValuation
                    creating rentalId p.d2 (PositionsValues.toValuationValue 260000.00M) (PositionsValues.toValuationBasis "Broker opinion")
            let valuationId = valuation |> Valuation.valuationId
            let read context = valuationId |> Valuation.fetchById context |> stamps Valuation.createdAt Valuation.modifiedAt
            let update context =
                RealEstateOrchestration.recordValuation
                    context rentalId p.d2 (PositionsValues.toValuationValue 265000.00M) (PositionsValues.toValuationBasis "Appraisal")
                |> ignoreResult
            return read, update
        | other -> return failwith $"No record kind \"{other}\""
    }

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
                let! link = CashFlowOps.constructNewAndPersist creating fixture.Data.cashFlow.legAId lineId
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
                let () =
                    match JournalEntryCommentOrchestration.updateComment updating noteId NoChange (SetTo(Some targetId)) with
                    | Error (AsError (JournalEntryCommentPrimaryAndSecondaryIdsAreSame (primary, secondary))) ->
                        Assert.Equal(targetId |> JournalEntryHeaderId.value, primary)
                        Assert.Equal(targetId |> JournalEntryHeaderId.value, secondary)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
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

    // =========================================================================
    // REQ-SYS-8.1 — an operation's writes land together or not at all
    // =========================================================================

    [<Theory>]
    [<InlineData("posting a journal entry")>]
    [<InlineData("a batch post")>]
    member _.``REQ-SYS-8.1 REQ-JE-2.12 for each of posting a journal entry whose last comment names a secondary journal entry that doesn't exist, and a batch post whose last staged entry is dated in a closed fiscal period, the request fails with that step's typed error after earlier writes were issued and none of its writes are in the database`` (operation: string) =
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
        // the header the operation wrote before it raised, captured from inside it to prove the write was issued
        let writtenId = ref None
        let outcome =
            runCommandRouteAndAutoCompleteTransaction JournalEntryPostNew (fun context ->
                result {
                    let! _, id = createTestJournalEntryFromPrimitives context description None (Calendar.today ()) lines [] []
                    writtenId.Value <- Some id
                    return raise (InvalidOperationException "raised after the write")
                })
            |> Result.map ignore
        let survivors = describedToday description
        try
            match outcome with
            | Error (AsError (DalErrorDuringAutoCompleteTransactionRun ex)) -> Assert.Equal("raised after the write", ex.Message)
            | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
            | Ok _ -> Assert.Fail "Expected failure; got success"
            match writtenId.Value with
            | None -> Assert.Fail "the operation never wrote its journal entry"
            | Some id ->
                match id |> JE.fetchById (fresh ()) with
                | Error (AsError (JournalEntryHeaderIdDoesntExist missing)) -> Assert.Equal(id |> JournalEntryHeaderId.value, missing)
                | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                | Ok _ -> Assert.Fail "the journal entry written before the exception is still in the database"
            Assert.Empty(survivors)
        finally
            survivors |> List.iter (fun e -> e |> JE.header |> JournalEntryHeader.journalEntryHeaderId |> Some |> Cleanup.cleanUpJournalEntryId |> orFail)

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

    // =========================================================================
    // REQ-SYS-3.2 and REQ-SYS-3.3 — Person and the Positions records
    // =========================================================================

    [<Theory>]
    [<InlineData("Person")>]
    [<InlineData("Dimension Value")>]
    [<InlineData("Security")>]
    [<InlineData("Investment Account")>]
    [<InlineData("Holding")>]
    [<InlineData("Account Snapshot")>]
    [<InlineData("Property")>]
    [<InlineData("Valuation")>]
    member _.``REQ-SYS-3.2 for each of Person, Dimension Value, Security, Investment Account, Holding, Account Snapshot, Property and Valuation, creating one under a clock that advances on every read sets its created-at and modified-at both to the operation's initiation instant`` (kind: string) =
        runCommandRouteAndAutoRollback PositionsCreateProperty (fun creating ->
            result {
                let! read, _ = createdRecord fixture kind creating
                let! createdAt, modifiedAt = read creating
                Assert.Equal((instantOf creating, instantOf creating), (createdAt, modifiedAt))
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Person")>]
    [<InlineData("Dimension Value")>]
    [<InlineData("Security")>]
    [<InlineData("Investment Account")>]
    [<InlineData("Holding")>]
    [<InlineData("Account Snapshot")>]
    [<InlineData("Property")>]
    [<InlineData("Valuation")>]
    member _.``REQ-SYS-3.3 for each update to a Person, Dimension Value, Security, Investment Account, Holding, Account Snapshot (re-recorded), Property and Valuation (re-recorded), under a clock that advances on every read, the record's modified-at is set to the operation's initiation instant and its created-at is unchanged`` (kind: string) =
        runCommandRouteAndAutoRollback PositionsUpdateProperty (fun creating ->
            result {
                let! read, update = createdRecord fixture kind creating
                let updating = creating |> TestContext.updateInitiationInstant
                Assert.True(instantOf updating > instantOf creating, "the updating operation's instant is not later than the creating one's")
                do! update updating
                let! createdAt, modifiedAt = read updating
                Assert.Equal((instantOf creating, instantOf updating), (createdAt, modifiedAt))
            })
        |> railroadWrapper
