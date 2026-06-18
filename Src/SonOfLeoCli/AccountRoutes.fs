module SonOfLeoCli.AccountRoutes

open Model.Audit
open Model.Ledger.Account.Account
open Model.Ledger.AccountComponent
open NodaTime
open Model.Ledger.Account
open Model.UI
open Utilities.ResultCE
open InterfaceContractTypes

let convertAccountToAccountReturn a : AccountReturn = {
            id = Some (Account.id a)
            code = AccountCode.value (Account.code a)
            name = AccountName.value (Account.name a)
            accountTypeSt = AccountType.toString (Account.accountType a)
            activeBegin = Account.activeBegin a
            activeEnd = Account.activeEnd a
            subType = Account.accountSubType a |> Option.map AccountSubtype.toString
            parentId = Account.parentId a
            reference = Account.externalReference a |> Option.map AccountExternalReference.value
            modifiedAt = Some (Account.modifiedAt a)
            createdAt = Some(Account.createdAt a)
        }
    
let accountCreate payload _ =
    result {
        let! accountCreateInput = Json.fromJson<AccountCreateInput> payload
        let envelope = AuditEnvelope.create AccountCreate
        let! account = Account.constructNewAndSaveToDb
                         accountCreateInput.code
                         accountCreateInput.name
                         accountCreateInput.accountTypeSt
                         accountCreateInput.activeBegin
                         accountCreateInput.activeEnd
                         accountCreateInput.subType
                         accountCreateInput.parentId
                         accountCreateInput.reference
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount
    }

let accountDeactivate payload _ =
    result {
        let! accountDeactivation = Json.fromJson<AccountDeactivationInput> payload
        let envelope = AuditEnvelope.create AccountDeactivation
        let! account = Account.deactivateAccount
                         accountDeactivation.id
                         (Some accountDeactivation.activeEnd)
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount
    }
    

let accountUpdateName payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateNameInput> payload
        let envelope = AuditEnvelope.create AccountUpdateName
        let! account = Account.updateAccountName
                         accountUpdate.id
                         accountUpdate.newName
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount
    }

let accountUpdateExternalReference payload _ =
    result {
        let! accountUpdate = Json.fromJson<AccountUpdateExternalReferenceInput> payload
        let envelope = AuditEnvelope.create AccountUpdateExtReference
        let! account = Account.updateExternalReference
                         accountUpdate.id
                         accountUpdate.newReference
                         envelope
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount
    }

let accountFetchById payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByIdInput> payload
        let! account = Account.fetchById accountFetch.id
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount
    }

let accountFetchByCode payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByCodeInput> payload
        let! account = Account.fetchByCode accountFetch.code
        let returnAccount : AccountReturn = convertAccountToAccountReturn account
        return! Json.toJson<AccountReturn> returnAccount
    }

let accountFetchByParentId payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByParentIdInput> payload
        let! accounts = Account.fetchByParentId accountFetch.parentId
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) //AccountReturn =  account
        return! Json.toJson<AccountReturn list> returnAccounts
    }

let accountFetchByAccountType payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchByAccountTypeInput> payload
        let! validType = AccountType.fromString accountFetch.accountTypeSt
        let! accounts = Account.fetchByAccountType validType
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) //AccountReturn =  account
        return! Json.toJson<AccountReturn list> returnAccounts
    }

let accountFetchAll payload _ =
    result {
        let! accountFetch = Json.fromJson<AccountFetchAllInput> payload
        let! accounts = Account.fetchAll accountFetch.activeOnly
        let returnAccounts = accounts |> List.map(convertAccountToAccountReturn) //AccountReturn =  account
        return! Json.toJson<AccountReturn list> returnAccounts
    }
    
let accountDomainCommandRoutes = [
    // create
    { domain = "Account"; verb = "Create"; description = "Create a new account and insert it into the database"
      inputType = typeof<AccountCreateInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountCreate }
    // read
    { domain = "Account"; verb = "FetchById"; description = "Returns the Account record matching the passed in UUID"
      inputType = typeof<AccountFetchByIdInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountFetchById }
    { domain = "Account"; verb = "FetchByCode"; description = "Returns the Account record matching the passed in account code string"
      inputType = typeof<AccountFetchByCodeInput>.Name; outputType = typeof<AccountReturn>.Name; handler =  accountFetchByCode }
    { domain = "Account"; verb = "FetchByParentId"; description = "Returns all Account records whose parentID matches the passed in UUID"
      inputType = typeof<AccountFetchByParentIdInput>.Name; outputType = typeof<AccountReturn list>.Name; handler =  accountFetchByParentId }
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
