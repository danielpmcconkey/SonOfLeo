module InterfaceBridge.Json

open System.Text.Json
open System.Text.Json.Serialization
open NodaTime.Serialization.SystemTextJson
open Utilities.AppError

module Json =
    let private options =
        let o = JsonSerializerOptions()
        o.Converters.Add(JsonFSharpConverter())
        o.Converters.Add(NodaConverters.InstantConverter)
        o.Converters.Add(NodaConverters.LocalDateConverter)
        o

    let fromJson<'T> (json: string) : Result<'T, AppError> = // REQ-NGUI-2.4, REQ-NGUI-3.5
        try
            Ok(JsonSerializer.Deserialize<'T>(json, options))
        with e ->
            Error(InterfaceBridgeFailedJsonDeserialization(typeof<'T>.ToString(), e.Message, e.StackTrace))

    let toJson<'T> (value: 'T) : Result<string, AppError> = // REQ-NGUI-2.4, REQ-NGUI-3.5
        try
            Ok(JsonSerializer.Serialize<'T>(value, options))
        with e ->
            Error(InterfaceBridgeFailedJsonSerialization(typeof<'T>.ToString(), e.Message, e.StackTrace))
