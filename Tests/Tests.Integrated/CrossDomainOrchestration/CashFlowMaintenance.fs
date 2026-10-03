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
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open App.Utility.IAppError
open Xunit

let private noAgreementFilter : AgreementFilter =
    { agreementIds = None
      agreementNames = None
      direction = None
      activeAgreementsOnly = false
      accountIds = None
      paymentAgreementExpectedAmount = None
      instanceTemporalFilter = None
      externalInvoiceId = None
      invoiceDateTemporalFilter = None
      invoiceDueTemporalFilter = None
      invoiceAmount = None
      invoiceState = None
      invoicePaymentState = None
      invoicePostedState = None
      invoiceBlocker = None
      journalEntryLineId = None
      stageEntryLineId = None
      paymentAmount = None
      paymentPostedToLedgerTemporalFilter = None }

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
                [ (legName, DebitAccount(accountIdOf "F-2230"), CreditAccount(accountIdOf "F-1280"), Some expected, Some due, None) ]
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
            InstanceOrchestration.createInstanceCompositeAndSaveToDb
                context agreementId invoiceDate
                [ (legId, None, { localDate = invoiceDate }, { localDate = invoiceDate }, { money = amount },
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
            InstanceOrchestration.createInstanceCompositeAndSaveToDb
                context agreementId invoiceDate
                [ (legId, None, { localDate = invoiceDate }, { localDate = invoiceDate }, { money = amount },
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
          instanceDateUpdate = FieldUpdate.NoChange
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
            (legId, None, ({ localDate = invoiceDate }: InvoiceDate), ({ localDate = invoiceDate }: DueDate),
             ({ money = amount }: InvoiceAmount), InvoiceReceived, None, None, [])
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

[<Collection("SharedTestData")>]
type CashFlowMaintenanceTests(fixture: TestDataFixture) =

    [<Theory>]
    [<InlineData("%")>]
    [<InlineData("_")>]
    [<InlineData(@"\")>]
    member _.``REQ-SYS-1.4 master agreement name filter returns every record containing search text with a literal %, _ or \ and no record lacking it`` (special: string) =
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let case = LiteralSearch.case "sys14agreement" special
                let! _ = createAgreement fixture context case.containing
                let! _ = createAgreement fixture context case.decoy
                let! search = case.search |> AgreementName.create
                let! found =
                    { noAgreementFilter with agreementNames = Some [ search ] }
                    |> AgreementOrchestration.fetchFiltered context AnyQuantityIsAcceptable
                let names =
                    found
                    |> List.map (fun a ->
                        a |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementName |> AgreementName.value)
                Assert.Contains(case.containing, names)
                Assert.DoesNotContain(case.decoy, names)
                Assert.All(names, fun n -> Assert.Contains(case.search, n))
            })
        |> railroadWrapper

    // a blocker's text is its note, so the search is a NeedsDecision blocker whose note carries the special character
    [<Theory>]
    [<InlineData("%")>]
    [<InlineData("_")>]
    [<InlineData(@"\")>]
    member _.``REQ-SYS-1.4 invoice blocker filter returns every record containing search text with a literal %, _ or \ and no record lacking it`` (special: string) =
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let case = LiteralSearch.case "sys14blocker" special
                let! agreementId, legId = createAgreement fixture context "sys14 blocker agreement"
                let needsDecision text = text |> BlockerNote.create |> Result.map NeedsDecision
                let! containingBlocker = needsDecision case.containing
                let! decoyBlocker = needsDecision case.decoy
                let! containingId = createBlockedInvoice context agreementId legId 1 containingBlocker
                let! decoyId = createBlockedInvoice context agreementId legId 0 decoyBlocker
                let! searchBlocker = needsDecision case.search
                let! found =
                    { noAgreementFilter with invoiceBlocker = Some searchBlocker }
                    |> InstanceOrchestration.fetchFiltered context AnyQuantityIsAcceptable
                let invoices = found |> List.map InstanceOrchestration.invoice
                let ids = invoices |> List.map Invoice.invoiceId
                Assert.Contains(containingId, ids)
                Assert.DoesNotContain(decoyId, ids)
                Assert.All(invoices, fun invoice ->
                    match invoice |> Invoice.invoiceLifeCycleState |> _.blocker with
                    | Some(NeedsDecision note) -> Assert.Contains(case.search, note |> BlockerNote.value)
                    | other -> Assert.Fail $"Expected a NeedsDecision blocker carrying the search text; got {other}")
            })
        |> railroadWrapper

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
                let! amount = Money.fromDecimal 40.00M
                let update =
                    { instanceCompositeUpdate instanceId with
                        invoiceCompositeUpdates =
                            [ { invoiceCompositeUpdate (created |> invoiceIdOf) with
                                  newPayments = [ (Staged missingId, { money = amount }, None, None, None) ] } ] }
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
                let! amount = Money.fromDecimal 40.00M
                let update =
                    { instanceCompositeUpdate instanceId with
                        invoiceCompositeUpdates =
                            [ { invoiceCompositeUpdate (created |> invoiceIdOf) with
                                  newPayments = [ (Posted missingId, { money = amount }, None, None, None) ] } ] }
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
                let byAgreement = { noAgreementFilter with agreementIds = Some [ agreementId ] }
                let! before = byAgreement |> InstanceOrchestration.fetchFiltered context AnyQuantityIsAcceptable
                let! newInvoice = newInvoiceFor legId
                do!
                    { instanceCompositeUpdate missingId with newInvoices = [ newInvoice ] }
                    |> InstanceOrchestration.updateInstanceComposite context
                    |> expectNotFound
                        (function AsError (CashFlowError.CashflowInstanceIdDoesntExist uuid) -> Some uuid | _ -> None)
                        (missingId |> InstanceId.value)
                let! after = byAgreement |> InstanceOrchestration.fetchFiltered context AnyQuantityIsAcceptable
                Assert.Equal<InstanceOrchestration.InvoiceComposite list>(before, after)
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

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a Master Agreement by an ID no agreement holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a Payment Agreement by an ID no agreement holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 an UpdateInvoice payload whose Invoice ID names no Invoice fails with a typed not-found error naming the kind of record and the ID, and nothing is changed`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 a CreatePayment payload whose Invoice ID names no Invoice fails with a typed not-found error naming the missing Invoice, and no Payment is stored`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 a payload deleting a Payment Agreement Link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "Not yet implemented"
