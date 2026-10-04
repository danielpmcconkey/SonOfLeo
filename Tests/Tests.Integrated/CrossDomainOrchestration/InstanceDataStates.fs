module Tests.Integrated.CrossDomainOrchestration.InstanceDataStates

open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Json
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
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* "Nothing is stored" means no Instance and an unchanged next-instance date.
   Every test builds its own Outgo agreements (debit F-2230, credit F-1280) with 100.00 legs, and its own Instances,
   through the orchestration, in a transaction that rolls back. Dates are in March 2049, where the 1st, 8th and 15th are
   Mondays and the 31st is the last day, so no sweep a test runs reaches them. The CreateInstance route commits, so the
   one test that uses it reads back from a fresh context and deletes what it made in a finally. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private march (day: int) = LocalDate(2049, 3, day)

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId
    let loanId = accountIdOf "F-2230"
    let cash = accountIdOf "F-1280"

    member _.Context = context
    member _.today = Calendar.today ()

    /// An agreement with the cadence and next-instance date and the number of 100.00 legs. Returns its id and leg ids.
    member _.agreement (label: string) (cadence: Cadence.CadenceType) (nextInstance: LocalDate) (legCount: int) =
        result {
            let name = $"{label} {Guid.NewGuid():N}"
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Instance data state test counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create ((Calendar.today ()).PlusYears(-1)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let legNames = List.init legCount (fun i -> $"{name} leg {i + 1}")
            let! legs =
                legNames
                |> List.map (fun legName ->
                    result {
                        let! paName = legName |> PaymentAgreementName.create
                        let! expected = Money.fromDecimal 100.00M
                        let! due = 0 |> DaysDueAfterInvoiceDate.create
                        return (paName, (DebitAccount.create loanId), (CreditAccount.create cash), Some expected, Some due, None)
                    })
                |> convertListOfResultsToResultsList
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo cadence { nextInstance = nextInstance } counterparty activityPeriod None legs
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legIds =
                legNames
                |> List.map (fun legName ->
                    agreement
                    |> AgreementOrchestration.paymentAgreements
                    |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                    |> PaymentAgreement.paymentAgreementId)
            return agreementId, legIds
        }

    /// A 100.00 Payment pointing at the F-2230 line of a new journal entry on the date, which needs a fiscal period.
    member _.postedPayment (date: LocalDate) =
        result {
            let! entry, _ =
                createTestJournalEntryFromPrimitives
                    context $"Instance data state test payment {Guid.NewGuid()}" None date
                    [ (loanId, 100.00M, "Debit", None); (cash, 100.00M, "Credit", None) ] [] []
            let line =
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = loanId)
                |> JournalEntryLine.journalEntryLineId
            let! money = Money.fromDecimal 100.00M
            return (Posted(line, None), None, None, None)
        }

    /// A 100.00 Invoice for the leg, dated the Instance's date, with the Payments.
    member _.invoiceFields (date: LocalDate) (legId: PaymentAgreementId) payments =
        result {
            let! amount = Money.fromDecimal 100.00M
            return
                (legId, None, (InvoiceDate.create date), (DueDate.create (date.PlusDays(30))),
                 (InvoiceAmount.create amount), InvoiceReceived, None, None, payments)
        }

    /// Creates an Instance on the date with a 100.00 Invoice per leg given, none paid.
    member this.instance (agreementId: MasterAgreementId) (date: LocalDate) (legIds: PaymentAgreementId list) =
        result {
            let! invoices = legIds |> List.map (fun legId -> this.invoiceFields date legId []) |> convertListOfResultsToResultsList
            return! InstanceOrchestration.constructNewAndPersist context agreementId date invoices
        }

    member _.instancesOf (agreementId: MasterAgreementId) = [ agreementId ] |> Instance.fetchByMasterAgreementIdList context

    member this.datesOf (agreementId: MasterAgreementId) =
        this.instancesOf agreementId |> Result.map (List.map Instance.instanceDate >> List.sort)

    member _.isFulfilled (instanceId: InstanceId) = instanceId |> Instance.fetchById context |> Result.map Instance.isFulfilled

    /// The leg ids of the Instance's stored Invoices, sorted.
    member _.invoiceLegsOf (instanceId: InstanceId) =
        instanceId
        |> InstanceOrchestration.fetchCompositeByInstanceId context
        |> Result.map (InstanceOrchestration.invoiceComposites
                       >> List.map (InstanceOrchestration.invoice >> Invoice.paymentAgreementId)
                       >> List.sortBy PaymentAgreementId.value)

    member _.nextInstanceOf (agreementId: MasterAgreementId) =
        agreementId |> MasterAgreement.fetchById context |> Result.map (fun master -> (master |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)

    member _.setNextInstance (agreementId: MasterAgreementId) (date: LocalDate) =
        result {
            let! master = agreementId |> MasterAgreement.fetchById context
            let! cadence = Cadence.create (master |> MasterAgreement.cadence |> Cadence.cadenceType) { nextInstance = date }
            let! _ = master |> MasterAgreement.updateCadence context cadence
            return ()
        }

    member _.sweep (days: int) =
        result {
            let! horizon = days |> ProjectionHorizonInDays.create
            return! horizon |> CashFlowOps.createUpcomingInstances context
        }

/// An update to the Instance that changes only what the caller says.
let private instanceUpdate (instanceId: InstanceId) : InstanceOrchestration.InstanceCompositeUpdate =
    { instanceUpdates = { instanceIdToUpdate = instanceId; isFulfilledUpdate = NoChange }
      invoiceCompositeUpdates = []
      newInvoices = [] }

let private idOf (composite: InstanceOrchestration.InstanceComposite) =
    composite |> InstanceOrchestration.instance |> Instance.instanceId

let private weekDay (name: string) =
    match name |> Cadence.WeekDay.fromString with
    | Ok d -> d
    | Error e -> failwith (e.ToMessage())

/// For each cadence rule: the rule, a date that fits it, and one that does not.
let private cadenceCase (cadence: string) : Cadence.CadenceType * LocalDate * LocalDate =
    let dayNumber n =
        match n |> Cadence.DateInMonthNumber.fromInt with
        | Ok d -> d
        | Error e -> failwith (e.ToMessage())
    let weekNumber n =
        match n |> Cadence.WeekInMonthNumber.fromInt with
        | Ok w -> w
        | Error e -> failwith (e.ToMessage())
    match cadence with
    | "Weekly" -> Cadence.Weekly(weekDay "Monday"), march 1, march 2
    | "EveryOtherWeek" -> Cadence.EveryOtherWeek(weekDay "Monday"), march 1, march 2
    | "MonthlyDateInMonth" -> Cadence.Monthly(Cadence.DateInMonth(dayNumber 1)), march 1, march 2
    | "MonthlyNthWeekDay" -> Cadence.Monthly(Cadence.NthWeekDay(weekNumber 1, weekDay "Monday")), march 1, march 8
    | "MonthlyLast" -> Cadence.Monthly Cadence.Last, march 31, march 30
    | "Annually" -> Cadence.Annually(Cadence.March, Cadence.DateInMonth(dayNumber 1)), march 1, march 2
    | other -> failwith $"no case for {other}"

/// The typed error each rule should give for its unfitting date.
let private namesDateAndRule (cadence: string) (date: LocalDate) (e: IAppError) =
    match cadence, e with
    | ("Weekly" | "EveryOtherWeek"), AsError (BizGeneralError.CadenceDateNotOnWeekDay(d, wd)) -> d = date && wd = "Monday"
    | "MonthlyDateInMonth", AsError (BizGeneralError.CadenceDateNotOnDateInMonth(d, n)) -> d = date && n = 1
    | "MonthlyNthWeekDay", AsError (BizGeneralError.CadenceDateNotNthWeekDayInMonth(d, n, wd)) -> d = date && n = 1 && wd = "Monday"
    | "MonthlyLast", AsError (BizGeneralError.CadenceDateNotLastDayOfMonth d) -> d = date
    | "Annually", AsError (BizGeneralError.CadenceDateNotOnAnnualDate(d, day, month)) -> d = date && day = "day 1" && month = "March"
    | _ -> false

let private weekly () = Cadence.Weekly(weekDay "Monday")

[<Collection("SharedTestData")>]
type InstanceDataStatesTests(fixture: TestDataFixture) =

    let rolledBack (body: Scenario -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context -> body (Scenario(fixture, context)))
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-4.3 — the agreement an Instance references
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-4.3 creating an Instance for a Master Agreement ID that names no stored agreement is rejected with a typed error and nothing is stored`` () =
        rolledBack (fun s ->
            result {
                let missing = MasterAgreementId.create ()
                let attempt = InstanceOrchestration.constructNewAndPersist s.Context missing (march 1) []
                let! stored = s.instancesOf missing
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-4.3 with two agreements stored, an Instance created for one of them is stored referencing that agreement's ID and not the other's`` () =
        rolledBack (fun s ->
            result {
                let! chosenId, _ = s.agreement "CF-4.3 chosen" (weekly ()) (march 1) 1
                let! otherId, _ = s.agreement "CF-4.3 other" (weekly ()) (march 1) 1
                let! created = s.instance chosenId (march 1) []
                let! stored = idOf created |> Instance.fetchById s.Context
                let! otherInstances = s.instancesOf otherId
                Assert.Equal(chosenId, stored |> Instance.masterAgreementID)
                Assert.Empty(otherInstances)
            })

    // =========================================================================
    // REQ-CF-4.5 — is-fulfilled defaults to false
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-4.5 an Instance created by CreateInstance or by the sweep, with no is-fulfilled value supplied, is stored with is-fulfilled false`` () =
        let mutable agreementId: Guid option = None
        let cleanUpFailures = ResizeArray<string>()
        try
            result {
                let! made =
                    runCommandRouteAndAutoCompleteTransaction CashFlowCreateInstance (fun context ->
                        let s = Scenario(fixture, context)
                        s.agreement "CF-4.5 route" (weekly ()) (march 1) 1
                        |> Result.map (fun (id, _) -> agreementId <- Some(id |> MasterAgreementId.value); id))
                let! master = made |> MasterAgreement.fetchById (fresh ())
                let! legs = [ made ] |> PaymentAgreement.fetchByMasterAgreementIdList (fresh ())
                let invoice : Contracts.NewInvoiceFieldsInput =
                    { paymentAgreementName = legs |> List.exactlyOne |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value
                      externalInvoiceId = None
                      invoiceDate = march 1
                      dueDate = march 31
                      amount = 100.00M
                      invoiceState = "InvoiceReceived"
                      blocker = None
                      memo = None
                      payments = [] }
                let input : Contracts.CreateInstanceInput =
                    { masterAgreementName = master |> MasterAgreement.agreementName |> AgreementName.value
                      instanceDate = march 1
                      invoices = [ invoice ] }
                let! json = Json.toJson input
                let! returned = routeUiCommandForTesting "CashFlow" "CreateInstance" [] json
                let! composite = Json.fromJson<Contracts.InstanceCompositeReturn> returned
                let! routeStored = composite.instance.instanceId |> InstanceId.fromGuid |> Instance.fetchById (fresh ())
                Assert.False(routeStored |> Instance.isFulfilled)
                (* The sweep, in a transaction of its own that rolls back: a Daily agreement due today gets an Instance
                   today and tomorrow, each with an unpaid Invoice. *)
                do!
                    runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
                        result {
                            let s = Scenario(fixture, context)
                            let! sweptId, _ = s.agreement "CF-4.5 sweep" Cadence.Daily s.today 1
                            let! _ = s.sweep 1
                            let! swept = s.instancesOf sweptId
                            Assert.Equal(2, swept.Length)
                            Assert.All(swept, fun i -> Assert.False(i |> Instance.isFulfilled))
                        })
            }
            |> railroadWrapper
        finally
            match Cleanup.cleanUpMasterAgreementTree agreementId with
            | Ok () -> ()
            | Error e -> cleanUpFailures.Add(e.ToMessage())
        Assert.Empty(cleanUpFailures)

    // =========================================================================
    // REQ-CF-4.6 — the date fits the cadence
    // =========================================================================

    [<Theory>]
    [<InlineData("Weekly")>]
    [<InlineData("EveryOtherWeek")>]
    [<InlineData("MonthlyDateInMonth")>]
    [<InlineData("MonthlyNthWeekDay")>]
    [<InlineData("MonthlyLast")>]
    [<InlineData("Annually")>]
    member _.``REQ-CF-4.6 for every cadence rule but Daily, creating an Instance on a date that does not fit its agreement's cadence is rejected with a typed error naming the date and the rule, and nothing is stored`` (cadence:string) =
        rolledBack (fun s ->
            result {
                let rule, fitting, unfitting = cadenceCase cadence
                let! agreementId, _ = s.agreement $"CF-4.6 {cadence}" rule fitting 1
                let attempt = s.instance agreementId unfitting []
                let! stored = s.instancesOf agreementId
                let! next = s.nextInstanceOf agreementId
                let namesIt =
                    match attempt with
                    | Error e -> namesDateAndRule cadence unfitting e
                    | Ok _ -> false
                Assert.True(namesIt)
                Assert.Empty(stored)
                Assert.Equal(fitting, next)
            })

    [<Fact>]
    member _.``REQ-CF-4.6 creating an Instance on a date that fits its agreement's cadence but is later than its next-instance date is stored with that date`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, _ = s.agreement "CF-4.6 later" (weekly ()) (march 1) 1
                let! _ = s.instance agreementId (march 15) []
                let! dates = s.datesOf agreementId
                Assert.Equal<LocalDate list>([ march 15 ], dates)
            })

    // =========================================================================
    // REQ-CF-4.7 — forward only
    // =========================================================================

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-1)>]
    member _.``REQ-CF-4.7 for each of the latest existing Instance's date and a cadence date before it, creating an Instance on that date is rejected with a typed error naming the latest existing date, and nothing is stored`` (offsetWeeks:int) =
        rolledBack (fun s ->
            result {
                let! agreementId, _ = s.agreement "CF-4.7 backward" (weekly ()) (march 1) 1
                let! _ = s.instance agreementId (march 15) []
                let attempted = (march 15).PlusWeeks(offsetWeeks)
                let attempt = s.instance agreementId attempted []
                let! dates = s.datesOf agreementId
                let! next = s.nextInstanceOf agreementId
                let namesLatest =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowInstanceDateNotAfterLatestInstance(_, _, latest))) -> latest = march 15
                    | _ -> false
                Assert.True(namesLatest)
                Assert.Equal<LocalDate list>([ march 15 ], dates)
                Assert.Equal(march 22, next)
            })

    [<Fact>]
    member _.``REQ-CF-4.7 creating an Instance on the first cadence date after the latest existing Instance of the same agreement is stored with that date`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, _ = s.agreement "CF-4.7 next" (weekly ()) (march 1) 1
                let! _ = s.instance agreementId (march 15) []
                let! _ = s.instance agreementId (march 22) []
                let! dates = s.datesOf agreementId
                Assert.Equal<LocalDate list>([ march 15; march 22 ], dates)
            })

    [<Fact>]
    member _.``REQ-CF-4.7 creating an Instance on a date earlier than the latest Instance of a different agreement, but later than every Instance of its own, is stored`` () =
        rolledBack (fun s ->
            result {
                let! otherId, _ = s.agreement "CF-4.7 other" (weekly ()) (march 1) 1
                let! ownId, _ = s.agreement "CF-4.7 own" (weekly ()) (march 1) 1
                let! _ = s.instance otherId (march 29) []
                let! _ = s.instance ownId (march 1) []
                let! _ = s.instance ownId (march 15) []
                let! dates = s.datesOf ownId
                Assert.Equal<LocalDate list>([ march 1; march 15 ], dates)
            })

    (* The next-instance date is put back behind the latest Instance, which no route allows, so the sweep enumerates
       dates on and before it. Whether the sweep then skips them or fails as a whole, none may be created. *)
    [<Fact>]
    member _.``REQ-CF-4.7 the sweep creates no Instance on a date earlier than or equal to the latest existing Instance of the agreement`` () =
        runCommandRouteAndAutoRollback CashFlowCreateUpcomingInstances (fun context ->
            result {
                let s = Scenario(fixture, context)
                let latest = s.today.PlusDays(-3)
                let! agreementId, _ = s.agreement "CF-4.7 sweep" Cadence.Daily (s.today.PlusDays(-10)) 1
                let! _ = s.instance agreementId latest []
                do! s.setNextInstance agreementId (s.today.PlusDays(-10))
                let _ = s.sweep 5
                let! dates = s.datesOf agreementId
                Assert.Equal<LocalDate list>([ latest ], dates |> List.filter (fun d -> d <= latest))
            })
        |> railroadWrapper

    // =========================================================================
    // REQ-CF-4.9 — is-fulfilled needs every Invoice FullyPaid
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-4.9 an Instance composite update that sets is-fulfilled to true on an Instance with no Invoices is rejected with a typed error and the stored flag stays false`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, _ = s.agreement "CF-4.9 no invoices" (weekly ()) (march 1) 1
                let! created = s.instance agreementId (march 1) []
                let update = instanceUpdate (idOf created)
                let attempt =
                    { update with instanceUpdates = { update.instanceUpdates with isFulfilledUpdate = SetTo true } }
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let! flag = s.isFulfilled (idOf created)
                Assert.True(attempt |> Result.isError)
                Assert.False(flag)
            })

    [<Fact>]
    member _.``REQ-CF-4.9 an Instance composite update that sets is-fulfilled to true on an Instance with at least one Invoice not FullyPaid is rejected with a typed error and the stored flag stays false`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds = s.agreement "CF-4.9 unpaid" (weekly ()) (march 1) 2
                let! paid = s.postedPayment s.today
                let! paidInvoice = s.invoiceFields (march 1) legIds[0] [ paid ]
                let! unpaidInvoice = s.invoiceFields (march 1) legIds[1] []
                let! created =
                    InstanceOrchestration.constructNewAndPersist s.Context agreementId (march 1)
                        [ paidInvoice; unpaidInvoice ]
                let update = instanceUpdate (idOf created)
                let attempt =
                    { update with instanceUpdates = { update.instanceUpdates with isFulfilledUpdate = SetTo true } }
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let! flag = s.isFulfilled (idOf created)
                Assert.True(attempt |> Result.isError)
                Assert.False(flag)
            })

    [<Fact>]
    member _.``REQ-CF-4.9 REQ-CF-9.10 an Instance composite update that sets is-fulfilled to false on an Instance whose every Invoice is FullyPaid is rejected with a typed error and the stored flag stays true`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds = s.agreement "CF-4.9 paid" (weekly ()) (march 1) 1
                let! paid = s.postedPayment s.today
                let! paidInvoice = s.invoiceFields (march 1) legIds[0] [ paid ]
                let! created =
                    InstanceOrchestration.constructNewAndPersist s.Context agreementId (march 1) [ paidInvoice ]
                let update = instanceUpdate (idOf created)
                let attempt =
                    { update with instanceUpdates = { update.instanceUpdates with isFulfilledUpdate = SetTo false } }
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let! flag = s.isFulfilled (idOf created)
                Assert.True(attempt |> Result.isError)
                Assert.True(flag)
            })

    // =========================================================================
    // REQ-CF-4.10 — one Invoice per leg, and only its own agreement's legs
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-4.10 creating an Instance with two Invoices for the same Payment Agreement is rejected with a typed error and nothing is stored`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds = s.agreement "CF-4.10 twice" (weekly ()) (march 1) 1
                let attempt = s.instance agreementId (march 1) [ legIds[0]; legIds[0] ]
                let! stored = s.instancesOf agreementId
                let! next = s.nextInstanceOf agreementId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
                Assert.Equal(march 1, next)
            })

    [<Fact>]
    member _.``REQ-CF-4.10 adding an Invoice for a Payment Agreement that already has an Invoice on the Instance is rejected with a typed error and the Instance's Invoices are unchanged`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds = s.agreement "CF-4.10 add twice" (weekly ()) (march 1) 1
                let! created = s.instance agreementId (march 1) [ legIds[0] ]
                let! again = s.invoiceFields (march 1) legIds[0] []
                let attempt =
                    { instanceUpdate (idOf created) with newInvoices = [ again ] }
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let! legs = s.invoiceLegsOf (idOf created)
                Assert.True(attempt |> Result.isError)
                Assert.Equal<PaymentAgreementId list>([ legIds[0] ], legs)
            })

    [<Fact>]
    member _.``REQ-CF-4.10 creating an Instance with an Invoice for a Payment Agreement of a different agreement is rejected with a typed error and nothing is stored`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, _ = s.agreement "CF-4.10 own" (weekly ()) (march 1) 1
                let! _, otherLegs = s.agreement "CF-4.10 foreign" (weekly ()) (march 1) 1
                let attempt = s.instance agreementId (march 1) [ otherLegs[0] ]
                let! stored = s.instancesOf agreementId
                let! next = s.nextInstanceOf agreementId
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
                Assert.Equal(march 1, next)
            })

    [<Fact>]
    member _.``REQ-CF-4.10 adding an Invoice for a Payment Agreement of a different agreement to an existing Instance is rejected with a typed error and the Instance's Invoices are unchanged`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds = s.agreement "CF-4.10 own add" (weekly ()) (march 1) 1
                let! _, otherLegs = s.agreement "CF-4.10 foreign add" (weekly ()) (march 1) 1
                let! created = s.instance agreementId (march 1) [ legIds[0] ]
                let! foreign = s.invoiceFields (march 1) otherLegs[0] []
                let attempt =
                    { instanceUpdate (idOf created) with newInvoices = [ foreign ] }
                    |> InstanceOrchestration.updateInstanceComposite s.Context
                let! legs = s.invoiceLegsOf (idOf created)
                Assert.True(attempt |> Result.isError)
                Assert.Equal<PaymentAgreementId list>([ legIds[0] ], legs)
            })

    [<Fact>]
    member _.``REQ-CF-4.10 an Instance holding one Invoice for each of several Payment Agreements of its own agreement is stored with every one of those Invoices`` () =
        rolledBack (fun s ->
            result {
                let! agreementId, legIds = s.agreement "CF-4.10 several" (weekly ()) (march 1) 3
                let! created = s.instance agreementId (march 1) legIds
                let! legs = s.invoiceLegsOf (idOf created)
                Assert.Equal<PaymentAgreementId list>(legIds |> List.sortBy PaymentAgreementId.value, legs)
            })
