module Utilities.Calendar

open System.Globalization
open NodaTime
open Utilities.AppError
open Utilities.ConfigManager

let timeZoneLocal =
    match getConfigValue<string> "LocalizedTimeZone" with
    Ok x -> DateTimeZoneProviders.Tzdb[x]
    | Error e -> failwith (e |> AppError.toMessage)

let dateFromInstant (i: Instant) : LocalDate = i.InZone(timeZoneLocal).Date

let today () : LocalDate = Clock.now() |> dateFromInstant

let localDateToString
    (format: string)
    (localDate: LocalDate)
    : string =
    localDate.ToString(format, CultureInfo.InvariantCulture)
