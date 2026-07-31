module InterfaceBridge.Routes.AccountRoutes

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.OrchestrationConverters
open InterfaceBridge.InterfaceContracts.AccountContracts
open Logger.Audit
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator
open ModelOrchestrator.AccountActivity
open InterfaceBridge.Json
open InterfaceBridge.CommandRoute
open DataAccessLayer.DbTransaction
open Utilities.AppError
open Utilities.ResultHelper
open Context

let private accountCreate payload _ =
    let context = Context.create NoTransaction AccountCreate
    result {
        let! accountCreateInput = Json.fromJson<AccountCreateInput> payload
        let! code = accountCreateInput.code |> AccountCode.create
        let! name = accountCreateInput.name |> AccountName.create
        let! accountType = accountCreateInput.accountTypeSt |> AccountType.fromString
        let! accountActivityPeriod =
            AccountActivityPeriod.create accountCreateInput.activeBegin accountCreateInput.activeEnd
        let! subtype = accountCreateInput.subType |> ``convert AccountSubtypeString Option to AccountSubtype Option``
        let! parentId =
            accountCreateInput.parentCode
            |> ``convert AccountCodeString Option to AccountId Option`` context
            |> function
                | Ok x -> Ok x
                | Error(AccountCodeDoesntMatchAccountId _) ->
                    Error(AccountParentCodeInvalid(accountCreateInput.parentCode |> Option.defaultValue "None"))
                | Error e -> Error e
        let! reference =
            accountCreateInput.reference
            |> ``convert [Account Reference String Option] to [AccountExternalReference Option]``
        let! account =
            AccountCreation.constructNewAndSaveToDb
                context
                code
                name
                accountType
                accountActivityPeriod
                subtype
                parentId
                reference
        let! returnAccount = account |> ``convert Account to AccountReturn`` context
        return! Json.toJson<AccountReturn> returnAccount
    }

let private accountDeactivate payload _ =
    let context = Context.create NoTransaction AccountDeactivate
    result {
        let! accountDeactivationInput = Json.fromJson<AccountDeactivationInput> payload
        let! account = accountDeactivationInput.code |> ``convert AccountCodeString to Account`` context
        let! deactivatedAccount =
            account |> AccountDeactivation.deactivateAccount context accountDeactivationInput.activeEnd
        let! returnAccount = ``convert Account to AccountReturn`` context deactivatedAccount
        return! Json.toJson<AccountReturn> returnAccount
    }

let private accountUpdateName payload _ =
    let context = Context.create NoTransaction AccountUpdateName
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateNameInput> payload
        let! id = accountUpdate.code |> ``convert AccountCodeString to Id`` context
        let! updatedAccount = updateAccountNameById context id accountUpdate.newName
        let! returnAccount = ``convert Account to AccountReturn`` context updatedAccount
        return! Json.toJson<AccountReturn> returnAccount
    }

let private accountUpdateExternalReference payload _ =
    let context = Context.create NoTransaction AccountUpdateExtReference
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateExternalReferenceInput> payload
        let! id = accountUpdate.code |> ``convert AccountCodeString to Id`` context
        let! updatedAccount = updateExternalReferenceById context id accountUpdate.newReference
        let! returnAccount = ``convert Account to AccountReturn`` context updatedAccount
        return! Json.toJson<AccountReturn> returnAccount
    }

let private accountFetchByCode payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! accountFetch = Json.fromJson<AccountFetchByCodeInput> payload
        let! id = accountFetch.code |> ``convert AccountCodeString to Id`` context
        let! account = fetchById context id
        let! returnAccount = ``convert Account to AccountReturn`` context account
        return! Json.toJson<AccountReturn> returnAccount
    }

let private accountFetchByParentCode payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! accountFetch = Json.fromJson<AccountFetchByParentCodeInput> payload
        let! parentId = accountFetch.parentCode |> ``convert AccountCodeString to Id`` context
        let! accounts = parentId |> fetchByParentId context
        let! returnAccounts =
            accounts
            |> List.map(``convert Account to AccountReturn`` context)
            |> convertListOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts
    }

let private accountFetchByAccountType payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! accountFetch = Json.fromJson<AccountFetchByAccountTypeInput> payload
        let! validType = AccountType.fromString accountFetch.accountTypeSt
        let! accounts = fetchByAccountType context validType
        let! returnAccounts =
            accounts
            |> List.map(``convert Account to AccountReturn`` context)
            |> convertListOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts
    }

let private accountFetchAll payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! accountFetch = Json.fromJson<AccountFetchAllInput> payload
        let! accounts = fetchAll context accountFetch.activeOnly
        let! returnAccounts =
            accounts
            |> List.map(``convert Account to AccountReturn`` context)
            |> convertListOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts
    }

let private accountActivityFetch payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<AccountActivityFetchInput> payload
        let! filter = input.filter |> ``convert AccountActivityFilterInput to AccountActivityFilter`` context
        let! fetched = fetchFiltered context filter input.sort
        let! returnList = fetched |> ``convert AccountActivity List to AccountActivityReturn List`` context
        return! returnList |> Json.toJson<AccountActivityReturn list>
    }

let private accountBalancesFetch payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<AccountBalanceFetchByAccountListInput> payload
        let! accountList = input.codes |> ``convert AccountCodeString List to AccountId List`` context
        let! accountBalances = AccountBalance.fetchByAccountIdList context accountList input.asOf
        let! returnList =
            accountBalances
            |> List.map(fun accountBalance ->
                accountBalance |> ``convert AccountBalance to AccountBalanceReturn`` context)
            |> convertListOfResultsToResultsList
        return! returnList |> Json.toJson<AccountBalanceReturn list>
    }

let accountDomainCommandRoutes: CommandRoute list =
    [
      // create
      { domain = "Account"
        verb = "Create"
        description = "Create a new account and insert it into the database"
        inputType = typeof<AccountCreateInput>.Name
        outputType = typeof<AccountReturn>.Name
        handler = accountCreate }
      // read
      { domain = "Account"
        verb = "FetchByCode"
        description = "Returns the Account record matching the passed in account code string"
        inputType = typeof<AccountFetchByCodeInput>.Name
        outputType = typeof<AccountReturn>.Name
        handler = accountFetchByCode }
      { domain = "Account"
        verb = "FetchByParentCode"
        description = "Returns all Account records whose parent account matches the passed in account code"
        inputType = typeof<AccountFetchByParentCodeInput>.Name
        outputType = typeof<AccountReturn list>.Name
        handler = accountFetchByParentCode }
      { domain = "Account"
        verb = "FetchByAccountType"
        description =
          "Returns all Account records whose account type parameter matches the passed in account type string"
        inputType = typeof<AccountFetchByAccountTypeInput>.Name
        outputType = typeof<AccountReturn list>.Name
        handler = accountFetchByAccountType }
      { domain = "Account"
        verb = "FetchAll"
        description =
          "Returns all Account records. If activeOnly is true, it filters on account records currently active (referencing system run time)"
        inputType = typeof<AccountFetchAllInput>.Name
        outputType = typeof<AccountReturn list>.Name
        handler = accountFetchAll }
      { domain = "Account"
        verb = "FetchActivity"
        description = "Returns Account records and associated JE activity for a given filter"
        inputType = typeof<AccountActivityFetchInput>.Name
        outputType = typeof<AccountActivityReturn list>.Name
        handler = accountActivityFetch }
      { domain = "Account"
        verb = "FetchBalances"
        description =
          "Returns Account records and their aggregate balances. Optional as-of date produces a balance as it would've been at the end of the as-of date"
        inputType = typeof<AccountBalanceFetchByAccountListInput>.Name
        outputType = typeof<AccountBalanceReturn list>.Name
        handler = accountBalancesFetch }
      // update
      { domain = "Account"
        verb = "Deactivate"
        description = "Updates an account's active end parameter to a specific instant"
        inputType = typeof<AccountDeactivationInput>.Name
        outputType = typeof<AccountReturn>.Name
        handler = accountDeactivate }
      { domain = "Account"
        verb = "UpdateName"
        description = "Updates an account's name"
        inputType = typeof<AccountUpdateNameInput>.Name
        outputType = typeof<AccountReturn>.Name
        handler = accountUpdateName }
      { domain = "Account"
        verb = "UpdateExternalReference"
        description = "Updates an account's external reference (or sets it to null)"
        inputType = typeof<AccountUpdateExternalReferenceInput>.Name
        outputType = typeof<AccountReturn>.Name
        handler = accountUpdateExternalReference } ]
