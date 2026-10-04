module Tests.Integrated.CrossDomainOrchestration.CashFlowMaintenance

open App.Session
open App.Utility
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open App.DataAccessLayer.ExecuteReader
open Ui.InterfaceBridge.CommandRoute
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Utility.Json
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.TestError
open App.Utility.IAppError
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* An Outgo agreement on the fixture's cash flow accounts (debit F-2230, credit F-1280), monthly on the 1st, with one
   100.00 leg. Returns the agreement's id and its leg's id. *)
let private createAgreement (fixture: TestDataFixture) (context: Context.Context) (name: string) =
    result {
        let accountIdOf code =
            fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
        let today = Calendar.today()
        let firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
        let! agreementName = name |> AgreementName.create
        let! first = 1 |> Cadence.DateInMonthNumber.fromInt
        let! counterparty = "Cash flow maintenance test lender" |> Counterparty.create
        let! activityPeriod =
            ActivityPeriod.create (firstOfThisMonth.PlusMonths(-2)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
        let! legName = $"{name} leg" |> PaymentAgreementName.create
        let! expected = Money.fromDecimal 100.00M
        let! due = 0 |> DaysDueAfterInvoiceDate.create
        let! agreement =
            AgreementOrchestration.constructNewAndPersist
                context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                { nextInstance = firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None
                [ (legName, DebitAccount.create(accountIdOf "F-2230"), CreditAccount.create(accountIdOf "F-1280"), Some expected, Some due, None) ]
        let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
        let legId = agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
        return agreementId, legId
    }

/// A 100.00 Invoice with the given blocker, on its own Instance dated the first of the month monthsAgo months back.
/// Instances only go forward, so each call on one agreement needs a later month than the last. Returns its id.
let private createBlockedInvoice context agreementId legId (monthsAgo: int) (blocker: Blocker) =
    result {
        let today = Calendar.today()
        let invoiceDate = LocalDate(today.Year, today.Month, 1).PlusMonths(-monthsAgo)
        let! amount = Money.fromDecimal 100.00M
        let! created =
            InstanceOrchestration.constructNewAndPersist
                context agreementId invoiceDate
                [ (legId, None, InvoiceDate.create(invoiceDate), DueDate.create(invoiceDate), InvoiceAmount.create(amount),
                   InvoiceReceived, Some blocker, None, []) ]
        return created |> InstanceOrchestration.invoiceComposites |> List.head |> InstanceOrchestration.invoice |> Invoice.invoiceId
    }

/// A 100.00 Invoice with no blocker on a new agreement's first Instance, dated the first of last month. Returns the
/// agreement's id, its leg's id and the Instance composite.
let private createInvoicedInstance (fixture: TestDataFixture) context (name: string) =
    result {
        let! agreementId, legId = createAgreement fixture context name
        let today = Calendar.today()
        let invoiceDate = LocalDate(today.Year, today.Month, 1).PlusMonths(-1)
        let! amount = Money.fromDecimal 100.00M
        let! created =
            InstanceOrchestration.constructNewAndPersist
                context agreementId invoiceDate
                [ (legId, None, InvoiceDate.create(invoiceDate), DueDate.create(invoiceDate), InvoiceAmount.create(amount),
                   InvoiceReceived, None, None, []) ]
        return agreementId, legId, created
    }

let private noChangeInvoiceUpdates invoiceId : Invoice.InvoiceFieldUpdates =
    { invoiceIdToUpdate = invoiceId
      externalInvoiceIdUpdate = FieldUpdate.NoChange
      invoiceDateUpdate = FieldUpdate.NoChange
      dueDateUpdate = FieldUpdate.NoChange
      amountUpdate = FieldUpdate.NoChange
      invoiceStateUpdate = FieldUpdate.NoChange
      paymentStateUpdate = FieldUpdate.NoChange
      postedStateUpdate = FieldUpdate.NoChange
      blockerUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

let private invoiceCompositeUpdate invoiceId : InstanceOrchestration.InvoiceCompositeUpdate =
    { invoiceUpdates = noChangeInvoiceUpdates invoiceId
      paymentUpdates = []
      paymentIdsToDelete = []
      newPayments = [] }

let private instanceCompositeUpdate instanceId : InstanceOrchestration.InstanceCompositeUpdate =
    { instanceUpdates =
        { instanceIdToUpdate = instanceId
          isFulfilledUpdate = FieldUpdate.NoChange }
      invoiceCompositeUpdates = []
      newInvoices = [] }

let private instanceIdOf composite = composite |> InstanceOrchestration.instance |> Instance.instanceId

let private invoiceIdOf composite =
    composite |> InstanceOrchestration.invoiceComposites |> List.head |> InstanceOrchestration.invoice |> Invoice.invoiceId

/// A new 100.00 Invoice for the given leg, with no payments, dated the first of this month.
let private newInvoiceFor legId =
    result {
        let today = Calendar.today()
        let invoiceDate = LocalDate(today.Year, today.Month, 1)
        let! amount = Money.fromDecimal 100.00M
        return
            (legId, None, (InvoiceDate.create invoiceDate), (DueDate.create invoiceDate),
             (InvoiceAmount.create amount), InvoiceReceived, None, None, [])
    }

/// expectNotFound passes when the result is the expected error carrying the expected id, and fails otherwise.
let private expectNotFound (isExpected: IAppError -> System.Guid option) (expectedId: System.Guid) result : Result<unit, IAppError> =
    match result with
    | Error e ->
        match isExpected e with
        | Some uuid ->
            Assert.Equal(expectedId, uuid)
            Ok ()
        | None -> Error (TestingError $"Wrong error: {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
    | Ok _ -> Error (TestingError "Expected failure; got success")

let private noMasterAgreementChange agreementId : MasterAgreement.MasterAgreementFieldUpdates =
    { agreementIdToUpdate = agreementId
      agreementNameUpdate = FieldUpdate.NoChange
      directionUpdate = FieldUpdate.NoChange
      cadenceUpdate = FieldUpdate.NoChange
      counterpartyUpdate = FieldUpdate.NoChange
      activeBeginUpdate = FieldUpdate.NoChange
      activeEndUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

let private noLegChange legId : PaymentAgreement.PaymentAgreementFieldUpdates =
    { paymentAgreementIdToUpdate = legId
      paymentAgreementNameUpdate = FieldUpdate.NoChange
      debitAccountUpdate = FieldUpdate.NoChange
      creditAccountUpdate = FieldUpdate.NoChange
      expectedAmountUpdate = FieldUpdate.NoChange
      daysDueAfterInvoiceDateUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

/// Every stored agreement tree, read from a fresh context once a route's transaction is gone.
let private storedAgreements () =
    ({ agreementIds = None; activeAgreementsOnly = false } : AgreementFilter)
    |> AgreementOrchestration.fetchFiltered (Context.create NoTransaction FetchOnly) AnyQuantityIsAcceptable

let private storedInvoices () =
    storedAgreements ()
    |> Result.map (List.collect AgreementOrchestration.invoices >> List.sortBy (Invoice.invoiceId >> InvoiceId.value))

let private storedPayments () =
    storedAgreements ()
    |> Result.map (List.collect AgreementOrchestration.payments >> List.sortBy (Payment.paymentId >> PaymentId.value))

[<Collection("SharedTestData")>]
type CashFlowMaintenanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-SYS-6.2 updating an invoice by an ID no invoice holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = InvoiceId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! _, _, created = createInvoicedInstance fixture context "sys62 invoice update"
                let! memo = "REQ-SYS-6.2 no such invoice" |> InvoiceMemo.create
                let update =
                    { instanceCompositeUpdate (created |> instanceIdOf) with
                        invoiceCompositeUpdates =
                            [ { invoiceCompositeUpdate missingId with
                                  invoiceUpdates =
                                    { noChangeInvoiceUpdates missingId with memoUpdate = FieldUpdate.SetTo(Some memo) } } ] }
                return!
                    InstanceOrchestration.updateInstanceComposite context update
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowInvoiceIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> InvoiceId.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a payment agreement link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = PaymentAgreementLinkId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! _, legId = createAgreement fixture context "sys62 link update"
                return!
                    PaymentAgreementLink.update
                        context { linkIdToUpdate = missingId; paymentAgreementIdUpdate = FieldUpdate.SetTo legId }
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowPaymentAgreementLinkIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> PaymentAgreementLinkId.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 deleting a payment agreement link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = PaymentAgreementLinkId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            PaymentAgreementLink.delete context missingId
            |> expectNotFound
                (function AsError (CashFlowError.CashflowPaymentAgreementLinkIdDoesntExist uuid) -> Some uuid | _ -> None)
                (missingId |> PaymentAgreementLinkId.value))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 deleting a payment by an ID no payment holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = PaymentId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! _, _, created = createInvoicedInstance fixture context "sys62 payment delete"
                let invoiceId = created |> invoiceIdOf
                let update =
                    { instanceCompositeUpdate (created |> instanceIdOf) with
                        invoiceCompositeUpdates =
                            [ { invoiceCompositeUpdate invoiceId with paymentIdsToDelete = [ missingId ] } ] }
                return!
                    InstanceOrchestration.updateInstanceComposite context update
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowPaymentIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> PaymentId.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a payment pointing at a staged line that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        let missingId = DataIngestion.StageEntryComponent.StageEntryLineId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! _, _, created = createInvoicedInstance fixture context "sys63 staged pointer"
                let instanceId = created |> instanceIdOf
                let update =
                    { instanceCompositeUpdate instanceId with
                        invoiceCompositeUpdates =
                            [ { invoiceCompositeUpdate (created |> invoiceIdOf) with
                                  newPayments = [ (Staged missingId, None, None, None) ] } ] }
                do!
                    InstanceOrchestration.updateInstanceComposite context update
                    |> expectNotFound
                        (function AsError (DataIngestion.DataIngestionError.IngestionStageEntryLineIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> DataIngestion.StageEntryComponent.StageEntryLineId.value)
                let! after = instanceId |> InstanceOrchestration.fetchCompositeByInstanceId context
                Assert.Equal(created, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a payment pointing at a journal entry line that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        let missingId = JournalEntryComponent.JournalEntryLineId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! _, _, created = createInvoicedInstance fixture context "sys63 posted pointer"
                let instanceId = created |> instanceIdOf
                let update =
                    { instanceCompositeUpdate instanceId with
                        invoiceCompositeUpdates =
                            [ { invoiceCompositeUpdate (created |> invoiceIdOf) with
                                  newPayments = [ (Posted(missingId, None), None, None, None) ] } ] }
                do!
                    InstanceOrchestration.updateInstanceComposite context update
                    |> expectNotFound
                        (function AsError (LedgerError.JournalEntryLineIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> JournalEntryComponent.JournalEntryLineId.value)
                let! after = instanceId |> InstanceOrchestration.fetchCompositeByInstanceId context
                Assert.Equal(created, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.3 creating an invoice on an instance that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        let missingId = InstanceId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! agreementId, legId = createAgreement fixture context "sys63 missing instance"
                let invoicesOfAgreement () =
                    agreementId |> AgreementOrchestration.fetchByMasterAgreementId context
                    |> Result.map AgreementOrchestration.invoices
                let! before = invoicesOfAgreement ()
                let! newInvoice = newInvoiceFor legId
                do!
                    { instanceCompositeUpdate missingId with newInvoices = [ newInvoice ] }
                    |> InstanceOrchestration.updateInstanceComposite context
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowInstanceIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> InstanceId.value)
                let! after = invoicesOfAgreement ()
                Assert.Equal<Invoice.Invoice list>(before, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.3 creating an invoice for a payment agreement that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        let missingId = PaymentAgreementId.create ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! _, _, created = createInvoicedInstance fixture context "sys63 missing agreement"
                let instanceId = created |> instanceIdOf
                let! newInvoice = newInvoiceFor missingId
                do!
                    { instanceCompositeUpdate instanceId with newInvoices = [ newInvoice ] }
                    |> InstanceOrchestration.updateInstanceComposite context
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowPaymentAgreementIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> PaymentAgreementId.value)
                let! after = instanceId |> InstanceOrchestration.fetchCompositeByInstanceId context
                Assert.Equal(created, after)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-SYS-6.2, 6.3 — a fresh Guid reaches the domain's not-found error, never a generic row-count error
    // =========================================================================

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a Master Agreement by an ID no agreement holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = MasterAgreementId.create ()
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! counterparty = "REQ-SYS-6.2 no such agreement" |> Counterparty.create
                return!
                    { noMasterAgreementChange missingId with counterpartyUpdate = FieldUpdate.SetTo counterparty }
                    |> AgreementOrchestration.updateAgreement context [] []
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowMasterAgreementIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> MasterAgreementId.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a Payment Agreement by an ID no agreement holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = PaymentAgreementId.create ()
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! legName = "REQ-SYS-6.2 no such leg" |> PaymentAgreementName.create
                return!
                    noMasterAgreementChange fixture.Data.cashFlow.agreementAId
                    |> AgreementOrchestration.updateAgreement
                        context [ { noLegChange missingId with paymentAgreementNameUpdate = FieldUpdate.SetTo legName } ] []
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowPaymentAgreementIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> PaymentAgreementId.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 an UpdateInvoice payload whose Invoice ID names no Invoice fails with a typed not-found error naming the kind of record and the ID, and nothing is changed`` () =
        let missingId = InvoiceId.create ()
        result {
            let! before = storedInvoices ()
            let! payload =
                ({ invoiceId = missingId |> InvoiceId.value
                   externalInvoiceIdUpdate = FieldUpdate.NoChange
                   invoiceDateUpdate = FieldUpdate.NoChange
                   dueDateUpdate = FieldUpdate.NoChange
                   amountUpdate = FieldUpdate.NoChange
                   invoiceStateUpdate = FieldUpdate.NoChange
                   blockerUpdate = FieldUpdate.NoChange
                   memoUpdate = FieldUpdate.SetTo(Some "REQ-SYS-6.2 no such invoice") } : Contracts.UpdateInvoiceInput)
                |> Json.toJson
            do!
                routeUiCommandForTesting "CashFlow" "UpdateInvoice" [] payload
                |> expectNotFound
                    (function AsError (CashFlowError.CashflowInvoiceIdDoesntExist uuid) -> Some uuid | _ -> None)
                    (missingId |> InvoiceId.value)
            let! after = storedInvoices ()
            Assert.Equal<Invoice.Invoice list>(before, after)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.3 a CreatePayment payload whose Invoice ID names no Invoice fails with a typed not-found error naming the missing Invoice, and no Payment is stored`` () =
        let missingId = InvoiceId.create ()
        let mutable paymentIdsToCleanUp : System.Guid list = []
        try
            result {
                let! before = storedPayments ()
                let! payload =
                    ({ invoiceId = missingId |> InvoiceId.value
                       payment =
                         { transactionPointer =
                             Contracts.TransactionPointerContract.Posted(
                                 fixture.Data.cashFlow.unclaimedLedgerLineId |> JournalEntryComponent.JournalEntryLineId.value)
                           postedToFiDate = None
                           postedToLedgerDate = None
                           memo = None } } : Contracts.CreatePaymentInput)
                    |> Json.toJson
                let attempt = routeUiCommandForTesting "CashFlow" "CreatePayment" [] payload
                // a success would have stored a Payment; capture it for the finally before asserting anything
                let beforeIds = before |> List.map (Payment.paymentId >> PaymentId.value)
                paymentIdsToCleanUp <-
                    match attempt |> Result.bind Json.fromJson<Contracts.InstanceCompositeReturn> with
                    | Ok returned ->
                        returned.invoiceComposites
                        |> List.collect _.payments
                        |> List.map _.paymentId
                        |> List.filter (fun id -> beforeIds |> List.contains id |> not)
                    | Error _ -> []
                do!
                    attempt
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowInvoiceIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> InvoiceId.value)
                let! after = storedPayments ()
                Assert.Equal<Payment.Payment list>(before, after)
            }
            |> railroadWrapper
        finally
            for paymentId in paymentIdsToCleanUp do
                ({ paymentId = paymentId } : Contracts.DeletePaymentInput)
                |> Json.toJson
                |> Result.bind (routeUiCommandForTesting "CashFlow" "DeletePayment" [])
                |> ignore

    [<Fact>]
    member _.``REQ-SYS-6.2 a payload deleting a Payment Agreement Link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = PaymentAgreementLinkId.create ()
        result {
            let! payload =
                ({ paymentAgreementLinkId = missingId |> PaymentAgreementLinkId.value }
                 : Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts.DeletePaymentAgreementLinkInput)
                |> Json.toJson
            return!
                routeUiCommandForTesting "Classification" "DeletePaymentAgreementLink" [] payload
                |> expectNotFound
                    (function AsError (CashFlowError.CashflowPaymentAgreementLinkIdDoesntExist uuid) -> Some uuid | _ -> None)
                    (missingId |> PaymentAgreementLinkId.value)
        }
        |> railroadWrapper
