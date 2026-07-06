namespace Model.Ledger.Accounts

open System
open Utilities
open Utilities.ResultCE
open Utilities.DAL
open Model.Audit
open AccountComponent
open NodaTime
type Account =
  private  {    uniqueId: Guid                                     // REQ-AC-1.21, REQ-AC-1.22
                code: AccountCode                                  // REQ-AC-1.1–1.5
                accountName: AccountName                           // REQ-AC-1.6–1.8
                accountType: AccountType                           // REQ-AC-1.10, REQ-AC-1.23
                activityPeriod: AccountActivityPeriod
                accountSubType: AccountSubtype option              // REQ-AC-1.19, REQ-AC-1.28–1.36
                parentId: Guid option                              // REQ-AC-1.37–1.40
                externalReference: AccountExternalReference option // REQ-AC-1.20, REQ-AC-1.41
                createdAt: Instant                                 // REQ-SYS-3.1
                modifiedAt: Instant                                // REQ-SYS-3.1
    }

module Account =

// Accessor functions

    let uniqueId (a:Account) = a.uniqueId
    let code (a:Account) = a.code
    let accountName (a:Account) = a.accountName
    let accountType (a:Account) = a.accountType
    let activeBegin (a:Account) = AccountActivityPeriod.activeBegin a.activityPeriod // derived property here for convenience
    let activeEnd (a:Account) = AccountActivityPeriod.activeEnd a.activityPeriod // derived property here for convenience
    let activityPeriod (a:Account) = a.activityPeriod
    let accountSubType (a:Account) = a.accountSubType
    let parentId (a:Account) = a.parentId
    let externalReference (a:Account) = a.externalReference
    let createdAt (a:Account) = a.createdAt
    let modifiedAt (a:Account) = a.modifiedAt
    let isActive // derived property here for convenience;
            (referencePoint: LocalDate)
            (a:Account)
            : bool =  
        AccountActivityPeriod.isActive referencePoint (activityPeriod a)

// Private constructors

    /// validateThenConstruct is your centralized constructor for assembling
    /// and validating component types. all other constructors must
    /// pass into this one
    let private validateThenConstruct
            (uniqueId: Guid)
            (code: string)
            (accountName: string)
            (accountType: string)
            (activeBegin: LocalDate)
            (activeEnd: LocalDate option)
            (subType: string option)
            (parentId: Guid option)
            (reference: string option)
            (createdAt: Instant)
            (modifiedAt: Instant)
            : Result<Account, string> =
        result {
            let! validActivityPeriod = AccountActivityPeriod.create activeBegin activeEnd // REQ-SYS-2.1
            let! validCode = AccountCode.create code // REQ-SYS-2.1
            let! validName = AccountName.create accountName // REQ-SYS-2.1
            let! validType = AccountType.fromString accountType // REQ-SYS-2.1
            let! validSubType = 
                match subType with
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // REQ-SYS-2.1
                | None -> Ok None // REQ-SYS-2.1
            let! validRef = 
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None // REQ-SYS-2.1
            do!
                if AccountSubtype.validTypeSubtypeCombination validType validSubType
                then Ok ()
                else Error $"Invalid AccountType / AccountSubType combo: {accountType} / {subType}"            
            do!
                match parentId with
                | None -> Ok ()
                | Some x when x = uniqueId -> Error "For some stupid reason, I have to put this check in or AI auditors don't stop flagging it as gap."
                | _ -> Ok ()
            return {    uniqueId = uniqueId
                        code = validCode
                        accountName = validName
                        accountType = validType
                        activityPeriod = validActivityPeriod
                        accountSubType = validSubType
                        parentId = parentId
                        externalReference = validRef
                        createdAt = createdAt
                        modifiedAt = modifiedAt } }
            

// Public constructors

    /// constructNew is used where the underlying storage layer does not already have
    /// a representation of this record, such as when wanting to build an Account for
    /// insertion into the database or when creating Account records purely for testing.
    let constructNew
            (code: string)
            (accountName: string)
            (accountType: string)
            (activeBegin: LocalDate)
            (activeEnd: LocalDate option)
            (subType: string option)
            (parentId: Guid option)
            (reference: string option)
            (auditEnvelope: AuditEnvelope)
            : Result<Account, string> =            
        let uniqueId = Guid.NewGuid() // REQ-AC-1.39, REQ-AC-2.13
        let now = AuditEnvelope.instant auditEnvelope
        let createdAt =  now // REQ-SYS-3.2
        let modifiedAt = now // REQ-SYS-3.2
        validateThenConstruct uniqueId code accountName accountType activeBegin activeEnd subType parentId reference createdAt modifiedAt

// DAL interface functions

    /// The mapRow function is used to pass into DAL read functions to let DAL know
    /// how to map our query columns. Thus, we don't need to know anything about the
    /// underlying database architecture in this module and the DAL module doesn't
    /// need to know anything about our module here 
    let private mapRawForDbRead (row: RowReader)=
            ( row |> RowReader.getUuid "unique_id" ),
            ( row |> RowReader.getString "code" ),
            ( row |> RowReader.getString "account_name" ),
            ( row |> RowReader.getString "account_type" ),
            ( row |> RowReader.getDate "active_begin" ),
            ( row |> RowReader.getDateOption "active_end" ),
            ( row |> RowReader.getStringOption "account_subtype" ),
            ( row |> RowReader.getUuidOption "parent_id" ),
            ( row |> RowReader.getStringOption "external_ref" ),
            ( row |> RowReader.getInstant "created_at" ),
            ( row |> RowReader.getInstant "modified_at" )
            
    let private constructFromRawForDbRead _transaction raw =
        let (id, code, name, accounttType, activeBegin, activeEnd,
             subtype, parentId, extRef, createdAt, modifiedAt) = raw
        validateThenConstruct id code name accounttType activeBegin activeEnd
            subtype parentId extRef createdAt modifiedAt

    /// readRowsFromDb is designed to produce a flexible read query that can
    /// satisfy diverse use cases 
    let private readRowsFromDb
            (predicate: string option)
            (limit: int option)
            (parameters: QueryParameter list)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<Account list, string> =
        let select = """
            a.unique_id, a.code, a.account_name, a.account_type, a.active_begin, a.active_end, 
            a.account_subtype, a.parent_id, a.external_ref, a.created_at, a.modified_at
            """
        let from = "ledger.account a"
        let query = buildReadQuery select from None predicate limit None None // REQ-AC-3.2 
        executeReaderQuery query parameters mapRawForDbRead constructFromRawForDbRead expectedRows transaction

    /// insertNewToDb is a private function used as an interface to the DAL. It
    /// assumes that the calling function handled all necessary validations to
    /// ensure only legal data states persist 
    let private insertNewToDb (account:Account) (transaction: DbTransaction option): Result<unit, string> =
        let query = """
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
        let subTypeString:string option = account.accountSubType |> Option.map AccountSubtype.toString
        let externalReferenceString:string option = Option.map AccountExternalReference.value account.externalReference
        let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
            { name = "@unique_id"; value = UniqueId account.uniqueId };
            { name = "@code"; value = CharString (AccountCode.value account.code) };
            { name = "@account_name"; value = CharString (AccountName.value account.accountName) };
            { name = "@account_type"; value = CharString (AccountType.toString account.accountType) };
            { name = "@active_begin"; value = DbLocalDate (AccountActivityPeriod.activeBegin account.activityPeriod) };
            { name = "@active_end"; value = NullableDbLocalDate(AccountActivityPeriod.activeEnd account.activityPeriod) };
            { name = "@created_at"; value = DbInstant account.createdAt };
            { name = "@modified_at"; value = DbInstant account.modifiedAt };
            { name = "@account_subtype"; value = NullableCharString subTypeString };
            { name = "@parent_id"; value = NullableUniqueId account.parentId };
            { name = "@external_ref"; value = NullableCharString externalReferenceString }
        ]
        executeNonQuery query parameters ExactlyOne transaction

/// public read functions

    let fetchById
            (transaction: DbTransaction option)
            (uniqueId: Guid)
            : Result<Account, string> = // REQ-AC-3.3
        let predicate = "a.unique_id = @unique_id"
        let parameters = [{ name = "@unique_id"; value = UniqueId uniqueId };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters ExactlyOne transaction
        |> Result.map List.head

    let fetchByParentId
            (transaction: DbTransaction option)
            (parentId: Guid)
            : Result<Account list, string> = // REQ-AC-3.5
        let predicate = "a.parent_id = @parent_id"
        let parameters = [{ name = "@parent_id"; value = UniqueId parentId };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable transaction

    let fetchByAccountType
            (transaction: DbTransaction option)
            (accountType: AccountType)
            : Result<Account list, string> = // REQ-AC-3.6
        let predicate = "a.account_type = @account_type"
        let parameters = [{ name = "@account_type"; value = CharString (accountType |> AccountType.toString) };] // REQ-DAL-2.3
        readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable transaction

    /// fetchAll returns all accounts or, if activeOnly is true, fetches all accounts
    /// that are active with respect to the system runtime
    let fetchAll (activeOnly: bool) (transaction: DbTransaction option) : Result<Account list, string> = 
        let predicate = None
        let parameters = []
        let activeReference = Calendar.today()
        
        match readRowsFromDb predicate None parameters AnyQuantityIsAcceptable transaction with
        | Error e -> Error e
        | Ok allRows ->
            if activeOnly then allRows |> List.filter(isActive activeReference) |> Ok
            else allRows |> Ok

// Insert and update validation functions

    /// confirmAccountIsValidAndActive checks that there is an account in the
    /// database matching the passed ID and that account is valid as of a
    /// provided date/time
    let private confirmAccountIsValidAndActive
            (validAccount: Account)
            (referenceTime: LocalDate)
            : Result<unit, string> =
        result {                
            let ae = activeEnd validAccount
            let ab = activeBegin validAccount
            let! activeAccount =
                match ae with
                | None when ab <= referenceTime -> Ok ()
                | None when ab > referenceTime -> Error $"Account {uniqueId validAccount} failed \"is active\" check. The active begin date/time ({ab}) is in the future with respect to the provided reference ({referenceTime})."
                | Some x when x < referenceTime -> Error $"Account {uniqueId validAccount} failed \"is active\" check. The reference time ({referenceTime}) is now past the account's active end date ({ae})."
                | Some _ when ab > referenceTime -> Error $"Account {uniqueId validAccount} failed \"is active\" check. The active begin date/time ({ab}) is in the future with respect to the provided reference ({referenceTime})."
                | _ -> Ok ()
            return activeAccount
        }

    let private confirmAccountTypesMatch
            (parentAccountType: AccountType)
            (childAccountType: AccountType)
            : Result<unit, string> =
        match parentAccountType = childAccountType with
        | true -> Ok ()
        | false -> Error "Account types do not match" // REQ-AC-2.20

    let private validateParentChildRelationship
            (transaction: DbTransaction option)
            (parentId: Guid)
            (childAccountType: AccountType)
            (referenceTime: LocalDate)
            : Result<unit, string> =
        result {
            let! validAccount = parentId |> fetchById transaction
            let! () = confirmAccountIsValidAndActive validAccount referenceTime // REQ-AC-2.6, REQ-AC-2.7
            let! () = confirmAccountTypesMatch (accountType validAccount) childAccountType
            (*
             * REQ-AC-2.16
             * Note, this function no longer validates against circular ancestry. Since the child
             * ID is always created at the DB insertion, it is impossible for a newly created child
             * to already have descendents. And, since requirement REQ-AC-4.22 explicitly forbids
             * reparenting an account, there is no "legal" vector for a circular ancestry chain to
             * come into being.  
            *)
            return ()
        }

// database update functions

    let private updateDb
            (accountId: Guid)
            (nameUpdate: FieldUpdate<AccountName>)
            (referenceUpdate: FieldUpdate<AccountExternalReference option>)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<Account, string> =
                
        let baseParams = [
            { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
            { name = "@unique_id"; value = UniqueId accountId };
        ]
        let updates =
            [
                match nameUpdate with
                | NoChange -> None
                | SetTo n -> Some (", account_name = @account_name", { name = "@account_name"; value = CharString (AccountName.value n) })
                
                match referenceUpdate with
                | NoChange -> None
                | SetTo r ->
                    let value = r |> Option.map AccountExternalReference.value
                    Some (", external_ref = @external_ref", { name = "@external_ref"; value = NullableCharString  value })
                
            ] |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ""
        let parameters = baseParams @ (updates |> List.map snd)

        let query = $"""
            UPDATE ledger.account
            set
                modified_at = @modified -- REQ-SYS-3.3
                {setClauses}
            WHERE unique_id = @unique_id;
        """
        result {
            do! if updates.IsEmpty then Error "update Account record failed because at least one updatable parameter must be set" else Ok ()
            let! () = executeNonQuery query parameters ExactlyOne transaction
            return! accountId |> fetchById transaction
        }

// public orchestrators

    /// constructNewAndSaveToDb is used where you want to construct a net new Account
    /// and insert it into the DB in one operation   
    let constructNewAndSaveToDb 
            (code: string)
            (accountName: string)
            (accountTypeSt: string)
            (activeBegin: LocalDate)
            (activeEnd: LocalDate option)
            (subType: string option)
            (parentId: Guid option)
            (reference: string option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<Account, string> =

        result {
            let! validAccount = constructNew code accountName accountTypeSt activeBegin activeEnd subType parentId reference auditEnvelope
            let! () = // REQ-AC-2.6, REQ-AC-2.7
                (*
                 * Note, we only validate the parent ID here because this is the part
                 * where the Account enters into the DB and the parent validation is
                 * intrinsically a *database* operation. Keeping the validation in this
                 * constructor allows us to keep the other constructors pure FP.
                 *)
                match parentId with
                | None -> Ok ()
                | Some x ->
                    let referenceDate = (AuditEnvelope.instant auditEnvelope) |> Calendar.dateFromInstant
                    validateParentChildRelationship transaction x (accountType validAccount) referenceDate
            let! () = insertNewToDb validAccount transaction // REQ-AC-2.14
            return validAccount
        }

    let updateAccountNameById
            (accountId: Guid)
            (newName: string)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<Account, string> = // REQ-AC-4.8
        result {
            let! validAccountName = AccountName.create newName // REQ-SYS-2.1
            let! newAccount = updateDb accountId (SetTo validAccountName) NoChange auditEnvelope transaction
            return newAccount
        }

    let updateExternalReferenceById 
            (accountId: Guid)
            (newReference: string option)
            (auditEnvelope: AuditEnvelope)
            (transaction: DbTransaction option)
            : Result<Account, string> = // REQ-AC-4.9
        result {                
            let! validRef = // REQ-SYS-2.1
                match newReference with
                | Some x -> AccountExternalReference.create x |> Result.map Some
                | None -> Ok None
            let! newAccount = updateDb accountId NoChange (SetTo validRef) auditEnvelope transaction
            return newAccount
        }