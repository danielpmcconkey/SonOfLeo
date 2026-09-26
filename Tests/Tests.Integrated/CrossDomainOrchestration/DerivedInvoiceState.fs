module Tests.Integrated.CrossDomainOrchestration.DerivedInvoiceState

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

[<Collection("SharedTestData")>]
type DerivedInvoiceStateTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    (* Creates A's next Instance with one Invoice on A's leg, optionally paid by a Posted Payment on the fixture's
       unclaimed 100.00 ledger line, and reads it back. The caller names only the invoice state; the rest must be
       derived. *)
    let createAndReadBack context (invoiceAmount: decimal) (withPayment: bool) =
        result {
            let instanceDate = cashFlow.nextInstanceDateA
            let! amount = Money.fromDecimal invoiceAmount
            let! lineAmount = Money.fromDecimal 100.00M
            let payments =
                if withPayment then [ (Posted cashFlow.unclaimedLedgerLineId, { money = lineAmount }, None, None, None) ]
                else []
            let! created =
                InstanceOrchestration.createInstanceCompositeAndSaveToDb
                    context cashFlow.agreementAId instanceDate
                    [ (cashFlow.legAId, None, { localDate = instanceDate }, { localDate = instanceDate.PlusDays(30) },
                       { money = amount }, InvoiceReceived, None, None, payments) ]
            let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
            let! readBack = instanceId |> InstanceOrchestration.fetchCompositeByInstanceId context
            let lifecycle =
                readBack |> InstanceOrchestration.invoiceComposites |> List.head
                |> InstanceOrchestration.invoice |> Invoice.invoiceLifeCycleState
            return lifecycle, (readBack |> InstanceOrchestration.instance |> Instance.isFulfilled)
        }

    // =========================================================================
    // REQ-CF-9.8 through 9.10 — a new Instance's derived state
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-9.8 REQ-CF-9.9 REQ-CF-9.10 a new Instance whose only Invoice carries a Posted Payment for its full amount is created FullyPaid, PostedToLedger and fulfilled`` () =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context ->
            result {
                let! lifecycle, isFulfilled = createAndReadBack context 100.00M true
                Assert.Equal(FullyPaid, lifecycle.paymentState)
                Assert.Equal(PostedToLedger, lifecycle.postedState)
                Assert.True(isFulfilled)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-9.8 REQ-CF-9.9 REQ-CF-9.10 a new Instance whose Invoice carries a Posted Payment for part of its amount is created PartiallyPaid, PartiallyPosted and unfulfilled`` () =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context ->
            result {
                let! lifecycle, isFulfilled = createAndReadBack context 150.00M true
                Assert.Equal(PartiallyPaid, lifecycle.paymentState)
                Assert.Equal(PartiallyPosted, lifecycle.postedState)
                Assert.False(isFulfilled)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-9.8 REQ-CF-9.10 a new Instance whose Invoice has no Payments is created NotYetPaid and unfulfilled`` () =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context ->
            result {
                let! lifecycle, isFulfilled = createAndReadBack context 100.00M false
                Assert.Equal(NotYetPaid, lifecycle.paymentState)
                Assert.False(isFulfilled)
            })
        |> railroadWrapper
