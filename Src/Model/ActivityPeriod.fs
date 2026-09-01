module Model.ActivityPeriod

open NodaTime
open Utilities.AppError

type BeginValidationBehavior =
    // sometimes, you want to be able to create an entity before its begin date. Sometimes, that's an illegal state
    | ConsideredAvailableBeforeBeginDate 
    | NotConsideredAvailableBeforeBeginDate

type ActivityPeriod =
    private { activeBegin: LocalDate
              activeEnd: LocalDate option
              beginValidationBehavior: BeginValidationBehavior }

let activeBegin (a: ActivityPeriod) = a.activeBegin
let activeEnd (a: ActivityPeriod) = a.activeEnd
let create
    (rawBegin: LocalDate)
    (rawEnd: LocalDate option)
    (beginValidationBehavior: BeginValidationBehavior)
    : Result<ActivityPeriod, AppError> =
    match rawEnd with
    | None -> Ok { activeBegin = rawBegin; activeEnd = None; beginValidationBehavior = beginValidationBehavior }
    | Some x ->
        if x < rawBegin then
            Error(ActiveEndBeforeBegin(rawBegin, rawEnd))
        else
            Ok { activeBegin = rawBegin; activeEnd = rawEnd; beginValidationBehavior = beginValidationBehavior }
            
let isActive
    (referencePoint: LocalDate)
    (ap: ActivityPeriod)
    : bool =
    // do not change these rules without also considering whether the same rules should be changed in isAvailable
    let beginDate = activeBegin ap
    let endDate = activeEnd ap
    match endDate with
    | None when beginDate <= referencePoint -> true // no end and begin is in the past
    | Some x when beginDate <= referencePoint && x >= referencePoint -> true // begin is in the past; end is in the future
    | None when beginDate > referencePoint -> false // no end, but hasn't started yet
    | Some x when x < referencePoint -> false // end is in the past
    | Some _ when beginDate > referencePoint -> false // there's an end date, but start is in the future
    | _ -> false
            
let isAvailable
    (referencePoint: LocalDate)
    (ap: ActivityPeriod)
    : bool =
    // do not change these rules without also considering whether the same rules should be changed in isActive
    let beginDate = activeBegin ap
    let endDate = activeEnd ap
    let beginValidationBehavior = ap.beginValidationBehavior
    match endDate with
    | None when beginDate <= referencePoint -> true // no end and begin is in the past
    | Some x when beginDate <= referencePoint && x >= referencePoint -> true // begin is in the past; end is in the future
    | None when beginDate > referencePoint && beginValidationBehavior = NotConsideredAvailableBeforeBeginDate -> false // no end, but hasn't started yet
    | None when beginDate > referencePoint && beginValidationBehavior = ConsideredAvailableBeforeBeginDate -> true // no end, hasn't started yet, but can be considered active before start
    | Some x when x < referencePoint -> false // end is in the past
    | Some _ when beginDate > referencePoint && beginValidationBehavior = NotConsideredAvailableBeforeBeginDate -> false // there's an end date, but start is in the future
    | Some _ when beginDate > referencePoint && beginValidationBehavior = ConsideredAvailableBeforeBeginDate -> true // there's an end date, start is in the future, but can be considered active before start
    | _ -> false

/// insistBeginValidationBehavior is used by model create functions to insist that the flag is set correctly when sent
/// in from their constituent calling functions
let insistBeginValidationBehavior
    (beginValidationBehavior: BeginValidationBehavior)
    (ap: ActivityPeriod) =
    // any failure here means we have a major code failure because the passed in activity period was already a valid
    // type. We're just (maybe) changing a DU that has no bearing on the type validity
    let beginDate = ap |> activeBegin
    let endDate = ap |> activeEnd
    create beginDate endDate beginValidationBehavior
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))  

