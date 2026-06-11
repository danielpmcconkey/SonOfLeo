namespace Model.Ledger

open System

module AccountComponent =
    
    (*
     * This module defines simple types that are used by the Account type. It really
     * only exists as a separate module because Account was getting huge
     *)

    type AccountActivityPeriod =
        private {   activeBegin: DateTimeOffset                        // FT-AC-1.42, FT-AC-1.44
                    activeEnd: DateTimeOffset option                   // FT-AC-1.43, FT-AC-1.45
        }
    
    module AccountActivityPeriod = // FT-AC-2.17
        let activeBegin (a:AccountActivityPeriod) = a.activeBegin
        let activeEnd (a:AccountActivityPeriod) = a.activeEnd
        let create (rawBegin: DateTimeOffset) (rawEnd: DateTimeOffset option) : Result<AccountActivityPeriod, string> =
            match rawEnd with
            | None -> Ok { activeBegin = rawBegin; activeEnd = None }
            | Some x -> 
                if x <= rawBegin then Error "Active end cannot be before active begin" else // FT-AC-1.46, FT-AC-2.18
                    Ok { activeBegin = rawBegin; activeEnd = rawEnd }
    
    type AccountCode = private AccountCode of string
    
    module AccountCode =
        let value (AccountCode ac) = ac // required because AccountCode is a private string
        let create (raw: string) : Result<AccountCode, string> =
            let trimmed = raw.Trim() // FT-AC-2.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Account code cannot be empty"  // FT-AC-1.1, FT-AC-1.2
            elif trimmed.Length > 10 then
                Error "Account code cannot exceed 10 characters" // FT-AC-1.3
            else
                Ok (AccountCode trimmed)

    type AccountName = private AccountName of string
    
    module AccountName =
        let value (AccountName an) = an // required because AccountName is a private string
        let create (raw: string) : Result<AccountName, string> =
            let trimmed = raw.Trim() // FT-AC-2.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Account name cannot be empty"  // FT-AC-1.6, FT-AC-1.7
            elif trimmed.Length > 100 then
                Error "Account name cannot exceed 100 characters"  // FT-AC-1.8
            else
                Ok (AccountName trimmed)
    
    type AccountTypeNormalBalance =  // FT-AC-1.9
        | Debit
        | Credit
        
    type AccountType =  // FT-AC-1.10
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense
        
    module AccountType =
        let toDbId(id: AccountType) : int =
            match id with
            | Asset -> 1      // FT-AC-1.11
            | Liability -> 2  // FT-AC-1.12
            | Equity -> 3     // FT-AC-1.13
            | Revenue -> 4    // FT-AC-1.14
            | Expense -> 5    // FT-AC-1.15
        let fromDbId (id: int) : Result<AccountType, string> = // FT-AC-1.10 (parse boundary)
            match id with
            | 1 -> Ok Asset
            | 2 -> Ok Liability
            | 3 -> Ok Equity
            | 4 -> Ok Revenue
            | 5 -> Ok Expense
            | _ -> Error $"Invalid AccountTypeId: '%d{id}'"
        let fromString (accountType: string) : Result<AccountType, string> = // FT-AC-1.10 (parse boundary)
            match accountType.Trim() with
            | "Asset" -> Ok Asset
            | "Liability" -> Ok Liability
            | "Equity" -> Ok Equity
            | "Revenue" -> Ok Revenue
            | "Expense" -> Ok Expense
            | _ -> Error $"Invalid AccountTypeString: '%s{accountType}'"   
        let normalBalance (t: AccountType) : AccountTypeNormalBalance =
            match t with
            | Asset | Expense -> Debit                  // FT-AC-1.16
            | Liability | Equity | Revenue -> Credit  // FT-AC-1.17
  
    type AccountSubtype =  // FT-AC-1.18
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
        let fromString (s: string) : Result<AccountSubtype, string> = // FT-AC-1.18 (parse boundary)
            match s.Trim() with
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
            | Cash | FixedAsset | Investment -> Asset  // FT-AC-1.28
            | CurrentLiability | LongTermLiability -> Liability // FT-AC-1.30
            | OperatingRevenue | OtherRevenue -> Revenue // FT-AC-1.33
            | OperatingExpense | OtherExpense -> Expense // FT-AC-1.35
        
        let validWith (t: AccountType) : AccountSubtype list = // confirms that type Y can only accept subtype A, B, C  
            match t with
            | Asset -> [Cash; FixedAsset; Investment] // FT-AC-1.29
            | Liability -> [CurrentLiability; LongTermLiability] // FT-AC-1.31
            | Equity -> [] // FT-AC-1.32 Account records of type 'Equity' can only have null subtypes
            | Revenue -> [OperatingRevenue; OtherRevenue] // FT-AC-1.34
            | Expense -> [OperatingExpense; OtherExpense] // FT-AC-1.36
            
        let validTypeSubtypeCombination (t: AccountType, st: AccountSubtype option) : bool =
            match st with
            | None -> true
            | Some x -> validWith t |> List.contains x
        
            
    type AccountExternalReference = private AccountExternalReference of string
    
    module AccountExternalReference =
        let value (AccountExternalReference er) = er // required due to private value 
        let create (raw: string) : Result<AccountExternalReference, string> =
            let trimmed = raw.Trim()
            if trimmed = String.Empty then
                Error $"Account external reference of \"{raw}\" is empty" // FT-AC-1.49
            elif trimmed.Length > 50 then
                Error $"Account external reference of \"{trimmed}\" exceeds 50 characters"  // FT-AC-1.20
            else
                Ok (AccountExternalReference trimmed)
