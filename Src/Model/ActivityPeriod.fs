module Model.ActivityPeriod

open NodaTime
open Utilities.AppError

type ActivityPeriod =
    private { activeBegin: LocalDate
              activeEnd: LocalDate option
              canBeActiveBeforeBegin: bool }

let activeBegin (a: ActivityPeriod) = a.activeBegin
let activeEnd (a: ActivityPeriod) = a.activeEnd
let create
    (rawBegin: LocalDate)
    (rawEnd: LocalDate option)
    (canBeActiveBeforeBegin: bool)
    : Result<ActivityPeriod, AppError> =
    match rawEnd with
    | None -> Ok { activeBegin = rawBegin; activeEnd = None; canBeActiveBeforeBegin = canBeActiveBeforeBegin }
    | Some x ->
        if x < rawBegin then
            Error(ActiveEndBeforeBegin(rawBegin, rawEnd))
        else
            Ok { activeBegin = rawBegin; activeEnd = rawEnd; canBeActiveBeforeBegin = canBeActiveBeforeBegin }
let isActive
    (referencePoint: LocalDate)
    (ap: ActivityPeriod)
    : bool =
    let beginDate = activeBegin ap
    let endDate = activeEnd ap
    let canBeActiveBeforeBegin = ap.canBeActiveBeforeBegin
    match endDate with
    | None when beginDate <= referencePoint -> true // no end and begin is in the past
    | Some x when beginDate <= referencePoint && x >= referencePoint -> true // begin is in the past; end is in the future
    | None when beginDate > referencePoint && canBeActiveBeforeBegin = false -> false // no end, but hasn't started yet
    | None when beginDate > referencePoint && canBeActiveBeforeBegin = true -> true // no end, hasn't started yet, but can be considered active before start
    | Some x when x < referencePoint -> false // end is in the past
    | Some _ when beginDate > referencePoint && canBeActiveBeforeBegin = false -> false // there's an end date, but start is in the future
    | Some _ when beginDate > referencePoint && canBeActiveBeforeBegin = true -> true // there's an end date, start is in the future, but can be considered active before start
    | _ -> false

/// insistActiveBeforeBeginFlag is used by model create functions to insist that the flag is set correctly when sent in
/// from their constituent calling functions
let insistActiveBeforeBeginFlag
    (canBeActiveBeforeBegin: bool)
    (ap: ActivityPeriod) =
    // any failure here means we have a major code failure because the passed in activity period was already a valid
    // type. We're just (maybe) changing a bool that has no bearing on the type validity
    let beginDate = ap |> activeBegin
    let endDate = ap |> activeEnd
    create beginDate endDate canBeActiveBeforeBegin
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))  

