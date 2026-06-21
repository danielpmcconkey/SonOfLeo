module SonOfLeoCli.AccountRoutes

open Model.Audit
open Model.Ledger.Account.Account
open Model.Ledger.AccountComponent
open Model.UI
open Utilities.ResultCE
open InterfaceContractTypes

let convertAccountToAccountReturn a : AccountReturn = {
            code = AccountCode.value (code a)
            name = AccountName.value (accountName a)
            accountTypeSt = AccountType.toString (accountType a)
            activeBegin = activeBegin a
            activeEnd = activeEnd a
            subType = accountSubType a |> Option.map AccountSubtype.toString
            parentCode = fetchCodeOptionByIdOption (parentId a) |> Result.defaultWith failwith
            reference = externalReference a |> Option.map AccountExternalReference.value
            createdAt = createdAt a
            modifiedAt = modifiedAt a
        }
    
let accountCreate payload _ =
    result {
        let! accountCreateInput = Json.fromJson<AccountCreateInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountCreate
        let! account = constructNewAndSaveToDbUsingParentCode
                         accountCreateInput.code
                         accountCreateInput.name
                         accountCreateInput.accountTypeSt
                         accountCreateInput.activeBegin
                         accountCreateInput.activeEnd
                         accountCreateInput.subType
                         accountCreateInput.parentCode
                         accountCreateInput.reference
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountDeactivate payload _ =
    result {
        let! accountDeactivation = Json.fromJson<AccountDeactivationInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountDeactivation
        let! account = deactivateAccountByCode
                         accountDeactivation.code
                         (Some accountDeactivation.activeEnd)
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }
    

let accountUpdateName payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateNameInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateName
        let! account = updateAccountNameByCode
                         accountUpdate.code
                         accountUpdate.newName
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountUpdateExternalReference payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateExternalReferenceInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create AccountUpdateExtReference
        let! account = updateExternalReferenceByCode
                         accountUpdate.code
                         accountUpdate.newReference
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountFetchByCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! account = fetchByCode accountFetch.code
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountFetchByParentCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByParentCodeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accounts = fetchByParentCode accountFetch.parentCode
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) 
        return! Json.toJson<AccountReturn list> returnAccounts// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountFetchByAccountType payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByAccountTypeInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! validType = AccountType.fromString accountFetch.accountTypeSt
        let! accounts = fetchByAccountType validType
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) 
        return! Json.toJson<AccountReturn list> returnAccounts// REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let accountFetchAll payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchAllInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! accounts = fetchAll accountFetch.activeOnly
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
