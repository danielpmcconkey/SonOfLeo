module ModelOrchestrator.FiscalPeriodCreation

open System
open Model.Ledger
open Model.Ledger.FiscalPeriodComponent
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper

let constructNewAndPersist (context: Context.Context) (periodKey: FiscalPeriodKey) : Result<FiscalPeriod.FiscalPeriod, AppError> =
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
