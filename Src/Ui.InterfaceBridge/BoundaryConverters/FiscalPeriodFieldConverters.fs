module Ui.InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open App.Utility.AppError
open App.Utility.Result
open App.DataAccessLayer
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Ui.InterfaceBridge.InterfaceContracts.FiscalPeriodContracts

let ``convert FiscalPeriodKeyString to FiscalPeriodId``
    (context: Context.Context)
    (key: string)
    : Result<FiscalPeriodId, AppError> =
    match key |> LookupCache.fiscalPeriodKeyToId.fetch (context |> Context.getDatabaseTransaction) with
    | Ok x -> x |> FiscalPeriodId.fromGuid |> Ok
    | Error (DalResultantRowsDidntMatchExpectation _) -> Error (FiscalPeriodNoPeriodMatchingKey key)
    | Error e -> Error e

let ``convert [FiscalPeriodKeyString] to FiscalPeriod``
    (context: Context.Context)
    (key: string)
    : Result<FiscalPeriod.FiscalPeriod, AppError> =
    result {
        let! fiscalPeriodId = key |> ``convert FiscalPeriodKeyString to FiscalPeriodId`` context
        return! fiscalPeriodId |> FiscalPeriod.fetchById context
    }

let ``convert FiscalPeriod to FiscalPeriodReturn`` fp : FiscalPeriodReturn =
    { periodKey = FiscalPeriodKey.value(FiscalPeriod.periodKey fp)
      startDate = FiscalPeriod.startDate fp
      endDate = FiscalPeriod.endDate fp
      isOpen = FiscalPeriod.isOpen fp
      createdAt = FiscalPeriod.createdAt fp
      modifiedAt = FiscalPeriod.modifiedAt fp }
