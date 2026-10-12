module Business.CrossDomainOrchestration.InvestmentWealthHistory

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.Calendar
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Positions
open Business.FinancialServices.Positions.PositionsError
open Business.FinancialServices.Positions.PositionsComponent
open Business.CrossDomainOrchestration.HoldingsAsOf

let private groupedValues
    (securitiesById: Map<SecurityId, Security.Security>)
    (grouping: WealthGrouping)
    (account: HoldingsAsOfAccount)
    : Result<(WealthGroup * Money.Money) list, IAppError> =
    let accountGroup =
        match grouping with
        | ByAccount -> Some(GroupInvestmentAccount account.investmentAccountId)
        | ByAccountGroup -> Some(GroupAccountGroup account.accountGroup)
        | ByTaxTreatment -> Some(GroupTaxTreatment account.taxTreatment)
        | ByOwners -> Some(GroupOwners account.ownerIds)
        | ByDimension _ -> None
    account.lines
    |> List.map (fun line ->
        match accountGroup, grouping with
        | Some g, _ -> Ok(g, line.marketValue)
        | None, ByDimension dimension ->
            match securitiesById |> Map.tryFind line.securityId with
            | None -> error (PositionsSecurityIdDoesntExist(line.securityId |> SecurityId.value))
            | Some security ->
                let group =
                    security
                    |> Security.dimensionValues
                    |> Map.tryFind dimension
                    |> Option.map GroupDimensionValue
                    |> Option.defaultValue Unassigned
                Ok(group, line.marketValue)
        | None, _ -> Ok(Unassigned, line.marketValue))
    |> convertListOfResultsToResultsList

let private pointAt
    (context: Context.Context)
    (securitiesById: Map<SecurityId, Security.Security>)
    (grouping: WealthGrouping)
    (monthEnd: LocalDate)
    : Result<WealthPoint, IAppError> =
    result {
        let! accounts = fetchHoldingsAsOf context monthEnd
        let! values =
            accounts
            |> List.map (groupedValues securitiesById grouping)
            |> convertListOfResultsToResultsList
            |> Result.map List.concat
        let! totals =
            values
            |> List.groupBy fst
            |> List.map (fun (group, members) ->
                members |> List.map snd |> Money.sumList |> Result.map (fun total -> group, total))
            |> convertListOfResultsToResultsList
        let! total = values |> List.map snd |> Money.sumList
        return { monthEnd = monthEnd; totals = totals; total = total }
    }

/// One point per month-end in the range. The groups within a point are in no particular order: they are ordered by
/// their names, which only the boundary resolves.
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
            let! securitiesById =
                match grouping with
                | ByDimension _ ->
                    Security.fetchAll context
                    |> Result.map (List.map (fun s -> s |> Security.securityId, s) >> Map.ofList)
                | ByAccount
                | ByAccountGroup
                | ByTaxTreatment
                | ByOwners -> Ok Map.empty
            let! points =
                monthEndsBetween beginDate endDate
                |> List.map (pointAt context securitiesById grouping)
                |> convertListOfResultsToResultsList
            let! zero = Money.fromDecimal 0M
            let groups = points |> List.collect (fun p -> p.totals |> List.map fst) |> List.distinct
            let withEveryGroup (point: WealthPoint) =
                let missing =
                    groups
                    |> List.filter (fun g -> not (point.totals |> List.exists (fun (present, _) -> present = g)))
                    |> List.map (fun g -> g, zero)
                { point with totals = point.totals @ missing }
            return points |> List.map withEveryGroup
        }
