module Tests.Integrated.CrossDomainOrchestration.PaymentAgreementUpdate

open System
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
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open App.DataAccessLayer.ExecuteReader
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Two kinds of test live here.

   Most go through the CashFlow routes, the way the operator does, because a Payment Agreement's accounts are given by
   code and only the route converts a code. A route commits when it succeeds and an update is written before the
   agreement is checked, so these tests read back from a fresh context, on an agreement of their own that is deleted in
   a finally whether or not the test expected it to be stored.

   The test that needs Invoices and Payments under the agreement works on fixture agreement A in the orchestration
   layer, inside a transaction that is rolled back. *)

let private unique (label: string) = $"{label} {Guid.NewGuid():N}"

let private fresh () = Context.create NoTransaction FetchOnly

let private noFilter : AgreementFilter = { agreementIds = None; activeAgreementsOnly = false }

/// The stored agreement trees whose Master Agreement carries the name, read from the given context.
let private treesNamed (context: Context.Context) (name: string) =
    noFilter
    |> AgreementOrchestration.fetchFiltered context AnyQuantityIsAcceptable
    |> Result.map (
        List.filter (fun a ->
            a |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementName |> AgreementName.value = name))

/// The stored Master Agreement carrying the name and its legs ordered by name, read from a fresh context.
let private stored (name: string) =
    treesNamed (fresh ()) name
    |> Result.map (
        List.map (fun a ->
            a |> AgreementOrchestration.masterAgreement,
            a |> AgreementOrchestration.paymentAgreements
            |> List.sortBy (PaymentAgreement.paymentAgreementName >> PaymentAgreementName.value)))

let private storedLeg name =
    stored name |> Result.map (List.exactlyOne >> snd >> List.exactlyOne)

let private originalMemo = "Original leg memo"

let private leg (name: string) : Contracts.CreatePaymentAgreementFieldsInput =
    { paymentAgreementName = $"{name} leg"
      debitAccountCode = "F-2230"
      creditAccountCode = "F-1280"
      expectedAmount = Some 100.00M
      daysDueAfterInvoiceDate = Some 0
      memo = Some originalMemo }

/// A CreateAgreement payload: Outgo, Daily, one leg, started 30 days ago, open ended.
let private createInput (name: string) : Contracts.CreateAgreementInput =
    { agreementName = name
      direction = "Outgo"
      cadence = { cadenceType = Contracts.Daily; nextInstance = Calendar.today () }
      counterparty = "Payment agreement update test counterparty"
      activeBegin = Calendar.today().PlusDays(-30)
      activeEnd = None
      memo = None
      paymentAgreements = [ leg name ] }

let private createRoute (name: string) =
    createInput name |> Json.toJson |> Result.bind (routeUiCommandForTesting "CashFlow" "CreateAgreement" [])

let private updateRoute (input: Contracts.UpdateAgreementInput) =
    input |> Json.toJson |> Result.bind (routeUiCommandForTesting "CashFlow" "UpdateAgreement" [])

let private noUpdate (name: string) : Contracts.UpdateAgreementInput =
    { agreementName = name
      agreementNameUpdate = NoChange
      directionUpdate = NoChange
      cadenceUpdate = NoChange
      counterpartyUpdate = NoChange
      activeBeginUpdate = NoChange
      activeEndUpdate = NoChange
      memoUpdate = NoChange
      paymentAgreementUpdates = []
      newPaymentAgreements = [] }

let private noLegUpdate (legName: string) : Contracts.UpdatePaymentAgreementInput =
    { paymentAgreementName = legName
      paymentAgreementNameUpdate = NoChange
      debitAccountCodeUpdate = NoChange
      creditAccountCodeUpdate = NoChange
      expectedAmountUpdate = NoChange
      daysDueAfterInvoiceDateUpdate = NoChange
      memoUpdate = NoChange }

/// An update of the agreement's one leg only.
let private legUpdate (name: string) (update: Contracts.UpdatePaymentAgreementInput) =
    { noUpdate name with paymentAgreementUpdates = [ update ] }

/// Runs the test, then deletes every stored agreement carrying one of the names, from a fresh context.
let private cleaningUp (names: string list) (test: unit -> Result<unit, IAppError>) =
    let cleanUpFailures = ResizeArray<string>()
    try
        test () |> railroadWrapper
    finally
        for name in names do
            match treesNamed (fresh ()) name with
            | Ok trees ->
                for tree in trees do
                    let agreementUuid =
                        tree |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID |> MasterAgreementId.value
                    match Cleanup.cleanUpMasterAgreementTree (Some agreementUuid) with
                    | Ok () -> ()
                    | Error e -> cleanUpFailures.Add(e.ToMessage())
            | Error e -> cleanUpFailures.Add(e.ToMessage())
    Assert.Empty(cleanUpFailures)

let private noMasterAgreementChange agreementId : MasterAgreement.MasterAgreementFieldUpdates =
    { agreementIdToUpdate = agreementId
      agreementNameUpdate = NoChange
      directionUpdate = NoChange
      cadenceUpdate = NoChange
      counterpartyUpdate = NoChange
      activeBeginUpdate = NoChange
      activeEndUpdate = NoChange
      memoUpdate = NoChange }

let private noLegChange legId : PaymentAgreement.PaymentAgreementFieldUpdates =
    { paymentAgreementIdToUpdate = legId
      paymentAgreementNameUpdate = NoChange
      debitAccountUpdate = NoChange
      creditAccountUpdate = NoChange
      expectedAmountUpdate = NoChange
      daysDueAfterInvoiceDateUpdate = NoChange
      memoUpdate = NoChange }

[<Collection("SharedTestData")>]
type PaymentAgreementUpdateTests(fixture: TestDataFixture) =

    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    let codeOf accountId =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.accountId = accountId) |> Account.code |> AccountCode.value

    // =========================================================================
    // REQ-CF-14.8 — updating a Payment Agreement's fields
    // =========================================================================

    [<Theory>]
    [<InlineData("name")>]
    [<InlineData("expectedAmount")>]
    [<InlineData("daysDue")>]
    [<InlineData("memo")>]
    [<InlineData("debitAccount")>]
    [<InlineData("creditAccount")>]
    member _.``REQ-CF-14.8 for each field (name, expected amount, days-due, memo, debit account, credit account), updating a Payment Agreement's field stores the new value and leaves every other field unchanged`` (field: string) =
        let name = unique "CF-14.8 field"
        let newName = $"{name} renamed leg"
        let newDebitCode = codeOf fixture.Data.mortgage2210Id
        let newCreditCode = codeOf fixture.Data.moneyMarket1270Id
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = storedLeg name
                let legName = $"{name} leg"
                let update =
                    match field with
                    | "name" -> { noLegUpdate legName with paymentAgreementNameUpdate = SetTo newName }
                    | "expectedAmount" -> { noLegUpdate legName with expectedAmountUpdate = SetTo(Some 250.00M) }
                    | "daysDue" -> { noLegUpdate legName with daysDueAfterInvoiceDateUpdate = SetTo(Some 45) }
                    | "memo" -> { noLegUpdate legName with memoUpdate = SetTo(Some "Updated leg memo") }
                    | "debitAccount" -> { noLegUpdate legName with debitAccountCodeUpdate = SetTo newDebitCode }
                    | _ -> { noLegUpdate legName with creditAccountCodeUpdate = SetTo newCreditCode }
                let! _ = legUpdate name update |> updateRoute
                let! after = storedLeg name
                // every field as it was created, except the one the update named
                let! expectedName = (if field = "name" then newName else legName) |> PaymentAgreementName.create
                let! expectedAmount = (if field = "expectedAmount" then 250.00M else 100.00M) |> Money.fromDecimal
                let! expectedDays = (if field = "daysDue" then 45 else 0) |> DaysDueAfterInvoiceDate.create
                let! expectedMemo = (if field = "memo" then "Updated leg memo" else originalMemo) |> PaymentAgreementMemo.create
                let expectedDebit = accountIdOf (if field = "debitAccount" then newDebitCode else "F-2230")
                let expectedCredit = accountIdOf (if field = "creditAccount" then newCreditCode else "F-1280")
                let expected =
                    PaymentAgreement.create
                        (before |> PaymentAgreement.paymentAgreementId) (before |> PaymentAgreement.masterAgreementID)
                        expectedName (DebitAccount.create expectedDebit) (CreditAccount.create expectedCredit)
                        (Some expectedAmount) (Some expectedDays) (Some expectedMemo)
                        (before |> PaymentAgreement.createdAt) (after |> PaymentAgreement.modifiedAt)
                Assert.Equal(expected, after)
            })

    [<Theory>]
    [<InlineData("debit")>]
    [<InlineData("credit")>]
    member _.``REQ-CF-14.8 updating a Payment Agreement's debit or credit account to a code no account holds is rejected with a typed error naming the code, and the agreement is unchanged`` (side: string) =
        let name = unique "CF-14.8 unknown code"
        let unknownCode = "CF-14.8-X"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = stored name
                let legName = $"{name} leg"
                let update =
                    if side = "debit" then { noLegUpdate legName with debitAccountCodeUpdate = SetTo unknownCode }
                    else { noLegUpdate legName with creditAccountCodeUpdate = SetTo unknownCode }
                let attempt = legUpdate name update |> updateRoute
                let! after = stored name
                let () =
                    match attempt with
                    | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId code)) -> Assert.Equal(unknownCode, code)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Equal<(MasterAgreement.MasterAgreement * PaymentAgreement.PaymentAgreement list) list>(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-14.8 REQ-CF-3.6 updating a Payment Agreement's debit account to its own credit account is rejected with a typed error and the agreement is unchanged`` () =
        let name = unique "CF-14.8 same account"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = stored name
                let attempt = legUpdate name { noLegUpdate $"{name} leg" with debitAccountCodeUpdate = SetTo "F-1280" } |> updateRoute
                let! after = stored name
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementDebitEqualsCredit uuid)) ->
                        Assert.Equal(accountIdOf "F-1280" |> AccountId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Equal<(MasterAgreement.MasterAgreement * PaymentAgreement.PaymentAgreement list) list>(before, after)
            })

    [<Fact>]
    member _.``REQ-CF-14.8 updating a Payment Agreement's expected amount and accounts leaves the amount of every existing Invoice and the pointer of every existing Payment unchanged`` () =
        (* Fixture agreement A has last month's Invoice, paid by a Posted Payment, and this month's open Invoice, each
           created at 100.00. *)
        let cashFlow = fixture.Data.cashFlow
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! before = cashFlow.agreementAId |> AgreementOrchestration.fetchByMasterAgreementId context
                let! newAmount = Money.fromDecimal 250.00M
                let! _ =
                    noMasterAgreementChange cashFlow.agreementAId
                    |> AgreementOrchestration.updateAgreement
                        context
                        [ { noLegChange cashFlow.legAId with
                              expectedAmountUpdate = SetTo(Some newAmount)
                              debitAccountUpdate = SetTo(DebitAccount.create fixture.Data.mortgage2210Id)
                              creditAccountUpdate = SetTo(CreditAccount.create fixture.Data.moneyMarket1270Id) } ]
                        []
                let! after = cashFlow.agreementAId |> AgreementOrchestration.fetchByMasterAgreementId context
                let legAfter = after |> AgreementOrchestration.paymentAgreements |> List.exactlyOne
                let amountOf invoiceId =
                    after |> AgreementOrchestration.invoices
                    |> List.find (fun i -> i |> Invoice.invoiceId = invoiceId)
                    |> Invoice.amount |> InvoiceAmount.value |> Money.amount
                let pointers agreement =
                    agreement |> AgreementOrchestration.payments
                    |> List.map (fun p -> p |> Payment.paymentId, p |> Payment.transactionPointer)
                    |> List.sortBy (fst >> PaymentId.value)
                // the leg did change
                Assert.Equal(Some 250.00M, legAfter |> PaymentAgreement.expectedAmount |> Option.map Money.amount)
                Assert.Equal(fixture.Data.mortgage2210Id, legAfter |> PaymentAgreement.debitAccount |> DebitAccount.value)
                Assert.Equal(fixture.Data.moneyMarket1270Id, legAfter |> PaymentAgreement.creditAccount |> CreditAccount.value)
                // what hangs off it did not
                Assert.Equal(100.00M, amountOf cashFlow.paidInvoiceAId)
                Assert.Equal(100.00M, amountOf cashFlow.openInvoiceAId)
                Assert.Equal<Invoice.Invoice list>(
                    before |> AgreementOrchestration.invoices, after |> AgreementOrchestration.invoices)
                Assert.NotEmpty(before |> AgreementOrchestration.payments)
                Assert.Equal<(PaymentId * TransactionPointer) list>(pointers before, pointers after)
                Assert.Equal<Payment.Payment list>(
                    before |> AgreementOrchestration.payments, after |> AgreementOrchestration.payments)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-14.8 an update to a Payment Agreement that names no field to change is rejected with a typed error and the agreement is unchanged`` () =
        let name = unique "CF-14.8 no field"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = stored name
                let attempt = legUpdate name (noLegUpdate $"{name} leg") |> updateRoute
                let! after = stored name
                let () =
                    match attempt with
                    | Error (AsError CashFlowError.CashflowAgreementUpdateNoOp) -> ()
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Equal<(MasterAgreement.MasterAgreement * PaymentAgreement.PaymentAgreement list) list>(before, after)
            })

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-CF-14.8 an update that sets a Payment Agreement's name to the name it already holds succeeds and advances its modified-at`` () =
        let name = unique "CF-14.8 same name"
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = storedLeg name
                System.Threading.Thread.Sleep(10)
                let legName = $"{name} leg"
                let! _ = legUpdate name { noLegUpdate legName with paymentAgreementNameUpdate = SetTo legName } |> updateRoute
                let! after = storedLeg name
                Assert.Equal<string>(legName, after |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value)
                Assert.True(
                    (after |> PaymentAgreement.modifiedAt) > (before |> PaymentAgreement.modifiedAt),
                    $"modified-at did not advance: before {before |> PaymentAgreement.modifiedAt}, after {after |> PaymentAgreement.modifiedAt}")
            })

    // =========================================================================
    // REQ-CF-14.9 — adding a leg to an existing Master Agreement
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.9 adding a Payment Agreement to an existing Master Agreement stores it under that agreement with every field given, alongside the agreement's existing legs`` () =
        let name = unique "CF-14.9 added leg"
        let addedName = $"{name} second leg"
        let added : Contracts.CreatePaymentAgreementFieldsInput =
            { paymentAgreementName = addedName
              debitAccountCode = codeOf fixture.Data.mortgage2210Id
              creditAccountCode = codeOf fixture.Data.moneyMarket1270Id
              expectedAmount = Some 75.25M
              daysDueAfterInvoiceDate = Some 10
              memo = Some "Added leg memo" }
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = stored name
                let masterBefore, legsBefore = before |> List.exactlyOne
                let! _ = { noUpdate name with newPaymentAgreements = [ added ] } |> updateRoute
                let! after = stored name
                let masterAfter, legsAfter = after |> List.exactlyOne
                let addedLeg =
                    legsAfter |> List.find (fun l -> l |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = addedName)
                let! expectedName = addedName |> PaymentAgreementName.create
                let! expectedAmount = 75.25M |> Money.fromDecimal
                let! expectedDays = 10 |> DaysDueAfterInvoiceDate.create
                let! expectedMemo = "Added leg memo" |> PaymentAgreementMemo.create
                let expectedLeg =
                    PaymentAgreement.create
                        (addedLeg |> PaymentAgreement.paymentAgreementId) (masterBefore |> MasterAgreement.agreementID)
                        expectedName (DebitAccount.create fixture.Data.mortgage2210Id)
                        (CreditAccount.create fixture.Data.moneyMarket1270Id)
                        (Some expectedAmount) (Some expectedDays) (Some expectedMemo)
                        (addedLeg |> PaymentAgreement.createdAt) (addedLeg |> PaymentAgreement.modifiedAt)
                Assert.Equal(expectedLeg, addedLeg)
                // the existing leg is still there, untouched, and nothing else was added
                Assert.Equal<PaymentAgreement.PaymentAgreement list>(
                    legsBefore @ [ expectedLeg ]
                    |> List.sortBy (PaymentAgreement.paymentAgreementName >> PaymentAgreementName.value),
                    legsAfter)
                Assert.Equal(masterBefore |> MasterAgreement.agreementID, masterAfter |> MasterAgreement.agreementID)
            })

    [<Fact>]
    member _.``REQ-CF-14.9 REQ-CF-3.6 adding a Payment Agreement whose debit and credit accounts are the same is rejected with a typed error and the Master Agreement keeps exactly its original legs`` () =
        let name = unique "CF-14.9 same account"
        let added : Contracts.CreatePaymentAgreementFieldsInput =
            { paymentAgreementName = $"{name} second leg"
              debitAccountCode = "F-2230"
              creditAccountCode = "F-2230"
              expectedAmount = Some 75.25M
              daysDueAfterInvoiceDate = Some 10
              memo = None }
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createRoute name
                let! before = stored name
                let attempt = { noUpdate name with newPaymentAgreements = [ added ] } |> updateRoute
                let! after = stored name
                let () =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementDebitEqualsCredit uuid)) ->
                        Assert.Equal(accountIdOf "F-2230" |> AccountId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
                Assert.Equal<(MasterAgreement.MasterAgreement * PaymentAgreement.PaymentAgreement list) list>(before, after)
            })
