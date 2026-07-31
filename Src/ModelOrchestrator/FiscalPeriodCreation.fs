module ModelOrchestrator.FiscalPeriodCreation

open System
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open Context.Context

/// constructNewAndSaveToDb validates that the components work together to
/// form a valid whole before adding it to the persistence layer. All new
/// account creation should route through here before being sent to the
/// persistence layer. Internal model functions may construct through other
/// means if they're operating on known good data.
let constructNewAndSaveToDb (context: Context) (periodKey: FiscalPeriodKey) : Result<FiscalPeriod, AppError> =
    let fiscalPeriodId = FiscalPeriodId.create()
    let keyString = periodKey |> FiscalPeriodKey.value
    let year = keyString[0..3]
    let yearNum = Int32.Parse(year) // we already validated via regex that this won't throw
    let month = keyString[5..6]
    let monthNum = Int32.Parse(month) // we already validated via regex that this won't throw
    let startDate = LocalDate(yearNum, monthNum, 1)
    let endDate = startDate.PlusMonths(1).PlusDays(-1)
    let isOpen = true
    let now = context |> getInitiationInstant
    let createdAt = now
    let modifiedAt = now
    let fiscalPeriod =
        FiscalPeriod.create fiscalPeriodId periodKey startDate endDate isOpen createdAt modifiedAt
    result {
        do! fiscalPeriod |> insertNewToDb context
        return fiscalPeriod
    }
