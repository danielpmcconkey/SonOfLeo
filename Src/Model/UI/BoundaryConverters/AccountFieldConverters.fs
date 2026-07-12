module Model.UI.BoundaryConverters.AccountFieldConverters

open Model
open Model.Ledger.Accounts.AccountComponent
open Model.UI.BoundaryConverters.GenericFieldHelpers
open Model.UI.InterfaceContractTypes
open ModelOrchestrator
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

let convertAccountUuIdOptionToAccountCodeOption
        (uuidOption: Guid option)
        : Result<AccountCode option, string> =
    let fallibleConverter =  (fun id ->
          id |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    uuidOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let convertAccountIdOptionToAccountCodeOption
        (idOption: AccountId option)
        : Result<AccountCode option, string> =
    let fallibleConverter =  (fun id ->
          id |> AccountId.value |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    idOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let convertAccountTypeStringOptionToAccountTypeOption
    (stringOption: string option)
    : Result<AccountType option, string> =
    let fallibleConverter = (fun string -> string |> AccountType.fromString)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let convertAccountSubtypeStringOptionToAccountSubtypeOption
    (stringOption: string option)
    : Result<AccountSubtype option, string> =
    let fallibleConverter = (fun string -> string |> AccountSubtype.fromString)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter