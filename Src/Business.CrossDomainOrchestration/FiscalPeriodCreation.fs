module Business.CrossDomainOrchestration.FiscalPeriodCreation

open System
open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.FiscalPeriodComponent

let constructNewAndPersist (context: Context.Context) (periodKey: FiscalPeriodKey) : Result<FiscalPeriod.FiscalPeriod, IAppError> =
    let fiscalPeriodId = FiscalPeriodId.create()
    let keyString = periodKey |> FiscalPeriodKey.value
    let year = keyString[0..3]
    let yearNum = Int32.Parse(year) // we already validated via regex that this won't throw
    let month = keyString[5..6]
    let monthNum = Int32.Parse(month) // we already validated via regex that this won't throw
    let startDate = LocalDate(yearNum, monthNum, 1)
    let endDate = startDate.PlusMonths(1).PlusDays(-1)
    let isOpen = true
    let now = context |> Context.getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    let fiscalPeriod =
        FiscalPeriod.create fiscalPeriodId periodKey startDate endDate isOpen createdAt modifiedAt
    result {
        do! fiscalPeriod |> FiscalPeriod.persist context
        return fiscalPeriod
    }

/// ensureFiscalPeriods creates an open period for every month from startKey through endKey, inclusive, that has none,
/// and returns only those it created. Existing periods, open or closed, are left alone. Nothing missing is not a no-op
/// error: the Saturday run calls this every week without checking first (REQ-FP-2.7, REQ-SYS-6.1.1).
let ensureFiscalPeriods
    (context: Context.Context)
    (startKey: FiscalPeriodKey)
    (endKey: FiscalPeriodKey)
    : Result<FiscalPeriod.FiscalPeriod list, IAppError> =
    let startStr = startKey |> FiscalPeriodKey.value
    let endStr = endKey |> FiscalPeriodKey.value
    let firstOfMonth (keyStr: string) = LocalDate(Int32.Parse(keyStr[0..3]), Int32.Parse(keyStr[5..6]), 1)
    result {
        do!
            // keys are zero-padded yyyy-MM, so they order as strings the way the months do
            if String.CompareOrdinal(startStr, endStr) > 0 then
                Error(LedgerError.FiscalPeriodEnsureStartAfterEnd(startStr, endStr))
            else Ok ()
        let! existing = FiscalPeriod.fetchAll context false
        let existingKeys = existing |> List.map (FiscalPeriod.periodKey >> FiscalPeriodKey.value) |> Set.ofList
        let lastMonth = endStr |> firstOfMonth
        let! missingKeys =
            startStr
            |> firstOfMonth
            |> List.unfold (fun month -> if month > lastMonth then None else Some(month, month.PlusMonths 1))
            |> List.map (fun month -> month.ToString("yyyy-MM", Globalization.CultureInfo.InvariantCulture))
            |> List.filter (fun keyStr -> existingKeys |> Set.contains keyStr |> not)
            |> List.map FiscalPeriodKey.fromString
            |> convertListOfResultsToResultsList
        return!
            missingKeys
            |> List.map (constructNewAndPersist context)
            |> convertListOfResultsToResultsList
    }
