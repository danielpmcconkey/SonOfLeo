namespace Model.Ledger.Accounts

open Utilities
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Model.Audit
open AccountComponent
open NodaTime
open DataAccessLayer.DbTransaction
open DataAccessLayer.QueryParameters
open DataAccessLayer.ExecuteReader
open DataAccessLayer.ExecuteNonQuery

type Account =
    private
        { accountId: AccountId // REQ-AC-1.21, REQ-AC-1.22
          code: AccountCode // REQ-AC-1.1–1.5
          accountName: AccountName // REQ-AC-1.6–1.8
          accountType: AccountType // REQ-AC-1.10, REQ-AC-1.23
          activityPeriod: AccountActivityPeriod
          accountSubType: AccountSubtype option // REQ-AC-1.19, REQ-AC-1.28–1.36
          parentId: AccountId option // REQ-AC-1.37–1.40
          externalReference: AccountExternalReference option // REQ-AC-1.20, REQ-AC-1.41
          createdAt: Instant // REQ-SYS-3.1
          modifiedAt: Instant } // REQ-SYS-3.1

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
        (accountActivityPeriod: AccountActivityPeriod)
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
            let! activityPeriod = AccountActivityPeriod.create activeBegin activeEnd
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
        (predicate: string option)
        (limit: int option)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        (transaction: DbTransaction)
        : Result<Account list, AppError> =
        let select =
            """
            a.unique_id, a.code, a.account_name, a.account_type, a.active_begin, a.active_end, 
            a.account_subtype, a.parent_id, a.external_ref, a.created_at, a.modified_at
            """
        let from = "ledger.account a"
        let query = buildReadQuery select from None predicate limit None None // REQ-AC-3.2
        executeReaderQuery query parameters mapRawForDbRead reconstitute expectedRows transaction

    /// insertNewToDb is a function used as an interface to the DAL. It
    /// assumes that the calling function handled all necessary validations to
    /// ensure only legal data states persist
    let insertNewToDb (account: Account) (transaction: DbTransaction) : Result<unit, AppError> =
        let query =
            """
            insert into ledger.account( -- REQ-SYS-5.1
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
            values ( --  REQ-DAL-2.1, REQ-SYS-5.1
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
            [ //  REQ-DAL-2.1, REQ-DAL-2.3
              { name = "@unique_id"; value = UniqueId(account.accountId |> AccountId.value) }
              { name = "@code"; value = CharString(AccountCode.value account.code) }
              { name = "@account_name"; value = CharString(AccountName.value account.accountName) }
              { name = "@account_type"; value = CharString(AccountType.toString account.accountType) }
              { name = "@active_begin"; value = DbLocalDate(AccountActivityPeriod.activeBegin account.activityPeriod) }
              { name = "@active_end"
                value = NullableDbLocalDate(AccountActivityPeriod.activeEnd account.activityPeriod) }
              { name = "@created_at"; value = DbInstant account.createdAt }
              { name = "@modified_at"; value = DbInstant account.modifiedAt }
              { name = "@account_subtype"; value = NullableCharString subTypeString }
              { name = "@parent_id"; value = NullableUniqueId parentId }
              { name = "@external_ref"; value = NullableCharString externalReferenceString } ]
        executeNonQuery query parameters ExactlyOne transaction

    let fetchById (transaction: DbTransaction) (accountId: AccountId) : Result<Account, AppError> = // REQ-AC-3.3
        let predicate = "a.unique_id = @unique_id"
        let accountIdGuid = accountId |> AccountId.value
        let parameters = [ { name = "@unique_id"; value = UniqueId accountIdGuid } ] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters ExactlyOne transaction |> Result.map List.head

    let fetchByParentId (transaction: DbTransaction) (parentId: AccountId) : Result<Account list, AppError> = // REQ-AC-3.5
        let predicate = "a.parent_id = @parent_id"
        let parentIdGuid = parentId |> AccountId.value
        let parameters = [ { name = "@parent_id"; value = UniqueId parentIdGuid } ] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable transaction

    let fetchByAccountType (transaction: DbTransaction) (accountType: AccountType) : Result<Account list, AppError> = // REQ-AC-3.6
        let predicate = "a.account_type = @account_type"
        let parameters = [ { name = "@account_type"; value = CharString(accountType |> AccountType.toString) } ] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable transaction

    /// fetchAll returns all accounts or, if activeOnly is true, fetches all accounts
    /// that are active with respect to the system runtime
    let fetchAll (activeOnly: bool) (transaction: DbTransaction) : Result<Account list, AppError> = // REQ-AC-3.7
        let predicate = None
        let parameters = []
        let activeReference = Calendar.today()

        match readRowsFromDb predicate None parameters AnyQuantityIsAcceptable transaction with
        | Error e -> Error e
        | Ok allRows ->
            if activeOnly then
                allRows
                |> List.filter(fun x -> x.activityPeriod |> AccountActivityPeriod.isActive activeReference)
                |> Ok // REQ-AC-3.9
            else
                Ok allRows

    let private updateDb
        (accountId: AccountId)
        (nameUpdate: FieldUpdate<AccountName>)
        (referenceUpdate: FieldUpdate<AccountExternalReference option>)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction)
        : Result<Account, AppError> =
        let accountIdGuid = accountId |> AccountId.value
        let baseParams =
            [ { name = "@modified"; value = DbInstant(AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3
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
                modified_at = @modified -- REQ-SYS-3.3
                {setClauses}
            WHERE unique_id = @unique_id;
        """
        result {
            do! if updates.IsEmpty then Error(AccountUpdateNoOp) else Ok()
            let! () = executeNonQuery query parameters ExactlyOne transaction
            return! accountId |> fetchById transaction
        }

    let updateAccountNameById
        (accountId: AccountId)
        (newName: string)
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction)
        : Result<Account, AppError> = // REQ-AC-4.8
        result {
            let! validAccountName = AccountName.create newName // REQ-SYS-2.1
            let! newAccount = updateDb accountId (SetTo validAccountName) NoChange auditEnvelope transaction
            return newAccount
        }

    let updateExternalReferenceById
        (accountId: AccountId)
        (newReference: string option) // todo make this as FieldUpdate
        (auditEnvelope: AuditEnvelope)
        (transaction: DbTransaction)
        : Result<Account, AppError> = // REQ-AC-4.9
        result {
            let! validRef = // REQ-SYS-2.1
                match newReference with
                | Some x -> AccountExternalReference.create x |> Result.map Some
                | None -> Ok None
            let! newAccount = updateDb accountId NoChange (SetTo validRef) auditEnvelope transaction
            return newAccount
        }
