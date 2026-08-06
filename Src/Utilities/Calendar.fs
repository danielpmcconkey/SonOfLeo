module Utilities.Calendar

open System.Globalization
open NodaTime

let localTimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]

let dateFromInstant (i: Instant) : LocalDate = i.InZone(localTimeZone).Date

let today () : LocalDate = Clock.now() |> dateFromInstant

let localDateToString
    (format: string)
    (localDate: LocalDate)
    : string =
    localDate.ToString(format, CultureInfo.InvariantCulture)
