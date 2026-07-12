module InterfaceBridge.BoundaryConverters.AccountFieldConverters

open InterfaceBridge.InterfaceContracts.AccountContracts
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open InterfaceBridge.BoundaryConverters.GenericFieldHelpers
open ModelOrchestrator
open ModelOrchestrator.AccountBalance
open Utilities
open Utilities.DAL
open Utilities.ResultCE
open System

let convertAccountIdToAccountCodeString
        (id: AccountId)
        : Result<string, string> =
    id |> AccountId.value |> LookupCache.accountIdToCode.fetch 

let convertAccountIdToAccountCode
        (id: AccountId)
        : Result<AccountCode, string> =
    id |> convertAccountIdToAccountCodeString |> Result.bind AccountCode.create

let convertAccountIdOptionToAccountCodeOption
        (idOption: AccountId option)
        : Result<AccountCode option, string> =
    let fallibleConverter =  (fun id ->
          id |> AccountId.value |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    idOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let convertAccountIdOptionToAccountCodeStringOption
        (idOption: AccountId option)
        : Result<string option, string> =
    let code = idOption |> convertAccountIdOptionToAccountCodeOption
    match code with
    | Error e -> Error $"The returned parent ID of {idOption} didn't match any recorded Accounts in the database. Further details: {e}" // REQ-NGUI-1.5
    | Ok x -> Ok (x |> Option.map(AccountCode.value))

let convertAccountCodeStringOptionToAccountUuidOption
        (code: string option)
        : Result<Guid option, string> =
    match code with
    | Some x ->
        x
        |> LookupCache.accountCodeToId.fetch
        |> Result.mapError(fun e -> $"Parent code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        |> Result.map Some
    | None -> Ok None

let convertAccountToAccountReturn (a:Account) : Result<AccountReturn, string> =
    result {
        let! parentCode = a |> parentId |> convertAccountIdOptionToAccountCodeStringOption
        return {
            code = AccountCode.value (code a)
            name = AccountName.value (accountName a)
            accountTypeSt = AccountType.toString (accountType a)
            activeBegin = activeBegin a
            activeEnd = activeEnd a
            subType = accountSubType a |> Option.map AccountSubtype.toString
            parentCode = parentCode
            reference = externalReference a |> Option.map AccountExternalReference.value
            createdAt = createdAt a
            modifiedAt = modifiedAt a
        } }

let convertAccountCodeStringToId (code:string) : Result<AccountId, string> =
    code
    |> LookupCache.accountCodeToId.fetch
    |> Result.mapError (fun e -> $"Account code provided ({code}) didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
    |> Result.map(AccountId.fromGuid)

let convertAccountCodeStringToAccount
            (transaction: DbTransaction option)
            (code:string)
            : Result<Account, string> =
    result {    let! id = code |> convertAccountCodeStringToId
                return! id |> fetchById transaction }

let convertAccountCodeStringOptionToAccountIdOption
        (codeStringOption: string option)
        : Result<AccountId option, string> =
    let fallibleConverter = (fun code -> 
          result {
              let! uuid = code |> LookupCache.accountCodeToId.fetch
              return uuid |> AccountId.fromGuid })
    codeStringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let convertAccountCodeStringListToAccountIdList
        (codes: string list)
        : Result<AccountId list, string> =
    codes
    |> List.map (fun x -> x |> convertAccountCodeStringToId )
    |> ListHelper.listOfResultsToResultsList
    
let convertAccountUuIdOptionToAccountCodeOption
        (uuidOption: Guid option)
        : Result<AccountCode option, string> =
    let fallibleConverter =  (fun id ->
          id |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    uuidOption
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

let convertAccountBalanceToAccountBalanceReturn
        (balance : AccountBalance)
        : Result<AccountBalanceReturn, string> =
    result {
        let! codeString = balance.accountId |> convertAccountIdToAccountCodeString
        return {    accountCode = codeString
                    totalCredits =  balance.totalCredits |> Money.amount
                    totalDebits = balance.totalDebits |> Money.amount
                    netBalance = balance.netBalance |> Money.amount } }