module Tests.Integrated.CrossDomainOrchestration.DerivedStateRules

open System
open System.Text.Json.Nodes
open App.DataAccessLayer.DbTransaction
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

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Every test builds its own Outgo agreement (debit F-2230, credit F-1280, monthly on the 1st) with two 100.00 legs,
   and its own Instances. A Posted Payment points at the F-2230 line of a journal entry; a Staged Payment points at the
   F-2230 line of a Classified staged entry. Most tests roll their transaction back. The REQ-CF-9.11 tests go through the
   routes, which commit, so they read back from a fresh context and delete what they made in a finally. *)

type private Pay =
    | PostedPay of decimal
    | StagedPay of decimal

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today()
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cash = accountIdOf "F-1280"
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
    let headerIdList = ResizeArray<JournalEntryHeaderId>()

    member _.Context = context
    member _.firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
    member _.cashId = cash
    member _.journalEntryHeaderIds = headerIdList |> List.ofSeq

    /// Returns the agreement's id, its name, and its two legs' ids and names.
    member this.agreement (name: string) =
        result {
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Derived state test lender" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (this.firstOfThisMonth.PlusMonths(-3)) None
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let legNames = [ $"{name} leg A"; $"{name} leg B" ]
            let! legs =
                legNames
                |> List.map (fun legName ->
                    result {
                        let! paName = legName |> PaymentAgreementName.create
                        let! expected = Money.fromDecimal 100.00M
                        let! due = 0 |> DaysDueAfterInvoiceDate.create
                        return (paName, DebitAccount loanId, CreditAccount cash, Some expected, Some due, None)
                    })
                |> convertListOfResultsToResultsList
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = this.firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None legs
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legIds =
                legNames
                |> List.map (fun legName ->
                    agreement
                    |> AgreementOrchestration.paymentAgreements
                    |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                    |> PaymentAgreement.paymentAgreementId)
            return agreementId, legIds, legNames
        }

    /// A journal entry for the amount; returns its line on the account.
    member this.ledgerLineOn (accountId: AccountId) (amount: decimal) =
        result {
            let other = if accountId = loanId then cash else loanId
            let debitOn, creditOn = accountId, other
            let! entry, headerId =
                createTestJournalEntryFromPrimitives
                    context $"Derived state test payment {Guid.NewGuid()}" None this.firstOfThisMonth
                    [ (debitOn, amount, "Debit", None); (creditOn, amount, "Credit", None) ] [] []
            headerIdList.Add headerId
            return
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = accountId)
                |> JournalEntryLine.journalEntryLineId
        }

    /// A Classified staged entry for the amount; returns its F-2230 line.
    member this.stagedLine (amount: decimal) =
        result {
            let start = Clock.now()
            let! staged =
                createStageEntryForTest context "/tmp/derived-state-test.dat" $"Derived state test {Guid.NewGuid()}"
                    (Guid.NewGuid().ToString()) testBank this.firstOfThisMonth
                    [ (amount, "Debit", Some "F-2230", None, None); (amount, "Credit", Some "F-1280", None, None) ]
                    [ (None, "Ingested", start, "StageIngestion")
                      (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier") ]
            return
                staged
                |> StageEntryOrchestration.seLines
                |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit)
                |> StageEntryLine.stageEntryLineId
        }

    member this.newPayment (pay: Pay) =
        result {
            let! pointer, amount =
                match pay with
                | PostedPay amount -> this.ledgerLineOn loanId amount |> Result.map (fun line -> Posted line, amount)
                | StagedPay amount -> this.stagedLine amount |> Result.map (fun line -> Staged line, amount)
            let! money = Money.fromDecimal amount
            return (pointer, { PaymentAmount.money = money }, None, None, None)
        }

    /// An Instance dated the 1st of this month with a 100.00 Invoice for each (leg, blocker, Payments) given. Returns the
    /// Instance's id and, per Invoice in the order given, its id and its Payments' ids in the order given.
    member this.instance agreementId (invoices: (PaymentAgreementId * Blocker option * Pay list) list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let date = this.firstOfThisMonth
            let! invoiceFields =
                invoices
                |> List.map (fun (legId, blocker, pays) ->
                    result {
                        let! payments = pays |> List.map this.newPayment |> convertListOfResultsToResultsList
                        return
                            (legId, None, { InvoiceDate.localDate = date }, { DueDate.localDate = date.PlusDays(30) },
                             { InvoiceAmount.money = amount }, InvoiceReceived, blocker, None, payments)
                    })
                |> convertListOfResultsToResultsList
            let! created = InstanceOrchestration.createInstanceCompositeAndSaveToDb context agreementId date invoiceFields
            let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
            let composites = created |> InstanceOrchestration.invoiceComposites
            let perInvoice =
                invoiceFields
                |> List.map (fun (legId, _, _, _, _, _, _, _, payments) ->
                    let composite =
                        composites |> List.find (fun c -> c |> InstanceOrchestration.invoice |> Invoice.paymentAgreementId = legId)
                    let createdPayments = composite |> InstanceOrchestration.payments
                    let paymentIds =
                        payments
                        |> List.map (fun (pointer, _, _, _, _) ->
                            createdPayments |> List.find (fun p -> p |> Payment.transactionPointer = pointer) |> Payment.paymentId)
                    (composite |> InstanceOrchestration.invoice |> Invoice.invoiceId), paymentIds)
            return instanceId, perInvoice
        }

    /// The Invoice's payment state and posted state, read back.
    member _.statesOf (invoiceId: InvoiceId) =
        invoiceId
        |> Invoice.fetchById context
        |> Result.map (fun invoice ->
            let state = invoice |> Invoice.invoiceLifeCycleState
            state.paymentState, state.postedState)

    member _.invoiceOf (invoiceId: InvoiceId) = invoiceId |> Invoice.fetchById context

    member _.isFulfilled (instanceId: InstanceId) =
        instanceId |> Instance.fetchById context |> Result.map Instance.isFulfilled

    member _.compositeOf (instanceId: InstanceId) = instanceId |> InstanceOrchestration.fetchCompositeByInstanceId context

    member _.paymentIdsOf (invoiceId: InvoiceId) =
        [ invoiceId ] |> Payment.fetchByInvoiceIdList context |> Result.map (List.map Payment.paymentId)

/// An update to one Invoice of the Instance that changes nothing unless the caller says so.
let private invoiceUpdate (instanceId: InstanceId) (invoiceId: InvoiceId) : InstanceOrchestration.InstanceCompositeUpdate =
    { instanceUpdates = { instanceIdToUpdate = instanceId; isFulfilledUpdate = NoChange }
      invoiceCompositeUpdates =
        [ { invoiceUpdates =
              { invoiceIdToUpdate = invoiceId
                externalInvoiceIdUpdate = NoChange
                invoiceDateUpdate = NoChange
                dueDateUpdate = NoChange
                amountUpdate = NoChange
                invoiceStateUpdate = NoChange
                paymentStateUpdate = NoChange
                postedStateUpdate = NoChange
                blockerUpdate = NoChange
                memoUpdate = NoChange }
            paymentUpdates = []
            paymentIdsToDelete = []
            newPayments = [] } ]
      newInvoices = [] }

let private withInvoiceChange change (update: InstanceOrchestration.InstanceCompositeUpdate) =
    { update with invoiceCompositeUpdates = update.invoiceCompositeUpdates |> List.map change }

let private isFullyPaidWithBlocker (r: Result<'a, IAppError>) =
    match r with
    | Error (AsError (CashFlowError.CashflowInvoiceFullyPaidWithBlocker _)) -> true
    | _ -> false

let private fresh () = Context.create NoTransaction FetchOnly

/// Adds each (property, value) to the JSON object.
let private addProperties (properties: (string * JsonNode) list) (node: JsonNode) =
    let o = node.AsObject()
    for name, value in properties do o[name] <- value

[<Collection("SharedTestData")>]
type DerivedStateRulesTests(fixture: TestDataFixture) =

    let rolledBack (body: Scenario -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context -> body (Scenario(fixture, context)))
        |> railroadWrapper

    (* Runs setup in a committed transaction, then the test, then deletes the agreement (with everything under it) and
       any journal entries the setup made. *)
    let committed (setup: Scenario -> Result<'a, IAppError>) (agreementIdOf: 'a -> MasterAgreementId) (test: 'a -> Result<unit, IAppError>) =
        let mutable agreementId: Guid option = None
        let mutable headerIds: JournalEntryHeaderId list = []
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! made =
                    runCommandRouteAndAutoCompleteTransaction CashFlowCreateInstance (fun context ->
                        let s = Scenario(fixture, context)
                        let made = setup s
                        headerIds <- s.journalEntryHeaderIds
                        made |> Result.map (fun m -> agreementId <- Some(m |> agreementIdOf |> MasterAgreementId.value); m))
                return! test made
            }
            |> railroadWrapper
        finally
            [ yield Cleanup.cleanUpMasterAgreementTree agreementId
              for id in headerIds do yield Cleanup.cleanUpJournalEntryId (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

    // =========================================================================
    // REQ-CF-9.1, 9.2 — the sum
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-9.1 an Invoice whose Payments sum to one cent less than its amount derives PartiallyPaid, not FullyPaid`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.1 one cent short"
                let! _, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 60.00M; PostedPay 39.99M ]) ]
                let! paymentState, _ = s.statesOf (fst invoices[0])
                Assert.Equal(PartiallyPaid, paymentState)
            })

    [<Fact>]
    member _.``REQ-CF-9.1 an Invoice whose Payments sum to more than its amount derives PartiallyPaid, not FullyPaid`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.1 overpaid"
                let! _, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 60.00M; PostedPay 40.01M ]) ]
                let! paymentState, _ = s.statesOf (fst invoices[0])
                Assert.Equal(PartiallyPaid, paymentState)
            })

    [<Fact>]
    member _.``REQ-CF-9.2 an Invoice whose every Payment is Posted but whose Payments sum to less than its amount derives PartiallyPosted, not PostedToLedger`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.2 all posted short"
                let! _, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 50.00M; PostedPay 30.00M ]) ]
                let! _, postedState = s.statesOf (fst invoices[0])
                Assert.Equal(PartiallyPosted, postedState)
            })

    // =========================================================================
    // REQ-CF-9.3, 9.4 — the blocker
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-9.3 adding a Payment that would make an Invoice carrying a blocker FullyPaid is rejected with a typed error, no Payment is written, and the Invoice keeps its prior payment state`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.3 blocked add"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], Some NoFunds, [ PostedPay 60.00M ]) ]
                let invoiceId, paymentIds = invoices[0]
                let! completing = s.newPayment (PostedPay 40.00M)
                let update =
                    invoiceUpdate instanceId invoiceId
                    |> withInvoiceChange (fun u -> { u with newPayments = [ completing ] })
                let attempt = update |> InstanceOrchestration.updateInstanceComposite s.Context
                Assert.True(attempt |> isFullyPaidWithBlocker)
                let! paymentsAfter = s.paymentIdsOf invoiceId
                let! paymentState, _ = s.statesOf invoiceId
                Assert.Equal<PaymentId list>(paymentIds, paymentsAfter)
                Assert.Equal(PartiallyPaid, paymentState)
            })

    [<Fact>]
    member _.``REQ-CF-9.3 reducing a blocked Invoice's amount to equal its Payment sum is rejected and the Invoice keeps its prior amount`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.3 blocked amount"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], Some NoFunds, [ PostedPay 60.00M ]) ]
                let invoiceId, _ = invoices[0]
                let! sixty = Money.fromDecimal 60.00M
                let update =
                    invoiceUpdate instanceId invoiceId
                    |> withInvoiceChange (fun u ->
                        { u with invoiceUpdates = { u.invoiceUpdates with amountUpdate = SetTo { money = sixty } } })
                let attempt = update |> InstanceOrchestration.updateInstanceComposite s.Context
                Assert.True(attempt |> isFullyPaidWithBlocker)
                let! invoice = s.invoiceOf invoiceId
                Assert.Equal(100.00M, (invoice |> Invoice.amount).money |> Money.amount)
            })

    [<Fact>]
    member _.``REQ-CF-9.4 setting a blocker on a FullyPaid Invoice is rejected with a typed error, and the Invoice keeps no blocker`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.4 blocker on paid"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 100.00M ]) ]
                let invoiceId, _ = invoices[0]
                let update =
                    invoiceUpdate instanceId invoiceId
                    |> withInvoiceChange (fun u ->
                        { u with invoiceUpdates = { u.invoiceUpdates with blockerUpdate = SetTo(Some NoFunds) } })
                let attempt = update |> InstanceOrchestration.updateInstanceComposite s.Context
                Assert.True(attempt |> isFullyPaidWithBlocker)
                let! invoice = s.invoiceOf invoiceId
                Assert.Equal(None, (invoice |> Invoice.invoiceLifeCycleState).blocker)
            })

    // =========================================================================
    // REQ-CF-9.5 through 9.10 — derivation as Payments come and go
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-9.5 REQ-CF-9.10 deleting an Invoice's only Payment returns it to NotYetPaid and NotHandled and its Instance to unfulfilled, not PartiallyPaid`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.5 delete only"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 100.00M ]) ]
                let invoiceId, paymentIds = invoices[0]
                let! fulfilledBefore = s.isFulfilled instanceId
                Assert.True(fulfilledBefore)
                let! _ = CashFlowOps.deletePaymentAndItsLinkage s.Context paymentIds[0]
                let! states = s.statesOf invoiceId
                let! fulfilled = s.isFulfilled instanceId
                Assert.Equal((NotYetPaid, NotHandled), states)
                Assert.False(fulfilled)
            })

    [<Fact>]
    member _.``REQ-CF-9.6 a FullyPaid Invoice with one Posted and one Staged Payment derives PartiallyPosted, not PostedToLedger`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.6 mixed"
                let! _, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 60.00M; StagedPay 40.00M ]) ]
                let! states = s.statesOf (fst invoices[0])
                Assert.Equal((FullyPaid, PartiallyPosted), states)
            })

    [<Fact>]
    member _.``REQ-CF-9.7 an Invoice with Payments but none Posted derives NotHandled, not PartiallyPosted, whether PartiallyPaid or FullyPaid`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.7 none posted"
                let! _, invoices =
                    s.instance agreementId
                        [ (legIds[0], None, [ StagedPay 40.00M ]); (legIds[1], None, [ StagedPay 100.00M ]) ]
                let! partly = s.statesOf (fst invoices[0])
                let! fully = s.statesOf (fst invoices[1])
                Assert.Equal((PartiallyPaid, NotHandled), partly)
                Assert.Equal((FullyPaid, NotHandled), fully)
            })

    [<Fact>]
    member _.``REQ-CF-9.10 deleting one Payment from a FullyPaid, PostedToLedger Invoice returns it to PartiallyPaid and PartiallyPosted and its Instance to unfulfilled, with no call other than the delete`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.10 delete one"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 60.00M; PostedPay 40.00M ]) ]
                let invoiceId, paymentIds = invoices[0]
                let! before = s.statesOf invoiceId
                Assert.Equal((FullyPaid, PostedToLedger), before)
                let! _ = CashFlowOps.deletePaymentAndItsLinkage s.Context paymentIds[1]
                let! after = s.statesOf invoiceId
                let! fulfilled = s.isFulfilled instanceId
                Assert.Equal((PartiallyPaid, PartiallyPosted), after)
                Assert.False(fulfilled)
            })

    [<Fact>]
    member _.``REQ-CF-9.10 adding the Payment that completes an existing Invoice's amount makes it FullyPaid and its Instance fulfilled, with no call other than the add`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.10 add completing"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 60.00M ]) ]
                let invoiceId, _ = invoices[0]
                let! completing = s.newPayment (PostedPay 40.00M)
                let! _ =
                    invoiceUpdate instanceId invoiceId
                    |> withInvoiceChange (fun u -> { u with newPayments = [ completing ] })
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let! paymentState, _ = s.statesOf invoiceId
                let! fulfilled = s.isFulfilled instanceId
                Assert.Equal(FullyPaid, paymentState)
                Assert.True(fulfilled)
            })

    [<Fact>]
    member _.``REQ-CF-9.10 an Instance with one FullyPaid and one PartiallyPaid Invoice is not fulfilled`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.10 half fulfilled"
                let! instanceId, invoices =
                    s.instance agreementId
                        [ (legIds[0], None, [ PostedPay 100.00M ]); (legIds[1], None, [ PostedPay 40.00M ]) ]
                let! fully, _ = s.statesOf (fst invoices[0])
                let! partly, _ = s.statesOf (fst invoices[1])
                let! fulfilled = s.isFulfilled instanceId
                Assert.Equal((FullyPaid, PartiallyPaid), (fully, partly))
                Assert.False(fulfilled)
            })

    [<Fact>]
    member _.``REQ-CF-9.10 a Payment change whose composite validation fails leaves the Payment, the Invoice's derived states and the Instance's is-fulfilled exactly as before`` () =
        (* The new Payment would complete the Invoice, but the journal entry line it points at does not exist. *)
        rolledBack (fun s ->
            result {
                let! agreementId, legIds, _ = s.agreement "CF-9.10 failed change"
                let! instanceId, invoices = s.instance agreementId [ (legIds[0], None, [ PostedPay 60.00M ]) ]
                let invoiceId, _ = invoices[0]
                let! before = s.compositeOf instanceId
                let missingLine = JournalEntryLineId.create ()
                let! forty = Money.fromDecimal 40.00M
                let attempt =
                    invoiceUpdate instanceId invoiceId
                    |> withInvoiceChange (fun u ->
                        { u with newPayments = [ (Posted missingLine, { PaymentAmount.money = forty }, None, None, None) ] })
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let rejectedForMissingLine =
                    match attempt with
                    | Error (AsError (LedgerError.JournalEntryLineIdDoesntExist uuid)) ->
                        uuid = (missingLine |> JournalEntryLineId.value)
                    | _ -> false
                Assert.True(rejectedForMissingLine)
                let! after = s.compositeOf instanceId
                Assert.Equal(before, after)
            })

    // =========================================================================
    // REQ-CF-9.11 — no caller sets a derived field
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-9.11 a CreateInstance payload supplying FullyPaid, PostedToLedger and is-fulfilled true for an Invoice with no Payments stores NotYetPaid, NotHandled and unfulfilled when re-fetched`` () =
        committed
            (fun s ->
                s.agreement $"CF-9.11 create instance {Guid.NewGuid()}"
                |> Result.map (fun (agreementId, _, legNames) -> agreementId, legNames, s.firstOfThisMonth))
            (fun (agreementId, _, _) -> agreementId)
            (fun (agreementId, legNames, date) ->
                result {
                    let! master = agreementId |> MasterAgreement.fetchById (fresh ())
                    let invoice : Contracts.NewInvoiceFieldsInput =
                        { paymentAgreementName = legNames[0]
                          externalInvoiceId = None
                          invoiceDate = date
                          dueDate = date.PlusDays(30)
                          amount = 100.00M
                          invoiceState = "InvoiceReceived"
                          blocker = None
                          memo = None
                          payments = [] }
                    let input : Contracts.CreateInstanceInput =
                        { masterAgreementName = master |> MasterAgreement.agreementName |> AgreementName.value
                          instanceDate = date
                          invoices = [ invoice ] }
                    let! json = Json.toJson input
                    let node = JsonNode.Parse(json)
                    node |> addProperties [ ("isFulfilled", JsonValue.Create(true) :> JsonNode) ]
                    node["invoices"].AsArray()
                    |> Seq.iter (addProperties
                            [ ("paymentState", JsonValue.Create("FullyPaid") :> JsonNode)
                              ("postedState", JsonValue.Create("PostedToLedger") :> JsonNode) ])
                    let! returned = routeUiCommandForTesting "CashFlow" "CreateInstance" [] (node.ToJsonString())
                    let! composite = Json.fromJson<Contracts.InstanceCompositeReturn> returned
                    let! stored =
                        composite.instance.instanceId |> InstanceId.fromGuid
                        |> InstanceOrchestration.fetchCompositeByInstanceId (fresh ())
                    let invoice = stored |> InstanceOrchestration.invoiceComposites |> List.exactlyOne |> InstanceOrchestration.invoice
                    let state = invoice |> Invoice.invoiceLifeCycleState
                    Assert.Equal((NotYetPaid, NotHandled), (state.paymentState, state.postedState))
                    Assert.False(stored |> InstanceOrchestration.instance |> Instance.isFulfilled)
                })

    [<Fact>]
    member _.``REQ-CF-9.11 a CreatePayment payload supplying payment state and posted state for its Invoice leaves the Invoice with the states its Payments derive when re-fetched`` () =
        (* A Posted 40.00 Payment on a 100.00 Invoice derives PartiallyPaid and PartiallyPosted; the payload claims
           FullyPaid and PostedToLedger. *)
        committed
            (fun s ->
                result {
                    let! agreementId, legIds, _ = s.agreement $"CF-9.11 create payment {Guid.NewGuid()}"
                    let! _, invoices = s.instance agreementId [ (legIds[0], None, []) ]
                    let! line = s.ledgerLineOn (fixture.Data.accounts
                                                |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-2230")
                                                |> Account.accountId) 40.00M
                    return agreementId, fst invoices[0], line
                })
            (fun (agreementId, _, _) -> agreementId)
            (fun (_, invoiceId, line) ->
                result {
                    let input : Contracts.CreatePaymentInput =
                        { invoiceId = invoiceId |> InvoiceId.value
                          payment =
                            { transactionPointer = Contracts.TransactionPointerContract.Posted(line |> JournalEntryLineId.value)
                              postedToFiDate = None
                              postedToLedgerDate = None
                              memo = None } }
                    let! json = Json.toJson input
                    let node = JsonNode.Parse(json)
                    let claimed () =
                        [ ("paymentState", JsonValue.Create("FullyPaid") :> JsonNode)
                          ("postedState", JsonValue.Create("PostedToLedger") :> JsonNode) ]
                    node |> addProperties (claimed ())
                    node["payment"] |> addProperties (claimed ())
                    let! _ = routeUiCommandForTesting "CashFlow" "CreatePayment" [] (node.ToJsonString())
                    let! invoice = invoiceId |> Invoice.fetchById (fresh ())
                    let state = invoice |> Invoice.invoiceLifeCycleState
                    Assert.Equal((PartiallyPaid, PartiallyPosted), (state.paymentState, state.postedState))
                })
