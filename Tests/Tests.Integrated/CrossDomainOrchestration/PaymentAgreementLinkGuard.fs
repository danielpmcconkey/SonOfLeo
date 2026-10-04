module Tests.Integrated.CrossDomainOrchestration.PaymentAgreementLinkGuard

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
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.TestError
open Xunit

(* Every test builds, inside a transaction that is rolled back, two Outgo agreements X and Y (debit F-2230, credit
   F-1280, one 100.00 leg each), a Classified staged entry whose Debit line is linked to X's leg, and a 100.00 Invoice
   on X carrying a Staged Payment that points at that line. *)
type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let today = Calendar.today ()
    let firstOfThisMonth = LocalDate(today.Year, today.Month, 1)
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let testBank =
        fixture.Data.ingestionSources
        |> List.find (fun source -> source |> IngestionSource.name |> JournalRefFinancialInstitution.value = "TestBank")

    /// An Outgo agreement with one 100.00 leg. Returns the agreement's id and its leg's id.
    member _.agreement (name: string) =
        result {
            let! agreementName = name |> AgreementName.create
            let! first = 1 |> Cadence.DateInMonthNumber.fromInt
            let! counterparty = "Link guard test counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create (firstOfThisMonth.PlusMonths(-3)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legName = $"{name} leg" |> PaymentAgreementName.create
            let! expected = Money.fromDecimal 100.00M
            let! due = 0 |> DaysDueAfterInvoiceDate.create
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo (Cadence.Monthly(Cadence.DateInMonth first))
                    { nextInstance = firstOfThisMonth.PlusMonths(1) } counterparty activityPeriod None
                    [ (legName, (DebitAccount.create (accountIdOf "F-2230")), (CreditAccount.create (accountIdOf "F-1280")),
                       Some expected, Some due, None) ]
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legId = agreement |> AgreementOrchestration.paymentAgreements |> List.head |> PaymentAgreement.paymentAgreementId
            return agreementId, legId
        }

    /// Agreements X and Y, a staged Debit line linked to X's leg, and a Payment on X's Invoice pointing at that line.
    /// Returns Y's leg id, the line, the link and the Payment's id.
    member this.paidLink (name: string) =
        result {
            let! agreementXId, legXId = this.agreement $"{name} X"
            let! _, legYId = this.agreement $"{name} Y"
            let start = Clock.now ()
            let! entry =
                createStageEntryForTest context "/tmp/link-guard-test.dat" $"{name} payment" (Guid.NewGuid().ToString())
                    testBank firstOfThisMonth
                    [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                    [ (None, "Ingested", start, "StageIngestion")
                      (Some "Ingested", "Classified", start.Plus(Duration.FromMilliseconds(10L)), "Classifier") ]
            let lineId =
                entry
                |> StageEntryOrchestration.seLines
                |> List.find (fun l -> l |> StageEntryLine.lineType = JournalEntryComponent.JournalEntryLineType.Debit)
                |> StageEntryLine.stageEntryLineId
            let! link = CashFlowOps.constructNewAndPersist context legXId lineId
            let! amount = Money.fromDecimal 100.00M
            let! created =
                InstanceOrchestration.constructNewAndPersist
                    context agreementXId firstOfThisMonth
                    [ (legXId, None, InvoiceDate.create firstOfThisMonth, DueDate.create (firstOfThisMonth.PlusDays(30)),
                       InvoiceAmount.create amount, InvoiceReceived, None, None, [ (Staged lineId, None, None, None) ]) ]
            let paymentId =
                created
                |> InstanceOrchestration.invoiceComposites
                |> List.head
                |> InstanceOrchestration.payments
                |> List.exactlyOne
                |> Payment.paymentId
            return legYId, lineId, link, paymentId
        }

    member _.linksOf (lineId: StageEntryLineId) = lineId |> PaymentAgreementLink.fetchByStageEntryLineId context

/// Ok with the Payment IDs a CashflowPaymentAgreementLinkLineHasPayments error named for the link, or an error saying
/// what came back instead.
let private paymentsNamedBy (link: PaymentAgreementLink.PaymentAgreementLink) (attempt: Result<'T, IAppError>) =
    match attempt with
    | Error (AsError (CashFlowError.CashflowPaymentAgreementLinkLineHasPayments (linkUuid, paymentUuids))) ->
        Assert.Equal(link |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLinkId.value, linkUuid)
        Ok paymentUuids
    | Error e -> TestError.error (TestingError $"Wrong error. {e.ToMessage()}")
    | Ok _ -> TestError.error (TestingError "Expected failure; got success")

[<Collection("SharedTestData")>]
type PaymentAgreementLinkGuardTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-12.9 re-pointing a Payment Agreement Link whose staged line a Payment references is rejected with a typed error naming the Payment, and the link still names its original Payment Agreement`` () =
        runCommandRouteAndAutoRollback UpdatePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! legYId, lineId, link, paymentId = scenario.paidLink "CF-12.9 repoint"
                let! named =
                    CashFlowOps.updatePaymentAgreementLink context
                        { linkIdToUpdate = link |> PaymentAgreementLink.paymentAgreementLinkId
                          paymentAgreementIdUpdate = FieldUpdate.SetTo legYId }
                    |> paymentsNamedBy link
                Assert.Equal<Guid list>([ paymentId |> PaymentId.value ], named)
                let! after = scenario.linksOf lineId
                let stored = Assert.Single(after)
                Assert.Equal(link |> PaymentAgreementLink.paymentAgreementLinkId, stored |> PaymentAgreementLink.paymentAgreementLinkId)
                Assert.Equal(link |> PaymentAgreementLink.paymentAgreementId, stored |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.9 deleting a Payment Agreement Link whose staged line a Payment references is rejected with a typed error naming the Payment, and the link still exists`` () =
        runCommandRouteAndAutoRollback DeletePaymentAgreementLink (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, lineId, link, paymentId = scenario.paidLink "CF-12.9 delete"
                let! named =
                    link |> PaymentAgreementLink.paymentAgreementLinkId |> CashFlowOps.deletePaymentAgreementLink context
                    |> paymentsNamedBy link
                Assert.Equal<Guid list>([ paymentId |> PaymentId.value ], named)
                let! after = scenario.linksOf lineId
                let stored = Assert.Single(after)
                Assert.Equal(link |> PaymentAgreementLink.paymentAgreementLinkId, stored |> PaymentAgreementLink.paymentAgreementLinkId)
                Assert.Equal(link |> PaymentAgreementLink.paymentAgreementId, stored |> PaymentAgreementLink.paymentAgreementId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-12.9 REQ-CF-14.6 deleting the Payment that blocked a link's re-point or delete removes that link along with the Payment`` () =
        runCommandRouteAndAutoRollback CashFlowDeletePayment (fun context ->
            result {
                let scenario = Scenario(fixture, context)
                let! _, lineId, link, paymentId = scenario.paidLink "CF-12.9 undo"
                (* The Payment blocks the link's delete. *)
                let! named =
                    link |> PaymentAgreementLink.paymentAgreementLinkId |> CashFlowOps.deletePaymentAgreementLink context
                    |> paymentsNamedBy link
                Assert.Equal<Guid list>([ paymentId |> PaymentId.value ], named)
                let! _ = paymentId |> CashFlowOps.deletePaymentAndItsLinkage context
                let! linksAfter = scenario.linksOf lineId
                Assert.Empty(linksAfter)
                let! paymentsAfter = [ lineId ] |> Payment.fetchByStageEntryLineIdList context
                Assert.Empty(paymentsAfter)
            })
        |> railroadWrapper
