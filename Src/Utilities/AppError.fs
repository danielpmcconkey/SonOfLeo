module Utilities.AppError

// todo: this is a placeholder for how I'd implement a better error system in future
type AppError =
    | AccountNotFound of code: string
    | LookupFailed of entity: string * key: string
    | ValidationFailed of field: string * reason: string

module AppError =
    let toMessage = function
    | AccountNotFound code -> $"Account code ({code}) didn't match any recorded accounts"
    | LookupFailed (entity, key) -> $"{entity} lookup failed for key: {key}"
    | ValidationFailed (field, reason) -> $"Validation failed for {field}: {reason}"
