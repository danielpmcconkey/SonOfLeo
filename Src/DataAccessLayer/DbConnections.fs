module DataAccessLayer.DbConnections

open System
open Npgsql
open Utilities.ResultHelper
open Utilities.AppError
open Utilities.ConfigManager

let private getConnectionStringConfig () : Result<string, AppError> =
    result {
        let! configVal = getConfigValue<string> "ConnectionStringEnvVar"
        do! if String.IsNullOrWhiteSpace(configVal) then Error DalConnectionStringEnvVarNotFound else Ok()
        return configVal
    }

let private confirmConfigDoesntContainConnectionString (configVal: string) : Result<unit, AppError> =
    let doesContain = configVal.Contains(";") || configVal.Contains("Host=")
    match doesContain with
    | true -> Error DalConnectionStringEnvVarContainsConnectionString
    | false -> Ok()

let private getRawConnectionString (envVarName: string) : Result<string, AppError> =
    match Environment.GetEnvironmentVariable envVarName |> Option.ofObj with
    | Some x -> Ok x
    | None -> Error(DalEnvVarNotSet envVarName)

let private getValidConnectionString (raw: string) : Result<string, AppError> =
    let trimmed = raw.Trim()
    if String.IsNullOrWhiteSpace(trimmed) then
        Error(DalConnectionStringIsEmpty)
    else
        Ok trimmed

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
