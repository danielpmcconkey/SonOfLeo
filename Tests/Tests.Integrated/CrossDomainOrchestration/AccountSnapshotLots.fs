module Tests.Integrated.CrossDomainOrchestration.AccountSnapshotLots

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.AccountSnapshotOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// A lot as stored: acquired date, quantity and reported cost basis.
type private LotSummary = LocalDate * decimal * decimal option

/// Each line's Security name and lots, lines in Security-name order and lots in stored order.
type private LotsSummary = (string * LotSummary list) list

let private lotSummary (lot: AccountSnapshotLot.AccountSnapshotLot) : LotSummary =
    lot |> AccountSnapshotLot.acquiredDate,
    lot |> AccountSnapshotLot.quantity |> Quantity.amount,
    lot |> AccountSnapshotLot.reportedCostBasis |> Option.map Money.amount

let private lotsOf (view: SnapshotView) : LotsSummary =
    view.lines |> List.map (fun l -> l.securityName, l.lots |> List.map lotSummary)

let private lot (acquired: LocalDate) (q: decimal) (cost: decimal option) : AccountSnapshotLotInput =
    { acquiredDate = acquired; quantity = q; reportedCostBasis = cost }

let private line security (q: decimal) (p: decimal) (mv: decimal) (cb: decimal option) lots : AccountSnapshotLineInput =
    { securityName = security; quantity = q; price = p; marketValue = mv; reportedCostBasis = cb; lots = lots }

let private snapshot account date lines : AccountSnapshotInput =
    { accountName = account
      snapshotDate = date
      provenance = "Reported"
      contributionBasis = None
      lines = lines }

let private recordSnapshots context (inputs: AccountSnapshotInput list) =
    inputs
    |> List.map (``convert [AccountSnapshotInput] to [Snapshot]`` context)
    |> convertListOfResultsToResultsList
    |> Result.bind (AccountSnapshotOrchestration.recordSnapshots context)

let private fetchSnapshot context account date =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> AccountSnapshotOrchestration.fetchSnapshot context accountId date)

let private storedLots context account date = fetchSnapshot context account date |> Result.map lotsOf

let private lotsDontSum =
    function
    | AsError(PositionsLotsDontSumToLineQuantity(a, d, s, sum, q)) -> Some(a, d, s, sum, q)
    | _ -> None

[<Collection("SharedTestData")>]
type AccountSnapshotLotsTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    let lastYear = p.accountsActiveBegin
    // Alex Brokerage has no snapshot on this date in the fixture.
    let freshDate () = p.d1.PlusDays(1)

    /// The fixture's Joint Brokerage d2 lots, in the order the fixture supplied them.
    let fixtureJointLots : LotSummary list =
        [ lastYear.PlusDays(60), 19.999998M, Some 2000.00M
          lastYear.PlusDays(30), 15.000001M, Some 1500.00M
          lastYear.PlusDays(30), 15.000001M, Some 1500.00M ]

    [<Fact>]
    member _.``REQ-POS-6.9 REQ-POS-7.3 a Taxable account's snapshot whose lines carry lots is recorded, and both the returned snapshot and a later fetch give each line's lots with their acquired date, quantity and reported cost basis exactly as supplied`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let date = freshDate ()
                let! recorded =
                    recordSnapshots context
                        [ snapshot PF.alexBrokerage date
                              [ line PF.totalMarket 10M 100.00M 1000.00M (Some 900.00M)
                                    [ lot (lastYear.PlusDays 10) 4.25M (Some 380.50M); lot (lastYear.PlusDays 20) 5.75M None ]
                                line PF.international 20M 25.00M 500.00M (Some 480.00M)
                                    [ lot (lastYear.PlusDays 5) 20M (Some 0.00M) ] ] ]
                let expected : LotsSummary =
                    [ PF.international, [ lastYear.PlusDays 5, 20M, Some 0.00M ]
                      PF.totalMarket, [ lastYear.PlusDays 10, 4.25M, Some 380.50M; lastYear.PlusDays 20, 5.75M, None ] ]
                Assert.Equal<LotsSummary>(expected, (recorded |> List.exactlyOne).snapshot |> lotsOf)
                let! stored = storedLots context PF.alexBrokerage date
                Assert.Equal<LotsSummary>(expected, stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.9 for each of TaxDeferred, Roth and Hsa, a snapshot whose line carries one lot is rejected with a typed error naming the account and the security, and the account's stored snapshot for that date is unchanged`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            for account, security in [ PF.sam401k, PF.totalMarket; PF.alexRoth, PF.totalMarket; PF.samHsa, PF.stableValue ] do
                let before = fetchSnapshot context account p.d1 |> mustBe
                recordSnapshots context
                    [ snapshot account p.d1 [ line security 1M 10.00M 10.00M None [ lot lastYear 1M None ] ] ]
                |> expectError
                    (function AsError(PositionsLotsNotAllowed(a, s)) -> Some(a, s) | _ -> None)
                    (fun found -> Assert.Equal((account, security), found))
                let after = fetchSnapshot context account p.d1 |> mustBe
                Assert.Equal(before, after)
            Ok())
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.10 a Taxable account's snapshot with lots on one line and none on another is recorded, and the line recorded without lots is fetched back with no lots`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let date = freshDate ()
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.alexBrokerage date
                              [ line PF.totalMarket 10M 100.00M 1000.00M None [ lot lastYear 10M None ]
                                line PF.international 20M 25.00M 500.00M None [] ] ]
                let! stored = storedLots context PF.alexBrokerage date
                Assert.Equal<LotsSummary>([ PF.international, []; PF.totalMarket, [ lastYear, 10M, None ] ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.10 a Taxable account's later snapshot recorded with no lots for a line whose earlier snapshot carried lots is fetched back with no lots, while the earlier snapshot still has its lots`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.jointBrokerage p.d3 [ line PF.totalMarket 50M 105.00M 5250.00M (Some 5000.00M) [] ] ]
                let! later = storedLots context PF.jointBrokerage p.d3
                Assert.Equal<LotsSummary>([ PF.totalMarket, [] ], later)
                let! earlier = storedLots context PF.jointBrokerage p.d2
                Assert.Equal<LotsSummary>([ PF.totalMarket, fixtureJointLots ], earlier)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.12 recording a snapshot whose line's lots sum to 0.000001 less than its quantity fails with the lot-sum error and the account's stored snapshot for that date keeps its lines and lots unchanged`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! before = fetchSnapshot context PF.jointBrokerage p.d2
                recordSnapshots context
                    [ snapshot PF.jointBrokerage p.d2
                          [ line PF.totalMarket 50M 110.00M 5500.00M (Some 5000.00M)
                                [ lot lastYear 25M None; lot lastYear 24.999999M None ] ] ]
                |> expectError lotsDontSum (fun found ->
                    Assert.Equal((PF.jointBrokerage, p.d2, PF.totalMarket, 49.999999M, 50M), found))
                let! after = fetchSnapshot context PF.jointBrokerage p.d2
                Assert.Equal(before, after)
                Assert.Equal<LotsSummary>([ PF.totalMarket, fixtureJointLots ], after |> lotsOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.13 two lots of one line identical in acquired date, quantity and cost basis are both recorded and both fetched back`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let date = freshDate ()
                let twin = lot (lastYear.PlusDays 40) 5M (Some 500.00M)
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.alexBrokerage date [ line PF.totalMarket 10M 100.00M 1000.00M None [ twin; twin ] ] ]
                let! stored = storedLots context PF.alexBrokerage date
                Assert.Equal<LotsSummary>(
                    [ PF.totalMarket, [ lastYear.PlusDays 40, 5M, Some 500.00M; lastYear.PlusDays 40, 5M, Some 500.00M ] ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.13 a line's lots supplied out of acquired-date and quantity order are fetched back in exactly the order supplied`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let date = freshDate ()
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.alexBrokerage date
                              [ line PF.totalMarket 10M 100.00M 1000.00M None
                                    [ lot (lastYear.PlusDays 50) 3M None
                                      lot (lastYear.PlusDays 10) 5M None
                                      lot (lastYear.PlusDays 30) 2M None ] ] ]
                let! stored = storedLots context PF.alexBrokerage date
                Assert.Equal<LotsSummary>(
                    [ PF.totalMarket,
                      [ lastYear.PlusDays 50, 3M, None; lastYear.PlusDays 10, 5M, None; lastYear.PlusDays 30, 2M, None ] ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.2 re-recording a snapshot whose line carried lots, with different lots on that line, leaves exactly the new lots on it in the order supplied and none of the old`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.jointBrokerage p.d2
                              [ line PF.totalMarket 50M 110.00M 5500.00M (Some 5000.00M)
                                    [ lot (lastYear.PlusDays 90) 25M (Some 2600.00M); lot (lastYear.PlusDays 80) 25M None ] ] ]
                let! stored = storedLots context PF.jointBrokerage p.d2
                Assert.Equal<LotsSummary>(
                    [ PF.totalMarket, [ lastYear.PlusDays 90, 25M, Some 2600.00M; lastYear.PlusDays 80, 25M, None ] ],
                    stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.2 re-recording a snapshot whose line carried lots, with that line carrying none, leaves the line with no lots`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.jointBrokerage p.d2 [ line PF.totalMarket 50M 110.00M 5500.00M (Some 5000.00M) [] ] ]
                let! stored = storedLots context PF.jointBrokerage p.d2
                Assert.Equal<LotsSummary>([ PF.totalMarket, [] ], stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.4 deleting the fixture's Taxable snapshot whose line carries lots returns it with those lots, after which neither the snapshot nor any of its lots can be fetched`` () =
        runCommandRouteAndAutoRollback PositionsDeleteAccountSnapshot (fun context ->
            result {
                let! accountId = PositionsLookups.investmentAccountIdOf context PF.jointBrokerage
                let! deleted = AccountSnapshotOrchestration.deleteSnapshot context accountId p.d2
                Assert.Equal<LotsSummary>([ PF.totalMarket, fixtureJointLots ], deleted |> lotsOf)
                fetchSnapshot context PF.jointBrokerage p.d2
                |> expectError
                    (function AsError(PositionsSnapshotDoesntExist(a, d)) -> Some(a, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.jointBrokerage, p.d2), found))
                let! remainingLots =
                    deleted.header |> AccountSnapshotHeader.accountSnapshotId |> AccountSnapshotLot.fetchByAccountSnapshot context
                Assert.Empty(remainingLots)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.5 fetching the fixture's Taxable snapshot gives each line's lots exactly as the fixture recorded them, including two identical lots in the order supplied`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! stored = storedLots context PF.jointBrokerage p.d2
                Assert.Equal<LotsSummary>([ PF.totalMarket, fixtureJointLots ], stored)
            })
        |> railroadWrapper
