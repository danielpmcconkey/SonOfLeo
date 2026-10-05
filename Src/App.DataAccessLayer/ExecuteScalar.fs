module App.DataAccessLayer.ExecuteScalar

open System
open Npgsql
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.DbConnection
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.QueryParameter

let stringUnboxing (objRaw: obj) : Result<string, IAppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error DalStringUnboxingReturnedNull
        else
            Ok(objRaw :?> string)
    with ex ->
        Error(DalErrorDuringStringUnboxing ex)

let longUnboxing (objRaw: obj) : Result<int64, IAppError> =
    try
        if objRaw = null || objRaw = DBNull.Value then
            Error(DalLongUnboxingReturnedNull)
        else
            let unboxed: int64 = objRaw |> unbox
            Ok unboxed
    with ex ->
        Error(DalErrorDuringLongUnboxing ex)

let executeScalar
    (dbTransaction: DbTransaction)
    (queryStatement: string)
    (parameters: QueryParameter list)
    (unboxingFunc: obj -> Result<'T, IAppError>)
    : Result<'T, IAppError> =
    result {
        let! ds = dataSource.Value
        let parameters = buildParamsList parameters
        let! returnVal =
            // standard dotnet I/O libraries throw standard dotnet exceptions
            // we use a try/with block to convert their results into more
            // paradigmatic F# Result Ok/Error at the impure boundary
            let objResult =
                try
                    match dbTransaction |> transactionAndConnection with
                    | None ->
                        use connection = ds.OpenConnection()
                        use command = new NpgsqlCommand(queryStatement, connection)
                        parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                        Ok (command.ExecuteScalar())
                    | Some(tran, conn) ->
                        use command = new NpgsqlCommand(queryStatement, conn)
                        command.Transaction <- tran
                        parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                        Ok (command.ExecuteScalar())
                with ex ->
                    Error(DalErrorDuringScalarExecution ex)
            match objResult with
            | Error e -> Error e
            | Ok x -> x |> unboxingFunc
        return returnVal
    }
