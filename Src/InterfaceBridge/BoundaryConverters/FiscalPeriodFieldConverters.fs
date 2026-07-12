module InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open Model
open Model.Ledger.Accounts.AccountComponent
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Utilities.ResultCE

let ``convert FiscalPeriodKeyString to FiscalPeriodId``
        (key:string)
        : Result<FiscalPeriodId, string> =
    key
    |> LookupCache.fiscalPeriodKeyToId.fetch
    |> Result.mapError (fun e -> $"Period key provided ({key}) didn't match any recorded Fiscal Periods in the database. Further details: {e}") // REQ-NGUI-1.5
    |> Result.map(FiscalPeriodId.fromGuid)
    
let ``convert FiscalPeriod to FiscalPeriodReturn`` fp : FiscalPeriodReturn = {
    periodKey = FiscalPeriodKey.value (periodKey fp)
    startDate = startDate fp
    endDate = endDate fp
    isOpen = isOpen fp
    createdAt = createdAt fp
    modifiedAt = modifiedAt fp }

