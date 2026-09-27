namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open Tests.Helpers
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.JournalEntryVoiding
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.CrossDomainOrchestration.StageEntryOrchestration
open App.Utility.FieldUpdate

[<Collection("SharedTestData")>]
type JournalEntryVoidingTests(fixture: TestDataFixture) =

    let commentText =
        "Voiding for test"
        |> CommentText.create
        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))


    let accountIdOf code =
        fixture.Data.accounts
        |> List.find (fun a -> a |> Account.code |> AccountCode.value = code)
        |> Account.accountId

    let fetchStaged context headerId = headerId |> StageEntryOrchestration.fetchByStageEntryHeaderId context

    let stagedHeaderIdOf entry = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

    let invoiceStatesOf context =
        result {
            let! composite =
                fixture.Data.cashFlow.openInstanceAId |> InstanceOrchestration.fetchCompositeByInstanceId context
            let invoice =
                composite
                |> InstanceOrchestration.invoiceComposites
                |> List.map InstanceOrchestration.invoice
                |> List.find (fun i -> i |> Invoice.invoiceId = fixture.Data.cashFlow.openInvoiceAId)
            let lifeCycle = invoice |> Invoice.invoiceLifeCycleState
            return lifeCycle.paymentState, lifeCycle.postedState, (composite |> InstanceOrchestration.instance |> Instance.isFulfilled)
        }

    /// Adds a 100.00 Payment on the fixture's open invoice on agreement A, with the given pointer. Returns its id.
    let addPayment (context: Context.Context) (pointer: CashFlowComponent.TransactionPointer) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let update : InstanceOrchestration.InstanceCompositeUpdate =
                { instanceUpdates =
                    { instanceIdToUpdate = fixture.Data.cashFlow.openInstanceAId
                      instanceDateUpdate = NoChange
                      isFulfilledUpdate = NoChange }
                  invoiceCompositeUpdates =
                    [ { invoiceUpdates =
                          { invoiceIdToUpdate = fixture.Data.cashFlow.openInvoiceAId
                            externalInvoiceIdUpdate = NoChange
                            invoiceDateUpdate = NoChange
                            dueDateUpdate = NoChange
                            amountUpdate = NoChange
                            invoiceStateUpdate = NoChange
                            paymentStateUpdate = NoChange
                            postedStateUpdate = NoChange
                            blockerUpdate = NoChange
                            memoUpdate = NoChange }
                        paymentUpdates = []
                        paymentIdsToDelete = []
                        newPayments = [ (pointer, { money = amount }, None, None, None) ] } ]
                  newInvoices = [] }
            let! composite = InstanceOrchestration.updateInstanceComposite context update
            return
                composite
                |> InstanceOrchestration.invoiceComposites
                |> List.collect InstanceOrchestration.payments
                |> List.find (fun p -> p |> Payment.transactionPointer = pointer)
                |> Payment.paymentId
        }

    /// A 100.00 outgo staged entry on the cash flow accounts (debit F-2230, credit F-1280), Classified, then posted to
    /// the ledger at a later instant exactly as the posting step does it. When withPayment is set, a Payment on the
    /// fixture's open invoice points at its debit line before posting and moves to the journal entry line with it.
    /// Returns the entry as it was before posting, the posting context, the journal entry's id and any Payment's id.
    let createPostedStagedEntry (context: Context.Context) (description: string) (withPayment: bool) =
        result {
            let classifyContext = context |> Context.updateInitiationInstant
            let! entry =
                createStageEntryForTest context "/tmp/void-unwind.dat" description (Guid.NewGuid().ToString())
                    (fixture.Data.ingestionSources |> List.head) (Calendar.today())
                    [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                    [ (Some "Ingested", "Classified", classifyContext |> Context.getInitiationInstant, "Classifier") ]
            let debitLineId =
                entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit) |> StageEntryLine.stageEntryLineId
            let! paymentId =
                if withPayment then addPayment classifyContext (CashFlowComponent.Staged debitLineId) |> Result.map Some
                else Ok None
            let postingContext = classifyContext |> Context.updateInitiationInstant
            let! jeSource =
                Some "Data ingestion import"
                |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
            do! entry |> postStageEntry postingContext jeSource
            do!
                entry
                |> stagedHeaderIdOf
                |> StageEntryHeader.updateHeaderStatus postingContext Posted StageStatusChangeMechanism.LedgerPoster
            let! _ = CashFlowOps.transitionPaymentsToPosted postingContext
            let! posted = entry |> stagedHeaderIdOf |> fetchStaged postingContext
            let! jeId =
                match posted |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId with
                | Some jeId -> Ok jeId
                | None -> Error(TestingError "posting did not record the journal entry on the staged entry")
            return entry, postingContext, jeId, paymentId
        }

    /// A plain 100.00 journal entry on the cash flow accounts, posted directly rather than from staging. Returns its id
    /// and its debit line's id.
    let createUnstagedJournalEntry (context: Context.Context) (description: string) =
        result {
            let! je, jeId =
                createTestJournalEntryFromPrimitives context description None (Calendar.today())
                    [ (accountIdOf "F-2230", 100.00M, "Debit", None); (accountIdOf "F-1280", 100.00M, "Credit", None) ]
                    [] []
            let debitLineId =
                je |> jeLines |> List.find (fun l -> l |> JournalEntryLine.lineType = JournalEntryLineType.Debit) |> JournalEntryLine.journalEntryLineId
            return jeId, debitLineId
        }

    [<Fact>]
    member _.``REQ-JE-4.3 voidJournalEntryOrchestration sets voided_at on the entry``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! _, jeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create for void 4.3"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                let! voided = jeId |> voidJournalEntry context None commentText
                Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.4 voidJournalEntryOrchestration attaches a reason comment to the voided entry``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! je, jeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create for void 4.4"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                Assert.Equal(0, je |> comments |> List.length) // just confirming that it's zero at the satrt
                let! voided = jeId |> voidJournalEntry context None commentText
                let comments = voided |> comments
                Assert.Equal(1, comments |> List.length)
                let comment = comments |> List.head
                Assert.Equal(
                    commentText |> CommentText.value,
                    comment |> JournalEntryComment.commentText |> CommentText.value
                )
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.5 voidJournalEntryOrchestration rejects void when fiscal period is closed``() =
        let today = Calendar.today()
        let sevenMonthsAgo = today.PlusMonths(-7)
        let monthF = sevenMonthsAgo.Month.ToString("D2")
        let periodKeyStr = $"{sevenMonthsAgo.Year}-{monthF}"
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                // create Fiscal Period as open so you can add an entry into it
                let! periodKey = periodKeyStr |> FiscalPeriodKey.fromString
                let! fp = periodKey |> FiscalPeriodCreation.constructNewAndPersist context
                let fpId = fp |> FiscalPeriod.fiscalPeriodId
                // add the JE into that FP
                let! _, jeId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create for void 4.5"
                        None
                        sevenMonthsAgo
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                // close the FP
                let! _ = fpId |> FiscalPeriod.closeFiscalPeriod context
                // try to void
                let voidedResult = jeId |> voidJournalEntry context None commentText
                do!
                    match voidedResult with
                    | Error (AsError (JournalEntryVoidingFiscalPeriodIsClosed _)) -> Ok()
                    | Error e -> Error(TestingError $"Wrong error message. {e.ToMessage()}")
                    | Ok _ -> Error(TestingError "Expected failure; got success")
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.6 voidJournalEntryOrchestration rejects void on already-voided entry``() =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            let voidedResult = fixture.Data.voidedJeId |> voidJournalEntry context None commentText
            match voidedResult with
            | Error (AsError (JournalEntryVoidingNoOp _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error message. {e.ToMessage()}")
            | Ok _ -> Error(TestingError "Expected failure; got success"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.3 voidJournalEntryOrchestration returns error for nonexistent entry id``() =
        // guards the railway itself: the fetch failure must propagate as an
        // Error, not escape the orchestrator as an exception
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            let badId = Guid.NewGuid() |> JournalEntryHeaderId.fromGuid
            let voidedResult = badId |> voidJournalEntry context None commentText
            match voidedResult with
            | Error (AsError (JournalEntryHeaderIdDoesntExist _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error message. {e.ToMessage()}")
            | Ok _ -> Error(TestingError "Expected failure; got success"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.11 REQ-STG-4.7 voiding a journal entry posted from a staged entry moves that staged entry from Posted to Reviewed, with change mechanism Operator`` () =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! entry, postingContext, jeId, _ = createPostedStagedEntry context "VOID UNWIND STATUS" false
                let headerId = entry |> stagedHeaderIdOf
                let voidContext = postingContext |> Context.updateInitiationInstant
                let! _ = jeId |> voidJournalEntry voidContext None commentText
                let! after = headerId |> fetchStaged voidContext
                Assert.Equal(Some Reviewed, after |> stageEntryHeader |> StageEntryHeader.currentStatus)
                let! transitions = headerId |> StageEntryStatusTransition.fetchByHeaderId voidContext
                let voidInstant = voidContext |> Context.getInitiationInstant
                let atVoid =
                    transitions
                    |> List.filter (fun t -> t |> StageEntryStatusTransition.instant = voidInstant)
                    |> List.map (fun t ->
                        (t |> StageEntryStatusTransition.fromStatus),
                        (t |> StageEntryStatusTransition.toStatus),
                        (t |> StageEntryStatusTransition.stageStatusChangeMechanism))
                Assert.Equal<(StagedEntryStatus option * StagedEntryStatus * StageStatusChangeMechanism) list>(
                    [ (Some Posted, Reviewed, StageStatusChangeMechanism.Operator) ], atVoid)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.11 voiding a journal entry posted from a staged entry clears the staged entry's journal entry ID and every one of its lines' journal entry line IDs`` () =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! entry, postingContext, jeId, _ = createPostedStagedEntry context "VOID UNWIND POINTERS" false
                let headerId = entry |> stagedHeaderIdOf
                let! posted = headerId |> fetchStaged postingContext
                // the posting really did set them, so clearing them is the void's doing
                Assert.True(posted |> seLines |> List.forall (fun l -> l |> StageEntryLine.journalEntryLineId |> Option.isSome))
                let voidContext = postingContext |> Context.updateInitiationInstant
                let! _ = jeId |> voidJournalEntry voidContext None commentText
                let! after = headerId |> fetchStaged voidContext
                Assert.Equal(None, after |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId)
                Assert.Equal(2, after |> seLines |> List.length)
                Assert.All(after |> seLines, fun l -> Assert.Equal(None, l |> StageEntryLine.journalEntryLineId))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.12 voiding a journal entry whose line a Payment with a staged line points at clears the Payment's journal entry line so it points at its staged line again, and re-derives the Invoice's and the Instance's states`` () =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! statesBeforePayment = invoiceStatesOf context
                let! entry, postingContext, jeId, paymentId = createPostedStagedEntry context "VOID UNWIND PAYMENT" true
                let paymentId = paymentId |> Option.get
                let debitLineId =
                    entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit) |> StageEntryLine.stageEntryLineId
                let! postedPayment = paymentId |> Payment.fetchById postingContext
                Assert.True(
                    match postedPayment |> Payment.transactionPointer with
                    | CashFlowComponent.Posted _ -> true
                    | CashFlowComponent.Staged _ -> false)
                let _, postedStateWhilePosted, _ = (invoiceStatesOf postingContext) |> Result.defaultWith (fun e -> failwith (e.ToMessage()))
                Assert.Equal(CashFlowComponent.PostedToLedger, postedStateWhilePosted)
                let voidContext = postingContext |> Context.updateInitiationInstant
                let! _ = jeId |> voidJournalEntry voidContext None commentText
                let! after = paymentId |> Payment.fetchById voidContext
                Assert.Equal(CashFlowComponent.Staged debitLineId, after |> Payment.transactionPointer)
                Assert.Equal(None, after |> Payment.postedToLedgerDate)
                let! paymentState, postedState, isFulfilled = invoiceStatesOf voidContext
                // the Payment still pays the invoice in full, but nothing is posted any more
                Assert.Equal(CashFlowComponent.FullyPaid, paymentState)
                Assert.Equal(CashFlowComponent.NotHandled, postedState)
                Assert.True(isFulfilled)
                let paymentStateBefore, _, _ = statesBeforePayment
                Assert.NotEqual(paymentStateBefore, paymentState)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.12 voiding a journal entry whose line a Payment with no staged line points at is rejected with a typed error naming the Payment, nothing is changed, and the reason comment is not written`` () =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! jeId, debitLineId = createUnstagedJournalEntry context "VOID PAYMENT WITHOUT STAGED LINE"
                let! paymentId = addPayment context (CashFlowComponent.Posted debitLineId)
                let! paymentBefore = paymentId |> Payment.fetchById context
                let! statesBefore = invoiceStatesOf context
                let voidContext = context |> Context.updateInitiationInstant
                let expectedPayment = paymentId |> CashFlowComponent.PaymentId.value
                let expectedJe = jeId |> JournalEntryHeaderId.value
                do!
                    match jeId |> voidJournalEntry voidContext None commentText with
                    | Error (AsError (CashFlowError.CashflowPaymentWithoutStagedLineBlocksVoid (payment, je)))
                        when payment = expectedPayment && je = expectedJe -> Ok()
                    | Error e -> Error(TestingError $"Wrong error message. {e.ToMessage()}")
                    | Ok _ -> Error(TestingError "Expected failure; got success")
                let! je = jeId |> JournalEntryOrchestration.fetchById voidContext
                Assert.True(je |> header |> JournalEntryHeader.voidedAt |> Option.isNone)
                Assert.Empty(je |> comments)
                let! paymentAfter = paymentId |> Payment.fetchById voidContext
                Assert.Equal(paymentBefore, paymentAfter)
                let! statesAfter = invoiceStatesOf voidContext
                Assert.Equal(statesBefore, statesAfter)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.11 voiding a journal entry that was not posted from staging changes no staged entry and no Payment`` () =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                // a staged entry and its Payment, both posted, on another journal entry
                let! entry, postingContext, _, paymentId = createPostedStagedEntry context "VOID LEAVES STAGING ALONE" true
                let paymentId = paymentId |> Option.get
                let headerId = entry |> stagedHeaderIdOf
                let! jeId, _ = createUnstagedJournalEntry postingContext "VOID UNSTAGED JE"
                let! stagedBefore = headerId |> fetchStaged postingContext
                let! paymentBefore = paymentId |> Payment.fetchById postingContext
                let! postedBefore = StageEntryHeader.fetchByStatus postingContext Posted
                let voidContext = postingContext |> Context.updateInitiationInstant
                let! voided = jeId |> voidJournalEntry voidContext None commentText
                Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
                let! stagedAfter = headerId |> fetchStaged voidContext
                Assert.Equal(stagedBefore, stagedAfter)
                let! paymentAfter = paymentId |> Payment.fetchById voidContext
                Assert.Equal(paymentBefore, paymentAfter)
                let! postedAfter = StageEntryHeader.fetchByStatus voidContext Posted
                Assert.Equal(postedBefore |> List.length, postedAfter |> List.length)
            })
        |> railroadWrapper
