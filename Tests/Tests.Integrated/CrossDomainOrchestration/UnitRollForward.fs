module Tests.Integrated.CrossDomainOrchestration.UnitRollForward

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.UnitRollForward
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

(* Every expected figure below is derived by hand from the fixture (see PositionsFixture.fs).

   Sam 401k, d1 to d3. The window is the day after d1 up to and including d3.
     Example Bond Fund        start 200; in 10 (RolloverIn) + 5 (Purchase) + 0.123456 (Reinvestment) = 15.123456;
                              out 2 (RolloverOut) + 3 (Sale) = 5; expected 210.123456; end 210.123456; difference 0
     Example Total Market     start 20; in 2 (Contribution) + 1 (TransferIn) + 3 (AdjustmentIn) + 0.25 (two
                              Reinvestments of 0.125) = 6.25; out 1 (Withdrawal) + 1.5 (TransferOut) + 3 (AdjustmentOut)
                              + 0.25 (Fee, on d3) = 5.75; expected 20.5; end 20.5; difference 0
     Not counted: the Purchases of 7 on d1 - 1 and 4 on d1, and the Sale of 6 on d3 + 1.
   Alex Brokerage, d3 to d4:
     Example International    start 20; no units move (its Dividend moves none); expected 20; end 0 (not on d4);
                              difference -20
     Example Total Market     start 12; out 15 (Sale); expected -3; end 12; difference 15 *)

/// A row: Security name, start quantity, units in, units out, expected, end quantity and difference.
type private Row = string * decimal * decimal * decimal * decimal * decimal * decimal

let private rowOf (r: RollForwardRow) : Row =
    r.securityName, r.startQuantity, r.unitsIn, r.unitsOut, r.expected, r.endQuantity, r.difference

let private rollForwardRows context account first second =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> rollForward context accountId first second)
    |> Result.map (fun r -> r.rows |> List.map rowOf)

let private activity date kind security quantity amount : InvestmentActivityInput =
    { activityDate = date
      kind = kind
      description = $"{kind} in the window"
      source = None
      securityName = security
      quantity = quantity
      price = None
      amount = amount }

let private recordRange context account (beginDate: LocalDate) (endDate: LocalDate) activities =
    { accountName = account; beginDate = beginDate; endDate = endDate; activities = activities }
    |> ``convert [InvestmentActivityRangeInput] to [ActivityRange]`` context
    |> Result.bind (fun range -> InvestmentActivityOrchestration.recordActivity context [ range ])

let private recordSnapshot context account date (lines: (string * decimal * decimal * decimal) list) =
    { accountName = account
      snapshotDate = date
      provenance = "Reported"
      contributionBasis = None
      lines =
        lines
        |> List.map (fun (security, q, price, mv) ->
            { securityName = security; quantity = q; price = price; marketValue = mv; reportedCostBasis = None; lots = [] }) }
    |> ``convert [AccountSnapshotInput] to [Snapshot]`` context
    |> Result.bind (fun snapshot -> AccountSnapshotOrchestration.recordSnapshots context [ snapshot ])

let private addHolding context account security =
    result {
        let! accountId = PositionsLookups.investmentAccountIdOf context account
        let! securityId = PositionsLookups.securityIdOf context security
        return! HoldingOrchestration.constructNewAndPersist context accountId securityId None
    }

let private sam401kRows : Row list =
    [ PF.bondFund, 200M, 15.123456M, 5M, 210.123456M, 210.123456M, 0M
      PF.totalMarket, 20M, 6.25M, 5.75M, 20.5M, 20.5M, 0M ]

let private find name (rows: Row list) = rows |> List.find (fun (n, _, _, _, _, _, _) -> n = name)
let private unitsInOf name rows = let _, _, i, _, _, _, _ = find name rows in i
let private unitsOutOf name rows = let _, _, _, o, _, _, _ = find name rows in o

[<Collection("SharedTestData")>]
type UnitRollForwardTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    let day (anchor: LocalDate) (offset: int) = anchor.PlusDays(offset)
    // no fixture activity of Sam 401k is dated here, between its d1 and d3 snapshots
    let freeDay () = day p.d1 20

    [<Fact>]
    member _.``REQ-POS-14.1 rolling forward with the first date equal to the second, and with the first date later than the second, is each rejected with a typed error naming the account and both dates`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            [ p.d1, p.d1; p.d3, p.d1 ]
            |> List.iter (fun (first, second) ->
                rollForwardRows context PF.sam401k first second
                |> expectError
                    (function AsError (PositionsRollForwardDatesNotInOrder (a, f, s)) -> Some(a, f, s) | _ -> None)
                    (fun found -> Assert.Equal((PF.sam401k, first, second), found)))
            Ok())
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.1 rolling forward from a date with no snapshot of the account, and to a date with no snapshot of the account, including a date on which only another account has a snapshot, is each rejected with a typed error naming the account and that date`` () =
        // Sam 401k has snapshots on d1 and d3 only; Alex Brokerage and Joint Brokerage have one on d2
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            [ day p.d1 1, p.d3, day p.d1 1; p.d2, p.d3, p.d2; p.d1, p.d2, p.d2 ]
            |> List.iter (fun (first, second, missing) ->
                rollForwardRows context PF.sam401k first second
                |> expectError
                    (function AsError (PositionsSnapshotDoesntExist (a, d)) -> Some(a, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.sam401k, missing), found)))
            Ok())
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 the balancing account's roll-forward between its two snapshots gives every Security its start quantity, units in, units out, expected quantity and end quantity as derived by hand from the fixture, with a difference of zero on every row`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! rows = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal<Row list>(sam401kRows, rows)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 activity dated on the first snapshot date and the day before it is not counted, and activity dated the day after the first snapshot date is counted`` () =
        // counting the Purchases of 7 on d1 - 1 and 4 on d1 would make Total Market's units in 17.25, not 6.25;
        // removing the Contribution of 2 on d1 + 1 takes them to 4.25
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! before = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal(6.25M, before |> unitsInOf PF.totalMarket)
                let! _ = recordRange context PF.sam401k (day p.d1 1) (day p.d1 1) []
                let! after = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal(4.25M, after |> unitsInOf PF.totalMarket)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 activity dated on the second snapshot date and the day before it is counted, and activity dated the day after the second snapshot date is not counted`` () =
        // counting the Sale of 6 on d3 + 1 would make Total Market's units out 11.75, not 5.75. Removing the Fee of
        // 0.25 on d3 takes units out to 5.5; removing the two Reinvestments of 0.125 on d3 - 1 takes units in to 6.0.
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! before = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal(5.75M, before |> unitsOutOf PF.totalMarket)
                let! _ = recordRange context PF.sam401k (day p.d3 -1) p.d3 []
                let! after = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal((6.0M, 5.5M), (after |> unitsInOf PF.totalMarket, after |> unitsOutOf PF.totalMarket))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 a Security on the second snapshot and not the first has a row with start quantity 0, and a Security on the first and not the second has a row with end quantity 0`` () =
        // Alex Brokerage's d4 holds Total Market 12 only; a new snapshot the day after holds International 5 only
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ = recordSnapshot context PF.alexBrokerage (day p.d4 1) [ PF.international, 5M, 25.00M, 125.00M ]
                let! rows = rollForwardRows context PF.alexBrokerage p.d4 (day p.d4 1)
                Assert.Equal<Row list>(
                    [ PF.international, 0M, 0M, 0M, 0M, 5M, 5M; PF.totalMarket, 12M, 0M, 0M, 12M, 0M, -12M ], rows)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 a Security on neither snapshot that a units-moving activity in the window names has a row with start and end quantity 0, its units in and out as the activities give, and the hand-derived difference`` () =
        // a Purchase of 3 and a Sale of 1: expected 0 + 3 - 1 = 2 against an end of 0, a difference of -2
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ = addHolding context PF.sam401k PF.international
                let! _ =
                    recordRange context PF.sam401k (freeDay ()) (freeDay ())
                        [ activity (freeDay ()) "Purchase" (Some PF.international) (Some 3M) 75.00M
                          activity (freeDay ()) "Sale" (Some PF.international) (Some 1M) 25.00M ]
                let! rows = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal<Row list>(
                    [ sam401kRows.[0]; (PF.international, 0M, 3M, 1M, 2M, 0M, -2M); sam401kRows.[1] ], rows)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 REQ-POS-12.2 every units-in activity's quantity is totalled under units in and every units-out activity's quantity under units out, each against the Security it names`` () =
        // the window's activity replaced: the six units-in kinds on Total Market and the six units-out kinds on the
        // Bond Fund, each with its own power of two, so any kind counted in the wrong direction or not at all changes
        // a total to a figure no other mistake gives
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let on offset kind security q = activity (day p.d1 offset) kind (Some security) (Some q) 0.00M
                let! _ =
                    recordRange context PF.sam401k (day p.d1 1) p.d3
                        [ on 1 "Contribution" PF.totalMarket 1M
                          on 2 "RolloverIn" PF.totalMarket 2M
                          on 3 "TransferIn" PF.totalMarket 4M
                          on 4 "Purchase" PF.totalMarket 8M
                          on 5 "Reinvestment" PF.totalMarket 16M
                          on 6 "AdjustmentIn" PF.totalMarket 32M
                          on 7 "Withdrawal" PF.bondFund 1M
                          on 8 "RolloverOut" PF.bondFund 2M
                          on 9 "TransferOut" PF.bondFund 4M
                          on 10 "Sale" PF.bondFund 8M
                          on 11 "Fee" PF.bondFund 16M
                          on 12 "AdjustmentOut" PF.bondFund 32M ]
                let! rows = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal<(decimal * decimal) list>(
                    [ 0M, 63M; 63M, 0M ],
                    [ rows |> unitsInOf PF.bondFund, rows |> unitsOutOf PF.bondFund
                      rows |> unitsInOf PF.totalMarket, rows |> unitsOutOf PF.totalMarket ])
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 a Security named in the window only by a Dividend, Interest or CapitalGainDistribution, and on neither snapshot, has no row`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ = addHolding context PF.sam401k PF.international
                let! _ =
                    recordRange context PF.sam401k (freeDay ()) (freeDay ())
                        [ activity (freeDay ()) "Dividend" (Some PF.international) None 4.00M
                          activity (freeDay ()) "Interest" (Some PF.international) None 0.50M
                          activity (freeDay ()) "CapitalGainDistribution" (Some PF.international) None 2.00M ]
                let! rows = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal<Row list>(sam401kRows, rows)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 a Contribution naming no Security leaves every row's units in unchanged`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ =
                    recordRange context PF.sam401k (freeDay ()) (freeDay ())
                        [ activity (freeDay ()) "Contribution" None None 500.00M ]
                let! rows = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal<Row list>(sam401kRows, rows)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 rows for Securities whose snapshot lines and activities were recorded out of name order are returned ordered by Security name`` () =
        // Total Market's line and activity are recorded before International's
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ =
                    recordSnapshot context PF.alexBrokerage (day p.d4 2)
                        [ PF.totalMarket, 13M, 115.00M, 1495.00M; PF.international, 4M, 25.00M, 100.00M ]
                let! _ =
                    recordRange context PF.alexBrokerage (day p.d4 1) (day p.d4 1)
                        [ activity (day p.d4 1) "Purchase" (Some PF.totalMarket) (Some 1M) 115.00M
                          activity (day p.d4 1) "Purchase" (Some PF.international) (Some 4M) 100.00M ]
                let! rows = rollForwardRows context PF.alexBrokerage p.d4 (day p.d4 2)
                Assert.Equal<string list>(
                    [ PF.international; PF.totalMarket ], rows |> List.map (fun (n, _, _, _, _, _, _) -> n))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 REQ-POS-14.3 the non-balancing account's roll-forward returns, as data and not as an error, the hand-derived non-zero difference for each Security whose activity is missing`` () =
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! rows = rollForwardRows context PF.alexBrokerage p.d3 p.d4
                Assert.Equal<Row list>(
                    [ PF.international, 20M, 0M, 0M, 20M, 0M, -20M; PF.totalMarket, 12M, 0M, 15M, -3M, 12M, 15M ], rows)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-14.2 a Security whose units out exceed its start quantity plus units in has the negative expected quantity derived by hand`` () =
        // a Sale of 300 more Bond Fund units: expected 200 + 15.123456 - 305 = -89.876544 against 210.123456
        runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity (fun context ->
            result {
                let! _ =
                    recordRange context PF.sam401k (freeDay ()) (freeDay ())
                        [ activity (freeDay ()) "Sale" (Some PF.bondFund) (Some 300M) 3000.00M ]
                let! rows = rollForwardRows context PF.sam401k p.d1 p.d3
                Assert.Equal<Row>(
                    (PF.bondFund, 200M, 15.123456M, 305M, -89.876544M, 210.123456M, 300M), rows |> find PF.bondFund)
            })
        |> railroadWrapper
