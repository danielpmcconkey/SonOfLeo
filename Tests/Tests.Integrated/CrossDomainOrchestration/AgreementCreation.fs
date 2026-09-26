module Tests.Integrated.CrossDomainOrchestration.AgreementCreation

open NodaTime
open App.Utility
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open Xunit

(* Creates an Outgo, monthly-on-the-1st agreement with one leg, then reads it back by ID. The
   leg is only there because an agreement cannot exist without one. *)
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
             DebitAccount fixture.Data.mortgage2210Id,
             CreditAccount fixture.Data.moneyMarket1270Id,
             None, None, None)
        let! created =
            AgreementOrchestration.constructNewAndPersist
                context agreementName Outgo cadenceType { nextInstance = nextInstance } counterparty
                activityPeriod agreementMemo [ leg ]
        let agreementId = created |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
        let! readBack = agreementId |> AgreementOrchestration.fetchByMasterAgreementId context
        return readBack |> AgreementOrchestration.masterAgreement
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
                let! readBack =
                    createAndReadBack context fixture "CF-2.21 open-ended" startDate None None nextInstance
                Assert.Equal(startDate, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeBegin)
                Assert.Equal(nextInstance, (readBack |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)
                Assert.Equal(None, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd)
                Assert.Equal(None, readBack |> MasterAgreement.memo |> Option.map AgreementMemo.value)
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
                let! readBack =
                    createAndReadBack context fixture "CF-2.22 bounded" startDate (Some endDate) (Some memo) nextInstance
                Assert.Equal(startDate, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeBegin)
                Assert.Equal(nextInstance, (readBack |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)
                Assert.Equal(Some endDate, readBack |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd)
                Assert.Equal(Some memo, readBack |> MasterAgreement.memo |> Option.map AgreementMemo.value)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-3.6 — a leg cannot debit and credit the same account
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.6 an agreement whose leg debits and credits the same account is rejected naming that account`` () =
        Assert.Fail "not implemented"
