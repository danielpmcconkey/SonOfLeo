module Tests.Integrated.CrossDomainOrchestration.PreLedgerBalances

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.PreLedgerBalanceOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// A balance as stored: account code, account name, balance date and balance.
type private BalanceSummary = string * string * LocalDate * decimal

let private summary (view: PreLedgerBalanceView) : BalanceSummary =
    view.accountCode,
    view.accountName,
    view.preLedgerBalance |> PreLedgerBalance.balanceDate,
    view.preLedgerBalance |> PreLedgerBalance.balance |> Money.amount

// the route path: balances arrive as input contracts and go through the route's converter, which resolves the account
// codes to IDs
let private record context (entries: (string * LocalDate * decimal) list) =
    entries
    |> List.map (fun (code, date, balance) ->
        { accountCode = code; balanceDate = date; balance = balance }
        |> ``convert [PreLedgerBalanceEntryInput] to [PreLedgerBalanceInput]`` context)
    |> convertListOfResultsToResultsList
    |> Result.bind (recordPreLedgerBalances context)

let private listed context (code: string option) beginDate endDate =
    code
    |> convertOptionToDesiredTypeWithFallibleConverter (fallibleConverterAccountCodeToAccountId context)
    |> Result.bind (fun accountId -> listPreLedgerBalances context accountId beginDate endDate)
    |> Result.map (List.map summary)

let private delete context code date =
    code
    |> fallibleConverterAccountCodeToAccountId context
    |> Result.bind (fun accountId -> deletePreLedgerBalance context accountId date)

let private positionsCash = "Fixture Positions Cash"
let private loanPayable = "Fixture Loan Payable"
let private rentalMortgage = "Fixture Rental Mortgage"

[<Collection("SharedTestData")>]
type PreLedgerBalancesTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    // the last day of month -6, the day before the earliest fiscal period starts; the fixture records nothing on it
    let me6 () = p.monthEnd6

    [<Fact>]
    member _.``REQ-POS-15.1 REQ-POS-15.4 recording a pre-ledger balance for an Asset account and one for a Liability account stores each and returns each as stored with its account code, date and balance, marked as not replacing one`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                let! recorded = record context [ "F-1275", me6 (), 1234.56M; "F-2230", me6 (), 789.01M ]
                Assert.Equal<(BalanceSummary * bool) list>(
                    [ ("F-1275", positionsCash, me6 (), 1234.56M), false
                      ("F-2230", loanPayable, me6 (), 789.01M), false ],
                    recorded |> List.map (fun r -> summary r.balance, r.replacedExisting))
                let! stored = listed context None (me6 ()) (me6 ())
                Assert.Equal<BalanceSummary list>(
                    [ "F-1275", positionsCash, me6 (), 1234.56M; "F-2230", loanPayable, me6 (), 789.01M ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.1 a pre-ledger balance of 0.00 and one of -15.00 are each recorded and listed exactly as recorded`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                let! _ = record context [ "F-1275", me6 (), 0.00M; "F-2230", me6 (), -15.00M ]
                let! stored = listed context None (me6 ()) (me6 ())
                Assert.Equal<BalanceSummary list>(
                    [ "F-1275", positionsCash, me6 (), 0.00M; "F-2230", loanPayable, me6 (), -15.00M ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.1 for each of an Equity, a Revenue and an Expense account, recording a pre-ledger balance is rejected with a typed error naming the account code and its type`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            [ "F-3040", "Equity"; "F-4000", "Revenue"; "F-5000", "Expense" ]
            |> List.iter (fun (code, accountType) ->
                record context [ code, me6 (), 10.00M ]
                |> expectError
                    (function AsError (PositionsPreLedgerAccountTypeNotAllowed (c, t)) -> Some(c, t) | _ -> None)
                    (fun found -> Assert.Equal((code, accountType), found)))
            Ok())
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.1 REQ-POS-15.4 recording a pre-ledger balance for an account and date that already has one replaces its balance, returns it marked as replacing one, and listing gives only the new balance for that account and date`` () =
        // the fixture records 2,000.00 for F-1275 on monthEnd8
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                let! recorded = record context [ "F-1275", p.monthEnd8, 2100.00M ]
                let only = recorded |> List.exactlyOne
                Assert.Equal((("F-1275", positionsCash, p.monthEnd8, 2100.00M), true), (summary only.balance, only.replacedExisting))
                let! stored = listed context (Some "F-1275") p.monthEnd8 p.monthEnd8
                Assert.Equal<BalanceSummary list>([ "F-1275", positionsCash, p.monthEnd8, 2100.00M ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.2 a pre-ledger balance dated the day before the earliest fiscal period's start date is accepted, and one dated on that start date is rejected with a typed error naming the account code and the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                let dayBefore = p.ledgerStart.PlusDays(-1)
                let! _ = record context [ "F-1275", dayBefore, 1.00M ]
                let! stored = listed context (Some "F-1275") dayBefore dayBefore
                Assert.Equal<BalanceSummary list>([ "F-1275", positionsCash, dayBefore, 1.00M ], stored)
                record context [ "F-1275", p.ledgerStart, 1.00M ]
                |> expectError
                    (function AsError (PositionsPreLedgerDateNotBeforeLedger (c, d)) -> Some(c, d) | _ -> None)
                    (fun found -> Assert.Equal(("F-1275", p.ledgerStart), found))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.3 a pre-ledger balance for a ledger account linked to an Investment Account is rejected with a typed error naming the account code and that Investment Account`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            record context [ "F-1260", me6 (), 1000.00M ]
            |> expectError
                (function AsError (PositionsPreLedgerAccountLinkedToInvestmentAccount (c, a)) -> Some(c, a) | _ -> None)
                (fun found -> Assert.Equal(("F-1260", PF.alexBrokerage), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.3 a pre-ledger balance for a Property's asset account is rejected with a typed error naming the account code and that Property, and one for a Property's mortgage account is accepted`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                record context [ "F-1510", me6 (), 400000.00M ]
                |> expectError
                    (function AsError (PositionsPreLedgerAccountIsPropertyAsset (c, prop)) -> Some(c, prop) | _ -> None)
                    (fun found -> Assert.Equal(("F-1510", PF.residence), found))
                let! _ = record context [ "F-2310", me6 (), 301000.00M ]
                let! stored = listed context (Some "F-2310") (me6 ()) (me6 ())
                Assert.Equal<BalanceSummary list>([ "F-2310", "Fixture Residence Mortgage", me6 (), 301000.00M ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.4 one operation naming the same account and date twice is rejected with a typed error naming the account code and the date, and neither balance is recorded`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                record context [ "F-1275", me6 (), 1.00M; "F-2230", me6 (), 3.00M; "F-1275", me6 (), 2.00M ]
                |> expectError
                    (function AsError (PositionsPreLedgerBalanceRepeatedInRequest (c, d)) -> Some(c, d) | _ -> None)
                    (fun found -> Assert.Equal(("F-1275", me6 ()), found))
                let! stored = listed context None (me6 ()) (me6 ())
                Assert.Empty(stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.5 deleting the pre-ledger balance for an account and date removes it and leaves the account's balances on other dates`` () =
        // the fixture records F-2320 on monthEnd9, monthEnd8 and monthEnd7
        runCommandRouteAndAutoRollback PositionsDeletePreLedgerBalance (fun context ->
            result {
                let! deleted = delete context "F-2320" p.monthEnd8
                Assert.Equal(("F-2320", rentalMortgage, p.monthEnd8, 185000.00M), summary deleted)
                let! stored = listed context (Some "F-2320") p.monthEnd9 p.monthEnd7
                Assert.Equal<BalanceSummary list>(
                    [ "F-2320", rentalMortgage, p.monthEnd9, 186000.00M; "F-2320", rentalMortgage, p.monthEnd7, 184000.00M ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.5 deleting for an account and date with no pre-ledger balance fails with a typed not-found error naming the account code and the date`` () =
        runCommandRouteAndAutoRollback PositionsDeletePreLedgerBalance (fun context ->
            delete context "F-1275" (me6 ())
            |> expectError
                (function AsError (PositionsPreLedgerBalanceDoesntExist (c, d)) -> Some(c, d) | _ -> None)
                (fun found -> Assert.Equal(("F-1275", me6 ()), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.6 listing pre-ledger balances for one account between two dates returns every balance of that account dated in the range, both ends included, in date order, each with the account's code and name, and none outside the range or of another account`` () =
        // F-2320's monthEnd9 balance is outside monthEnd8 to monthEnd7; F-1275 and F-2230 have balances inside it
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                let! stored = listed context (Some "F-2320") p.monthEnd8 p.monthEnd7
                Assert.Equal<BalanceSummary list>(
                    [ "F-2320", rentalMortgage, p.monthEnd8, 185000.00M; "F-2320", rentalMortgage, p.monthEnd7, 184000.00M ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.6 listing pre-ledger balances for every account between two dates, recorded out of account-code and date order, returns every balance in the range and none outside it, ordered by account code and then balance date`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            result {
                let! _ = record context [ "F-2320", me6 (), 183000.00M; "F-1000", me6 (), 50.00M; "F-1275", me6 (), 10.00M ]
                let! stored = listed context None p.monthEnd8 (me6 ())
                Assert.Equal<BalanceSummary list>(
                    [ "F-1000", "Assets", p.monthEnd8, 100.00M
                      "F-1000", "Assets", me6 (), 50.00M
                      "F-1275", positionsCash, p.monthEnd8, 2000.00M
                      "F-1275", positionsCash, p.monthEnd7, 2500.00M
                      "F-1275", positionsCash, me6 (), 10.00M
                      "F-1280", "Fixture Operating Cash", p.monthEnd8, -15.00M
                      "F-2230", loanPayable, p.monthEnd8, 1500.00M
                      "F-2230", loanPayable, p.monthEnd7, 0.00M
                      "F-2320", rentalMortgage, p.monthEnd8, 185000.00M
                      "F-2320", rentalMortgage, p.monthEnd7, 184000.00M
                      "F-2320", rentalMortgage, me6 (), 183000.00M ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.6 listing pre-ledger balances with the end date the day before the begin date fails with a typed error naming both dates`` () =
        runCommandRouteAndAutoRollback PositionsRecordPreLedgerBalances (fun context ->
            listed context None p.monthEnd7 (p.monthEnd7.PlusDays(-1))
            |> expectError
                (function AsError (PositionsPreLedgerListEndBeforeBegin (b, e)) -> Some(b, e) | _ -> None)
                (fun found -> Assert.Equal((p.monthEnd7, p.monthEnd7.PlusDays(-1)), found))
            |> Ok)
        |> railroadWrapper
