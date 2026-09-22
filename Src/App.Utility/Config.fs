module App.Utility.Config

open System
open Microsoft.Extensions.Configuration
open App.Utility.IAppError
open App.Utility.UtilityError
open App.Utility.Result

let private configRoot = // this is intentionally static
    ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional = false)
        .AddEnvironmentVariables()
        .Build()

let mutable private cache: Map<string, obj> = Map.empty

let private readConfigValue<'T> keyString : Result<'T, IAppError> =
    try
        let section = configRoot.GetSection(keyString)
        if section.Exists() then configRoot.GetValue<'T>(keyString) |> Ok
        else Error(ConfigNotFound keyString)
    with ex ->
        Error(ConfigReadError (keyString, ex))

let getConfigValue<'T> keyString : Result<'T, IAppError> =
    try
        match cache |> Map.tryFind keyString with 
        | Some v -> Ok(v :?> 'T)
        | None ->
            result {
                let! readValue = readConfigValue keyString
                cache <- cache |> Map.add keyString (readValue :> obj)
                return readValue
            }
    with ex -> 
        Error(ConfigReadError (keyString, ex))
