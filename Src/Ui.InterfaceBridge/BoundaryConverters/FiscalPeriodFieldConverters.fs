module Ui.InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open App.Utility.IAppError
open App.DataAccessLayer.DalError
open App.Utility.Result
open App.DataAccessLayer
open App.Session
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Ui.InterfaceBridge.InterfaceContracts.FiscalPeriodContracts

let ``convert FiscalPeriodKeyString to FiscalPeriodId``
    (context: Context.Context)
    (key: string)
    : Result<FiscalPeriodId, IAppError> =
    key |> LookupCache.fiscalPeriodKeyToId.fetch (context |> Context.getDatabaseTransaction)
    |> whenNoRows (LedgerError.FiscalPeriodNoPeriodMatchingKey key)
    |> Result.map FiscalPeriodId.fromGuid

let ``convert [FiscalPeriodKeyString] to FiscalPeriod``
    (context: Context.Context)
    (key: string)
    : Result<FiscalPeriod.FiscalPeriod, IAppError> =
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
