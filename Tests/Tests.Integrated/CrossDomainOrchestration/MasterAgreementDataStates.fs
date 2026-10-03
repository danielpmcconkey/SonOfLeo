module Tests.Integrated.CrossDomainOrchestration.MasterAgreementDataStates

open System
open System.Text.Json.Nodes
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
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open App.DataAccessLayer.ExecuteReader
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Two kinds of test live here.

   The payload and update tests go through the CashFlow routes, the way the operator does. A route commits when it
   succeeds, and an update is written before the agreement is checked, so what an update leaves behind is only visible
   once its transaction is gone. These tests read back from a fresh context, and every agreement whose name they use is
   deleted in a finally, whether or not the test expected it to be stored.

   The rest build agreements in the orchestration layer inside a transaction that is rolled back.

   Every agreement has one Outgo leg on F-2230 and F-1280. Names carry a Guid so a leftover from a failed run never
   collides with the next one. *)

let private unique (label: string) = $"{label} {Guid.NewGuid():N}"

let private fresh () = Context.create NoTransaction FetchOnly

let private noFilter : AgreementFilter =
    { agreementIds = None
      agreementNames = None
      direction = None
      activeAgreementsOnly = false
      accountIds = None
      paymentAgreementExpectedAmount = None
      instanceTemporalFilter = None
      externalInvoiceId = None
      invoiceDateTemporalFilter = None
      invoiceDueTemporalFilter = None
      invoiceAmount = None
      invoiceState = None
      invoicePaymentState = None
      invoicePostedState = None
      invoiceBlocker = None
      journalEntryLineId = None
      stageEntryLineId = None
      paymentAmount = None
      paymentPostedToLedgerTemporalFilter = None }

/// Every stored Master Agreement for which `pick` holds.
let private storedWhere (context: Context.Context) (pick: MasterAgreement.MasterAgreement -> bool) =
    noFilter
    |> AgreementOrchestration.fetchFiltered context AnyQuantityIsAcceptable
    |> Result.map (List.map AgreementOrchestration.masterAgreement >> List.filter pick)

let private named (name: string) (m: MasterAgreement.MasterAgreement) =
    m |> MasterAgreement.agreementName |> AgreementName.value = name

let private storedNamed context name = storedWhere context (named name)

let private leg (name: string) : Contracts.CreatePaymentAgreementFieldsInput =
    { paymentAgreementName = $"{name} leg"
      debitAccountCode = "F-2230"
      creditAccountCode = "F-1280"
      expectedAmount = Some 100.00M
      daysDueAfterInvoiceDate = Some 0
      memo = None }

/// A CreateAgreement payload: Outgo, one leg, started 30 days ago, open ended.
let private createInput (name: string) (cadenceType: Contracts.CadenceTypeContract) (nextInstance: LocalDate) : Contracts.CreateAgreementInput =
    { agreementName = name
      direction = "Outgo"
      cadence = { cadenceType = cadenceType; nextInstance = nextInstance }
      counterparty = "Master agreement data state test counterparty"
      activeBegin = Calendar.today().PlusDays(-30)
      activeEnd = None
      memo = None
      paymentAgreements = [ leg name ] }

let private toNode (input: 'a) = input |> Json.toJson |> Result.map (fun json -> JsonNode.Parse(json))

let private createRoute (payload: string) = routeUiCommandForTesting "CashFlow" "CreateAgreement" [] payload

let private updateRoute (input: Contracts.UpdateAgreementInput) =
    input |> Json.toJson |> Result.bind (routeUiCommandForTesting "CashFlow" "UpdateAgreement" [])

let private noUpdate (name: string) : Contracts.UpdateAgreementInput =
    { agreementName = name
      agreementNameUpdate = NoChange
      directionUpdate = NoChange
      cadenceUpdate = NoChange
      counterpartyUpdate = NoChange
      activeBeginUpdate = NoChange
      activeEndUpdate = NoChange
      memoUpdate = NoChange
      paymentAgreementUpdates = []
      newPaymentAgreements = [] }

/// The next date on or after `from` that falls on the week day.
let private nextOn (day: IsoDayOfWeek) (from: LocalDate) =
    List.init 7 (fun i -> from.PlusDays(i)) |> List.find (fun d -> d.DayOfWeek = day)

/// Runs the test, then deletes every stored agreement carrying one of the names, from a fresh context.
let private cleaningUp (names: string list) (test: unit -> Result<unit, IAppError>) =
    let cleanUpFailures = ResizeArray<string>()
    try
        test () |> railroadWrapper
    finally
        for name in names do
            match storedNamed (fresh ()) name with
            | Ok stored ->
                for m in stored do
                    match Cleanup.cleanUpMasterAgreementTree (Some(m |> MasterAgreement.agreementID |> MasterAgreementId.value)) with
                    | Ok () -> ()
                    | Error e -> cleanUpFailures.Add(e.ToMessage())
            | Error e -> cleanUpFailures.Add(e.ToMessage())
    Assert.Empty(cleanUpFailures)

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    member _.Context = context
    member _.today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

    /// Creates an Outgo agreement with `legs` legs (each 100.00, due the same day). Returns the agreement.
    member _.create (name: string) (cadence: Cadence.CadenceType) (nextInstance: LocalDate) (start: LocalDate) (endDate: LocalDate option) (legs: int) =
        result {
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Master agreement data state test counterparty" |> Counterparty.create
            let! period = ActivityPeriod.create start endDate ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let! legComponents =
                List.init legs (fun i ->
                    result {
                        let! legName = $"{name} leg {i + 1}" |> PaymentAgreementName.create
                        let! expected = Money.fromDecimal 100.00M
                        let! due = 0 |> DaysDueAfterInvoiceDate.create
                        return (legName, DebitAccount(accountIdOf "F-2230"), CreditAccount(accountIdOf "F-1280"), Some expected, Some due, None)
                    })
                |> convertListOfResultsToResultsList
            return!
                AgreementOrchestration.constructNewAndPersist
                    context agreementName Outgo cadence { nextInstance = nextInstance } counterparty period None legComponents
        }

    member this.daily (name: string) (start: LocalDate) (endDate: LocalDate option) =
        this.create name Cadence.Daily this.today start endDate 1

    member _.stored (name: string) = storedNamed context name

[<Collection("SharedTestData")>]
type MasterAgreementDataStatesTests(fixture: TestDataFixture) =

    let rolledBack (body: Scenario -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback CashFlowCreateAgreement (fun context -> body (Scenario(fixture, context)))
        |> railroadWrapper

    let today () = Calendar.today()

    /// Commits a Daily agreement through the route and returns its stored Master Agreement.
    let createDaily (name: string) =
        result {
            let! json = createInput name Contracts.Daily (today ()) |> Json.toJson
            let! _ = createRoute json
            let! stored = storedNamed (fresh ()) name
            return stored |> List.exactlyOne
        }

    // =========================================================================
    // REQ-CF-2.3, 2.8, 2.17 — values a payload can't be allowed to carry
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.3 a CreateAgreement payload with a null agreement name is rejected with a typed error and no agreement is stored`` () =
        let counterparty = unique "CF-2.3 null name counterparty"
        cleaningUp [] (fun () ->
            result {
                let! node = { createInput "placeholder" Contracts.Daily (today ()) with counterparty = counterparty } |> toNode
                node["agreementName"] <- null
                let attempt = createRoute (node.ToJsonString())
                let! stored =
                    storedWhere (fresh ()) (fun m -> m |> MasterAgreement.counterparty |> Counterparty.value = counterparty)
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-2.17 a CreateAgreement payload with a null counterparty is rejected with a typed error and no agreement is stored`` () =
        let name = unique "CF-2.17 null counterparty"
        cleaningUp [ name ] (fun () ->
            result {
                let! node = createInput name Contracts.Daily (today ()) |> toNode
                node["counterparty"] <- null
                let attempt = createRoute (node.ToJsonString())
                let! stored = storedNamed (fresh ()) name
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-2.8 a CreateAgreement payload naming a cadence other than Daily, Weekly, EveryOtherWeek, Monthly and Annually is rejected with a typed error and no agreement is stored`` () =
        let name = unique "CF-2.8 fortnightly"
        cleaningUp [ name ] (fun () ->
            result {
                let! node = createInput name Contracts.Daily (today ()) |> toNode
                node["cadence"].["cadenceType"] <- JsonNode.Parse("""{"Case":"Fortnightly","Fields":["Monday"]}""")
                let attempt = createRoute (node.ToJsonString())
                let! stored = storedNamed (fresh ()) name
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
            })

    // =========================================================================
    // REQ-CF-2.4, 2.5, 2.18, 2.19 with REQ-SYS-1.1 — trimming at the boundary
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.4 REQ-SYS-1.1 an agreement name with leading and trailing spaces is accepted and stored trimmed`` () =
        let name = unique "CF-2.4 trimmed"
        cleaningUp [ name ] (fun () ->
            result {
                let! json = createInput $"  {name}\t " Contracts.Daily (today ()) |> Json.toJson
                let! _ = createRoute json
                let! stored = storedNamed (fresh ()) name
                Assert.Equal<string>(name, stored |> List.exactlyOne |> MasterAgreement.agreementName |> AgreementName.value)
            })

    [<Fact>]
    member _.``REQ-CF-2.5 REQ-SYS-1.1 an agreement name of 100 characters padded with spaces on both sides is accepted and stored as the 100 characters`` () =
        let name = (unique "CF-2.5 padded").PadRight(100, 'x')
        cleaningUp [ name ] (fun () ->
            result {
                let! json = createInput $"   {name}   " Contracts.Daily (today ()) |> Json.toJson
                let! _ = createRoute json
                let! stored = storedNamed (fresh ()) name
                Assert.Equal<string>(name, stored |> List.exactlyOne |> MasterAgreement.agreementName |> AgreementName.value)
            })

    [<Fact>]
    member _.``REQ-CF-2.18 REQ-SYS-1.1 a counterparty with leading and trailing spaces is accepted and stored trimmed`` () =
        let name = unique "CF-2.18 trimmed"
        cleaningUp [ name ] (fun () ->
            result {
                let! json =
                    { createInput name Contracts.Daily (today ()) with counterparty = " \t Trimmed counterparty  " }
                    |> Json.toJson
                let! _ = createRoute json
                let! stored = storedNamed (fresh ()) name
                Assert.Equal<string>("Trimmed counterparty", stored |> List.exactlyOne |> MasterAgreement.counterparty |> Counterparty.value)
            })

    [<Fact>]
    member _.``REQ-CF-2.19 REQ-SYS-1.1 a counterparty of 250 characters padded with spaces on both sides is accepted and stored as the 250 characters`` () =
        let name = unique "CF-2.19 padded"
        let counterparty = String('c', 250)
        cleaningUp [ name ] (fun () ->
            result {
                let! json =
                    { createInput name Contracts.Daily (today ()) with counterparty = $"   {counterparty}   " }
                    |> Json.toJson
                let! _ = createRoute json
                let! stored = storedNamed (fresh ()) name
                Assert.Equal<string>(counterparty, stored |> List.exactlyOne |> MasterAgreement.counterparty |> Counterparty.value)
            })

    // =========================================================================
    // REQ-CF-2.6 — unique names
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.6 creating an agreement with the name of an existing agreement is rejected with a typed error, no second agreement is stored and the existing one is unchanged`` () =
        let name = unique "CF-2.6 duplicate"
        cleaningUp [ name ] (fun () ->
            result {
                let! existing = createDaily name
                let! json =
                    { createInput name Contracts.Daily (today ()) with counterparty = "A different counterparty" }
                    |> Json.toJson
                let attempt = createRoute json
                let! stored = storedNamed (fresh ()) name
                Assert.True(attempt |> Result.isError)
                Assert.Equal<MasterAgreement.MasterAgreement list>([ existing ], stored)
            })

    [<Fact>]
    member _.``REQ-CF-2.6 updating an agreement's name to the name of another existing agreement is rejected with a typed error and both stored agreements are unchanged`` () =
        let first = unique "CF-2.6 rename first"
        let second = unique "CF-2.6 rename second"
        cleaningUp [ first; second ] (fun () ->
            result {
                let! firstBefore = createDaily first
                let! secondBefore = createDaily second
                let attempt = updateRoute { noUpdate second with agreementNameUpdate = SetTo first }
                let! firstAfter = storedNamed (fresh ()) first
                let! secondAfter = storedNamed (fresh ()) second
                Assert.True(attempt |> Result.isError)
                Assert.Equal<MasterAgreement.MasterAgreement list>([ firstBefore ], firstAfter)
                Assert.Equal<MasterAgreement.MasterAgreement list>([ secondBefore ], secondAfter)
            })

    // =========================================================================
    // REQ-CF-2.9 through 2.12 — the fields each cadence needs
    // =========================================================================

    (* Sends the payload with its cadence type replaced by `broken`, then by `whole`. Returns whether the first was
       refused, what was stored after it, and what was stored after the second. *)
    member private _.brokenThenWhole (name: string) (nextInstance: LocalDate) (broken: string) (whole: Contracts.CadenceTypeContract) =
        result {
            let! node = createInput name whole nextInstance |> toNode
            node["cadence"].["cadenceType"] <- JsonNode.Parse(broken)
            let attempt = createRoute (node.ToJsonString())
            let! afterBroken = storedNamed (fresh ()) name
            let! json = createInput name whole nextInstance |> Json.toJson
            let! _ = createRoute json
            let! afterWhole = storedNamed (fresh ()) name
            return (attempt |> Result.isError), afterBroken, afterWhole
        }

    [<Fact>]
    member this.``REQ-CF-2.9 a CreateAgreement payload with a Weekly cadence and no week day is rejected with a typed error and no agreement is stored, while the same payload with a week day is created`` () =
        let name = unique "CF-2.9 weekly"
        let monday = nextOn IsoDayOfWeek.Monday (today ())
        cleaningUp [ name ] (fun () ->
            result {
                let! refused, afterBroken, afterWhole =
                    this.brokenThenWhole name monday """{"Case":"Weekly","Fields":[]}""" (Contracts.Weekly "Monday")
                Assert.True(refused)
                Assert.Empty(afterBroken)
                Assert.Equal(Cadence.Weekly Cadence.Monday, afterWhole |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    [<Fact>]
    member this.``REQ-CF-2.9 a CreateAgreement payload with an EveryOtherWeek cadence and no week day is rejected with a typed error and no agreement is stored, while the same payload with a week day is created`` () =
        let name = unique "CF-2.9 fortnightly"
        let monday = nextOn IsoDayOfWeek.Monday (today ())
        cleaningUp [ name ] (fun () ->
            result {
                let! refused, afterBroken, afterWhole =
                    this.brokenThenWhole name monday """{"Case":"EveryOtherWeek","Fields":[]}"""
                        (Contracts.EveryOtherWeek "Monday")
                Assert.True(refused)
                Assert.Empty(afterBroken)
                Assert.Equal(Cadence.EveryOtherWeek Cadence.Monday, afterWhole |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    [<Fact>]
    member this.``REQ-CF-2.10 a CreateAgreement payload with a Monthly cadence and no month day is rejected with a typed error and no agreement is stored, while the same payload with a month day is created`` () =
        let name = unique "CF-2.10 monthly"
        let t = today ()
        let first = LocalDate(t.Year, t.Month, 1).PlusMonths(1)
        cleaningUp [ name ] (fun () ->
            result {
                let! refused, afterBroken, afterWhole =
                    this.brokenThenWhole name first """{"Case":"Monthly","Fields":[]}"""
                        (Contracts.Monthly(Contracts.DateInMonth 1))
                let! day1 = 1 |> Cadence.DateInMonthNumber.fromInt
                Assert.True(refused)
                Assert.Empty(afterBroken)
                Assert.Equal(Cadence.Monthly(Cadence.DateInMonth day1), afterWhole |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    [<Theory>]
    [<InlineData("DateInMonth")>]
    [<InlineData("NthWeekDay")>]
    [<InlineData("Last")>]
    member _.``REQ-CF-2.10 for each of a date-in-month, an nth weekday and Last, a CreateAgreement payload with a Monthly cadence and that month day is created with that month day`` (monthDay: string) =
        let name = unique $"CF-2.10 {monthDay}"
        let t = today ()
        let nextMonth = LocalDate(t.Year, t.Month, 1).PlusMonths(1)
        cleaningUp [ name ] (fun () ->
            result {
                let! day15 = 15 |> Cadence.DateInMonthNumber.fromInt
                let! second = 2 |> Cadence.WeekInMonthNumber.fromInt
                let contract, expected, nextInstance =
                    match monthDay with
                    | "DateInMonth" -> Contracts.DateInMonth 15, Cadence.DateInMonth day15, nextMonth.PlusDays(14)
                    | "NthWeekDay" ->
                        Contracts.NthWeekDay(2, "Tuesday"), Cadence.NthWeekDay(second, Cadence.Tuesday),
                        LocalDate.FromYearMonthWeekAndDay(nextMonth.Year, nextMonth.Month, 2, IsoDayOfWeek.Tuesday)
                    | _ -> Contracts.Last, Cadence.Last, nextMonth.PlusMonths(1).PlusDays(-1)
                let! json = createInput name (Contracts.Monthly contract) nextInstance |> Json.toJson
                let! _ = createRoute json
                let! stored = storedNamed (fresh ()) name
                Assert.Equal(Cadence.Monthly expected, stored |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    [<Fact>]
    member _.``REQ-CF-2.10 a CreateAgreement payload with a Monthly nth-weekday month day that has a week number and no week day is rejected with a typed error and no agreement is stored`` () =
        let name = unique "CF-2.10 no week day"
        let t = today ()
        let nextMonth = LocalDate(t.Year, t.Month, 1).PlusMonths(1)
        cleaningUp [ name ] (fun () ->
            result {
                let nextInstance = LocalDate.FromYearMonthWeekAndDay(nextMonth.Year, nextMonth.Month, 2, IsoDayOfWeek.Tuesday)
                let! node = createInput name (Contracts.Monthly(Contracts.NthWeekDay(2, "Tuesday"))) nextInstance |> toNode
                node["cadence"].["cadenceType"] <- JsonNode.Parse("""{"Case":"Monthly","Fields":[{"Case":"NthWeekDay","Fields":[2]}]}""")
                let attempt = createRoute (node.ToJsonString())
                let! stored = storedNamed (fresh ()) name
                Assert.True(attempt |> Result.isError)
                Assert.Empty(stored)
            })

    [<Fact>]
    member this.``REQ-CF-2.11 a CreateAgreement payload with an Annually cadence and no month is rejected with a typed error and no agreement is stored, while the same payload with a month is created`` () =
        let name = unique "CF-2.11 no month"
        let march = LocalDate((today ()).Year + 1, 3, 1)
        cleaningUp [ name ] (fun () ->
            result {
                let! refused, afterBroken, afterWhole =
                    this.brokenThenWhole name march """{"Case":"Annually","Fields":[{"Case":"DateInMonth","Fields":[1]}]}"""
                        (Contracts.Annually("March", Contracts.DateInMonth 1))
                let! day1 = 1 |> Cadence.DateInMonthNumber.fromInt
                Assert.True(refused)
                Assert.Empty(afterBroken)
                Assert.Equal(Cadence.Annually(Cadence.March, Cadence.DateInMonth day1), afterWhole |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    [<Fact>]
    member this.``REQ-CF-2.11 a CreateAgreement payload with an Annually cadence and no month day is rejected with a typed error and no agreement is stored, while the same payload with a month day is created`` () =
        let name = unique "CF-2.11 no month day"
        let march = LocalDate((today ()).Year + 1, 3, 1)
        cleaningUp [ name ] (fun () ->
            result {
                let! refused, afterBroken, afterWhole =
                    this.brokenThenWhole name march """{"Case":"Annually","Fields":["March"]}"""
                        (Contracts.Annually("March", Contracts.DateInMonth 1))
                let! day1 = 1 |> Cadence.DateInMonthNumber.fromInt
                Assert.True(refused)
                Assert.Empty(afterBroken)
                Assert.Equal(Cadence.Annually(Cadence.March, Cadence.DateInMonth day1), afterWhole |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    [<Fact>]
    member _.``REQ-CF-2.12 a CreateAgreement payload with a Daily cadence and no week day, month or month day creates the agreement with a Daily cadence`` () =
        let name = unique "CF-2.12 daily"
        cleaningUp [ name ] (fun () ->
            result {
                let! node = createInput name Contracts.Daily (today ()) |> toNode
                Assert.Equal<string>("""{"Case":"Daily"}""", (node["cadence"].["cadenceType"]).ToJsonString())
                let! _ = createRoute (node.ToJsonString())
                let! stored = storedNamed (fresh ()) name
                Assert.Equal(Cadence.Daily, stored |> List.exactlyOne |> MasterAgreement.cadence |> Cadence.cadenceType)
            })

    // =========================================================================
    // REQ-CF-2.23, 2.25 — the next-instance date
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.23 after an EveryOtherWeek agreement whose start date is on the off week of its next-instance date gets two successive Instances, its next-instance dates are exactly 14 and then 28 days after the original`` () =
        rolledBack (fun s ->
            result {
                let nextInstance = s.today.PlusDays(3)
                let fortnightly = Cadence.EveryOtherWeek(Cadence.WeekDay.fromIsoDayOfWeek nextInstance.DayOfWeek)
                let! agreement =
                    s.create (unique "CF-2.23 anchor") fortnightly nextInstance (nextInstance.PlusDays(-7)) None 1
                let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
                let nextInstanceNow () =
                    agreementId |> MasterAgreement.fetchById s.Context
                    |> Result.map (fun m -> (m |> MasterAgreement.cadence |> Cadence.nextInstance).nextInstance)
                let! _ = InstanceOrchestration.createInstanceCompositeAndSaveToDb s.Context agreementId nextInstance []
                let! afterFirst = nextInstanceNow ()
                let! _ = InstanceOrchestration.createInstanceCompositeAndSaveToDb s.Context agreementId afterFirst []
                let! afterSecond = nextInstanceNow ()
                Assert.Equal<LocalDate list>([ nextInstance.PlusDays(14); nextInstance.PlusDays(28) ], [ afterFirst; afterSecond ])
            })

    [<Fact>]
    member _.``REQ-CF-2.25 creating an agreement whose next-instance date does not fit its cadence is rejected with a typed error and no agreement is stored`` () =
        rolledBack (fun s ->
            result {
                let name = unique "CF-2.25 misfit"
                let monday = nextOn IsoDayOfWeek.Monday s.today
                let attempt = s.create name (Cadence.Weekly Cadence.Monday) (monday.PlusDays(1)) (s.today.PlusDays(-30)) None 1
                let rejected =
                    match attempt with
                    | Error (AsError (BizGeneralError.CadenceDateNotOnWeekDay (d, "Monday"))) -> d = monday.PlusDays(1)
                    | _ -> false
                let! stored = s.stored name
                Assert.True(rejected)
                Assert.Empty(stored)
            })

    [<Fact>]
    member _.``REQ-CF-2.25 updating an agreement's next-instance date to one that does not fit its cadence is rejected with a typed error and the stored agreement is unchanged`` () =
        let name = unique "CF-2.25 update misfit"
        let monday = nextOn IsoDayOfWeek.Monday (today ())
        cleaningUp [ name ] (fun () ->
            result {
                let! json = createInput name (Contracts.Weekly "Monday") monday |> Json.toJson
                let! _ = createRoute json
                let! before = storedNamed (fresh ()) name
                let attempt =
                    updateRoute
                        { noUpdate name with
                            cadenceUpdate = SetTo { cadenceType = Contracts.Weekly "Monday"; nextInstance = monday.PlusDays(1) } }
                let! after = storedNamed (fresh ()) name
                Assert.True(attempt |> Result.isError)
                Assert.Equal<MasterAgreement.MasterAgreement list>(before, after)
            })

    // =========================================================================
    // REQ-CF-2.26 — start and end dates against today
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.26 creating an agreement whose start date is after today stores it with that start date`` () =
        rolledBack (fun s ->
            result {
                let name = unique "CF-2.26 future start"
                let! _ = s.daily name (s.today.PlusDays(30)) None
                let! stored = s.stored name
                Assert.Equal(s.today.PlusDays(30), stored |> List.exactlyOne |> MasterAgreement.activityPeriod |> ActivityPeriod.activeBegin)
            })

    [<Fact>]
    member _.``REQ-CF-2.26 updating an agreement whose start date is after today succeeds and stores the change`` () =
        rolledBack (fun s ->
            result {
                let name = unique "CF-2.26 future update"
                let! agreement = s.daily name (s.today.PlusDays(30)) None
                let! counterparty = "Updated before its start" |> Counterparty.create
                let! _ =
                    AgreementOrchestration.updateAgreement s.Context [] []
                        { agreementIdToUpdate = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
                          agreementNameUpdate = NoChange
                          directionUpdate = NoChange
                          cadenceUpdate = NoChange
                          counterpartyUpdate = SetTo counterparty
                          activityPeriodUpdate = NoChange
                          memoUpdate = NoChange }
                let! stored = s.stored name
                Assert.Equal<string>("Updated before its start", stored |> List.exactlyOne |> MasterAgreement.counterparty |> Counterparty.value)
            })

    [<Fact>]
    member _.``REQ-CF-2.26 creating an agreement whose end date is yesterday is rejected with a typed error and no agreement is stored, while one whose end date is today is created`` () =
        rolledBack (fun s ->
            result {
                let endedName = unique "CF-2.26 ended yesterday"
                let endingName = unique "CF-2.26 ends today"
                let start = s.today.PlusDays(-30)
                let attempt = s.daily endedName start (Some(s.today.PlusDays(-1)))
                let rejected =
                    match attempt with
                    | Error (AsError (CashFlowError.CashflowMasterAgreementUnavailable _)) -> true
                    | _ -> false
                let! _ = s.daily endingName start (Some s.today)
                let! ended = s.stored endedName
                let! ending = s.stored endingName
                Assert.True(rejected)
                Assert.Empty(ended)
                Assert.Equal(Some s.today, ending |> List.exactlyOne |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd)
            })

    [<Fact>]
    member _.``REQ-CF-2.26 updating an agreement's end date to yesterday is rejected with a typed error and its stored end date is unchanged, while updating it to today succeeds`` () =
        let name = unique "CF-2.26 end update"
        let t = today ()
        cleaningUp [ name ] (fun () ->
            result {
                let! _ = createDaily name
                let refused = updateRoute { noUpdate name with activeEndUpdate = SetTo(Some(t.PlusDays(-1))) }
                let! afterRefused = storedNamed (fresh ()) name
                let! _ = updateRoute { noUpdate name with activeEndUpdate = SetTo(Some t) }
                let! afterAccepted = storedNamed (fresh ()) name
                let endOf stored = stored |> List.exactlyOne |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd
                Assert.True(refused |> Result.isError)
                Assert.Equal(None, afterRefused |> endOf)
                Assert.Equal(Some t, afterAccepted |> endOf)
            })

    [<Fact>]
    member _.``REQ-CF-2.26 updating a field other than the end date of an agreement whose stored end date is before today is rejected with a typed error and the agreement is unchanged`` () =
        (* The agreement is ended at the model level, which checks nothing; no route can end one in the past. *)
        let name = unique "CF-2.26 already ended"
        let t = today ()
        cleaningUp [ name ] (fun () ->
            result {
                let! created = createDaily name
                let! _ =
                    runCommandRouteAndAutoCompleteTransaction CashFlowUpdateAgreement (fun context ->
                        result {
                            let! period =
                                ActivityPeriod.create (t.PlusDays(-30)) (Some(t.PlusDays(-1)))
                                    ActivityPeriod.ConsideredAvailableBeforeBeginDate
                            return!
                                MasterAgreement.update context
                                    { agreementIdToUpdate = created |> MasterAgreement.agreementID
                                      agreementNameUpdate = NoChange
                                      directionUpdate = NoChange
                                      cadenceUpdate = NoChange
                                      counterpartyUpdate = NoChange
                                      activityPeriodUpdate = SetTo period
                                      memoUpdate = NoChange }
                        })
                let! before = storedNamed (fresh ()) name
                let attempt = updateRoute { noUpdate name with counterpartyUpdate = SetTo "Changed after it ended" }
                let! after = storedNamed (fresh ()) name
                Assert.True(attempt |> Result.isError)
                Assert.Equal<MasterAgreement.MasterAgreement list>(before, after)
            })

    // =========================================================================
    // REQ-CF-2.27 — at least one Payment Agreement
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.27 creating an agreement with no Payment Agreements is rejected with a typed error and no agreement is stored, while the same agreement with exactly one Payment Agreement is created`` () =
        rolledBack (fun s ->
            result {
                let name = unique "CF-2.27 legs"
                let start = s.today.PlusDays(-30)
                let attempt = s.create name Cadence.Daily s.today start None 0
                let rejected =
                    match attempt with
                    | Error (AsError CashFlowError.CashflowPaymentAgreementsListCannotBeEmpty) -> true
                    | _ -> false
                let! afterNone = s.stored name
                let! _ = s.create name Cadence.Daily s.today start None 1
                let! afterOne = s.stored name
                Assert.True(rejected)
                Assert.Empty(afterNone)
                Assert.Single(afterOne) |> ignore
            })
