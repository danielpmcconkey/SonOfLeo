module Tests.Integrated.CrossDomainOrchestration.AccountSnapshotRecording

open System
open NodaTime
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
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
open Business.CrossDomainOrchestration.AccountSnapshotOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

/// A line as stored: Security name, quantity, price, market value and cost basis.
type private LineSummary = string * decimal * decimal * decimal * decimal option

/// A snapshot as stored: account, date, provenance, contribution basis and lines in Security-name order.
type private SnapshotSummary = string * LocalDate * Provenance * decimal option * LineSummary list

let private lineSummary (view: SnapshotLineView) : LineSummary =
    view.securityName,
    view.line |> AccountSnapshotLine.quantity |> Quantity.amount,
    view.line |> AccountSnapshotLine.price |> Price.amount,
    view.line |> AccountSnapshotLine.marketValue |> Money.amount,
    view.line |> AccountSnapshotLine.reportedCostBasis |> Option.map Money.amount

let private summary (view: SnapshotView) : SnapshotSummary =
    view.investmentAccountName,
    view.header |> AccountSnapshotHeader.snapshotDate,
    view.header |> AccountSnapshotHeader.provenance,
    view.header |> AccountSnapshotHeader.contributionBasis |> Option.map (ContributionBasis.value >> Money.amount),
    view.lines |> List.map lineSummary

let private line security (q: decimal) (p: decimal) (mv: decimal) (cb: decimal option) : AccountSnapshotLineInput =
    { securityName = security; quantity = q; price = p; marketValue = mv; reportedCostBasis = cb }

let private snapshot account date provenance (contribution: decimal option) lines : AccountSnapshotInput =
    { accountName = account
      snapshotDate = date
      provenance = provenance |> Provenance.toString
      contributionBasis = contribution
      lines = lines }

// the route path: snapshots arrive as input contracts and go through the route's converter, which resolves the account
// and Security names to IDs
let private recordSnapshots context (inputs: AccountSnapshotInput list) =
    inputs
    |> List.map (``convert [AccountSnapshotInput] to [Snapshot]`` context)
    |> convertListOfResultsToResultsList
    |> Result.bind (AccountSnapshotOrchestration.recordSnapshots context)

let private fetchSnapshot context account date =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> AccountSnapshotOrchestration.fetchSnapshot context accountId date)

let private deleteSnapshot context account date =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> AccountSnapshotOrchestration.deleteSnapshot context accountId date)

let private listSnapshotDates context account beginDate endDate =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> AccountSnapshotOrchestration.listSnapshotDates context accountId beginDate endDate)

let private fetched context account date =
    fetchSnapshot context account date |> Result.map summary

let private today (context: Context.Context) = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

let private outsideActivePeriod = function AsError (PositionsSnapshotDateOutsideActivePeriod (a, d)) -> Some(a, d) | _ -> None
let private notHeld = function AsError (PositionsSnapshotSecurityNotHeld (a, s)) -> Some(a, s) | _ -> None
let private notFound = function AsError (PositionsSnapshotDoesntExist (a, d)) -> Some(a, d) | _ -> None

[<Collection("SharedTestData")>]
type AccountSnapshotRecordingTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions

    [<Fact>]
    member _.``REQ-POS-6.1 REQ-POS-7.3 recording a new snapshot stores its date, provenance, contribution basis and every line, and returns it as stored, marked as not a replacement`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! recorded =
                    recordSnapshots context
                        [ snapshot PF.alexRoth p.d2 Imported (Some 1300.00M)
                              [ line PF.totalMarket 6M 110.00M 660.00M None
                                line PF.bondFund 100M 10.10M 1010.00M (Some 1000.00M) ] ]
                let expected : SnapshotSummary =
                    PF.alexRoth, p.d2, Imported, Some 1300.00M,
                    [ PF.bondFund, 100M, 10.10M, 1010.00M, Some 1000.00M
                      PF.totalMarket, 6M, 110.00M, 660.00M, None ]
                let only = recorded |> List.exactlyOne
                Assert.False(only.replacedExisting)
                Assert.Equal(expected, only.snapshot |> summary)
                let! stored = fetched context PF.alexRoth p.d2
                Assert.Equal(expected, stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.1 a snapshot with no lines is recorded, and fetching it gives back no lines`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ = recordSnapshots context [ snapshot PF.alexRoth p.d3 Reported None [] ]
                let! stored = fetched context PF.alexRoth p.d3
                Assert.Equal((PF.alexRoth, p.d3, Reported, None, []), stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.1 REQ-POS-7.1 one operation recording snapshots for two accounts stores both`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! recorded =
                    recordSnapshots context
                        [ snapshot PF.alexRoth p.d2 Reported None [ line PF.totalMarket 5M 110.00M 550.00M None ]
                          snapshot PF.samHsa p.d2 Reported None [ line PF.stableValue 310M 1.00M 310.00M None ] ]
                Assert.Equal(2, recorded.Length)
                let! roth = fetched context PF.alexRoth p.d2
                let! hsa = fetched context PF.samHsa p.d2
                Assert.Equal((PF.alexRoth, p.d2, Reported, None, [ PF.totalMarket, 5M, 110.00M, 550.00M, None ]), roth)
                Assert.Equal((PF.samHsa, p.d2, Reported, None, [ PF.stableValue, 310M, 1.00M, 310.00M, None ]), hsa)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.1 one operation naming the same account and date twice is rejected with a typed error naming the account and the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            recordSnapshots context
                [ snapshot PF.alexRoth p.d2 Reported None []
                  snapshot PF.samHsa p.d2 Reported None []
                  snapshot PF.alexRoth p.d2 Imported None [] ]
            |> expectError
                (function AsError (PositionsSnapshotRepeatedInRequest (a, d)) -> Some(a, d) | _ -> None)
                (fun found -> Assert.Equal((PF.alexRoth, p.d2), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.2 REQ-POS-7.3 recording a date that already has a snapshot replaces its provenance, contribution basis and every line, so a line present before and absent from the new snapshot is gone, and returns it marked as a replacement`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                // the fixture's Alex Roth IRA snapshot on d1: Reported, 1200.00, a Bond Fund line and a Total Market line
                let! recorded =
                    recordSnapshots context
                        [ snapshot PF.alexRoth p.d1 Imported (Some 1250.00M) [ line PF.bondFund 120M 10.00M 1200.00M None ] ]
                let expected : SnapshotSummary = PF.alexRoth, p.d1, Imported, Some 1250.00M, [ PF.bondFund, 120M, 10.00M, 1200.00M, None ]
                let only = recorded |> List.exactlyOne
                Assert.True(only.replacedExisting)
                Assert.Equal(expected, only.snapshot |> summary)
                let! stored = fetched context PF.alexRoth p.d1
                Assert.Equal(expected, stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.2 re-recording a date with figures identical to the stored snapshot succeeds and is marked as a replacement, not rejected as a no-op`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! recorded =
                    recordSnapshots context
                        [ snapshot PF.samHsa p.d1 Reported None [ line PF.stableValue 300M 1.00M 300.00M None ] ]
                Assert.True((recorded |> List.exactlyOne).replacedExisting)
                let! stored = fetched context PF.samHsa p.d1
                Assert.Equal((PF.samHsa, p.d1, Reported, None, [ PF.stableValue, 300M, 1.00M, 300.00M, None ]), stored)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.7 a line's six-decimal quantity and price, its market value and its cost basis are fetched back exactly as recorded, and a line recorded with no cost basis is fetched back with none`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                // 12.345678 x 98.765432 = 1219.326221002896, 0.003779 short of 1219.33
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.alexBrokerage (p.d2.PlusDays(1)) Reported None
                              [ line PF.totalMarket 12.345678M 98.765432M 1219.33M (Some 1100.01M)
                                line PF.international 1M 10.00M 10.00M None ] ]
                let! stored = fetched context PF.alexBrokerage (p.d2.PlusDays(1))
                let _, _, _, _, lines = stored
                Assert.Equal<LineSummary list>(
                    [ PF.international, 1M, 10.00M, 10.00M, None
                      PF.totalMarket, 12.345678M, 98.765432M, 1219.33M, Some 1100.01M ],
                    lines)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.3 a snapshot dated the day before the account's active begin, and one dated the day after its active end, are each rejected with a typed error naming the account and the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            let beforeBegin = p.accountsActiveBegin.PlusDays(-1)
            let afterEnd = p.monthEnd4.PlusDays(1)
            recordSnapshots context [ snapshot PF.oldBrokerage beforeBegin Reported None [] ]
            |> expectError outsideActivePeriod (fun found -> Assert.Equal((PF.oldBrokerage, beforeBegin), found))
            recordSnapshots context [ snapshot PF.oldBrokerage afterEnd Reported None [] ]
            |> expectError outsideActivePeriod (fun found -> Assert.Equal((PF.oldBrokerage, afterEnd), found))
            Ok())
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.3 snapshots dated on the account's active begin and on its active end are each accepted`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ =
                    recordSnapshots context
                        [ snapshot PF.oldBrokerage p.accountsActiveBegin Reported None []
                          snapshot PF.oldBrokerage p.monthEnd4 Reported None [] ]
                let! dates = listSnapshotDates context PF.oldBrokerage p.accountsActiveBegin p.monthEnd4
                Assert.Equal<(LocalDate * Provenance) list>(
                    [ p.accountsActiveBegin, Reported; p.d1, Reported; p.monthEnd4, Reported ], dates)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.3 REQ-SYS-3.4 a snapshot dated on the calendar date of the operation's initiation instant is accepted, and one dated the day after is rejected with a typed error naming the account and the date`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let currentDate = today context
                let! recorded = recordSnapshots context [ snapshot PF.samHsa currentDate Reported None [] ]
                Assert.Equal(currentDate, (recorded |> List.exactlyOne).snapshot.header |> AccountSnapshotHeader.snapshotDate)
                recordSnapshots context [ snapshot PF.samHsa (currentDate.PlusDays(1)) Reported None [] ]
                |> expectError
                    (function AsError (PositionsSnapshotDateLaterThanCurrentDate (a, d)) -> Some(a, d) | _ -> None)
                    (fun found -> Assert.Equal((PF.samHsa, currentDate.PlusDays(1)), found))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.4 a contribution basis on a Roth account's snapshot is stored, and for each of Taxable, TaxDeferred and Hsa, one on that account's snapshot is rejected with a typed error naming the account`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                let! _ = recordSnapshots context [ snapshot PF.alexRoth p.d2 Reported (Some 0.00M) [] ]
                let! stored = fetched context PF.alexRoth p.d2
                let _, _, _, contribution, _ = stored
                Assert.Equal(Some 0.00M, contribution)
                [ PF.alexBrokerage, "Taxable"; PF.sam401k, "TaxDeferred"; PF.samHsa, "Hsa" ]
                |> List.iter (fun (account, treatment) ->
                    recordSnapshots context [ snapshot account p.d2 Reported (Some 100.00M) [] ]
                    |> expectError
                        (function AsError (PositionsContributionBasisNotAllowed (a, t)) -> Some(a, t) | _ -> None)
                        (fun found -> Assert.Equal((account, treatment), found)))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.5 a line naming a Security of which the account has no Holding fails with a typed error naming the account and the Security, and no Holding is created`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            result {
                recordSnapshots context [ snapshot PF.samHsa p.d2 Reported None [ line PF.totalMarket 1M 10.00M 10.00M None ] ]
                |> expectError notHeld (fun found -> Assert.Equal((PF.samHsa, PF.totalMarket), found))
                let! samHsaId = PositionsLookups.investmentAccountIdOf context PF.samHsa
                let! holdings = HoldingOrchestration.listHoldings context (Some samHsaId)
                Assert.Equal<string list>([ PF.stableValue ], holdings |> List.map (fun h -> h.securityName))
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.5 a line naming a Security held only in a different account fails with a typed error naming the account and the Security`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            recordSnapshots context [ snapshot PF.jointBrokerage p.d3 Reported None [ line PF.bondFund 1M 10.00M 10.00M None ] ]
            |> expectError notHeld (fun found -> Assert.Equal((PF.jointBrokerage, PF.bondFund), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-6.6 a snapshot naming one Security on two lines is rejected with a typed error naming the Security`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            recordSnapshots context
                [ snapshot PF.alexBrokerage (p.d2.PlusDays(2)) Reported None
                      [ line PF.totalMarket 1M 10.00M 10.00M None; line PF.totalMarket 2M 10.00M 20.00M None ] ]
            |> expectError
                (function AsError (PositionsSnapshotSecurityRepeated (a, d, s)) -> Some(a, d, s) | _ -> None)
                (fun found -> Assert.Equal((PF.alexBrokerage, p.d2.PlusDays(2), PF.totalMarket), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-11.9 recording a snapshot for an account name that matches no account fails with a typed not-found error naming Investment Account and the name`` () =
        runCommandRouteAndAutoRollback PositionsRecordAccountSnapshots (fun context ->
            recordSnapshots context [ snapshot "Missing Brokerage" p.d2 Reported None [] ]
            |> expectError
                (function AsError (PositionsInvestmentAccountNameDoesntMatch n) -> Some n | _ -> None)
                (fun n -> Assert.Equal("Missing Brokerage", n))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.4 deleting the snapshot for an account and date removes it and its lines, and leaves the account's other snapshots`` () =
        runCommandRouteAndAutoRollback PositionsDeleteAccountSnapshot (fun context ->
            result {
                let! deleted = deleteSnapshot context PF.alexBrokerage p.d2
                let! remainingLines =
                    deleted.header |> AccountSnapshotHeader.accountSnapshotId |> AccountSnapshotLine.fetchByAccountSnapshot context
                Assert.Empty(remainingLines)
                fetchSnapshot context PF.alexBrokerage p.d2
                |> expectError notFound (fun found -> Assert.Equal((PF.alexBrokerage, p.d2), found))
                let! dates = listSnapshotDates context PF.alexBrokerage p.d1 p.d4
                Assert.Equal<LocalDate list>([ p.d1; p.d3; p.d4 ], dates |> List.map fst)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.4 deleting for an account and date with no snapshot fails with a typed not-found error naming the account and the date`` () =
        runCommandRouteAndAutoRollback PositionsDeleteAccountSnapshot (fun context ->
            deleteSnapshot context PF.alexBrokerage (p.d2.PlusDays(1))
            |> expectError notFound (fun found -> Assert.Equal((PF.alexBrokerage, p.d2.PlusDays(1)), found))
            |> Ok)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.5 fetching an account and date returns that snapshot's provenance, contribution basis and lines as recorded`` () =
        fetched (Context.create NoTransaction FetchOnly) PF.alexRoth p.d1
        |> Result.map (fun stored ->
            Assert.Equal(
                (PF.alexRoth, p.d1, Reported, Some 1200.00M,
                 [ PF.bondFund, 100M, 10.00M, 1000.00M, None
                   PF.totalMarket, 5M, 100.00M, 500.00M, None ]),
                stored))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-POS-7.5 listing an account's snapshot dates between two dates returns every snapshot date of that account in the range, both ends included, in date order with its provenance, and no date outside the range or of another account`` () =
        let context = Context.create NoTransaction FetchOnly
        result {
            let! brokerage = listSnapshotDates context PF.alexBrokerage p.d2 p.d3
            Assert.Equal<(LocalDate * Provenance) list>([ p.d2, Reported; p.d3, Reported ], brokerage)
            let! retirement = listSnapshotDates context PF.sam401k p.d1 p.d3
            Assert.Equal<(LocalDate * Provenance) list>([ p.d1, Imported; p.d3, Reported ], retirement)
        }
        |> railroadWrapper

    // Placeholders committed before the Src was read (audit 2026-10-04a remediation)

    [<Fact>]
    member _.``REQ-POS-7.5 listing an account's snapshot dates with the end date the day before the begin date fails with a typed error naming both dates`` () =
        Assert.Fail "Not yet implemented"
