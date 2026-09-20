module Business.FinancialServices.CashFlow.MasterAgreement

open NodaTime
open App.Utility.AppError
open App.Utility.FieldUpdate
open App.Utility.Result
open App.DataAccessLayer.ExecuteNonQuery
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.QueryParameter
open App.Session
open Business.General
open Business.FinancialServices.CashFlow.CashFlowComponent

let masterAgreementSelectFields = """
    ma.unique_id, ma.agreement_name, ma.flow_direction, ma.cadence, ma.cadence_week_day,
    ma.cadence_date_in_month, ma.cadence_week_in_month, ma.cadence_month, ma.counterparty,
    ma.start_date, ma.end_date, ma.next_instance, ma.memo, ma.created_at, ma.modified_at
    """

type MasterAgreement = private {
    agreementId: MasterAgreementId
    agreementName: AgreementName
    direction: FlowDirection
    cadence: Cadence.Cadence
    counterparty: Counterparty
    activityPeriod: ActivityPeriod.ActivityPeriod
    memo: AgreementMemo option
    createdAt: Instant
    modifiedAt: Instant
}

type MasterAgreementFieldUpdates = {
    agreementIdToUpdate: MasterAgreementId
    agreementNameUpdate: FieldUpdate<AgreementName>
    directionUpdate: FieldUpdate<FlowDirection>
    cadenceUpdate: FieldUpdate<Cadence.Cadence>
    counterpartyUpdate: FieldUpdate<Counterparty>
    activityPeriodUpdate: FieldUpdate<ActivityPeriod.ActivityPeriod>
    memoUpdate: FieldUpdate<AgreementMemo option>
}
        
let agreementID m = m.agreementId
let agreementName m = m.agreementName
let direction m = m.direction
let cadence m = m.cadence
let counterparty m = m.counterparty
let activityPeriod m = m.activityPeriod
let memo m = m.memo
let createdAt m = m.createdAt
let modifiedAt m = m.modifiedAt

let create 
    (agreementID: MasterAgreementId)
    (agreementName: AgreementName)
    (direction: FlowDirection)
    (cadence: Cadence.Cadence)
    (counterparty: Counterparty)
    (agreementActivityPeriod: ActivityPeriod.ActivityPeriod)
    (memo: AgreementMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : MasterAgreement =
    let rebuiltActivityPeriod =
        agreementActivityPeriod
        |> ActivityPeriod.insistBeginValidationBehavior ActivityPeriod.ConsideredAvailableBeforeBeginDate
    {  agreementId = agreementID
       agreementName = agreementName
       direction = direction
       cadence = cadence
       counterparty = counterparty
       activityPeriod = rebuiltActivityPeriod
       memo = memo
       createdAt = createdAt
       modifiedAt = modifiedAt }
        
    

let persist
    (context: Context.Context)
    (masterAgreement: MasterAgreement)
    : Result<unit, AppError> =
    result {
        let queryStatement =
            """
            insert into cashflow.master_agreement(
	            unique_id, agreement_name, flow_direction, cadence, cadence_week_day, cadence_date_in_month,
                cadence_week_in_month, cadence_month, counterparty, start_date, next_instance, end_date, memo,
                created_at, modified_at)
            values (
	            @unique_id, @agreement_name, @flow_direction, @cadence, @cadence_week_day, @cadence_date_in_month,
                @cadence_week_in_month, @cadence_month, @counterparty, @start_date, @end_date, @memo, @next_instance,
                @created_at, @modified_at);"""
        let uuid = masterAgreement.agreementId |> MasterAgreementId.value
        let agreementName = masterAgreement.agreementName |> AgreementName.value
        let direction = masterAgreement.direction |> FlowDirection.toString
        let cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth, nextInstance =
            masterAgreement.cadence |> Cadence.cadenceToColumns
        let counterparty = masterAgreement.counterparty |> Counterparty.value
        let activeBegin = masterAgreement.activityPeriod |> ActivityPeriod.activeBegin
        let activeEnd = masterAgreement.activityPeriod |> ActivityPeriod.activeEnd
        let memo = masterAgreement.memo |> Option.map AgreementMemo.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@agreement_name"; value = CharString(agreementName) }
              { name = "@flow_direction"; value = CharString(direction) }
              { name = "@cadence"; value = CharString(cadenceName) }
              { name = "@cadence_week_day"; value = NullableCharString(cadenceWeekDay) }
              { name = "@cadence_date_in_month"; value = NullableInteger(cadenceDateInMonth) }
              { name = "@cadence_week_in_month"; value = NullableInteger(cadenceWeekInMonth) }
              { name = "@cadence_month"; value = NullableCharString(cadenceMonth) }
              { name = "@counterparty"; value = CharString(counterparty) }
              { name = "@start_date"; value = DbLocalDate(activeBegin) }
              { name = "@next_instance"; value = DbLocalDate(nextInstance) }
              { name = "@end_date"; value = NullableDbLocalDate(activeEnd) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@created_at"; value = DbInstant(masterAgreement.createdAt) }
              { name = "@modified_at"; value = DbInstant(masterAgreement.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
    }

let private reconstitute raw =
    result {
        let (uuid,
             agreementNameStr,
             flowDirectionStr,
             cadenceName,
             cadenceWeekDay,
             cadenceDateInMonth,
             cadenceWeekInMonth,
             cadenceMonth,
             counterpartyStr,
             startDate,
             endDate,
             nextInstance,
             memoStr,
             createdAt,
             modifiedAt) =
            raw
        let agreementID = uuid |> MasterAgreementId.fromGuid
        let! agreementName = agreementNameStr |> AgreementName.create
        let! direction = flowDirectionStr |> FlowDirection.fromString
        let! cadence = Cadence.reconstitute
                           cadenceName cadenceWeekDay cadenceDateInMonth cadenceWeekInMonth cadenceMonth nextInstance
        let! counterparty = counterpartyStr |> Counterparty.create
        let! agreementActivityPeriod =
            ActivityPeriod.create startDate endDate ActivityPeriod.ConsideredAvailableBeforeBeginDate
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter AgreementMemo.create
        return  
            create
                agreementID
                agreementName
                direction
                cadence
                counterparty
                agreementActivityPeriod
                memo
                createdAt
                modifiedAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "agreement_name"),
    (row |> RowReader.getString "flow_direction"),
    (row |> RowReader.getString "cadence"),
    (row |> RowReader.getStringOption "cadence_week_day"),
    (row |> RowReader.getIntOption "cadence_date_in_month"),
    (row |> RowReader.getIntOption "cadence_week_in_month"),
    (row |> RowReader.getStringOption "cadence_month"),
    (row |> RowReader.getString "counterparty"),
    (row |> RowReader.getDate "start_date"),
    (row |> RowReader.getDateOption "end_date"),
    (row |> RowReader.getDate "next_instance"),
    (row |> RowReader.getStringOption "memo"),
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
    : Result<MasterAgreement list, AppError> =
    let from = "cashflow.master_agreement ma"
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
    : Result<MasterAgreement list, AppError> =
    query context None masterAgreementSelectFields None predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (agreementID: MasterAgreementId) : Result<MasterAgreement, AppError> =
    let predicate = "ma.unique_id = @unique_id"
    let uuid = agreementID |> MasterAgreementId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchAny context (Some predicate) None parameters ExactlyOne |> Result.map List.head

let fetchByMasterAgreementIdList
    (context: Context.Context)
    (agreementIds: MasterAgreementId list)
    : Result<MasterAgreement list, AppError> =
    if agreementIds |> List.isEmpty then Error CashflowMasterAgreementIdListCannotBeEmpty else
    let namesAndParameters =
        List.zip [ 1 .. agreementIds.Length ] agreementIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@masterAgreementId{ordinal}"
            name, { name = name; value = UniqueId(id |> MasterAgreementId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"ma.unique_id in ({names})"
    fetchAny context (Some predicate) None parameters AnyQuantityIsAcceptable

let update
    (context: Context.Context)
    (fieldUpdates: MasterAgreementFieldUpdates)
    : Result<MasterAgreement, AppError> =
    let agreementID = fieldUpdates.agreementIdToUpdate
    let uuid = agreementID |> MasterAgreementId.value
    let baseParams =
        [ { name = "@unique_id"; value = UniqueId uuid } ]
    let updates =
        [
              fieldUpdates.agreementNameUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  [ ("agreement_name = @agreement_name",
                     { name = "@agreement_name"; value = CharString(AgreementName.value n) }) ])

              fieldUpdates.directionUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  [ ("flow_direction = @flow_direction",
                     { name = "@flow_direction"; value = CharString(FlowDirection.toString n) }) ])

              fieldUpdates.cadenceUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  let cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth, nextInstance =
                      n |> Cadence.cadenceToColumns
                  [ ("cadence = @cadence", { name = "@cadence"; value = CharString(cadenceName) })
                    ("cadence_week_day = @cadence_week_day",
                     { name = "@cadence_week_day"; value = NullableCharString(cadenceWeekDay) })
                    ("cadence_date_in_month = @cadence_date_in_month",
                     { name = "@cadence_date_in_month"; value = NullableInteger(cadenceDateInMonth) })
                    ("cadence_week_in_month = @cadence_week_in_month",
                     { name = "@cadence_week_in_month"; value = NullableInteger(cadenceWeekInMonth) })
                    ("cadence_month = @cadence_month",
                     { name = "@cadence_month"; value = NullableCharString(cadenceMonth) })
                    ("next_instance = @next_instance",
                     { name = "@next_instance"; value = DbLocalDate(nextInstance) }) ])

              fieldUpdates.counterpartyUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  [ ("counterparty = @counterparty",
                     { name = "@counterparty"; value = CharString(Counterparty.value n) }) ])

              fieldUpdates.activityPeriodUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  let activeBegin = n |> ActivityPeriod.activeBegin
                  let activeEnd = n |> ActivityPeriod.activeEnd
                  [ ("start_date = @start_date", { name = "@start_date"; value = DbLocalDate(activeBegin) })
                    ("end_date = @end_date", { name = "@end_date"; value = NullableDbLocalDate(activeEnd) }) ])

              fieldUpdates.memoUpdate
              |> mapNoChangeToOptionWithConversion(fun n ->
                  [ ("memo = @memo", { name = "@memo"; value = NullableCharString(n |> Option.map AgreementMemo.value) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let queryStatement =
        $"""
        UPDATE cashflow.master_agreement
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowMasterAgreementUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne
        return! agreementID |> fetchById context
    }

let updateCadence context newCadence masterAgreement =
    let fieldUpdates = {
        agreementIdToUpdate = masterAgreement.agreementId
        agreementNameUpdate = NoChange
        directionUpdate = NoChange
        cadenceUpdate = SetTo newCadence
        counterpartyUpdate = NoChange
        activityPeriodUpdate = NoChange
        memoUpdate = NoChange
    }
    fieldUpdates |> update context

