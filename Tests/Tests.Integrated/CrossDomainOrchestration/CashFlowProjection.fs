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
        (* The fixture's Invoices still open against F-1280 are this month's on A, B and C. Last month's are paid in
           full. C's part payment is the only Payment on an open one, so known outflows are the three Invoices'
           amounts less exactly that. *)
        let openInvoices = [ cashFlow.openInvoiceAId; cashFlow.openInvoiceBId; cashFlow.partlyPaidInvoiceCId ]
        let context = Context.create NoTransaction FetchOnly
        result {
            let! account = projectOperatingCash context
            Assert.Equal<Set<InvoiceId>>(Set.ofList openInvoices, account.invoices |> List.map _.invoiceId |> Set.ofList)
            let! expected =
                (decimal openInvoices.Length) * cashFlow.invoiceAmount - cashFlow.partPaymentCAmount |> Money.fromDecimal
            Assert.Equal(expected, account.knownOutflows)
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-8.10 each projected Invoice carries both its amount and its outstanding amount`` () =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! account = projectOperatingCash context
            let partlyPaid = account.invoices |> List.find (fun invoice -> invoice.invoiceId = cashFlow.partlyPaidInvoiceCId)
            let! amount = cashFlow.invoiceAmount |> Money.fromDecimal
            let! outstanding = cashFlow.invoiceAmount - cashFlow.partPaymentCAmount |> Money.fromDecimal
            Assert.Equal(amount, partlyPaid.amount |> CashFlowComponent.InvoiceAmount.value)
            Assert.Equal(outstanding, partlyPaid.outstanding)
        }
        |> railroadWrapper
