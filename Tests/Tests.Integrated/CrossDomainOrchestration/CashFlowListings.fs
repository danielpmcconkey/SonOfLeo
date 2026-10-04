module Tests.Integrated.CrossDomainOrchestration.CashFlowListings

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Both listings are read-only routes that read committed data, so every test here makes its agreements through the
   CreateAgreement and CreateInstance routes, which commit, and deletes every agreement it named (with everything under
   it) in a finally. Names carry a Guid so a leftover from a failed run never collides with the next one.

   The fixture's own agreements are A, B and C (Outgo, open ended) and D (not yet started). Its open Instances are this
   month's on A, B and C: A's and B's Invoices are unpaid, C's is part paid. Last month's Instances on A and B are paid
   in full, so fulfilled. The fixture holds A's open Instance by id and the others' through their Invoices. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private unique (label: string) = $"{label} {Guid.NewGuid():N}"

let private firstOfThisMonth () =
    let today = Calendar.today()
    LocalDate(today.Year, today.Month, 1)

let private routeWith (verb: string) (input: 'T) =
    result {
        let! json = Json.toJson input
        return! routeUiCommandForTesting "CashFlow" verb [] json
    }

let private listAgreements () =
    result {
        let! returned = routeUiCommandForTesting "CashFlow" "ListAgreements" [] ""
        return! Json.fromJson<Contracts.AgreementListingReturn list> returned
    }

let private fetchOpenInstances () =
    result {
        let! returned = routeUiCommandForTesting "CashFlow" "FetchOpenInstances" [] ""
        return! Json.fromJson<Contracts.InstanceCompositeReturn list> returned
    }

let private outgoLeg (name: string) (expected: decimal option) (daysDue: int option) : Contracts.CreatePaymentAgreementFieldsInput =
    { paymentAgreementName = name
      debitAccountCode = "F-2230"
      creditAccountCode = "F-1280"
      expectedAmount = expected
      daysDueAfterInvoiceDate = daysDue
      memo = None }

/// An Outgo agreement, monthly on the 1st, started four months ago, open ended.
let private monthlyOutgo (name: string) (legs: Contracts.CreatePaymentAgreementFieldsInput list) : Contracts.CreateAgreementInput =
    { agreementName = name
      direction = "Outgo"
      cadence = { cadenceType = Contracts.Monthly(Contracts.DateInMonth 1); nextInstance = firstOfThisMonth().PlusMonths(1) }
      counterparty = "Cash flow listings test counterparty"
      activeBegin = firstOfThisMonth().PlusMonths(-4)
      activeEnd = None
      memo = None
      paymentAgreements = legs }

let private createAgreement (input: Contracts.CreateAgreementInput) =
    routeWith "CreateAgreement" input |> Result.bind Json.fromJson<Contracts.AgreementReturn>

/// An Instance on the date with a 100.00 Invoice (or `amount`) for each (leg name, amount, Posted pointer) given.
let private createInstance (agreementName: string) (date: LocalDate) (invoices: (string * decimal * JournalEntryLineId option) list) =
    let input : Contracts.CreateInstanceInput =
        { masterAgreementName = agreementName
          instanceDate = date
          invoices =
            invoices
            |> List.map (fun (legName, amount, line) ->
                { paymentAgreementName = legName
                  externalInvoiceId = None
                  invoiceDate = date
                  dueDate = date
                  amount = amount
                  invoiceState = "InvoiceReceived"
                  blocker = None
                  memo = None
                  payments =
                    line
                    |> Option.toList
                    |> List.map (fun l ->
                        { transactionPointer = Contracts.TransactionPointerContract.Posted(l |> JournalEntryLineId.value)
                          postedToFiDate = None
                          postedToLedgerDate = None
                          memo = None }) } : Contracts.NewInvoiceFieldsInput) }
    routeWith "CreateInstance" input |> Result.bind Json.fromJson<Contracts.InstanceCompositeReturn>

/// Runs the test, then deletes every stored agreement carrying one of the names, from a fresh context.
let private cleaningUp (names: string list) (test: unit -> Result<unit, IAppError>) =
    let cleanUpFailures = ResizeArray<string>()
    try
        test () |> railroadWrapper
    finally
        let noFilter : AgreementFilter = { agreementIds = None; activeAgreementsOnly = false }
        match noFilter |> AgreementOrchestration.fetchFiltered (fresh ()) AnyQuantityIsAcceptable with
        | Ok agreements ->
            for agreement in agreements do
                let master = agreement |> AgreementOrchestration.masterAgreement
                if names |> List.contains (master |> MasterAgreement.agreementName |> AgreementName.value) then
                    match Cleanup.cleanUpMasterAgreementTree (Some(master |> MasterAgreement.agreementID |> MasterAgreementId.value)) with
                    | Ok () -> ()
                    | Error e -> cleanUpFailures.Add(e.ToMessage())
        | Error e -> cleanUpFailures.Add(e.ToMessage())
    Assert.Empty(cleanUpFailures)

[<Collection("SharedTestData")>]
type CashFlowListingsTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    let accountNameOf (code: string) =
        fixture.Data.accounts
        |> List.find (fun a -> a |> Account.code |> AccountCode.value = code)
        |> Account.accountName
        |> AccountName.value

    let fixtureAgreementUuids =
        [ cashFlow.agreementAId; cashFlow.agreementBId; cashFlow.agreementCId; cashFlow.notYetStartedAgreementDId ]
        |> List.map MasterAgreementId.value

    let instanceOfInvoice (invoiceId: InvoiceId) =
        invoiceId |> Invoice.fetchById (fresh ()) |> Result.map (Invoice.instanceId >> InstanceId.value)

    /// The fixture's open Instances: this month's on A, B and C.
    let fixtureOpenInstanceUuids () =
        result {
            let! b = instanceOfInvoice cashFlow.openInvoiceBId
            let! c = instanceOfInvoice cashFlow.partlyPaidInvoiceCId
            return Set.ofList [ cashFlow.openInstanceAId |> InstanceId.value; b; c ]
        }

    // =========================================================================
    // REQ-CF-14.11 — list agreements
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.11 listing agreements returns every Master Agreement with its name, flow direction, counterparty, cadence, start date and end date, and every Payment Agreement with its name, debit and credit account code and name, expected amount and days-due, as stored`` () =
        (* An Income agreement, weekly on a Friday, with an end date, and two legs: one with an expected amount and
           days-due, one with neither. *)
        let name = unique "CF-14.11 content"
        let today = Calendar.today()
        let nextFriday = List.init 7 (fun i -> today.PlusDays(i + 1)) |> List.find (fun d -> d.DayOfWeek = IsoDayOfWeek.Friday)
        let rentLeg : Contracts.CreatePaymentAgreementFieldsInput =
            { paymentAgreementName = $"{name} rent"; debitAccountCode = "F-1280"; creditAccountCode = "F-4290"
              expectedAmount = Some 1200.00M; daysDueAfterInvoiceDate = Some 5; memo = None }
        let utilityLeg : Contracts.CreatePaymentAgreementFieldsInput =
            { paymentAgreementName = $"{name} utility"; debitAccountCode = "F-1280"; creditAccountCode = "F-4290"
              expectedAmount = None; daysDueAfterInvoiceDate = None; memo = None }
        let input : Contracts.CreateAgreementInput =
            { agreementName = name
              direction = "Income"
              cadence = { cadenceType = Contracts.Weekly "Friday"; nextInstance = nextFriday }
              counterparty = "Cash flow listings test tenant"
              activeBegin = today.PlusDays(-10)
              activeEnd = Some(today.PlusDays(365))
              memo = None
              paymentAgreements = [ rentLeg; utilityLeg ] }
        cleaningUp [ name ] (fun () ->
            result {
                let! created = createAgreement input
                let! listed = listAgreements ()
                let listedUuids = listed |> List.map (fun l -> l.masterAgreement.agreementId) |> Set.ofList
                Assert.Equal<Set<Guid>>(
                    (created.masterAgreement.agreementId :: fixtureAgreementUuids) |> Set.ofList, listedUuids)
                let mine = listed |> List.find (fun l -> l.masterAgreement.agreementId = created.masterAgreement.agreementId)
                let m = mine.masterAgreement
                Assert.Equal(
                    (name, "Income", "Cash flow listings test tenant", input.cadence, input.activeBegin, input.activeEnd),
                    (m.agreementName, m.direction, m.counterparty, m.cadence, m.activeBegin, m.activeEnd))
                let expectedLegs =
                    [ rentLeg; utilityLeg ]
                    |> List.map (fun leg ->
                        leg.paymentAgreementName,
                        leg.debitAccountCode, accountNameOf leg.debitAccountCode,
                        leg.creditAccountCode, accountNameOf leg.creditAccountCode,
                        leg.expectedAmount, leg.daysDueAfterInvoiceDate)
                let listedLegs =
                    mine.paymentAgreements
                    |> List.map (fun pa ->
                        pa.paymentAgreementName,
                        pa.debitAccountCode, pa.debitAccountName,
                        pa.creditAccountCode, pa.creditAccountName,
                        pa.expectedAmount, pa.daysDueAfterInvoiceDate)
                Assert.Equal<(string * string * string * string * string * decimal option * int option) list>(expectedLegs, listedLegs)
            })

    [<Fact>]
    member _.``REQ-CF-14.11 listing agreements orders the Master Agreements by name and each agreement's Payment Agreements by name, whatever order they were created in`` () =
        (* "zulu" is created before "alpha", and each is given its "zulu" leg before its "alpha" leg. *)
        let tag = Guid.NewGuid().ToString("N")
        let zulu = $"CF-14.11 order zulu {tag}"
        let alpha = $"CF-14.11 order alpha {tag}"
        let legsOf (name: string) =
            [ outgoLeg $"{name} leg zulu" (Some 100.00M) (Some 0); outgoLeg $"{name} leg alpha" (Some 100.00M) (Some 0) ]
        cleaningUp [ zulu; alpha ] (fun () ->
            result {
                let! _ = createAgreement (monthlyOutgo zulu (legsOf zulu))
                let! _ = createAgreement (monthlyOutgo alpha (legsOf alpha))
                let! listed = listAgreements ()
                let names = listed |> List.map (fun l -> l.masterAgreement.agreementName)
                Assert.Equal<string list>(names |> List.sortWith (fun a b -> String.CompareOrdinal(a, b)), names)
                let mine = listed |> List.filter (fun l -> l.masterAgreement.agreementName = zulu || l.masterAgreement.agreementName = alpha)
                Assert.Equal<(string * string list) list>(
                    [ (alpha, [ $"{alpha} leg alpha"; $"{alpha} leg zulu" ])
                      (zulu, [ $"{zulu} leg alpha"; $"{zulu} leg zulu" ]) ],
                    mine |> List.map (fun l -> l.masterAgreement.agreementName, (l.paymentAgreements |> List.map _.paymentAgreementName)))
            })

    // =========================================================================
    // REQ-CF-14.12 — fetch open Instances
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.12 fetching open Instances returns every open Instance across all Master Agreements, each with its Master Agreement's name, its Invoices and their Payments`` () =
        (* A new agreement's Instance this month: a 250.00 Invoice on its first leg part paid by the fixture's unclaimed
           100.00 ledger line, and nothing yet on its second leg. *)
        let name = unique "CF-14.12 content"
        let firstLeg = $"{name} leg 1"
        let line = cashFlow.unclaimedLedgerLineId
        cleaningUp [ name ] (fun () ->
            result {
                let! _ =
                    createAgreement
                        (monthlyOutgo name [ outgoLeg firstLeg (Some 100.00M) (Some 0); outgoLeg $"{name} leg 2" None None ])
                let! created = createInstance name (firstOfThisMonth ()) [ (firstLeg, 250.00M, Some line) ]
                let myInstance = created.instance.instanceId
                let! fixtureOpen = fixtureOpenInstanceUuids ()
                let! listed = fetchOpenInstances ()
                Assert.Equal<Set<Guid>>(fixtureOpen |> Set.add myInstance, listed |> List.map _.instance.instanceId |> Set.ofList)
                let mine = listed |> List.find (fun i -> i.instance.instanceId = myInstance)
                Assert.Equal(name, mine.instance.masterAgreementName)
                Assert.Equal<(string * decimal * (Contracts.TransactionPointerContract * decimal) list) list>(
                    [ (firstLeg, 250.00M, [ (Contracts.TransactionPointerContract.Posted(line |> JournalEntryLineId.value), 100.00M) ]) ],
                    mine.invoiceComposites
                    |> List.map (fun ic ->
                        ic.invoice.paymentAgreementName, ic.invoice.amount,
                        (ic.payments |> List.map (fun p -> p.transactionPointer, p.amount))))
            })

    [<Fact>]
    member _.``REQ-CF-14.12 fetching open Instances orders them by Instance date, and Instances sharing a date by Master Agreement name, whatever order they were created in`` () =
        (* "zulu" and its Instances are created first: two months ago and last month. "alpha" then gets an Instance last
           month, sharing zulu's date. *)
        let tag = Guid.NewGuid().ToString("N")
        let zulu = $"CF-14.12 order zulu {tag}"
        let alpha = $"CF-14.12 order alpha {tag}"
        let twoMonthsAgo = firstOfThisMonth().PlusMonths(-2)
        let lastMonth = firstOfThisMonth().PlusMonths(-1)
        cleaningUp [ zulu; alpha ] (fun () ->
            result {
                let! _ = createAgreement (monthlyOutgo zulu [ outgoLeg $"{zulu} leg" None None ])
                let! _ = createAgreement (monthlyOutgo alpha [ outgoLeg $"{alpha} leg" None None ])
                let! zuluEarly = createInstance zulu twoMonthsAgo []
                let! zuluLate = createInstance zulu lastMonth []
                let! alphaLate = createInstance alpha lastMonth []
                let! listed = fetchOpenInstances ()
                let keys = listed |> List.map (fun i -> i.instance.instanceDate, i.instance.masterAgreementName)
                Assert.Equal<(LocalDate * string) list>(
                    keys |> List.sortWith (fun (d1, n1) (d2, n2) -> match compare d1 d2 with 0 -> String.CompareOrdinal(n1, n2) | c -> c),
                    keys)
                let mineInOrder =
                    listed
                    |> List.map _.instance.instanceId
                    |> List.filter (fun id -> [ zuluEarly; zuluLate; alphaLate ] |> List.exists (fun c -> c.instance.instanceId = id))
                Assert.Equal<Guid list>(
                    [ zuluEarly.instance.instanceId; alphaLate.instance.instanceId; zuluLate.instance.instanceId ], mineInOrder)
            })

    [<Fact>]
    member _.``REQ-CF-14.12 REQ-CF-4.14 fetching open Instances omits a fulfilled Instance and a cancelled Instance`` () =
        (* Two months ago: a 100.00 Invoice paid in full by the fixture's unclaimed 100.00 ledger line, so fulfilled.
           Last month: cancelled. This month: neither. The fixture's own fulfilled Instance, last month's on A, is
           checked too. *)
        let name = unique "CF-14.12 omits"
        let legName = $"{name} leg"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createAgreement (monthlyOutgo name [ outgoLeg legName (Some 100.00M) (Some 0) ])
                let! fulfilled =
                    createInstance name (firstOfThisMonth().PlusMonths(-2)) [ (legName, 100.00M, Some cashFlow.unclaimedLedgerLineId) ]
                let! cancelled = createInstance name (firstOfThisMonth().PlusMonths(-1)) [ (legName, 100.00M, None) ]
                let! openOne = createInstance name (firstOfThisMonth ()) [ (legName, 100.00M, None) ]
                let! _ =
                    routeWith "CancelInstance"
                        ({ instanceId = cancelled.instance.instanceId; cancellationReasonNote = "Never billed" } : Contracts.CancelInstanceInput)
                let! fixtureFulfilled = instanceOfInvoice cashFlow.paidInvoiceAId
                Assert.True(fulfilled.instance.isFulfilled)
                let! listed = fetchOpenInstances ()
                let listedIds = listed |> List.map _.instance.instanceId |> Set.ofList
                Assert.Contains(openOne.instance.instanceId, listedIds)
                Assert.DoesNotContain(fulfilled.instance.instanceId, listedIds)
                Assert.DoesNotContain(cancelled.instance.instanceId, listedIds)
                Assert.DoesNotContain(fixtureFulfilled, listedIds)
            })
