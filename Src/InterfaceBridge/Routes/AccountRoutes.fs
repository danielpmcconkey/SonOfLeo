module InterfaceBridge.Routes.AccountRoutes

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.OrchestrationConverters
open InterfaceBridge.InterfaceContracts.AccountContracts
open Model.Audit
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator
open ModelOrchestrator.AccountActivity
open InterfaceBridge.Json
open InterfaceBridge.CommandRoute
open Utilities.ResultHelper
    
let private accountCreate payload _ =
    result {
        let! accountCreateInput = Json.fromJson<AccountCreateInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountCreate
        let! code = accountCreateInput.code |> AccountCode.create
        let! name = accountCreateInput.name |> AccountName.create
        let! accountType = accountCreateInput.accountTypeSt |> AccountType.fromString
        let! accountActivityPeriod = AccountActivityPeriod.create accountCreateInput.activeBegin accountCreateInput.activeEnd
        let! subtype = accountCreateInput.subType |> ``convert AccountSubtypeString Option to AccountSubtype Option``
        let! parentId = accountCreateInput.parentCode |> ``convert AccountCodeString Option to AccountId Option``
        let! reference = accountCreateInput.reference |> ``convert [Account Reference String Option] to [AccountExternalReference Option]``
        let! account = AccountCreation.constructNewAndSaveToDb
                         code
                         name
                         accountType
                         accountActivityPeriod
                         subtype
                         parentId
                         reference
                         envelope
                         None
        let! returnAccount = account |> ``convert Account to AccountReturn``
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountDeactivate payload _ =
    let envelope = AuditEnvelope.create AccountDeactivation
    result {
        let! accountDeactivationInput = Json.fromJson<AccountDeactivationInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        
        let! account = accountDeactivationInput.code |> ``convert AccountCodeString to Account`` None
        let! deactivatedAccount = account |> AccountDeactivation.deactivateAccount None envelope accountDeactivationInput.activeEnd
        let! returnAccount = ``convert Account to AccountReturn`` deactivatedAccount
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountUpdateName payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateNameInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateName
        let! id = accountUpdate.code |> ``convert AccountCodeString to Id``
        let! updatedAccount = updateAccountNameById id accountUpdate.newName envelope None
        let! returnAccount = ``convert Account to AccountReturn`` updatedAccount
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountUpdateExternalReference payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateExternalReferenceInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateExtReference
        let! id = accountUpdate.code |> ``convert AccountCodeString to Id``
        let! updatedAccount = updateExternalReferenceById
                                 id
                                 accountUpdate.newReference
                                 envelope
                                 None
        let! returnAccount = ``convert Account to AccountReturn`` updatedAccount
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchByCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! id = accountFetch.code |> ``convert AccountCodeString to Id``
        let! account = fetchById None id
        let! returnAccount = ``convert Account to AccountReturn`` account
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchByParentCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByParentCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! parentId = accountFetch.parentCode |> ``convert AccountCodeString to Id``
        let! accounts = parentId |> fetchByParentId None
        let! returnAccounts = accounts |> List.map(``convert Account to AccountReturn``) |> convertListOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchByAccountType payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByAccountTypeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! validType = AccountType.fromString accountFetch.accountTypeSt
        let! accounts = fetchByAccountType None validType
        let! returnAccounts = accounts |> List.map(``convert Account to AccountReturn``) |> convertListOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchAll payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchAllInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accounts = fetchAll accountFetch.activeOnly None
        let! returnAccounts = accounts |> List.map(``convert Account to AccountReturn``) |> convertListOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountActivityFetch payload _ =
    result {
        let! input = Json.fromJson<AccountActivityFetchInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! filter = input.filter |> ``convert AccountActivityFilterInput to AccountActivityFilter``
        let! fetched = fetchFiltered None filter input.sort
        let! returnList = fetched |> ``convert AccountActivity List to AccountActivityReturn List``
        return! returnList |> Json.toJson<AccountActivityReturn list> } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountBalancesFetch payload _ = // REQ-JE-3.6
    result {
        let! input = Json.fromJson<AccountBalanceFetchByAccountListInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accountList =  input.codes |> ``convert AccountCodeString List to AccountId List`` // REQ-NGUI-1.5
        let! accountBalances = AccountBalance.fetchByAccountIdList None accountList input.asOf
        let! returnList =
            accountBalances
            |> List.map (fun accountBalance -> accountBalance |> ``convert AccountBalance to AccountBalanceReturn`` )
            |> convertListOfResultsToResultsList
        return! returnList |> Json.toJson<AccountBalanceReturn list> } // REQ-NGUI-2.4, REQ-NGUI-3.5

let accountDomainCommandRoutes : CommandRoute list = [
    // create
    { domain = "Account"; verb = "Create"; description = "Create a new account and insert it into the database"
      inputType = typeof<AccountCreateInput>.Name; outputType = typeof<AccountReturn>.Name; handler = accountCreate }
    // read
    { domain = "Account"; verb = "FetchByCode"; description = "Returns the Account record matching the passed in account code string"
      inputType = typeof<AccountFetchByCodeInput>.Name; outputType = typeof<AccountReturn>.Name; handler = accountFetchByCode }
    { domain = "Account"; verb = "FetchByParentCode"; description = "Returns all Account records whose parent account matches the passed in account code"
      inputType = typeof<AccountFetchByParentCodeInput>.Name; outputType = typeof<AccountReturn list>.Name; handler = accountFetchByParentCode }
    { domain = "Account"; verb = "FetchByAccountType"; description = "Returns all Account records whose account type parameter matches the passed in account type string"
      inputType = typeof<AccountFetchByAccountTypeInput>.Name; outputType = typeof<AccountReturn list>.Name; handler = accountFetchByAccountType }
    { domain = "Account"; verb = "FetchAll"; description = "Returns all Account records. If activeOnly is true, it filters on account records currently active (referencing system run time)"
      inputType = typeof<AccountFetchAllInput>.Name; outputType = typeof<AccountReturn list>.Name; handler = accountFetchAll }
    { domain = "Account"; verb = "FetchActivity"; description = "Returns Account records and associated JE activity for a given filter"
      inputType = typeof<AccountActivityFetchInput>.Name; outputType = typeof<AccountActivityReturn list>.Name; handler = accountActivityFetch }
    { domain = "Account"; verb = "FetchBalances"; description = "Returns Account records and their aggregate balances. Optional as-of date produces a balance as it would've been at the end of the as-of date"
      inputType = typeof<AccountBalanceFetchByAccountListInput>.Name; outputType = typeof<AccountBalanceReturn list>.Name; handler = accountBalancesFetch }
    // update
    { domain = "Account"; verb = "Deactivate"; description = "Updates an account's active end parameter to a specific instant"
      inputType = typeof<AccountDeactivationInput>.Name; outputType = typeof<AccountReturn>.Name; handler = accountDeactivate }
    { domain = "Account"; verb = "UpdateName"; description = "Updates an account's name"
      inputType = typeof<AccountUpdateNameInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountUpdateName }
    { domain = "Account"; verb = "UpdateExternalReference"; description = "Updates an account's external reference (or sets it to null)"
      inputType = typeof<AccountUpdateExternalReferenceInput>.Name; outputType = typeof<AccountReturn>.Name; handler = accountUpdateExternalReference }
]
