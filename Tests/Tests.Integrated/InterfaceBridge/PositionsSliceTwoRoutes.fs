module Tests.Integrated.InterfaceBridge.PositionsSliceTwoRoutes

open System
open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open App.Utility.IAppError
open App.Utility.Json
open App.Utility.Result
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.UnitRollForward
open Tests.Helpers
open Tests.Helpers.Cleanup
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Xunit

module C = Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
module PF = PositionsFixture

(* Every route that writes commits. A test that writes to Positions makes its own Investment Accounts, named uniquely,
   through the routes and deletes them by name in a finally. Pre-ledger balances go on the fixture's ledger accounts
   on monthEnd6, a date the fixture leaves empty, and are deleted in a finally. Tests that only read use the fixture. *)

let private fresh () = Context.create NoTransaction FetchOnly

let private unique (prefix: string) = $"{prefix} {Guid.NewGuid():N}"

let private send domain verb (payload: string) = routeUiCommandForTesting domain verb [] payload

let private call<'i, 'r> domain verb (input: 'i) : Result<'r, IAppError> =
    input |> Json.toJson<'i> |> Result.bind (send domain verb) |> Result.bind Json.fromJson<'r>

let private cleanUp (cleanUps: (unit -> Result<unit, IAppError>) list) = cleanUps |> cleanUpAll |> railroadWrapper

let private ledger code name : C.LedgerAccountReturn = { code = code; name = name }

// ---- Accounts, Holdings and snapshots ----

let private createAccount (input: C.InvestmentAccountCreateInput) =
    call<C.InvestmentAccountCreateInput, C.InvestmentAccountReturn> "InvestmentAccount" "Create" input

let private createHolding account security basisMethod =
    call<C.HoldingCreateInput, C.HoldingReturn> "Holding" "Create"
        { accountName = account; securityName = security; basisMethod = basisMethod }

let private lot acquired quantity cost : C.AccountSnapshotLotInput =
    { acquiredDate = acquired; quantity = quantity; reportedCostBasis = cost }

let private lotOf (l: C.AccountSnapshotLotReturn) = l.acquiredDate, l.quantity, l.reportedCostBasis

let private recordSnapshots (snapshots: C.AccountSnapshotInput list) =
    call<C.AccountSnapshotRecordInput, C.RecordedAccountSnapshotReturn list> "AccountSnapshot" "Record" { snapshots = snapshots }

let private fetchSnapshot account date =
    call<C.AccountSnapshotFetchInput, C.AccountSnapshotReturn> "AccountSnapshot" "Fetch" { accountName = account; snapshotDate = date }

// ---- Investment activity ----

let private activity date kind description security quantity price amount : C.InvestmentActivityInput =
    { activityDate = date
      kind = kind
      description = description
      source = None
      securityName = security
      quantity = quantity
      price = price
      amount = amount }

let private interest date amount = activity date "Interest" "Interest on cash" None None None amount

let private rangeOf account beginDate endDate activities : C.InvestmentActivityRangeInput =
    { accountName = account; beginDate = beginDate; endDate = endDate; activities = activities }

let private recordActivity (ranges: C.InvestmentActivityRangeInput list) =
    call<C.InvestmentActivityRecordInput, C.RecordedActivityRangeReturn list> "InvestmentActivity" "Record" { ranges = ranges }

let private listActivity account beginDate endDate =
    call<C.InvestmentActivityListInput, C.InvestmentActivityReturn list> "InvestmentActivity" "List"
        { accountName = account; beginDate = beginDate; endDate = endDate }

let private activityOf (a: C.InvestmentActivityReturn) =
    a.activityDate, a.kind, a.description, a.securityName, a.quantity, a.price, a.amount

// ---- Pre-ledger balances ----

let private recordBalances (balances: (string * LocalDate * decimal) list) =
    call<C.PreLedgerBalanceRecordInput, C.RecordedPreLedgerBalanceReturn list> "PreLedgerBalance" "Record"
        { balances = balances |> List.map (fun (code, date, balance) -> { accountCode = code; balanceDate = date; balance = balance }) }

let private listBalances (code: string option) beginDate endDate =
    call<C.PreLedgerBalanceListInput, C.PreLedgerBalanceReturn list> "PreLedgerBalance" "List"
        { accountCode = code; beginDate = beginDate; endDate = endDate }

let private balanceOf (b: C.PreLedgerBalanceReturn) = b.ledgerAccount, b.balanceDate, b.balance

let private codeNotFound = function AsError (AccountCodeDoesntMatchAccountId code) -> Some code | _ -> None

[<Collection("SharedTestData")>]
type PositionsSliceTwoRoutesTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    /// A route-made Taxable account owned by Alex, active from the fixture's begin date, holding the total market fund.
    let accountHoldingTotalMarket name =
        result {
            let! _ =
                createAccount
                    { accountName = name
                      institution = "Example Route Bank"
                      accountGroup = "Route Group"
                      taxTreatment = "Taxable"
                      owners = [ PF.alex ]
                      activeBegin = p.accountsActiveBegin
                      activeEnd = None
                      ledgerAccountCode = None }
            let! _ = createHolding name PF.totalMarket (Some "AverageCost")
            return ()
        }

    [<Fact>]
    member _.``REQ-POS-6.13 REQ-POS-7.5 an AccountSnapshot Record payload whose line carries three lots, two identical and one with a six-decimal quantity, returns them, and an AccountSnapshot Fetch payload gives them back, in the order sent with every quantity exactly as sent`` () =
        let name = unique "Route Lots Account"
        let lastYear = p.accountsActiveBegin
        try
            result {
                do! accountHoldingTotalMarket name
                let lots =
                    [ lot (lastYear.PlusDays 30) 2.5M (Some 250.00M)
                      lot (lastYear.PlusDays 30) 2.5M (Some 250.00M)
                      lot (lastYear.PlusDays 10) 5.123456M None ]
                let! returned =
                    recordSnapshots
                        [ { accountName = name
                            snapshotDate = p.d1
                            provenance = "Reported"
                            contributionBasis = None
                            lines =
                              [ { securityName = PF.totalMarket
                                  quantity = 10.123456M
                                  price = 100.00M
                                  marketValue = 1012.35M
                                  reportedCostBasis = None
                                  lots = lots } ] } ]
                let expected =
                    [ lastYear.PlusDays 30, 2.5M, Some 250.00M
                      lastYear.PlusDays 30, 2.5M, Some 250.00M
                      lastYear.PlusDays 10, 5.123456M, None ]
                let returnedLine = returned |> List.exactlyOne |> _.snapshot.lines |> List.exactlyOne
                Assert.Equal<(LocalDate * decimal * decimal option) list>(expected, returnedLine.lots |> List.map lotOf)
                let! fetched = fetchSnapshot name p.d1
                Assert.Equal<(LocalDate * decimal * decimal option) list>(
                    expected, (fetched.lines |> List.exactlyOne).lots |> List.map lotOf)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-8.3 the Holding FetchAsOf route returns each line's lots in the order recorded, with each acquired date, quantity and cost basis as recorded`` () =
        let lastYear = p.accountsActiveBegin
        result {
            let! returned =
                call<C.HoldingFetchAsOfInput, C.HoldingsAsOfAccountReturn list> "Holding" "FetchAsOf" { asOf = p.monthEnd3 }
            let joint = returned |> List.find (fun a -> a.accountName = PF.jointBrokerage)
            Assert.Equal<(string * (LocalDate * decimal * decimal option) list) list>(
                [ PF.totalMarket,
                  [ lastYear.PlusDays(60), 19.999998M, Some 2000.00M
                    lastYear.PlusDays(30), 15.000001M, Some 1500.00M
                    lastYear.PlusDays(30), 15.000001M, Some 1500.00M ] ],
                joint.lines |> List.map (fun l -> l.securityName, l.lots |> List.map lotOf))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.1 REQ-POS-13.4 an InvestmentActivity Record payload with ranges for two accounts records both and returns, for each account and range, a removed count equal to the activities it replaced and a recorded count equal to the activities sent`` () =
        let first = unique "Route Activity Account"
        let second = unique "Route Activity Account"
        try
            result {
                do! accountHoldingTotalMarket first
                do! accountHoldingTotalMarket second
                let! _ = recordActivity [ rangeOf first p.d1 p.d2 [ interest p.d1 1.00M; interest p.d2 2.00M ] ]
                let! returned =
                    recordActivity
                        [ rangeOf first p.d1 p.d2 [ interest p.d1 3.00M ]
                          rangeOf second p.d1 p.d2 [ interest p.d1 1.00M; interest p.d1 2.00M; interest p.d2 3.00M ] ]
                Assert.Equal<(string * LocalDate * LocalDate * int * int) list>(
                    [ first, p.d1, p.d2, 2, 1; second, p.d1, p.d2, 0, 3 ],
                    returned |> List.map (fun r -> r.accountName, r.beginDate, r.endDate, r.removed, r.recorded))
                let! firstStored = listActivity first p.d1 p.d2
                let! secondStored = listActivity second p.d1 p.d2
                Assert.Equal((1, 3), (firstStored.Length, secondStored.Length))
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpInvestmentAccountByName first); (fun () -> cleanUpInvestmentAccountByName second) ]

    [<Fact>]
    member _.``REQ-POS-13.5 REQ-POS-12.9 an InvestmentActivity List payload returns the activities in the range in the order they were sent, each six-decimal quantity and price exactly as sent`` () =
        let name = unique "Route Activity Account"
        try
            result {
                do! accountHoldingTotalMarket name
                let sent =
                    [ activity p.d2 "Sale" "Sold" (Some PF.totalMarket) (Some 0.654321M) (Some 101.123456M) 66.17M
                      activity p.d2 "Purchase" "Bought" (Some PF.totalMarket) (Some 1.123456M) (Some 9.876543M) 11.10M
                      activity p.d1 "Dividend" "Dividend paid" (Some PF.totalMarket) None None 4.00M ]
                let! _ = recordActivity [ rangeOf name p.d1 p.d2 sent ]
                let! listed = listActivity name p.d1 p.d2
                Assert.Equal<(LocalDate * string * string * string option * decimal option * decimal option * decimal) list>(
                    [ p.d1, "Dividend", "Dividend paid", Some PF.totalMarket, None, None, 4.00M
                      p.d2, "Sale", "Sold", Some PF.totalMarket, Some 0.654321M, Some 101.123456M, 66.17M
                      p.d2, "Purchase", "Bought", Some PF.totalMarket, Some 1.123456M, Some 9.876543M, 11.10M ],
                    listed |> List.map activityOf)
                Assert.All(listed, fun a -> Assert.Equal(name, a.accountName))
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-13.1 an InvestmentActivity Record payload whose second account's activity is invalid records nothing for the first account, as read back after the route returns`` () =
        let first = unique "Route Activity Account"
        let second = unique "Route Activity Account"
        try
            result {
                do! accountHoldingTotalMarket first
                do! accountHoldingTotalMarket second
                recordActivity
                    [ rangeOf first p.d1 p.d2 [ interest p.d1 1.00M ]
                      rangeOf second p.d1 p.d2 [ activity p.d1 "Purchase" "Bought" (Some PF.totalMarket) None None 10.00M ] ]
                |> expectError
                    (function AsError (PositionsActivityShapeInvalid (a, d, k, problem)) -> Some(a, d, k, problem) | _ -> None)
                    (fun found -> Assert.Equal((second, p.d1, "Purchase", QuantityMissing), found))
                let! firstStored = listActivity first p.d1 p.d2
                Assert.Empty(firstStored)
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpInvestmentAccountByName first); (fun () -> cleanUpInvestmentAccountByName second) ]

    [<Fact>]
    member _.``REQ-POS-12.2 an InvestmentActivity Record payload with the kind 'purchase' is rejected with a typed error naming the text given, and nothing is recorded`` () =
        let name = unique "Route Activity Account"
        try
            result {
                do! accountHoldingTotalMarket name
                recordActivity
                    [ rangeOf name p.d1 p.d1 [ activity p.d1 "purchase" "Bought" (Some PF.totalMarket) (Some 1M) None 100.00M ] ]
                |> expectError
                    (function AsError (PositionsInvalidActivityKind text) -> Some text | _ -> None)
                    (fun text -> Assert.Equal("purchase", text))
                let! stored = listActivity name p.d1 p.d1
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-12.1 REQ-POS-11.9 an InvestmentActivity Record payload naming an account that matches no Investment Account fails with a typed not-found error naming the kind of record and the name, and nothing is recorded`` () =
        let missing = unique "Missing Account"
        let name = unique "Route Activity Account"
        try
            result {
                do! accountHoldingTotalMarket name
                recordActivity [ rangeOf name p.d1 p.d1 [ interest p.d1 1.00M ]; rangeOf missing p.d1 p.d1 [ interest p.d1 1.00M ] ]
                |> expectError
                    (function AsError (PositionsInvestmentAccountNameDoesntMatch n) -> Some n | _ -> None)
                    (fun n -> Assert.Equal(missing, n))
                let! stored = listActivity name p.d1 p.d1
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpInvestmentAccountByName name ]

    [<Fact>]
    member _.``REQ-POS-14.1 an InvestmentAccount RollForward payload for the non-balancing account returns the same rows, including each non-zero difference, as the roll-forward computation for that account and dates`` () =
        result {
            let! returned =
                call<C.InvestmentAccountRollForwardInput, C.RollForwardReturn> "InvestmentAccount" "RollForward"
                    { accountName = PF.alexBrokerage; firstDate = p.d3; secondDate = p.d4 }
            let! accountId = PositionsLookups.investmentAccountIdOf (fresh ()) PF.alexBrokerage
            let! computed = rollForward (fresh ()) accountId p.d3 p.d4
            let fromComputed =
                computed.rows
                |> List.map (fun r -> r.securityName, r.startQuantity, r.unitsIn, r.unitsOut, r.expected, r.endQuantity, r.difference)
            let fromRoute =
                returned.rows
                |> List.map (fun r -> r.securityName, r.startQuantity, r.unitsIn, r.unitsOut, r.expected, r.endQuantity, r.difference)
            Assert.Equal((PF.alexBrokerage, p.d3, p.d4), (returned.accountName, returned.firstDate, returned.secondDate))
            Assert.Equal<(string * decimal * decimal * decimal * decimal * decimal * decimal) list>(fromComputed, fromRoute)
            Assert.Equal<decimal list>([ -20M; 15M ], fromRoute |> List.map (fun (_, _, _, _, _, _, d) -> d))
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-15.4 REQ-NGUI-1.6 a PreLedgerBalance Record payload records each balance and returns each with its account code, the account's name beside it, the date, the balance and whether it replaced one`` () =
        try
            result {
                let! _ = recordBalances [ "F-1275", p.monthEnd6, 10.00M ]
                let! returned = recordBalances [ "F-1275", p.monthEnd6, 11.00M; "F-2230", p.monthEnd6, -2.50M ]
                Assert.Equal<(C.LedgerAccountReturn * LocalDate * decimal * bool) list>(
                    [ ledger "F-1275" "Fixture Positions Cash", p.monthEnd6, 11.00M, true
                      ledger "F-2230" "Fixture Loan Payable", p.monthEnd6, -2.50M, false ],
                    returned |> List.map (fun r -> r.balance.ledgerAccount, r.balance.balanceDate, r.balance.balance, r.replacedExisting))
                let! stored = listBalances None p.monthEnd6 p.monthEnd6
                Assert.Equal<(C.LedgerAccountReturn * LocalDate * decimal) list>(
                    [ ledger "F-1275" "Fixture Positions Cash", p.monthEnd6, 11.00M
                      ledger "F-2230" "Fixture Loan Payable", p.monthEnd6, -2.50M ],
                    stored |> List.map balanceOf)
            }
            |> railroadWrapper
        finally
            cleanUp [ (fun () -> cleanUpPreLedgerBalance "F-1275" p.monthEnd6); (fun () -> cleanUpPreLedgerBalance "F-2230" p.monthEnd6) ]

    [<Fact>]
    member _.``REQ-POS-15.4 REQ-NGUI-1.5 a PreLedgerBalance Record payload whose account code matches no account fails with a typed error naming the code, and nothing is recorded`` () =
        try
            result {
                recordBalances [ "F-1275", p.monthEnd6, 10.00M; "F-1998", p.monthEnd6, 10.00M ]
                |> expectError codeNotFound (fun code -> Assert.Equal("F-1998", code))
                let! stored = listBalances None p.monthEnd6 p.monthEnd6
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPreLedgerBalance "F-1275" p.monthEnd6 ]

    [<Fact>]
    member _.``REQ-POS-15.4 a PreLedgerBalance Record payload whose second balance is invalid records neither, as read back after the route returns`` () =
        try
            result {
                recordBalances [ "F-1275", p.monthEnd6, 10.00M; "F-3040", p.monthEnd6, 10.00M ]
                |> expectError
                    (function AsError (PositionsPreLedgerAccountTypeNotAllowed (c, t)) -> Some(c, t) | _ -> None)
                    (fun found -> Assert.Equal(("F-3040", "Equity"), found))
                let! stored = listBalances None p.monthEnd6 p.monthEnd6
                Assert.Empty(stored)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPreLedgerBalance "F-1275" p.monthEnd6 ]

    [<Fact>]
    member _.``REQ-POS-15.5 a PreLedgerBalance Delete payload deletes the balance for the account and date given, after which the PreLedgerBalance List route no longer returns it and still returns the account's other balances`` () =
        try
            result {
                let! _ = recordBalances [ "F-1275", p.monthEnd6, 10.00M ]
                let! deleted =
                    call<C.PreLedgerBalanceDeleteInput, C.PreLedgerBalanceReturn> "PreLedgerBalance" "Delete"
                        { accountCode = "F-1275"; balanceDate = p.monthEnd6 }
                Assert.Equal((ledger "F-1275" "Fixture Positions Cash", p.monthEnd6, 10.00M), balanceOf deleted)
                let! stored = listBalances (Some "F-1275") p.monthEnd8 p.monthEnd6
                Assert.Equal<(C.LedgerAccountReturn * LocalDate * decimal) list>(
                    [ ledger "F-1275" "Fixture Positions Cash", p.monthEnd8, 2000.00M
                      ledger "F-1275" "Fixture Positions Cash", p.monthEnd7, 2500.00M ],
                    stored |> List.map balanceOf)
            }
            |> railroadWrapper
        finally
            cleanUp [ fun () -> cleanUpPreLedgerBalance "F-1275" p.monthEnd6 ]

    [<Fact>]
    member _.``REQ-POS-15.6 REQ-NGUI-1.6 the PreLedgerBalance List route with and without an account filter returns the balances in the range ordered by account code then date, each account's name beside its code`` () =
        let mortgage = ledger "F-2320" "Fixture Rental Mortgage"
        result {
            let! filtered = listBalances (Some "F-2320") p.monthEnd9 p.monthEnd7
            Assert.Equal<(C.LedgerAccountReturn * LocalDate * decimal) list>(
                [ mortgage, p.monthEnd9, 186000.00M; mortgage, p.monthEnd8, 185000.00M; mortgage, p.monthEnd7, 184000.00M ],
                filtered |> List.map balanceOf)
            let! all = listBalances None p.monthEnd8 p.monthEnd7
            Assert.Equal<(C.LedgerAccountReturn * LocalDate * decimal) list>(
                [ ledger "F-1000" "Assets", p.monthEnd8, 100.00M
                  ledger "F-1275" "Fixture Positions Cash", p.monthEnd8, 2000.00M
                  ledger "F-1275" "Fixture Positions Cash", p.monthEnd7, 2500.00M
                  ledger "F-1280" "Fixture Operating Cash", p.monthEnd8, -15.00M
                  ledger "F-2230" "Fixture Loan Payable", p.monthEnd8, 1500.00M
                  ledger "F-2230" "Fixture Loan Payable", p.monthEnd7, 0.00M
                  mortgage, p.monthEnd8, 185000.00M
                  mortgage, p.monthEnd7, 184000.00M ],
                all |> List.map balanceOf)
        }
        |> railroadWrapper
