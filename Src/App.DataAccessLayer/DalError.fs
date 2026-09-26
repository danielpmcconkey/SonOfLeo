module App.DataAccessLayer.DalError

open System
open App.Utility.IAppError

type DalError =
    | DalCantCompleteTransactionOfNone
    | DalCantFetchTransactionOfNone
    | DalCantUseTransactionOfNoneInAutoCommit
    | DalConnectionStringConfigRetrievalError of string
    | DalConnectionStringEnvVarContainsConnectionString
    | DalConnectionStringEnvVarNotFound
    | DalConnectionStringIsEmpty
    | DalDecimalUnboxingReturnedNull
    | DalEnvVarNotSet of string
    | DalErrorDuringAutoCompleteTransactionRun of exn
    | DalErrorDuringDecimalOptionUnboxing of exn
    | DalErrorDuringDecimalUnboxing of exn
    | DalErrorDuringInstantOptionUnboxing of exn
    | DalErrorDuringInstantUnboxing of exn
    | DalErrorDuringIntOptionUnboxing of exn
    | DalErrorDuringIntUnboxing of exn
    | DalErrorDuringLocalDateOptionUnboxing of exn
    | DalErrorDuringLocalDateUnboxing of exn
    | DalErrorDuringLongOptionUnboxing of exn
    | DalErrorDuringLongUnboxing of exn
    | DalErrorDuringNonQueryExecution of exn
    | DalErrorDuringReaderQueryExecution of exn
    | DalErrorDuringScalarExecution of exn
    | DalErrorDuringStringOptionUnboxing of exn
    | DalErrorDuringStringUnboxing of exn
    | DalErrorDuringTransactionCommit of exn
    | DalErrorDuringTransactionCreation of exn
    | DalErrorDuringTransactionRollback of exn
    | DalErrorDuringUuidOptionUnboxing of exn
    | DalErrorDuringUuidUnboxing of exn
    | DalInstantUnboxingReturnedNull
    | DalIntUnboxingReturnedNull
    | DalLocalDateUnboxingReturnedNull
    | DalLongUnboxingReturnedNull
    | DalNoOp of string * int
    | DalResultantRowsDidntMatchExpectation of string * int
    | DalStringUnboxingReturnedNull
    | DalUuidUnboxingReturnedNull
    | ReaderFailedToConvertRawRows of IAppError
    
    interface IAppError with
        member this.DomainName = nameof DalError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with        
            | DalCantCompleteTransactionOfNone -> "Error. You cannot commit or rollback with a raw transaction of None."
            | DalCantFetchTransactionOfNone -> "Error. You cannot fetch a connection with a raw transaction of None."
            | DalCantUseTransactionOfNoneInAutoCommit -> "Error. You cannot send a transaction of None into the auto-commit pipeline."
            | DalConnectionStringConfigRetrievalError s -> $"Error reading the config for the connection string. Message: {s}"
            | DalConnectionStringEnvVarContainsConnectionString -> "ConnectionStringEnvVar contains a connection string, not an env var name."
            | DalConnectionStringEnvVarNotFound -> "ConnectionStringEnvVar not found in appsettings.json."
            | DalConnectionStringIsEmpty -> "Connection string is empty."
            | DalDecimalUnboxingReturnedNull -> "Decimal unboxing returned DB null"
            | DalEnvVarNotSet envVarName -> $"Environment variable {envVarName} not set or empty."
            | DalErrorDuringAutoCompleteTransactionRun ex -> $"Database error during runWithAutoCompleteTransaction. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringDecimalOptionUnboxing ex -> $"Database error decimal option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringDecimalUnboxing ex -> $"Database error during decimal unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringInstantOptionUnboxing ex -> $"Database error during instant option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringInstantUnboxing ex -> $"Database error during instant unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringIntOptionUnboxing ex -> $"Database error during int option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringIntUnboxing ex -> $"Database error during int unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringLocalDateOptionUnboxing ex -> $"Database error during LocalDate option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringLocalDateUnboxing ex -> $"Database error during LocalDate unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringLongOptionUnboxing ex -> $"Database error during long option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringLongUnboxing ex -> $"Database error during long unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringNonQueryExecution ex -> $"Database error during non query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringReaderQueryExecution ex -> $"Database error during reader query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringScalarExecution ex -> $"Database error during scalar execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringStringOptionUnboxing ex -> $"Database error during string option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringStringUnboxing ex -> $"Database error during string unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringTransactionCommit ex -> $"Database error during transaction commit. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringTransactionCreation ex -> $"Database error during transaction creation: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringTransactionRollback ex -> $"Database error during transaction rollback. You probably have corrupted data that you should address immediately. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringUuidOptionUnboxing ex -> $"Database error during UUID option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringUuidUnboxing ex -> $"Database error during UUID unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalInstantUnboxingReturnedNull -> "Instant unboxing returned DB null"
            | DalIntUnboxingReturnedNull -> "Int unboxing returned DB null"
            | DalLocalDateUnboxingReturnedNull -> "LocalDate unboxing returned DB null"
            | DalLongUnboxingReturnedNull -> "Long unboxing returned DB null"
            | DalNoOp(expected, actual) -> $"Resultant rows was either ExactlyOne or OneOrMany and the result set returned zero rows. Expected {expected}. Actual {actual}."
            | DalResultantRowsDidntMatchExpectation(expected, actual) -> $"Resultant rows didn't match expectation. Expected {expected}. Actual {actual}."
            | DalStringUnboxingReturnedNull -> "String unboxing returned DB null"
            | DalUuidUnboxingReturnedNull -> "UUID unboxing returned DB null"
            | ReaderFailedToConvertRawRows appError -> $"Failure to convert raw rows on DB read. Message: {appError.ToMessage()}"

let toMessage (e: DalError) = (e :> IAppError).ToMessage()
let toAppError (e: DalError) : IAppError = e :> IAppError
let error (e: DalError) : Result<'T, IAppError> = Error (e :> IAppError)

/// whenNoRows swaps DalNoOp, the DAL's backstop for "no rows came back", for the domain error that names what was
/// missing. Only the caller knows what an empty result means. Every other error passes through untouched.
let whenNoRows (specific: #IAppError) (result: Result<'T, IAppError>) : Result<'T, IAppError> =
    match result with
    | Error (AsError (DalNoOp _)) -> Error (specific :> IAppError)
    | other -> other
