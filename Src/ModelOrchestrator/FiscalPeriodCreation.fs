module ModelOrchestrator.FiscalPeriodCreation

open System
open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open NodaTime
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultCE



/// constructNewAndSaveToDb validates that the components work together to
/// form a valid whole before adding it to the persistence layer. All new
/// account creation should route through here before being sent to the
/// persistence layer. Internal model functions may construct through other
/// means if they're operating on known good data. 
let constructNewAndSaveToDb 
        (periodKey: FiscalPeriodKey)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<FiscalPeriod, AppError> =
    let fiscalPeriodId = FiscalPeriodId.create ()
    let keyString = periodKey |> FiscalPeriodKey.value
    let year =  keyString[0..3]
    let yearNum = Int32.Parse(year) // we already validated via regex that this won't throw
    let month = keyString[5..6]
    let monthNum = Int32.Parse(month) // we already validated via regex that this won't throw
    let startDate = LocalDate(yearNum, monthNum, 1) // REQ-FP-1.4, REQ-FP-2.3
    let endDate = startDate.PlusMonths(1).PlusDays(-1) // REQ-FP-1.5, REQ-FP-2.3
    let isOpen = true // REQ-FP-1.8
    let now = AuditEnvelope.instant auditEnvelope
    let createdAt =  now // REQ-SYS-3.2
    let modifiedAt = now // REQ-SYS-3.2
    let fiscalPeriod = create fiscalPeriodId periodKey startDate endDate isOpen createdAt modifiedAt
    result {    do! insertNewToDb fiscalPeriod transaction // REQ-FP-2.4
                return fiscalPeriod } // REQ-FP-2.4
    