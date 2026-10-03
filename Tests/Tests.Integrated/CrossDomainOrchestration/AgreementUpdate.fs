module Tests.Integrated.CrossDomainOrchestration.AgreementUpdate

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

let private noMasterAgreementChange agreementId : MasterAgreement.MasterAgreementFieldUpdates =
    { agreementIdToUpdate = agreementId
      agreementNameUpdate = FieldUpdate.NoChange
      directionUpdate = FieldUpdate.NoChange
      cadenceUpdate = FieldUpdate.NoChange
      counterpartyUpdate = FieldUpdate.NoChange
      activityPeriodUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

let private noLegChange legId : PaymentAgreement.PaymentAgreementFieldUpdates =
    { paymentAgreementIdToUpdate = legId
      paymentAgreementNameUpdate = FieldUpdate.NoChange
      debitAccountUpdate = FieldUpdate.NoChange
      creditAccountUpdate = FieldUpdate.NoChange
      expectedAmountUpdate = FieldUpdate.NoChange
      daysDueAfterInvoiceDateUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

[<Collection("SharedTestData")>]
type AgreementUpdateTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    // =========================================================================
    // REQ-CF-3.6 — a leg cannot debit and credit the same account
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.6 updating a leg's credit account to its debit account is rejected naming that account`` () =
        (* A's leg debits F-2230; pointing its credit side there too makes both sides one account. *)
        let debitAccountId =
            fixture.Data.accounts
            |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-2230")
            |> Account.accountId
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let updated =
                    AgreementOrchestration.updateAgreement
                        context
                        [ { noLegChange cashFlow.legAId with creditAccountUpdate = FieldUpdate.SetTo(CreditAccount.create debitAccountId) } ]
                        []
                        (noMasterAgreementChange cashFlow.agreementAId)
                return
                    match updated with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementDebitEqualsCredit uuid)) ->
                        Assert.Equal(debitAccountId |> AccountId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-14.2 — a direction change must leave every Invoice's state valid
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.2 REQ-CF-5.10 changing an Outgo agreement with an InvoiceReceived Invoice to Income is rejected naming the Invoice`` () =
        (* Both of A's Invoices are InvoiceReceived; either may be the one named. *)
        let aInvoiceUuids =
            [ cashFlow.paidInvoiceAId; cashFlow.openInvoiceAId ] |> List.map InvoiceId.value
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let updated =
                    AgreementOrchestration.updateAgreement
                        context [] []
                        { noMasterAgreementChange cashFlow.agreementAId with directionUpdate = FieldUpdate.SetTo Income }
                return
                    match updated with
                    | Error (AsError (CashFlowError.CashflowInvoiceStateInvalidForFlowDirection(uuid, state, direction))) ->
                        Assert.Contains(uuid, aInvoiceUuids)
                        Assert.Equal("InvoiceReceived", state)
                        Assert.Equal("Income", direction)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
            })
        |> railroadWrapper

    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-CF-14.2 REQ-CF-4.6 changing a Master Agreement's cadence leaves its existing Instances, whose dates no longer fit the new cadence, unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.2 for each of the latest existing Instance's date and a date before it, an update setting the next-instance date to it is rejected with a typed error naming the latest Instance date, and the agreement is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.2 an update setting the next-instance date to the first cadence date after the latest existing Instance is stored`` () =
        Assert.Fail "Not yet implemented"
