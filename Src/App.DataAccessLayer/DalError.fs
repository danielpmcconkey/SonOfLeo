module App.DataAccessLayer.DalError

open System
open App.Utility.IAppError

type DalError =
    | DalCantCompleteTransactionOfNone
    | DalCantUseTransactionOfNoneInAutoCommit
    | DalConnectionStringConfigRetrievalError of string
    | DalConnectionStringEnvVarContainsConnectionString
    | DalConnectionStringEnvVarSettingIsEmpty
    | DalConnectionStringIsEmpty
    | DalEnvVarNotSet of string
    | DalErrorDuringAutoCompleteTransactionRun of exn
    | DalErrorDuringLongUnboxing of exn
    | DalErrorDuringNonQueryExecution of exn
    | DalErrorDuringReaderQueryExecution of exn
    | DalErrorDuringScalarExecution of exn
    | DalErrorDuringStringUnboxing of exn
    | DalErrorDuringTransactionCommit of exn
    | DalErrorDuringTransactionCreation of exn
    | DalErrorDuringTransactionRollback of exn
    | DalLongUnboxingReturnedNull
    | DalNoOp of string * int
    | DalResultantRowsDidntMatchExpectation of string * int
    | DalStringUnboxingReturnedNull
    
    interface IAppError with
        member this.DomainName = nameof DalError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with        
            | DalCantCompleteTransactionOfNone -> "Error. You cannot commit or rollback with a raw transaction of None."
            | DalCantUseTransactionOfNoneInAutoCommit -> "Error. You cannot send a transaction of None into the auto-commit pipeline."
            | DalConnectionStringConfigRetrievalError s -> $"Error reading the config for the connection string. Message: {s}"
            | DalConnectionStringEnvVarContainsConnectionString -> "ConnectionStringEnvVar contains a connection string, not an env var name."
            | DalConnectionStringEnvVarSettingIsEmpty ->
                "The ConnectionStringEnvVar setting in appsettings.json is empty. It must name the environment variable that holds the connection string."
            | DalConnectionStringIsEmpty -> "Connection string is empty."
            | DalEnvVarNotSet envVarName -> $"Environment variable {envVarName} not set."
            | DalErrorDuringAutoCompleteTransactionRun ex -> $"Database error during runWithAutoCompleteTransaction. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringLongUnboxing ex -> $"Database error during long unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringNonQueryExecution ex -> $"Database error during non query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringReaderQueryExecution ex -> $"Database error during reader query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringScalarExecution ex -> $"Database error during scalar execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringStringUnboxing ex -> $"Database error during string unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringTransactionCommit ex -> $"Database error during transaction commit. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringTransactionCreation ex -> $"Database error during transaction creation: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalErrorDuringTransactionRollback ex -> $"Database error during transaction rollback. You probably have corrupted data that you should address immediately. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | DalLongUnboxingReturnedNull -> "Long unboxing returned DB null"
            | DalNoOp(expected, actual) -> $"Resultant rows was ExactlyOne and the result set returned zero rows. Expected {expected}. Actual {actual}."
            | DalResultantRowsDidntMatchExpectation(expected, actual) -> $"Resultant rows didn't match expectation. Expected {expected}. Actual {actual}."
            | DalStringUnboxingReturnedNull -> "String unboxing returned DB null"

let toMessage (e: DalError) = (e :> IAppError).ToMessage()
let toAppError (e: DalError) : IAppError = e :> IAppError
let error (e: DalError) : Result<'T, IAppError> = Error (e :> IAppError)

/// whenNoRows swaps DalNoOp, the DAL's backstop for "no rows came back", for the domain error that names what was
/// missing. Only the caller knows what an empty result means. Every other error passes through untouched.
let whenNoRows (specific: #IAppError) (result: Result<'T, IAppError>) : Result<'T, IAppError> =
    match result with
    | Error (AsError (DalNoOp _)) -> Error (specific :> IAppError)
    | other -> other
