module InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open Model
open Model.Ledger
open Model.Ledger.FiscalPeriodComponent
open Utilities.AppError
open Utilities.ResultHelper


let ``convert FiscalPeriodKeyString to FiscalPeriodId``
    (context: Context.Context)
    (key: string)
    : Result<FiscalPeriodId, AppError> =
    match key |> LookupCache.fiscalPeriodKeyToId.fetch context with
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
