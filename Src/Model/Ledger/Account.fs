namespace Model.Ledger

open System
open Utilities.ResultCE
open Utilities.DAL
open AccountComponent

module Account =
    
    type Account =
      private  {    id: Guid                                           // FT-AC-1.21, FT-AC-1.22
                    code: AccountCode                                  // FT-AC-1.1–1.5
                    name: AccountName                                  // FT-AC-1.6–1.8
                    accountType: AccountType                           // FT-AC-1.10, FT-AC-1.23
                    activityPeriod: AccountActivityPeriod
                    createdAt: DateTimeOffset                          // FT-AC-1.25
                    modifiedAt: DateTimeOffset                         // FT-AC-1.26, FT-AC-1.27
                    accountSubType: AccountSubtype option              // FT-AC-1.19, FT-AC-1.28–1.36
                    parentId: Guid option                              // FT-AC-1.37–1.40
                    externalReference: AccountExternalReference option // FT-AC-1.20, FT-AC-1.41
        }
    
    module Account =
        
// Accessor functions
        
        let id (a:Account) = a.id
        let code (a:Account) = a.code
        let name (a:Account) = a.name
        let accountType (a:Account) = a.accountType
        let activeBegin (a:Account) = AccountActivityPeriod.activeBegin a.activityPeriod // here for convenience
        let activeEnd (a:Account) = AccountActivityPeriod.activeEnd a.activityPeriod // here for convenience
        let activityPeriod (a:Account) = a.activityPeriod
        let accountSubType (a:Account) = a.accountSubType
        let parentId (a:Account) = a.parentId
        let externalReference (a:Account) = a.externalReference
        
// Private constructors
        
        /// constructOmni is a private method used in all "construct" modes by specific public
        /// constructors designed to be used in specific use cases. It assumes the caller
        /// (public constructor) knows its business and makes decisions on whether or when to
        /// create things like UUIDs and timestamps
        let private constructOmni
                (id: Guid)
                (code: AccountCode)
                (name: AccountName)
                (accountType: AccountType)
                (activityPeriod: AccountActivityPeriod)
                (createdAt: DateTimeOffset)
                (modifiedAt: DateTimeOffset)
                (subType: AccountSubtype option)
                (parentId: Guid option)
                (reference: AccountExternalReference option)
                : Result<Account, string> =
            if AccountSubtype.validTypeSubtypeCombination(accountType, subType) then Ok {
                id = id
                code = code
                name = name
                accountType = accountType
                activityPeriod = activityPeriod
                createdAt = createdAt
                modifiedAt = modifiedAt
                accountSubType = subType
                parentId = parentId
                externalReference = reference
            } else
                Error $"Invalid AccountType / AccountSubType combo: {accountType} / {subType}"

// Public constructors
                        
        /// constructNew is used where the underlying storage layer does not already have
        /// a representation of this record, such as when wanting to build an Account for
        /// insertion into the database or when creating Account records purely for testing.
        let constructNew
                (code: string)
                (name: string)
                (accountType: string)
                (activeBegin: DateTimeOffset)
                (activeEnd: DateTimeOffset option)
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                : Result<Account, string> =            
            let id = Guid.NewGuid() // FT-AC-1.39, FT-AC-2.13
            let createdAt = DateTimeOffset.UtcNow // FT-AC-1.25, FT-AC-2.11
            let modifiedAt = DateTimeOffset.UtcNow // FT-AC-1.26, FT-AC-2.12
            let activityPeriodResult = AccountActivityPeriod.create activeBegin activeEnd
            let codeResult = AccountCode.create(code)
            let nameResult = AccountName.create(name)
            let typeResult = AccountType.fromString(accountType) // FT-AC-2.4
            let subTypeResult = 
                match subType with
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // FT-AC-2.10
                | None -> Ok None
            let referenceResult =
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None
            
            result {
                let! activityPeriod = activityPeriodResult
                let! validCode = codeResult
                let! validName = nameResult
                let! validType = typeResult
                let! validSubType = subTypeResult
                let! validRef = referenceResult
                return!
                    constructOmni id validCode validName validType activityPeriod
                        createdAt modifiedAt validSubType parentId validRef
            }
        
        /// reconstitute is used where the underlying storage layer already represents
        /// this record (e.g. database read operations)
        let reconstitute
                (id: Guid)
                (code: string)
                (name: string)
                (accountTypeId: int)
                (activeBegin: DateTimeOffset)
                (activeEnd: DateTimeOffset option)
                (createdAt: DateTimeOffset)
                (modifiedAt: DateTimeOffset)                
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                : Result<Account, string> =            
            let activityPeriodResult = AccountActivityPeriod.create activeBegin activeEnd
            let codeResult = AccountCode.create(code)
            let nameResult = AccountName.create(name)
            let typeResult = AccountType.fromDbId(accountTypeId)
            let subTypeResult = 
                match subType with
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // FT-AC-2.10
                | None -> Ok None
            let referenceResult =
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None
            
            result {
                let! activityPeriod = activityPeriodResult
                let! validCode = codeResult // FT-AC-3.1
                let! validName = nameResult // FT-AC-3.1
                let! validType = typeResult // FT-AC-3.1
                let! validSubType = subTypeResult // FT-AC-3.1
                let! validRef = referenceResult // FT-AC-3.1
                return!
                    constructOmni id validCode validName validType activityPeriod
                        createdAt modifiedAt validSubType parentId validRef // FT-AC-3.2 
            }
            
// DAL interface functions
        
        /// The mapRow function is used to pass into DAL read functions to let DAL know
        /// how to map our query columns. Thus, we don't need to know anything about the
        /// underlying database architecture in this module and the DAL module doesn't
        /// need to know anything about our module here 
        let mapAccountRowForDbRead (row: RowReader) : Result<Account, string> =
            reconstitute
                ( row |> RowReader.getUuid "id" )
                ( row |> RowReader.getString "code" )
                ( row |> RowReader.getString "name" )
                ( row |> RowReader.getInt "account_type_id" )
                ( row |> RowReader.getDateTimeOffset "active_begin" )
                ( row |> RowReader.getDateTimeOffsetOption "active_end" )
                ( row |> RowReader.getDateTimeOffset "created_at" )
                ( row |> RowReader.getDateTimeOffset "modified_at" )
                ( row |> RowReader.getStringOption "account_subtype" )
                ( row |> RowReader.getUuidOption "parent_id" )
                ( row |> RowReader.getStringOption "external_ref" )
        
        /// readRowsFromDb is designed to produce a flexible read query that can
        /// satisfy diverse use cases 
        let private readRowsFromDb
                (predicate: string option)
                (limit: int option)
                (parameters: QueryParameter list)
                (expectedRows: AcceptableExpectedRows): Result<Account list, string> =
            let predicateString =
                match predicate with
                | Some x -> x
                | None -> String.Empty
            let limitString =
                match limit with
                | Some x -> $"limit {x}"
                | None -> String.Empty
            let query = $"""
                select  -- FT-AC-3.2 
	                id, 
                    code, 
                    name, 
                    account_type_id, 
                    active_begin,
                    active_end,
                    created_at, 
                    modified_at, 
                    account_subtype, 
                    parent_id, 
                    external_ref
                from ledger.account
                {predicateString}
                {limitString}
                ;
                """
            executeReaderQuery query parameters mapAccountRowForDbRead expectedRows

        /// insertNewToDb is a private function used as an interface to the DAL. It
        /// assumes that the calling function handled all necessary validations to
        /// ensure only legal data states persist 
        let private insertNewToDb (account:Account): Result<unit, string> =            
            let query = """
                insert into ledger.account( -- FT-AC-2.15
	                id, 
                    code, 
                    name, 
                    account_type_id, 
                    active_begin,
                    active_end,
                    created_at, 
                    modified_at, 
                    account_subtype, 
                    parent_id, 
                    external_ref)
                values ( --  FT-DAL-2.1, FT-AC-2.15
	                @id, 
                    @code, 
                    @name, 
                    @account_type_id, 
                    @active_begin,
                    @active_end,
                    @created_at, 
                    @modified_at, 
                    @account_subtype, 
                    @parent_id, 
                    @external_ref);"""
            let subTypeString:string option = account.accountSubType |> Option.map AccountSubtype.toString
            let externalReferenceString:string option = Option.map AccountExternalReference.value account.externalReference
            let parameters = [ //  FT-DAL-2.1, FT-DAL-2.3 
                { name = "@id"; value = UniqueId account.id };
                { name = "@code"; value = CharString (AccountCode.value account.code) };
                { name = "@name"; value = CharString (AccountName.value account.name) };
                { name = "@account_type_id"; value = Integer (AccountType.toDbId account.accountType) };
                { name = "@active_begin"; value = DateTimeWithOffset (AccountActivityPeriod.activeBegin account.activityPeriod) };
                { name = "@active_end"; value = NullableDateTimeWithOffset(AccountActivityPeriod.activeEnd account.activityPeriod) };
                { name = "@created_at"; value = DateTimeWithOffset account.createdAt };
                { name = "@modified_at"; value = DateTimeWithOffset account.modifiedAt };
                { name = "@account_subtype"; value = NullableCharString subTypeString };
                { name = "@parent_id"; value = NullableUniqueId account.parentId };
                { name = "@external_ref"; value = NullableCharString externalReferenceString }
            ]
            executeNonQuery query parameters ExactlyOne

/// public read functions

        let fetchById (id: Guid) : Result<Account, string> = // FT-AC-3.3
            let predicate = "where id = @id"
            let parameters = [{ name = "@id"; value = UniqueId id };] // FT-DAL-2.3         
            readRowsFromDb (Some predicate) None parameters ExactlyOne
            |> Result.map List.head
        
        let fetchByCode (code: string) : Result<Account, string> = // FT-AC-3.4
            let predicate = "where code = @code"
            let parameters = [{ name = "@code"; value = CharString code };] // FT-DAL-2.3        
            readRowsFromDb (Some predicate) None parameters ExactlyOne
            |> Result.map List.head
        
        let fetchByParentId (parentId: Guid) : Result<Account list, string> = // FT-AC-3.5
            let predicate = $"where parent_id = @parent_id"
            let parameters = [{ name = "@parent_id"; value = UniqueId parentId };] // FT-DAL-2.3          
            readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable
        
        let fetchByAccountType (accountType: AccountType): Result<Account list, string> = // FT-AC-3.6
            let typeId = AccountType.toDbId(accountType)
            let predicate = $"where account_type_id = @type_id"
            let parameters = [{ name = "@type_id"; value = Integer typeId };] // FT-DAL-2.3        
            readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable            
 
// Insert and update validation functions        
        
        /// confirmAccountIsValidAndActive checks that there is an account in the
        /// database matching the passed ID and that account is valid as of the
        /// system run-time
        let private confirmAccountIsValidAndActive (id: Guid) : Result<unit, string> =
            result {
                let! validAccount = fetchById id
                let ae = activeEnd validAccount
                let ab = activeBegin validAccount                
                let! activeAccount =
                    match ae with
                    | None when ab <= DateTimeOffset.Now -> Ok ()
                    | None when ab > DateTimeOffset.Now -> Error $"Account {id} failed \"is active\" check. The active begin date/time is in the future."
                    | Some x when x <= DateTimeOffset.Now -> Error $"Account {id} failed \"is active\" check. The current runtime {DateTimeOffset.Now} is now past the active end date."
                    | _ -> Ok ()
                return activeAccount
            }

// public orchestrators

        /// constructNewAndSaveToDb is used where you want to construct a net new Account
        /// and insert it into the DB in one operation   
        let constructNewAndSaveToDb 
                (code: string)
                (name: string)
                (accountType: string)
                (activeBegin: DateTimeOffset)
                (activeEnd: DateTimeOffset option)
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                : Result<Account, string> =            
            
            result {
                let! validAccount = constructNew code name accountType activeBegin activeEnd subType parentId reference
                let! () = // FT-AC-2.6, FT-AC-2.7 
                    match parentId with
                    | None -> Ok ()
                    | Some x -> confirmAccountIsValidAndActive(x) // FT-AC-2.6, FT-AC-2.7                   
                let! () = insertNewToDb validAccount // FT-AC-2.14
                return validAccount
            }
           