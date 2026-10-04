module Tests.Integrated.CrossDomainOrchestration.RevisedRequirementsCashFlow

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.FieldUpdate
open App.Utility.IAppError
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

(* Plan item 30: the clauses of revised CashFlow requirements that no earlier test reached. Every test runs in a
   transaction that rolls back and calls the orchestration directly. The projection tests build their own Cash
   account, so its figures come only from the test's own agreements: Daily, with 100.00 legs. An Outgo leg debits
   F-2230 and credits the account; an Income leg debits the account and credits F-4290. A Payment points at a line of
   a journal entry dated today. *)

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let idOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    member _.Context = context
    member _.today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

    /// A new Asset account of subtype Cash.
    member _.cashAccount () =
        result {
            let code = $"X{Guid.NewGuid():N}".Substring(0, 10)
            let! _, accountId =
                createTestAccountFromPrimitives
                    context code $"Revised cash flow {code}" "Asset" ((Calendar.today ()).PlusYears(-2)) None (Some "Cash") None None
            return accountId
        }

    /// A Daily agreement of the direction with the number of 100.00 legs on the cash account. Returns its id and legs.
    member this.agreement (direction: FlowDirection) (cashId: AccountId) (legCount: int) =
        result {
            let name = $"Revised cash flow {Guid.NewGuid():N}"
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Revised cash flow counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create ((Calendar.today ()).PlusYears(-2)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let debit, credit =
                match direction with
                | Outgo -> idOf "F-2230", cashId
                | Income -> cashId, idOf "F-4290"
            let! legs =
                List.init legCount (fun i -> $"{name} leg {i + 1}")
                |> List.map (fun legName ->
                    result {
                        let! paName = legName |> PaymentAgreementName.create
                        let! expected = Money.fromDecimal 100.00M
                        let! due = 0 |> DaysDueAfterInvoiceDate.create
                        return (paName, (DebitAccount.create debit), (CreditAccount.create credit), Some expected, Some due, None)
                    })
                |> convertListOfResultsToResultsList
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName direction Cadence.Daily { nextInstance = this.today.PlusDays(-90) }
                    counterparty activityPeriod None legs
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legIds = agreement |> AgreementOrchestration.paymentAgreements |> List.map PaymentAgreement.paymentAgreementId
            return agreementId, legIds
        }

    /// A journal entry dated today moving the amount between the cash account and the other account of the direction;
    /// returns a pointer to the line a Payment of that direction points at.
    member this.paymentLine (direction: FlowDirection) (cashId: AccountId) (amount: decimal) =
        result {
            let landing, lines =
                match direction with
                | Outgo -> idOf "F-2230", [ (idOf "F-2230", amount, "Debit", None); (cashId, amount, "Credit", None) ]
                | Income -> idOf "F-4290", [ (cashId, amount, "Debit", None); (idOf "F-4290", amount, "Credit", None) ]
            let! entry, _ =
                createTestJournalEntryFromPrimitives context $"Revised cash flow {Guid.NewGuid()}" None this.today lines [] []
            return
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = landing)
                |> JournalEntryLine.journalEntryLineId
                |> fun line -> Posted(line, None)
        }

    /// An Instance of the agreement on the date with a 100.00 Invoice per (leg, due date, Payment amounts) given.
    /// Returns the Invoices' ids in the order given.
    member this.instance (direction: FlowDirection) (cashId: AccountId) (agreementId: MasterAgreementId) (date: LocalDate)
                         (invoices: (PaymentAgreementId * LocalDate * decimal list) list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let state = match direction with | Outgo -> InvoiceReceived | Income -> InvoiceSent
            let! fields =
                invoices
                |> List.map (fun (legId, due, paid) ->
                    result {
                        let! payments =
                            paid
                            |> List.map (fun p ->
                                result {
                                    let! pointer = this.paymentLine direction cashId p
                                    let! money = Money.fromDecimal p
                                    return (pointer, None, None, None)
                                })
                            |> convertListOfResultsToResultsList
                        return
                            (legId, None, (InvoiceDate.create date), (DueDate.create due), (InvoiceAmount.create amount),
                             state, None, None, payments)
                    })
                |> convertListOfResultsToResultsList
            let! created = InstanceOrchestration.constructNewAndPersist context agreementId date fields
            let made = created |> InstanceOrchestration.invoiceComposites |> List.map InstanceOrchestration.invoice
            return
                invoices
                |> List.map (fun (legId, _, _) -> made |> List.find (fun i -> i |> Invoice.paymentAgreementId = legId) |> Invoice.invoiceId)
        }

    /// An Instance of the agreement on the date with one Invoice; returns its id.
    member this.invoice direction cashId agreementId legId date due paid =
        this.instance direction cashId agreementId date [ (legId, due, paid) ] |> Result.map List.head

    member _.project (days: int) =
        result {
            let! horizon = days |> ProjectionHorizonInDays.create
            return! horizon |> CashFlowOps.projectCashFlowNDaysForward context
        }

let private accountIn (projection: CashFlowProjection) (accountId: AccountId) =
    projection.accounts |> List.find (fun a -> a.accountId = accountId)

let private amountOf (money: Money.Money) = money |> Money.amount

let private rolledBack (fixture: TestDataFixture) (body: Scenario -> Result<unit, IAppError>) =
    runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context -> body (Scenario(fixture, context))) |> railroadWrapper

let private noChange agreementId : MasterAgreement.MasterAgreementFieldUpdates =
    { agreementIdToUpdate = agreementId
      agreementNameUpdate = NoChange
      directionUpdate = NoChange
      cadenceUpdate = NoChange
      counterpartyUpdate = NoChange
      activityPeriodUpdate = NoChange
      memoUpdate = NoChange }

/// The master agreement's updatable fields, in a form that compares by value.
let private fieldsOf (agreement: AgreementOrchestration.Agreement) =
    let m = agreement |> AgreementOrchestration.masterAgreement
    let period = m |> MasterAgreement.activityPeriod
    (m |> MasterAgreement.agreementName |> AgreementName.value,
     m |> MasterAgreement.direction,
     m |> MasterAgreement.cadence |> Cadence.cadenceType,
     (m |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance,
     m |> MasterAgreement.counterparty |> Counterparty.value,
     period |> ActivityPeriod.activeBegin,
     period |> ActivityPeriod.activeEnd,
     m |> MasterAgreement.memo |> Option.map AgreementMemo.value)

[<Collection("SharedTestData")>]
type RevisedRequirementsCashFlowTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-8.3, 8.9 — which Invoices make up an account's known outflows
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-8.3 for each outgo invoice (due on the horizon end, overdue before today, fully paid, on a fulfilled instance, due the day after the horizon end, on an agreement crediting another account), the account's known outflows include its outstanding amount only for the first two`` () =
        rolledBack fixture (fun s ->
            result {
                let! cashId = s.cashAccount ()
                let! otherCashId = s.cashAccount ()
                let! agreementId, legs = s.agreement Outgo cashId 2
                let! otherAgreementId, otherLegs = s.agreement Outgo otherCashId 1
                let today = s.today
                (* Instances are only ever created forward, so they are made in date order *)
                let! overdue = s.invoice Outgo cashId agreementId legs[0] (today.PlusDays(-5)) (today.PlusDays(-5)) []
                (* an Instance whose every Invoice is fully paid *)
                let! fulfilled = s.instance Outgo cashId agreementId (today.PlusDays(-2)) [ (legs[0], today.PlusDays(3), [ 60.00M; 40.00M ]) ]
                (* one Instance holding a fully paid Invoice beside an unpaid one due after the horizon, so the Instance
                   is not fulfilled and neither Invoice may count *)
                let! mixed =
                    s.instance Outgo cashId agreementId (today.PlusDays(-1))
                        [ (legs[1], today.PlusDays(2), [ 100.00M ]); (legs[0], today.PlusDays(11), []) ]
                let! onHorizonEnd = s.invoice Outgo cashId agreementId legs[0] today (today.PlusDays(10)) [ 30.00M ]
                let! elsewhere = s.invoice Outgo otherCashId otherAgreementId otherLegs[0] today (today.PlusDays(1)) []
                let! projection = s.project 10
                let account = accountIn projection cashId
                Assert.Equal<Set<InvoiceId>>(set [ onHorizonEnd; overdue ], account.invoices |> List.map _.invoiceId |> Set.ofList)
                Assert.Equal(70.00M + 100.00M, account.knownOutflows |> amountOf)
                ignore (mixed, fulfilled, elsewhere)
            })

    [<Fact>]
    member _.``REQ-CF-8.3 an income invoice otherwise meeting every known-outflow condition for an account is not among its known outflows`` () =
        rolledBack fixture (fun s ->
            result {
                let! cashId = s.cashAccount ()
                let! agreementId, legs = s.agreement Income cashId 1
                let! income = s.invoice Income cashId agreementId legs[0] s.today (s.today.PlusDays(5)) []
                let! projection = s.project 10
                let account = accountIn projection cashId
                Assert.Equal(0.00M, account.knownOutflows |> amountOf)
                Assert.Equal(100.00M, account.knownInflows |> amountOf)
                Assert.Equal<InvoiceId list>([ income ], account.invoices |> List.map _.invoiceId)
            })

    [<Fact>]
    member _.``REQ-CF-8.9 an invoice whose payments exceed its amount has an outstanding amount of zero`` () =
        rolledBack fixture (fun s ->
            result {
                let! cashId = s.cashAccount ()
                let! agreementId, legs = s.agreement Outgo cashId 1
                let! overpaid = s.invoice Outgo cashId agreementId legs[0] s.today (s.today.PlusDays(5)) [ 80.00M; 50.00M ]
                let! projection = s.project 10
                let account = accountIn projection cashId
                let projected = account.invoices |> List.filter (fun i -> i.invoiceId = overpaid) |> Assert.Single
                Assert.Equal(0.00M, projected.outstanding |> amountOf)
                Assert.Equal(0.00M, account.knownOutflows |> amountOf)
            })

    // =========================================================================
    // REQ-CF-14.2 — updating a master agreement
    // =========================================================================

    [<Theory>]
    [<InlineData("name")>]
    [<InlineData("cadence")>]
    [<InlineData("counterparty")>]
    [<InlineData("start date")>]
    [<InlineData("end date")>]
    [<InlineData("memo")>]
    member _.``REQ-CF-14.2 for each field (name, cadence with its next-instance date, counterparty, start date, end date, memo), updating a master agreement's field stores the new value and leaves the others unchanged`` (field: string) =
        rolledBack fixture (fun s ->
            result {
                let! cashId = s.cashAccount ()
                let! agreementId, _ = s.agreement Outgo cashId 1
                let! before = agreementId |> AgreementOrchestration.fetchByMasterAgreementId s.Context
                let name, direction, cadenceType, next, counterparty, activeBegin, activeEnd, memo = fieldsOf before
                let! updates, expected =
                    match field with
                    | "name" ->
                        let newName = $"Renamed {Guid.NewGuid():N}"
                        newName |> AgreementName.create
                        |> Result.map (fun n ->
                            { noChange agreementId with agreementNameUpdate = SetTo n },
                            (newName, direction, cadenceType, next, counterparty, activeBegin, activeEnd, memo))
                    | "cadence" ->
                        let newNext = s.today.PlusDays(7)
                        Cadence.create (Cadence.Weekly(newNext.DayOfWeek |> Cadence.WeekDay.fromIsoDayOfWeek)) { nextInstance = newNext }
                        |> Result.map (fun c ->
                            { noChange agreementId with cadenceUpdate = SetTo c },
                            (name, direction, c |> Cadence.cadenceType, newNext, counterparty, activeBegin, activeEnd, memo))
                    | "counterparty" ->
                        "A different counterparty" |> Counterparty.create
                        |> Result.map (fun c ->
                            { noChange agreementId with counterpartyUpdate = SetTo c },
                            (name, direction, cadenceType, next, "A different counterparty", activeBegin, activeEnd, memo))
                    | "start date" ->
                        let newBegin = activeBegin.PlusDays(-30)
                        ActivityPeriod.create newBegin activeEnd ActivityPeriod.ConsideredAvailableBeforeBeginDate
                        |> Result.map (fun p ->
                            { noChange agreementId with activityPeriodUpdate = SetTo p },
                            (name, direction, cadenceType, next, counterparty, newBegin, activeEnd, memo))
                    | "end date" ->
                        let newEnd = Some(LocalDate(2049, 12, 31))
                        ActivityPeriod.create activeBegin newEnd ActivityPeriod.ConsideredAvailableBeforeBeginDate
                        |> Result.map (fun p ->
                            { noChange agreementId with activityPeriodUpdate = SetTo p },
                            (name, direction, cadenceType, next, counterparty, activeBegin, newEnd, memo))
                    | _ ->
                        "A new memo" |> AgreementMemo.create
                        |> Result.map (fun m ->
                            { noChange agreementId with memoUpdate = SetTo(Some m) },
                            (name, direction, cadenceType, next, counterparty, activeBegin, activeEnd, Some "A new memo"))
                s.Context |> ignore
                let updating = s.Context |> TestContext.updateInitiationInstant
                let! _ = AgreementOrchestration.updateAgreement updating [] [] updates
                let! after = agreementId |> AgreementOrchestration.fetchByMasterAgreementId updating
                Assert.NotEqual(fieldsOf before, expected)
                Assert.Equal(expected, fieldsOf after)
            })

    [<Fact>]
    member _.``REQ-CF-14.2 updating a master agreement's flow direction when every invoice's state is valid for the new direction stores the new direction`` () =
        (* An Outgo Invoice's states (InvoiceExpected, InvoiceReceived) are never valid for Income (REQ-CF-5.10), so the
           only agreement whose every Invoice is valid for the change is one with no Invoices. *)
        rolledBack fixture (fun s ->
            result {
                let! cashId = s.cashAccount ()
                let! agreementId, _ = s.agreement Outgo cashId 1
                let! _ = AgreementOrchestration.updateAgreement s.Context [] [] { noChange agreementId with directionUpdate = SetTo Income }
                let! after = agreementId |> AgreementOrchestration.fetchByMasterAgreementId s.Context
                Assert.Equal(Income, after |> AgreementOrchestration.masterAgreement |> MasterAgreement.direction)
            })

    [<Fact>]
    member _.``REQ-CF-14.2 an update to a master agreement that names no field to change is rejected with a typed error and the agreement is unchanged`` () =
        rolledBack fixture (fun s ->
            result {
                let! cashId = s.cashAccount ()
                let! agreementId, _ = s.agreement Outgo cashId 1
                let! before = agreementId |> AgreementOrchestration.fetchByMasterAgreementId s.Context
                let updating = s.Context |> TestContext.updateInitiationInstant
                let attempt = AgreementOrchestration.updateAgreement updating [] [] (noChange agreementId)
                let refused =
                    match attempt with
                    | Error (AsError CashFlowError.CashflowAgreementUpdateNoOp) -> true
                    | _ -> false
                Assert.True(refused, $"%A{attempt |> Result.mapError (fun e -> e.ToMessage())}")
                let! after = agreementId |> AgreementOrchestration.fetchByMasterAgreementId updating
                Assert.Equal(fieldsOf before, fieldsOf after)
                Assert.Equal(before |> AgreementOrchestration.masterAgreement |> MasterAgreement.modifiedAt,
                             after |> AgreementOrchestration.masterAgreement |> MasterAgreement.modifiedAt)
            })

    // =========================================================================
    // REQ-CF-6.5, REQ-CF-9.8 — a Payment's amount is its line's
    // =========================================================================

    [<Theory>]
    [<InlineData("staged line", "below")>]
    [<InlineData("staged line", "above")>]
    [<InlineData("journal entry line", "below")>]
    [<InlineData("journal entry line", "above")>]
    member _.``REQ-CF-6.5 REQ-CF-9.8 for each transaction pointer (staged line, journal entry line) and each payload amount (below and above the line's), creating a Payment against a line whose amount equals the amount of an Invoice with no other Payments leaves the Invoice FullyPaid and the Payment's amount reads back as the line's`` (pointer: string, payloadAmount: string) =
        (* CreatePayment goes through its route, which commits, so this test commits what it builds and deletes it
           afterwards. The payload is written as JSON so that it can claim an amount whether or not the contract still
           carries one: a property the contract doesn't have is ignored on reading. *)
        let agreements = ResizeArray<MasterAgreementId>()
        let stageEntries = ResizeArray<StageEntryHeaderId>()
        let journalEntries = ResizeArray<JournalEntryHeaderId>()
        let committed body = runCommandRouteAndAutoCompleteTransaction CashFlowCreatePayment body
        let idOf code =
            fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
        let fresh () = Context.create NoTransaction FetchOnly
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! invoiceId =
                    committed (fun context ->
                        result {
                            let s = Scenario(fixture, context)
                            let! agreementId, legIds = s.agreement Outgo (idOf "F-1280") 1
                            agreements.Add agreementId
                            return! s.invoice Outgo (idOf "F-1280") agreementId legIds[0] s.today s.today []
                        })
                (* An Outgo Payment lands on the leg's debit account, F-2230. *)
                let lines = [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                let! pointerJson =
                    committed (fun context ->
                        result {
                            let today = Calendar.today ()
                            match pointer with
                            | "staged line" ->
                                let testBank =
                                    fixture.Data.ingestionSources
                                    |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
                                let start = Clock.now ()
                                let! staged =
                                    createStageEntryForTest context "/tmp/revised-cash-flow.dat" $"Revised cash flow {Guid.NewGuid()}"
                                        (Guid.NewGuid().ToString()) testBank today lines
                                        [ (None, "Ingested", start, "StageIngestion")
                                          (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier") ]
                                stageEntries.Add(staged |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId)
                                let lineId =
                                    staged
                                    |> StageEntryOrchestration.seLines
                                    |> List.find (fun l -> l |> StageEntryLine.accountId = Some(idOf "F-2230"))
                                    |> StageEntryLine.stageEntryLineId
                                    |> StageEntryLineId.value
                                return $"{{\"Case\":\"Staged\",\"Fields\":[\"{lineId}\"]}}"
                            | _ ->
                                let! entry, headerId =
                                    createTestJournalEntryFromPrimitives context $"Revised cash flow {Guid.NewGuid()}" None today
                                        [ (idOf "F-2230", 100.00M, "Debit", None); (idOf "F-1280", 100.00M, "Credit", None) ] [] []
                                journalEntries.Add headerId
                                let lineId =
                                    entry
                                    |> JournalEntryOrchestration.jeLines
                                    |> List.find (fun l -> l |> JournalEntryLine.accountId = idOf "F-2230")
                                    |> JournalEntryLine.journalEntryLineId
                                    |> JournalEntryLineId.value
                                return $"{{\"Case\":\"Posted\",\"Fields\":[\"{lineId}\"]}}"
                        })
                let claimed = if payloadAmount = "below" then "60.00" else "140.00"
                let payload =
                    $"{{\"invoiceId\":\"{invoiceId |> InvoiceId.value}\",\"payment\":{{\"transactionPointer\":{pointerJson},"
                    + $"\"amount\":{claimed},\"postedToFiDate\":null,\"postedToLedgerDate\":null,\"memo\":null}}}}"
                let! _ = routeUiCommandForTesting "CashFlow" "CreatePayment" [] payload
                let! invoice = invoiceId |> Invoice.fetchById (fresh ())
                Assert.Equal(FullyPaid, ((invoice |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState))
                let! payments = [ invoiceId ] |> Payment.fetchByInvoiceIdList (fresh ())
                let payment = Assert.Single(payments)
                Assert.Equal(100.00M, ((payment |> Payment.amount) |> CashFlowComponent.PaymentAmount.value) |> Money.amount)
            }
            |> railroadWrapper
        finally
            [ for id in agreements do yield Cleanup.cleanUpMasterAgreementTree (Some(id |> MasterAgreementId.value))
              for id in stageEntries do yield Cleanup.cleanUpStageEntryHeaderId (Some id)
              for id in journalEntries do yield Cleanup.cleanUpJournalEntryId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

