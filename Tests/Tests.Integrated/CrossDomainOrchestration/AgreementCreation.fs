module Tests.Integrated.CrossDomainOrchestration.AgreementCreation

open NodaTime
open App.Utility
open App.Utility.Result
open App.Utility.IAppError
open Business.General
open Business.FinancialServices
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Xunit

(* Creates an Outgo, monthly-on-the-1st agreement with one leg, then reads it back by ID. The
   leg is only there because an agreement cannot exist without one. Returns the agreement read back, and the
   Master Agreement and Payment Agreement records built from the inputs, with the IDs the creation assigned and both
   timestamps the instant the context was initiated, for whole-record comparison. *)
let private createAndReadBack
    (context: App.Session.Context.Context)
    (fixture: TestDataFixture)
    (name: string)
    (startDate: LocalDate)
    (endDate: LocalDate option)
    (memo: string option)
    (nextInstance: LocalDate) =
    result {
        let! agreementName = name |> AgreementName.create
        let! first = 1 |> Cadence.DateInMonthNumber.fromInt
        let cadenceType = Cadence.Monthly(Cadence.DateInMonth first)
        let! counterparty = "Fixture counterparty" |> Counterparty.create
        let! activityPeriod =
            ActivityPeriod.create startDate endDate ActivityPeriod.ConsideredAvailableBeforeBeginDate
        let! agreementMemo =
            match memo with
            | None -> Ok None
            | Some m -> m |> AgreementMemo.create |> Result.map Some
        let! legName = $"{name} leg" |> PaymentAgreementName.create
        let leg =
            (legName,
             (DebitAccount.create fixture.Data.mortgage2210Id),
             (CreditAccount.create fixture.Data.moneyMarket1270Id),
             None, None, None)
        let! created =
            AgreementOrchestration.constructNewAndPersist
                context agreementName Outgo cadenceType { nextInstance = nextInstance } counterparty
                activityPeriod agreementMemo [ leg ]
        let agreementId = created |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
        let! readBack = agreementId |> AgreementOrchestration.fetchByMasterAgreementId context
        let now = context |> App.Session.Context.getInitiationInstant
        let! cadence = Cadence.create cadenceType { nextInstance = nextInstance }
        let expectedMaster =
            MasterAgreement.create agreementId agreementName Outgo cadence counterparty activityPeriod agreementMemo now now
        let legId = created |> AgreementOrchestration.paymentAgreements |> List.exactlyOne |> PaymentAgreement.paymentAgreementId
        let expectedLeg =
            PaymentAgreement.create
                legId agreementId legName (DebitAccount.create fixture.Data.mortgage2210Id)
                (CreditAccount.create fixture.Data.moneyMarket1270Id) None None None now now
        return readBack, expectedMaster, expectedLeg
    }

let private firstOfNextMonth () =
    let today = Calendar.today()
    LocalDate(today.Year, today.Month, 1).PlusMonths(1)

[<Collection("SharedTestData")>]
type AgreementCreationTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-2.20 through 2.24, REQ-SYS-5.1 — a created agreement reads back as created
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.20 REQ-CF-2.21 REQ-CF-2.22 REQ-CF-2.24 REQ-SYS-5.1 an agreement created with no end date and no memo reads back with its start and next-instance dates unchanged and no end date or memo`` () =
        let startDate = Calendar.today().PlusMonths(-2)
        let nextInstance = firstOfNextMonth ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! agreement, expectedMaster, expectedLeg =
                    createAndReadBack context fixture "CF-2.21 open-ended" startDate None None nextInstance
                let readBack = agreement |> AgreementOrchestration.masterAgreement
                Assert.Equal(startDate, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeBegin)
                Assert.Equal(nextInstance, (readBack |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)
                Assert.Equal(None, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd)
                Assert.Equal(None, readBack |> MasterAgreement.memo |> Option.map AgreementMemo.value)
                Assert.Equal(expectedMaster, readBack)
                Assert.Equal<PaymentAgreement.PaymentAgreement list>([ expectedLeg ], agreement |> AgreementOrchestration.paymentAgreements)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-2.20 REQ-CF-2.21 REQ-CF-2.22 REQ-CF-2.24 REQ-SYS-5.1 an agreement created with an end date and a memo reads back with start date, next-instance date, end date and memo exactly as created`` () =
        let startDate = Calendar.today().PlusMonths(-2)
        let endDate = Calendar.today().PlusYears(1)
        let nextInstance = firstOfNextMonth ()
        let memo = "Fixture agreement memo"
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! agreement, expectedMaster, expectedLeg =
                    createAndReadBack context fixture "CF-2.22 bounded" startDate (Some endDate) (Some memo) nextInstance
                let readBack = agreement |> AgreementOrchestration.masterAgreement
                Assert.Equal(startDate, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeBegin)
                Assert.Equal(nextInstance, (readBack |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)
                Assert.Equal(Some endDate, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd)
                Assert.Equal(Some memo, readBack |> MasterAgreement.memo |> Option.map AgreementMemo.value)
                Assert.Equal(expectedMaster, readBack)
                Assert.Equal<PaymentAgreement.PaymentAgreement list>([ expectedLeg ], agreement |> AgreementOrchestration.paymentAgreements)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-5.1 an Instance and its Invoice each read back by ID equal to the records created, and the Invoice's Payment reads back with every value it was created with and its journal entry's date as its posted-to-ledger date, each with created and modified timestamps the instant they were created`` () =
        let nextInstance = firstOfNextMonth ()
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! agreement, _, leg =
                    createAndReadBack context fixture "SYS-5.1 instance" (Calendar.today().PlusMonths(-2)) None None nextInstance
                let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
                let! entry, _ =
                    createTestJournalEntryFromPrimitives
                        context "SYS-5.1 instance payment" None (Calendar.today())
                        [ (fixture.Data.mortgage2210Id, 100.00M, "Debit", None)
                          (fixture.Data.moneyMarket1270Id, 100.00M, "Credit", None) ] [] []
                let line =
                    entry
                    |> JournalEntryOrchestration.jeLines
                    |> List.find (fun l -> l |> JournalEntryLine.accountId = fixture.Data.mortgage2210Id)
                    |> JournalEntryLine.journalEntryLineId
                let! amount = Money.fromDecimal 100.00M
                let! memo = "SYS-5.1 payment memo" |> PaymentMemo.create
                let! created =
                    InstanceOrchestration.constructNewAndPersist context agreementId nextInstance
                        [ (leg |> PaymentAgreement.paymentAgreementId, None, InvoiceDate.create nextInstance,
                           DueDate.create (nextInstance.PlusDays(30)), InvoiceAmount.create amount, InvoiceReceived, None, None,
                           [ (Posted(line, None), None, None, Some memo) ]) ]
                let instance = created |> InstanceOrchestration.instance
                let invoiceComposite = created |> InstanceOrchestration.invoiceComposites |> List.exactlyOne
                let invoice = invoiceComposite |> InstanceOrchestration.invoice
                let payment = invoiceComposite |> InstanceOrchestration.payments |> List.exactlyOne
                let! instanceBack = instance |> Instance.instanceId |> Instance.fetchById context
                let! invoiceBack = invoice |> Invoice.invoiceId |> Invoice.fetchById context
                let! paymentBack = payment |> Payment.paymentId |> Payment.fetchById context
                let now = context |> App.Session.Context.getInitiationInstant
                Assert.Equal(instance, instanceBack)
                Assert.Equal(invoice, invoiceBack)
                (* The posted-to-ledger date is not stored: it is read from the journal entry the pointer names. *)
                let expectedPayment =
                    Payment.create
                        (payment |> Payment.paymentId) (invoice |> Invoice.invoiceId) (Posted(line, None))
                        (PaymentAmount.create amount) None (Some(PostedToLedgerDate.create (Calendar.today()))) (Some memo) now now
                Assert.Equal(expectedPayment, paymentBack)
                Assert.Equal((now, now), (instanceBack |> Instance.createdAt, instanceBack |> Instance.modifiedAt))
                Assert.Equal((now, now), (invoiceBack |> Invoice.createdAt, invoiceBack |> Invoice.modifiedAt))
                Assert.Equal((now, now), (paymentBack |> Payment.createdAt, paymentBack |> Payment.modifiedAt))
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-3.6 — a leg cannot debit and credit the same account
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.6 an agreement whose leg debits and credits the same account is rejected naming that account`` () =
        let sameAccount = fixture.Data.mortgage2210Id
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context ->
            result {
                let! agreementName = "CF-3.6 same account" |> AgreementName.create
                let! first = 1 |> Cadence.DateInMonthNumber.fromInt
                let! counterparty = "Fixture counterparty" |> Counterparty.create
                let! activityPeriod =
                    ActivityPeriod.create (Calendar.today()) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
                let! legName = "CF-3.6 same account leg" |> PaymentAgreementName.create
                let created =
                    AgreementOrchestration.constructNewAndPersist
                        context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                        { nextInstance = firstOfNextMonth () } counterparty activityPeriod None
                        [ (legName, (DebitAccount.create sameAccount), (CreditAccount.create sameAccount), None, None, None) ]
                return
                    match created with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementDebitEqualsCredit uuid)) ->
                        Assert.Equal(sameAccount |> Business.FinancialServices.Ledger.AccountComponent.AccountId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
            })
        |> railroadWrapper
