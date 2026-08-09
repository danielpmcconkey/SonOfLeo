module Model.DataIngestion.StageEntryStatusTransition

open NodaTime
open Utilities.AppError
open Utilities.ResultHelper

type StageEntryStatusTransition =
    private {
        fromStatus: StagedEntryStatus
        toStatus: StagedEntryStatus
        instant: Instant
        stageStatusChangeMechanism: StageStatusChangeMechanism
    }

let fromStatus v = v.fromStatus
let toStatus v = v.toStatus
let instant v = v.instant
let stageStatusChangeMechanism v = v.stageStatusChangeMechanism

let validTransitions fromType = fromType |> function
    | NoStatus -> [ Read ]
    | Read -> [ Ingested ]
    | Ingested -> [ Classified; NoMatch; Conflict ]
    | Classified -> [ Reviewed; Posted ]
    | NoMatch -> [ Reviewed ]
    | Conflict -> [ Reviewed ]
    | Reviewed -> [ Posted ]
    | Duplicate -> [ Reviewed ]
    | Posted -> []
    | Ignored -> [ Reviewed ]

let confirmValidTransition fromType toType =
    if fromType |> validTransitions |> List.contains toType then Ok ()
    else
        let fromStr = fromType |> StagedEntryStatus.toString
        let toStr = toType |> StagedEntryStatus.toString
        Error (IngestionInvalidStageStatusTransition (fromStr, toStr))

let create
    (fromStatus: StagedEntryStatus)
    (toStatus: StagedEntryStatus)
    (instant: Instant)
    (stageStatusChangeMechanism: StageStatusChangeMechanism)
    : Result<StageEntryStatusTransition, AppError> =
    result {
        do! confirmValidTransition fromStatus toStatus
        return {
            fromStatus = fromStatus
            toStatus = toStatus
            instant = instant
            stageStatusChangeMechanism = stageStatusChangeMechanism } }
