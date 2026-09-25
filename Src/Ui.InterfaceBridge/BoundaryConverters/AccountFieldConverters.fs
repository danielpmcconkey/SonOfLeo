module Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters

open System
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.CrossDomainOrchestration.AccountBalance
open Ui.InterfaceBridge
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts

let fallibleConverterAccountCodeStringToAccountUuid context codeString =
    result {
        // see if the string represents a valid code first
        let! _ = codeString |> AccountCode.create
        // now see if it matches an account ID
        return!
            match codeString |> LookupCache.accountCodeToId.fetch (context |> Context.getDatabaseTransaction) with
            | Ok x -> Ok x
            | Error e ->
                if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                then Error (LedgerError.AccountCodeDoesntMatchAccountId codeString)
                else Error e
    }

let fallibleConverterAccountCodeToAccountId context codeString =
    result {
        let! uuid = codeString |> fallibleConverterAccountCodeStringToAccountUuid context
        return uuid |> AccountId.fromGuid
    }

let ``convert AccountId to AccountCodeString`` (context: Context.Context) (id: AccountId) : Result<string, IAppError> =
    id |> AccountId.value |> LookupCache.accountIdToCode.fetch (context |> Context.getDatabaseTransaction)

let ``convert AccountId to AccountNameString`` (context: Context.Context) (id: AccountId) : Result<string, IAppError> =
    id |> AccountId.value |> LookupCache.accountIdToName.fetch (context |> Context.getDatabaseTransaction)

let ``convert AccountId to AccountCode`` (context: Context.Context) (id: AccountId) : Result<AccountCode, IAppError> =
    id |> ``convert AccountId to AccountCodeString`` context |> Result.bind AccountCode.create

let ``convert AccountId Option to AccountCode Option``
    (context: Context.Context)
    (idOption: AccountId option)
    : Result<AccountCode option, IAppError> =
    let fallibleConverter =
        (fun id ->
        id
        |> AccountId.value
        |> LookupCache.accountIdToCode.fetch (context |> Context.getDatabaseTransaction)
        |> Result.bind AccountCode.create)
    idOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountId Option to AccountCodeString Option``
    (context: Context.Context)
    (idOption: AccountId option)
    : Result<string option, IAppError> =
    let code = idOption |> ``convert AccountId Option to AccountCode Option`` context
    match code with
    | Error e ->
        let originalType = idOption.GetType().Name
        let originalValue =
            match idOption with
            | None -> "None"
            | Some x -> x.ToString()
        let desiredType = "AccountCode string option"
        let childError = e.ToMessage()
        Error(BridgeError.InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError))
    | Ok x -> Ok(x |> Option.map(AccountCode.value))
    
let ``convert [AccountId option] to [AccountName option]``
    (context: Context.Context)
    (idOption: AccountId option)
    : Result<AccountName option, IAppError> =
    match idOption with
    | None -> Ok None
    | Some accountId ->
        match accountId |> Account.fetchById context with
        | Error e -> Error e
        | Ok a -> a |> Account.accountName |> Some |> Ok
    
let ``convert [AccountId option] to [AccountName string option]``
    (context: Context.Context)
    (idOption: AccountId option)
    : Result<string option, IAppError> =
    match idOption with
    | None -> Ok None
    | Some accountId -> accountId |> ``convert AccountId to AccountNameString`` context |> Result.map Some
        

let ``convert AccountCodeString Option to AccountUuidOption``
    (context: Context.Context)
    (code: string option)
    : Result<Guid option, IAppError> =
    match code with
    | Some x ->
        x
        |> LookupCache.accountCodeToId.fetch (context |> Context.getDatabaseTransaction)
        |> Result.mapError(fun e ->
            let originalType = code.GetType().Name
            let originalValue =
                match code with
                | None -> "None"
                | Some x -> x.ToString()
            let desiredType = "Account UUID option"
            let childError = e.ToMessage()
            BridgeError.InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError)
            |> BridgeError.toAppError
            )
        |> Result.map Some
    | None -> Ok None

let ``convert Account to AccountReturn`` (context: Context.Context) (a: Account.Account) : Result<AccountReturn, IAppError> =
    result {
        let! parentCode = a |> Account.parentId |> ``convert AccountId Option to AccountCodeString Option`` context
        let activityPeriod = a |> Account.activityPeriod
        let activeBegin = activityPeriod |> ActivityPeriod.activeBegin
        let activeEnd = activityPeriod |> ActivityPeriod.activeEnd
        return
            { code = AccountCode.value(Account.code a)
              name = AccountName.value(Account.accountName a)
              accountTypeSt = AccountType.toString(Account.accountType a)
              activeBegin = activeBegin
              activeEnd = activeEnd
              subType = Account.accountSubType a |> Option.map AccountSubtype.toString
              parentCode = parentCode
              reference = Account.externalReference a |> Option.map AccountExternalReference.value
              createdAt = Account.createdAt a
              modifiedAt = Account.modifiedAt a }
    }

let ``convert AccountCodeString to Id`` (context: Context.Context) (codeString: string) : Result<AccountId, IAppError> =
    codeString |> fallibleConverterAccountCodeToAccountId context

let ``convert AccountCodeString to Account`` (context: Context.Context) (codeString: string) : Result<Account.Account, IAppError> =
    result {
        let! accountId = codeString |> fallibleConverterAccountCodeToAccountId context
        return! accountId |> Account.fetchById context
    }

let ``convert AccountCodeString Option to AccountId Option``
    (context: Context.Context)
    (codeStringOption: string option)
    : Result<AccountId option, IAppError> =
    match codeStringOption with
    | None -> Ok None
    | Some codeString ->
        result {
            let! accountId = codeString |> fallibleConverterAccountCodeToAccountId context
            return (Some accountId)
        }

let ``convert AccountCodeString List to AccountId List``
    (context: Context.Context)
    (codes: string list)
    : Result<AccountId list, IAppError> =
    codes
    |> List.map(fun x -> x |> ``convert AccountCodeString to Id`` context)
    |> convertListOfResultsToResultsList

let ``convert AccountUuId Option to AccountCode Option``
    (context: Context.Context)
    (uuidOption: Guid option)
    : Result<AccountCode option, IAppError> =
    let fallibleConverter =
        (fun id -> id |> LookupCache.accountIdToCode.fetch (context |> Context.getDatabaseTransaction) |> Result.bind AccountCode.create)
    uuidOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountTypeString Option to AccountType Option``
    (stringOption: string option)
    : Result<AccountType option, IAppError> =
    let fallibleConverter = (fun string -> string |> AccountType.fromString)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountSubtypeString Option to AccountSubtype Option``
    (stringOption: string option)
    : Result<AccountSubtype option, IAppError> =
    let fallibleConverter = (fun string -> string |> AccountSubtype.fromString)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter

let ``convert AccountBalance to AccountBalanceReturn``
    (context: Context.Context)
    (balance: AccountBalance)
    : Result<AccountBalanceReturn, IAppError> =
    result {
        let! codeString = balance.accountId |> ``convert AccountId to AccountCodeString`` context
        let! nameString = balance.accountId |> ``convert AccountId to AccountNameString`` context
        return
            { accountCode = codeString
              accountName = nameString
              totalCredits = balance.totalCredits |> Money.amount
              totalDebits = balance.totalDebits |> Money.amount
              netBalance = balance.netBalance |> Money.amount }
    }

let ``convert [Account Reference String Option] to [AccountExternalReference Option]``
    (stringOption: string option)
    : Result<AccountExternalReference option, IAppError> =
    let fallibleConverter = (fun string -> string |> AccountExternalReference.create)
    stringOption |> convertOptionToDesiredTypeWithFallibleConverter fallibleConverter
