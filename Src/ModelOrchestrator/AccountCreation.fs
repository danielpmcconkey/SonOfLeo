module ModelOrchestrator.AccountCreation

open Model.Audit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.Account
open Model.Ledger.Accounts.AccountComponent
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.DAL
open Utilities.ResultCE
    
let private confirmParentAccountIsActive
        (parentAccount: Account)
        (referenceDate: LocalDate)
        : Result<unit, AppError> =
    match parentAccount |> activityPeriod |> AccountActivityPeriod.isActive referenceDate with
    | true -> Ok ()
    | false -> Error (AccountParentIsInactive (parentAccount |> accountId |> AccountId.value))

let private confirmParentAndChildAccountTypesMatch
        (parentAccountType: AccountType)
        (childAccountType: AccountType)
        : Result<unit, AppError> =
    match parentAccountType = childAccountType with
    | true -> Ok ()
    | false ->
        Error (AccountParentAndChildTypesDontMatch
                   (parentAccountType |> AccountType.toString,
                    childAccountType |> AccountType.toString)) // REQ-AC-2.20

let private confirmParentAndChildAreDistinct
        (parentId: AccountId option)
        (childId: AccountId)
        : Result<unit, AppError> =
    match parentId with
    | None -> Ok ()
    | Some x when x = childId-> Error (AccountParentAndChildAreSame (parentId |> Option.map AccountId.value, childId |> AccountId.value))
    | _ -> Ok ()

let private validateParentChildRelationship
        (transaction: DbTransaction option)
        (parentId: AccountId option)
        (childId: AccountId)
        (childType: AccountType)
        (referenceDate: LocalDate)
        : Result<unit, AppError> =
    (*
     * REQ-AC-2.16
     * Note, this function no longer validates against circular ancestry. Since the child
     * ID is always created at the DB insertion, it is impossible for a newly created child
     * to already have descendents. And, since requirement REQ-AC-4.22 explicitly forbids
     * reparenting an account, there is no "legal" vector for a circular ancestry chain to
     * come into being.  
    *)
    match parentId with
    | None -> Ok ()
    | Some someParentId -> result {
            let! validParent = someParentId |> fetchById transaction // REQ-AC-2.6,
            let parentType = validParent |> accountType
            do! confirmParentAccountIsActive validParent referenceDate // REQ-AC-2.7
            do! confirmParentAndChildAccountTypesMatch parentType childType
            do! confirmParentAndChildAreDistinct parentId childId
            return () }
        
let confirmTypeAndSubtypeAreValid
        (accountType: AccountType)
        (subType: AccountSubtype option)
        : Result<unit, AppError> =
    if AccountSubtype.validTypeSubtypeCombination accountType subType
    then Ok ()
    else Error (AccountInvalidTypeSubtypeCombo (accountType |> AccountType.toString, subType |> Option.map(AccountSubtype.toString)))

/// constructNewAndSaveToDb validates that the components work together to
/// form a valid whole before adding it to the persistence layer. All new
/// account creation should route through here before being sent to the
/// persistence layer. Internal model functions may construct through other
/// means if they're operating on known good data. 
let constructNewAndSaveToDb 
        (code: AccountCode)
        (accountName: AccountName)
        (accountType: AccountType)
        (accountActivityPeriod: AccountActivityPeriod)
        (subType: AccountSubtype option)
        (parentId: AccountId option)
        (reference: AccountExternalReference option)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction option)
        : Result<Account, AppError> =
    result {
        let accountId = AccountId.create () // REQ-AC-1.39, REQ-AC-2.13
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        let validAccount = create accountId code accountName accountType accountActivityPeriod subType parentId reference createdAt modifiedAt
        let referenceDate = (AuditEnvelope.instant auditEnvelope) |> Calendar.dateFromInstant
        do! validateParentChildRelationship transaction parentId accountId accountType referenceDate
        do! confirmTypeAndSubtypeAreValid accountType subType
        do! insertNewToDb validAccount transaction // REQ-AC-2.14
        return validAccount }