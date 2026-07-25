module Utilities.Calendar

open NodaTime

let localTimeZone = DateTimeZoneProviders.Tzdb["America/New_York"]

let dateFromInstant (i: Instant) : LocalDate = i.InZone(localTimeZone).Date

let today () : LocalDate = Clock.now() |> dateFromInstant
