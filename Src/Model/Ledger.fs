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
    
    type AccountTypeNormalBalance =
        | Debit
        | Credit
        
    type AccountType =
        | Asset
        | Liability
        | Equity
        | Revenue
        | Expense
        
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
    
    type AccountSubtype = 
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
        let fromDbString (s: string) : Result<AccountSubtype, string> =
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
            | _ -> Error (sprintf "Invalid account_subtype: '%s'" s)
            
    type AccountExternalReference = private AccountExternalReference of string
    
    module AccountExternalReference =
        let create (raw: string) : Result<AccountExternalReference, string> =
            if raw.Length > 50 then
                Error "Account external reference cannot exceed 50 characters"
            else
                Ok (AccountExternalReference raw)
    type Account =
      { id: int // needs to become uuid
        code: AccountCode
        name: AccountName
        accountType: AccountType
        isActive: bool // needs to default to true
        createdAt: System.DateTimeOffset
        modifiedAt: System.DateTimeOffset
        accountSubType: AccountSubtype option // need to specify valid subs for type
        parentId: int option
        externalReference: AccountExternalReference option }
        
(*
CREATE TABLE IF NOT EXISTS ledger.account
(
    id integer NOT NULL DEFAULT nextval('ledger.account_id_seq'::regclass),
    code character varying(10) COLLATE pg_catalog."default" NOT NULL,
    name character varying(100) COLLATE pg_catalog."default" NOT NULL,
    account_type_id integer NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    modified_at timestamp with time zone NOT NULL DEFAULT now(),
    account_subtype character varying(25) COLLATE pg_catalog."default",
    parent_id integer,
    external_ref character varying(50) COLLATE pg_catalog."default",
    CONSTRAINT account_pkey PRIMARY KEY (id),
    CONSTRAINT account_code_key UNIQUE (code),
    CONSTRAINT account_account_type_id_fkey FOREIGN KEY (account_type_id)
        REFERENCES ledger.account_type (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT,
    CONSTRAINT account_parent_id_fkey FOREIGN KEY (parent_id)
        REFERENCES ledger.account (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE RESTRICT
)*)