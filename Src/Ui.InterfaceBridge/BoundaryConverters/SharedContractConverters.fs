module Ui.InterfaceBridge.BoundaryConverters.SharedContractConverters

open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer
open App.Session
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Business.CrossDomainOrchestration.FetchFilters
open Ui.InterfaceBridge.InterfaceContracts.SharedContracts

let ``convert TemporalFilterInput to TemporalFilter``
    (context: Context.Context)
    (input: TemporalFilterInput)
    : Result<TemporalFilter, IAppError> =

    match input with
    | TemporalFilterInput.DateRange dateRange ->
        Ok(TemporalFilter.DateRange { beginDate = dateRange.beginDate; endInclusive = dateRange.endInclusive })
    | TemporalFilterInput.PeriodKey periodKey ->
        result {
            // make sure it's a valid string for even being a period key
            let! key = periodKey |> FiscalPeriodKey.fromString
            let! fiscalPeriodId =
                key
                |> Business.FinancialServices.Ledger.FiscalPeriod.keyToId context
                |> Result.mapError(fun _ -> FiscalPeriodNoPeriodMatchingKey periodKey)
            return fiscalPeriodId |> TemporalFilter.FiscalPeriodIdentifier
        }
let ``convert TemporalFilterInput Option To TemporalFilter Option``
    (context: Context.Context)
    (input: TemporalFilterInput option)
    : Result<TemporalFilter option, IAppError> =
    let fallibleConverter = (fun x -> x |> ``convert TemporalFilterInput to TemporalFilter`` context)
    input |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter
