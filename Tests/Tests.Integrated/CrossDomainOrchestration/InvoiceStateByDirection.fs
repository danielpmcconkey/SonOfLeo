module Tests.Integrated.CrossDomainOrchestration.InvoiceStateByDirection

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

let private expectStateRejected (expectedState: string) (outcome: Result<'T, IAppError>) =
    match outcome with
    | Error (AsError (CashFlowError.CashflowInvoiceStateInvalidForFlowDirection(_, state, direction))) ->
        Assert.Equal(expectedState, state)
        Assert.Equal("Outgo", direction)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

[<Collection("SharedTestData")>]
type InvoiceStateByDirectionTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    // =========================================================================
    // REQ-CF-5.10 — invoice state must suit the agreement's flow direction
    // =========================================================================

    [<Theory>]
    [<InlineData("InvoiceGenerated")>]
    [<InlineData("InvoiceSent")>]
    member _.``REQ-CF-5.10 a new Instance on an Outgo agreement carrying an Invoice in an Income state is rejected naming the state``
        (state: string) =
        let instanceDate = cashFlow.nextInstanceDateA
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context ->
            result {
                let! invoiceState = state |> InvoiceState.fromString
                let! amount = Money.fromDecimal 100.00M
                let created =
                    InstanceOrchestration.constructNewAndPersist
                        context cashFlow.agreementAId instanceDate
                        [ (cashFlow.legAId, None, { localDate = instanceDate }, { localDate = instanceDate.PlusDays(30) },
                           { money = amount }, invoiceState, None, None, []) ]
                return created |> expectStateRejected state
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-5.10 updating an Outgo agreement's Invoice to InvoiceSent is rejected naming the state`` () =
        runCommandRouteAndAutoRollback CashFlowUpdateInvoice (fun context ->
            result {
                let invoiceUpdates: Invoice.InvoiceFieldUpdates =
                    { invoiceIdToUpdate = cashFlow.openInvoiceAId
                      externalInvoiceIdUpdate = FieldUpdate.NoChange
                      invoiceDateUpdate = FieldUpdate.NoChange
                      dueDateUpdate = FieldUpdate.NoChange
                      amountUpdate = FieldUpdate.NoChange
                      invoiceStateUpdate = FieldUpdate.SetTo InvoiceSent
                      paymentStateUpdate = FieldUpdate.NoChange
                      postedStateUpdate = FieldUpdate.NoChange
                      blockerUpdate = FieldUpdate.NoChange
                      memoUpdate = FieldUpdate.NoChange }
                let updated =
                    InstanceOrchestration.updateInstanceComposite
                        context
                        { instanceUpdates =
                            { instanceIdToUpdate = cashFlow.openInstanceAId
                              isFulfilledUpdate = FieldUpdate.NoChange }
                          invoiceCompositeUpdates =
                            [ { invoiceUpdates = invoiceUpdates
                                paymentUpdates = []
                                paymentIdsToDelete = []
                                newPayments = [] } ]
                          newInvoices = [] }
                return updated |> expectStateRejected "InvoiceSent"
            })
        |> railroadWrapper
