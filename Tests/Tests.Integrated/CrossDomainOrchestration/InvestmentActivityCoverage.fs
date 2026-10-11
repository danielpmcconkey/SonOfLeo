module Tests.Integrated.CrossDomainOrchestration.InvestmentActivityCoverage

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices.Positions.PositionsAuditableAction
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration.InvestmentActivityOrchestration
open Ui.InterfaceBridge.InterfaceContracts.PositionsContracts
open Ui.InterfaceBridge.BoundaryConverters.PositionsFieldConverters
open Ui.InterfaceBridge.CommandRoute
open Tests.Helpers
open Tests.Helpers.PositionsValues
open Tests.Helpers.Railroad
open Xunit

module PF = PositionsFixture

(* Every test records through recordActivity, in a transaction that is rolled back. Coverage is read back straight from
   the table, not through any function recording uses, so a read that agrees with a broken merge cannot hide it. The
   fixture records Sam 401k for d1 - 1 to d3 + 1 and Alex Brokerage for d3 + 1 to d4; Joint Brokerage and Alex Roth IRA
   start with no coverage. *)

let private interest date (amount: decimal) : InvestmentActivityInput =
    { activityDate = date
      kind = "Interest"
      description = "Interest on cash"
      source = None
      securityName = None
      quantity = None
      price = None
      amount = amount }

let private range account beginDate endDate activities : InvestmentActivityRangeInput =
    { accountName = account; beginDate = beginDate; endDate = endDate; activities = activities }

/// A range with one Interest activity on its begin date.
let private supplied account (beginDate: LocalDate) (endDate: LocalDate) =
    range account beginDate endDate [ interest beginDate 1.00M ]

let private record context (ranges: InvestmentActivityRangeInput list) =
    ranges
    |> List.map (``convert [InvestmentActivityRangeInput] to [ActivityRange]`` context)
    |> convertListOfResultsToResultsList
    |> Result.bind (recordActivity context)

/// Every stored coverage row as (account name, begin, end), ordered by account name and then begin date.
let private allCoverage (context: Context.Context) : Result<(string * LocalDate * LocalDate) list, IAppError> =
    let queryStatement =
        """
        select ia.account_name, c.begin_date, c.end_date
        from positions.investment_activity_coverage c
        join positions.investment_account ia on ia.unique_id = c.investment_account_id
        order by ia.account_name, c.begin_date;"""
    let mapRawForDbRead (row: RowReader) =
        (row |> RowReader.getString "account_name"), (row |> RowReader.getDate "begin_date"), (row |> RowReader.getDate "end_date")
    executeReaderQuery (context |> Context.getDatabaseTransaction) queryStatement [] mapRawForDbRead Ok AnyQuantityIsAcceptable

/// The account's stored coverage rows as (account name, begin, end), in begin-date order.
let private coverageOf context (account: string) =
    allCoverage context |> Result.map (List.filter (fun (name, _, _) -> name = account))

let private gaps context account beginDate endDate =
    PositionsLookups.investmentAccountIdOf context account
    |> Result.bind (fun accountId -> listActivityCoverageGaps context accountId beginDate endDate)
    |> Result.map (List.map (fun (gap: ActivityCoverageGap) -> gap.firstUncoveredDate, gap.lastUncoveredDate))

let private rolledBack (body: Context.Context -> Result<unit, IAppError>) =
    runCommandRouteAndAutoRollback PositionsRecordInvestmentActivity body |> railroadWrapper

[<Collection("SharedTestData")>]
type InvestmentActivityCoverageTests(fixture: TestDataFixture) =

    let p = fixture.Data.positions
    let day (offset: int) = p.d1.PlusDays(offset)
    let joint = PF.jointBrokerage

    // =========================================================================
    // REQ-POS-13.6 — recording adds coverage, merged
    // =========================================================================

    [<Fact>]
    member _.``REQ-POS-13.6 recording one range for an account with no activity coverage leaves exactly one coverage row for that account, from the range's begin date to its end date`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 5) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 5 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that overlaps the account's one covered range leaves exactly one coverage row, from the earlier of the two begin dates to the later of the two end dates`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 5) ]
                let! _ = record context [ supplied joint (day 3) (day 9) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 9 ], stored)
            })

    [<Theory>]
    [<InlineData("after")>]
    [<InlineData("before")>]
    member _.``REQ-POS-13.6 for each of a range beginning the day after the account's covered range ends and a range ending the day before it begins, recording it leaves exactly one coverage row, from the earlier begin date to the later end date`` (side: string) =
        rolledBack (fun context ->
            result {
                let covered, adjoining =
                    if side = "after" then (day 0, day 5), (day 6, day 9) else (day 4, day 9), (day 0, day 3)
                let! _ = record context [ supplied joint (fst covered) (snd covered) ]
                let! _ = record context [ supplied joint (fst adjoining) (snd adjoining) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 9 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that reaches from inside one covered range of the account to inside a later one leaves exactly one coverage row, from the first's begin date to the second's end date`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 3); supplied joint (day 8) (day 12) ]
                let! _ = record context [ supplied joint (day 2) (day 9) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 12 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that begins two days after the account's covered range ends, leaving one uncovered day between them, adds a second coverage row for exactly that range and leaves the first row's begin and end dates unchanged`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 3) ]
                let! _ = record context [ supplied joint (day 5) (day 8) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 3; joint, day 5, day 8 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range lying wholly inside the account's covered range leaves exactly one coverage row, with the covered range's begin and end dates unchanged`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 10) ]
                let! _ = record context [ supplied joint (day 2) (day 5) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 10 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 REQ-POS-13.3 two adjoining ranges for one account in one operation, supplied later range first, leave exactly one coverage row, from the earlier range's begin date to the later range's end date`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 6) (day 9); supplied joint (day 0) (day 5) ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 9 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range supplied with no activities, for an account with no activity coverage, leaves exactly one coverage row, from the range's begin date to its end date`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ range joint (day 0) (day 5) [] ]
                let! stored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 5 ], stored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording ranges for two accounts in one operation leaves each account with exactly one coverage row, matching its own range`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 5); supplied PF.alexRoth (day 3) (day 9) ]
                let! jointStored = coverageOf context joint
                let! rothStored = coverageOf context PF.alexRoth
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, day 0, day 5 ], jointStored)
                Assert.Equal<(string * LocalDate * LocalDate) list>([ PF.alexRoth, day 3, day 9 ], rothStored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 recording a range that overlaps or adjoins another account's covered range leaves the other account's coverage rows exactly as they were and merges nothing across accounts`` () =
        // Sam 401k is covered d1 - 1 to d3 + 1; Joint Brokerage's range overlaps its end and runs past it
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint p.d3 (p.d3.PlusDays(5)) ]
                let! samStored = coverageOf context PF.sam401k
                let! jointStored = coverageOf context joint
                Assert.Equal<(string * LocalDate * LocalDate) list>([ PF.sam401k, day -1, p.d3.PlusDays(1) ], samStored)
                Assert.Equal<(string * LocalDate * LocalDate) list>([ joint, p.d3, p.d3.PlusDays(5) ], jointStored)
            })

    [<Fact>]
    member _.``REQ-POS-13.6 REQ-POS-13.1 a recording of ranges for two accounts that is rejected because one account's two ranges overlap leaves both accounts' coverage rows exactly as they were before`` () =
        // Alex Brokerage's range adjoins its coverage and would merge with it; Sam 401k's two ranges share a day
        rolledBack (fun context ->
            result {
                record context
                    [ supplied PF.alexBrokerage (p.d4.PlusDays(1)) (p.d4.PlusDays(3))
                      supplied PF.sam401k (p.d3.PlusDays(3)) (p.d3.PlusDays(5))
                      supplied PF.sam401k (p.d3.PlusDays(5)) (p.d3.PlusDays(7)) ]
                |> expectError
                    (function AsError (PositionsActivityRangesOverlap (a, _, _)) -> Some a | _ -> None)
                    (fun account -> Assert.Equal(PF.sam401k, account))
                let! stored = allCoverage context
                Assert.Equal<(string * LocalDate * LocalDate) list>(
                    [ PF.alexBrokerage, p.d3.PlusDays(1), p.d4; PF.sam401k, day -1, p.d3.PlusDays(1) ], stored)
            })

    // =========================================================================
    // REQ-POS-13.7 — the stretches not covered
    // =========================================================================

    [<Fact>]
    member _.``REQ-POS-13.7 for an account with no coverage between two dates, the uncovered stretches are exactly one, from the begin date to the end date`` () =
        // nothing covers d4 + 1 to d4 + 5 for any account
        rolledBack (fun context ->
            result {
                let! found = gaps context joint (p.d4.PlusDays(1)) (p.d4.PlusDays(5))
                Assert.Equal<(LocalDate * LocalDate) list>([ p.d4.PlusDays(1), p.d4.PlusDays(5) ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for two dates lying wholly inside one covered range, the uncovered stretches are an empty list`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 10) ]
                let! found = gaps context joint (day 2) (day 5)
                Assert.Empty(found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for two dates enclosing two covered ranges, the uncovered stretches are exactly the stretch from the begin date to the day before the first range, the stretch between the two ranges, and the stretch from the day after the second range to the end date, in that order`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 3) (day 5); supplied joint (day 9) (day 12) ]
                let! found = gaps context joint (day 0) (day 15)
                Assert.Equal<(LocalDate * LocalDate) list>([ day 0, day 2; day 6, day 8; day 13, day 15 ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for a single uncovered day between two covered ranges, the uncovered stretches are exactly one, whose begin and end dates are both that day`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 4); supplied joint (day 6) (day 10) ]
                let! found = gaps context joint (day 0) (day 10)
                Assert.Equal<(LocalDate * LocalDate) list>([ day 5, day 5 ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for coverage ending on the begin date and starting on the end date, the uncovered stretches are exactly one, from the day after the begin date to the day before the end date`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day -3) (day 0); supplied joint (day 10) (day 12) ]
                let! found = gaps context joint (day 0) (day 10)
                Assert.Equal<(LocalDate * LocalDate) list>([ day 1, day 9 ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for coverage ending the day before the begin date and starting the day after the end date, the uncovered stretches are exactly one, from the begin date to the end date`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day -3) (day -1); supplied joint (day 11) (day 12) ]
                let! found = gaps context joint (day 0) (day 10)
                Assert.Equal<(LocalDate * LocalDate) list>([ day 0, day 10 ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for an uncovered day given as both the begin and the end date, the uncovered stretches are exactly one, whose begin and end dates are both that day`` () =
        rolledBack (fun context ->
            result {
                let! _ = record context [ supplied joint (day 0) (day 4); supplied joint (day 6) (day 8) ]
                let! found = gaps context joint (day 5) (day 5)
                Assert.Equal<(LocalDate * LocalDate) list>([ day 5, day 5 ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 for an account with no coverage between two dates that another account covers in full, the uncovered stretches are exactly one, from the begin date to the end date`` () =
        // Sam 401k covers d1 - 1 to d3 + 1
        rolledBack (fun context ->
            result {
                let! found = gaps context joint (day 0) (day 10)
                Assert.Equal<(LocalDate * LocalDate) list>([ day 0, day 10 ], found)
            })

    [<Fact>]
    member _.``REQ-POS-13.7 listing uncovered stretches with the end date the day before the begin date fails with a typed error naming both dates`` () =
        rolledBack (fun context ->
            gaps context PF.sam401k (day 5) (day 4)
            |> expectError
                (function AsError (PositionsActivityCoverageGapsEndBeforeBegin (b, e)) -> Some(b, e) | _ -> None)
                (fun found -> Assert.Equal((day 5, day 4), found))
            |> Ok)
