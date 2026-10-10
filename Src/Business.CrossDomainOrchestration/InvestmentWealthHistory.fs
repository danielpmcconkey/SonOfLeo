module Business.CrossDomainOrchestration.InvestmentWealthHistory

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.HoldingsAsOf

type WealthPoint = {
    monthEnd: LocalDate
    totals: (WealthGroup * Money.Money) list
    total: Money.Money
}

/// Every month-end on or after the begin date and on or before the end date, in date order.
let monthEndsBetween (beginDate: LocalDate) (endDate: LocalDate) : LocalDate list =
    let monthEndOf (d: LocalDate) = d.With(DateAdjusters.EndOfMonth)
    Seq.unfold
        (fun (monthEnd: LocalDate) ->
            if monthEnd > endDate then None
            else Some(monthEnd, monthEndOf (monthEnd.PlusDays 1)))
        (monthEndOf beginDate)
    |> Seq.filter (fun d -> d >= beginDate)
    |> Seq.toList

let private groupedValues (grouping: WealthGrouping) (account: HoldingsAsOfAccount) : (WealthGroup * Money.Money) list =
    let accountGroup =
        match grouping with
        | ByAccount -> Some(GroupName account.investmentAccountName)
        | ByAccountGroup -> Some(GroupName account.accountGroup)
        | ByTaxTreatment -> Some(GroupName(account.taxTreatment |> TaxTreatment.toString))
        | ByOwners -> Some(GroupOwners account.ownerNames)
        | ByDimension _ -> None
    account.lines
    |> List.map (fun line ->
        let group =
            match accountGroup, grouping with
            | Some g, _ -> g
            | None, ByDimension dimension ->
                line.dimensionValueNames |> Map.tryFind dimension |> Option.map GroupName |> Option.defaultValue Unassigned
            | None, _ -> Unassigned
        group, line.marketValue)

let private pointAt
    (context: Context.Context)
    (grouping: WealthGrouping)
    (monthEnd: LocalDate)
    : Result<WealthPoint, IAppError> =
    result {
        let! accounts = fetchHoldingValuesAsOf context monthEnd
        let values = accounts |> List.collect (groupedValues grouping)
        let! totals =
            values
            |> List.groupBy fst
            |> List.map (fun (group, members) ->
                members |> List.map snd |> Money.sumList |> Result.map (fun total -> group, total))
            |> convertListOfResultsToResultsList
        let! total = values |> List.map snd |> Money.sumList
        return { monthEnd = monthEnd; totals = totals |> List.sortBy fst; total = total }
    }

let computeInvestmentWealthHistory
    (context: Context.Context)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    (grouping: WealthGrouping)
    : Result<WealthPoint list, IAppError> =
    if endDate < beginDate then
        error (PositionsWealthHistoryEndBeforeBegin(beginDate, endDate))
    else
        result {
            let! points =
                monthEndsBetween beginDate endDate
                |> List.map (pointAt context grouping)
                |> convertListOfResultsToResultsList
            let! zero = Money.fromDecimal 0M
            let groups = points |> List.collect (fun p -> p.totals |> List.map fst) |> List.distinct
            let withEveryGroup (point: WealthPoint) =
                let missing =
                    groups
                    |> List.filter (fun g -> not (point.totals |> List.exists (fun (present, _) -> present = g)))
                    |> List.map (fun g -> g, zero)
                { point with totals = point.totals @ missing |> List.sortBy fst }
            return points |> List.map withEveryGroup
        }
