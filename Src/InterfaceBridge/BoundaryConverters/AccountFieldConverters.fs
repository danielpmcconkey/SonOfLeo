module InterfaceBridge.BoundaryConverters.AccountFieldConverters

open Context.Context
open InterfaceBridge.InterfaceContracts.AccountContracts
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.AccountBalance
open Utilities.AppError
open DataAccessLayer.DbTransaction
open Utilities.ResultHelper
open System

let fallibleConverterAccountCodeStringToAccountUuid tran codeString =
    result {
        // see if the string represents a valid code first
        let! _ = codeString |> AccountCode.create
        // now see if it matches an account ID
        return!
            match codeString |> LookupCache.accountCodeToId.fetch tran with
            | Ok x -> Ok x
            | Error(DalResultantRowsDidntMatchExpectation _) -> Error(AccountCodeDoesntMatchAccountId codeString)
            | Error e -> Error e
    }

let fallibleConverterAccountCodeToAccountId tran codeString =
    result {
        let! uuid = codeString |> fallibleConverterAccountCodeStringToAccountUuid tran
        return uuid |> AccountId.fromGuid
    }

let ``convert AccountId to AccountCodeString`` (context: Context) (id: AccountId) : Result<string, AppError> =
    id |> AccountId.value |> LookupCache.accountIdToCode.fetch tran

let ``convert AccountId to AccountCode`` (context: Context) (id: AccountId) : Result<AccountCode, AppError> =
    id |> ``convert AccountId to AccountCodeString`` tran |> Result.bind AccountCode.create

let ``convert AccountId Option to AccountCode Option``
    (context: Context)
    (idOption: AccountId option)
    : Result<AccountCode option, AppError> =
    let fallibleConverter =
        (fun id -> id |> AccountId.value |> LookupCache.accountIdToCode.fetch tran |> Result.bind AccountCode.create)
    idOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountId Option to AccountCodeString Option``
    (context: Context)
    (idOption: AccountId option)
    : Result<string option, AppError> =
    let code = idOption |> ``convert AccountId Option to AccountCode Option`` tran
    match code with
    | Error e ->
        let originalType = idOption.GetType().Name
        let originalValue =
            match idOption with
            | None -> "None"
            | Some x -> x.ToString()
        let desiredType = "AccountCode string option"
        let childError = e |> AppError.toMessage
        Error(InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError)) // REQ-NGUI-1.5
    | Ok x -> Ok(x |> Option.map(AccountCode.value))

let ``convert AccountCodeString Option to AccountUuidOption``
    (context: Context)
    (code: string option)
    : Result<Guid option, AppError> =
    match code with
    | Some x ->
        x
        |> LookupCache.accountCodeToId.fetch tran
        |> Result.mapError(fun e ->
            let originalType = code.GetType().Name
            let originalValue =
                match code with
                | None -> "None"
                | Some x -> x.ToString()
            let desiredType = "Account UUID option"
            let childError = e |> AppError.toMessage
            InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError)) // REQ-NGUI-1.5
        |> Result.map Some
    | None -> Ok None

let ``convert Account to AccountReturn`` (context: Context) (a: Account) : Result<AccountReturn, AppError> =
    result {
        let! parentCode = a |> parentId |> ``convert AccountId Option to AccountCodeString Option`` tran
        let activityPeriod = a |> Account.activityPeriod
        let activeBegin = activityPeriod |> AccountActivityPeriod.activeBegin
        let activeEnd = activityPeriod |> AccountActivityPeriod.activeEnd
        return
            { code = AccountCode.value(code a)
              name = AccountName.value(accountName a)
              accountTypeSt = AccountType.toString(accountType a)
              activeBegin = activeBegin
              activeEnd = activeEnd
              subType = accountSubType a |> Option.map AccountSubtype.toString
              parentCode = parentCode
              reference = externalReference a |> Option.map AccountExternalReference.value
              createdAt = createdAt a
              modifiedAt = modifiedAt a }
    }

let ``convert AccountCodeString to Id`` (context: Context) (codeString: string) : Result<AccountId, AppError> =
    codeString |> fallibleConverterAccountCodeToAccountId tran

let ``convert AccountCodeString to Account`` (context: Context) (codeString: string) : Result<Account, AppError> =
    result {
        let! accountId = codeString |> fallibleConverterAccountCodeToAccountId tran
        return! accountId |> fetchById tran
    }

let ``convert AccountCodeString Option to AccountId Option``
    (context: Context)
    (codeStringOption: string option)
    : Result<AccountId option, AppError> =
    match codeStringOption with
    | None -> Ok None
    | Some codeString ->
        result {
            let! accountId = codeString |> fallibleConverterAccountCodeToAccountId tran
            return (Some accountId)
        }

let ``convert AccountCodeString List to AccountId List``
    (context: Context)
    (codes: string list)
    : Result<AccountId list, AppError> =
    codes
    |> List.map(fun x -> x |> ``convert AccountCodeString to Id`` tran)
    |> convertListOfResultsToResultsList

let ``convert AccountUuId Option to AccountCode Option``
    (context: Context)
    (uuidOption: Guid option)
    : Result<AccountCode option, AppError> =
    let fallibleConverter =
        (fun id -> id |> LookupCache.accountIdToCode.fetch tran |> Result.bind AccountCode.create)
    uuidOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountTypeString Option to AccountType Option``
    (stringOption: string option)
    : Result<AccountType option, AppError> =
    let fallibleConverter = (fun string -> string |> AccountType.fromString)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountSubtypeString Option to AccountSubtype Option``
    (stringOption: string option)
    : Result<AccountSubtype option, AppError> =
    let fallibleConverter = (fun string -> string |> AccountSubtype.fromString)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountBalance to AccountBalanceReturn``
    (context: Context)
    (balance: AccountBalance)
    : Result<AccountBalanceReturn, AppError> =
    result {
        let! codeString = balance.accountId |> ``convert AccountId to AccountCodeString`` tran
        return
            { accountCode = codeString
              totalCredits = balance.totalCredits |> Money.amount
              totalDebits = balance.totalDebits |> Money.amount
              netBalance = balance.netBalance |> Money.amount }
    }

let ``convert [Account Reference String Option] to [AccountExternalReference Option]``
    (stringOption: string option)
    : Result<AccountExternalReference option, AppError> =
    let fallibleConverter = (fun string -> string |> AccountExternalReference.create)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter
