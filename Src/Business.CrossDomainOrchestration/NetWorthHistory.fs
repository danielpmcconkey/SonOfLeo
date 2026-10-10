module Business.CrossDomainOrchestration.NetWorthHistory

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Positions.PositionsError
open Business.CrossDomainOrchestration.NetWorth
open Business.CrossDomainOrchestration.InvestmentWealthHistory

/// Net worth on one month-end, reduced to its totals, whether the date is pre-ledger, and what is absent.
type NetWorthPoint = {
    monthEnd: LocalDate
    isPreLedger: bool
    totalLedgerAssets: Money.Money
    totalInvestments: Money.Money
    totalPropertyValues: Money.Money
    totalLiabilities: Money.Money
    totalOwnedPropertyMortgages: Money.Money
    netWorth: Money.Money
    investableWealth: Money.Money
    absentComponents: NetWorthComponent list
}

let private pointOf (netWorth: NetWorth) : NetWorthPoint =
    { monthEnd = netWorth.asOf
      isPreLedger = netWorth.isPreLedger
      totalLedgerAssets = netWorth.totalLedgerAssets
      totalInvestments = netWorth.totalInvestments
      totalPropertyValues = netWorth.totalPropertyValues
      totalLiabilities = netWorth.totalLiabilities
      totalOwnedPropertyMortgages = netWorth.totalOwnedPropertyMortgages
      netWorth = netWorth.netWorth
      investableWealth = netWorth.investableWealth
      absentComponents = netWorth.absentComponents }

/// Net worth on every month-end in the range, in date order. Fails, naming the earliest, when any month-end is neither
/// in a fiscal period nor a pre-ledger date: a missing month would read as a month with nothing to report.
let computeNetWorthHistory
    (context: Context.Context)
    (beginDate: LocalDate)
    (endDate: LocalDate)
    : Result<NetWorthPoint list, IAppError> =
    if endDate < beginDate then
        error (PositionsNetWorthHistoryEndBeforeBegin(beginDate, endDate))
    else
        result {
            let monthEnds = monthEndsBetween beginDate endDate
            let! fiscalPeriods = FiscalPeriod.fetchAll context false
            do!
                monthEnds
                |> List.map (classifyDate fiscalPeriods >> Result.map ignore)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            return!
                monthEnds
                |> List.map (computeNetWorth context >> Result.map pointOf)
                |> convertListOfResultsToResultsList
        }
