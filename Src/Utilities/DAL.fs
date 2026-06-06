namespace Utilities

open System
open Npgsql
open Microsoft.Extensions.Configuration
open NpgsqlTypes
open Utilities.ResultCE

module DAL =
    
    type QueryParameterValue = 
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
    
    type QueryParameter = {
        name: string
        value: QueryParameterValue }
            
    
    let private getEnvironment(): Result<string, string> =
        let envVarOption = 
            Environment.GetEnvironmentVariable "LEOBLOOM_ENV"
            |> Option.ofObj
        match envVarOption with
        | Some x ->
            let trimX = x.Trim() // @FT-DAL-1.12
            if trimX = String.Empty then
                Error("Environment var LEOBLOOM_ENV cannot be empty") else // @FT-DAL-1.13
                Ok trimX // @FT-DAL-1.12
        | None -> Error("Environment var LEOBLOOM_ENV cannot be null") // @FT-DAL-1.1
            
    let private getTemplate (env:string) : Result<string, string> =   
        try
            let config =
                ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile($"appsettings.{env}.json", optional = false) // @FT-DAL-1.2
                    .AddEnvironmentVariables()
                    .Build()
            let template = config["ConnectionStrings:SonOfLeo"] // @FT-DAL-1.4
            if String.IsNullOrWhiteSpace(template) then 
                Error $"ConnectionStrings:SonOfLeo not found in appsettings.{env}.json" else // @FT-DAL-1.6, @FT-DAL-1.5
                Ok(template)
        with
        | ex -> Error $"Error retrieving appsettings.{env}.json. Error message: {ex.Message}" // @FT-DAL-1.3
            
    let private injectPassword (template: string): Result<string, string> =
        let envVarOption = 
            Environment.GetEnvironmentVariable "LEOBLOOM_DB_PASSWORD"
            |> Option.ofObj
        match envVarOption with
        | Some x ->
            let trimX = x.Trim() // @FT-DAL-1.10 
            if trimX = String.Empty then
                Error("Environment var LEOBLOOM_DB_PASSWORD cannot be empty") else // @FT-DAL-1.11
                Ok (template.Replace("{LEOBLOOM_DB_PASSWORD}", trimX)) // @FT-DAL-1.9,  @FT-DAL-1.10 
        | None -> Error("Environment var LEOBLOOM_DB_PASSWORD cannot be null") // @FT-DAL-1.7
            
    let private getConnectionString(): Result<string,string> =        
        result {
            let! env = getEnvironment()
            let! template = getTemplate env
            return! injectPassword template
        }
    
    let private convertParamToDbParam (parameter: QueryParameter) : NpgsqlParameter =
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
        
    let private buildParamsList (parameters: QueryParameter list) : NpgsqlParameter list =
        List.map (fun x -> convertParamToDbParam(x)) parameters
        
    let executeNonQuery (query: string) (parameters: QueryParameter list) : Result<int, string> =
        result {
            let! connectionString = getConnectionString()
            let parameters = buildParamsList parameters
            return!
                try
                    use connection = new NpgsqlConnection(connectionString)
                    connection.Open()
                    use command = new NpgsqlCommand(query, connection)
                    parameters |> List.iter (fun p -> command.Parameters.Add(p) |> ignore)
                    Ok (command.ExecuteNonQuery())
                with
                | ex -> Error $"Database error during non query execution {ex.Message}"
        }
    
        

