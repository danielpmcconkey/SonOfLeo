module Tests.Integrated.CrossDomainOrchestration.InvoiceMatching

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Xunit

/// Every staged line an invoice decision in the run names, whether it got a Payment or was one of several candidates.
let private linesOfferedIn (result: InstanceOrchestration.PaymentAgreementClassificationResult) =
    result.invoiceDecisionLog
    |> List.collect (fun decision ->
        match decision.outcome with
        | PaymentCreated lineId -> [ lineId ]
        | ManyCandidateEntries lineIds -> lineIds
        | Overpayment -> []
        | BlockerCleared _ -> [])

let private paymentsReferencing context (lineId: StageEntryLineId) =
    [ lineId ] |> Payment.fetchByStageEntryLineIdList context |> Result.map List.length

(* Each test below builds its own Outgo agreements on the fixture's cash flow accounts (debit F-2230, credit F-1280),
   monthly on the 1st, one 100.00 leg each. With a monthly cadence the grace period is 7 days, so an Invoice dated d
   and due d + n takes lines dated d - 7 through d + n + 7. *)

/// every link to any of the given Payment Agreements
let private linksTo context (agreementIds: PaymentAgreementId list) =
    PaymentAgreementLink.fetchAll context
    |> Result.map (List.filter (fun link -> agreementIds |> List.contains (link |> PaymentAgreementLink.paymentAgreementId)))

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today()
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cashId = accountIdOf "F-1280"
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")

    member _.firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
    member this.firstOfLastMonth = this.firstOfThisMonth.PlusMonths(-1)

    /// Returns the agreement and its one leg.
    member this.agreement (name: string) =
        result {
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Invoice matching test lender" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (this.firstOfLastMonth.PlusMonths(-1)) None
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = $"{name} leg" |> PaymentAgreementName.create
            let! expected = Money.fromDecimal 100.00M
            let! due = 0 |> DaysDueAfterInvoiceDate.create
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = this.firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None
                    [ (legName, DebitAccount loanId, CreditAccount cashId, Some expected, Some due, None) ]
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legId =
                agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
            return agreementId, legId
        }

    /// A 100.00 Invoice on its own Instance, with Posted Payments of the given amounts on ledger-only lines.
    member _.invoice agreementId legId (invoiceDate: LocalDate) (daysDue: int) (paidAmounts: decimal list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let! payments =
                paidAmounts
                |> List.map (fun paid ->
                    result {
                        let! entry, _ =
                            createTestJournalEntryFromPrimitives
                                context "Invoice matching test ledger payment" None invoiceDate
                                [ (loanId, paid, "Debit", None); (cashId, paid, "Credit", None) ] [] []
                        let lineId =
                            entry
                            |> JournalEntryOrchestration.jeLines
                            |> List.find (fun l -> l |> JournalEntryLine.accountId = loanId)
                            |> JournalEntryLine.journalEntryLineId
                        return (TransactionPointer.Posted lineId, None, None, None)
                    })
                |> convertListOfResultsToResultsList
            let! created =
                InstanceOrchestration.createInstanceCompositeAndSaveToDb
                    context agreementId invoiceDate
                    [ (legId, None, { localDate = invoiceDate }, { localDate = invoiceDate.PlusDays(daysDue) },
                       { money = amount }, InvoiceReceived, None, None, payments) ]
            return
                created |> InstanceOrchestration.invoiceComposites |> List.head
                |> InstanceOrchestration.invoice |> Invoice.invoiceId
        }

    /// A 100.00 staged entry (Debit F-2230, Credit F-1280) that went Ingested, then through each given status.
    /// Returns its header and its Debit line, the line an Outgo leg claims.
    member _.stagedEntry (description: string) (entryDate: LocalDate) (statuses: (string * string) list) =
        result {
            let start = Clock.now()
            let transitions =
                ("Ingested", "StageIngestion") :: statuses
                |> List.pairwise
                |> List.mapi (fun i ((prior, _), (status, mechanism)) ->
                    (Some prior, status, start.Plus(Duration.FromMilliseconds(int64 ((i + 1) * 10))), mechanism))
                |> fun later -> (None, "Ingested", start, "StageIngestion") :: later
            let! entry =
                createStageEntryForTest context "/tmp/invoice-matching-test.dat" description
                    (Guid.NewGuid().ToString()) testBank entryDate
                    [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                    transitions
            let lineId =
                entry
                |> StageEntryOrchestration.seLines
                |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit)
                |> StageEntryLine.stageEntryLineId
            return (entry |> StageEntryOrchestration.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId), lineId
        }

    /// A Classified staged entry whose Debit line is linked to the leg.
    member this.linkedLine legId (description: string) (entryDate: LocalDate) =
        result {
            let! headerId, lineId = this.stagedEntry description entryDate [ ("Classified", "Classifier") ]
            let! _ = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context legId lineId
            return headerId, lineId
        }

    /// An active rule claiming, for the leg, any entry whose description matches.
    member _.paymentAgreementRule legId (description: string) =
        result {
            let! name = $"Invoice matching test: {description}" |> ClassificationRuleName.create
            let! pattern = description |> StringSearchPattern.create
            let! groups = [ ("And", [ FieldMatch.Description pattern ], None) ] |> createClassificationRuleGroupListForTest
            return!
                ClassificationOrchestration.createNewClassificationRule
                    context name (ClassificationClaimant.PaymentAgreement legId) 500 groups
        }

let private orphansIn (run: Result<'T, IAppError>) =
    match run with
    | Error (AsError (CashFlowError.CashflowPaymentAgreementLinksOrphaned orphans)) -> orphans
    | Error e -> failwith $"Wrong error. {e.ToMessage()}"
    | Ok _ -> failwith "Expected the run to fail on orphaned lines; it succeeded"

let private lineUuid (lineId: StageEntryLineId) = lineId |> StageEntryLineId.value
let private legUuid (legId: PaymentAgreementId) = legId |> PaymentAgreementId.value


/// The Instance and Invoice IDs an agreement has, so a failed run can be shown to have created neither.
let private instancesAndInvoicesOf context agreementId =
    result {
        let! instances = [ agreementId ] |> Instance.fetchByMasterAgreementIdList context
        let! composites =
            instances
            |> List.map (Instance.instanceId >> InstanceOrchestration.fetchCompositeByInstanceId context)
            |> convertListOfResultsToResultsList
        let invoiceIds =
            composites
            |> List.collect InstanceOrchestration.invoiceComposites
            |> List.map (InstanceOrchestration.invoice >> Invoice.invoiceId)
        return (instances |> List.map Instance.instanceId |> Set.ofList), (invoiceIds |> Set.ofList)
    }

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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
                let! run = ClassificationOrchestration.classifyPaymentAgreements context
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
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-12.3 reviewed"
                (* An open Invoice covering the line, so a correctly linked line has somewhere to go. *)
                let! _ = scenario.invoice agreementId legId scenario.firstOfThisMonth 30 []
                let! _ = scenario.paymentAgreementRule legId "CF-12.3 reviewed payment"
                let! _, lineId =
                    scenario.stagedEntry "CF-12.3 reviewed payment" scenario.firstOfThisMonth
                        [ ("Classified", "Classifier"); ("Reviewed", "Operator") ]
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! links = lineId |> PaymentAgreementLink.fetchByStageEntryLineId context
                let link = Assert.Single(links)
                Assert.Equal(legId, link |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.1 — which Invoices are candidates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.1 an older Invoice whose Payments already exceed its amount is passed over, and a linked line both Invoices' windows cover is paid to the newer Invoice`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.1 overpaid"
                (* Last month's 100.00 Invoice carries 150.00 of Payments. Due 30 days on, its window reaches past the
                   1st of this month, where this month's Invoice's window also starts. *)
                let! olderInvoiceId = scenario.invoice agreementId legId scenario.firstOfLastMonth 30 [ 150.00M ]
                let! newerInvoiceId = scenario.invoice agreementId legId scenario.firstOfThisMonth 30 []
                let! _, lineId = scenario.linkedLine legId "CF-13.1 payment" scenario.firstOfThisMonth
                let! _ = ClassificationOrchestration.classifyPaymentAgreements context
                let! payments = [ lineId ] |> Payment.fetchByStageEntryLineIdList context
                let payment = Assert.Single(payments)
                Assert.Equal(newerInvoiceId, payment |> Payment.invoiceId)
                Assert.NotEqual(olderInvoiceId, payment |> Payment.invoiceId)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-13.7 — orphaned linked lines
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-13.7 when several eligible linked lines, two on one Payment Agreement and one on another, have no candidate Invoice, the run fails with one error naming every line with its Payment Agreement`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                (* Each agreement has an open Invoice this month, due on its date, so it takes lines within a week of
                   the 1st. Every line here is dated mid last month. *)
                let lastMonthMidpoint = scenario.firstOfLastMonth.PlusDays(10)
                let! agreementXId, legXId = scenario.agreement "CF-13.7 several X"
                let! agreementYId, legYId = scenario.agreement "CF-13.7 several Y"
                let! _ = scenario.invoice agreementXId legXId scenario.firstOfThisMonth 0 []
                let! _ = scenario.invoice agreementYId legYId scenario.firstOfThisMonth 0 []
                let! _, lineX1 = scenario.linkedLine legXId "CF-13.7 several X first" lastMonthMidpoint
                let! _, lineX2 = scenario.linkedLine legXId "CF-13.7 several X second" lastMonthMidpoint
                let! _, lineY = scenario.linkedLine legYId "CF-13.7 several Y" lastMonthMidpoint
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let named = orphans |> List.map (fun (line, leg, _) -> line, leg) |> Set.ofList
                let expected =
                    set [ lineUuid lineX1, legUuid legXId
                          lineUuid lineX2, legUuid legXId
                          lineUuid lineY, legUuid legYId ]
                Assert.Equal<Set<Guid * Guid>>(expected, named)
                Assert.Equal(3, orphans |> List.length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.7 an orphaned line whose date only another Payment Agreement's open Invoice covers fails the run with the no-open-Invoice reason, not the overpaid reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let lastMonthMidpoint = scenario.firstOfLastMonth.PlusDays(10)
                let! agreementXId, legXId = scenario.agreement "CF-13.7 own X"
                let! agreementYId, legYId = scenario.agreement "CF-13.7 other Y"
                (* X's only open Invoice takes lines within a week of this month's 1st. Y's, due 30 days after last
                   month's 1st, covers the line's date, but the line is linked to X. *)
                let! _ = scenario.invoice agreementXId legXId scenario.firstOfThisMonth 0 []
                let! _ = scenario.invoice agreementYId legYId scenario.firstOfLastMonth 30 []
                let! _, lineId = scenario.linkedLine legXId "CF-13.7 own X payment" lastMonthMidpoint
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineUuid lineId, orphanLine)
                Assert.Equal(legUuid legXId, orphanLeg)
                Assert.Equal(CashFlowError.NoOpenInvoiceCoversDate, reason)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.1 REQ-CF-13.7 an orphaned line whose date only an overpaid Invoice covers fails the run with the overpaid reason, not the no-open-Invoice reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.7 overpaid only"
                (* The agreement's only open Invoice is 100.00 with 150.00 paid, and its window covers the line. *)
                let! _ = scenario.invoice agreementId legId scenario.firstOfLastMonth 30 [ 150.00M ]
                let! _, lineId =
                    scenario.linkedLine legId "CF-13.7 overpaid only payment" (scenario.firstOfLastMonth.PlusDays(10))
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineUuid lineId, orphanLine)
                Assert.Equal(legUuid legId, orphanLeg)
                Assert.Equal(CashFlowError.CoveringInvoicesOverpaid, reason)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.7 when one orphan has no covering open Invoice and another is covered only by an overpaid Invoice, the one error gives each line its own reason`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let lastMonthMidpoint = scenario.firstOfLastMonth.PlusDays(10)
                let! agreementXId, legXId = scenario.agreement "CF-13.7 mixed uncovered"
                let! agreementYId, legYId = scenario.agreement "CF-13.7 mixed overpaid"
                let! _ = scenario.invoice agreementXId legXId scenario.firstOfThisMonth 0 []
                let! _ = scenario.invoice agreementYId legYId scenario.firstOfLastMonth 30 [ 150.00M ]
                let! _, uncoveredLineId = scenario.linkedLine legXId "CF-13.7 mixed uncovered payment" lastMonthMidpoint
                let! _, overpaidLineId = scenario.linkedLine legYId "CF-13.7 mixed overpaid payment" lastMonthMidpoint
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let reasons = orphans |> List.map (fun (line, _, reason) -> line, reason) |> Set.ofList
                let expected =
                    set [ lineUuid uncoveredLineId, CashFlowError.NoOpenInvoiceCoversDate
                          lineUuid overpaidLineId, CashFlowError.CoveringInvoicesOverpaid ]
                Assert.Equal<Set<Guid * CashFlowError.OrphanedLineReason>>(expected, reasons)
                Assert.Equal(2, orphans |> List.length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.7 an eligible linked line on a Payment Agreement with no Instances fails the run, the error names the line and its Payment Agreement with the no-open-Invoice reason, and no Instance or Invoice is created`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.7 no Instances"
                (* The agreement has no Instance at all, so no Invoice of any state. *)
                let! _, lineId =
                    scenario.linkedLine legId "CF-13.7 no Instances payment" (scenario.firstOfLastMonth.PlusDays(10))
                let! before = instancesAndInvoicesOf context agreementId
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineUuid lineId, orphanLine)
                Assert.Equal(legUuid legId, orphanLeg)
                Assert.Equal(CashFlowError.NoOpenInvoiceCoversDate, reason)
                let! after = instancesAndInvoicesOf context agreementId
                Assert.Equal(before, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.7 an eligible linked line on a Payment Agreement whose Invoices are all exactly FullyPaid, none overpaid and one covering the line's date, fails the run, the error names the line and its Payment Agreement with the no-open-Invoice reason, and no Instance or Invoice is created`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.7 all FullyPaid"
                (* Its only Invoice is 100.00 with exactly 100.00 paid, and its window covers the line's date: not
                   open, and not overpaid either, so the overpaid reason would be wrong. *)
                let! _ = scenario.invoice agreementId legId scenario.firstOfLastMonth 30 [ 100.00M ]
                let! _, lineId =
                    scenario.linkedLine legId "CF-13.7 all FullyPaid payment" (scenario.firstOfLastMonth.PlusDays(10))
                let! before = instancesAndInvoicesOf context agreementId
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineUuid lineId, orphanLine)
                Assert.Equal(legUuid legId, orphanLeg)
                Assert.Equal(CashFlowError.NoOpenInvoiceCoversDate, reason)
                let! after = instancesAndInvoicesOf context agreementId
                Assert.Equal(before, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-13.7 an eligible linked line on a Payment Agreement whose open Invoices do not cover its date fails the run, the error names the line and its Payment Agreement with the no-open-Invoice reason, and no Instance or Invoice is created`` () =
        runCommandRouteAndAutoRollback ClassifyPaymentAgreements (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-13.7 open elsewhere"
                (* Its open Invoice, due on this month's 1st, takes lines within a week of that date only. *)
                let! _ = scenario.invoice agreementId legId scenario.firstOfThisMonth 0 []
                let! _, lineId =
                    scenario.linkedLine legId "CF-13.7 open elsewhere payment" (scenario.firstOfLastMonth.PlusDays(10))
                let! before = instancesAndInvoicesOf context agreementId
                let orphans = ClassificationOrchestration.classifyPaymentAgreements context |> orphansIn
                let orphanLine, orphanLeg, reason = Assert.Single(orphans)
                Assert.Equal(lineUuid lineId, orphanLine)
                Assert.Equal(legUuid legId, orphanLeg)
                Assert.Equal(CashFlowError.NoOpenInvoiceCoversDate, reason)
                let! after = instancesAndInvoicesOf context agreementId
                Assert.Equal(before, after)
            })
        |> railroadWrapper

    (* This one commits its setup, because what it checks is what a failed run leaves behind once its transaction is
       gone, and a test that rolls back its own transaction can't see that. It runs the operation the way its route
       does, under a transaction that commits on success and rolls back on failure, then reads back from a fresh
       context. Everything it made is deleted in the finally. *)
    [<Fact>]
    member _.``REQ-CF-13.7 a run that fails on an orphaned line, alongside a line that would have been paid, leaves links, Payments, Instances and Invoices as they were before the run`` () =
        let mutable agreementIds: Guid list = []
        let mutable headerIds: StageEntryHeaderId list = []
        let mutable ruleId: ClassificationRuleId option = None
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! setup =
                    runCommandRouteAndAutoCompleteTransaction ClassifyPaymentAgreements (fun context ->
                        result {
                            let scenario = Scenario(fixture, context)
                            let! agreementXId, legXId = scenario.agreement "CF-13.7 rollback payable"
                            let! agreementYId, legYId = scenario.agreement "CF-13.7 rollback orphan"
                            agreementIds <- [ agreementXId; agreementYId ] |> List.map MasterAgreementId.value
                            let! _ = scenario.invoice agreementXId legXId scenario.firstOfThisMonth 30 []
                            let! _ = scenario.invoice agreementYId legYId scenario.firstOfThisMonth 0 []
                            (* X's line is unlinked; the run would link it and pay X's Invoice with it. *)
                            let! rule = scenario.paymentAgreementRule legXId "CF-13.7 rollback payable payment"
                            ruleId <- Some(rule |> ClassificationRule.classificationRuleId)
                            let! payableHeaderId, payableLineId =
                                scenario.stagedEntry "CF-13.7 rollback payable payment" scenario.firstOfThisMonth
                                    [ ("Classified", "Classifier") ]
                            headerIds <- payableHeaderId :: headerIds
                            let! orphanHeaderId, _ =
                                scenario.linkedLine legYId "CF-13.7 rollback orphan payment"
                                    (scenario.firstOfLastMonth.PlusDays(10))
                            headerIds <- orphanHeaderId :: headerIds
                            return [ agreementXId; agreementYId ], [ legXId; legYId ], payableLineId
                        })
                let agreements, legs, payableLineId = setup
                let snapshot () =
                    let context = Context.create NoTransaction FetchOnly
                    result {
                        let! instances = agreements |> Instance.fetchByMasterAgreementIdList context
                        let! composites =
                            instances
                            |> List.map (Instance.instanceId >> InstanceOrchestration.fetchCompositeByInstanceId context)
                            |> convertListOfResultsToResultsList
                        let! links = linksTo context legs
                        return composites |> List.sortBy (InstanceOrchestration.instance >> Instance.instanceId >> InstanceId.value), links
                    }
                let! before = snapshot ()
                let run =
                    runCommandRouteAndAutoCompleteTransaction ClassifyPaymentAgreements ClassificationOrchestration.classifyPaymentAgreements
                let orphans = run |> orphansIn
                Assert.NotEmpty(orphans)
                let! after = snapshot ()
                Assert.Equal(before, after)
                let context = Context.create NoTransaction FetchOnly
                let! payableLinks = payableLineId |> PaymentAgreementLink.fetchByStageEntryLineId context
                Assert.Empty(payableLinks)
                let! payablePayments = [ payableLineId ] |> Payment.fetchByStageEntryLineIdList context
                Assert.Empty(payablePayments)
            }
            |> railroadWrapper
        finally
            (* Every clean up runs even when an earlier one fails, and a failure here never hides the test's own.
               Order matters: the rule's matches, then the rule (it points at a leg), then the agreements (which take
               their links and Payments with them), then the staged entries those pointed at. *)
            [ yield Cleanup.cleanUpRuleMatchesOfRuleId ruleId
              yield Cleanup.cleanUpClassificationRuleId ruleId
              for id in agreementIds do yield Cleanup.cleanUpMasterAgreementTree (Some id)
              yield headerIds |> List.map Some |> Cleanup.cleanUpStageEntryHeaderIdList ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)
