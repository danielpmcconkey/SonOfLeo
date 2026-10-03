module Business.FinancialServices.CashFlow.Instance

open NodaTime
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Result
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Session
open Business.FinancialServices.CashFlow.CashFlowError
open Business.FinancialServices.CashFlow.CashFlowComponent

type Instance = private {
    instanceId: InstanceId
    masterAgreementID: MasterAgreementId
    masterAgreementName: AgreementName // not separately tracked in the database; here for read convenience
    instanceDate: LocalDate
    isFulfilled: bool
    // present exactly when the Instance is cancelled
    cancellationReasonNote: CancellationReasonNote option
    createdAt: Instant
    modifiedAt: Instant
}

type InstanceFieldUpdates = {
    instanceIdToUpdate: InstanceId
    isFulfilledUpdate: FieldUpdate<bool>
}

let instanceId i = i.instanceId
let masterAgreementID i = i.masterAgreementID
let masterAgreementName i = i.masterAgreementName
let instanceDate i = i.instanceDate
let isFulfilled i = i.isFulfilled
let cancellationReasonNote i = i.cancellationReasonNote
let isCancelled i = i.cancellationReasonNote |> Option.isSome
let createdAt i = i.createdAt
let modifiedAt i = i.modifiedAt

let create
    (instanceId: InstanceId)
    (masterAgreementID: MasterAgreementId)
    (masterAgreementName: AgreementName)
    (instanceDate: LocalDate)
    (isFulfilled: bool)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : Instance =
    { instanceId = instanceId
      masterAgreementID = masterAgreementID
      masterAgreementName = masterAgreementName
      instanceDate = instanceDate
      isFulfilled = isFulfilled
      cancellationReasonNote = None
      createdAt = createdAt
      modifiedAt = modifiedAt }

let applyFieldUpdates (fieldUpdates: InstanceFieldUpdates) (instance: Instance) : Instance =
    { instance with isFulfilled = fieldUpdates.isFulfilledUpdate |> valueOrCurrent instance.isFulfilled }

let persist
    (context: Context.Context)
    (instance: Instance)
    : Result<unit, IAppError> =
    result {
        let queryStatement =
            """
            insert into cashflow.instance(
	            unique_id, master_agreement_id, instance_date, is_fulfilled, cancellation_reason_note, created_at,
                modified_at)
            values (
	            @unique_id, @master_agreement_id, @instance_date, @is_fulfilled, @cancellation_reason_note,
                @created_at, @modified_at);"""
        let uuid = instance.instanceId |> InstanceId.value
        let masterAgreementUuid = instance.masterAgreementID |> MasterAgreementId.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@master_agreement_id"; value = UniqueId(masterAgreementUuid) }
              { name = "@instance_date"; value = DbLocalDate(instance.instanceDate) }
              { name = "@is_fulfilled"; value = Boolean(instance.isFulfilled) }
              { name = "@cancellation_reason_note"
                value = NullableCharString(instance.cancellationReasonNote |> Option.map CancellationReasonNote.value) }
              { name = "@created_at"; value = DbInstant(instance.createdAt) }
              { name = "@modified_at"; value = DbInstant(instance.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }

let private reconstitute raw =
    result {
        let (uuid,
             masterAgreementUuid,
             masterAgreementNameStr,
             instanceDate,
             isFulfilled,
             cancellationReasonNoteStr,
             createdAt,
             modifiedAt) =
            raw
        let instanceId = uuid |> InstanceId.fromGuid
        let masterAgreementID = masterAgreementUuid |> MasterAgreementId.fromGuid
        let! masterAgreementName = masterAgreementNameStr |> AgreementName.create
        let! cancellationReasonNote =
            cancellationReasonNoteStr |> convertOptionToDesiredTypeWithFallibleConverter CancellationReasonNote.create
        let instance =
            create
                instanceId
                masterAgreementID
                masterAgreementName
                instanceDate
                isFulfilled
                createdAt
                modifiedAt
        return { instance with cancellationReasonNote = cancellationReasonNote }
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "master_agreement_id"),
    (row |> RowReader.getString "agreement_name"),
    (row |> RowReader.getDate "instance_date"),
    (row |> RowReader.getBool "is_fulfilled"),
    (row |> RowReader.getStringOption "cancellation_reason_note"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let query
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
    : Result<Instance list, IAppError> =
    let from = "cashflow.instance ins"
    let queryStatement = buildReadQuery cteList select from joinList predicate limit groupBy orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let private fetchAny
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (expectedRows: AcceptableExpectedRows)
    : Result<Instance list, IAppError> =
    let select = """
        ins.unique_id, ins.master_agreement_id, ma.agreement_name, ins.instance_date, ins.is_fulfilled,
        ins.cancellation_reason_note, ins.created_at, ins.modified_at
        """
    let joinList = [ "join cashflow.master_agreement ma on ins.master_agreement_id = ma.unique_id" ]
    query context None select (Some joinList) predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (instanceId: InstanceId) : Result<Instance, IAppError> =
    let predicate = "ins.unique_id = @unique_id"
    let uuid = instanceId |> InstanceId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchAny context (Some predicate) None parameters ExactlyOne
    |> whenNoRows (CashflowInstanceIdDoesntExist uuid)
    |> Result.map List.head

let fetchByMasterAgreementIdList
    (context: Context.Context)
    (masterAgreementIds: MasterAgreementId list)
    : Result<Instance list, IAppError> =
    if masterAgreementIds |> List.isEmpty then Error CashflowMasterAgreementIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. masterAgreementIds.Length ] masterAgreementIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@masterAgreementId{ordinal}"
            name, { name = name; value = UniqueId(id |> MasterAgreementId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"ins.master_agreement_id in ({names})"
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

/// fetchOpen returns every open Instance: neither fulfilled nor cancelled.
let fetchOpen (context: Context.Context) : Result<Instance list, IAppError> =
    let predicate = "ins.is_fulfilled = false and ins.cancellation_reason_note is null"
    fetchAny context (Some predicate) None [] AnyQuantityIsAcceptable

/// cancel records the Instance as cancelled with its reason note. The caller decides whether it may be cancelled.
let cancel
    (context: Context.Context)
    (note: CancellationReasonNote)
    (instanceId: InstanceId)
    : Result<unit, IAppError> =
    let uuid = instanceId |> InstanceId.value
    let queryStatement =
        """
        UPDATE cashflow.instance
        set cancellation_reason_note = @cancellation_reason_note, modified_at = @modified
        WHERE unique_id = @unique_id;
        """
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@cancellation_reason_note"; value = CharString(note |> CancellationReasonNote.value) }
          { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    |> whenNoRows (CashflowInstanceIdDoesntExist uuid)

let update
    (context: Context.Context)
    (fieldUpdates: InstanceFieldUpdates)
    : Result<Instance, IAppError> =
    let instanceId = fieldUpdates.instanceIdToUpdate
    let uuid = instanceId |> InstanceId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              fieldUpdates.isFulfilledUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  [ ("is_fulfilled = @is_fulfilled", { name = "@is_fulfilled"; value = Boolean(n) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let modified = { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
    let parameters = baseParams @ (updates |> List.map snd) @ [ modified ]
    let queryStatement =
        $"""
        UPDATE cashflow.instance
        set
            {setClauses},
            modified_at = @modified
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowInstanceUpdateNoOp) else Ok()
        do!
            executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
            |> whenNoRows (CashflowInstanceIdDoesntExist uuid)
        return! instanceId |> fetchById context
    }
