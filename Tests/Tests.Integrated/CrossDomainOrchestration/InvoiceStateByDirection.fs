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

let private expectStateRejectedFor (expectedDirection: string) (expectedState: string) (outcome: Result<'T, IAppError>) =
    match outcome with
    | Error (AsError (CashFlowError.CashflowInvoiceStateInvalidForFlowDirection(_, state, direction))) ->
        Assert.Equal(expectedState, state)
        Assert.Equal(expectedDirection, direction)
    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
    | Ok _ -> Assert.Fail "Expected failure; got success"

let private expectStateRejected (expectedState: string) (outcome: Result<'T, IAppError>) =
    expectStateRejectedFor "Outgo" expectedState outcome

[<Collection("SharedTestData")>]
type InvoiceStateByDirectionTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    let accountIdOf code =
        fixture.Data.accounts
        |> List.find (fun a -> a |> Ledger.Account.code |> Ledger.AccountComponent.AccountCode.value = code)
        |> Ledger.Account.accountId

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
                        [ (cashFlow.legAId, None, InvoiceDate.create(instanceDate), DueDate.create(instanceDate.PlusDays(30)),
                           InvoiceAmount.create(amount), invoiceState, None, None, []) ]
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

    (* The fixture's agreements are all Outgo, so the Income agreement is the test's own: monthly on the 1st, debit
       F-1280 (cash), credit F-4290 (revenue), one 100.00 leg. *)
    [<Theory>]
    [<InlineData("InvoiceExpected")>]
    [<InlineData("InvoiceReceived")>]
    member _.``REQ-CF-5.10 a new Instance on an Income agreement carrying an Invoice in an Outgo state is rejected naming the state``
        (state: string) =
        let today = Calendar.today ()
        let firstOfThisMonth = NodaTime.LocalDate(today.Year, today.Month, 1)
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context ->
            result {
                let! agreementName = $"CF-5.10 income {state}" |> AgreementName.create
                let! first = 1 |> Business.General.Cadence.DateInMonthNumber.fromInt
                let! counterparty = "Invoice state test tenant" |> Counterparty.create
                let! activityPeriod =
                    Business.General.ActivityPeriod.create (firstOfThisMonth.PlusMonths(-3)) None
                        Business.General.ActivityPeriod.ConsideredAvailableBeforeBeginDate
                let! legName = $"CF-5.10 income {state} leg" |> PaymentAgreementName.create
                let! expected = Money.fromDecimal 100.00M
                let! due = 0 |> DaysDueAfterInvoiceDate.create
                let! agreement =
                    AgreementOrchestration.constructNewAndPersist
                        context agreementName Income
                        (Business.General.Cadence.Monthly(Business.General.Cadence.DateInMonth first))
                        { nextInstance = firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None
                        [ (legName, DebitAccount.create (accountIdOf "F-1280"), CreditAccount.create (accountIdOf "F-4290"),
                           Some expected, Some due, None) ]
                let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
                let legId =
                    agreement |> AgreementOrchestration.paymentAgreements |> List.exactlyOne |> PaymentAgreement.paymentAgreementId
                let! invoiceState = state |> InvoiceState.fromString
                let created =
                    InstanceOrchestration.constructNewAndPersist
                        context agreementId firstOfThisMonth
                        [ (legId, None, InvoiceDate.create(firstOfThisMonth), DueDate.create(firstOfThisMonth.PlusDays(30)),
                           InvoiceAmount.create(expected), invoiceState, None, None, []) ]
                return created |> expectStateRejectedFor "Income" state
            })
        |> railroadWrapper
