module InterfaceBridge.BoundaryConverters.AccountFieldConverters

open InterfaceBridge.InterfaceContracts.AccountContracts
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.AccountBalance
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultHelper
open System

let fallibleConverterAccountCodeStringToAccountUuid = (fun codeString -> 
          result {
              // see if the string represents a valid code first
              let! _ = codeString |> AccountCode.create
              // now see if it matches an account ID
              return! match codeString |> LookupCache.accountCodeToId.fetch with
                      | Ok x -> Ok x
                      | Error (DalResultantRowsDidntMatchExpectation _) -> Error (AccountCodeDoesntMatchAccountId codeString)
                      | Error e -> Error e })

let fallibleConverterAccountCodeToAccountId = (fun code -> 
          result {
              let! uuid = code |> fallibleConverterAccountCodeStringToAccountUuid
              return uuid |> AccountId.fromGuid })

let ``convert AccountId to AccountCodeString``
        (id: AccountId)
        : Result<string, AppError> =
    id |> AccountId.value |> LookupCache.accountIdToCode.fetch 

let ``convert AccountId to AccountCode``
        (id: AccountId)
        : Result<AccountCode, AppError> =
    id |> ``convert AccountId to AccountCodeString`` |> Result.bind AccountCode.create

let ``convert AccountId Option to AccountCode Option``
        (idOption: AccountId option)
        : Result<AccountCode option, AppError> =
    let fallibleConverter =  (fun id ->
          id |> AccountId.value |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    idOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountId Option to AccountCodeString Option``
        (idOption: AccountId option)
        : Result<string option, AppError> =
    let code = idOption |> ``convert AccountId Option to AccountCode Option``
    match code with
    | Error e ->
        let originalType = idOption.GetType().Name
        let originalValue = match idOption with | None -> "None" | Some x -> x.ToString()
        let desiredType = "AccountCode string option"
        let childError = e |> AppError.toMessage
        Error (InterfaceBridgeConversionFailure (originalType, originalValue, desiredType, childError)) // REQ-NGUI-1.5
    | Ok x -> Ok (x |> Option.map(AccountCode.value))

let ``convert AccountCodeString Option to AccountUuidOption``
        (code: string option)
        : Result<Guid option, AppError> =
    match code with
    | Some x ->
        x
        |> LookupCache.accountCodeToId.fetch
        |> Result.mapError(fun e -> 
            let originalType = code.GetType().Name
            let originalValue = match code with | None -> "None" | Some x -> x.ToString()
            let desiredType = "Account UUID option"
            let childError = e |> AppError.toMessage
            InterfaceBridgeConversionFailure (originalType, originalValue, desiredType, childError)) // REQ-NGUI-1.5
        |> Result.map Some
    | None -> Ok None

let ``convert Account to AccountReturn`` (a:Account) : Result<AccountReturn, AppError> =
    result {
        let! parentCode = a |> parentId |> ``convert AccountId Option to AccountCodeString Option``
        let activityPeriod = a |> Account.activityPeriod
        let activeBegin = activityPeriod |> AccountActivityPeriod.activeBegin
        let activeEnd = activityPeriod |> AccountActivityPeriod.activeEnd
        return {
            code = AccountCode.value (code a)
            name = AccountName.value (accountName a)
            accountTypeSt = AccountType.toString (accountType a)
            activeBegin = activeBegin
            activeEnd = activeEnd
            subType = accountSubType a |> Option.map AccountSubtype.toString
            parentCode = parentCode
            reference = externalReference a |> Option.map AccountExternalReference.value
            createdAt = createdAt a
            modifiedAt = modifiedAt a } }

let ``convert AccountCodeString to Id`` (codeString:string) : Result<AccountId, AppError> =
    codeString |> fallibleConverterAccountCodeToAccountId

let ``convert AccountCodeString to Account``
            (transaction: DbTransaction option)
            (codeString:string)
            : Result<Account, AppError> =
    result {    let! accountId = codeString |> fallibleConverterAccountCodeToAccountId
                return! accountId |> fetchById transaction }

let ``convert AccountCodeString Option to AccountId Option``
        (codeStringOption: string option)
        : Result<AccountId option, AppError> = 
    match codeStringOption with
    | None -> Ok None
    | Some codeString -> result {
                let! accountId = codeString |> fallibleConverterAccountCodeToAccountId
                return (Some accountId) }

let ``convert AccountCodeString List to AccountId List``
        (codes: string list)
        : Result<AccountId list, AppError> =
    codes
    |> List.map (fun x -> x |> ``convert AccountCodeString to Id`` )
    |> convertListOfResultsToResultsList
    
let ``convert AccountUuId Option to AccountCode Option``
        (uuidOption: Guid option)
        : Result<AccountCode option, AppError> =
    let fallibleConverter =  (fun id ->
          id |> LookupCache.accountIdToCode.fetch |> Result.bind AccountCode.create)
    uuidOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountTypeString Option to AccountType Option``
    (stringOption: string option)
    : Result<AccountType option, AppError> =
    let fallibleConverter = (fun string -> string |> AccountType.fromString)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountSubtypeString Option to AccountSubtype Option``
    (stringOption: string option)
    : Result<AccountSubtype option, AppError> =
    let fallibleConverter = (fun string -> string |> AccountSubtype.fromString)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountBalance to AccountBalanceReturn``
        (balance : AccountBalance)
        : Result<AccountBalanceReturn, AppError> =
    result {
        let! codeString = balance.accountId |> ``convert AccountId to AccountCodeString``
        return {    accountCode = codeString
                    totalCredits =  balance.totalCredits |> Money.amount
                    totalDebits = balance.totalDebits |> Money.amount
                    netBalance = balance.netBalance |> Money.amount } }

let ``convert [Account Reference String Option] to [AccountExternalReference Option]``
    (stringOption: string option)
    : Result<AccountExternalReference option, AppError> =
    let fallibleConverter = (fun string -> string |> AccountExternalReference.create)
    stringOption
    |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter