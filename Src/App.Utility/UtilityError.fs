module App.Utility.UtilityError

open System
open App.Utility.IAppError

type UtilityError =
    | ConfigReadError of string * exn
    | ConfigNotFound of string
    | FileIoDirectoryDoesntExist of string
    | FileIoFileDoesntExist of string
    | FileIoError of exn
    | JsonDeserializationFailed of string * string * string
    | JsonSerializationFailed of string * string * string
    
    interface IAppError with
        member this.DomainName = nameof UtilityError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with        
            | ConfigReadError (keyString, ex) -> $"Cannot resolve config with key {keyString}. It likely cannot be parsed as the requested type. Full error: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
            | ConfigNotFound keyString -> $"Cannot find config with key {keyString}."
            | FileIoDirectoryDoesntExist str -> $"Directory {str} doesn't exist."
            | FileIoFileDoesntExist str -> $"No file exists at path \"{str}\"."
            | FileIoError ex -> $"Error in File I/O operation. Error message: {ex.Message}{Environment.NewLine} {ex.StackTrace}"
            | JsonDeserializationFailed(typeName, error, stackTrace) -> $"Failed to deserialize JSON string into type {typeName}. {error}{Environment.NewLine}{stackTrace}"
            | JsonSerializationFailed(typeName, error, stackTrace) -> $"Failed to serialize JSON string into type {typeName}. {error}{Environment.NewLine}{stackTrace}"

let toMessage (e: UtilityError) = (e :> IAppError).ToMessage()
let toAppError (e: UtilityError) : IAppError = e :> IAppError
let error (e: UtilityError) : Result<'T, IAppError> = Error (e :> IAppError)
