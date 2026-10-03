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

    let stagedHeaderIdOf entry = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId

    let debitLineIdOf entry =
        entry
        |> seLines
        |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryLineType.Debit)
        |> StageEntryLine.stageEntryLineId

    /// Adds a 100.00 Payment on the fixture's open invoice on agreement A, with the given pointer. Returns its id.
    let addPayment (context: Context.Context) (pointer: CashFlowComponent.TransactionPointer) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let update : InstanceOrchestration.InstanceCompositeUpdate =
                { instanceUpdates =
                    { instanceIdToUpdate = fixture.Data.cashFlow.openInstanceAId
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

    /// A 100.00 outgo staged entry on the cash flow accounts (debit F-2230, credit F-1280), Classified, then posted at
    /// a later instant the way batch post does it: the journal entry, the entry's and lines' links to it, and Posted.
    /// When withPayment is set, a Payment on the fixture's open invoice points at the debit line before posting and
    /// moves to the journal entry line with it. Returns the entry before posting, the posting context, the journal
    /// entry's id and the Payment's id, if any.
    let createPostedStagedEntry (context: Context.Context) (description: string) (withPayment: bool) =
        result {
            let classifyContext = context |> TestContext.updateInitiationInstant
            let! entry =
                createStageEntryForTest context "/tmp/void-staging.dat" description (Guid.NewGuid().ToString())
                    (fixture.Data.ingestionSources |> List.head) (Calendar.today())
                    [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                    [ (Some "Ingested", "Classified", classifyContext |> Context.getInitiationInstant, "Classifier") ]
            let! paymentId =
                if withPayment then
                    addPayment classifyContext (CashFlowComponent.Staged(entry |> debitLineIdOf)) |> Result.map Some
                else Ok None
            let postingContext = classifyContext |> TestContext.updateInitiationInstant
            let! jeSource =
                Some "Data ingestion import"
                |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
            do! entry |> postStageEntry postingContext jeSource
            do!
                entry
                |> stagedHeaderIdOf
                |> StageEntryHeader.updateHeaderStatus postingContext Posted StageStatusChangeMechanism.LedgerPoster
            let! _ = CashFlowOps.transitionPaymentsToPosted postingContext
            let! posted = entry |> stagedHeaderIdOf |> StageEntryOrchestration.fetchByStageEntryHeaderId postingContext
            let! jeId =
                match posted |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId with
                | Some jeId -> Ok jeId
                | None -> Error(TestingError "posting did not record the journal entry on the staged entry")
            return entry, postingContext, jeId, paymentId
        }

    /// A plain 100.00 journal entry on the cash flow accounts, not posted from staging, with a Payment on the fixture's
    /// open invoice pointing straight at its debit line. Returns the journal entry's id and the Payment's id.
    let createUnstagedEntryWithPayment (context: Context.Context) (description: string) =
        result {
            let! je, jeId =
                createTestJournalEntryFromPrimitives context description None (Calendar.today())
                    [ (accountIdOf "F-2230", 100.00M, "Debit", None); (accountIdOf "F-1280", 100.00M, "Credit", None) ]
                    [] []
            let debitLineId =
                je |> jeLines |> List.find (fun l -> l |> JournalEntryLine.lineType = JournalEntryLineType.Debit)
                |> JournalEntryLine.journalEntryLineId
            let! paymentId = addPayment context (CashFlowComponent.Posted debitLineId)
            return jeId, paymentId
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
    member _.``REQ-JE-4.13 voiding a journal entry posted from a staged entry leaves the staged entry and every one of its lines exactly as they were before the void: still Posted with no new status transition, and keeping their journal entry and journal entry line IDs`` () =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! entry, postingContext, jeId, _ = createPostedStagedEntry context "VOID LEAVES STAGING" false
                let headerId = entry |> stagedHeaderIdOf
                let! before = headerId |> StageEntryOrchestration.fetchByStageEntryHeaderId postingContext
                let! transitionsBefore = headerId |> StageEntryStatusTransition.fetchByHeaderId postingContext
                // the posting really did link them, so an unchanged link is the void leaving it alone
                Assert.Equal(Some jeId, before |> stageEntryHeader |> StageEntryHeader.journalEntryHeaderId)
                Assert.All(before |> seLines, fun l -> Assert.True(l |> StageEntryLine.journalEntryLineId |> Option.isSome))
                let voidContext = postingContext |> TestContext.updateInitiationInstant
                let! voided = jeId |> voidJournalEntry voidContext None commentText
                Assert.True(voided |> header |> JournalEntryHeader.voidedAt |> Option.isSome)
                let! after = headerId |> StageEntryOrchestration.fetchByStageEntryHeaderId voidContext
                let! transitionsAfter = headerId |> StageEntryStatusTransition.fetchByHeaderId voidContext
                Assert.Equal(before, after)
                Assert.Equal(Some Posted, after |> stageEntryHeader |> StageEntryHeader.currentStatus)
                Assert.Equal<StageEntryStatusTransition.StageEntryStatusTransition list>(transitionsBefore, transitionsAfter)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData(true)>]
    [<InlineData(false)>]
    member _.``REQ-JE-4.14 voiding a journal entry when a Payment's transaction pointer references one of its lines is rejected with a typed error naming that Payment, and the journal entry stays unvoided with no reason comment written, whether or not the Payment also has a staged line`` (paymentHasStagedLine: bool) =
        runCommandRouteAndAutoRollback JournalEntryVoid (fun context ->
            result {
                let! jeId, paymentId, setupContext =
                    if paymentHasStagedLine then
                        createPostedStagedEntry context "VOID REFUSED STAGED PAYMENT" true
                        |> Result.map (fun (_, postingContext, jeId, paymentId) -> jeId, paymentId |> Option.get, postingContext)
                    else
                        createUnstagedEntryWithPayment context "VOID REFUSED LEDGER PAYMENT"
                        |> Result.map (fun (jeId, paymentId) -> jeId, paymentId, context)
                let! paymentBefore = paymentId |> Payment.fetchById setupContext
                // the Payment really does point at the entry's line, whatever its origin
                Assert.True(
                    match paymentBefore |> Payment.transactionPointer with
                    | CashFlowComponent.Posted _ -> true
                    | CashFlowComponent.Staged _ -> false)
                let voidContext = setupContext |> TestContext.updateInitiationInstant
                let expectedPayments = [ paymentId |> CashFlowComponent.PaymentId.value ]
                let expectedJe = jeId |> JournalEntryHeaderId.value
                do!
                    match jeId |> voidJournalEntry voidContext None commentText with
                    | Error (AsError (CashFlowError.CashflowPaymentsReferenceEntryBeingVoided (payments, je)))
                        when payments = expectedPayments && je = expectedJe -> Ok()
                    | Error e -> Error(TestingError $"Wrong error message. {e.ToMessage()}")
                    | Ok _ -> Error(TestingError "Expected failure; got success")
                let! je = jeId |> JournalEntryOrchestration.fetchById voidContext
                Assert.True(je |> header |> JournalEntryHeader.voidedAt |> Option.isNone)
                Assert.Empty(je |> comments)
                let! paymentAfter = paymentId |> Payment.fetchById voidContext
                Assert.Equal(paymentBefore, paymentAfter)
            })
        |> railroadWrapper
