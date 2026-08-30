module Model.ActivityPeriod

open NodaTime
open Utilities.AppError

type ActivityPeriod =
    private
        { activeBegin: LocalDate
          activeEnd: LocalDate option }

let activeBegin (a: ActivityPeriod) = a.activeBegin
let activeEnd (a: ActivityPeriod) = a.activeEnd
let create (rawBegin: LocalDate) (rawEnd: LocalDate option) : Result<ActivityPeriod, AppError> =
    match rawEnd with
    | None -> Ok { activeBegin = rawBegin; activeEnd = None }
    | Some x ->
        if x < rawBegin then
            Error(AccountActiveEndBeforeBegin(rawBegin, rawEnd))
        else
            Ok { activeBegin = rawBegin; activeEnd = rawEnd }
let isActive
    (referencePoint: LocalDate)
    (aap: ActivityPeriod)
    : bool =
    let beginDate = activeBegin aap
    let endDate = activeEnd aap
    match endDate with
    | None when beginDate <= referencePoint -> true // no end and begin is in the past
    | Some x when beginDate <= referencePoint && x >= referencePoint -> true // begin is in the past; end is in the future
    | None when beginDate > referencePoint -> false // no end, but hasn't started yet
    | Some x when x < referencePoint -> false // end is in the past
    | Some _ when beginDate > referencePoint -> false // there's an end date, but start is in the future
    | _ -> false
