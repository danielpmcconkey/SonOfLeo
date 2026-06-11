namespace Utilities

open System
open System.Data
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
        | DateTimeWithOffset of DateTimeOffset
        | UniqueId of Guid
        | Boolean of bool
        | NullableInteger of int option
        | NullableNumeric of decimal option
        | NullableCharString of string option
        | NullableDateTimeWithOffset of DateTimeOffset option
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
    
    let private getEnvironment(): Result<string, string> =
        let envVarOption = 
            Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
            |> Option.ofObj
        match envVarOption with
        | Some x ->
            let trimX = x.Trim() // REQ-DAL-1.12
            if trimX = String.Empty then
                Error("Environment var LEOBLOOM_ENV cannot be empty") else // REQ-DAL-1.13
#if DEBUG
(*
 * IMPORTANT! REQ-DAL-3.3
 * note this is a fail guard. Dan does all his development work in the
 * host machine which, from a database perspective, is production and
 * the LEOBLOOM_ENV where Dan does his dev work is "Production". This
 * guard prevents Dan from triggering runs that he thinks are just test
 * and having those runs contaminate his database. 
 *) 
                if trimX = "Production" then
                    Error "Debug builds cannot connect to Production"
                else
#endif
                Ok trimX // REQ-DAL-1.12
        | None -> Error("Environment var LEOBLOOM_ENV cannot be null") // REQ-DAL-1.1
            
    let private getTemplate (env:string) : Result<string, string> =   
        try
            let config =
                ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile($"appsettings.{env}.json", optional = false) // REQ-DAL-1.2
                    .AddEnvironmentVariables()
                    .Build()
            let template = config["ConnectionStrings:SonOfLeo"] // REQ-DAL-1.4
            if String.IsNullOrWhiteSpace(template) then 
                Error $"ConnectionStrings:SonOfLeo not found in appsettings.{env}.json" else // REQ-DAL-1.6, REQ-DAL-1.5
                Ok(template)
        with
        | ex -> Error $"Error retrieving appsettings.{env}.json. Error message: {ex.Message}" // REQ-DAL-1.3
            
    let private injectPassword (template: string): Result<string, string> =
        let envVarOption = 
            Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD"
            |> Option.ofObj
        match envVarOption with
        | Some x ->
            let trimX = x.Trim() // REQ-DAL-1.10 
            if trimX = String.Empty then
                Error("Environment var LEOBLOOM_DB_PASSWORD cannot be empty") else // REQ-DAL-1.11
                Ok (template.Replace("{LEOBLOOM_DB_PASSWORD}", trimX)) // REQ-DAL-1.9,  REQ-DAL-1.10 
        | None -> Error("Environment var LEOBLOOM_DB_PASSWORD cannot be null") // REQ-DAL-1.7
            
    let private getConnectionString(): Result<string,string> =        
        result {
            let! env = getEnvironment()
            let! template = getTemplate env
            return! injectPassword template
        }
    
    let private convertParamToDbParam (parameter: QueryParameter) : NpgsqlParameter = // REQ-DAL-3.2
        let dbType, value =
            match parameter.value with
            | Integer x -> NpgsqlDbType.Integer, box x
            | Numeric x -> NpgsqlDbType.Numeric, box x
            | CharString x -> NpgsqlDbType.Varchar, box x
            | DateTimeWithOffset x -> NpgsqlDbType.TimestampTz, box x
            | UniqueId x -> NpgsqlDbType.Uuid, box x
            | Boolean x -> NpgsqlDbType.Boolean, box x
            | NullableInteger x -> NpgsqlDbType.Integer, match x with Some b -> box b | None -> box DBNull.Value
            | NullableNumeric x -> NpgsqlDbType.Numeric, match x with Some b -> box b | None -> box DBNull.Value
            | NullableCharString x -> NpgsqlDbType.Varchar, match x with Some b -> box b | None -> box DBNull.Value
            | NullableDateTimeWithOffset x -> NpgsqlDbType.TimestampTz, match x with Some b -> box b | None -> box DBNull.Value
            | NullableUniqueId x -> NpgsqlDbType.Uuid, match x with Some b -> box b | None -> box DBNull.Value
            | NullableBoolean x -> NpgsqlDbType.Boolean, match x with Some b -> box b | None -> box DBNull.Value
        let p = NpgsqlParameter(parameter.name, dbType)
        p.Value <- value // necessary because NpgsqlParameter doesn't take a value in its constructor
        p
        
    let private buildParamsList (parameters: QueryParameter list) : NpgsqlParameter list = // REQ-DAL-3.2
        List.map (fun x -> convertParamToDbParam(x)) parameters
        
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
        (expectedRows: AcceptableExpectedRows) : Result<unit, string> =
        result {
            let! connectionString = getConnectionString()
            let parameters = buildParamsList parameters
            let! numRows =
                (*
                 * standard dotnet I/O libraries throw standard dotnet exceptions
                 * we use a try/with block to convert their results into more
                 * paradigmatic F# Result Ok/Error at the impure boundary
                 *)
                try
                    use connection = new NpgsqlConnection(connectionString)
                    connection.Open()
                    use command = new NpgsqlCommand(query, connection)                    
                    parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                    Ok (command.ExecuteNonQuery())
                with
                | ex -> Error $"Database error during non query execution {ex.Message}"
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
        let getDateTimeOffset (col: string) (r: RowReader) =
            r.reader.GetFieldValue<DateTimeOffset>(r.reader.GetOrdinal(col))
        let getDateTimeOffsetOption (col: string) (r: RowReader) : DateTimeOffset option = 
            let ordinal = r.reader.GetOrdinal(col)
            if r.reader.IsDBNull(ordinal) then None
            else Some (r.reader.GetFieldValue<DateTimeOffset>(ordinal))
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
    
    let rec private readRows // REQ-DAL-3.2
            (reader: Common.DbDataReader)
            (mapRow: RowReader -> Result<'T,string>)
            (acc: 'T list) // the list that gets pre-pended with every recursion, the "accumulator"
            : Result<'T list, string> =
        if reader.Read() then // increment the reader and continue the pattern as long as there are rows to be read
            let row = RowReader.create reader
            match mapRow row with
            | Ok mapped -> readRows reader mapRow (mapped :: acc) // map the row to an appropriately mapped object and call the next recursion layer (but only if that map didn't fail)
            | Error e -> Error e // fail and bail cause one row not mapping is a catastrophe
        else // no more rows to spool off the reader
            Ok (List.rev acc) // reverse the list (because it was pre-pended the entire time), return the final state of the list back through the recursion stack

    let executeReaderQuery
            (query: string)
            (parameters: QueryParameter list)
            (mapRow: RowReader -> Result<'T, string>) // REQ-DAL-3.2
            (expectedRows: AcceptableExpectedRows): Result<'T list, string> =
        result {
            let! connectionString = getConnectionString()
            let parameters = buildParamsList parameters
            let! rows =
                (*
                 * standard dotnet I/O libraries throw standard dotnet exceptions
                 * we use a try/with block to convert their results into more
                 * paradigmatic F# Result Ok/Error at the impure boundary
                 *)
                try
                    use connection = new NpgsqlConnection(connectionString)
                    connection.Open()
                    use command = new NpgsqlCommand(query, connection)                    
                    parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                    use nReader = command.ExecuteReader()
                    readRows nReader mapRow []
                with
                | ex -> Error $"Database error during non query execution {ex.Message}"
            let! () = validateNumRows rows.Length expectedRows // REQ-DAL-2.2
            return rows
        }


    