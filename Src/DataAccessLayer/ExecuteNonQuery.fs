module DataAccessLayer.ExecuteNonQuery

open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open DataAccessLayer.DbConnections
open Npgsql
open Utilities.AppError
open Utilities.ResultHelper

let executeNonQuery
    (dbTransaction: DbTransaction)
    (query: string)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<unit, AppError> =
    result {
        let! ds = dataSource.Value
        let parameters = buildParamsList parameters
        let! numRows =
            (*
             * standard dotnet I/O libraries throw standard dotnet exceptions
             * we use a try/with block to convert their results into more
             * paradigmatic F# Result Ok/Error at the impure boundary
             *)
            try
                match dbTransaction |> isNone with
                | true ->
                    use connection = ds.OpenConnection()
                    use command = new NpgsqlCommand(query, connection)
                    parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                    Ok(command.ExecuteNonQuery())
                | false ->
                    dbTransaction
                    |> getTranAndConn
                    |> function
                        | Error e -> Error e
                        | Ok(tran, conn) ->
                            use command = new NpgsqlCommand(query, conn)
                            command.Transaction <- tran
                            parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                            Ok(command.ExecuteNonQuery())
            with ex ->
                Error(DalErrorDuringNonQueryExecution ex)
        return! validateNumRows numRows expectedRows // REQ-DAL-2.2
    }
