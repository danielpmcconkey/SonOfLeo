module SonOfLeoCli.AccountRoutes

open Model
open Model.Audit
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open Model.UI
open Model.UI.BoundaryConverters.AccountFieldConverters
open Model.UI.BoundaryConverters.JournalEntryFieldConverters
open Model.UI.BoundaryConverters.MoneyFieldConverters
open ModelOrchestrator
open ModelOrchestrator.AccountActivity
open Utilities
open Utilities.ResultCE
open InterfaceContractTypes

let private convertAccountToAccountReturn a : Result<AccountReturn, string> =
    result {
        let! parentCode =
            match parentId a with
            | None -> Ok None
            | Some x ->
                x
                |> AccountId.value
                |> LookupCache.accountIdToCode.fetch
                |> Result.mapError (fun e -> $"Parent ID returned {x} didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
                |> Result.map Some
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
    
let private accountCreate payload _ =
    result {
        let! accountCreateInput = Json.fromJson<AccountCreateInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountCreate
        let! parentId =
                match accountCreateInput.parentCode with
                | Some x ->
                    x
                    |> LookupCache.accountCodeToId.fetch
                    |> Result.mapError(fun e -> $"Parent code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
                    |> Result.map Some
                | None -> Ok None
        let! account = constructNewAndSaveToDb
                         accountCreateInput.code
                         accountCreateInput.name
                         accountCreateInput.accountTypeSt
                         accountCreateInput.activeBegin
                         accountCreateInput.activeEnd
                         accountCreateInput.subType
                         parentId
                         accountCreateInput.reference
                         envelope
                         None
        let! returnAccount = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountDeactivate payload _ =
    result {
        let! accountDeactivation = Json.fromJson<AccountDeactivationInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountDeactivation
        let! id =
            accountDeactivation.code
            |> LookupCache.accountCodeToId.fetch
            |> Result.mapError(fun e -> $"Account code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        let! account =
            id
            |> AccountId.fromGuid
            |> ModelOrchestrator.AccountDeactivation.deactivateAccountById
                 (Some accountDeactivation.activeEnd)
                 envelope
                 None
        let! returnAccount = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5
    

let private accountUpdateName payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateNameInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateName
        let! idGuid =
            accountUpdate.code
            |> LookupCache.accountCodeToId.fetch
            |> Result.mapError(fun e -> $"Account code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        let id = idGuid |> AccountId.fromGuid
        let! account = updateAccountNameById id accountUpdate.newName envelope None
        let! returnAccount = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountUpdateExternalReference payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateExternalReferenceInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateExtReference
        let! idGuid =
            accountUpdate.code
            |> LookupCache.accountCodeToId.fetch
            |> Result.mapError(fun e -> $"Account code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        let id = idGuid |> AccountId.fromGuid
        let! account = updateExternalReferenceById
                         id
                         accountUpdate.newReference
                         envelope
                         None
        let! returnAccount = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchByCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! idGuid =
            accountFetch.code
            |> LookupCache.accountCodeToId.fetch
            |> Result.mapError(fun e -> $"Account code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        let id = idGuid |> AccountId.fromGuid
        let! account = fetchById None id
        let! returnAccount = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchByParentCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByParentCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! parentIdGuid =
            accountFetch.parentCode
            |> LookupCache.accountCodeToId.fetch
            |> Result.mapError(fun e -> $"Parent code provided didn't match any recorded Accounts in the database. Further details: {e}") // REQ-NGUI-1.5
        let parentId = parentIdGuid |> AccountId.fromGuid
        let! accounts = parentId |> fetchByParentId None
        let! returnAccounts = accounts |> List.map(convertAccountToAccountReturn) |> ListHelper.listOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchByAccountType payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByAccountTypeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! validType = AccountType.fromString accountFetch.accountTypeSt
        let! accounts = fetchByAccountType None validType
        let! returnAccounts = accounts |> List.map(convertAccountToAccountReturn) |> ListHelper.listOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountFetchAll payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchAllInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accounts = fetchAll accountFetch.activeOnly None
        let! returnAccounts = accounts |> List.map(convertAccountToAccountReturn) |> ListHelper.listOfResultsToResultsList
        return! Json.toJson<AccountReturn list> returnAccounts } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private accountActivityFetch payload _ =
    result {
        let! input = Json.fromJson<AccountActivityFetchInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! fetched = fetchFiltered None filter sort
        let! returnList = 
            fetched
            |> List.map (fun x ->
                result {
                    let! parentCodeOption = x.accountParentId |> convertAccountIdOptionToAccountCodeOption // REQ-NGUI-1.5
                    let detail = x.activityDetail |> Option.map(fun d -> {
                        lineId = d.lineId; amount = d.amount |> Money.amount; lineType = d.lineType; lineMemo = d.lineMemo
                        lineCreatedAt = d.lineCreatedAt; lineModifiedAt = d.lineModifiedAt; journalEntryId = d.journalEntryId
                        entryDate = d.entryDate; journalEntryDescription = d.journalEntryDescription
                        journalEntrySource = d.journalEntrySource; journalEntryVoidedAt = d.journalEntryVoidedAt; })
                    return {  accountCode = x.accountCode; accountName = x.accountName; accountType = x.accountType
                              accountSubtype = x.accountSubtype; accountParentCode = parentCodeOption
                              accountExternalRef = x.accountExternalRef; activityDetail = detail }
                } )
            |> ListHelper.listOfResultsToResultsList
        return! returnList |> Json.toJson<AccountActivityReturn list> // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountBalancesFetch payload _ = // REQ-JE-3.6
    result {
        let! input = Json.fromJson<AccountBalanceFetchByAccountListInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accountList = // REQ-NGUI-1.5
            input.codes
            |> List.map (fun x ->
                result { let! accountIdGuid =
                            x
                            |> LookupCache.accountCodeToId.fetch
                            |> Result.mapError(fun e -> $"Account Code {x} didn't match any recorded Accounts in the database. Further details: {e}")
                         return accountIdGuid |> AccountId.fromGuid } )
            |> ListHelper.listOfResultsToResultsList
        let! accountBalances = AccountBalance.fetchByAccountIdList None accountList input.asOf
        let! returnList =
            accountBalances
            |> List.map (fun accountBalance ->
                let codeResult = accountBalance.accountId |> AccountId.value |> LookupCache.accountIdToCode.fetch
                match codeResult with
                | Error _ -> Error $"The returned account ID {accountBalance.accountId} does not match any database rows." // REQ-NGUI-1.5
                | Ok c -> Ok {  accountCode = c
                                totalCredits =  accountBalance.totalCredits |> Money.amount
                                totalDebits = accountBalance.totalDebits |> Money.amount
                                netBalance = accountBalance.netBalance |> Money.amount } )
            |> ListHelper.listOfResultsToResultsList
        return! returnList |> Json.toJson<AccountBalanceReturn list> // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountDomainCommandRoutes = [
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
