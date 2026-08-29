module Model.CashFlow.Instance

open Model.CashFlow.CashFlowComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

type Instance = private {
    instanceId: InstanceId
    masterAgreementID: MasterAgreementId
    instanceDate: LocalDate
    isFulfilled: bool
    createdAt: Instant
    modifiedAt: Instant
}

type InstanceFieldUpdates = {
    instanceIdToUpdate: InstanceId
    instanceDateUpdate: FieldUpdate<LocalDate>
    isFulfilledUpdate: FieldUpdate<bool>
}

let instanceId i = i.instanceId
let masterAgreementID i = i.masterAgreementID
let instanceDate i = i.instanceDate
let isFulfilled i = i.isFulfilled
let createdAt i = i.createdAt
let modifiedAt i = i.modifiedAt

let create
    (instanceId: InstanceId)
    (masterAgreementID: MasterAgreementId)
    (instanceDate: LocalDate)
    (isFulfilled: bool)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Instance =
    { instanceId = instanceId
      masterAgreementID = masterAgreementID
      instanceDate = instanceDate
      isFulfilled = isFulfilled
      createdAt = createdAt
      modifiedAt = modifiedAt }

let insertNewToDb
    (context: Context.Context)
    (instance: Instance)
    : Result<unit, AppError> =
    result {
        let query =
            """
            insert into cashflow.instance(
	            unique_id, master_agreement_id, instance_date, is_fulfilled, created_at, modified_at)
            values (
	            @unique_id, @master_agreement_id, @instance_date, @is_fulfilled, @created_at, @modified_at);"""
        let uuid = instance.instanceId |> InstanceId.value
        let masterAgreementUuid = instance.masterAgreementID |> MasterAgreementId.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@master_agreement_id"; value = UniqueId(masterAgreementUuid) }
              { name = "@instance_date"; value = DbLocalDate(instance.instanceDate) }
              { name = "@is_fulfilled"; value = Boolean(instance.isFulfilled) }
              { name = "@created_at"; value = DbInstant(instance.createdAt) }
              { name = "@modified_at"; value = DbInstant(instance.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }

let private reconstitute raw =
    result {
        let (uuid,
             masterAgreementUuid,
             instanceDate,
             isFulfilled,
             createdAt,
             modifiedAt) =
            raw
        let instanceId = uuid |> InstanceId.fromGuid
        let masterAgreementID = masterAgreementUuid |> MasterAgreementId.fromGuid
        return
            create
                instanceId
                masterAgreementID
                instanceDate
                isFulfilled
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "master_agreement_id"),
    (row |> RowReader.getDate "instance_date"),
    (row |> RowReader.getBool "is_fulfilled"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let readRowsFromDb
    (context: Context.Context)
    (cteList: string list option)
    (select: string)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (groupBy: string option)
    (orderBy: string option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Instance list, AppError> =
    let from = "cashflow.instance ins"
    let query = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchGenericRead
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Instance list, AppError> =
    let select = """
        ins.unique_id, ins.master_agreement_id, ins.instance_date, ins.is_fulfilled, ins.created_at, ins.modified_at
        """
    readRowsFromDb context None select None predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (instanceId: InstanceId) : Result<Instance, AppError> =
    let predicate = "ins.unique_id = @unique_id"
    let uuid = instanceId |> InstanceId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchGenericRead context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByMasterAgreementIdList
    (context: Context.Context)
    (masterAgreementIds: MasterAgreementId list)
    : Result<Instance list, AppError> =
    if masterAgreementIds |> List.isEmpty then Error CashflowMasterAgreementIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. masterAgreementIds.Length ] masterAgreementIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@masterAgreementId{ordinal}"
            name, { name = name; value = UniqueId(id |> MasterAgreementId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"ins.master_agreement_id in ({names})"
    fetchGenericRead context (Some predicate) None parameters AnyQuantityIsAcceptable

/// updateDb is incredibly powerful and should only be used very deliberately. It will let you update your database in a
/// type-unsafe manner. Only use it with controlled database transactions and with certainty that you are validating
/// your resultant data state appropriately.
let updateDb
    (context: Context.Context)
    (fieldUpdates: InstanceFieldUpdates)
    : Result<Instance, AppError> =
    let instanceId = fieldUpdates.instanceIdToUpdate
    let uuid = instanceId |> InstanceId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              fieldUpdates.instanceDateUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("instance_date = @instance_date", { name = "@instance_date"; value = DbLocalDate(n) }) ])

              fieldUpdates.isFulfilledUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("is_fulfilled = @is_fulfilled", { name = "@is_fulfilled"; value = Boolean(n) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let query =
        $"""
        UPDATE cashflow.instance
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowInstanceUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! instanceId |> fetchById context
    }
