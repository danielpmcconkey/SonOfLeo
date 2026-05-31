namespace Model

module Ledger =
    
    type AccountCode = private AccountCode of string
    
    module AccountCode =
        let create (raw: string) : Result<AccountCode, string> =
            if System.String.IsNullOrWhiteSpace raw then
                Error "Account code cannot be empty"
            elif raw.Length > 10 then
                Error "Account code cannot exceed 10 characters"
            else
                Ok (AccountCode raw)

    type AccountName = private AccountName of string
    
    module AccountName =
        let create (raw: string) : Result<AccountName, string> =
            if System.String.IsNullOrWhiteSpace raw then
                Error "Account name cannot be empty"
            elif raw.Length > 100 then
                Error "Account name cannot exceed 100 characters"
            else
                Ok (AccountName raw)
    
    type AccountType =
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense
        
    type AccountTypeNormalBalance =
        | Debit
        | Credit
        
    module AccountType =
        let toDbId(id: AccountType) : int =
            match id with
            | Asset -> 1
            | Liability -> 2
            | Equity -> 3
            | Revenue -> 4
            | Expense -> 5
        let fromDbId (id: int) : Result<AccountType, string> =
            match id with
            | 1 -> Ok Asset
            | 2 -> Ok Liability
            | 3 -> Ok Equity
            | 4 -> Ok Revenue
            | 5 -> Ok Expense
            | _ -> Error (sprintf "Invalid AccountTypeId: '%d'" id)            
        let normalBalance (t: AccountType) : AccountTypeNormalBalance =
            match t with
            | Asset | Expense -> Debit
            | Liability | Equity | Revenue -> Credit
    

