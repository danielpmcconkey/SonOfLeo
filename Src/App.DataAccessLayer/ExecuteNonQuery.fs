module App.DataAccessLayer.ExecuteNonQuery

open Npgsql
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.DataAccessLayer.DbTransaction
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.DbConnection

let executeNonQuery
    (dbTransaction: DbTransaction)
    (queryStatement: string)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<unit, IAppError> =
    result {
        let! ds = dataSource.Value
        let parameters = buildParamsList parameters
        let! numRows =
            // standard dotnet I/O libraries throw standard dotnet exceptions
            // we use a try/with block to convert their results into more
            // paradigmatic F# Result Ok/Error at the impure boundary
            try
                match dbTransaction |> isNone with
                | true ->
                    use connection = ds.OpenConnection()
                    use command = new NpgsqlCommand(queryStatement, connection)
                    parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                    Ok(command.ExecuteNonQuery())
                | false ->
                    dbTransaction
                    |> transactionAndConnection
                    |> function
                        | Error e -> Error e
                        | Ok(tran, conn) ->
                            use command = new NpgsqlCommand(queryStatement, conn)
                            command.Transaction <- tran
                            parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                            Ok(command.ExecuteNonQuery())
            with ex ->
                Error(DalErrorDuringNonQueryExecution ex)
        return! confirmNumRows numRows expectedRows
    }
