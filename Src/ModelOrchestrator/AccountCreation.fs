module ModelOrchestrator.AccountCreation

open Model
open Model.Ledger.Account
open Model.Ledger.AccountComponent
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.ResultHelper


let private confirmParentAccountIsActive (parentAccount: Account) (referenceDate: LocalDate) : Result<unit, AppError> =
    match parentAccount |> Account.activityPeriod |> ActivityPeriod.isActive referenceDate with
    | true -> Ok()
    | false -> Error(AccountParentIsInactive(parentAccount |> Account.accountId |> AccountId.value))

let private confirmParentAndChildAccountTypesMatch
    (parentAccountType: AccountType)
    (childAccountType: AccountType)
    : Result<unit, AppError> =
    match parentAccountType = childAccountType with
    | true -> Ok()
    | false ->
        Error(
            AccountParentAndChildTypesDontMatch(
                parentAccountType |> AccountType.toString,
                childAccountType |> AccountType.toString
            )
        )

let private confirmParentAndChildAreDistinct
    (parentId: AccountId option)
    (childId: AccountId)
    : Result<unit, AppError> =
    match parentId with
    | None -> Ok()
    | Some x when x = childId ->
        Error(AccountParentAndChildAreSame(parentId |> Option.map AccountId.value, childId |> AccountId.value))
    | _ -> Ok()

let private confirmParentChildRelationship
    (context: Context.Context)
    (parentId: AccountId option)
    (childId: AccountId)
    (childType: AccountType)
    (referenceDate: LocalDate)
    : Result<unit, AppError> =
    // Note, this function no longer validates against circular ancestry. Since the child
    // ID is always created at the DB insertion, it is impossible for a newly created child
    // to already have descendents. And, since requirement REQ-AC-4.22 explicitly forbids
    // reparenting an account, there is no "legal" vector for a circular ancestry chain to
    // come into being.
    match parentId with
    | None -> Ok()
    | Some someParentId ->
        result {
            let! validParent = someParentId |> Account.fetchById context
            let parentType = validParent |> Account.accountType
            do! confirmParentAccountIsActive validParent referenceDate
            do! confirmParentAndChildAccountTypesMatch parentType childType
            do! confirmParentAndChildAreDistinct parentId childId
            return ()
        }

let private confirmTypeAndSubtypeAreValid (accountType: AccountType) (subType: AccountSubtype option) : Result<unit, AppError> =
    if AccountSubtype.validTypeSubtypeCombination accountType subType then
        Ok()
    else
        Error(
            AccountInvalidTypeSubtypeCombo(
                accountType |> AccountType.toString,
                subType |> Option.map(AccountSubtype.toString)
            )
        )

let constructNewAndPersist
    (context: Context.Context)
    (code: AccountCode)
    (accountName: AccountName)
    (accountType: AccountType)
    (accountActivityPeriod: ActivityPeriod.ActivityPeriod)
    (subType: AccountSubtype option)
    (parentId: AccountId option)
    (reference: AccountExternalReference option)
    : Result<Account, AppError> =
    result {
        let accountId = AccountId.create()
        let now = context |> Context.getInitiationInstant
        let createdAt = now
        let modifiedAt = now
        let validAccount =
            Account.create
                accountId
                code
                accountName
                accountType
                accountActivityPeriod
                subType
                parentId
                reference
                createdAt
                modifiedAt
        let referenceDate = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
        do! confirmParentChildRelationship context parentId accountId accountType referenceDate
        do! confirmTypeAndSubtypeAreValid accountType subType
        do! validAccount |> Account.persist context
        return validAccount
    }
