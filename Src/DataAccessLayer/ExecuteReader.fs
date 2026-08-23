module DataAccessLayer.ExecuteReader

open System
open DataAccessLayer.DbTransaction
open DataAccessLayer.QueryParameters
open DataAccessLayer.DbConnections
open NodaTime
open Npgsql
open Utilities.ResultHelper
open Utilities.AppError
open System.Data

type AcceptableExpectedRows =
    | Zero
    | ExactlyOne
    | OneOrMany
    | AnyQuantityIsAcceptable

let internal confirmNumRows (numRows: int) (expectation: AcceptableExpectedRows) : Result<unit, AppError> =
    match expectation with
    | Zero when numRows = 0 -> Ok()
    | ExactlyOne when numRows = 1 -> Ok()
    | OneOrMany when numRows >= 1 -> Ok()
    | AnyQuantityIsAcceptable -> Ok()
    | _ -> Error(DalResultantRowsDidntMatchExpectation(expectation.ToString(), numRows))

type RowReader = private { reader: Common.DbDataReader }

module RowReader =
    let create (reader: Common.DbDataReader) : RowReader = { reader = reader }
    let getInt (col: string) (r: RowReader) =
        r.reader.GetInt32(r.reader.GetOrdinal(col))
    let getIntOption (col: string) (r: RowReader) : int option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetInt32(ordinal))
    let getNumeric (col: string) (r: RowReader) =
        r.reader.GetDecimal(r.reader.GetOrdinal(col))
    let getNumericOption (col: string) (r: RowReader) : decimal option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetDecimal(ordinal))
    let getString (col: string) (r: RowReader) =
        r.reader.GetString(r.reader.GetOrdinal(col))
    let getStringOption (col: string) (r: RowReader) : string option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetString(ordinal))
    let getInstant (col: string) (r: RowReader) =
        r.reader.GetFieldValue<Instant>(r.reader.GetOrdinal(col))
    let getInstantOption (col: string) (r: RowReader) : Instant option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetFieldValue<Instant>(ordinal))
    let getDate (col: string) (r: RowReader) =
        r.reader.GetFieldValue<LocalDate>(r.reader.GetOrdinal(col))
    let getDateOption (col: string) (r: RowReader) : LocalDate option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetFieldValue<LocalDate>(ordinal))
    let getUuid (col: string) (r: RowReader) =
        r.reader.GetGuid(r.reader.GetOrdinal(col))
    let getUuidOption (col: string) (r: RowReader) : Guid option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetGuid(ordinal))
    let getBool (col: string) (r: RowReader) =
        r.reader.GetBoolean(r.reader.GetOrdinal(col))
    let getBoolOption (col: string) (r: RowReader) : bool option =
        let ordinal = r.reader.GetOrdinal(col)
        if r.reader.IsDBNull(ordinal) then
            None
        else
            Some(r.reader.GetBoolean(ordinal))

let rec private readRawRows
    (reader: Common.DbDataReader)
    (mapRawFunc: RowReader -> 'T)
    (acc: 'T list) // the list that gets pre-pended with every recursion, the "accumulator"
    : 'T list =
    if reader.Read() then // increment the reader and continue the pattern as long as there are rows to be read
        let rawRow = RowReader.create reader
        let mappedRow = mapRawFunc rawRow
        let appendedAcc = mappedRow :: acc
        readRawRows reader mapRawFunc appendedAcc
    else // no more rows to spool off the reader
        List.rev acc // reverse the list (because it was pre-pended the entire time), return the final state of the list back through the recursion stack

/// buildReadQuery is designed to produce a flexible read query that can
/// satisfy diverse use cases
let buildReadQuery
    (cte: string option)
    (selectColumns: string)
    (from: string)
    (join: string option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    : string =
    let cteString =
        match cte with
        | Some x -> x
        | None -> String.Empty
    let joinString =
        match join with
        | Some x -> x
        | None -> String.Empty
    let predicateString =
        match predicate with
        | Some x -> $"where {x}"
        | None -> String.Empty
    let limitString =
        match limit with
        | Some x -> $"limit {x}"
        | None -> String.Empty
    let groupByString =
        match groupBy with
        | Some x -> $"group by {x}"
        | None -> String.Empty
    let orderByString =
        match orderBy with
        | Some x -> $"order by {x}"
        | None -> String.Empty
    $"""
        {cteString}
        select {selectColumns}
        from {from}
        {joinString}
        {predicateString}
        {groupByString}
        {orderByString}
        {limitString}
        ;
        """

let executeReaderQuery
    (dbTransaction: DbTransaction)
    (query: string)
    (parameters: QueryParameter list)
    (mapRaw: RowReader -> 'Tuple)
    (constructFromRaw: 'Tuple -> Result<'T, AppError>)
    (expectedRows: AcceptableExpectedRows)
    : Result<'T list, AppError> =
    result {
        let! ds = dataSource.Value
        let parameters = buildParamsList parameters
        let! rows =
            (*
             * standard dotnet I/O libraries throw standard dotnet exceptions
             * we use a try/with block to convert their results into more
             * paradigmatic F# Result Ok/Error at the impure boundary
             *)
            try
                match dbTransaction |> isNone with
                | true ->
                    let rawRows =
                        use connection = ds.OpenConnection()
                        use command = new NpgsqlCommand(query, connection)
                        parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                        use nReader = command.ExecuteReader()
                        readRawRows nReader mapRaw []
                    rawRows |> List.map constructFromRaw |> convertListOfResultsToResultsList
                | false ->
                    dbTransaction
                    |> getTranAndConn
                    |> function
                        | Error e -> Error e
                        | Ok(tran, conn) ->
                            let rawRows =
                                use command = new NpgsqlCommand(query, conn)
                                command.Transaction <- tran
                                parameters |> List.iter(fun p -> command.Parameters.Add(p) |> ignore)
                                use nReader = command.ExecuteReader()
                                readRawRows nReader mapRaw []
                            rawRows |> List.map constructFromRaw |> convertListOfResultsToResultsList
            with ex ->
                Error(DalErrorDuringReaderQueryExecution ex)
        let! () = confirmNumRows rows.Length expectedRows
        return rows
    }
