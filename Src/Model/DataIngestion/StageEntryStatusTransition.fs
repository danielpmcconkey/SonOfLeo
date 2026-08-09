module Model.DataIngestion.StageEntryStatusTransition

open NodaTime
open Utilities.AppError
open Context.Context
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

let stageEntryStatusTransitionId v = v.stageEntryStatusTransitionId
let stageEntryHeaderId v = v.stageEntryHeaderId
let fromStatus v = v.fromStatus
let toStatus v = v.toStatus
let instant v = v.instant
let stageStatusChangeMechanism v = v.stageStatusChangeMechanism

let validTransitions fromType = fromType |> function
    | None -> [ Read ]
    | Some x ->
        match x with
        | Read -> [ Ingested ]
        | Ingested -> [ Classified; NoMatch; Conflict ]
        | Classified -> [ Reviewed; Posted ]
        | NoMatch -> [ Reviewed ]
        | Conflict -> [ Reviewed ]
        | Reviewed -> [ Posted ]
        | Duplicate -> [ Reviewed ]
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

let insertNewToDb (context: Context) (stageEntryStatusTransition: StageEntryStatusTransition) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.staged_entry_audit(
	        unique_id, entry_id, from_status, to_status, modified_at, change_mechanism)
        values (
	        @unique_id, 
            @entry_id, 
            @from_status, 
            @to_status, 
            @modified_at,
            @change_mechanism);"""
    let uuid = stageEntryStatusTransition.stageEntryStatusTransitionId |> StageEntryStatusTransitionId.value
    let headerUuid = stageEntryStatusTransition.stageEntryHeaderId |> StageEntryHeaderId.value
    let fromStatus = stageEntryStatusTransition.fromStatus |> Option.map StagedEntryStatus.toString
    let toStatus = stageEntryStatusTransition.toStatus |> StagedEntryStatus.toString
    let stageStatusChangeMechanism =
        stageEntryStatusTransition.stageStatusChangeMechanism |> StageStatusChangeMechanism.toString
    let parameters =
        [
          { name = "@unique_id"; value = UniqueId(uuid) }
          { name = "@entry_id"; value = UniqueId(headerUuid) }
          { name = "@from_status"; value = NullableCharString(fromStatus) }
          { name = "@to_status"; value = CharString(toStatus) }
          { name = "@modified_at"; value = DbInstant(stageEntryStatusTransition.instant) }
          { name = "@change_mechanism"; value = CharString(stageStatusChangeMechanism) }
        ]
    executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
        
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
    (context: Context)
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
    let query = buildReadQuery select from None predicate limit None None
    executeReaderQuery
        (context |> getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById
    (context: Context)
    (transitionId: StageEntryStatusTransitionId)
    : Result<StageEntryStatusTransition, AppError> =
    let predicate = "sea.unique_id = @unique_id"
    let uuid = transitionId |> StageEntryStatusTransitionId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByHeaderId (context: Context) (lineId: StageEntryHeaderId) : Result<StageEntryStatusTransition list, AppError> =
    let predicate = "sea.entry_id = @unique_id"
    let uuid = lineId |> StageEntryHeaderId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    readRowsFromDb context (Some predicate) None parameters AnyQuantityIsAcceptable

let fetchByHeaderIdList
    (context: Context)
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
