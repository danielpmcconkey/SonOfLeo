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
