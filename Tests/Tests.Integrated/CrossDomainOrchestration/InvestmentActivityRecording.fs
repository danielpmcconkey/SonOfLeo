module Tests.Integrated.CrossDomainOrchestration.InvestmentActivityRecording

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsComponent
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.InvestmentActivityOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// An activity as listed: date, kind, description, source, Security name, quantity, price and amount.
type private ActivitySummary =
    LocalDate * string * string * string option * string option * decimal option * decimal option * decimal

let private summary (view: ActivityView) : ActivitySummary =
    let a = view.activity
    a |> InvestmentActivity.activityDate,
    a |> InvestmentActivity.kind |> ActivityKind.toString,
    a |> InvestmentActivity.description |> ActivityDescription.value,
    a |> InvestmentActivity.source |> Option.map ActivitySource.value,
    view.securityName,
    a |> InvestmentActivity.quantity |> Option.map Quantity.amount,
    a |> InvestmentActivity.price |> Option.map Price.amount,
    a |> InvestmentActivity.amount |> Money.amount

let private activity date kind description source security quantity price amount : InvestmentActivityInput =
    { activityDate = date
      kind = kind
      description = description
      source = source
      securityName = security
      quantity = quantity
      price = price
      amount = amount }

/// The input that records the summarised activity.
let private inputOf ((date, kind, description, source, security, quantity, price, amount): ActivitySummary) =
    activity date kind description source security quantity price amount

let private interest date (amount: decimal) = activity date "Interest" "Interest on cash" None None None None amount

let private range account beginDate endDate activities : InvestmentActivityRangeInput =
    { accountName = account; beginDate = beginDate; endDate = endDate; activities = activities }

// the route path: ranges arrive as input contracts and go through the route's converter, which resolves the account
// and Security names to IDs
let private record context (ranges: InvestmentActivityRangeInput list) =
    ranges
    |> List.map (``convert [InvestmentActivityRangeInput] to [ActivityRange]`` context)
    |> convertListOfResultsToResultsList
    |> Result.bind (recordActivity context)

let private listed context account beginDate endDate =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> listActivity context accountId beginDate endDate)
    |> Result.map (List.map summary)

let private today (context: Context.Context) = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

let private notHeld = function AsError (PositionsActivitySecurityNotHeld (a, s)) -> Some(a, s) | _ -> None

let private recordedSummary (r: RecordedRange) = r.investmentAccountName, r.beginDate, r.endDate, r.removed, r.recorded

[<Collection("SharedTestData")>]
type InvestmentActivityRecordingTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    let day (anchor: LocalDate) (offset: int) = anchor.PlusDays(offset)

    /// Sam 401k's activity as the fixture records it, in date order and, within a date, in the order supplied.
    let sam401kFixtureActivity : ActivitySummary list =
        [ day p.d1 -1, "Purchase", "Bought before the window", None, Some PF.totalMarket, Some 7M, Some 100.00M, 700.00M
          p.d1, "Purchase", "Bought on the first snapshot date", None, Some PF.totalMarket, Some 4M, Some 100.00M, 400.00M
          day p.d1 1, "Contribution", "Payroll contribution", Some "Employee deferral", Some PF.totalMarket, Some 2M, Some 100.00M, 200.00M
          day p.d1 2, "Contribution", "Employer contribution to cash", Some "Employer match", None, None, None, 150.00M
          day p.d1 3, "RolloverIn", "Rollover from a prior plan", None, Some PF.bondFund, Some 10M, None, 100.00M
          day p.d1 4, "TransferIn", "Transfer in of shares", None, Some PF.totalMarket, Some 1M, None, 100.00M
          day p.d1 5, "Purchase", "Exchange purchase", None, Some PF.bondFund, Some 5M, Some 10.00M, 50.00M
          day p.d1 6, "Reinvestment", "Interest reinvested", None, Some PF.bondFund, Some 0.123456M, Some 9.876543M, 1.22M
          day p.d1 7, "AdjustmentIn", "Share class conversion in", None, Some PF.totalMarket, Some 3M, None, 0.00M
          day p.d1 8, "Withdrawal", "Hardship withdrawal", None, Some PF.totalMarket, Some 1M, None, 100.00M
          day p.d1 9, "RolloverOut", "Rollover to another plan", None, Some PF.bondFund, Some 2M, None, 20.00M
          day p.d1 10, "TransferOut", "Transfer out of shares", None, Some PF.totalMarket, Some 1.5M, None, 150.00M
          day p.d1 11, "Sale", "Exchange sale", None, Some PF.bondFund, Some 3M, Some 10.00M, 30.00M
          day p.d1 13, "AdjustmentOut", "Share class conversion out", None, Some PF.totalMarket, Some 3M, None, 0.00M
          day p.d1 14, "Dividend", "Dividend paid", None, Some PF.totalMarket, None, None, 12.34M
          day p.d1 15, "Interest", "Interest on cash", None, None, None, None, 0.56M
          day p.d1 16, "CapitalGainDistribution", "Capital gain paid", None, Some PF.bondFund, None, None, 7.89M
          day p.d3 -1, "Reinvestment", "Dividend reinvested", None, Some PF.totalMarket, Some 0.125M, Some 105.00M, 13.13M
          day p.d3 -1, "Reinvestment", "Dividend reinvested", None, Some PF.totalMarket, Some 0.125M, Some 105.00M, 13.13M
          p.d3, "Fee", "Advisory fee", None, Some PF.totalMarket, Some 0.25M, None, 25.00M
          day p.d3 1, "Sale", "Sold after the window", None, Some PF.totalMarket, Some 6M, Some 105.00M, 630.00M ]

    /// Alex Brokerage's activity as the fixture records it, in its range d3 + 1 to d4.
    let alexBrokerageFixtureActivity : ActivitySummary list =
        [ day p.d3 2, "Sale", "Sale", None, Some PF.totalMarket, Some 15M, Some 105.00M, 1575.00M
          day p.d3 3, "Dividend", "Dividend paid", None, Some PF.international, None, None, 5.50M ]

    let byDate date (activities: ActivitySummary list) =
        activities |> List.filter (fun (d, _, _, _, _, _, _, _) -> d = date)

    [<Fact>]
    member _.``REQ-POS-12.1 REQ-POS-13.5 listing the fixture's activity account across its recorded range gives every activity, one of each of the fifteen kinds among them, with its date, kind, description, source, Security, quantity, price and amount exactly as the fixture recorded it, including activities with no source, no Security, no quantity and no price, which list back with each absent`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! stored = listed context PF.sam401k (day p.d1 -1) (day p.d3 1)
                Assert.Equal<ActivitySummary list>(sam401kFixtureActivity, stored)
                Assert.Equal(15, stored |> List.map (fun (_, kind, _, _, _, _, _, _) -> kind) |> List.distinct |> List.length)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-12.5 recording an activity naming a Security of which the account has no Holding fails with a typed error naming the account and the Security, and no Holding is created`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                record context
                    [ range PF.jordanCustodial p.d1 p.d1
                          [ activity p.d1 "Purchase" "Bought" None (Some PF.totalMarket) (Some 1M) (Some 100.00M) 100.00M ] ]
                |> expectError notHeld (fun found -> Assert.Equal((PF.jordanCustodial, PF.totalMarket), found))
                let! jordanId = PositionsLookups.investmentAccountIdOf context PF.jordanCustodial
                let! holdings = Holding.fetchByInvestmentAccount context jordanId
                Assert.Empty(holdings)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-12.5 recording an activity naming a Security held only in a different account fails with a typed error naming the account and the Security`` () =
        // the International fund is held by Alex Brokerage and Old Brokerage, not by Sam 401k
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            record context
                [ range PF.sam401k (day p.d1 20) (day p.d1 20)
                      [ activity (day p.d1 20) "Dividend" "Dividend paid" None (Some PF.international) None None 1.00M ] ]
            |> expectError notHeld (fun found -> Assert.Equal((PF.sam401k, PF.international), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-12.8 activities dated the day before the account's active begin and the day after its active end are each rejected with a typed error naming the account and the date, and activities dated on its active begin and on its active end are accepted`` () =
        // Old Brokerage is active from accountsActiveBegin to monthEnd4
        let activeBegin = p.accountsActiveBegin
        let activeEnd = p.monthEnd4
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            for outside in [ activeBegin.PlusDays(-1); activeEnd.PlusDays(1) ] do
                record context [ range PF.oldBrokerage outside outside [ interest outside 1.00M ] ]
                |> expectError
                    (function AsError (PositionsActivityDateOutsideActivePeriod (a, d)) -> Some(a, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.oldBrokerage, outside), found))
            result {
                let! _ = record context [ range PF.oldBrokerage activeBegin activeEnd [ interest activeBegin 1.00M; interest activeEnd 2.00M ] ]
                let! stored = listed context PF.oldBrokerage activeBegin activeEnd
                Assert.Equal<(LocalDate * decimal) list>(
                    [ activeBegin, 1.00M; activeEnd, 2.00M ],
                    stored |> List.map (fun (d, _, _, _, _, _, _, amount) -> d, amount))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.1 REQ-SYS-3.4 a range ending on the calendar date of the operation's initiation instant with an activity dated on it is accepted, and a range ending the day after is rejected with a typed error naming the account and the range`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let currentDate = today context
                let! _ = record context [ range PF.alexBrokerage currentDate currentDate [ interest currentDate 1.00M ] ]
                let! stored = listed context PF.alexBrokerage currentDate currentDate
                Assert.Equal(1, stored.Length)
                record context [ range PF.alexBrokerage currentDate (currentDate.PlusDays(1)) [] ]
                |> expectError
                    (function AsError (PositionsActivityRangeEndsAfterCurrentDate (a, b, e)) -> Some(a, b, e) | _ -> None)
                    (fun found -> Assert.Equal((PF.alexBrokerage, currentDate, currentDate.PlusDays(1)), found))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.2 recording a range replaces every activity of the account dated within it with those supplied, and leaves the account's activities dated the day before and the day after the range untouched`` () =
        // the fixture has one Sam 401k activity on each of d1 + 1 to d1 + 6; the range is d1 + 2 to d1 + 5
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let replacement = interest (day p.d1 3) 4.44M
                let! _ = record context [ range PF.sam401k (day p.d1 2) (day p.d1 5) [ replacement ] ]
                let! stored = listed context PF.sam401k (day p.d1 1) (day p.d1 6)
                Assert.Equal<ActivitySummary list>(
                    [ yield! sam401kFixtureActivity |> byDate (day p.d1 1)
                      day p.d1 3, "Interest", "Interest on cash", None, None, None, None, 4.44M
                      yield! sam401kFixtureActivity |> byDate (day p.d1 6) ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.2 recording an empty list for a range removes every activity of the account dated within it and leaves the activities outside the range`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ = record context [ range PF.sam401k (day p.d1 2) (day p.d1 5) [] ]
                let! stored = listed context PF.sam401k (day p.d1 1) (day p.d1 6)
                Assert.Equal<ActivitySummary list>(
                    (sam401kFixtureActivity |> byDate (day p.d1 1)) @ (sam401kFixtureActivity |> byDate (day p.d1 6)),
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.2 recording a range for one account leaves another account's activities on the same dates untouched`` () =
        // Alex Brokerage's range is d3 + 1 to d4; clearing Sam 401k over the same dates leaves Alex Brokerage's two
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ = record context [ range PF.sam401k (day p.d3 1) p.d4 [] ]
                let! alex = listed context PF.alexBrokerage (day p.d3 1) p.d4
                Assert.Equal<ActivitySummary list>(alexBrokerageFixtureActivity, alex)
                let! sam = listed context PF.sam401k (day p.d3 1) p.d4
                Assert.Empty(sam)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.2 REQ-POS-13.4 re-recording a range with activities identical to those stored succeeds with removed equal to recorded, and listing gives each activity once, not twice`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! recorded =
                    record context [ range PF.alexBrokerage (day p.d3 1) p.d4 (alexBrokerageFixtureActivity |> List.map inputOf) ]
                Assert.Equal((PF.alexBrokerage, day p.d3 1, p.d4, 2, 2), recorded |> List.exactlyOne |> recordedSummary)
                let! stored = listed context PF.alexBrokerage (day p.d3 1) p.d4
                Assert.Equal<ActivitySummary list>(alexBrokerageFixtureActivity, stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.4 recording two ranges returns for each its account and range, the number removed equal to the hand-counted activities that were dated in it, and the number recorded equal to the number supplied`` () =
        // Sam 401k has five activities dated d1 + 1 to d1 + 5; Alex Brokerage has two dated d3 + 1 to d4
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! recorded =
                    record context
                        [ range PF.sam401k (day p.d1 1) (day p.d1 5) [ interest (day p.d1 1) 1.00M ]
                          range PF.alexBrokerage (day p.d3 1) p.d4
                              [ interest (day p.d3 1) 1.00M; interest (day p.d3 2) 2.00M; interest p.d4 3.00M ] ]
                Assert.Equal<(string * LocalDate * LocalDate * int * int) list>(
                    [ PF.sam401k, day p.d1 1, day p.d1 5, 5, 1; PF.alexBrokerage, day p.d3 1, p.d4, 2, 3 ],
                    recorded |> List.map recordedSummary)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-12.9 two activities of one account on one date identical in every field are both recorded and both listed`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let twin = activity p.d4 "Dividend" "Dividend paid" None (Some PF.international) None None 2.50M
                let! _ = record context [ range PF.alexBrokerage p.d4 p.d4 [ twin; twin ] ]
                let! stored = listed context PF.alexBrokerage p.d4 p.d4
                let expected : ActivitySummary = p.d4, "Dividend", "Dividend paid", None, Some PF.international, None, None, 2.50M
                Assert.Equal<ActivitySummary list>([ expected; expected ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-12.9 activities of one account on one date are listed in the order supplied, not sorted by kind, description or amount`` () =
        // sorted by kind the order would be Dividend, Fee, Interest; by description Alpha, Middle, Zeta; by amount 1, 5, 9
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ =
                    record context
                        [ range PF.alexBrokerage p.d4 p.d4
                              [ activity p.d4 "Interest" "Zeta interest" None None None None 9.00M
                                activity p.d4 "Dividend" "Alpha dividend" None (Some PF.international) None None 1.00M
                                activity p.d4 "Fee" "Middle fee" None None None None 5.00M ] ]
                let! stored = listed context PF.alexBrokerage p.d4 p.d4
                Assert.Equal<(string * string * decimal) list>(
                    [ "Interest", "Zeta interest", 9.00M; "Dividend", "Alpha dividend", 1.00M; "Fee", "Middle fee", 5.00M ],
                    stored |> List.map (fun (_, kind, description, _, _, _, _, amount) -> kind, description, amount))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.5 listing an account's activities supplied out of date order between two dates returns every activity of that account dated in the range, both ends included, ordered by date, and none dated outside the range or belonging to another account`` () =
        let first = day p.d4 -5
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ =
                    record context
                        [ range PF.alexBrokerage (day p.d4 -6) p.d4
                              [ interest p.d4 4.00M; interest (day p.d4 -6) 1.00M; interest first 2.00M; interest (day p.d4 -2) 3.00M ]
                          range PF.sam401k first p.d4 [ interest (day p.d4 -3) 9.00M ] ]
                let! stored = listed context PF.alexBrokerage first p.d4
                Assert.Equal<(LocalDate * decimal) list>(
                    [ first, 2.00M; day p.d4 -2, 3.00M; p.d4, 4.00M ],
                    stored |> List.map (fun (d, _, _, _, _, _, _, amount) -> d, amount))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-13.5 listing an account's activities with the end date the day before the begin date fails with a typed error naming both dates`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            listed context PF.sam401k p.d3 (day p.d3 -1)
            |> expectError
                (function AsError (PositionsActivityListEndBeforeBegin (b, e)) -> Some(b, e) | _ -> None)
                (fun found -> Assert.Equal((p.d3, day p.d3 -1), found))
            |> Ok)
        |> railroadWrapper
