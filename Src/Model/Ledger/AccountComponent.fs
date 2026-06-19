namespace Model.Ledger

open System
open NodaTime

module AccountComponent =
    
    (*
     * This module defines simple types that are used by the Account type. It really
     * only exists as a separate module because Account was getting huge
     *)

    type AccountActivityPeriod =
        private {   activeBegin: Instant                        // REQ-AC-1.42, REQ-AC-1.44
                    activeEnd: Instant option                   // REQ-AC-1.43, REQ-AC-1.45
        }
    
    module AccountActivityPeriod = // REQ-AC-2.17
        let activeBegin (a:AccountActivityPeriod) = a.activeBegin
        let activeEnd (a:AccountActivityPeriod) = a.activeEnd
        let create (rawBegin: Instant) (rawEnd: Instant option) : Result<AccountActivityPeriod, string> =
            match rawEnd with
            | None -> Ok { activeBegin = rawBegin; activeEnd = None }
            | Some x -> 
                if x <= rawBegin then Error "Active end cannot be before active begin" else // REQ-AC-1.46, REQ-AC-2.18
                    Ok { activeBegin = rawBegin; activeEnd = rawEnd }
        let isActive
                (referencePoint: Instant) // REQ-AC-1.48.1
                (aap: AccountActivityPeriod)
                : bool =
            let beginDate = activeBegin aap
            let endDate = activeEnd aap
            match endDate with
            | None when beginDate <= referencePoint -> true
            | Some x when beginDate <= referencePoint && x > referencePoint -> true // REQ-AC-1.48
            | _ -> false
    
    type AccountCode = private AccountCode of string
    
    module AccountCode =
        let value (AccountCode ac) = ac // required because AccountCode is a private string
        let create (raw: string) : Result<AccountCode, string> =
            let trimmed = raw.Trim() // REQ-SYS-1.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Account code cannot be empty"  // REQ-AC-1.1, REQ-AC-1.2, REQ-SYS-1.2
            elif trimmed.Length > 10 then
                Error "Account code cannot exceed 10 characters" // REQ-AC-1.3
            else
                Ok (AccountCode trimmed)

    type AccountName = private AccountName of string
    
    module AccountName =
        let value (AccountName an) = an // required because AccountName is a private string
        let create (raw: string) : Result<AccountName, string> =
            let trimmed = raw.Trim() // REQ-SYS-1.1
            if String.IsNullOrWhiteSpace trimmed then
                Error "Account name cannot be empty"  // REQ-AC-1.6, REQ-AC-1.7, REQ-SYS-1.2
            elif trimmed.Length > 100 then
                Error "Account name cannot exceed 100 characters"  // REQ-AC-1.8
            else
                Ok (AccountName trimmed)
    
    type AccountTypeNormalBalance =  // REQ-AC-1.9
        | Debit
        | Credit
        
    type AccountType =  // REQ-AC-1.10
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense
        
    module AccountType =
        let toDbId (id: AccountType) : int =
            match id with
            | Asset -> 1      // REQ-AC-1.11
            | Liability -> 2  // REQ-AC-1.12
            | Equity -> 3     // REQ-AC-1.13
            | Revenue -> 4    // REQ-AC-1.14
            | Expense -> 5    // REQ-AC-1.15
        let fromDbId (id: int) : Result<AccountType, string> = // REQ-AC-1.10 (parse boundary)
            match id with
            | 1 -> Ok Asset
            | 2 -> Ok Liability
            | 3 -> Ok Equity
            | 4 -> Ok Revenue
            | 5 -> Ok Expense
            | _ -> Error $"Invalid AccountTypeId: '%d{id}'"
        let fromString (accountType: string) : Result<AccountType, string> = // REQ-AC-1.10 (parse boundary)
            match accountType.Trim() with // REQ-SYS-1.1
            | "Asset" -> Ok Asset
            | "Liability" -> Ok Liability
            | "Equity" -> Ok Equity
            | "Revenue" -> Ok Revenue
            | "Expense" -> Ok Expense
            | _ -> Error $"Invalid AccountTypeString: '%s{accountType}'"
        
        let toString (at: AccountType) : string =
            match at with
            | Asset -> "Asset"
            | Liability -> "Liability"
            | Equity -> "Equity"
            | Revenue -> "Revenue"
            | Expense -> "Expense"
            
        let normalBalance (t: AccountType) : AccountTypeNormalBalance =
            match t with
            | Asset | Expense -> Debit                  // REQ-AC-1.16
            | Liability | Equity | Revenue -> Credit  // REQ-AC-1.17
  
    type AccountSubtype =  // REQ-AC-1.18
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
        let fromString (s: string) : Result<AccountSubtype, string> = // REQ-AC-1.18 (parse boundary)
            match s.Trim() with // REQ-SYS-1.1
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
            | Cash | FixedAsset | Investment -> Asset  // REQ-AC-1.28
            | CurrentLiability | LongTermLiability -> Liability // REQ-AC-1.30
            | OperatingRevenue | OtherRevenue -> Revenue // REQ-AC-1.33
            | OperatingExpense | OtherExpense -> Expense // REQ-AC-1.35
        
        let validWith (t: AccountType) : AccountSubtype list = // confirms that type Y can only accept subtype A, B, C  
            match t with
            | Asset -> [Cash; FixedAsset; Investment] // REQ-AC-1.29
            | Liability -> [CurrentLiability; LongTermLiability] // REQ-AC-1.31
            | Equity -> [] // REQ-AC-1.32 Account records of type 'Equity' can only have null subtypes
            | Revenue -> [OperatingRevenue; OtherRevenue] // REQ-AC-1.34
            | Expense -> [OperatingExpense; OtherExpense] // REQ-AC-1.36
            
        let validTypeSubtypeCombination (t: AccountType, st: AccountSubtype option) : bool =
            match st with
            | None -> true
            | Some x -> validWith t |> List.contains x
        
            
    type AccountExternalReference = private AccountExternalReference of string
    
    module AccountExternalReference =
        let value (AccountExternalReference er) = er // required due to private value 
        let create (raw: string) : Result<AccountExternalReference, string> =
            let trimmed = raw.Trim() // REQ-SYS-1.1
            if trimmed = String.Empty then
                Error $"Account external reference of \"{raw}\" is empty" // REQ-AC-1.49, REQ-SYS-1.3
            elif trimmed.Length > 50 then
                Error $"Account external reference of \"{trimmed}\" exceeds 50 characters"  // REQ-AC-1.20
            else
                Ok (AccountExternalReference trimmed)
