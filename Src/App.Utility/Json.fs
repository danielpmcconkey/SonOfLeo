module App.Utility.Json

open System.Text.Json
open System.Text.Json.Serialization
open Microsoft.FSharp.Reflection
open NodaTime.Serialization.SystemTextJson
open App.Utility.IAppError
open App.Utility.UtilityError

module Json =
    let private options =
        let o = JsonSerializerOptions()
        o.Converters.Add(JsonFSharpConverter())
        o.Converters.Add(NodaConverters.InstantConverter)
        o.Converters.Add(NodaConverters.LocalDateConverter)
        o

    /// FSharp.SystemTextJson names the first field it did not set, and it counts an option field sent as null as not
    /// set. So a payload that sends null for an option field and leaves off a required field declared after it gets a
    /// message naming the option field. When the record the library names is the one being read, this names the
    /// field the payload actually left off instead; any other message is returned as the library gave it.
    let private missingFieldMessage<'T> (json: string) (libraryMessage: string) : string =
        let recordType = typeof<'T>
        let prefix = $"Missing field for record type {recordType.FullName}: "
        if not (FSharpType.IsRecord recordType) || not (libraryMessage.StartsWith prefix) then
            libraryMessage
        else
            try
                use document = JsonDocument.Parse json
                if document.RootElement.ValueKind <> JsonValueKind.Object then
                    libraryMessage
                else
                    let sent = document.RootElement.EnumerateObject() |> Seq.map _.Name |> Set.ofSeq
                    match FSharpType.GetRecordFields recordType |> Array.tryFind (fun f -> not (sent.Contains f.Name)) with
                    | Some field -> prefix + field.Name
                    | None -> libraryMessage
            with _ ->
                libraryMessage

    let fromJson<'T> (json: string) : Result<'T, IAppError> =
        try
            Ok(JsonSerializer.Deserialize<'T>(json, options))
        with e ->
            Error(JsonDeserializationFailed(typeof<'T>.ToString(), missingFieldMessage<'T> json e.Message, e.StackTrace))

    let toJson<'T> (value: 'T) : Result<string, IAppError> =
        try
            Ok(JsonSerializer.Serialize<'T>(value, options))
        with e ->
            Error(JsonSerializationFailed(typeof<'T>.ToString(), e.Message, e.StackTrace))
