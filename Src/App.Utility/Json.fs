module App.Utility.Json

open System.Text.Json
open System.Text.Json.Serialization
open NodaTime.Serialization.SystemTextJson
open App.Utility.UtilityError

module Json =
    let private options =
        let o = JsonSerializerOptions()
        o.Converters.Add(JsonFSharpConverter())
        o.Converters.Add(NodaConverters.InstantConverter)
        o.Converters.Add(NodaConverters.LocalDateConverter)
        o

    let fromJson<'T> (json: string) : Result<'T, UtilityError> =
        try
            Ok(JsonSerializer.Deserialize<'T>(json, options))
        with e ->
            Error(JsonDeserializationFailed(typeof<'T>.ToString(), e.Message, e.StackTrace))

    let toJson<'T> (value: 'T) : Result<string, UtilityError> =
        try
            Ok(JsonSerializer.Serialize<'T>(value, options))
        with e ->
            Error(JsonSerializationFailed(typeof<'T>.ToString(), e.Message, e.StackTrace))
