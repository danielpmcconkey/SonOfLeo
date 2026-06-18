namespace Model.UI
open System.Text.Json
open System.Text.Json.Serialization
open NodaTime.Serialization.SystemTextJson

module Json =
    let private options =
        let o = JsonSerializerOptions()
        o.Converters.Add(JsonFSharpConverter())
        o.Converters.Add(NodaConverters.InstantConverter)
        o
    
    let fromJson<'T> (json: string) : Result<'T, string> = // REQ-NGUI-2.4, REQ-NGUI-3.5
        try
            Ok (JsonSerializer.Deserialize<'T>(json, options))
        with e -> Error $"Failed to deserialize JSON string into type {typeof<'T>}. {e}"
    
    let toJson<'T> (value: 'T) : Result<string, string> = // REQ-NGUI-2.4, REQ-NGUI-3.5
        try
            Ok (JsonSerializer.Serialize<'T>(value, options))
        with e -> Error $"Failed to serialize {typeof<'T>} value into JSON string. {e}"