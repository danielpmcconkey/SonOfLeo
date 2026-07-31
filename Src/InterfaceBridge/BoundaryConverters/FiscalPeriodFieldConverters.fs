module InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open Model
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Utilities.AppError
open Utilities.ResultHelper
open Context.Context

let ``convert FiscalPeriodKeyString to FiscalPeriodId``
    (context: Context)
    (key: string)
    : Result<FiscalPeriodId, AppError> =
    key
    |> LookupCache.fiscalPeriodKeyToId.fetch context
    |> Result.mapError(fun e ->
        let originalType = key.GetType().Name
        let originalValue = key
        let desiredType = "FiscalPeriodId"
        let childError = e |> AppError.toMessage
        InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError))
    |> Result.map(FiscalPeriodId.fromGuid)

let ``convert [FiscalPeriodKeyString] to FiscalPeriod``
    (context: Context)
    (key: string)
    : Result<FiscalPeriod, AppError> =
    result {
        let! fiscalPeriodId = key |> ``convert FiscalPeriodKeyString to FiscalPeriodId`` context
        return! fiscalPeriodId |> fetchById context
    }

let ``convert FiscalPeriod to FiscalPeriodReturn`` fp : FiscalPeriodReturn =
    { periodKey = FiscalPeriodKey.value(periodKey fp)
      startDate = startDate fp
      endDate = endDate fp
      isOpen = isOpen fp
      createdAt = createdAt fp
      modifiedAt = modifiedAt fp }
