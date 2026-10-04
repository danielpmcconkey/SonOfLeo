module Tests.Integrated.CrossDomainOrchestration.PaymentsToPosted

open System
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open Xunit

(* Every test builds its own Outgo agreements (debit F-2230, credit F-1280, monthly on the 1st) and Invoices inside a
   transaction that is rolled back. A "posted line" is a Posted staged entry whose Debit line records the journal entry
   Debit line it produced; an "unposted line" is a Classified staged entry's Debit line, which records none. *)
type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today()
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cashId = accountIdOf "F-1280"
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")
    let transitions (statuses: (string * string) list) =
        let start = Clock.now()
        ("Ingested", "StageIngestion") :: statuses
        |> List.pairwise
        |> List.mapi (fun i ((prior, _), (status, mechanism)) ->
            (Some prior, status, start.Plus(Duration.FromMilliseconds(int64 ((i + 1) * 10))), mechanism))
        |> fun later -> (None, "Ingested", start, "StageIngestion") :: later
    let debitLineOf entry =
        entry
        |> StageEntryOrchestration.seLines
        |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit)
        |> StageEntryLine.stageEntryLineId

    member _.firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
    member this.firstOfLastMonth = this.firstOfThisMonth.PlusMonths(-1)

    /// Returns the agreement's id and its one leg's id.
    member this.agreement (name: string) =
        result {
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Payments to posted test lender" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (this.firstOfThisMonth.PlusMonths(-3)) None
                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = $"{name} leg" |> PaymentAgreementName.create
            let! expected = Money.fromDecimal 100.00M
            let! due = 0 |> DaysDueAfterInvoiceDate.create
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = this.firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None
                    [ (legName, (DebitAccount.create loanId), (CreditAccount.create cashId), Some expected, Some due, None) ]
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legId =
                agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
            return agreementId, legId
        }

    /// A journal entry for the amount; returns its Debit line (on F-2230).
    member this.ledgerLine (description: string) (amount: decimal) =
        result {
            let! entry, _ =
                createTestJournalEntryFromPrimitives
                    context description None this.firstOfThisMonth
                    [ (loanId, amount, "Debit", None); (cashId, amount, "Credit", None) ] [] []
            return
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = loanId)
                |> JournalEntryLine.journalEntryLineId
        }

    /// A Posted staged entry for the amount whose Debit line records the journal entry Debit line it produced.
    /// Returns the staged Debit line and that journal entry line.
    member this.postedLine (description: string) (amount: decimal) =
        result {
            let! entry, _ =
                createTestJournalEntryFromPrimitives
                    context description None this.firstOfThisMonth
                    [ (loanId, amount, "Debit", None); (cashId, amount, "Credit", None) ] [] []
            let jeLineOn accountId =
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = accountId)
                |> JournalEntryLine.journalEntryLineId
            let! staged =
                createStageEntryForTest context "/tmp/payments-to-posted-test.dat" description
                    (Guid.NewGuid().ToString()) testBank this.firstOfThisMonth
                    [ (amount, "Debit", Some "F-2230", None, Some (jeLineOn loanId))
                      (amount, "Credit", Some "F-1280", None, Some (jeLineOn cashId)) ]
                    (transitions [ ("Classified", "Classifier"); ("Posted", "LedgerPoster") ])
            return (staged |> debitLineOf), (jeLineOn loanId)
        }

    /// A Classified staged entry for the amount; returns its Debit line, which records no journal entry line.
    member _.unpostedLine (description: string) (amount: decimal) =
        result {
            let! staged =
                createStageEntryForTest context "/tmp/payments-to-posted-test.dat" description
                    (Guid.NewGuid().ToString()) testBank today
                    [ (amount, "Debit", Some "F-2230", None, None); (amount, "Credit", Some "F-1280", None, None) ]
                    (transitions [ ("Classified", "Classifier") ])
            return staged |> debitLineOf
        }

    /// A 100.00 Invoice on its own Instance dated invoiceDate, carrying Payments at the given pointers (each Payment's
    /// amount is its line's). Returns the Invoice's id and its Payments' ids, in the order given.
    member _.invoice agreementId legId (invoiceDate: LocalDate) (payments: TransactionPointer list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let newPayments = payments |> List.map (fun pointer -> (pointer, None, None, None))
            let! created =
                InstanceOrchestration.constructNewAndPersist
                    context agreementId invoiceDate
                    [ (legId, None, InvoiceDate.create(invoiceDate), DueDate.create(invoiceDate.PlusDays(30)),
                       InvoiceAmount.create(amount), InvoiceReceived, None, None, newPayments) ]
            let invoiceComposite = created |> InstanceOrchestration.invoiceComposites |> List.head
            let invoiceId = invoiceComposite |> InstanceOrchestration.invoice |> Invoice.invoiceId
            let createdPayments = invoiceComposite |> InstanceOrchestration.payments
            (* Match each created Payment back to the pointer the test gave it. *)
            let paymentIds =
                payments
                |> List.map (fun pointer ->
                    createdPayments |> List.find (fun p -> p |> Payment.transactionPointer = pointer) |> Payment.paymentId)
            return invoiceId, paymentIds
        }

    /// Voids the journal entry the line belongs to. Returns that entry's header id.
    member _.voidEntryOf (jeLineId: JournalEntryLineId) =
        result {
            let! line = jeLineId |> JournalEntryLine.fetchById context
            let headerId = line |> JournalEntryLine.journalEntryHeaderId
            let! reason = "Payments to posted test: the cash was posted to the wrong entry" |> CommentText.create
            let! _ = headerId |> JournalEntryVoiding.voidJournalEntry context None reason
            return headerId
        }

    member _.postedStateOf (invoiceId: InvoiceId) =
        invoiceId
        |> InstanceOrchestration.fetchCompositeByInvoiceId context
        |> Result.map (fun composite ->
            ((composite |> InstanceOrchestration.invoice |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.postedState))

    member _.payment (paymentId: PaymentId) = paymentId |> Payment.fetchById context

    member _.stagedLineOf (paymentId: PaymentId) =
        paymentId |> Payment.fetchById context
        |> Result.map (Payment.transactionPointer >> TransactionPointer.stageEntryLineId)

let private movedIds (transitions: PaymentPostingTransition list) =
    transitions |> List.map _.paymentId |> Set.ofList

[<Collection("SharedTestData")>]
type PaymentsToPostedTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-10.1 through 10.3 — which Payments move, and to what
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.1 REQ-CF-10.3 a Staged Payment whose staged line records the journal entry line it produced gets that journal entry line ID and keeps its staged line ID`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.1 moved"
                let! stagedLineId, jeLineId = scenario.postedLine "CF-10.1 moved payment" 100.00M
                let! _, paymentIds = scenario.invoice agreementId legId scenario.firstOfThisMonth [ Staged stagedLineId ]
                let paymentId = paymentIds |> List.head
                let! _ = CashFlowOps.transitionPaymentsToPosted context
                let! payment = scenario.payment paymentId
                Assert.Equal(Posted(jeLineId, Some stagedLineId), payment |> Payment.transactionPointer)
                let! kept = scenario.stagedLineOf paymentId
                Assert.Equal(Some stagedLineId, kept)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.1 the transition moves every Staged Payment whose staged line records a journal entry line, across every Invoice and agreement, in one run`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-10.1 every X"
                let! agreementYId, legYId = scenario.agreement "CF-10.1 every Y"
                let! lineX1, jeX1 = scenario.postedLine "CF-10.1 every X last month" 100.00M
                let! lineX2, jeX2 = scenario.postedLine "CF-10.1 every X this month" 100.00M
                let! lineY, jeY = scenario.postedLine "CF-10.1 every Y" 100.00M
                let! _, x1 = scenario.invoice agreementXId legXId scenario.firstOfLastMonth [ Staged lineX1 ]
                let! _, x2 = scenario.invoice agreementXId legXId scenario.firstOfThisMonth [ Staged lineX2 ]
                let! _, y = scenario.invoice agreementYId legYId scenario.firstOfThisMonth [ Staged lineY ]
                let! _ = CashFlowOps.transitionPaymentsToPosted context
                let! pointers =
                    [ x1.Head; x2.Head; y.Head ]
                    |> List.map (fun id -> scenario.payment id |> Result.map Payment.transactionPointer)
                    |> convertListOfResultsToResultsList
                Assert.Equal<TransactionPointer list>([ Posted(jeX1, Some lineX1); Posted(jeX2, Some lineX2); Posted(jeY, Some lineY) ], pointers)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.2 a Staged Payment whose staged line records no journal entry line is left Staged, with no journal entry line ID, and is not listed by the transition`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.2 unposted"
                let! lineId = scenario.unpostedLine "CF-10.2 unposted payment" 100.00M
                let! _, paymentIds = scenario.invoice agreementId legId scenario.firstOfThisMonth [ Staged lineId ]
                let paymentId = paymentIds |> List.head
                let! moved = CashFlowOps.transitionPaymentsToPosted context
                let! payment = scenario.payment paymentId
                Assert.Equal(Staged lineId, payment |> Payment.transactionPointer)
                Assert.DoesNotContain(paymentId, moved |> movedIds)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.1 a Posted Payment that also carries a staged line ID, whose staged line records a journal entry line, is not changed and not listed by the transition`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.1 both pointers"
                let! stagedLineId, jeLineId = scenario.postedLine "CF-10.1 both pointers payment" 100.00M
                let! _, paymentIds = scenario.invoice agreementId legId scenario.firstOfThisMonth [ Staged stagedLineId ]
                let paymentId = paymentIds |> List.head
                (* The first run gives the Payment both pointers. *)
                let! first = CashFlowOps.transitionPaymentsToPosted context
                Assert.Contains(paymentId, first |> movedIds)
                let! before = scenario.payment paymentId
                let! stagedBefore = scenario.stagedLineOf paymentId
                Assert.Equal(Posted(jeLineId, Some stagedLineId), before |> Payment.transactionPointer)
                Assert.Equal(Some stagedLineId, stagedBefore)
                let! second = CashFlowOps.transitionPaymentsToPosted context
                Assert.DoesNotContain(paymentId, second |> movedIds)
                let! after = scenario.payment paymentId
                let! stagedAfter = scenario.stagedLineOf paymentId
                Assert.Equal(before |> Payment.transactionPointer, after |> Payment.transactionPointer)
                Assert.Equal(before |> Payment.modifiedAt, after |> Payment.modifiedAt)
                Assert.Equal(stagedBefore, stagedAfter)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-10.4 — the Invoice's posted state follows
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves every Staged Payment of a FullyPaid, NotHandled Invoice, the Invoice's posted state becomes PostedToLedger`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.4 all"
                let! line1, _ = scenario.postedLine "CF-10.4 all one" 60.00M
                let! line2, _ = scenario.postedLine "CF-10.4 all two" 40.00M
                let! invoiceId, _ =
                    scenario.invoice agreementId legId scenario.firstOfThisMonth [ Staged line1; Staged line2 ]
                let! before = scenario.postedStateOf invoiceId
                Assert.Equal(NotHandled, before)
                let! _ = CashFlowOps.transitionPaymentsToPosted context
                let! after = scenario.postedStateOf invoiceId
                Assert.Equal(PostedToLedger, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves the last Staged Payment of a FullyPaid, PartiallyPosted Invoice, the Invoice's posted state becomes PostedToLedger`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.4 last"
                let! ledgerLineId = scenario.ledgerLine "CF-10.4 last ledger" 40.00M
                let! stagedLineId, _ = scenario.postedLine "CF-10.4 last staged" 60.00M
                let! invoiceId, _ =
                    scenario.invoice agreementId legId scenario.firstOfThisMonth
                        [ Posted(ledgerLineId, None); Staged stagedLineId ]
                let! before = scenario.postedStateOf invoiceId
                Assert.Equal(PartiallyPosted, before)
                let! _ = CashFlowOps.transitionPaymentsToPosted context
                let! after = scenario.postedStateOf invoiceId
                Assert.Equal(PostedToLedger, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves some but not all of a FullyPaid, NotHandled Invoice's Staged Payments, the Invoice's posted state becomes PartiallyPosted`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.4 some"
                let! postedLineId, _ = scenario.postedLine "CF-10.4 some posted" 60.00M
                let! unpostedLineId = scenario.unpostedLine "CF-10.4 some unposted" 40.00M
                let! invoiceId, _ =
                    scenario.invoice agreementId legId scenario.firstOfThisMonth
                        [ Staged postedLineId; Staged unpostedLineId ]
                let! before = scenario.postedStateOf invoiceId
                Assert.Equal(NotHandled, before)
                let! _ = CashFlowOps.transitionPaymentsToPosted context
                let! after = scenario.postedStateOf invoiceId
                Assert.Equal(PartiallyPosted, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.4 when the transition moves every Staged Payment of a PartiallyPaid, NotHandled Invoice, the Invoice's posted state becomes PartiallyPosted, not PostedToLedger`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.4 part paid"
                let! lineId, _ = scenario.postedLine "CF-10.4 part paid payment" 40.00M
                let! invoiceId, paymentIds = scenario.invoice agreementId legId scenario.firstOfThisMonth [ Staged lineId ]
                let! before = scenario.postedStateOf invoiceId
                Assert.Equal(NotHandled, before)
                let! moved = CashFlowOps.transitionPaymentsToPosted context
                Assert.Contains(paymentIds.Head, moved |> movedIds)
                let! after = scenario.postedStateOf invoiceId
                Assert.Equal(PartiallyPosted, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.4 an Invoice none of whose Payments the transition moves keeps its posted state`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.4 none"
                let! ledgerLineId = scenario.ledgerLine "CF-10.4 none ledger" 40.00M
                let! unpostedLineId = scenario.unpostedLine "CF-10.4 none unposted" 60.00M
                let! invoiceId, _ =
                    scenario.invoice agreementId legId scenario.firstOfThisMonth
                        [ Posted(ledgerLineId, None); Staged unpostedLineId ]
                let! before = scenario.postedStateOf invoiceId
                Assert.Equal(PartiallyPosted, before)
                let! _ = CashFlowOps.transitionPaymentsToPosted context
                let! after = scenario.postedStateOf invoiceId
                Assert.Equal(PartiallyPosted, after)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-10.5 — idempotent
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.5 a second transition run straight after the first moves no Payment, lists nothing, and leaves every Payment's pointers and every Invoice's posted state as the first run left them`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-10.5 twice"
                let! postedLineId, _ = scenario.postedLine "CF-10.5 twice posted" 60.00M
                let! unpostedLineId = scenario.unpostedLine "CF-10.5 twice unposted" 40.00M
                let! invoiceId, paymentIds =
                    scenario.invoice agreementId legId scenario.firstOfThisMonth
                        [ Staged postedLineId; Staged unpostedLineId ]
                let snapshot () =
                    result {
                        let! payments =
                            paymentIds
                            |> List.map (fun id ->
                                result {
                                    let! payment = scenario.payment id
                                    let! staged = scenario.stagedLineOf id
                                    return payment |> Payment.transactionPointer, staged, payment |> Payment.modifiedAt
                                })
                            |> convertListOfResultsToResultsList
                        let! postedState = scenario.postedStateOf invoiceId
                        return payments, postedState
                    }
                let! first = CashFlowOps.transitionPaymentsToPosted context
                Assert.NotEmpty(first)
                let! afterFirst = snapshot ()
                let! second = CashFlowOps.transitionPaymentsToPosted context
                Assert.Empty(second)
                let! afterSecond = snapshot ()
                Assert.Equal(afterFirst, afterSecond)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-10.7 — what the transition returns
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.7 the transition lists every Payment it moved, each with its agreement name, its Invoice's amount and the journal entry line ID it now points at, and no Payment it did not move`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-10.7 listed X"
                let! agreementYId, legYId = scenario.agreement "CF-10.7 listed Y"
                let! lineX, jeX = scenario.postedLine "CF-10.7 listed X payment" 100.00M
                let! lineY, jeY = scenario.postedLine "CF-10.7 listed Y payment" 60.00M
                let! unpostedLineId = scenario.unpostedLine "CF-10.7 listed Y unposted" 40.00M
                let! _, xPayments = scenario.invoice agreementXId legXId scenario.firstOfThisMonth [ Staged lineX ]
                let! _, yPayments =
                    scenario.invoice agreementYId legYId scenario.firstOfThisMonth
                        [ Staged lineY; Staged unpostedLineId ]
                let! moved = CashFlowOps.transitionPaymentsToPosted context
                let listed =
                    moved
                    |> List.map (fun t ->
                        t.paymentId, (t.agreementName |> AgreementName.value), ((t.invoiceAmount |> CashFlowComponent.InvoiceAmount.value) |> Money.amount), t.journalEntryLineId)
                    |> Set.ofList
                let expected =
                    set [ xPayments.[0], "CF-10.7 listed X", 100.00M, jeX
                          yPayments.[0], "CF-10.7 listed Y", 100.00M, jeY ]
                Assert.Equal<Set<PaymentId * string * decimal * JournalEntryLineId>>(expected, listed)
                Assert.Equal(2, moved |> List.length)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-10.8 — a voided target refuses the transition
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-10.8 when the staged lines of two Payments were posted and both journal entries were then voided, the transition fails with a typed error naming each Payment with its journal entry`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-10.8 voided X"
                let! agreementYId, legYId = scenario.agreement "CF-10.8 voided Y"
                let! lineX, jeLineX = scenario.postedLine "CF-10.8 voided X payment" 100.00M
                let! lineY, jeLineY = scenario.postedLine "CF-10.8 voided Y payment" 100.00M
                let! _, xPayments = scenario.invoice agreementXId legXId scenario.firstOfThisMonth [ Staged lineX ]
                let! _, yPayments = scenario.invoice agreementYId legYId scenario.firstOfThisMonth [ Staged lineY ]
                let! entryX = scenario.voidEntryOf jeLineX
                let! entryY = scenario.voidEntryOf jeLineY
                let expected =
                    [ (xPayments.Head |> PaymentId.value), (entryX |> JournalEntryHeaderId.value)
                      (yPayments.Head |> PaymentId.value), (entryY |> JournalEntryHeaderId.value) ]
                    |> List.sort
                return!
                    match CashFlowOps.transitionPaymentsToPosted context with
                    | Error (AsError (CashFlowError.CashflowPaymentsTargetVoidedEntries named)) ->
                        Assert.Equal<(Guid * Guid) list>(expected, named |> List.sort)
                        Ok ()
                    | Error e -> TestError.error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> TestError.error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-10.8 a transition refused for a voided target writes nothing: a Payment in the same run whose line posted to an unvoided entry still has no journal entry line ID, and every Invoice keeps its posted state`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementXId, legXId = scenario.agreement "CF-10.8 nothing X"
                let! agreementYId, legYId = scenario.agreement "CF-10.8 nothing Y"
                let! voidedLine, voidedJeLine = scenario.postedLine "CF-10.8 nothing voided payment" 100.00M
                let! keptLine, _ = scenario.postedLine "CF-10.8 nothing kept payment" 100.00M
                let! voidedInvoiceId, _ = scenario.invoice agreementXId legXId scenario.firstOfThisMonth [ Staged voidedLine ]
                let! keptInvoiceId, keptPayments = scenario.invoice agreementYId legYId scenario.firstOfThisMonth [ Staged keptLine ]
                let! _ = scenario.voidEntryOf voidedJeLine
                let! voidedStateBefore = scenario.postedStateOf voidedInvoiceId
                let! keptStateBefore = scenario.postedStateOf keptInvoiceId
                Assert.Equal(NotHandled, voidedStateBefore)
                Assert.Equal(NotHandled, keptStateBefore)
                let! _ =
                    match CashFlowOps.transitionPaymentsToPosted context with
                    | Error (AsError (CashFlowError.CashflowPaymentsTargetVoidedEntries _)) -> Ok ()
                    | Error e -> TestError.error (TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> TestError.error (TestingError "Expected failure; got success")
                let! kept = scenario.payment keptPayments.Head
                Assert.Equal(Staged keptLine, kept |> Payment.transactionPointer)
                let! voidedStateAfter = scenario.postedStateOf voidedInvoiceId
                let! keptStateAfter = scenario.postedStateOf keptInvoiceId
                Assert.Equal(voidedStateBefore, voidedStateAfter)
                Assert.Equal(keptStateBefore, keptStateAfter)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-6.4 — batch post leaves Payments alone
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-6.4 batch post alone leaves a Payment whose staged line it posted Staged, with no journal entry line ID, and its Invoice's posted state unchanged`` () =
        runCommandRouteAndAutoRollback CashFlowTransitionPaymentsToPosted (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! agreementId, legId = scenario.agreement "CF-6.4 batch post"
                let! lineId = scenario.unpostedLine "CF-6.4 batch post payment" 100.00M
                let! invoiceId, paymentIds = scenario.invoice agreementId legId scenario.firstOfThisMonth [ Staged lineId ]
                let! stateBefore = scenario.postedStateOf invoiceId
                Assert.Equal(NotHandled, stateBefore)
                do! StageEntryOrchestration.post (context |> TestContext.updateInitiationInstant)
                (* The post reached the line: it now records the journal entry line it produced. *)
                let! posted = [ lineId ] |> StageEntryLine.fetchByIdList context
                Assert.True(posted |> List.exactlyOne |> StageEntryLine.journalEntryLineId |> Option.isSome)
                let! payment = scenario.payment paymentIds.Head
                Assert.Equal(Staged lineId, payment |> Payment.transactionPointer)
                let! stateAfter = scenario.postedStateOf invoiceId
                Assert.Equal(stateBefore, stateAfter)
            })
        |> railroadWrapper
