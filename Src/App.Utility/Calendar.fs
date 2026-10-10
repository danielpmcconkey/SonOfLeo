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

/// Every month-end on or after the begin date and on or before the end date, in date order.
let monthEndsBetween (beginDate: LocalDate) (endDate: LocalDate) : LocalDate list =
    let monthEndOf (d: LocalDate) = d.With(DateAdjusters.EndOfMonth)
    Seq.unfold
        (fun (monthEnd: LocalDate) ->
            if monthEnd > endDate then None
            else Some(monthEnd, monthEndOf (monthEnd.PlusDays 1)))
        (monthEndOf beginDate)
    |> Seq.filter (fun d -> d >= beginDate)
    |> Seq.toList
