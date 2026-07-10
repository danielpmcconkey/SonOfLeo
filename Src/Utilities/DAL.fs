namespace Utilities

open System
open System.Data
open NodaTime
open Npgsql // REQ-DAL-3.1
open Microsoft.Extensions.Configuration
open NpgsqlTypes // REQ-DAL-3.1
open Utilities.ResultCE

module DAL =
    
    /// FieldUpdate is a simple DU to use for functions that can update one
    /// or many columns. This allows us to easily distinguish between "don't
    /// update" and "update it to null"
    type FieldUpdate<'a> =
        | NoChange
        | SetTo of 'a

    type QueryParameterValue = // REQ-DAL-3.2
        | Integer of int
        | Numeric of decimal
        | CharString of string
        | DbInstant of Instant
        | DbLocalDate of LocalDate
        | UniqueId of Guid
        | Boolean of bool
        | NullableInteger of int option
        | NullableNumeric of decimal option
        | NullableCharString of string option
        | NullableDbInstant of Instant option
        | NullableDbLocalDate of LocalDate option
        | NullableUniqueId of Guid option
        | NullableBoolean of bool option
    
    type QueryParameter = { // REQ-DAL-3.2
        name: string
        value: QueryParameterValue }
    
    type AcceptableExpectedRows = // REQ-DAL-2.2
        | Zero
        | ExactlyOne
        | OneOrMany
        | AnyQuantityIsAcceptable
        
    type DbTransaction = private { connection: NpgsqlConnection; transaction: NpgsqlTransaction }
    
    let private getConnectionStringConfig() : Result<string, string> =   
        try
            let config =
                ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile($"appsettings.json", optional = false)
                    .AddEnvironmentVariables()
                    .Build()
            let configVal = config["ConnectionStringEnvVar"] 
            if String.IsNullOrWhiteSpace(configVal) then 
                Error $"ConnectionStringEnvVar not found in appsettings.json" else // REQ-DAL-1.14, REQ-DAL-1.15
                Ok(configVal)
        with
        | ex -> Error $"Error retrieving appsettings.json. Error message: {ex.Message}{Environment.NewLine} {ex.StackTrace}" // REQ-DAL-1.3, REQ-NGUI-1.3.1

    let private confirmConfigDoesntContainConnectionString (configVal: string) : Result<unit, string> =
        let doesContain = configVal.Contains(";") || configVal.Contains("Host=")
        match doesContain with
        | true -> Error "ConnectionStringEnvVar contains a connection string, not an env var name." // REQ-DAL-1.16
        | false -> Ok ()
    
    let private getRawConnectionString (envVarName:string) : Result<string, string> =
        match Environment.GetEnvironmentVariable envVarName |> Option.ofObj with
        | Some x -> Ok x
        | None -> Error $"Environment variable {envVarName} not set or empty" // REQ-DAL-1.17
        
    let private getValidConnectionString (raw: string) : Result<string, string> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace(trimmed)
        then Error "Connection string is empty" // REQ-DAL-1.18
        else Ok trimmed // REQ-DAL-1.19
        
    let private getConnectionString(): Result<string, string> =
        result {
            let! config = getConnectionStringConfig ()
            let! _ = confirmConfigDoesntContainConnectionString config
            let! rawConnectionString = getRawConnectionString config
            return! getValidConnectionString rawConnectionString
        }
        
    let private dataSource : Lazy<Result<NpgsqlDataSource, string>> =
        lazy (
            getConnectionString()
            |> Result.map (fun cs ->
                let b = NpgsqlDataSourceBuilder(cs)
                b.UseNodaTime() |> ignore
                b.Build())
        )

    let createDbTransaction () : Result<DbTransaction, string> =
        result {
            let! ds = dataSource.Value
            return!
                try
                    let connection = ds.OpenConnection()
                    let transaction = connection.BeginTransaction()
                    Ok { connection = connection; transaction = transaction }
                with
                    | ex -> Error $"Database error during transaction creation: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        }

    let commitDbTransactionAndDisposeConnection (transaction: DbTransaction) : Result<unit, string> =
        try
            try
                transaction.transaction.Commit()
                Ok ()
            with
                | ex -> Error $"Database error during transaction commit. {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        finally
            transaction.transaction.Dispose()
            transaction.connection.Dispose()

    let rollbackDbTransactionAndDisposeConnection (transaction: DbTransaction) : Result<unit, string> =
        try
            try
                transaction.transaction.Rollback()
                Ok ()
            with
                | ex -> Error $"Database error during transaction rollback. You probably have corrupted data that you should address immediately. {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        finally
            transaction.transaction.Dispose()
            transaction.connection.Dispose()

    let private convertParamToDbParam (parameter: QueryParameter) : NpgsqlParameter = // REQ-DAL-3.2
        let dbType, value =
            match parameter.value with
            | Integer x -> NpgsqlDbType.Integer, box x
            | Numeric x -> NpgsqlDbType.Numeric, box x
            | CharString x -> NpgsqlDbType.Varchar, box x
            | DbInstant x -> NpgsqlDbType.TimestampTz, box x
            | DbLocalDate x -> NpgsqlDbType.Date, box x
            | UniqueId x -> NpgsqlDbType.Uuid, box x
            | Boolean x -> NpgsqlDbType.Boolean, box x
            | NullableInteger x -> NpgsqlDbType.Integer, match x with Some b -> box b | None -> box DBNull.Value
            | NullableNumeric x -> NpgsqlDbType.Numeric, match x with Some b -> box b | None -> box DBNull.Value
            | NullableCharString x -> NpgsqlDbType.Varchar, match x with Some b -> box b | None -> box DBNull.Value
            | NullableDbInstant x -> NpgsqlDbType.TimestampTz, match x with Some b -> box b | None -> box DBNull.Value
            | NullableDbLocalDate x -> NpgsqlDbType.Date, match x with Some b -> box b | None -> box DBNull.Value
            | NullableUniqueId x -> NpgsqlDbType.Uuid, match x with Some b -> box b | None -> box DBNull.Value
            | NullableBoolean x -> NpgsqlDbType.Boolean, match x with Some b -> box b | None -> box DBNull.Value
        let p = NpgsqlParameter(parameter.name, dbType)
        p.Value <- value // necessary because NpgsqlParameter doesn't take a value in its constructor
        p

    let private buildParamsList (parameters: QueryParameter list) : NpgsqlParameter list = // REQ-DAL-3.2
        parameters |> List.map convertParamToDbParam
        
    let private validateNumRows (numRows: int) (expectation: AcceptableExpectedRows): Result<unit, string> = // REQ-DAL-2.2
        match expectation with
        | Zero when numRows = 0 -> Ok()
        | ExactlyOne when numRows = 1 -> Ok()
        | OneOrMany when numRows >= 1 -> Ok()
        | AnyQuantityIsAcceptable -> Ok()
        | _ -> Error "Resultant rows didn't match expectation"
        
    let executeNonQuery
        (query: string)
        (parameters: QueryParameter list)
        (expectedRows: AcceptableExpectedRows)
        (transaction: DbTransaction option)
        : Result<unit, string> =
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
                    match transaction with
                    | None ->
                        use connection = ds.OpenConnection()
                        use command = new NpgsqlCommand(query, connection)
                        parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                        Ok (command.ExecuteNonQuery())
                    | Some t ->
                        use command = new NpgsqlCommand(query, t.connection)
                        command.Transaction <- t.transaction
                        parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                        Ok (command.ExecuteNonQuery())
                with
                | ex -> Error $"Database error during non query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
            return! validateNumRows numRows expectedRows  // REQ-DAL-2.2
        }
    
    type RowReader =
        private {
            reader: Common.DbDataReader
        }
    
    module RowReader = // REQ-DAL-3.2
        let create (reader: Common.DbDataReader) :RowReader =
            { reader = reader }
        let getInt (col: string) (r: RowReader) = r.reader.GetInt32(r.reader.GetOrdinal(col))
        let getIntOption (col: string) (r: RowReader) : int option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetInt32(ordinal))
        let getNumeric (col: string) (r: RowReader) = r.reader.GetDecimal(r.reader.GetOrdinal(col))
        let getNumericOption (col: string) (r: RowReader) : decimal option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetDecimal(ordinal))
        let getString (col: string) (r: RowReader) = r.reader.GetString(r.reader.GetOrdinal(col))
        let getStringOption (col: string) (r: RowReader) : string option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetString(ordinal))
        let getInstant (col: string) (r: RowReader) =
            r.reader.GetFieldValue<Instant>(r.reader.GetOrdinal(col))
        let getInstantOption (col: string) (r: RowReader) : Instant option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetFieldValue<Instant>(ordinal))
        let getDate (col: string) (r: RowReader) =
            r.reader.GetFieldValue<LocalDate>(r.reader.GetOrdinal(col))
        let getDateOption (col: string) (r: RowReader) : LocalDate option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetFieldValue<LocalDate>(ordinal))
        let getUuid (col: string) (r: RowReader) = r.reader.GetGuid(r.reader.GetOrdinal(col))
        let getUuidOption (col: string) (r: RowReader) : Guid option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetGuid(ordinal))
        let getBool (col: string) (r: RowReader) = r.reader.GetBoolean(r.reader.GetOrdinal(col))
        let getBoolOption (col: string) (r: RowReader) : bool option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetBoolean(ordinal))
    
    let rec private readRawRows // REQ-DAL-3.2
            (reader: Common.DbDataReader)
            (mapRawFunc: RowReader -> 'T)
            (acc: 'T list) // the list that gets pre-pended with every recursion, the "accumulator"
            : 'T list =
        if reader.Read() then // increment the reader and continue the pattern as long as there are rows to be read
            let rawRow = RowReader.create reader
            let mappedRow = mapRawFunc rawRow
            let appendedAcc = mappedRow::acc
            readRawRows reader mapRawFunc appendedAcc 
        else // no more rows to spool off the reader
            List.rev acc // reverse the list (because it was pre-pended the entire time), return the final state of the list back through the recursion stack

    /// buildReadQuery is designed to produce a flexible read query that can
    /// satisfy diverse use cases 
    let buildReadQuery
            (selectColumns: string)
            (from: string)
            (join: string option)
            (predicate: string option)
            (limit: int option)
            (groupBy: string option)
            (orderBy: string option)
            : string =
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
            select {selectColumns}
            from {from}
            {joinString}
            {predicateString}
            {limitString}
            {groupByString}
            {orderByString}
            ;
            """

    let executeReaderQuery
            (query: string)
            (parameters: QueryParameter list)
            (mapRaw: RowReader -> 'Tuple) // REQ-DAL-3.2
            (constructFromRaw: DbTransaction option -> 'Tuple -> Result<'T, string>)
            (expectedRows: AcceptableExpectedRows)
            (transaction: DbTransaction option)
            : Result<'T list, string> =
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
                    match transaction with
                    | None -> 
                        let rawRows = 
                            use connection = ds.OpenConnection()
                            use command = new NpgsqlCommand(query, connection)                    
                            parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                            use nReader = command.ExecuteReader()
                            readRawRows nReader mapRaw []
                        rawRows |> List.map (constructFromRaw transaction) |> ListHelper.listOfResultsToResultsList
                    | Some t ->
                        let rawRows =
                            use command = new NpgsqlCommand(query, t.connection)
                            command.Transaction <- t.transaction
                            parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                            use nReader = command.ExecuteReader()
                            readRawRows nReader mapRaw []
                        rawRows |> List.map (constructFromRaw transaction) |> ListHelper.listOfResultsToResultsList
                with
                | ex -> Error $"Database error during reader query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
            let! () = validateNumRows rows.Length expectedRows // REQ-DAL-2.2
            return rows
        }

    let stringUnboxing (objRaw: obj) : Result<string, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "String unboxing returned DB null"
            else 
                Ok (objRaw :?> string)
        with
        | ex -> Error $"Database error during string unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let stringOptionUnboxing (objRaw: obj) : Result<string option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                Ok (Some (objRaw :?> string))
        with
        | ex -> Error $"Database error during string option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let intUnboxing (objRaw: obj) : Result<int, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "Int unboxing returned DB null"
            else 
                let unboxed : int = objRaw |> unbox
                Ok unboxed
        with
        | ex -> Error $"Database error during int unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let intOptionUnboxing (objRaw: obj) : Result<int option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                
                let unboxed : int = objRaw |> unbox
                Ok (Some unboxed)
        with
        | ex -> Error $"Database error during int option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let longUnboxing (objRaw: obj) : Result<int64, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "Long unboxing returned DB null"
            else 
                let unboxed : int64 = objRaw |> unbox
                Ok unboxed
        with
        | ex -> Error $"Database error during long unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let longOptionUnboxing (objRaw: obj) : Result<int64 option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                let unboxed : int64 = objRaw |> unbox
                Ok (Some unboxed)
        with
        | ex -> Error $"Database error during long option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let decimalUnboxing (objRaw: obj) : Result<decimal, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "Decimal unboxing returned DB null"
            else 
                let unboxed : decimal = objRaw |> unbox
                Ok unboxed
        with
        | ex -> Error $"Database error during decimal unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let decimalOptionUnboxing (objRaw: obj) : Result<decimal option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                let unboxed : decimal = objRaw |> unbox
                Ok (Some unboxed)
        with
        | ex -> Error $"Database error during decimal option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let localDateUnboxing (objRaw: obj) : Result<LocalDate, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "LocalDate unboxing returned DB null"
            else 
                let unboxed : LocalDate = objRaw |> unbox
                Ok unboxed
        with
        | ex -> Error $"Database error during LocalDate unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let localDateOptionUnboxing (objRaw: obj) : Result<LocalDate option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                let unboxed : LocalDate = objRaw |> unbox
                Ok (Some unboxed)
        with
        | ex -> Error $"Database error during LocalDate option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let instantUnboxing (objRaw: obj) : Result<Instant, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "Instant unboxing returned DB null"
            else 
                let unboxed : Instant = objRaw |> unbox
                Ok unboxed
        with
        | ex -> Error $"Database error during Instant unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let instantOptionUnboxing (objRaw: obj) : Result<Instant option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                let unboxed : Instant = objRaw |> unbox
                Ok (Some unboxed)
        with
        | ex -> Error $"Database error during Instant option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let uuidUnboxing (objRaw: obj) : Result<Guid, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Error "UUID unboxing returned DB null"
            else 
                let unboxed : Guid = objRaw |> unbox
                Ok unboxed
        with
        | ex -> Error $"Database error during UUID unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        
    let uuidOptionUnboxing (objRaw: obj) : Result<Guid option, string> =
        try
            if objRaw = null || objRaw = DBNull.Value  then Ok None
            else 
                let unboxed : Guid = objRaw |> unbox
                Ok (Some unboxed)
        with
        | ex -> Error $"Database error during UUID option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
        

    let executeScalar
            (query: string)
            (parameters: QueryParameter list)
            (unboxingFunc: obj -> Result<'T, string>)
            (transaction: DbTransaction option)
            : Result<'T, string> =
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
                    let objResult =
                        match transaction with
                        | None -> 
                            use connection = ds.OpenConnection()
                            use command = new NpgsqlCommand(query, connection)                    
                            parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                            command.ExecuteScalar()
                        | Some t -> 
                            use command = new NpgsqlCommand(query, t.connection)
                            command.Transaction <- t.transaction
                            parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                            command.ExecuteScalar()
                    objResult |> unboxingFunc
                with
                | ex -> Error $"Database error during reader scalar execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}" // REQ-NGUI-1.3.1
            return rows
        }
    