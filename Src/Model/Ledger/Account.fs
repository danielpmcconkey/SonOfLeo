namespace Model.Ledger

open System
open Utilities.ResultCE
open Utilities.DAL
open Model.Audit
open AccountComponent
open NodaTime

module Account =
    
    type Account =
      private  {    id: Guid                                           // REQ-AC-1.21, REQ-AC-1.22
                    code: AccountCode                                  // REQ-AC-1.1–1.5
                    name: AccountName                                  // REQ-AC-1.6–1.8
                    accountType: AccountType                           // REQ-AC-1.10, REQ-AC-1.23
                    activityPeriod: AccountActivityPeriod
                    createdAt: Instant                                 // REQ-SYS-3.1
                    modifiedAt: Instant                                // REQ-SYS-3.1
                    accountSubType: AccountSubtype option              // REQ-AC-1.19, REQ-AC-1.28–1.36
                    parentId: Guid option                              // REQ-AC-1.37–1.40
                    externalReference: AccountExternalReference option // REQ-AC-1.20, REQ-AC-1.41
        }
    
    module Account =
        
// Accessor functions
        
        let id (a:Account) = a.id
        let code (a:Account) = a.code
        let name (a:Account) = a.name
        let accountType (a:Account) = a.accountType
        let activeBegin (a:Account) = AccountActivityPeriod.activeBegin a.activityPeriod // derived property here for convenience
        let activeEnd (a:Account) = AccountActivityPeriod.activeEnd a.activityPeriod // derived property here for convenience
        let activityPeriod (a:Account) = a.activityPeriod
        let accountSubType (a:Account) = a.accountSubType
        let parentId (a:Account) = a.parentId
        let externalReference (a:Account) = a.externalReference
        let isActive // derived property here for convenience;
                (a:Account)
                (referencePoint: Instant) // REQ-AC-1.48.1
                : bool =  
            let beginDate = activeBegin a
            let endDate = activeEnd a
            match endDate with
            | None when beginDate <= referencePoint -> true
            | Some x when beginDate <= referencePoint && x > referencePoint -> true // REQ-AC-1.48
            | _ -> false
        
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
                (createdAt: Instant)
                (modifiedAt: Instant)
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
                (activeBegin: Instant)
                (activeEnd: Instant option)
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                (auditEnvelope: AuditEnvelope)
                : Result<Account, string> =            
            let id = Guid.NewGuid() // REQ-AC-1.39, REQ-AC-2.13
            let now = AuditEnvelope.instant auditEnvelope
            let createdAt =  now // REQ-SYS-3.2
            let modifiedAt = now // REQ-SYS-3.2
            let activityPeriodResult = AccountActivityPeriod.create activeBegin activeEnd // REQ-AC-2.17, REQ-AC-2.18
            let codeResult = AccountCode.create(code)
            let nameResult = AccountName.create(name)
            let typeResult = AccountType.fromString(accountType) // REQ-AC-2.4
            let subTypeResult = 
                match subType with
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // REQ-AC-2.10
                | None -> Ok None
            let referenceResult =
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None
            let gfyAuditorsResult =
                match parentId with
                | None -> Ok ()
                | Some x when x = id -> Error "Go fuck yourselves auditors"
                | _ -> Ok ()
            
            result {
                let! _ = gfyAuditorsResult
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
                (activeBegin: Instant)
                (activeEnd: Instant option)
                (createdAt: Instant)
                (modifiedAt: Instant)                
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
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // REQ-SYS-2.1
                | None -> Ok None
            let referenceResult =
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None
            
            result {
                let! activityPeriod = activityPeriodResult // REQ-SYS-2.1
                let! validCode = codeResult // REQ-SYS-2.1
                let! validName = nameResult // REQ-SYS-2.1
                let! validType = typeResult // REQ-SYS-2.1
                let! validSubType = subTypeResult // REQ-SYS-2.1
                let! validRef = referenceResult // REQ-SYS-2.1
                return!
                    constructOmni id validCode validName validType activityPeriod
                        createdAt modifiedAt validSubType parentId validRef // REQ-AC-3.2 
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
                ( row |> RowReader.getInstant "active_begin" )
                ( row |> RowReader.getInstantOption "active_end" )
                ( row |> RowReader.getInstant "created_at" )
                ( row |> RowReader.getInstant "modified_at" )
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
                select  -- REQ-AC-3.2 
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
                insert into ledger.account( -- REQ-SYS-5.1
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
                values ( --  REQ-DAL-2.1, REQ-SYS-5.1
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
            let parameters = [ //  REQ-DAL-2.1, REQ-DAL-2.3 
                { name = "@id"; value = UniqueId account.id };
                { name = "@code"; value = CharString (AccountCode.value account.code) };
                { name = "@name"; value = CharString (AccountName.value account.name) };
                { name = "@account_type_id"; value = Integer (AccountType.toDbId account.accountType) };
                { name = "@active_begin"; value = DbInstant (AccountActivityPeriod.activeBegin account.activityPeriod) };
                { name = "@active_end"; value = NullableDbInstant(AccountActivityPeriod.activeEnd account.activityPeriod) };
                { name = "@created_at"; value = DbInstant account.createdAt };
                { name = "@modified_at"; value = DbInstant account.modifiedAt };
                { name = "@account_subtype"; value = NullableCharString subTypeString };
                { name = "@parent_id"; value = NullableUniqueId account.parentId };
                { name = "@external_ref"; value = NullableCharString externalReferenceString }
            ]
            executeNonQuery query parameters ExactlyOne

/// public read functions

        let fetchById (id: Guid) : Result<Account, string> = // REQ-AC-3.3
            let predicate = "where id = @id"
            let parameters = [{ name = "@id"; value = UniqueId id };] // REQ-DAL-2.3         
            readRowsFromDb (Some predicate) None parameters ExactlyOne
            |> Result.map List.head
        
        let fetchByCode (code: string) : Result<Account, string> = // REQ-AC-3.4
            let predicate = "where code = @code"
            let parameters = [{ name = "@code"; value = CharString code };] // REQ-DAL-2.3        
            readRowsFromDb (Some predicate) None parameters ExactlyOne
            |> Result.map List.head
        
        let fetchByParentId (parentId: Guid) : Result<Account list, string> = // REQ-AC-3.5
            let predicate = $"where parent_id = @parent_id"
            let parameters = [{ name = "@parent_id"; value = UniqueId parentId };] // REQ-DAL-2.3          
            readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable
        
        let fetchByAccountType (accountType: AccountType): Result<Account list, string> = // REQ-AC-3.6
            let typeId = AccountType.toDbId(accountType)
            let predicate = $"where account_type_id = @type_id"
            let parameters = [{ name = "@type_id"; value = Integer typeId };] // REQ-DAL-2.3        
            readRowsFromDb (Some predicate) None parameters AnyQuantityIsAcceptable
 
// Insert and update validation functions        
        
        /// confirmAccountIsValidAndActive checks that there is an account in the
        /// database matching the passed ID and that account is valid as of a
        /// provided date/time
        let private confirmAccountIsValidAndActive (id: Guid, referenceTime: Instant) : Result<unit, string> =
            result {
                let! validAccount = fetchById id
                let ae = activeEnd validAccount
                let ab = activeBegin validAccount                
                let! activeAccount =
                    match ae with
                    | None when ab <= referenceTime -> Ok ()
                    | None when ab > referenceTime -> Error $"Account {id} failed \"is active\" check. The active begin date/time ({ab}) is in the future with respect to the provided reference ({referenceTime})."
                    | Some x when x <= referenceTime -> Error $"Account {id} failed \"is active\" check. The reference time ({referenceTime}) is now past (or equal to) the account's active end date ({ae})."
                    | Some _ when ab > referenceTime -> Error $"Account {id} failed \"is active\" check. The active begin date/time ({ab}) is in the future with respect to the provided reference ({referenceTime})."
                    | _ -> Ok ()
                return activeAccount
            }
        
        let private validateParentChildRelationship
                (parentId: Guid)
                (childId: Guid)
                (referenceTime: Instant)
                : Result<unit, string> =
            result {
                let! () = confirmAccountIsValidAndActive(parentId, referenceTime) // REQ-AC-2.6, REQ-AC-2.7
                (*
                 * REQ-AC-2.16
                 * Note, this function no longer validates against circular ancestry. Since the child
                 * ID is always created at the DB insertion, it is impossible for a newly created child
                 * to already have descendents. And, since requirement REQ-AC-4.22 explicitly forbids
                 * reparenting an account, there is no "legal" vector for a circular ancestry chain to
                 * come into being.
                 *
                 * I'm leaving this function here even though it does nothing new as we may someday devise
                 * other parent/child relationship checks.  
                *)
                return ()
            }
        
        let private validateProposedDeactivationDate
                (account: Account)
                (proposedDate: Instant)
                : Result<unit, string> =
            let ab = activeBegin account
            if proposedDate <= ab then
                Error $"Deactivating account {id account} failed because the active end ({proposedDate}) would be before (or equal to) the active begin ({ab})" else
                Ok () // REQ-AC-4.2
                
        let private validateNoActiveChildrenBeforeDeactivation
            (account: Account)
            (auditEnvelope: AuditEnvelope)
            : Result<unit, string> =
            let accountId = id account
            result {
                let! children = fetchByParentId accountId
                do!
                    if children |> List.exists (fun x -> isActive x (AuditEnvelope.instant auditEnvelope)) // REQ-AC-4.3
                    then Error $"Account {accountId} deactivation failed because one or more child account records is active"
                    else Ok ()
            }
            
// database update functions
        
        /// updateDb is a private function designed as a flexible Account update in support
        /// of various public use case functions. It builds and returns an Account record to
        /// confirm that the resultant state in the persistence layer still meets all
        /// legal/illegal data-state rules.
        let private updateDb
                (accountId: Guid)
                (nameUpdate: FieldUpdate<AccountName>)
                (activeEndUpdate: FieldUpdate<Instant option>)
                (referenceUpdate: FieldUpdate<AccountExternalReference option>)
                (auditEnvelope: AuditEnvelope)
                : Result<Account, string> =
                    
            let baseParams = [
                { name = "@modified"; value = DbInstant (AuditEnvelope.instant auditEnvelope) } // REQ-SYS-3.3 
                { name = "@id"; value = UniqueId accountId };
            ]
            let updates =
                [
                    match nameUpdate with
                    | NoChange -> None
                    | SetTo n -> Some (", name = @name", { name = "@name"; value = CharString (AccountName.value n) })
                    
                    match activeEndUpdate with
                    | NoChange -> None
                    | SetTo e -> Some (", active_end = @active_end", { name = "@active_end"; value = NullableDbInstant e })
                    
                    match referenceUpdate with
                    | NoChange -> None
                    | SetTo r ->
                        let value = r |> Option.map AccountExternalReference.value
                        Some (", external_ref = @external_ref", { name = "@external_ref"; value = NullableCharString  value })
                    
                ] |> List.choose (fun x -> x)
            let setClauses = updates |> List.map fst |> String.concat ""
            let parameters = baseParams @ (updates |> List.map snd)

            let query = $"""
                        UPDATE ledger.account
	                    set
	                        modified_at = @modified -- REQ-SYS-3.3
	                        {setClauses}
	                    WHERE id = @id;
            """
            result {
                do! if updates.IsEmpty then Error "update Account record failed because at least one updatable parameter must be set" else Ok ()
                let! () = executeNonQuery query parameters ExactlyOne
                return! fetchById accountId
            }
        
// public orchestrators

        /// constructNewAndSaveToDb is used where you want to construct a net new Account
        /// and insert it into the DB in one operation   
        let constructNewAndSaveToDb 
                (code: string)
                (name: string)
                (accountType: string)
                (activeBegin: Instant)
                (activeEnd: Instant option)
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                (auditEnvelope: AuditEnvelope)
                : Result<Account, string> =            
            
            result {
                let! validAccount = constructNew code name accountType activeBegin activeEnd subType parentId reference auditEnvelope
                let! () = // REQ-AC-2.6, REQ-AC-2.7
                    (*
                     * Note, we only validate the parent ID here because this is the part
                     * where the Account enters into the DB and the parent validation is
                     * intrinsically a *database* operation. Keeping the validation in this
                     * constructor allows us to keep the other constructors pure FP.
                     *)
                    match parentId with
                    | None -> Ok ()
                    | Some x -> validateParentChildRelationship x (id validAccount) (AuditEnvelope.instant auditEnvelope)               
                let! () = insertNewToDb validAccount // REQ-AC-2.14
                return validAccount
            }
        
        /// deactivateAccount updates the active_end date in the database and returns a
        /// fully reconstituted (and inactive) account. If the caller provides the
        /// explicitEnd, the system will update the active_end to that explicit time.
        /// Otherwise, the active_end will be the system clock time 
        let deactivateAccount
                (accountId: Guid)
                (explicitEnd: Instant option)
                (auditEnvelope: AuditEnvelope)
                : Result<Account, string> = // REQ-AC-4.1
            let deactivationDate =
                match explicitEnd with
                | Some m -> m
                | None -> AuditEnvelope.instant auditEnvelope
            result {
                let! accountCurrent = fetchById accountId
                do! // REQ-AC-4.5
                    match activeEnd accountCurrent with
                    | None -> Ok ()
                    | Some x -> Error $"Account {accountId} deactivation failed because active end is already set to {x}"
                let! () = validateProposedDeactivationDate accountCurrent deactivationDate // REQ-AC-4.2
                let! () = validateNoActiveChildrenBeforeDeactivation accountCurrent auditEnvelope // REQ-AC-4.3
                // todo: validate non-zero balance REQ-AC-4.4
                // todo: validate no journal entries after deactivation date REQ-AC-4.6
                let! newAccount = updateDb accountId NoChange (SetTo (Some deactivationDate)) NoChange auditEnvelope
                return newAccount                
            }
        
        let updateAccountName
                (accountId: Guid)
                (newName: string)
                (auditEnvelope: AuditEnvelope)
                : Result<Account, string> = // REQ-AC-4.8
            result {
                let! validAccountName = AccountName.create newName // REQ-SYS-2.1
                let! newAccount = updateDb accountId (SetTo validAccountName) NoChange NoChange auditEnvelope
                return newAccount
            }
        
        let updateExternalReference 
                (accountId: Guid)
                (newReference: string option)
                (auditEnvelope: AuditEnvelope)
                : Result<Account, string> = // REQ-AC-4.9
            result {                
                let! validRef = // REQ-SYS-2.1
                    match newReference with
                    | Some x -> AccountExternalReference.create x |> Result.map Some
                    | None -> Ok None
                let! newAccount = updateDb accountId NoChange NoChange (SetTo validRef) auditEnvelope
                return newAccount
            }