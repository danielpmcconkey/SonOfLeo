module Tests.Integrated.CrossDomainOrchestration.ProjectionSweep

open App.Utility.Result
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

[<Collection("SharedTestData")>]
type ProjectionSweepTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    (* The fixture's newest Instances are this month's; the sweep resumes from each agreement's next-instance date,
       the 1st of next month. A 40-day horizon always reaches it, so every Invoice dated on or after that date is
       one the sweep made. *)
    let sweptInvoices context =
        result {
            let! horizon = 40 |> ProjectionHorizonInDays.create
            let! unfulfilled = horizon |> CashFlowOps.createUpcomingInstances context
            return
                unfulfilled
                |> List.collect InstanceOrchestration.invoiceComposites
                |> List.map InstanceOrchestration.invoice
                |> List.filter (fun invoice -> (invoice |> Invoice.invoiceDate).localDate >= cashFlow.nextInstanceDateA)
        }

    let assertIncludesAgreementANextInstance (invoices: Invoice.Invoice list) =
        Assert.Contains(
            (cashFlow.legAId, cashFlow.nextInstanceDateA),
            invoices |> List.map (fun invoice ->
                (invoice |> Invoice.paymentAgreementId), (invoice |> Invoice.invoiceDate).localDate))

    // =========================================================================
    // REQ-CF-7.10, 7.11 — the lifecycle of an Invoice the sweep creates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.10 every Invoice the sweep creates for an Outgo agreement is InvoiceExpected`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let! swept = sweptInvoices context
                swept |> assertIncludesAgreementANextInstance
                Assert.All(swept, fun invoice ->
                    Assert.Equal(InvoiceExpected, (invoice |> Invoice.invoiceLifeCycleState).invoiceState))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.11 every Invoice the sweep creates is NotYetPaid and NotHandled`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let! swept = sweptInvoices context
                swept |> assertIncludesAgreementANextInstance
                Assert.All(swept, fun invoice ->
                    let lifecycle = invoice |> Invoice.invoiceLifeCycleState
                    Assert.Equal((NotYetPaid, NotHandled), (lifecycle.paymentState, lifecycle.postedState)))
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-7.2 — which agreements the sweep considers active
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.2 the sweep creates no Instance for an agreement whose start date is still ahead`` () =
        Assert.Fail "not implemented"
