module Business.CrossDomainOrchestration.InvestmentWealthHistory

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.HoldingsAsOf

type WealthGrouping =
    | ByAccount
    | ByAccountGroup
    | ByTaxTreatment
    | ByOwners
    | ByDimension of Dimension

module WealthGrouping =
    let all =
        [ ByAccount; ByAccountGroup; ByTaxTreatment; ByOwners ] @ (Dimension.all |> List.map ByDimension)
    let toString grouping =
        match grouping with
        | ByAccount -> "Account"
        | ByAccountGroup -> "AccountGroup"
        | ByTaxTreatment -> "TaxTreatment"
        | ByOwners -> "Owners"
        | ByDimension dimension -> dimension |> Dimension.toString
    let fromString (raw: string) : Result<WealthGrouping, IAppError> =
        match all |> List.tryFind (fun g -> toString g = raw) with
        | Some grouping -> Ok grouping
        | None -> error (PositionsInvalidWealthGrouping raw)

/// One value of the grouping. Owners is the complete owner set of an account, so a joint account is its own group.
type WealthGroup =
    | GroupName of string
    | GroupOwners of string list
    | Unassigned

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
        let! accounts = fetchHoldingsAsOf context monthEnd
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
        monthEndsBetween beginDate endDate
        |> List.map (pointAt context grouping)
        |> convertListOfResultsToResultsList
