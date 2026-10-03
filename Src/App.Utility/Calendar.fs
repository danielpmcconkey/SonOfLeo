module App.Utility.Calendar

open System.Globalization
open NodaTime
open App.Utility

let dateFromInstant (i: Instant) : LocalDate = i.InZone(Clock.timeZoneLocal).Date

let today () : LocalDate = Clock.now() |> dateFromInstant

let localDateToString
    (format: string)
    (localDate: LocalDate)
    : string =
    localDate.ToString(format, CultureInfo.InvariantCulture)
