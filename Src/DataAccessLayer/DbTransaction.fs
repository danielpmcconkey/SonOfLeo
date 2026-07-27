module DataAccessLayer.DbTransaction

open Npgsql
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.DbConnections
type NpgTranAndConn = private { connection: NpgsqlConnection; transaction: NpgsqlTransaction }

type DbTransaction = private { npgTranAndConn: NpgTranAndConn option }

type TransactionNeed =
    | NoTransaction
    | NewTransaction
    | ExistingTransaction of DbTransaction

type CompletionAction =
    | Commit
    | Rollback

type ManualTransactionResult<'T> =
    | Failed of AppError * DbTransaction
    | Success of 'T * DbTransaction
    | TransactionCreateFail of AppError

let internal isSome dbTransaction =
    dbTransaction.npgTranAndConn |> Option.isSome

let internal isNone dbTransaction =
    dbTransaction.npgTranAndConn |> Option.isNone

let internal getTranAndConn dbTransaction =
    if dbTransaction.npgTranAndConn |> Option.isNone then
        Error DalCantFetchTransactionOfNone
    else
        let npgTranAndConn = dbTransaction.npgTranAndConn |> Option.get
        let tran = npgTranAndConn.transaction
        let conn = npgTranAndConn.connection
        Ok(tran, conn)

let createDbTransaction () : Result<DbTransaction, AppError> =
    result {
        let! ds = dataSource.Value
        return!
            try
                let connection = ds.OpenConnection()
                let transaction = connection.BeginTransaction()
                Ok { npgTranAndConn = Some { connection = connection; transaction = transaction } }
            with ex ->
                Error(DalErrorDuringTransactionCreation ex)
    }

let private commitOrRollbackAndDispose completionAction dbTransaction : Result<unit, AppError> =
    if dbTransaction.npgTranAndConn |> Option.isNone then
        Error DalCantCompleteTransactionOfNone
    else
        let npgTranAndConn = (dbTransaction.npgTranAndConn |> Option.get)
        let npgTran = npgTranAndConn.transaction
        let conn = npgTranAndConn.connection
        try
            try
                match completionAction with
                | Commit -> npgTran.Commit()
                | Rollback -> npgTran.Rollback()
                Ok()
            with ex ->
                match completionAction with
                | Commit -> Error(DalErrorDuringTransactionCommit ex)
                | Rollback -> Error(DalErrorDuringTransactionRollback ex)
        finally
            npgTran.Dispose()
            conn.Dispose()

let commit (dbTransaction: DbTransaction) : Result<unit, AppError> =
    dbTransaction |> commitOrRollbackAndDispose Commit

let rollback (dbTransaction: DbTransaction) : Result<unit, AppError> =
    dbTransaction |> commitOrRollbackAndDispose Rollback

/// runWithAutoCompleteTransaction executes the func and then either
/// automatically commits or rolls back the transaction, depending
/// on success or failure of the function.
let runWithAutoCompleteTransaction
    (dbTransaction: DbTransaction)
    (func: unit -> Result<'T, AppError>)
    : Result<'T, AppError> =
    if dbTransaction.npgTranAndConn |> Option.isNone then
        Error DalCantUseTransactionOfNoneInAutoCommit
    else
        match () |> func with
        | Error funcError ->
            match dbTransaction |> rollback with
            | Ok _ -> Error funcError
            | Error rollbackError -> Error rollbackError
        | Ok funcResult ->
            match dbTransaction |> commit with
            | Ok _ -> Ok funcResult
            | Error commitError -> Error commitError

/// createNoTransaction is used to easily establish context without creating an unneeded DbContext
let createNoTransaction () : DbTransaction = { npgTranAndConn = None }
