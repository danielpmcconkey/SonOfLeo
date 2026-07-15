module Utilities.AppError

open System
open NodaTime

type AppError =
    

    | DalConnectionStringEnvVarNotFound of unit
    | DalErrorRetrievingAppSettings of exn
    | DalConnectionStringEnvVarContainsConnectionString of unit
    | DalEnvVarNotSet of string
    | DalConnectionStringIsEmpty of unit
    | DalErrorDuringTransactionCreation of exn
    | DalErrorDuringTransactionCommit of exn
    | DalErrorDuringTransactionRollback of exn
    | DalResultantRowsDidntMatchExpectation of string * int
    | DalErrorDuringNonQueryExecution of exn
    | DalErrorDuringReaderQueryExecution of exn
    | DalErrorDuringScalarExecution of exn
    | DalStringUnboxingReturnedNull of unit
    | DalIntUnboxingReturnedNull  of unit
    | DalLongUnboxingReturnedNull  of unit
    | DalDecimalUnboxingReturnedNull  of unit
    | DalLocalDateUnboxingReturnedNull  of unit
    | DalInstantUnboxingReturnedNull  of unit
    | DalUuidUnboxingReturnedNull  of unit
    | DalErrorDuringStringUnboxing of exn
    | DalErrorDuringStringOptionUnboxing of exn
    | DalErrorDuringIntUnboxing of exn
    | DalErrorDuringIntOptionUnboxing of exn
    | DalErrorDuringLongUnboxing of exn
    | DalErrorDuringLongOptionUnboxing of exn
    | DalErrorDuringDecimalUnboxing of exn
    | DalErrorDuringDecimalOptionUnboxing of exn
    | DalErrorDuringLocalDateUnboxing of exn
    | DalErrorDuringLocalDateOptionUnboxing of exn
    | DalErrorDuringInstantUnboxing of exn
    | DalErrorDuringInstantOptionUnboxing of exn
    | DalErrorDuringUuidUnboxing of exn
    | DalErrorDuringUuidOptionUnboxing of exn
    
    | AccountActiveEndBeforeBegin of LocalDate * LocalDate option
    | AccountInvalidTypeSubtypeCombo of string * string option
    | AccountParentAndChildAreSame of Guid option * Guid
    | AccountCodeIsEmpty of string
    | AccountCodeTooLong of string * int
    | AccountNameIsEmpty of string
    | AccountNameTooLong of string * int
    | AccountTypeInvalid of string
    | AccountSubtypeInvalid of string
    | AccountExternalReferenceIsEmpty of string
    | AccountExternalReferenceTooLong of string * int
    | AccountParentIsInactive of Guid
    | AccountParentAndChildTypesDontMatch of string * string
    | AccountUpdateNoOp of unit
    
    | FiscalPeriodInvalidKeyString of string
    | FiscalPeriodNoPeriodMatchingKey of string
    

module AppError =
    let toMessage = function
    | DalConnectionStringEnvVarNotFound _ -> "ConnectionStringEnvVar not found in appsettings.json."
    | DalErrorRetrievingAppSettings ex -> $"Error retrieving appsettings.json. Error message: {ex.Message}{Environment.NewLine} {ex.StackTrace}" // REQ-DAL-1.3, REQ-NGUI-1.3.1
    | DalConnectionStringEnvVarContainsConnectionString _ -> "ConnectionStringEnvVar contains a connection string, not an env var name."
    | DalEnvVarNotSet envVarName -> $"Environment variable {envVarName} not set or empty."
    | DalConnectionStringIsEmpty _ -> "Connection string is empty."
    | DalErrorDuringTransactionCreation ex -> $"Database error during transaction creation: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringTransactionCommit ex -> $"Database error during transaction commit. {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringTransactionRollback ex -> $"Database error during transaction rollback. You probably have corrupted data that you should address immediately. {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalResultantRowsDidntMatchExpectation (expected, actual) -> $"Resultant rows didn't match expectation. Expected {expected}. Actual {actual}."
    | DalErrorDuringNonQueryExecution ex -> $"Database error during non query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringReaderQueryExecution ex -> $"Database error during reader query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringScalarExecution ex -> $"Database error during scalar execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalStringUnboxingReturnedNull _ -> "String unboxing returned DB null"
    | DalIntUnboxingReturnedNull _ -> "Int unboxing returned DB null"
    | DalLongUnboxingReturnedNull _ -> "Long unboxing returned DB null"
    | DalDecimalUnboxingReturnedNull _ -> "Decimal unboxing returned DB null"
    | DalLocalDateUnboxingReturnedNull _ -> "LocalDate unboxing returned DB null"
    | DalInstantUnboxingReturnedNull _ -> "Instant unboxing returned DB null"
    | DalUuidUnboxingReturnedNull _ -> "UUID unboxing returned DB null"
    | DalErrorDuringStringUnboxing ex -> $"Database error during string unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringStringOptionUnboxing ex -> $"Database error during string option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringIntUnboxing ex -> $"Database error during int unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringIntOptionUnboxing ex -> $"Database error during int option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringLongUnboxing ex -> $"Database error during long unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringLongOptionUnboxing ex -> $"Database error during long option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringDecimalUnboxing ex -> $"Database error during decimal unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringDecimalOptionUnboxing ex -> $"Database error decimal string option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringLocalDateUnboxing ex -> $"Database error during LocalDate unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringLocalDateOptionUnboxing ex -> $"Database error during LocalDate option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringInstantUnboxing ex -> $"Database error during instant unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringInstantOptionUnboxing ex -> $"Database error during instant option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringUuidUnboxing ex -> $"Database error during UUID unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    | DalErrorDuringUuidOptionUnboxing ex -> $"Database error during UUID option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
    
    | AccountActiveEndBeforeBegin (activeBegin, activeEnd) -> $"Active end ({activeEnd}) cannot be before active begin ({activeBegin})"
    | AccountInvalidTypeSubtypeCombo (accountType, subtype) -> $"Invalid AccountType / AccountSubType combo: {accountType} / {subtype}"
    | AccountParentAndChildAreSame (parent, child) -> $"A child account ({child}) cannot be its own parent ({parent})."
    | AccountCodeIsEmpty code -> $"Account code cannot be empty. Provided code is {code}."
    | AccountCodeTooLong (code, max) -> $"Account code cannot exceed {max} characters. Provided code is {code}."
    | AccountNameIsEmpty name -> $"Account name cannot be empty. Provided name is {name}."
    | AccountNameTooLong (name, max) -> $"Account name cannot exceed {max} characters. Provided name is {name}."
    | AccountTypeInvalid typeString -> $"Provided string of '{typeString}' is not a valid account type."
    | AccountSubtypeInvalid subtype -> $"Provided string of '{subtype}' is not a valid account subtype."
    | AccountExternalReferenceIsEmpty externalReference -> $"Account external reference cannot be empty. Provided external reference is {externalReference}."
    | AccountExternalReferenceTooLong (externalReference, max) -> $"Account external reference cannot exceed {max} characters. Provided external reference is {externalReference}."
    | AccountParentIsInactive uuid -> $"Parent account {uuid} failed \"is active\" check."
    | AccountParentAndChildTypesDontMatch (parent, child) -> $"Parent ({parent}) and child ({child}) account types do not match."
    | AccountUpdateNoOp _ -> "Updating the account record failed because at least one updatable parameter must be set."
    
    | FiscalPeriodInvalidKeyString key -> $"Passed string \"{key}\" is invalid as a Period Key."
    | FiscalPeriodNoPeriodMatchingKey key -> $"No Fiscal Period matching {key} could be found in the database."