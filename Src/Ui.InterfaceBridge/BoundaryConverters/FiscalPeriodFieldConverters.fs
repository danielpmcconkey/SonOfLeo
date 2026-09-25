module Ui.InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open App.Utility.IAppError
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
    match key |> LookupCache.fiscalPeriodKeyToId.fetch (context |> Context.getDatabaseTransaction) with
    | Ok x -> x |> FiscalPeriodId.fromGuid |> Ok
    | Error e ->
        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
        then Error (LedgerError.FiscalPeriodNoPeriodMatchingKey key)
        else Error e

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
