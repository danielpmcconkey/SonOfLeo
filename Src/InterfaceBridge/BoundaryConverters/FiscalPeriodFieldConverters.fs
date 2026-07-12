module InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters

open Model
open Model.Ledger.Accounts.AccountComponent
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers
open Utilities.ResultCE
open System

let convertAccountCodeStringOptionToAccountIdOption
    (codeStringOption: string option)
    : Result<AccountId option, string> =
    let fallibleConverter = (fun code -> 
          result {
              let! uuid = code |> LookupCache.accountCodeToId.fetch
              return uuid |> AccountId.fromGuid })
    codeStringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

