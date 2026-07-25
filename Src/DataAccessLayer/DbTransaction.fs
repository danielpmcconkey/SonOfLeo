module DataAccessLayer.DbTransaction

open Npgsql
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.DbConnections

type NpgTranAndConn = private { connection: NpgsqlConnection; transaction: NpgsqlTransaction }

type DbTransaction = private { npgTranAndConn: NpgTranAndConn option }

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

let private createDbTransaction () : Result<DbTransaction, AppError> =
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

let withAutoCommitTransaction (func: DbTransaction -> Result<'T, AppError>) : Result<'T, AppError> =
    match createDbTransaction() with
    | Error createError -> Error createError
    | Ok tran ->
        match tran |> func with
        | Error funcError ->
            match tran |> rollback with
            | Ok _ -> Error funcError
            | Error rollbackError -> Error rollbackError
        | Ok funcResult ->
            match tran |> commit with
            | Ok _ -> Ok funcResult
            | Error commitError -> Error commitError

let withManualCommitTransaction (func: DbTransaction -> Result<'T, AppError>) : ManualTransactionResult<'T> =
    match createDbTransaction() with
    | Error createError -> TransactionCreateFail createError
    | Ok tran ->
        match tran |> func with
        | Ok funcResult -> Success(funcResult, tran)
        | Error funcError -> Failed(funcError, tran)

let withoutTransaction (func: DbTransaction -> Result<'T, AppError>) : Result<'T, AppError> =
    { npgTranAndConn = None } |> func
