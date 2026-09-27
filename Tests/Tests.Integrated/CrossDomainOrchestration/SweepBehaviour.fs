module Tests.Integrated.CrossDomainOrchestration.SweepBehaviour

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Result
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
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

(* Every test builds its own agreements on the fixture's cash flow accounts: F-2230 (a liability) and F-1280 (cash) for
   Outgo, F-1280 and F-4290 (revenue) for Income. The sweep reads every active agreement, the fixture's included, so
   each test reads back only its own agreements. "Today" is the sweep's own today: the date of the context's initiation
   instant. The dates a cadence should yield are worked out here day by day from what the cadence means, not with the
   code's next-date function, which is what the sweep itself uses. *)

let private daysBetween (earlier: LocalDate) (later: LocalDate) = Period.Between(earlier, later, PeriodUnits.Days).Days

/// Every date from `from` through `through`, inclusive, that `fits`.
let private datesFrom (from: LocalDate) (through: LocalDate) (fits: LocalDate -> bool) =
    List.unfold (fun (d: LocalDate) -> if d > through then None else Some(d, d.PlusDays(1))) from |> List.filter fits

let private orFail (r: Result<'a, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

/// A cadence named by the theory, with the test's own reading of the dates it falls on.
let private cadenceCase (name: string) (today: LocalDate) : Cadence.CadenceType * (LocalDate -> bool) =
    match name with
    | "Weekly" ->
        let day = today.PlusDays(3).DayOfWeek
        Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek day), (fun d -> d.DayOfWeek = day)
    | "MonthlyDateInMonth" ->
        Cadence.Monthly(Cadence.DateInMonth(15 |> Cadence.DateInMonthNumber.fromInt |> orFail)), (fun d -> d.Day = 15)
    | "MonthlyNthWeekDay" ->
        (* The second Tuesday: a Tuesday falling on the 8th through the 14th. *)
        Cadence.Monthly(Cadence.NthWeekDay(2 |> Cadence.WeekInMonthNumber.fromInt |> orFail, Cadence.Tuesday)),
        (fun d -> d.DayOfWeek = IsoDayOfWeek.Tuesday && d.Day >= 8 && d.Day <= 14)
    | "MonthlyLast" -> Cadence.Monthly Cadence.Last, (fun d -> d.PlusDays(1).Day = 1)
    | "Annually" ->
        let month =
            today.ToString("MMMM", Globalization.CultureInfo.InvariantCulture) |> Cadence.Month.fromString |> orFail
        Cadence.Annually(month, Cadence.DateInMonth(1 |> Cadence.DateInMonthNumber.fromInt |> orFail)),
        (fun d -> d.Month = today.Month && d.Day = 1)
    | other -> failwith $"no cadence {other}"

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cashId = accountIdOf "F-1280"
    let revenueId = accountIdOf "F-4290"
    let fetchInstancesOf agreementId = [ agreementId ] |> Instance.fetchByMasterAgreementIdList context

    member _.today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

    /// An agreement with a leg for each (expected amount, days due) pair, in order. Returns the agreement's id and its
    /// legs' ids in the same order.
    member _.agreementWith
        (name: string) (direction: FlowDirection) (cadence: Cadence.CadenceType) (nextInstance: LocalDate)
        (start: LocalDate) (endDate: LocalDate option) (legs: (decimal option * int option) list) =
        result {
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Sweep behaviour test counterparty" |> Counterparty.create
            let! activityPeriod = ActivityPeriod.create start endDate ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let debit, credit =
                match direction with
                | Outgo -> loanId, cashId
                | Income -> cashId, revenueId
            let legNames = legs |> List.mapi (fun i _ -> $"{name} leg {i + 1}")
            let! legComponents =
                List.zip legNames legs
                |> List.map (fun (legName, (expected, due)) ->
                    result {
                        let! paName = legName |> PaymentAgreementName.create
                        let! expectedMoney =
                            match expected with
                            | Some amount -> Money.fromDecimal amount |> Result.map Some
                            | None -> Ok None
                        let! daysDue =
                            match due with
                            | Some days -> DaysDueAfterInvoiceDate.create days |> Result.map Some
                            | None -> Ok None
                        return (paName, DebitAccount debit, CreditAccount credit, expectedMoney, daysDue, None)
                    })
                |> convertListOfResultsToResultsList
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName direction cadence { nextInstance = nextInstance } counterparty activityPeriod
                    None legComponents
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let paymentAgreements = agreement |> AgreementOrchestration.paymentAgreements
            let legIds =
                legNames
                |> List.map (fun legName ->
                    paymentAgreements
                    |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                    |> PaymentAgreement.paymentAgreementId)
            return agreementId, legIds
        }

    /// Replaces the agreement's activity period at the model level, which checks nothing. This is the only way to
    /// hold an agreement whose end date has passed.
    member _.setActivityPeriod (agreementId: MasterAgreementId) (start: LocalDate) (endDate: LocalDate option) =
        result {
            let! period = ActivityPeriod.create start endDate ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! _ =
                MasterAgreement.update context
                    { agreementIdToUpdate = agreementId
                      agreementNameUpdate = NoChange
                      directionUpdate = NoChange
                      cadenceUpdate = NoChange
                      counterpartyUpdate = NoChange
                      activityPeriodUpdate = SetTo period
                      memoUpdate = NoChange }
            return ()
        }

    /// Moves the agreement's next-instance date without touching its Instances.
    member _.setNextInstance (agreementId: MasterAgreementId) (date: LocalDate) =
        result {
            let! master = agreementId |> MasterAgreement.fetchById context
            let! cadence = Cadence.create (master |> MasterAgreement.cadence |> Cadence.cadenceType) { nextInstance = date }
            let! _ = master |> MasterAgreement.updateCadence context cadence
            return ()
        }

    member _.setExpectedAmount (legId: PaymentAgreementId) (amount: decimal) =
        result {
            let! money = Money.fromDecimal amount
            let! _ =
                PaymentAgreement.update context
                    { paymentAgreementIdToUpdate = legId
                      paymentAgreementNameUpdate = NoChange
                      debitAccountUpdate = NoChange
                      creditAccountUpdate = NoChange
                      expectedAmountUpdate = SetTo(Some money)
                      daysDueAfterInvoiceDateUpdate = NoChange
                      memoUpdate = NoChange }
            return ()
        }

    /// A journal entry for the amount; returns its Debit line, on F-2230.
    member this.ledgerLine (description: string) (amount: decimal) =
        result {
            let! entry, _ =
                createTestJournalEntryFromPrimitives
                    context description None this.today
                    [ (loanId, amount, "Debit", None); (cashId, amount, "Credit", None) ] [] []
            return
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = loanId)
                |> JournalEntryLine.journalEntryLineId
        }

    /// An Instance made outside the sweep, with one 100.00 Invoice on the leg (when given) carrying a posted Payment
    /// of `paid` (when non-zero). Returns the Instance's id, its Invoices' ids and its Payments' ids.
    member this.instance agreementId (legId: PaymentAgreementId option) (date: LocalDate) (paid: decimal) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let! payments =
                if paid = 0M then Ok [] else
                result {
                    let! line = this.ledgerLine $"Sweep test payment {date}" paid
                    let! paidMoney = Money.fromDecimal paid
                    return [ (Posted line, { money = paidMoney }, None, None, None) ]
                }
            let invoices =
                match legId with
                | Some leg ->
                    [ (leg, None, { InvoiceDate.localDate = date }, { DueDate.localDate = date.PlusDays(30) }, { InvoiceAmount.money = amount },
                       InvoiceReceived, None, None, payments) ]
                | None -> []
            let! created = InstanceOrchestration.createInstanceCompositeAndSaveToDb context agreementId date invoices
            let instanceId = created |> InstanceOrchestration.instance |> Instance.instanceId
            let composites = created |> InstanceOrchestration.invoiceComposites
            let invoiceIds = composites |> List.map (InstanceOrchestration.invoice >> Invoice.invoiceId)
            let paymentIds = composites |> List.collect InstanceOrchestration.payments |> List.map Payment.paymentId
            return instanceId, invoiceIds, paymentIds
        }

    member _.sweep (days: int) =
        result {
            let! horizon = days |> ProjectionHorizonInDays.create
            return! horizon |> CashFlowOps.createUpcomingInstances context
        }

    member _.instancesOf agreementId = fetchInstancesOf agreementId

    member _.instanceDatesOf agreementId =
        fetchInstancesOf agreementId |> Result.map (List.map Instance.instanceDate >> List.sort)

    /// Every Invoice on the agreement's Instances, as (Instance date, leg, amount, invoice date, due date, state),
    /// sorted by Instance date then amount.
    member _.invoiceRowsOf agreementId =
        result {
            let! instances = fetchInstancesOf agreementId
            if instances |> List.isEmpty then return [] else
            let dateOf =
                instances |> List.map (fun i -> (i |> Instance.instanceId), (i |> Instance.instanceDate)) |> Map.ofList
            let! invoices = instances |> List.map Instance.instanceId |> Invoice.fetchByInstanceIdList context
            return
                invoices
                |> List.map (fun invoice ->
                    dateOf[invoice |> Invoice.instanceId],
                    invoice |> Invoice.paymentAgreementId,
                    (invoice |> Invoice.amount).money |> Money.amount,
                    (invoice |> Invoice.invoiceDate).localDate,
                    (invoice |> Invoice.dueDate).localDate,
                    (invoice |> Invoice.invoiceLifeCycleState).invoiceState)
                |> List.sortBy (fun (instanceDate, _, amount, _, _, _) -> instanceDate, amount)
        }

    member _.nextInstanceOf agreementId =
        agreementId
        |> MasterAgreement.fetchById context
        |> Result.map (fun master -> (master |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)

/// Every Instance, Invoice and Payment in the database, and every active agreement's next-instance date, each sorted
/// by id, read through the given context.
let private everything (context: Context.Context) =
    result {
        let! unfulfilled = Instance.fetchByIsFulfilled context false
        let! fulfilled = Instance.fetchByIsFulfilled context true
        let instances = unfulfilled @ fulfilled |> List.sortBy (Instance.instanceId >> InstanceId.value)
        let! invoices =
            if instances |> List.isEmpty then Ok []
            else instances |> List.map Instance.instanceId |> Invoice.fetchByInstanceIdList context
        let invoices = invoices |> List.sortBy (Invoice.invoiceId >> InvoiceId.value)
        let! payments =
            if invoices |> List.isEmpty then Ok []
            else invoices |> List.map Invoice.invoiceId |> Payment.fetchByInvoiceIdList context
        let payments = payments |> List.sortBy (Payment.paymentId >> PaymentId.value)
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        let nextInstances =
            agreements
            |> List.map (fun agreement ->
                let master = agreement |> AgreementOrchestration.masterAgreement
                (master |> MasterAgreement.agreementID |> MasterAgreementId.value),
                (master |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)
            |> List.sort
        return instances, invoices, payments, nextInstances
    }

[<Collection("SharedTestData")>]
type SweepBehaviourTests(fixture: TestDataFixture) =

    let noInvoiceLeg = [ (None, None) ]

    (* The two REQ-CF-7.15 tests commit their setup, because what they check is what a failed sweep leaves behind once
       its transaction is gone, and a test that rolls back its own transaction can't see that. Each runs the sweep the
       way its route does, under a transaction that commits on success and rolls back on failure, then reads back from
       a fresh context. The sweep does not order the agreements it reads, so a Daily control agreement is made both
       before and after the failing one: whichever way the read runs, a control is swept before the failure. Every
       agreement made is deleted in the finally. *)
    let sweepFailureLeavesNothing (label: string) (makeFailingAgreement: Scenario -> Result<MasterAgreementId, IAppError>) (isExpectedFailure: IAppError -> bool) =
        let mutable agreementIds: Guid list = []
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! created =
                    runCommandRouteAndAutoCompleteTransaction CashFlowCreateUpcomingInstances (fun context ->
                        result {
                            let s = Scenario(fixture, context)
                            let! controlBefore, _ =
                                s.agreementWith $"CF-7.15 {label} control before" Outgo Cadence.Daily s.today
                                    (s.today.PlusDays(-30)) None [ (Some 100.00M, Some 0) ]
                            agreementIds <- (controlBefore |> MasterAgreementId.value) :: agreementIds
                            let! failing = makeFailingAgreement s
                            agreementIds <- (failing |> MasterAgreementId.value) :: agreementIds
                            let! controlAfter, _ =
                                s.agreementWith $"CF-7.15 {label} control after" Outgo Cadence.Daily s.today
                                    (s.today.PlusDays(-30)) None [ (Some 100.00M, Some 0) ]
                            agreementIds <- (controlAfter |> MasterAgreementId.value) :: agreementIds
                            return [ controlBefore; controlAfter ]
                        })
                let controls = created
                let! before = everything (Context.create NoTransaction FetchOnly)
                let run =
                    runCommandRouteAndAutoCompleteTransaction CashFlowCreateUpcomingInstances (fun context ->
                        result {
                            let! horizon = 10 |> ProjectionHorizonInDays.create
                            return! horizon |> CashFlowOps.createUpcomingInstances context
                        })
                let failedAsExpected =
                    match run with
                    | Error e -> isExpectedFailure e
                    | Ok _ -> false
                Assert.True(failedAsExpected)
                let! after = everything (Context.create NoTransaction FetchOnly)
                Assert.Equal(before, after)
                let! controlInstances = controls |> Instance.fetchByMasterAgreementIdList (Context.create NoTransaction FetchOnly)
                Assert.Empty(controlInstances)
            }
            |> railroadWrapper
        finally
            [ for id in agreementIds do yield Cleanup.cleanUpMasterAgreementTree (Some id) ]
            |> List.iter (function
                | Ok () -> ()
                | Error e -> cleanUpFailures.Add(e.ToMessage()))
        Assert.Empty(cleanUpFailures)

    // =========================================================================
    // REQ-CF-7.1, 7.2 — the horizon and which agreements are swept
    // =========================================================================

    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(365)>]
    member _.``REQ-CF-7.1 REQ-CF-7.3 for each of 1 and 365, a sweep with that horizon gives a Daily agreement whose next-instance date is today an Instance on today plus the horizon and none on the day after`` (horizon: int) =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let! agreementId, _ =
                    s.agreementWith "CF-7.1 horizon" Outgo Cadence.Daily s.today (s.today.PlusDays(-30)) None noInvoiceLeg
                let! _ = s.sweep horizon
                let! dates = s.instanceDatesOf agreementId
                Assert.Contains(s.today.PlusDays(horizon), dates)
                Assert.DoesNotContain(s.today.PlusDays(horizon + 1), dates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement whose end date is before today gets no Instance, even for missed cadence dates on or before that end date, while an active agreement in the same sweep does`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let start = s.today.PlusDays(-60)
                (* Its next-instance date is 20 days ago, so 16 missed Daily dates fall on or before its end date. *)
                let! endedId, _ =
                    s.agreementWith "CF-7.2 ended" Outgo Cadence.Daily (s.today.PlusDays(-20)) start None noInvoiceLeg
                do! s.setActivityPeriod endedId start (Some(s.today.PlusDays(-5)))
                let! activeId, _ =
                    s.agreementWith "CF-7.2 active control" Outgo Cadence.Daily s.today start None noInvoiceLeg
                let! _ = s.sweep 10
                let! endedDates = s.instanceDatesOf endedId
                let! activeDates = s.instanceDatesOf activeId
                Assert.Empty(endedDates)
                Assert.NotEmpty(activeDates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement whose end date is today and whose next-instance date is today gets an Instance dated today`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let! agreementId, _ =
                    s.agreementWith "CF-7.2 ends today" Outgo Cadence.Daily s.today (s.today.PlusDays(-60))
                        (Some s.today) noInvoiceLeg
                let! _ = s.sweep 10
                let! dates = s.instanceDatesOf agreementId
                Assert.Contains(s.today, dates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement whose start date is today and whose next-instance date is today gets an Instance dated today`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let! agreementId, _ =
                    s.agreementWith "CF-7.2 starts today" Outgo Cadence.Daily s.today s.today None noInvoiceLeg
                let! _ = s.sweep 10
                let! dates = s.instanceDatesOf agreementId
                Assert.Contains(s.today, dates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.2 an agreement with no end date whose start date has passed gets Instances through the horizon end`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let! agreementId, _ =
                    s.agreementWith "CF-7.2 open ended" Outgo Cadence.Daily s.today (s.today.PlusDays(-60)) None
                        noInvoiceLeg
                let! _ = s.sweep 10
                let! dates = s.instanceDatesOf agreementId
                Assert.Equal<LocalDate list>(datesFrom s.today (s.today.PlusDays(10)) (fun _ -> true), dates)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-7.3, 7.4, 7.6 — which dates the sweep fills and where it leaves the next-instance date
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.3 REQ-CF-7.6 an agreement whose next-instance date is in the past gets an Instance on exactly the cadence dates from that date through the horizon end, missed ones included, and none before it even when on or after its start date`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let nextInstance = s.today.PlusDays(-21)
                let weekDay = nextInstance.DayOfWeek
                let! agreementId, _ =
                    s.agreementWith "CF-7.3 catch up" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek weekDay))
                        nextInstance (s.today.PlusDays(-60)) None noInvoiceLeg
                let! _ = s.sweep 20
                let! dates = s.instanceDatesOf agreementId
                let expected = datesFrom nextInstance (s.today.PlusDays(20)) (fun d -> d.DayOfWeek = weekDay)
                Assert.Equal<LocalDate list>(expected, dates)
                Assert.DoesNotContain(nextInstance.PlusDays(-7), dates)
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("Weekly")>]
    [<InlineData("MonthlyDateInMonth")>]
    [<InlineData("MonthlyNthWeekDay")>]
    [<InlineData("MonthlyLast")>]
    [<InlineData("Annually")>]
    member _.``REQ-CF-7.3 for each of Weekly, Monthly date-in-month, Monthly nth-weekday, Monthly Last and Annually, the sweep creates an Instance on exactly the dates the rule yields from the next-instance date through the horizon end`` (cadence: string) =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let cadenceType, fits = cadenceCase cadence s.today
                (* The first date the rule yields on or after 40 days ago, so the sweep has missed dates to fill too. *)
                let nextInstance = datesFrom (s.today.PlusDays(-40)) (s.today.PlusDays(400)) fits |> List.head
                let horizon = if cadence = "Annually" then 365 else 100
                let! agreementId, _ =
                    s.agreementWith $"CF-7.3 {cadence}" Outgo cadenceType nextInstance (nextInstance.PlusDays(-30)) None
                        noInvoiceLeg
                let! _ = s.sweep horizon
                let! dates = s.instanceDatesOf agreementId
                Assert.Equal<LocalDate list>(datesFrom nextInstance (s.today.PlusDays(horizon)) fits, dates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.4 an EveryOtherWeek agreement whose start date is on the off week of its next-instance date gets Instances every 14 days from the next-instance date through the horizon end, and none on the weeks between`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let nextInstance = s.today.PlusDays(-7)
                let weekDay = nextInstance.DayOfWeek
                let! agreementId, _ =
                    s.agreementWith "CF-7.4 fortnightly" Outgo
                        (Cadence.EveryOtherWeek(Cadence.WeekDay.fromIsoDayOfWeek weekDay)) nextInstance
                        (nextInstance.PlusDays(-7)) None noInvoiceLeg
                let! _ = s.sweep 60
                let! dates = s.instanceDatesOf agreementId
                let expected =
                    datesFrom nextInstance (s.today.PlusDays(60)) (fun d -> daysBetween nextInstance d % 14 = 0)
                Assert.Equal<LocalDate list>(expected, dates)
                Assert.DoesNotContain(nextInstance.PlusDays(7), dates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.6 REQ-CF-4.8 after the sweep, a Weekly agreement's next-instance date is its first cadence date after the horizon end, not the day after the horizon end`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let nextInstance = s.today.PlusDays(2)
                let! agreementId, _ =
                    s.agreementWith "CF-7.6 next instance" Outgo
                        (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek nextInstance.DayOfWeek)) nextInstance
                        (s.today.PlusDays(-30)) None noInvoiceLeg
                (* The horizon ends two days after the second cadence date, so the day after it is no cadence date. *)
                let! _ = s.sweep 11
                let! next = s.nextInstanceOf agreementId
                Assert.Equal(nextInstance.PlusDays(14), next)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-7.7, 7.12 — sweeping again
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.7 REQ-CF-7.12 a second sweep with the same horizon on the same day creates no Instance or Invoice and leaves every Instance, Invoice, Payment and next-instance date exactly as the first left it`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let! agreementId, _ =
                    s.agreementWith "CF-7.7 again" Outgo Cadence.Daily s.today (s.today.PlusDays(-30)) None
                        [ (Some 100.00M, Some 5) ]
                let! _ = s.sweep 20
                let! swept = s.instanceDatesOf agreementId
                Assert.NotEmpty(swept)
                let! afterFirst = everything context
                let! _ = s.sweep 20
                let! afterSecond = everything context
                Assert.Equal(afterFirst, afterSecond)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.7 a sweep with a longer horizon after a shorter one on the same day creates exactly the cadence dates after the shorter horizon end through the longer one`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let nextInstance = s.today.PlusDays(1)
                let weekDay = nextInstance.DayOfWeek
                let! agreementId, _ =
                    s.agreementWith "CF-7.7 longer" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek weekDay))
                        nextInstance (s.today.PlusDays(-30)) None noInvoiceLeg
                let! _ = s.sweep 10
                let! shorter = s.instanceDatesOf agreementId
                let! _ = s.sweep 40
                let! longer = s.instanceDatesOf agreementId
                let added = longer |> List.except shorter
                let expected = datesFrom (s.today.PlusDays(11)) (s.today.PlusDays(40)) (fun d -> d.DayOfWeek = weekDay)
                Assert.Equal<LocalDate list>(expected, added)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-7.8, 7.9, 7.10 — the Invoices the sweep creates
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.8 for each Instance the sweep creates, each leg with an expected amount and a days-due value gets one Invoice for the expected amount, dated the Instance date and due that many days later`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let weekDay = s.today.DayOfWeek
                let! agreementId, legIds =
                    s.agreementWith "CF-7.8 two legs" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek weekDay))
                        s.today (s.today.PlusDays(-30)) None [ (Some 100.00M, Some 5); (Some 250.00M, Some 12) ]
                let legA, legB = legIds[0], legIds[1]
                let! _ = s.sweep 20
                let! rows = s.invoiceRowsOf agreementId
                let actual = rows |> List.map (fun (instanceDate, leg, amount, invoiceDate, dueDate, _) ->
                    instanceDate, leg, amount, invoiceDate, dueDate)
                let expected =
                    datesFrom s.today (s.today.PlusDays(20)) (fun d -> d.DayOfWeek = weekDay)
                    |> List.collect (fun d ->
                        [ (d, legA, 100.00M, d, d.PlusDays(5)); (d, legB, 250.00M, d, d.PlusDays(12)) ])
                Assert.Equal<(LocalDate * PaymentAgreementId * decimal * LocalDate * LocalDate) list>(expected, actual)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.8 an Instance that existed before the sweep gets no Invoice from it, while an Instance the same run creates for the same agreement gets one for each leg with an expected amount and a days-due value`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let weekDay = s.today.DayOfWeek
                let! agreementId, legIds =
                    s.agreementWith "CF-7.8 existing" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek weekDay))
                        s.today (s.today.PlusDays(-30)) None [ (Some 100.00M, Some 5) ]
                (* Made before the sweep with no Invoice; it moves the next-instance date on a week. *)
                let! _ = s.instance agreementId None s.today 0M
                let! _ = s.sweep 20
                let! rows = s.invoiceRowsOf agreementId
                let actual = rows |> List.map (fun (instanceDate, leg, _, _, _, _) -> instanceDate, leg)
                let expected =
                    datesFrom (s.today.PlusDays(1)) (s.today.PlusDays(20)) (fun d -> d.DayOfWeek = weekDay)
                    |> List.map (fun d -> d, legIds[0])
                Assert.Equal<(LocalDate * PaymentAgreementId) list>(expected, actual)
                Assert.DoesNotContain(s.today, actual |> List.map fst)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.9 a leg with no expected amount gets no Invoice on an Instance the sweep creates, while a sibling leg with both values does`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let weekDay = s.today.DayOfWeek
                let! agreementId, legIds =
                    s.agreementWith "CF-7.9 no amount" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek weekDay))
                        s.today (s.today.PlusDays(-30)) None [ (None, Some 5); (Some 100.00M, Some 5) ]
                let! _ = s.sweep 20
                let! rows = s.invoiceRowsOf agreementId
                let actual = rows |> List.map (fun (instanceDate, leg, _, _, _, _) -> instanceDate, leg)
                let expected =
                    datesFrom s.today (s.today.PlusDays(20)) (fun d -> d.DayOfWeek = weekDay)
                    |> List.map (fun d -> d, legIds[1])
                Assert.Equal<(LocalDate * PaymentAgreementId) list>(expected, actual)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.9 a leg with no days-due value gets no Invoice on an Instance the sweep creates, while a sibling leg with both values does`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let weekDay = s.today.DayOfWeek
                let! agreementId, legIds =
                    s.agreementWith "CF-7.9 no days due" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek weekDay))
                        s.today (s.today.PlusDays(-30)) None [ (Some 100.00M, None); (Some 100.00M, Some 5) ]
                let! _ = s.sweep 20
                let! rows = s.invoiceRowsOf agreementId
                let actual = rows |> List.map (fun (instanceDate, leg, _, _, _, _) -> instanceDate, leg)
                let expected =
                    datesFrom s.today (s.today.PlusDays(20)) (fun d -> d.DayOfWeek = weekDay)
                    |> List.map (fun d -> d, legIds[1])
                Assert.Equal<(LocalDate * PaymentAgreementId) list>(expected, actual)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.10 in one sweep, every Invoice created for an Income agreement is InvoiceGenerated and every one for an Outgo agreement is InvoiceExpected`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let weekly = Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek s.today.DayOfWeek)
                let! incomeId, _ =
                    s.agreementWith "CF-7.10 income" Income weekly s.today (s.today.PlusDays(-30)) None
                        [ (Some 100.00M, Some 5) ]
                let! outgoId, _ =
                    s.agreementWith "CF-7.10 outgo" Outgo weekly s.today (s.today.PlusDays(-30)) None
                        [ (Some 100.00M, Some 5) ]
                let! _ = s.sweep 20
                let statesOf rows = rows |> List.map (fun (_, _, _, _, _, state) -> state) |> List.distinct
                let! incomeRows = s.invoiceRowsOf incomeId
                let! outgoRows = s.invoiceRowsOf outgoId
                Assert.Equal<InvoiceState list>([ InvoiceGenerated ], incomeRows |> statesOf)
                Assert.Equal<InvoiceState list>([ InvoiceExpected ], outgoRows |> statesOf)
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-7.14, 7.15, 7.16 — what the sweep returns, failure, and the end date
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-7.14 the sweep returns every Instance that is unfulfilled after it runs, with its Invoices and their Payments, including ones it did not create, and no fulfilled Instance`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let first = s.today.PlusDays(-14)
                let! agreementId, legIds =
                    s.agreementWith "CF-7.14 returned" Outgo (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek first.DayOfWeek))
                        first (s.today.PlusDays(-60)) None [ (Some 100.00M, Some 30) ]
                (* Made before the sweep: one paid in full, one paid 40.00 of 100.00. *)
                let! fulfilledId, _, _ = s.instance agreementId (Some legIds[0]) first 100.00M
                let! unfulfilledId, unfulfilledInvoices, unfulfilledPayments =
                    s.instance agreementId (Some legIds[0]) (first.PlusDays(7)) 40.00M
                let! returned = s.sweep 20
                let! instances = s.instancesOf agreementId
                let returnedById =
                    returned
                    |> List.map (fun composite -> (composite |> InstanceOrchestration.instance |> Instance.instanceId), composite)
                    |> Map.ofList
                let returnedIds = returnedById |> Map.keys |> Set.ofSeq
                let createdIds =
                    instances
                    |> List.map Instance.instanceId
                    |> List.filter (fun id -> id <> fulfilledId && id <> unfulfilledId)
                    |> Set.ofList
                Assert.True(instances |> List.exists (fun i -> i |> Instance.instanceId = fulfilledId && i |> Instance.isFulfilled))
                Assert.NotEmpty(createdIds)
                Assert.Empty(Set.difference createdIds returnedIds)
                Assert.Contains(unfulfilledId, returnedIds)
                let unfulfilledComposites = returnedById[unfulfilledId] |> InstanceOrchestration.invoiceComposites
                Assert.Equal<InvoiceId list>(
                    unfulfilledInvoices,
                    unfulfilledComposites |> List.map (InstanceOrchestration.invoice >> Invoice.invoiceId))
                Assert.Equal<PaymentId list>(
                    unfulfilledPayments,
                    unfulfilledComposites |> List.collect InstanceOrchestration.payments |> List.map Payment.paymentId)
                Assert.DoesNotContain(fulfilledId, returnedIds)
                Assert.DoesNotContain(true, returned |> List.map (InstanceOrchestration.instance >> Instance.isFulfilled))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-CF-7.15 when an Instance fails to be created after other agreements' Instances and Invoices were already created in the run, none of them remains and no next-instance date has moved`` () =
        (* The failing agreement has an Instance a week after its next-instance date, then has that date put back, so
           the sweep's first Instance for it is not after its latest one. *)
        sweepFailureLeavesNothing "instance"
            (fun s ->
                result {
                    let weekly = Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek s.today.DayOfWeek)
                    let! failing, legIds =
                        s.agreementWith "CF-7.15 instance failing" Outgo weekly s.today (s.today.PlusDays(-30)) None
                            [ (Some 100.00M, Some 0) ]
                    let! _ = s.instance failing (Some legIds[0]) (s.today.PlusDays(7)) 0M
                    do! s.setNextInstance failing s.today
                    return failing
                })
            (function
             | AsError (CashFlowError.CashflowInstanceDateNotAfterLatestInstance _) -> true
             | _ -> false)

    [<Fact>]
    member _.``REQ-CF-7.15 when an Invoice fails to be created after other Instances and Invoices were already created in the run, none of them remains and no next-instance date has moved`` () =
        (* The failing agreement's leg has its expected amount set to zero at the model level, so the Invoice the
           sweep builds for it fails validation. *)
        sweepFailureLeavesNothing "invoice"
            (fun s ->
                result {
                    let! failing, legIds =
                        s.agreementWith "CF-7.15 invoice failing" Outgo Cadence.Daily s.today (s.today.PlusDays(-30))
                            None [ (Some 100.00M, Some 0) ]
                    do! s.setExpectedAmount legIds[0] 0.00M
                    return failing
                })
            (function
             | AsError (CashFlowError.CashflowInvoiceNonPositiveAmount _) -> true
             | _ -> false)

    [<Fact>]
    member _.``REQ-CF-7.16 an agreement whose end date falls inside the horizon, on a day that is not a cadence date, gets an Instance on its last cadence date before the end date and none after it`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let nextInstance = s.today.PlusDays(1)
                let endDate = nextInstance.PlusDays(10)
                let! agreementId, _ =
                    s.agreementWith "CF-7.16 ends inside" Outgo
                        (Cadence.Weekly(Cadence.WeekDay.fromIsoDayOfWeek nextInstance.DayOfWeek)) nextInstance
                        (s.today.PlusDays(-30)) (Some endDate) noInvoiceLeg
                let! _ = s.sweep 40
                let! dates = s.instanceDatesOf agreementId
                Assert.Contains(nextInstance.PlusDays(7), dates)
                Assert.Empty(dates |> List.filter (fun d -> d > endDate))
            })
        |> railroadWrapper
