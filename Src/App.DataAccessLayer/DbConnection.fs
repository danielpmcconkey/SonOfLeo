module App.DataAccessLayer.DbConnection

open System
open App.Utility
open Npgsql
open App.Utility.Result
open App.DataAccessLayer.DalError
open App.Utility.Config

let private getConnectionStringConfig () : Result<string, DalError> =
    result {
        let! configVal =
            getConfigValue<string> "ConnectionStringEnvVar"
            |> Result.mapError(fun e -> (DalConnectionStringConfigRetrievalError (UtilityError.toMessage e)))
        do! if String.IsNullOrWhiteSpace(configVal) then Error DalConnectionStringEnvVarNotFound else Ok()
        return configVal
    }

let private confirmConfigDoesntContainConnectionString (configVal: string) : Result<unit, DalError> =
    let doesContain = configVal.Contains(";") || configVal.Contains("Host=")
    match doesContain with
    | true -> Error DalConnectionStringEnvVarContainsConnectionString
    | false -> Ok()

let private getRawConnectionString (envVarName: string) : Result<string, DalError> =
    match Environment.GetEnvironmentVariable envVarName |> Option.ofObj with
    | Some x -> Ok x
    | None -> Error(DalEnvVarNotSet envVarName)

let private getValidConnectionString (raw: string) : Result<string, DalError> =
    let trimmed = raw.Trim()
    if String.IsNullOrWhiteSpace(trimmed) then
        Error(DalConnectionStringIsEmpty)
    else
        Ok trimmed

let private getConnectionString () : Result<string, DalError> =
    result {
        let! config = getConnectionStringConfig()
        let! _ = confirmConfigDoesntContainConnectionString config
        let! rawConnectionString = getRawConnectionString config
        return! getValidConnectionString rawConnectionString
    }

let internal dataSource: Lazy<Result<NpgsqlDataSource, DalError>> =
    lazy
        (getConnectionString()
         |> Result.map(fun cs ->
             let b = NpgsqlDataSourceBuilder(cs)
             b.UseNodaTime() |> ignore
             b.Build()))
