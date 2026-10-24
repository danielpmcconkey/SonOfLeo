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
    let startDate = periodKey |> FiscalPeriodKey.startDate
    let endDate = periodKey |> FiscalPeriodKey.endDate
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
/// error: callers run it repeatedly without checking first.
let ensureFiscalPeriods
    (context: Context.Context)
    (startKey: FiscalPeriodKey)
    (endKey: FiscalPeriodKey)
    : Result<FiscalPeriod.FiscalPeriod list, IAppError> =
    let startStr = startKey |> FiscalPeriodKey.value
    let endStr = endKey |> FiscalPeriodKey.value
    result {
        do!
            // keys are zero-padded yyyy-MM, so they order as strings the way the months do
            if String.CompareOrdinal(startStr, endStr) > 0 then
                Error(LedgerError.FiscalPeriodEnsureStartAfterEnd(startStr, endStr))
            else Ok ()
        let! existing = FiscalPeriod.fetchAll context false
        let existingKeys = existing |> List.map (FiscalPeriod.periodKey >> FiscalPeriodKey.value) |> Set.ofList
        let lastMonth = endKey |> FiscalPeriodKey.startDate
        let missingKeys =
            startKey
            |> FiscalPeriodKey.startDate
            |> List.unfold (fun month -> if month > lastMonth then None else Some(month, month.PlusMonths 1))
            |> List.map FiscalPeriodKey.ofDate
            |> List.filter (fun key -> existingKeys |> Set.contains (key |> FiscalPeriodKey.value) |> not)
        // in month order, stopping at the first that fails, so a refused month leaves no later one created
        return!
            missingKeys
            |> List.fold
                (fun created key ->
                    created |> Result.bind (fun soFar -> constructNewAndPersist context key |> Result.map (fun p -> p :: soFar)))
                (Ok [])
            |> Result.map List.rev
    }
