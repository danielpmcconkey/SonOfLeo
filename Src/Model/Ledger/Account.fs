namespace Model.Ledger

open System
open Utilities
open Utilities.ResultCE
open Utilities.DAL

module Account =
    
    type AccountCode = private AccountCode of string
    
    module AccountCode =
        let value (AccountCode ac) = ac // required because AccountCode is a private string
        let create (raw: string) : Result<AccountCode, string> =
            let trimmed = raw.Trim() // @FT-AC-2.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Account code cannot be empty"  // @FT-AC-1.1, @FT-AC-1.2
            elif trimmed.Length > 10 then
                Error "Account code cannot exceed 10 characters" // @FT-AC-1.3
            else
                Ok (AccountCode trimmed)

    type AccountName = private AccountName of string
    
    module AccountName =
        let value (AccountName an) = an // required because AccountName is a private string
        let create (raw: string) : Result<AccountName, string> =
            let trimmed = raw.Trim() // @FT-AC-2.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Account name cannot be empty"  // @FT-AC-1.6, @FT-AC-1.7
            elif trimmed.Length > 100 then
                Error "Account name cannot exceed 100 characters"  // @FT-AC-1.8
            else
                Ok (AccountName trimmed)
    
    type AccountTypeNormalBalance =  // @FT-AC-1.9
        | Debit
        | Credit
        
    type AccountType =  // @FT-AC-1.10
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense
        
    module AccountType =
        let toDbId(id: AccountType) : int =
            match id with
            | Asset -> 1      // @FT-AC-1.11
            | Liability -> 2  // @FT-AC-1.12
            | Equity -> 3     // @FT-AC-1.13
            | Revenue -> 4    // @FT-AC-1.14
            | Expense -> 5    // @FT-AC-1.15
        let fromDbId (id: int) : Result<AccountType, string> = // @FT-AC-1.10 (parse boundary)
            match id with
            | 1 -> Ok Asset
            | 2 -> Ok Liability
            | 3 -> Ok Equity
            | 4 -> Ok Revenue
            | 5 -> Ok Expense
            | _ -> Error $"Invalid AccountTypeId: '%d{id}'"
        let fromString (accountType: string) : Result<AccountType, string> = // @FT-AC-1.10 (parse boundary)
            match accountType with
            | "Asset" -> Ok Asset
            | "Liability" -> Ok Liability
            | "Equity" -> Ok Equity
            | "Revenue" -> Ok Revenue
            | "Expense" -> Ok Expense
            | _ -> Error $"Invalid AccountTypeString: '%s{accountType}'"   
        let normalBalance (t: AccountType) : AccountTypeNormalBalance =
            match t with
            | Asset | Expense -> Debit                  // @FT-AC-1.16
            | Liability | Equity | Revenue -> Credit  // @FT-AC-1.17
  
    type AccountSubtype =  // @FT-AC-1.18
        | Cash
        | CurrentLiability
        | FixedAsset
        | Investment
        | LongTermLiability
        | OperatingExpense
        | OperatingRevenue
        | OtherRevenue
        | OtherExpense
    
    module AccountSubtype =
        let toString (st: AccountSubtype) : string =
            match st with
            | Cash -> "Cash"
            | CurrentLiability -> "CurrentLiability"
            | FixedAsset -> "FixedAsset"
            | Investment -> "Investment"
            | LongTermLiability -> "LongTermLiability"
            | OperatingRevenue -> "OperatingRevenue"
            | OperatingExpense -> "OperatingExpense"
            | OtherRevenue -> "OtherRevenue"
            | OtherExpense -> "OtherExpense"
        let fromString (s: string) : Result<AccountSubtype, string> = // @FT-AC-1.18 (parse boundary)
            match s with
            | "Cash" -> Ok Cash
            | "CurrentLiability" -> Ok CurrentLiability
            | "FixedAsset" -> Ok FixedAsset
            | "Investment" -> Ok Investment
            | "LongTermLiability" -> Ok LongTermLiability
            | "OperatingRevenue" -> Ok OperatingRevenue
            | "OperatingExpense" -> Ok OperatingExpense
            | "OtherRevenue" -> Ok OtherRevenue
            | "OtherExpense" -> Ok OtherExpense
            | _ -> Error $"Invalid account_subtype: '%s{s}'"
        let validFor (st: AccountSubtype) : AccountType = // confirms that subtype A, B, C can only be associated to type Y
            match st with
            | Cash | FixedAsset | Investment -> Asset  // @FT-AC-1.28
            | CurrentLiability | LongTermLiability -> Liability // @FT-AC-1.30
            | OperatingRevenue | OtherRevenue -> Revenue // @FT-AC-1.33
            | OperatingExpense | OtherExpense -> Expense // @FT-AC-1.35
        
        let validWith (t: AccountType) : AccountSubtype list = // confirms that type Y can only accept subtype A, B, C  
            match t with
            | Asset -> [Cash; FixedAsset; Investment] // @FT-AC-1.29
            | Liability -> [CurrentLiability; LongTermLiability] // @FT-AC-1.31
            | Equity -> [] // @FT-AC-1.32 Account records of type 'Equity' can only have null subtypes
            | Revenue -> [OperatingRevenue; OtherRevenue] // @FT-AC-1.34
            | Expense -> [OperatingExpense; OtherExpense] // @FT-AC-1.36
            
        let validTypeSubtypeCombination (t: AccountType, st: AccountSubtype option) : bool =
            match st with
            | None -> true
            | Some x -> validWith t |> List.contains x
        
            
    type AccountExternalReference = private AccountExternalReference of string
    
    module AccountExternalReference =
        let value (AccountExternalReference er) = er // required due to private value 
        let create (raw: string) : Result<AccountExternalReference, string> =
            if raw.Length > 50 then
                Error "Account external reference cannot exceed 50 characters"  // @FT-AC-1.20
            else
                Ok (AccountExternalReference raw)
    type Account =
      private  {    id: Guid                                           // @FT-AC-1.21, @FT-AC-1.22
                    code: AccountCode                                  // @FT-AC-1.1–1.5
                    name: AccountName                                  // @FT-AC-1.6–1.8
                    accountType: AccountType                           // @FT-AC-1.10, @FT-AC-1.23
                    isActive: bool                                     // @FT-AC-1.24
                    createdAt: DateTimeOffset                          // @FT-AC-1.25
                    modifiedAt: DateTimeOffset                         // @FT-AC-1.26, @FT-AC-1.27
                    accountSubType: AccountSubtype option              // @FT-AC-1.19, @FT-AC-1.28–1.36
                    parentId: Guid option                              // @FT-AC-1.37–1.40
                    externalReference: AccountExternalReference option // @FT-AC-1.20, @FT-AC-1.41
        }
    
    module Account =
        
        // accessor functions
        let id (a:Account) = a.id
        let code (a:Account) = a.code
        let accountType (a:Account) = a.accountType
        let isActive (a:Account) = a.isActive
        let accountSubType (a:Account) = a.accountSubType
        let parentId (a:Account) = a.parentId
        let externalReference (a:Account) = a.externalReference
        
        // creation functions
        
        // used in all "construct" modes by specific constructors designed to be used in specific use cases
        let private constructOmni
                (id: Guid)
                (code: AccountCode)
                (name: AccountName)
                (accountType: AccountType)
                (isActive: bool)
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
                isActive = isActive
                createdAt = createdAt
                modifiedAt = modifiedAt
                accountSubType = subType
                parentId = parentId // todo: check that parent isActive matches child isActive // @FT-AC-1.38, @FT-AC-2.6, @FT-AC-2.7 
                externalReference = reference
            } else
                Error $"Invalid AccountType / AccountSubType combo: {accountType} / {subType}"
        
        // used where the underlying storage layer does not already have a representation of this record
        let constructNew
                (code: string)
                (name: string)
                (accountType: string)
                (isActive: bool option)
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                : Result<Account, string> =            
            let id = Guid.NewGuid() // @FT-AC-1.39, @FT-AC-2.13
            let createdAt = DateTimeOffset.UtcNow // @FT-AC-1.25, @FT-AC-2.11
            let modifiedAt = DateTimeOffset.UtcNow // @FT-AC-1.26, @FT-AC-2.12
            let isActive = match isActive with | None -> true | Some ia -> ia // @FT-AC-1.24, @FT-AC-2.5 
            let codeResult = AccountCode.create(code)
            let nameResult = AccountName.create(name)
            let typeResult = AccountType.fromString(accountType) // @FT-AC-2.4
            let subTypeResult = 
                match subType with
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // @FT-AC-2.10
                | None -> Ok None
            let referenceResult =
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None
            
            result {
                let! validCode = codeResult
                let! validName = nameResult
                let! validType = typeResult
                let! validSubType = subTypeResult
                let! validRef = referenceResult
                return!
                    constructOmni id validCode validName validType isActive
                        createdAt modifiedAt validSubType parentId validRef
            }
        
        // used where the underlying storage layer already represents this record (e.g. database read operations)
        let reconstitute
                (id: Guid)
                (code: string)
                (name: string)
                (accountTypeId: int)
                (isActive: bool)
                (createdAt: DateTimeOffset)
                (modifiedAt: DateTimeOffset)                
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                : Result<Account, string> =            
            let codeResult = AccountCode.create(code)
            let nameResult = AccountName.create(name)
            let typeResult = AccountType.fromDbId(accountTypeId)
            let subTypeResult = 
                match subType with
                | Some st -> AccountSubtype.fromString(st) |> Result.map Some // @FT-AC-2.10
                | None -> Ok None
            let referenceResult =
                match reference with
                | Some r -> AccountExternalReference.create(r) |> Result.map Some
                | None -> Ok None
            
            result {
                let! validCode = codeResult
                let! validName = nameResult
                let! validType = typeResult
                let! validSubType = subTypeResult
                let! validRef = referenceResult
                return!
                    constructOmni id validCode validName validType isActive
                        createdAt modifiedAt validSubType parentId validRef
            }
        let insertNewToDb (account:Account): Result<unit, string> =            
            let query = """
                insert into ledger.account(
	                id, 
                    code, 
                    name, 
                    account_type_id, 
                    is_active, 
                    created_at, 
                    modified_at, 
                    account_subtype, 
                    parent_id, 
                    external_ref)
                values ( --  @FT-DAL-2.1
	                @id, 
                    @code, 
                    @name, 
                    @account_type_id, 
                    @is_active, 
                    @created_at, 
                    @modified_at, 
                    @account_subtype, 
                    @parent_id, 
                    @external_ref);"""
            let subTypeString:string option = account.accountSubType |> Option.map AccountSubtype.toString
            let externalReferenceString:string option = Option.map AccountExternalReference.value account.externalReference
            let parameters = [ //  @FT-DAL-2.1
                { name = "@id"; value = UniqueId account.id };
                { name = "@code"; value = CharString (AccountCode.value account.code) };
                { name = "@name"; value = CharString (AccountName.value account.name) };
                { name = "@account_type_id"; value = Integer (AccountType.toDbId account.accountType) };
                { name = "@is_active"; value = Boolean account.isActive };
                { name = "@created_at"; value = DateTimeWithOffset account.createdAt };
                { name = "@modified_at"; value = DateTimeWithOffset account.modifiedAt };
                { name = "@account_subtype"; value = NullableCharString subTypeString };
                { name = "@parent_id"; value = NullableUniqueId account.parentId };
                { name = "@external_ref"; value = NullableCharString externalReferenceString }
            ]
            executeNonQuery query parameters ExactlyOne
        
        // use where you want to construct a net new Account and insert it into the DB in one operation        
        let constructNewAndSaveToDb 
                (code: string)
                (name: string)
                (accountType: string)
                (isActive: bool option)
                (subType: string option)
                (parentId: Guid option)
                (reference: string option)
                : Result<Account, string> =                    
            constructNew code name accountType isActive subType parentId reference 
            |> Result.bind( fun account ->
                insertNewToDb(account) // @FT-AC-2.14
                |> Result.map (fun () -> account) // @FT-AC-2.14
            )
        
        // The mapRow function is used to pass into DAL read functions to let DAL know how to map our query columns.
        // Thus, we don't need to know anything about the underlying database architecture in this module and the DAL
        // module doesn't need to know anything about our module here 
        let mapAccountRowForDbRead (row: RowReader) : Result<Account, string> =
            // calling reconstitute Account constructor
            reconstitute
                ( row |> RowReader.getUuid "id" )
                ( row |> RowReader.getString "code" )
                ( row |> RowReader.getString "name" )
                ( row |> RowReader.getInt "account_type_id" )
                ( row |> RowReader.getBool "is_active" )
                ( row |> RowReader.getDateTimeOffset "created_at" )
                ( row |> RowReader.getDateTimeOffset "modified_at" )
                ( row |> RowReader.getStringOption "account_subtype" )
                ( row |> RowReader.getUuidOption "parent_id" )
                ( row |> RowReader.getStringOption "external_ref" )
        
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
                select 
	                id, 
                    code, 
                    name, 
                    account_type_id, 
                    is_active, 
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
        
        let fetchById (id: Guid) : Result<Account, string> =
            let predicate = "where id = @id"
            let parameters = [{ name = "@id"; value = UniqueId id };]            
            readRowsFromDb (Some predicate) (None) (parameters) (ExactlyOne)
            |> Result.map List.head
        
        let fetchByCode (code: string) : Result<Account, string> =
            let predicate = "where code = @code"
            let parameters = [{ name = "@code"; value = CharString code };]            
            readRowsFromDb (Some predicate) (None) (parameters) (ExactlyOne)
            |> Result.map List.head
        
        let fetchByParentId (parentId: Guid) : Result<Account list, string> =
            let predicate = "where parent_id = @parent_id"
            let parameters = [{ name = "@parent_id"; value = UniqueId parentId };]            
            readRowsFromDb (Some predicate) (None) (parameters) (NoValidationNeeded)
        
        let fetchByAccountType (accountType: AccountType) : Result<Account list, string> =
            let typeId = AccountType.toDbId(accountType)
            let predicate = "where account_type_id = @type_id"
            let parameters = [{ name = "@type_id"; value = Integer typeId };]            
            readRowsFromDb (Some predicate) (None) (parameters) (NoValidationNeeded)            
            