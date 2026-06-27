module SonOfLeoCli.AccountRoutes

open Model
open Model.Audit
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open Model.UI
open Utilities.ResultCE
open InterfaceContractTypes

let private convertAccountToAccountReturn a : AccountReturn = {
            code = AccountCode.value (code a)
            name = AccountName.value (accountName a)
            accountTypeSt = AccountType.toString (accountType a)
            activeBegin = activeBegin a
            activeEnd = activeEnd a
            subType = accountSubType a |> Option.map AccountSubtype.toString
            parentCode =
                match parentId a with
                | None -> None
                | Some x -> x |> LookupCache.accountIdToCode.fetch |> Result.defaultWith failwith |> Some
            reference = externalReference a |> Option.map AccountExternalReference.value
            createdAt = createdAt a
            modifiedAt = modifiedAt a
        }
    
let private accountCreate payload _ =
    result {
        let! accountCreateInput = Json.fromJson<AccountCreateInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountCreate
        let! parentId =
                match accountCreateInput.parentCode with
                | Some x -> x |> LookupCache.accountCodeToId.fetch |> Result.map Some
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
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountDeactivate payload _ =
    result {
        let! accountDeactivation = Json.fromJson<AccountDeactivationInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountDeactivation
        let! id = accountDeactivation.code |> LookupCache.accountCodeToId.fetch
        let! account = id |> ModelOrchestrator.AccountDeactivation.deactivateAccountById
                         (Some accountDeactivation.activeEnd)
                         envelope
                         None
                         
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }
    

let private accountUpdateName payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateNameInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateName
        let! id = accountUpdate.code |> LookupCache.accountCodeToId.fetch
        let! account = updateAccountNameById
                         id
                         accountUpdate.newName
                         envelope
                         None
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountUpdateExternalReference payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateExternalReferenceInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateExtReference
        let! id = accountUpdate.code |> LookupCache.accountCodeToId.fetch
        let! account = updateExternalReferenceById
                         id
                         accountUpdate.newReference
                         envelope
                         None
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountFetchByCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! account = fetchByCode None accountFetch.code
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountFetchByParentCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByParentCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! parentId = accountFetch.parentCode |> LookupCache.accountCodeToId.fetch
        let! accounts = parentId |> fetchByParentId None
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) 
        return! Json.toJson<AccountReturn list> returnAccounts// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountFetchByAccountType payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByAccountTypeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! validType = AccountType.fromString accountFetch.accountTypeSt
        let! accounts = fetchByAccountType None validType
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) 
        return! Json.toJson<AccountReturn list> returnAccounts// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private accountFetchAll payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchAllInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accounts = fetchAll accountFetch.activeOnly None
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn)
        return! Json.toJson<AccountReturn list> returnAccounts// REQ-NGUI-2.4, REQ-NGUI-3.5
    }
    
let accountDomainCommandRoutes = [
    // create
    { domain = "Account"; verb = "Create"; description = "Create a new account and insert it into the database"
      inputType = typeof<AccountCreateInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountCreate }
    // read
    { domain = "Account"; verb = "FetchByCode"; description = "Returns the Account record matching the passed in account code string"
      inputType = typeof<AccountFetchByCodeInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountFetchByCode }
    { domain = "Account"; verb = "FetchByParentCode"; description = "Returns all Account records whose parent account matches the passed in account code"
      inputType = typeof<AccountFetchByParentCodeInput>.Name; outputType = typeof<AccountReturn list>.Name; handler =  accountFetchByParentCode }
    { domain = "Account"; verb = "FetchByAccountType"; description = "Returns all Account records whose account type parameter matches the passed in account type string"
      inputType = typeof<AccountFetchByAccountTypeInput>.Name; outputType = typeof<AccountReturn list>.Name; handler =  accountFetchByAccountType }
    { domain = "Account"; verb = "FetchAll"; description = "Returns all Account records. If activeOnly is true, it filters on account records currently active (referencing system run time)"
      inputType = typeof<AccountFetchAllInput>.Name; outputType = typeof<AccountReturn list>.Name; handler =  accountFetchAll }    
    // update
    { domain = "Account"; verb = "Deactivate"; description = "Updates an account's active end parameter to a specific instant"
      inputType = typeof<AccountDeactivationInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountDeactivate }
    { domain = "Account"; verb = "UpdateName"; description = "Updates an account's name"
      inputType = typeof<AccountUpdateNameInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountUpdateName }
    { domain = "Account"; verb = "UpdateExternalReference"; description = "Updates an account's external reference (or sets it to null)"
      inputType = typeof<AccountUpdateExternalReferenceInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountUpdateExternalReference }
]
