module Tests.Integrated.CrossDomainOrchestration.InvoiceMatching

open App.Utility.Result
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

/// Every staged line an invoice decision in the run names, whether it got a Payment or was one of several candidates.
let private linesOfferedIn (result: InstanceOrchestration.PaymentAgreementClassificationResult) =
    result.invoiceDecisionLog
    |> List.collect (fun decision ->
        match decision.outcome with
        | PaymentCreated lineId -> [ lineId ]
        | ManyCandidateEntries lineIds -> lineIds
        | Overpayment -> [])

let private paymentsReferencing context (lineId: StageEntryLineId) =
    [ lineId ] |> Payment.fetchByStageEntryLineIdList context |> Result.map List.length

[<Collection("SharedTestData")>]
type InvoiceMatchingTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    let lineWithStatusInWindow status =
        match status with
        | "Duplicate" -> cashFlow.duplicateLineInWindowId
        | "Ignored" -> cashFlow.ignoredLineInWindowId
        | other -> failwith $"no fixture line for status {other}"

    let lineWithStatusOutsideWindow status =
        match status with
        | "Duplicate" -> cashFlow.duplicateLineOutsideWindowId
        | "Ignored" -> cashFlow.ignoredLineOutsideWindowId
        | other -> failwith $"no fixture line for status {other}"

    // =========================================================================
    // REQ-CF-13.2 — which linked lines are candidates for an Invoice
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.2 a linked line whose Payment has moved to Posted is not offered to a later open Invoice whose window covers its date`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let! run = CashFlowOps.classifyPaymentAgreements context
                Assert.DoesNotContain(cashFlow.paidPostedLineAId, run |> linesOfferedIn)
                let! payments = cashFlow.paidPostedLineAId |> paymentsReferencing context
                Assert.Equal(1, payments)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.2 REQ-CF-13.7 a linked line whose Payment has moved to Posted is not an orphan when no open Invoice covers its date`` () =
        (* An orphan fails the whole run, so reaching the assertions at all is the claim. *)
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let! run = CashFlowOps.classifyPaymentAgreements context
                Assert.DoesNotContain(cashFlow.paidPostedLineBId, run |> linesOfferedIn)
                let! payments = cashFlow.paidPostedLineBId |> paymentsReferencing context
                Assert.Equal(1, payments)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-CF-13.2 a linked line whose staged entry is Duplicate or Ignored gets no Payment from an open Invoice whose window covers its date``
        (status: string) =
        let lineId = status |> lineWithStatusInWindow
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let! run = CashFlowOps.classifyPaymentAgreements context
                Assert.DoesNotContain(lineId, run |> linesOfferedIn)
                let! payments = lineId |> paymentsReferencing context
                Assert.Equal(0, payments)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Duplicate")>]
    [<InlineData("Ignored")>]
    member _.``REQ-CF-13.2 REQ-CF-13.7 a linked line whose staged entry is Duplicate or Ignored is not an orphan when no open Invoice covers its date``
        (status: string) =
        (* As above: an orphan fails the whole run. *)
        let lineId = status |> lineWithStatusOutsideWindow
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let! run = CashFlowOps.classifyPaymentAgreements context
                Assert.DoesNotContain(lineId, run |> linesOfferedIn)
                let! payments = lineId |> paymentsReferencing context
                Assert.Equal(0, payments)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-12.3 — which staged lines linkage considers
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-12.3 an unlinked line of a Reviewed staged entry that an active Payment Agreement rule claims is linked to that rule's Payment Agreement`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.1 — which Invoices are candidates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.1 an older Invoice whose Payments already exceed its amount is passed over, and a linked line both Invoices' windows cover is paid to the newer Invoice`` () =
        Assert.Fail "not implemented"

    // =========================================================================
    // REQ-CF-13.7 — orphaned linked lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.7 when several eligible linked lines, two on one Payment Agreement and one on another, have no candidate Invoice, the run fails with one error naming every line with its Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.7 an orphaned line whose date only another Payment Agreement's open Invoice covers fails the run with the no-open-Invoice reason, not the overpaid reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.1 REQ-CF-13.7 an orphaned line whose date only an overpaid Invoice covers fails the run with the overpaid reason, not the no-open-Invoice reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.7 when one orphan has no covering open Invoice and another is covered only by an overpaid Invoice, the one error gives each line its own reason`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-13.7 a run that fails on an orphaned line, alongside a line that would have been paid, leaves links, Payments, Instances and Invoices as they were before the run`` () =
        Assert.Fail "not implemented"
