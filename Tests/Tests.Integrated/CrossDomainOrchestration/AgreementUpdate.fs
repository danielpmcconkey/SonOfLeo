module Tests.Integrated.CrossDomainOrchestration.AgreementUpdate

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open Business.General
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.Railroad
open NodaTime
open Xunit

let private noMasterAgreementChange agreementId : MasterAgreement.MasterAgreementFieldUpdates =
    { agreementIdToUpdate = agreementId
      agreementNameUpdate = FieldUpdate.NoChange
      directionUpdate = FieldUpdate.NoChange
      cadenceUpdate = FieldUpdate.NoChange
      counterpartyUpdate = FieldUpdate.NoChange
      activeBeginUpdate = FieldUpdate.NoChange
      activeEndUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange
      gracePeriodInDaysUpdate = FieldUpdate.NoChange }

let private noLegChange legId : PaymentAgreement.PaymentAgreementFieldUpdates =
    { paymentAgreementIdToUpdate = legId
      paymentAgreementNameUpdate = FieldUpdate.NoChange
      debitAccountUpdate = FieldUpdate.NoChange
      creditAccountUpdate = FieldUpdate.NoChange
      expectedAmountUpdate = FieldUpdate.NoChange
      daysDueAfterInvoiceDateUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

[<Collection("SharedTestData")>]
type AgreementUpdateTests(fixture: TestDataFixture) =

    let cashFlow = fixture.Data.cashFlow

    // =========================================================================
    // REQ-CF-3.6 — a leg cannot debit and credit the same account
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-3.6 updating a leg's credit account to its debit account is rejected naming that account`` () =
        (* A's leg debits F-2230; pointing its credit side there too makes both sides one account. *)
        let debitAccountId =
            fixture.Data.accounts
            |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-2230")
            |> Account.accountId
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let updated =
                    AgreementOrchestration.updateAgreement
                        context
                        [ { noLegChange cashFlow.legAId with creditAccountUpdate = FieldUpdate.SetTo(CreditAccount.create debitAccountId) } ]
                        []
                        (noMasterAgreementChange cashFlow.agreementAId)
                return
                    match updated with
                    | Error (AsError (CashFlowError.CashflowPaymentAgreementDebitEqualsCredit uuid)) ->
                        Assert.Equal(debitAccountId |> AccountId.value, uuid)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-14.2 — a direction change must leave every Invoice's state valid
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.2 REQ-CF-5.10 changing an Outgo agreement with an InvoiceReceived Invoice to Income is rejected naming the Invoice`` () =
        (* Both of A's Invoices are InvoiceReceived; either may be the one named. *)
        let aInvoiceUuids =
            [ cashFlow.paidInvoiceAId; cashFlow.openInvoiceAId ] |> List.map InvoiceId.value
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let updated =
                    AgreementOrchestration.updateAgreement
                        context [] []
                        { noMasterAgreementChange cashFlow.agreementAId with directionUpdate = FieldUpdate.SetTo Income }
                return
                    match updated with
                    | Error (AsError (CashFlowError.CashflowInvoiceStateInvalidForFlowDirection(uuid, state, direction))) ->
                        Assert.Contains(uuid, aInvoiceUuids)
                        Assert.Equal("InvoiceReceived", state)
                        Assert.Equal("Income", direction)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok _ -> Assert.Fail "Expected failure; got success"
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-14.2 — a cadence change and the next-instance date
    // =========================================================================

    (* Agreement A is monthly on the 1st. Its Instances are last month's and this month's, both on the 1st, and its
       next-instance date is the 1st of next month. *)

    [<Fact>]
    member _.``REQ-CF-14.2 REQ-CF-4.6 changing a Master Agreement's cadence leaves its existing Instances, whose dates no longer fit the new cadence, unchanged`` () =
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! latest = cashFlow.openInstanceAId |> Instance.fetchById context
                let latestDate = latest |> Instance.instanceDate
                let! before = [ cashFlow.agreementAId ] |> Instance.fetchByMasterAgreementIdList context
                (* the 15th of the latest Instance's month: later than every Instance, and no Instance falls on a 15th *)
                let fifteenth = LocalDate(latestDate.Year, latestDate.Month, 15)
                Assert.Empty(before |> List.filter (fun i -> (i |> Instance.instanceDate).Day = 15))
                let! onThe15th = 15 |> Cadence.DateInMonthNumber.fromInt
                let! cadence = Cadence.create (Cadence.Monthly(Cadence.DateInMonth onThe15th)) { nextInstance = fifteenth }
                let! _ =
                    AgreementOrchestration.updateAgreement
                        context [] [] { noMasterAgreementChange cashFlow.agreementAId with cadenceUpdate = FieldUpdate.SetTo cadence }
                let! after = [ cashFlow.agreementAId ] |> Instance.fetchByMasterAgreementIdList context
                let byId = List.sortBy (Instance.instanceId >> InstanceId.value)
                Assert.Equal<Instance.Instance list>(before |> byId, after |> byId)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("latest")>]
    [<InlineData("before")>]
    member _.``REQ-CF-14.2 for each of the latest existing Instance's date and a date before it, an update setting the next-instance date to it is rejected with a typed error naming the latest Instance date, and the agreement is unchanged`` (which: string) =
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! latest = cashFlow.openInstanceAId |> Instance.fetchById context
                let latestDate = latest |> Instance.instanceDate
                (* a month back is still the 1st, so the date fits A's cadence and only its place in time is wrong *)
                let attempted = if which = "latest" then latestDate else latestDate.PlusMonths(-1)
                let! master = cashFlow.agreementAId |> MasterAgreement.fetchById context
                let! cadence = Cadence.create (master |> MasterAgreement.cadence |> Cadence.cadenceType) { nextInstance = attempted }
                let updated =
                    AgreementOrchestration.updateAgreement
                        context [] [] { noMasterAgreementChange cashFlow.agreementAId with cadenceUpdate = FieldUpdate.SetTo cadence }
                do!
                    match updated with
                    | Error (AsError (CashFlowError.CashflowMasterAgreementNextInstanceNotAfterExistingInstances(uuid, date, latestNamed))) ->
                        Assert.Equal(cashFlow.agreementAId |> MasterAgreementId.value, uuid)
                        Assert.Equal(attempted, date)
                        Assert.Equal(latestDate, latestNamed)
                        Ok ()
                    | Error e -> Error (TestError.TestingError $"Wrong error. {e.ToMessage()}")
                    | Ok _ -> Error (TestError.TestingError "Expected failure; got success")
                let! after = cashFlow.agreementAId |> MasterAgreement.fetchById context
                Assert.Equal(master, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-14.2 an update setting the next-instance date to the first cadence date after the latest existing Instance is stored`` () =
        (* A Daily cadence's first date after the latest Instance is the day after it. *)
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! latest = cashFlow.openInstanceAId |> Instance.fetchById context
                let dayAfter = (latest |> Instance.instanceDate).PlusDays(1)
                let! cadence = Cadence.create Cadence.Daily { nextInstance = dayAfter }
                let! _ =
                    AgreementOrchestration.updateAgreement
                        context [] [] { noMasterAgreementChange cashFlow.agreementAId with cadenceUpdate = FieldUpdate.SetTo cadence }
                let! stored = cashFlow.agreementAId |> MasterAgreement.fetchById context
                let storedCadence = stored |> MasterAgreement.cadence
                Assert.Equal(Cadence.Daily, storedCadence |> Cadence.cadenceType)
                Assert.Equal(dayAfter, (storedCadence |> Cadence.nextInstance).nextInstance)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-14.2 — grace period in days
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-14.2 an agreement update setting the grace period in days from none to 12 reads back 12, and a second update setting it to none reads back none`` () =
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let graceOf () =
                    cashFlow.agreementAId
                    |> MasterAgreement.fetchById context
                    |> Result.map (MasterAgreement.gracePeriodInDays >> Option.map GracePeriodInDays.value)
                let! initially = graceOf ()
                Assert.Equal(None, initially)
                let! twelve = GracePeriodInDays.create 12
                let! _ =
                    AgreementOrchestration.updateAgreement
                        context [] [] { noMasterAgreementChange cashFlow.agreementAId with gracePeriodInDaysUpdate = FieldUpdate.SetTo(Some twelve) }
                let! afterSetting = graceOf ()
                Assert.Equal(Some 12, afterSetting)
                let! _ =
                    AgreementOrchestration.updateAgreement
                        context [] [] { noMasterAgreementChange cashFlow.agreementAId with gracePeriodInDaysUpdate = FieldUpdate.SetTo None }
                let! afterClearing = graceOf ()
                Assert.Equal(None, afterClearing)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("12")>]
    [<InlineData("none")>]
    member _.``REQ-CF-14.2 for each of a grace period in days of 12 and none, a cadence change from Monthly to Daily leaves the grace period in days as it was`` (grace: string) =
        (* Agreement A starts with no grace period of its own; the 12 case gives it one before the cadence change. *)
        let expected = if grace = "12" then Some 12 else None
        runCommandRouteAndAutoRollback CashFlowUpdateAgreement (fun context ->
            result {
                let! own = expected |> convertOptionToDesiredTypeWithFallibleConverter GracePeriodInDays.create
                let! _ =
                    match own with
                    | Some _ ->
                        AgreementOrchestration.updateAgreement
                            context [] [] { noMasterAgreementChange cashFlow.agreementAId with gracePeriodInDaysUpdate = FieldUpdate.SetTo own }
                        |> Result.map ignore
                    | None -> Ok ()
                let! latest = cashFlow.openInstanceAId |> Instance.fetchById context
                let! daily = Cadence.create Cadence.Daily { nextInstance = (latest |> Instance.instanceDate).PlusDays(40) }
                let! _ =
                    AgreementOrchestration.updateAgreement
                        context [] [] { noMasterAgreementChange cashFlow.agreementAId with cadenceUpdate = FieldUpdate.SetTo daily }
                let! stored = cashFlow.agreementAId |> MasterAgreement.fetchById context
                Assert.Equal(Cadence.Daily, stored |> MasterAgreement.cadence |> Cadence.cadenceType)
                Assert.Equal(expected, stored |> MasterAgreement.gracePeriodInDays |> Option.map GracePeriodInDays.value)
            })
        |> railroadWrapper
