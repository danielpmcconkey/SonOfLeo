module InterfaceBridge.ReportWriters.TrialBalanceWriter

open InterfaceBridge.InterfaceContracts.ReportsContracts
open ModelOrchestrator.TrialBalanceReport
open NodaTime
open Utilities.AppError

let write
    (pathInfo: OutputPathInput)
    (asOf: LocalDate)
    (sortedRows: TrialBalanceReturnRow list)
    : Result<TrialBalanceReturn, AppError> =
    let dateInterpolation = if pathInfo.interpolateAsOf then $"{asOf}" else ""
    let fullPath = $"{pathInfo.baseDir}/{pathInfo.fileName}{dateInterpolation}.html"
    Ok (TrialBalanceReturn.Report{ fullyQualifiedPath = fullPath})
