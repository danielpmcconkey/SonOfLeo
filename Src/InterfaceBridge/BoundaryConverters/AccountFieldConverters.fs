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

let ``convert AccountId to AccountCodeString``
        (id: AccountId)
        : Result<string, string> =
    id |> AccountId.value |> LookupCache.accountIdToCode.fetch 

let ``convert AccountId to AccountCode``
        (id: AccountId)
        : Result<AccountCode, string> =
    id |> ``convert AccountId to AccountCodeString`` |> Result.bind AccountCode.create

let ``convert AccountId Option to AccountCode Option``
        (idOption: AccountId option)
        : Result<AccountCode option, string> =
    let fallibleConverter =  (fun id ->
          id |> AccountId.value |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    idOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert AccountId Option to AccountCodeString Option``
        (idOption: AccountId option)
        : Result<string option, string> =
    let code = idOption |> ``convert AccountId Option to AccountCode Option``
    match code with
    | Error e -> Error $"The returned parent ID of {idOption} didn't match any recorded Accounts in the database. Further details: {e}" // REQ-NGUI-1.5
    | Ok x -> Ok (x |> Option.map(AccountCode.value))

let ``convert AccountCodeString Option to AccountUuidOption``
        (code: string option)
        : Result<Guid option, string> =
    match code with
    | Some x ->
        x
        |> LookupCache.accountCodeToId.fetch
        |> Result.mapError(fun e -> $"Parent code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        |> Result.map Some
    | None -> Ok None

let ``convert Account to AccountReturn`` (a:Account) : Result<AccountReturn, string> =
    result {
        let! parentCode = a |> parentId |> ``convert AccountId Option to AccountCodeString Option``
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
            modifiedAt = modifiedAt a } }

let ``convert AccountCodeString to Id`` (code:string) : Result<AccountId, string> =
    code
    |> LookupCache.accountCodeToId.fetch
    |> Result.mapError (fun e -> $"Account code provided ({code}) didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
    |> Result.map(AccountId.fromGuid)

let ``convert AccountCodeString to AccountUuid`` (code:string) : Result<Guid, string> =
    code
    |> LookupCache.accountCodeToId.fetch
    |> Result.mapError (fun e -> $"Account code provided ({code}) didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5

let ``convert AccountCodeString to Account``
            (transaction: DbTransaction option)
            (code:string)
            : Result<Account, string> =
    result {    let! id = code |> ``convert AccountCodeString to Id``
                return! id |> fetchById transaction }

let ``convert AccountCodeString Option to AccountId Option``
        (codeStringOption: string option)
        : Result<AccountId option, string> =
    let fallibleConverter = (fun code -> 
          result {
              let! uuid = code |> LookupCache.accountCodeToId.fetch
              return uuid |> AccountId.fromGuid })
    codeStringOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert AccountCodeString List to AccountId List``
        (codes: string list)
        : Result<AccountId list, string> =
    codes
    |> List.map (fun x -> x |> ``convert AccountCodeString to Id`` )
    |> ListHelper.listOfResultsToResultsList
    
let ``convert AccountUuId Option to AccountCode Option``
        (uuidOption: Guid option)
        : Result<AccountCode option, string> =
    let fallibleConverter =  (fun id ->
          id |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    uuidOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert AccountTypeString Option to AccountType Option``
    (stringOption: string option)
    : Result<AccountType option, string> =
    let fallibleConverter = (fun string -> string |> AccountType.fromString)
    stringOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert AccountSubtypeString Option to AccountSubtype Option``
    (stringOption: string option)
    : Result<AccountSubtype option, string> =
    let fallibleConverter = (fun string -> string |> AccountSubtype.fromString)
    stringOption
    |> ``convert Option to Desired Type with Fallible Converter`` fallibleConverter

let ``convert AccountBalance to AccountBalanceReturn``
        (balance : AccountBalance)
        : Result<AccountBalanceReturn, string> =
    result {
        let! codeString = balance.accountId |> ``convert AccountId to AccountCodeString``
        return {    accountCode = codeString
                    totalCredits =  balance.totalCredits |> Money.amount
                    totalDebits = balance.totalDebits |> Money.amount
                    netBalance = balance.netBalance |> Money.amount } }