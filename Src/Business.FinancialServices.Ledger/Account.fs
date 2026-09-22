module Business.FinancialServices.Ledger.Account

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Result
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.QueryParameter
open App.Session
open Business.General
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.Ledger.AccountComponent

type Account =
    private
        { accountId: AccountId
          code: AccountCode
          accountName: AccountName
          accountType: AccountType
          activityPeriod: ActivityPeriod.ActivityPeriod
          accountSubType: AccountSubtype option
          parentId: AccountId option
          externalReference: AccountExternalReference option
          createdAt: Instant
          modifiedAt: Instant }

let accountId (a: Account) = a.accountId
let code (a: Account) = a.code
let accountName (a: Account) = a.accountName
let accountType (a: Account) = a.accountType
let activityPeriod (a: Account) = a.activityPeriod
let accountSubType (a: Account) = a.accountSubType
let parentId (a: Account) = a.parentId
let externalReference (a: Account) = a.externalReference
let createdAt (a: Account) = a.createdAt
let modifiedAt (a: Account) = a.modifiedAt

let create
    (accountId: AccountId)
    (code: AccountCode)
    (accountName: AccountName)
    (accountType: AccountType)
    (accountActivityPeriod: ActivityPeriod.ActivityPeriod)
    (subType: AccountSubtype option)
    (parentId: AccountId option)
    (reference: AccountExternalReference option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Account =
    let rebuiltActivityPeriod =
        accountActivityPeriod
        |> ActivityPeriod.insistBeginValidationBehavior ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    { accountId = accountId
      code = code
      accountName = accountName
      accountType = accountType
      activityPeriod = rebuiltActivityPeriod
      accountSubType = subType
      parentId = parentId
      externalReference = reference
      createdAt = createdAt
      modifiedAt = modifiedAt }

let private reconstitute raw =
    result {
        let (uuid,
             codeString,
             nameString,
             accountTypeString,
             activeBegin,
             activeEnd,
             subtypeString,
             parentUuid,
             extRefString,
             createdAt,
             modifiedAt) =
            raw
        let accountId = uuid |> AccountId.fromGuid
        let! accountCode = codeString |> AccountCode.create
        let! accountName = nameString |> AccountName.create
        let! accountType = accountTypeString |> AccountType.fromString
        let! activityPeriod =
            match ActivityPeriod.create activeBegin activeEnd ActivityPeriod.NotConsideredAvailableBeforeBeginDate with
            | Ok x -> Ok x
            | Error e ->
                if e.DomainName = nameof BizGeneralError && e.CaseName = nameof BizGeneralError.ActiveEndBeforeBegin
                then error(AccountActiveEndBeforeBegin (activeBegin, activeEnd))
                else Error e 
        let! subtype =
            subtypeString
            |> Option.map(fun x -> x |> AccountSubtype.fromString |> Result.map Some)
            |> Option.defaultValue(Ok None)
        let parentAccountId = parentUuid |> Option.map AccountId.fromGuid
        let! externalReference =
            extRefString
            |> Option.map(fun x -> x |> AccountExternalReference.create |> Result.map Some)
            |> Option.defaultValue(Ok None)
        return
            create
                accountId
                accountCode
                accountName
                accountType
                activityPeriod
                subtype
                parentAccountId
                externalReference
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "code"),
    (row |> RowReader.getString "account_name"),
    (row |> RowReader.getString "account_type"),
    (row |> RowReader.getDate "active_begin"),
    (row |> RowReader.getDateOption "active_end"),
    (row |> RowReader.getStringOption "account_subtype"),
    (row |> RowReader.getUuidOption "parent_id"),
    (row |> RowReader.getStringOption "external_ref"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private query
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Account list, IAppError> =
    let select =
        """
        a.unique_id, a.code, a.account_name, a.account_type, a.active_begin, a.active_end, 
        a.account_subtype, a.parent_id, a.external_ref, a.created_at, a.modified_at
        """
    let from = "ledger.account a"
    let queryStatement = buildReadQuery None select from None predicate limit None None
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows 

let persist (context: Context.Context) (account: Account) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into ledger.account(
	        unique_id, 
            code, 
            account_name, 
            account_type, 
            active_begin,
            active_end,
            account_subtype, 
            parent_id, 
            external_ref,
            created_at, 
            modified_at)
        values (
	        @unique_id, 
            @code, 
            @account_name, 
            @account_type, 
            @active_begin,
            @active_end,
            @account_subtype, 
            @parent_id, 
            @external_ref,
            @created_at, 
            @modified_at);"""
    let subTypeString: string option = account.accountSubType |> Option.map AccountSubtype.toString
    let externalReferenceString: string option =
        Option.map AccountExternalReference.value account.externalReference
    let parentId = account.parentId |> Option.map AccountId.value
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId(account.accountId |> AccountId.value) }
          { name = "@code"; value = CharString(AccountCode.value account.code) }
          { name = "@account_name"; value = CharString(AccountName.value account.accountName) }
          { name = "@account_type"; value = CharString(AccountType.toString account.accountType) }
          { name = "@active_begin"; value = DbLocalDate(ActivityPeriod.activeBegin account.activityPeriod) }
          { name = "@active_end"
            value = NullableDbLocalDate(ActivityPeriod.activeEnd account.activityPeriod) }
          { name = "@created_at"; value = DbInstant account.createdAt }
          { name = "@modified_at"; value = DbInstant account.modifiedAt }
          { name = "@account_subtype"; value = NullableCharString subTypeString }
          { name = "@parent_id"; value = NullableUniqueId parentId }
          { name = "@external_ref"; value = NullableCharString externalReferenceString } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let fetchById (context: Context.Context) (accountId: AccountId) : Result<Account, IAppError> =
    let predicate = "a.unique_id = @unique_id"
    let accountIdGuid = accountId |> AccountId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ]
    query context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByParentId (context: Context.Context) (parentId: AccountId) : Result<Account list, IAppError> =
    let predicate = "a.parent_id = @parent_id"
    let parentIdGuid = parentId |> AccountId.value
    let parameters = [ { name = "@parent_id"; value = UniqueId parentIdGuid } ]
    query context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByAccountType (context: Context.Context) (accountType: AccountType) : Result<Account list, IAppError> =
    let predicate = "a.account_type = @account_type"
    let parameters = [ { name = "@account_type"; value = CharString(accountType |> AccountType.toString) } ]
    query context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchAll (context: Context.Context) (activeOnly: bool) : Result<Account list, IAppError> =
    let predicate = None
    let parameters = []
    let activeReference = context |> Context.getInitiationInstant |> Calendar.dateFromInstant

    match query context predicate None parameters AnyQuantityIsAcceptable with
    | Error e -> Error e
    | Ok allRows ->
        if activeOnly then
            allRows
            |> List.filter(fun x -> x.activityPeriod |> ActivityPeriod.isActive activeReference)
            |> Ok
        else
            Ok allRows

let private update
    (context: Context.Context)
    (accountId: AccountId)
    (nameUpdate: FieldUpdate<AccountName>)
    (referenceUpdate: FieldUpdate<AccountExternalReference option>)
    : Result<Account, IAppError> =
    let accountIdGuid = accountId |> AccountId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId accountIdGuid } ]
    let updates =
        [ nameUpdate
          |> mapNoChangeToOptionWithConversion(fun n ->
              (", account_name = @account_name",
               { name = "@account_name"; value = CharString(AccountName.value n) }))

          referenceUpdate
          |> mapNoChangeToOptionWithConversion(fun r ->
              let value = r |> Option.map AccountExternalReference.value
              (", external_ref = @external_ref", { name = "@external_ref"; value = NullableCharString value })) ]
        |> List.choose id
    let setClauses = updates |> List.map fst |> String.concat ""
    let parameters = baseParams @ (updates |> List.map snd)

    let queryStatement =
        $"""
        UPDATE ledger.account
        set
            modified_at = @modified
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates.IsEmpty then Error(AccountUpdateNoOp) else Ok()
        let! () = executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! accountId |> fetchById context
    }

let updateAccountNameById (context: Context.Context) (accountId: AccountId) (newName: string) : Result<Account, IAppError> =
    result {
        let! validAccountName = AccountName.create newName
        let! newAccount = update context accountId (SetTo validAccountName) NoChange
        return newAccount
    }

let updateExternalReferenceById
    (context: Context.Context)
    (accountId: AccountId)
    (newReference: string option) // todo make this as FieldUpdate
    : Result<Account, IAppError> =
    result {
        let! validRef =
            match newReference with
            | Some x -> AccountExternalReference.create x |> Result.map Some
            | None -> Ok None
        let! newAccount = update context accountId NoChange (SetTo validRef)
        return newAccount
    }
