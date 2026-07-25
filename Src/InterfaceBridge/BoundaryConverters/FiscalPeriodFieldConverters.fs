module InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open Model
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultHelper

let ``convert FiscalPeriodKeyString to FiscalPeriodId`` (key: string) : Result<FiscalPeriodId, AppError> =
    key
    |> LookupCache.fiscalPeriodKeyToId.fetch
    |> Result.mapError(fun e ->
        let originalType = key.GetType().Name
        let originalValue = key
        let desiredType = "FiscalPeriodId"
        let childError = e |> AppError.toMessage
        InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError)) // REQ-NGUI-1.5
    |> Result.map(FiscalPeriodId.fromGuid)

let ``convert [FiscalPeriodKeyString] to FiscalPeriod``
    (transaction: DbTransaction option)
    (key: string)
    : Result<FiscalPeriod, AppError> =
    result {
        let! fiscalPeriodId = key |> ``convert FiscalPeriodKeyString to FiscalPeriodId``
        return! fiscalPeriodId |> fetchById transaction
    }

let ``convert FiscalPeriod to FiscalPeriodReturn`` fp : FiscalPeriodReturn =
    { periodKey = FiscalPeriodKey.value(periodKey fp)
      startDate = startDate fp
      endDate = endDate fp
      isOpen = isOpen fp
      createdAt = createdAt fp
      modifiedAt = modifiedAt fp }
