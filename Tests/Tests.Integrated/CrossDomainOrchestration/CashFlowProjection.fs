module Tests.Integrated.CrossDomainOrchestration.CashFlowProjection

open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.CrossDomainOrchestration
open App.Session
open App.Operation.CoreAuditableAction
open App.DataAccessLayer.DbTransaction
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

[<Collection("SharedTestData")>]
type CashFlowProjectionTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    (* Every cash flow archetype pays from F-1280. C's Invoice is due 30 days after the 1st of this month, so a 40-day
       horizon always includes it. *)
    let projectOperatingCash context =
        result {
            let! horizon = 40 |> ProjectionHorizonInDays.create
            let! projection = horizon |> CashFlowOps.projectCashFlowNDaysForward context
            return projection.accounts |> List.find (fun a -> a.accountCode |> AccountCode.value = "F-1280")
        }

    // =========================================================================
    // REQ-CF-8.3, 8.9, 8.10 — projected invoices count what is still owed
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-8.3 REQ-CF-8.9 a partly paid Outgo Invoice contributes only its outstanding amount to projected outflows`` () =
        (* C's 40.00 Payment is the only Payment on any Invoice still open against F-1280, so known outflows are the
           listed Invoices' full amounts less exactly that. *)
        let partPayment = 40.00M
        let context = Context.create NoTransaction FetchOnly
        result {
            let! account = projectOperatingCash context
            Assert.Contains(cashFlow.partlyPaidInvoiceCId, account.invoices |> List.map _.invoiceId)
            let fullAmounts = account.invoices |> List.sumBy (fun invoice -> invoice.amount |> CashFlowComponent.InvoiceAmount.value |> Money.amount)
            Assert.Equal(fullAmounts - partPayment, account.knownOutflows |> Money.amount)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-8.10 each projected Invoice carries both its amount and its outstanding amount`` () =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! account = projectOperatingCash context
            let partlyPaid = account.invoices |> List.find (fun invoice -> invoice.invoiceId = cashFlow.partlyPaidInvoiceCId)
            Assert.Equal(100.00M, (partlyPaid.amount |> CashFlowComponent.InvoiceAmount.value) |> Money.amount)
            Assert.Equal(100.00M - 40.00M, partlyPaid.outstanding |> Money.amount)
        }
        |> railroadWrapper
