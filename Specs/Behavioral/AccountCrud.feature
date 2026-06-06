Feature: Account CRUD
    Service-level behavioral specs for creating, updating, and deactivating
    chart-of-accounts entries. All scenarios exercise the service layer.
    Structural constraints (FK, unique index) are covered separately in
    structural specs; these scenarios verify the service rejects invalid
    inputs with meaningful error messages before any DB round-trip where
    possible.

    # 1. Valid and invalid data states for the Account type and related types that comprise the Account type
    
    @FT-AC-1.1 Account code cannot be null
    @FT-AC-1.2 Account code cannot be whitespace only
    @FT-AC-1.3 Account code length cannot exceed 10 chars
    @FT-AC-1.4 No 2 (or more) account records may share the same account code. (Account code must be unique)
    @FT-AC-1.5 Account code is case sensitive. "ACCT-100" and "acct-100" are distinct account codes.
    @FT-AC-1.6 Account name cannot be null
    @FT-AC-1.7 Account name cannot be whitespace only
    @FT-AC-1.8 Account name length cannot exceed 100 chars
    @FT-AC-1.9 Account type normal balance must be one of 'Debit' or 'Credit'
    @FT-AC-1.10 Account type name must be constrained to ['Asset','Liability','Equity','Revenue','Expense']
    @FT-AC-1.11 Account type name of 'Asset' must map to the database ID of 1
    @FT-AC-1.12 Account type name of 'Liability' must map to the database ID of 2
    @FT-AC-1.13 Account type name of 'Equity' must map to the database ID of 3
    @FT-AC-1.14 Account type name of 'Revenue' must map to the database ID of 4
    @FT-AC-1.15 Account type name of 'Expense' must map to the database ID of 5
    @FT-AC-1.16 Account types with name of 'Asset','Expense' must have a normal balance of 'Debit'
    @FT-AC-1.17 Account types with name of 'Liability','Equity','Revenue' must have a normal balance of 'Credit'
    @FT-AC-1.18 Account subtype must be constrained to ['Cash','CurrentLiability','FixedAsset','Investment','LongTermLiability','OperatingExpense','OperatingRevenue','OtherRevenue','OtherExpense']
    @FT-AC-1.19 Account subtype can be null
    @FT-AC-1.20 Account external reference length must not exceed 50 characters
    @FT-AC-1.21 Account ID cannot be null
    @FT-AC-1.22 Account ID must be unique
    @FT-AC-1.23 Account type cannot be null
    @FT-AC-1.24 Account is active should default to true if a null value is provided
    @FT-AC-1.25 Account created at should default to the current runtime timestamp at time of database creation of the record
    @FT-AC-1.26 Account modified at should default to the current runtime timestamp at time of database creation of the record
    @FT-AC-1.27 Account modified at should be updated to the current runtime timestamp at time of database update of the record
    @FT-AC-1.28 Account sub type of 'Cash', 'FixedAsset', and 'Investment' can only be applied account records of type 'Asset'
    @FT-AC-1.29 Account records of type 'Asset' can only have null, 'Cash', 'FixedAsset', and 'Investment' subtypes
    @FT-AC-1.30 Account sub type of 'CurrentLiability', and 'LongTermLiability' can only be applied account records of type 'Liability'
    @FT-AC-1.31 Account records of type 'Liability' can only have null, 'CurrentLiability', and 'LongTermLiability' subtypes
    @FT-AC-1.32 Account records of type 'Equity' can only have null subtypes
    @FT-AC-1.33 Account sub type of 'OperatingRevenue' and 'OtherRevenue' can only be applied account records of type 'Revenue'
    @FT-AC-1.34 Account records of type 'Revenue' can only have null, 'OperatingRevenue' and 'OtherRevenue' subtypes
    @FT-AC-1.35 Account sub type of 'OperatingExpense' and 'OtherExpense' can only be applied account records of type 'Expense'
    @FT-AC-1.36 Account records of type 'Expense' can only have null, 'OperatingExpense' and 'OtherExpense' subtypes
    @FT-AC-1.37 Account parent ID can be null
    @FT-AC-1.38 An account record with the is active flag set to true may not have a parent ID that references an account record with the is active flag set to false
    @FT-AC-1.39 An account record's ID and parent ID cannot be the same (an account cannot be its own parent)
    @FT-AC-1.40 When not null, account parent Id must be a UUID of a preexisting database account record
    @FT-AC-1.41 Account external reference can be null
    
    # 2. Create behaviors for Account type and related types that comprise the Account type
        
    @FT-AC-2.1 When creating an Account record, either through primitive types or through defined types, all raw string values must be trimmed of any leading or trailing white space before being added to the persistence layer or being returned to the caller of the function.
    @FT-AC-2.2 When creating an Account record, the database must be able to persist strings with full UTF-8 support.
    @FT-AC-2.3 When creating an Account record, any string field must be stored in the database with case-perfect fidelity (post-trim).
    @FT-AC-2.4 When creating an Account record via primitive types, the passed in (and trimmed) string for account type must match one of the enumerated account types exactly or the creation must fail.
    @FT-AC-2.5 When creating an Account record, if the provided "is active" value is null, the newly created Account record will be active.
    @FT-AC-2.6 When creating an Account record, if the caller of the function provided a parent ID, the system must confirm that the ID maps to an existing Account in the database.
    @FT-AC-2.7 When creating an Account record, if the caller of the function provided a parent ID, the system must confirm that the parent account's "is active" flag matches the new account record's "is active" state (with a passed null value on the child defaults to active)
    @FT-AC-2.8 When creating an Account record, the system must reject any duplicated ID
    @FT-AC-2.9 When creating an Account record, the system must reject any duplicated account code
    @FT-AC-2.10 When creating an Account record via primitive types, the passed in (and trimmed) string for account sub-type must match one of the enumerated account sub-types exactly or the creation must fail.
    @FT-AC-2.11 When creating an Account record, the system must generate a "created at" timestamp that represents the system clock at time of creation.
    @FT-AC-2.12 When creating an Account record, the system must generate a "modified at" timestamp that represents the system clock at time of creation.
    @FT-AC-2.13 When creating an Account record, the creation function must generate a unique UUID for the ID (new UUIDs may not be passed in).
    @FT-AC-2.14 When creating an Account record, if all validations pass and the passed in arguments represent a valid data state, the creation function must persist the fully validated account record in the database and return an account record with the created ID and created/modified timestamps
    
    
