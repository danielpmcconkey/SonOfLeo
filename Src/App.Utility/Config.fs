module App.Utility.Config

open System
open Microsoft.Extensions.Configuration
open App.Utility.IAppError
open App.Utility.UtilityError
open App.Utility.Result

let configFilePath = IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json")

// read once, and a file that is missing or will not parse is a typed error rather than a type initialiser exception
let private configRoot : Lazy<Result<IConfigurationRoot, IAppError>> =
    lazy
        (if not (IO.File.Exists configFilePath) then
            Error(ConfigFileNotFound configFilePath)
         else
            try
                ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional = false)
                    .AddEnvironmentVariables()
                    .Build()
                |> Ok
            with ex ->
                Error(ConfigFileUnreadable(configFilePath, ex.GetBaseException().Message)))

/// confirmReadable is Ok when the configuration file exists and parses.
let confirmReadable () : Result<unit, IAppError> =
    configRoot.Value |> Result.map ignore

let mutable private cache: Map<string, obj> = Map.empty

let private readConfigValue<'T> keyString : Result<'T, IAppError> =
    result {
        let! root = configRoot.Value
        return!
            try
                let section = root.GetSection(keyString)
                if section.Exists() then root.GetValue<'T>(keyString) |> Ok
                else Error(ConfigNotFound keyString)
            with ex ->
                Error(ConfigReadError (keyString, ex))
    }

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
