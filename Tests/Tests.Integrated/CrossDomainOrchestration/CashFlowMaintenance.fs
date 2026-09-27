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
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a payment agreement link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 deleting a payment agreement link by an ID no link holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.2 deleting a payment by an ID no payment holds fails with a typed not-found error naming the kind of record and the ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a payment pointing at a staged line that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating a payment pointing at a journal entry line that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating an invoice on an instance that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-6.3 creating an invoice for a payment agreement that does not exist fails with a typed not-found error naming the missing referent, and nothing is written or changed`` () =
        Assert.Fail "not implemented"
