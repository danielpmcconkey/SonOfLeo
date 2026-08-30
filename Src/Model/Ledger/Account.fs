module Model.Ledger.Account

open Model.ActivityPeriod
open Utilities
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Model.Ledger.AccountComponent
open NodaTime
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery

type Account =
    private
        { accountId: AccountId
          code: AccountCode
          accountName: AccountName
          accountType: AccountType
          activityPeriod: ActivityPeriod
          accountSubType: AccountSubtype option
          parentId: AccountId option
          externalReference: AccountExternalReference option
          createdAt: Instant
          modifiedAt: Instant }

module Account =

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
        (accountActivityPeriod: ActivityPeriod)
        (subType: AccountSubtype option)
        (parentId: AccountId option)
        (reference: AccountExternalReference option)
        (createdAt: Instant)
        (modifiedAt: Instant)
        : Account =
        { accountId = accountId
          code = code
          accountName = accountName
          accountType = accountType
          activityPeriod = accountActivityPeriod
          accountSubType = subType
          parentId = parentId
          externalReference = reference
          createdAt = createdAt
          modifiedAt = modifiedAt }

    /// reconstitute constructs from primitives, performing zero validation at
    /// the collective level. All fields are assumed to have come from a
    /// trusted source (e.g. the database) where such validation occurred at
    /// the time of writing the entity. Important: no additional DB lookups can
    /// be triggered inside this function since it is called within a database
    /// reader.
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
            let! activityPeriod = Model.ActivityPeriod.create activeBegin activeEnd
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

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here
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

    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases
    let private readRowsFromDb
        (context: Context.Context)
        (predicate: string option)
        (limit: int option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        : Result<Account list, AppError> =
        let select =
            """
            a.unique_id, a.code, a.account_name, a.account_type, a.active_begin, a.active_end, 
            a.account_subtype, a.parent_id, a.external_ref, a.created_at, a.modified_at
            """
        let from = "ledger.account a"
        let query = buildReadQuery None select from None predicate limit None None
        executeReaderQuery
            (context |> Context.getDatabaseTransaction)
            query
            parameters
            mapRawForDbRead
            reconstitute
            expectedRows

    /// insertNewToDb is a function used as an interface to the DAL. It
    /// assumes that the calling function handled all necessary validations to
    /// ensure only legal data states persist
    let insertNewToDb (context: Context.Context) (account: Account) : Result<unit, AppError> =
        let query =
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
              { name = "@active_begin"; value = DbLocalDate(activeBegin account.activityPeriod) }
              { name = "@active_end"
                value = NullableDbLocalDate(activeEnd account.activityPeriod) }
              { name = "@created_at"; value = DbInstant account.createdAt }
              { name = "@modified_at"; value = DbInstant account.modifiedAt }
              { name = "@account_subtype"; value = NullableCharString subTypeString }
              { name = "@parent_id"; value = NullableUniqueId parentId }
              { name = "@external_ref"; value = NullableCharString externalReferenceString } ]
        executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne

    let fetchById (context: Context.Context) (accountId: AccountId) : Result<Account, AppError> =
        let predicate = "a.unique_id = @unique_id"
        let accountIdGuid = accountId |> AccountId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ]
        readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

    let fetchByParentId (context: Context.Context) (parentId: AccountId) : Result<Account list, AppError> =
        let predicate = "a.parent_id = @parent_id"
        let parentIdGuid = parentId |> AccountId.value
        let parameters = [ { name = "@parent_id"; value = UniqueId parentIdGuid } ]
        readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

    let fetchByAccountType (context: Context.Context) (accountType: AccountType) : Result<Account list, AppError> =
        let predicate = "a.account_type = @account_type"
        let parameters = [ { name = "@account_type"; value = CharString(accountType |> AccountType.toString) } ]
        readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

    /// fetchAll returns all accounts or, if activeOnly is true, fetches all accounts
    /// that are active with respect to the system runtime
    let fetchAll (context: Context.Context) (activeOnly: bool) : Result<Account list, AppError> =
        let predicate = None
        let parameters = []
        let activeReference = Calendar.today()

        match readRowsFromDb context predicate None parameters AnyQuantityIsAcceptable with
        | Error e -> Error e
        | Ok allRows ->
            if activeOnly then
                allRows
                |> List.filter(fun x -> x.activityPeriod |> isActive activeReference)
                |> Ok
            else
                Ok allRows

    let private updateDb
        (context: Context.Context)
        (accountId: AccountId)
        (nameUpdate: FieldUpdate<AccountName>)
        (referenceUpdate: FieldUpdate<AccountExternalReference option>)
        : Result<Account, AppError> =
        let accountIdGuid = accountId |> AccountId.value
        let baseParams =
            [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
              { name = "@unique_id"; value = UniqueId accountIdGuid } ]
        let updates =
            [ nameUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  (", account_name = @account_name",
                   { name = "@account_name"; value = CharString(AccountName.value n) }))

              referenceUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun r ->
                  let value = r |> Option.map AccountExternalReference.value
                  (", external_ref = @external_ref", { name = "@external_ref"; value = NullableCharString value })) ]
            |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ""
        let parameters = baseParams @ (updates |> List.map snd)

        let query =
            $"""
            UPDATE ledger.account
            set
                modified_at = @modified
                {setClauses}
            WHERE unique_id = @unique_id;
        """
        result {
            do! if updates.IsEmpty then Error(AccountUpdateNoOp) else Ok()
            let! () = executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
            return! accountId |> fetchById context
        }

    let updateAccountNameById (context: Context.Context) (accountId: AccountId) (newName: string) : Result<Account, AppError> =
        result {
            let! validAccountName = AccountName.create newName
            let! newAccount = updateDb context accountId (SetTo validAccountName) NoChange
            return newAccount
        }

    let updateExternalReferenceById
        (context: Context.Context)
        (accountId: AccountId)
        (newReference: string option) // todo make this as FieldUpdate
        : Result<Account, AppError> =
        result {
            let! validRef =
                match newReference with
                | Some x -> AccountExternalReference.create x |> Result.map Some
                | None -> Ok None
            let! newAccount = updateDb context accountId NoChange (SetTo validRef)
            return newAccount
        }
