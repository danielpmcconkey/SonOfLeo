namespace Model

module Ledger =
    
    type AccountCode = private AccountCode of string
    
    module AccountCode =
        let create (raw: string) : Result<AccountCode, string> =
            if System.String.IsNullOrWhiteSpace raw then
                Error "Account code cannot be empty"  // @FT-AC-1.1.1, @FT-AC-1.1.2
            elif raw.Length > 10 then
                Error "Account code cannot exceed 10 characters" // @FT-AC-1.1.3
            else
                Ok (AccountCode raw)

    type AccountName = private AccountName of string
    
    module AccountName =
        let create (raw: string) : Result<AccountName, string> =
            if System.String.IsNullOrWhiteSpace raw then
                Error "Account name cannot be empty"  // @FT-AC-1.1.6, @FT-AC-1.1.7
            elif raw.Length > 100 then
                Error "Account name cannot exceed 100 characters"  // @FT-AC-1.1.8
            else
                Ok (AccountName raw)
    
    type AccountTypeNormalBalance =  // @FT-AC-1.1.9
        | Debit
        | Credit
        
    type AccountType =  // @FT-AC-1.1.10
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense
        
    module AccountType =
        let toDbId(id: AccountType) : int =
            match id with
            | Asset -> 1      // @FT-AC-1.1.11
            | Liability -> 2  // @FT-AC-1.1.12
            | Equity -> 3     // @FT-AC-1.1.13
            | Revenue -> 4    // @FT-AC-1.1.14
            | Expense -> 5    // @FT-AC-1.1.15
        let fromDbId (id: int) : Result<AccountType, string> = // @FT-AC-1.1.10 (parse boundary)
            match id with
            | 1 -> Ok Asset
            | 2 -> Ok Liability
            | 3 -> Ok Equity
            | 4 -> Ok Revenue
            | 5 -> Ok Expense
            | _ -> Error (sprintf "Invalid AccountTypeId: '%d'" id)            
        let normalBalance (t: AccountType) : AccountTypeNormalBalance =
            match t with
            | Asset | Expense -> Debit                  // @FT-AC-1.1.16
            | Liability | Equity | Revenue -> Credit  // @FT-AC-1.1.17
    
    type AccountSubtype =  // @FT-AC-1.1.18
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
        let toDbString (st: AccountSubtype) : string =
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
        let fromDbString (s: string) : Result<AccountSubtype, string> = // @FT-AC-1.1.18 (parse boundary)
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
        let validFor (st: AccountSubtype) : AccountType =
            match st with
            | Cash | FixedAsset | Investment -> Asset  // @FT-AC-1.1.28 
            | CurrentLiability | LongTermLiability -> Liability // @FT-AC-1.1.30 
            | OperatingRevenue | OtherRevenue -> Revenue // @FT-AC-1.1.33
            | OperatingExpense | OtherExpense -> Expense // @FT-AC-1.1.35
            
    type AccountExternalReference = private AccountExternalReference of string
    
    module AccountExternalReference =
        let create (raw: string) : Result<AccountExternalReference, string> =
            if raw.Length > 50 then
                Error "Account external reference cannot exceed 50 characters"  // @FT-AC-1.1.20
            else
                Ok (AccountExternalReference raw)
    type Account =
      { id: System.Guid                                    // @FT-AC-1.1.21, @FT-AC-1.1.22
        code: AccountCode                                  // @FT-AC-1.1.1–1.1.5
        name: AccountName                                  // @FT-AC-1.1.6–1.1.8
        accountType: AccountType                           // @FT-AC-1.1.10, @FT-AC-1.1.23
        isActive: bool                                     // @FT-AC-1.1.24
        createdAt: System.DateTimeOffset                   // @FT-AC-1.1.25
        modifiedAt: System.DateTimeOffset                  // @FT-AC-1.1.26, @FT-AC-1.1.27
        accountSubType: AccountSubtype option              // @FT-AC-1.1.19, @FT-AC-1.1.28–1.1.36
        parentId: System.Guid option                       // @FT-AC-1.1.37–1.1.40
        externalReference: AccountExternalReference option // @FT-AC-1.1.20, @FT-AC-1.1.41
        }                                                  
        
