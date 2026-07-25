module DataAccessLayer.DbConnections

open System
open Npgsql // REQ-DAL-3.1
open Microsoft.Extensions.Configuration
open Utilities.ResultHelper
open Utilities.AppError

let private getConnectionStringConfig () : Result<string, AppError> =
    try
        let config =
            ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional = false)
                .AddEnvironmentVariables()
                .Build()
        let configVal = config["ConnectionStringEnvVar"]
        if String.IsNullOrWhiteSpace(configVal) then
            Error DalConnectionStringEnvVarNotFound
        else // REQ-DAL-1.14, REQ-DAL-1.15
            Ok(configVal)
    with ex ->
        Error(DalErrorRetrievingAppSettings ex)

let private confirmConfigDoesntContainConnectionString (configVal: string) : Result<unit, AppError> =
    let doesContain = configVal.Contains(";") || configVal.Contains("Host=")
    match doesContain with
    | true -> Error DalConnectionStringEnvVarContainsConnectionString // REQ-DAL-1.16
    | false -> Ok()

let private getRawConnectionString (envVarName: string) : Result<string, AppError> =
    match Environment.GetEnvironmentVariable envVarName |> Option.ofObj with
    | Some x -> Ok x
    | None -> Error(DalEnvVarNotSet envVarName) // REQ-DAL-1.17

let private getValidConnectionString (raw: string) : Result<string, AppError> =
    let trimmed = raw.Trim()
    if String.IsNullOrWhiteSpace(trimmed) then
        Error(DalConnectionStringIsEmpty) // REQ-DAL-1.18
    else
        Ok trimmed // REQ-DAL-1.19

let private getConnectionString () : Result<string, AppError> =
    result {
        let! config = getConnectionStringConfig()
        let! _ = confirmConfigDoesntContainConnectionString config
        let! rawConnectionString = getRawConnectionString config
        return! getValidConnectionString rawConnectionString
    }

let internal dataSource: Lazy<Result<NpgsqlDataSource, AppError>> =
    lazy
        (getConnectionString()
         |> Result.map(fun cs ->
             let b = NpgsqlDataSourceBuilder(cs)
             b.UseNodaTime() |> ignore
             b.Build()))
