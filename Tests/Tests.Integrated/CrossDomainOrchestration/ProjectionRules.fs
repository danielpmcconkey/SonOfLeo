module Tests.Integrated.CrossDomainOrchestration.ProjectionRules

open System
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility
open App.Utility.IAppError
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
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Ui.InterfaceBridge.CommandRoute
open NodaTime
open Tests.Helpers
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module Contracts = Ui.InterfaceBridge.InterfaceContracts.CashFlowContracts

(* Every test but the route tests runs in a transaction that rolls back and calls the projection directly, so it sees
   what it built. Each builds its own Cash account, so that account's figures come only from the test's own
   agreements: Daily, so any Instance date fits, with 100.00 legs. An Outgo leg is debit F-2230, credit the test's cash
   account. An Income leg is debit the test's cash account, credit F-4290. A Payment on an Outgo Invoice points at the
   F-2230 line of a journal entry dated today; one on an Income Invoice points at the F-4290 line. *)

type private Scenario(fixture: TestDataFixture, context: Context.Context) =
    let accountIdOf code =
        fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = code) |> Account.accountId

    member _.Context = context
    member _.today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

    /// A new Asset account of subtype Cash, active from last year to the end date given. Returns its id, code and name.
    member _.cashAccount (activeEnd: LocalDate option) =
        result {
            let code = $"X{Guid.NewGuid():N}".Substring(0, 10)
            let name = $"Projection rules test cash {code}"
            let! _, accountId =
                createTestAccountFromPrimitives
                    context code name "Asset" ((Calendar.today ()).PlusYears(-2)) activeEnd (Some "Cash") None None
            return accountId, code, name
        }

    /// A Daily agreement with the legs, paying from or into the cash account. Returns its id and its legs' ids.
    member this.agreement (direction: FlowDirection) (cashId: AccountId) (legCount: int) =
        result {
            let name = $"Projection rules test {Guid.NewGuid():N}"
            let! agreementName = name |> AgreementName.create
            let! counterparty = "Projection rules test counterparty" |> Counterparty.create
            let! activityPeriod =
                ActivityPeriod.create ((Calendar.today ()).PlusYears(-2)) None ActivityPeriod.ConsideredAvailableBeforeBeginDate
            let debit, credit =
                match direction with
                | Outgo -> accountIdOf "F-2230", cashId
                | Income -> cashId, accountIdOf "F-4290"
            let legNames = List.init legCount (fun i -> $"{name} leg {i + 1}")
            let! legs =
                legNames
                |> List.map (fun legName ->
                    result {
                        let! paName = legName |> PaymentAgreementName.create
                        let! expected = Money.fromDecimal 100.00M
                        let! due = 0 |> DaysDueAfterInvoiceDate.create
                        return (paName, (DebitAccount.create debit), (CreditAccount.create credit), Some expected, Some due, None)
                    })
                |> convertListOfResultsToResultsList
            let! agreement =
                AgreementOrchestration.constructNewAndPersist
                    context agreementName direction Cadence.Daily { nextInstance = this.today.PlusDays(-90) }
                    counterparty activityPeriod None legs
            let agreementId = agreement |> AgreementOrchestration.masterAgreement |> MasterAgreement.agreementID
            let legIds =
                legNames
                |> List.map (fun legName ->
                    agreement
                    |> AgreementOrchestration.paymentAgreements
                    |> List.find (fun pa -> pa |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value = legName)
                    |> PaymentAgreement.paymentAgreementId)
            return agreementId, legIds, legNames
        }

    /// A journal entry dated today moving the amount between the cash account and the leg's other account. Returns
    /// the pointer to the line a Payment of the direction must point at.
    member this.paymentLine (direction: FlowDirection) (cashId: AccountId) (amount: decimal) =
        result {
            let landing, lines =
                match direction with
                | Outgo -> accountIdOf "F-2230", [ (accountIdOf "F-2230", amount, "Debit", None); (cashId, amount, "Credit", None) ]
                | Income -> accountIdOf "F-4290", [ (cashId, amount, "Debit", None); (accountIdOf "F-4290", amount, "Credit", None) ]
            let! entry, _ =
                createTestJournalEntryFromPrimitives context $"Projection rules test {Guid.NewGuid()}" None this.today lines [] []
            return
                entry
                |> JournalEntryOrchestration.jeLines
                |> List.find (fun l -> l |> JournalEntryLine.accountId = landing)
                |> JournalEntryLine.journalEntryLineId
                |> Posted
        }

    /// An Instance on the date with a 100.00 Invoice per (leg, due date, Payment amounts) given. Returns its id and its
    /// Invoices' ids in the order given.
    member this.instance
        (direction: FlowDirection) (cashId: AccountId) (agreementId: MasterAgreementId) (date: LocalDate)
        (invoices: (PaymentAgreementId * LocalDate * decimal list) list) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let state = match direction with | Outgo -> InvoiceReceived | Income -> InvoiceSent
            let! fields =
                invoices
                |> List.map (fun (legId, due, paid) ->
                    result {
                        let! payments =
                            paid
                            |> List.map (fun p ->
                                result {
                                    let! pointer = this.paymentLine direction cashId p
                                    let! money = Money.fromDecimal p
                                    return (pointer, None, None, None)
                                })
                            |> convertListOfResultsToResultsList
                        return
                            (legId, None, (InvoiceDate.create date), (DueDate.create due),
                             (InvoiceAmount.create amount), state, None, None, payments)
                    })
                |> convertListOfResultsToResultsList
            let! created = InstanceOrchestration.constructNewAndPersist context agreementId date fields
            let invoiceIds =
                invoices
                |> List.map (fun (legId, _, _) ->
                    created
                    |> InstanceOrchestration.invoiceComposites
                    |> List.map InstanceOrchestration.invoice
                    |> List.find (fun i -> i |> Invoice.paymentAgreementId = legId)
                    |> Invoice.invoiceId)
            return (created |> InstanceOrchestration.instance |> Instance.instanceId), invoiceIds
        }

    member _.project (days: int) =
        result {
            let! horizon = days |> ProjectionHorizonInDays.create
            return! horizon |> CashFlowOps.projectCashFlowNDaysForward context
        }

let private accountIn (projection: CashFlowProjection) (accountId: AccountId) =
    projection.accounts |> List.find (fun a -> a.accountId = accountId)

let private billsFor (projection: CashFlowProjection) (instanceId: InstanceId) =
    projection.billsToChase |> List.filter (fun b -> b.instanceId = instanceId)

let private amountOf (money: Money.Money) = money |> Money.amount

/// A digest of every row of the table, so any added, changed or removed row changes it.
let private digestOf (table: string) =
    executeReaderQuery
        (Context.create NoTransaction FetchOnly |> Context.getDatabaseTransaction)
        $"select md5(coalesce(string_agg(t::text, '|' order by t::text), '')) as digest from {table} t"
        []
        (fun row -> row |> RowReader.getString "digest")
        Ok
        ExactlyOne
    |> Result.map List.exactlyOne

[<Collection("SharedTestData")>]
type ProjectionRulesTests(fixture: TestDataFixture) =

    let rolledBack (body: Scenario -> Result<unit, IAppError>) =
        runCommandRouteAndAutoRollback CashFlowCreateInstance (fun context -> body (Scenario(fixture, context)))
        |> railroadWrapper


    [<Theory>]
    [<InlineData(1)>]
    [<InlineData(365)>]
    member _.``REQ-CF-8.1 for each of 1 and 365, a ProjectCashFlow payload with that horizon returns a projection rather than an error`` (horizon:int) =
        result {
            let! returned = routeUiCommandForTesting "CashFlow" "ProjectCashFlow" [] $"{{\"projectionHorizonInDays\":{horizon}}}"
            let! projection = Json.fromJson<Contracts.CashFlowProjectionReturn> returned
            Assert.NotEmpty(projection.accounts)
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-1)>]
    [<InlineData(366)>]
    member _.``REQ-CF-8.1 for each of 0, -1 and 366, a ProjectCashFlow payload with that horizon is rejected with a typed error naming the horizon and the bound it breaks`` (horizon:int) =
        let attempt = routeUiCommandForTesting "CashFlow" "ProjectCashFlow" [] $"{{\"projectionHorizonInDays\":{horizon}}}"
        let namesIt =
            match attempt with
            | Error (AsError (CashFlowError.CashflowProjectionHorizonInDaysBelowMin(raw, bound))) -> raw = horizon && bound = 1
            | Error (AsError (CashFlowError.CashflowProjectionHorizonInDaysExceededMax(raw, bound))) -> raw = horizon && bound = 365
            | _ -> false
        Assert.True(namesIt)

    [<Fact>]
    member _.``REQ-CF-8.1 for an account with nonzero current balance, known inflows and known outflows, projected low equals current balance plus known inflows minus known outflows`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! incomeId, incomeLegs, _ = s.agreement Income cashId 1
                let! outgoId, outgoLegs, _ = s.agreement Outgo cashId 1
                (* The Income Invoice's 30.00 Payment also debits the cash account, so its balance is nonzero. *)
                let! _ = s.instance Income cashId incomeId s.today [ (incomeLegs[0], s.today.PlusDays(5), [ 30.00M ]) ]
                let! _ = s.instance Outgo cashId outgoId s.today [ (outgoLegs[0], s.today.PlusDays(5), []) ]
                let! projection = s.project 10
                let account = accountIn projection cashId
                Assert.NotEqual(0M, account.currentBalance |> amountOf)
                Assert.NotEqual(0M, account.knownInflows |> amountOf)
                Assert.NotEqual(0M, account.knownOutflows |> amountOf)
                Assert.Equal(
                    (account.currentBalance |> amountOf) + (account.knownInflows |> amountOf) - (account.knownOutflows |> amountOf),
                    account.projectedLow |> amountOf)
            })

    [<Fact>]
    member _.``REQ-CF-8.1 a journal entry dated today on a managed cash account moves that account's current balance by its net amount, while one dated tomorrow does not move it`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let balance () = s.project 10 |> Result.map (fun p -> (accountIn p cashId).currentBalance |> amountOf)
                let! before = balance ()
                let! _ =
                    createTestJournalEntryFromPrimitives s.Context "Projection rules test today" None s.today
                        [ (cashId, 50.00M, "Debit", None); (fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-4290") |> Account.accountId, 50.00M, "Credit", None) ] [] []
                let! afterToday = balance ()
                let! _ =
                    createTestJournalEntryFromPrimitives s.Context "Projection rules test tomorrow" None (s.today.PlusDays(1))
                        [ (cashId, 20.00M, "Debit", None); (fixture.Data.accounts |> List.find (fun a -> a |> Account.code |> AccountCode.value = "F-4290") |> Account.accountId, 20.00M, "Credit", None) ] [] []
                let! afterTomorrow = balance ()
                Assert.Equal(before + 50.00M, afterToday)
                Assert.Equal(afterToday, afterTomorrow)
            })

    [<Fact>]
    member _.``REQ-CF-8.2 a partially paid Income Invoice on an unfulfilled Instance due within the horizon adds its outstanding amount, not its amount, to the known inflows of its Payment Agreement's debit account, and to no other account's inflows or outflows`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Income cashId 1
                let! before = s.project 10
                let! _ = s.instance Income cashId agreementId s.today [ (legs[0], s.today.PlusDays(5), [ 40.00M ]) ]
                let! after = s.project 10
                let figures (p: CashFlowProjection) =
                    p.accounts
                    |> List.filter (fun a -> a.accountId <> cashId)
                    |> List.map (fun a -> a.accountId, a.knownInflows |> amountOf, a.knownOutflows |> amountOf)
                    |> List.sortBy (fun (id, _, _) -> id |> AccountId.value)
                let account = accountIn after cashId
                Assert.Equal(60.00M, account.knownInflows |> amountOf)
                Assert.Equal(0M, account.knownOutflows |> amountOf)
                Assert.Equal<(AccountId * decimal * decimal) list>(figures before, figures after)
            })

    [<Fact>]
    member _.``REQ-CF-8.2 an unpaid Income Invoice due on the horizon end adds its outstanding amount to known inflows, while one due the day after adds nothing`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Income cashId 2
                let! _ =
                    s.instance Income cashId agreementId s.today
                        [ (legs[0], s.today.PlusDays(10), []); (legs[1], s.today.PlusDays(11), []) ]
                let! projection = s.project 10
                Assert.Equal(100.00M, (accountIn projection cashId).knownInflows |> amountOf)
            })

    [<Fact>]
    member _.``REQ-CF-8.2 an unpaid Income Invoice due 60 days ago adds its outstanding amount to known inflows`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Income cashId 1
                let! _ = s.instance Income cashId agreementId (s.today.PlusDays(-60)) [ (legs[0], s.today.PlusDays(-60), []) ]
                (* The horizon is 10 days, so a window mirrored back from today would miss this Invoice. *)
                let! projection = s.project 10
                Assert.Equal(100.00M, (accountIn projection cashId).knownInflows |> amountOf)
            })

    [<Fact>]
    member _.``REQ-CF-8.2 a FullyPaid Income Invoice due within the horizon adds nothing to known inflows`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Income cashId 1
                let! _ = s.instance Income cashId agreementId s.today [ (legs[0], s.today.PlusDays(5), [ 100.00M ]) ]
                let! projection = s.project 10
                Assert.Equal(0M, (accountIn projection cashId).knownInflows |> amountOf)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance within the horizon with one of its two Payment Agreements invoiced yields exactly one bill to chase, for the other, reporting its agreement name, payment agreement name, Instance date and cadence`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, legNames = s.agreement Outgo cashId 2
                let date = s.today.PlusDays(3)
                let! instanceId, _ = s.instance Outgo cashId agreementId date [ (legs[0], date, []) ]
                let! master = agreementId |> MasterAgreement.fetchById s.Context
                let! projection = s.project 10
                let bill = billsFor projection instanceId |> List.exactlyOne
                Assert.Equal<string>(legNames[1], bill.paymentAgreementName |> PaymentAgreementName.value)
                Assert.Equal(master |> MasterAgreement.agreementName, bill.agreementName)
                Assert.Equal(date, bill.instanceDate)
                Assert.Equal(Cadence.Daily, bill.cadenceType)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance with no Invoices dated on the horizon end yields a bill to chase for each of its Payment Agreements, while one dated the day after yields none`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, _, _ = s.agreement Outgo cashId 2
                let! onEnd, _ = s.instance Outgo cashId agreementId (s.today.PlusDays(10)) []
                let! dayAfter, _ = s.instance Outgo cashId agreementId (s.today.PlusDays(11)) []
                let! projection = s.project 10
                Assert.Equal(2, billsFor projection onEnd |> List.length)
                Assert.Empty(billsFor projection dayAfter)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance with no Invoices dated 60 days ago yields a bill to chase for each of its Payment Agreements`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, _, _ = s.agreement Outgo cashId 2
                let! instanceId, _ = s.instance Outgo cashId agreementId (s.today.PlusDays(-60)) []
                let! projection = s.project 10
                Assert.Equal(2, billsFor projection instanceId |> List.length)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 an unfulfilled Instance with an Invoice for every Payment Agreement yields no bill to chase for that Instance`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Outgo cashId 2
                let date = s.today.PlusDays(3)
                let! instanceId, _ = s.instance Outgo cashId agreementId date [ (legs[0], date, []); (legs[1], date, []) ]
                let! projection = s.project 10
                Assert.Empty(billsFor projection instanceId)
            })

    [<Fact>]
    member _.``REQ-CF-8.4 a fulfilled Instance within the horizon, with one of its two Payment Agreements invoiced and that Invoice FullyPaid, yields no bill to chase for the other`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Outgo cashId 2
                let date = s.today.PlusDays(3)
                let! instanceId, _ = s.instance Outgo cashId agreementId date [ (legs[0], date, [ 100.00M ]) ]
                let! fulfilled = instanceId |> Instance.fetchById s.Context |> Result.map Instance.isFulfilled
                let! projection = s.project 10
                Assert.True(fulfilled)
                Assert.Empty(billsFor projection instanceId)
            })

    [<Fact>]
    member _.``REQ-CF-8.5 two projections with the same horizon over unchanged data give identical results, including the order of accounts, Invoices and bills to chase`` () =
        rolledBack (fun s ->
            result {
                let! cashId, _, _ = s.cashAccount None
                let! agreementId, legs, _ = s.agreement Outgo cashId 2
                let! _ = s.instance Outgo cashId agreementId s.today [ (legs[0], s.today.PlusDays(2), [ 30.00M ]) ]
                let! first = s.project 30
                let! second = s.project 30
                Assert.Equal<CashFlowProjection>(first, second)
            })

    [<Fact>]
    member _.``REQ-CF-8.6 every active account with subtype Cash appears in the projection exactly once, including one with no Invoices, and no account of another subtype appears`` () =
        rolledBack (fun s ->
            result {
                let! cashId, code, _ = s.cashAccount None
                let! all = Account.fetchAll s.Context false
                let expected =
                    all
                    |> List.filter (fun a ->
                        a |> Account.accountSubType = Some Cash
                        && a |> Account.activityPeriod |> ActivityPeriod.isActive s.today)
                    |> List.map (Account.code >> AccountCode.value)
                    |> List.sort
                let! projection = s.project 10
                let codes = projection.accounts |> List.map (fun a -> a.accountCode |> AccountCode.value) |> List.sort
                Assert.Contains(code, codes)
                Assert.Equal<string list>(expected, codes)
            })

    [<Fact>]
    member _.``REQ-CF-8.6 an inactive account with subtype Cash does not appear in the projection`` () =
        rolledBack (fun s ->
            result {
                let! inactiveId, _, _ = s.cashAccount (Some(s.today.PlusDays(-1)))
                let! projection = s.project 10
                Assert.DoesNotContain(inactiveId, projection.accounts |> List.map _.accountId)
            })

    [<Fact>]
    member _.``REQ-CF-8.7 each projected account reports its code and name as stored, and lists exactly the Invoices that contributed to its known inflows and outflows`` () =
        rolledBack (fun s ->
            result {
                let! cashId, code, name = s.cashAccount None
                let! incomeId, incomeLegs, _ = s.agreement Income cashId 3
                let! outgoId, outgoLegs, _ = s.agreement Outgo cashId 1
                let! _, incomeInvoices =
                    s.instance Income cashId incomeId s.today
                        [ (incomeLegs[0], s.today.PlusDays(5), [])
                          (incomeLegs[1], s.today.PlusDays(40), [])
                          (incomeLegs[2], s.today.PlusDays(5), [ 100.00M ]) ]
                let! _, outgoInvoices = s.instance Outgo cashId outgoId s.today [ (outgoLegs[0], s.today.PlusDays(5), []) ]
                let! projection = s.project 10
                let account = accountIn projection cashId
                let expected = [ incomeInvoices[0]; outgoInvoices[0] ] |> List.sortBy InvoiceId.value
                Assert.Equal<string>(code, account.accountCode |> AccountCode.value)
                Assert.Equal<string>(name, account.accountName |> AccountName.value)
                Assert.Equal<InvoiceId list>(expected, account.invoices |> List.map _.invoiceId |> List.sortBy InvoiceId.value)
            })

    [<Fact>]
    member _.``REQ-CF-8.8 a ProjectCashFlow call adds, alters and removes no row of any Account, Master Agreement, Payment Agreement, Instance, Invoice, Payment or journal entry`` () =
        let tables =
            [ "ledger.account"; "ledger.journal_entry"; "ledger.journal_entry_line"; "cashflow.master_agreement"
              "cashflow.payment_agreement"; "cashflow.instance"; "cashflow.invoice"; "cashflow.payment" ]
        result {
            let! before = tables |> List.map digestOf |> convertListOfResultsToResultsList
            let! _ = routeUiCommandForTesting "CashFlow" "ProjectCashFlow" [] "{\"projectionHorizonInDays\":365}"
            let! after = tables |> List.map digestOf |> convertListOfResultsToResultsList
            Assert.Equal<string list>(before, after)
        }
        |> railroadWrapper
