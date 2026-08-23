module Model.DataIngestion.StageEntryStatusTransition

open NodaTime
open Utilities.AppError
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Utilities.ResultHelper

type StageEntryStatusTransition =
    private {
        stageEntryStatusTransitionId: StageEntryStatusTransitionId
        stageEntryHeaderId: StageEntryHeaderId
        fromStatus: StagedEntryStatus option
        toStatus: StagedEntryStatus
        instant: Instant
        stageStatusChangeMechanism: StageStatusChangeMechanism
    }

type StageEntryStatusTransitionSortOrder =
    | Asc
    | Desc

let stageEntryStatusTransitionId v = v.stageEntryStatusTransitionId
let stageEntryHeaderId v = v.stageEntryHeaderId
let fromStatus v = v.fromStatus
let toStatus v = v.toStatus
let instant v = v.instant
let stageStatusChangeMechanism v = v.stageStatusChangeMechanism

let validTransitions fromType = fromType |> function
    | None -> [ Ingested ]
    | Some x ->
        match x with
        | Ingested -> [ Duplicate; Classified; NoMatch; Conflict; Ignored ]
        | Classified -> [ Duplicate; Reviewed; Posted; Ignored ]
        | NoMatch -> [ Duplicate; Reviewed; Ignored ]
        | Conflict -> [ Duplicate; Reviewed; Ignored ]
        | Reviewed -> [ Posted; Ignored ]
        | Duplicate -> [ Reviewed; Ignored ]
        | Posted -> []
        | Ignored -> [ Reviewed ]

let create
    (stageEntryStatusTransitionId: StageEntryStatusTransitionId)
    (stageEntryHeaderId: StageEntryHeaderId)
    (fromStatus: StagedEntryStatus option)
    (toStatus: StagedEntryStatus)
    (instant: Instant)
    (stageStatusChangeMechanism: StageStatusChangeMechanism)
    : StageEntryStatusTransition = {
        stageEntryStatusTransitionId = stageEntryStatusTransitionId
        stageEntryHeaderId = stageEntryHeaderId
        fromStatus = fromStatus
        toStatus = toStatus
        instant = instant
        stageStatusChangeMechanism = stageStatusChangeMechanism } 
        
let private reconstitute raw =
    result {
        let (uuid,
             headerUuid,
             fromStatusStr,
             toStatusStr,
             instant,
             stageStatusChangeMechanismStr) =
            raw
        let stageEntryStatusTransitionId = uuid |> StageEntryStatusTransitionId.fromGuid
        let stageEntryHeaderId = headerUuid |> StageEntryHeaderId.fromGuid
        let! fromStatus = fromStatusStr |> convertOptionToDesiredTypeWithFallibleConverter StagedEntryStatus.fromString
        let! toStatus = toStatusStr |> StagedEntryStatus.fromString
        let! stageStatusChangeMechanism = stageStatusChangeMechanismStr |> StageStatusChangeMechanism.fromString
        return
            create
                stageEntryStatusTransitionId
                stageEntryHeaderId
                fromStatus
                toStatus
                instant
                stageStatusChangeMechanism
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "entry_id"),
    (row |> RowReader.getStringOption "from_status"),
    (row |> RowReader.getString "to_status"),
    (row |> RowReader.getInstant "modified_at"),
    (row |> RowReader.getString "change_mechanism")
    
let private readRowsFromDb
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<StageEntryStatusTransition list, AppError> =
    let select =
        """
        sea.unique_id, sea.entry_id, sea.from_status, sea.to_status, sea.modified_at, sea.change_mechanism
        """
    let from = "ingestion.staged_entry_audit sea"
    let query = buildReadQuery None select from None predicate limit None None
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchByHeaderId (context: Context.Context) (headerId: StageEntryHeaderId) : Result<StageEntryStatusTransition list, AppError> =
    let predicate = "sea.entry_id = @unique_id"
    let uuid = headerId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByHeaderIdList
    (context: Context.Context)
    (stageEntryHeaderIds: StageEntryHeaderId list)
    : Result<StageEntryStatusTransition list, AppError> =
    if stageEntryHeaderIds |> List.isEmpty then Error IngestionStageHeaderIdListCannotBeEmpty else
    let ordinals = [ 1 .. stageEntryHeaderIds.Length ]
    let zipped = List.zip ordinals stageEntryHeaderIds
    let namesAndParameters =
        zipped
        |> List.map(fun (ordinal, id) ->
            let uuid = id |> StageEntryHeaderId.value
            let name = $"@stageEntryHeaderId{ordinal}"
            let parameter = { name = name; value = UniqueId uuid }
            name, parameter)
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"sea.entry_id in ({names})"
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

let confirmValidTransition transition =
    let fromType = transition |> fromStatus
    let toType = transition |> toStatus
    if fromType |> validTransitions |> List.contains toType then Ok ()
    else
        let fromStr = fromType |> Option.map StagedEntryStatus.toString
        let toStr = toType |> StagedEntryStatus.toString
        Error (IngestionInvalidStageStatusTransition (fromStr, toStr))

let formAllStatusesCte sortOrder =
    let orderBy =
        match sortOrder with
        | Asc -> "asc" // ordinal 1 is the earliest
        | Desc -> "desc" // ordinal 1 is the latest
    $"""
        all_statuses_{orderBy} as (
            select
                entry_id,
                modified_at,
                from_status,
                to_status,
                row_number() over (partition by entry_id order by modified_at {orderBy}) as ordinal
            from ingestion.staged_entry_audit
        )
    """

let formLatestStatusCte : string list  =
    [
        formAllStatusesCte Desc
        $"""
            latest_statuses as (
                select
                    entry_id,
                    modified_at,
                    from_status,
                    to_status,
                    ordinal
                from all_statuses_desc where ordinal = 1
            )
        """
    ]

let formEarliestStatusCte : string list  =
    [
        formAllStatusesCte Asc
        $"""
            earliest_statuses as (
                select
                    entry_id,
                    modified_at,
                    from_status,
                    to_status,
                    ordinal
                from all_statuses_asc where ordinal = 1
            )
        """
    ]
    
