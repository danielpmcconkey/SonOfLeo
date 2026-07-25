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
