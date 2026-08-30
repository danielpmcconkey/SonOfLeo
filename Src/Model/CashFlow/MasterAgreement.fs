module Model.CashFlow.MasterAgreement

open Model
open Model.CashFlow.CashFlowComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

let masterAgreementSelectFields = """
    ma.unique_id, ma.agreement_name, ma.flow_direction, ma.cadence, ma.cadence_week_day,
    ma.cadence_date_in_month, ma.cadence_week_in_month, ma.cadence_month, ma.counterparty,
    ma.start_date, ma.end_date, ma.memo, ma.created_at, ma.modified_at
    """

type MasterAgreement = private {
    agreementId: MasterAgreementId
    agreementName: AgreementName
    direction: FlowDirection
    cadence: Cadence
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
    cadenceUpdate: FieldUpdate<Cadence>
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
    (cadence: Cadence)
    (counterparty: Counterparty)
    (agreementActivityPeriod: ActivityPeriod.ActivityPeriod)
    (memo: AgreementMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : MasterAgreement =
    let rebuiltActivityPeriod = agreementActivityPeriod |> ActivityPeriod.insistActiveBeforeBeginFlag true
    {  agreementId = agreementID
       agreementName = agreementName
       direction = direction
       cadence = cadence
       counterparty = counterparty
       activityPeriod = rebuiltActivityPeriod
       memo = memo
       createdAt = createdAt
       modifiedAt = modifiedAt }

let private monthDayToColumns md =
    match md with
    | DateInMonth d -> Some(d |> DateInMonthNumber.value), None, None
    | NthWeekDay(w, wd) -> None, Some(w |> WeekInMonthNumber.value), Some(wd |> WeekDay.toString)
    | Last -> None, None, None

/// cadenceToColumns returns (cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth),
/// matching the shape of the cadence_* columns on cashflow.master_agreement.
let private cadenceToColumns cadence =
    match cadence with
    | Daily -> "Daily", None, None, None, None
    | Weekly wd -> "Weekly", None, None, Some(wd |> WeekDay.toString), None
    | EveryOtherWeek wd -> "EveryOtherWeek", None, None, Some(wd |> WeekDay.toString), None
    | Monthly md ->
        let dateInMonth, weekInMonth, weekDay = md |> monthDayToColumns
        "Monthly", dateInMonth, weekInMonth, weekDay, None
    | Annually(month, md) ->
        let dateInMonth, weekInMonth, weekDay = md |> monthDayToColumns
        "Annually", dateInMonth, weekInMonth, weekDay, Some(month |> Month.toString)

let private monthDayFromColumns
    (cadenceDateInMonth: int option)
    (cadenceWeekInMonth: int option)
    (cadenceWeekDay: string option)
    : Result<MonthDay, AppError> =
    match cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay with
    | Some d, None, None -> d |> DateInMonthNumber.fromInt |> Result.map DateInMonth
    | None, Some w, Some wd ->
        result {
            let! weekInMonth = w |> WeekInMonthNumber.fromInt
            let! weekDay = wd |> WeekDay.fromString
            return NthWeekDay(weekInMonth, weekDay)
        }
    | None, None, None -> Ok Last
    | _ ->
        Error(CashflowInvalidCadenceRow
            $"inconsistent MonthDay columns: cadence_date_in_month={cadenceDateInMonth}, cadence_week_in_month={cadenceWeekInMonth}, cadence_week_day={cadenceWeekDay}")

let private cadenceFromColumns
    (cadenceName: string)
    (cadenceWeekDay: string option)
    (cadenceDateInMonth: int option)
    (cadenceWeekInMonth: int option)
    (cadenceMonth: string option)
    : Result<Cadence, AppError> =
    match cadenceName with
    | "Daily" -> Ok Daily
    | "Weekly" ->
        match cadenceWeekDay with
        | Some wd -> wd |> WeekDay.fromString |> Result.map Weekly
        | None -> Error(CashflowInvalidCadenceRow "Weekly cadence requires cadence_week_day")
    | "EveryOtherWeek" ->
        match cadenceWeekDay with
        | Some wd -> wd |> WeekDay.fromString |> Result.map EveryOtherWeek
        | None -> Error(CashflowInvalidCadenceRow "EveryOtherWeek cadence requires cadence_week_day")
    | "Monthly" ->
        monthDayFromColumns cadenceDateInMonth cadenceWeekInMonth cadenceWeekDay
        |> Result.map Monthly
    | "Annually" ->
        result {
            let! cadenceMonth =
                match cadenceMonth with
                | Some m -> Ok m
                | None -> Error(CashflowInvalidCadenceRow "Annually cadence requires cadence_month")
            let! month = cadenceMonth |> Month.fromString
            let! monthDay = monthDayFromColumns cadenceDateInMonth cadenceWeekInMonth cadenceWeekDay
            return Annually(month, monthDay)
        }
    | other -> Error(CashflowInvalidCadenceRow $"unrecognized cadence \"{other}\"")

let insertNewToDb
    (context: Context.Context)
    (masterAgreement: MasterAgreement)
    : Result<unit, AppError> =
    result {
        let query =
            """
            insert into cashflow.master_agreement(
	            unique_id, agreement_name, flow_direction, cadence, cadence_week_day, cadence_date_in_month,
                cadence_week_in_month, cadence_month, counterparty, start_date, end_date, memo, created_at,
                modified_at)
            values (
	            @unique_id, @agreement_name, @flow_direction, @cadence, @cadence_week_day, @cadence_date_in_month,
                @cadence_week_in_month, @cadence_month, @counterparty, @start_date, @end_date, @memo, @created_at,
                @modified_at);"""
        let uuid = masterAgreement.agreementId |> MasterAgreementId.value
        let agreementName = masterAgreement.agreementName |> AgreementName.value
        let direction = masterAgreement.direction |> FlowDirection.toString
        let cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth =
            masterAgreement.cadence |> cadenceToColumns
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
              { name = "@end_date"; value = NullableDbLocalDate(activeEnd) }
              { name = "@memo"; value = NullableCharString(memo) }
              { name = "@created_at"; value = DbInstant(masterAgreement.createdAt) }
              { name = "@modified_at"; value = DbInstant(masterAgreement.modifiedAt) }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
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
             memoStr,
             createdAt,
             modifiedAt) =
            raw
        let agreementID = uuid |> MasterAgreementId.fromGuid
        let! agreementName = agreementNameStr |> AgreementName.create
        let! direction = flowDirectionStr |> FlowDirection.fromString
        let! cadence = cadenceFromColumns cadenceName cadenceWeekDay cadenceDateInMonth cadenceWeekInMonth cadenceMonth
        let! counterparty = counterpartyStr |> Counterparty.create
        let! agreementActivityPeriod = ActivityPeriod.create startDate endDate true
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
    (row |> RowReader.getStringOption "memo"),
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
    : Result<MasterAgreement list, AppError> =
    let from = "cashflow.master_agreement ma"
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
    : Result<MasterAgreement list, AppError> =
    readRowsFromDb context None masterAgreementSelectFields None predicate limit None None parameters expectedRows

let fetchById (context: Context.Context) (agreementID: MasterAgreementId) : Result<MasterAgreement, AppError> =
    let predicate = "ma.unique_id = @unique_id"
    let uuid = agreementID |> MasterAgreementId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    fetchGenericRead context (Some predicate) None parameters ExactlyOne |> Result.map List.head

/// updateDb is incredibly powerful and should only be used very deliberately. It will let you update your database in a
/// type-unsafe manner. Only use it with controlled database transactions and with certainty that you are validating
/// your resultant data state appropriately.
let updateDb
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
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("agreement_name = @agreement_name",
                     { name = "@agreement_name"; value = CharString(AgreementName.value n) }) ])

              fieldUpdates.directionUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("flow_direction = @flow_direction",
                     { name = "@flow_direction"; value = CharString(FlowDirection.toString n) }) ])

              fieldUpdates.cadenceUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth =
                      n |> cadenceToColumns
                  [ ("cadence = @cadence", { name = "@cadence"; value = CharString(cadenceName) })
                    ("cadence_week_day = @cadence_week_day",
                     { name = "@cadence_week_day"; value = NullableCharString(cadenceWeekDay) })
                    ("cadence_date_in_month = @cadence_date_in_month",
                     { name = "@cadence_date_in_month"; value = NullableInteger(cadenceDateInMonth) })
                    ("cadence_week_in_month = @cadence_week_in_month",
                     { name = "@cadence_week_in_month"; value = NullableInteger(cadenceWeekInMonth) })
                    ("cadence_month = @cadence_month",
                     { name = "@cadence_month"; value = NullableCharString(cadenceMonth) }) ])

              fieldUpdates.counterpartyUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("counterparty = @counterparty",
                     { name = "@counterparty"; value = CharString(Counterparty.value n) }) ])

              fieldUpdates.activityPeriodUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  let activeBegin = n |> ActivityPeriod.activeBegin
                  let activeEnd = n |> ActivityPeriod.activeEnd
                  [ ("start_date = @start_date", { name = "@start_date"; value = DbLocalDate(activeBegin) })
                    ("end_date = @end_date", { name = "@end_date"; value = NullableDbLocalDate(activeEnd) }) ])

              fieldUpdates.memoUpdate
              |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                  [ ("memo = @memo", { name = "@memo"; value = NullableCharString(n |> Option.map AgreementMemo.value) }) ])
        ]
        |> List.choose id
        |> List.collect id
    let setClauses = updates |> List.map fst |> String.concat ", "
    let parameters = baseParams @ (updates |> List.map snd)
    let query =
        $"""
        UPDATE cashflow.master_agreement
        set
            {setClauses}
        WHERE unique_id = @unique_id;
    """
    result {
        do! if updates |> List.isEmpty then Error(CashflowMasterAgreementUpdateNoOp) else Ok()
        do! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! agreementID |> fetchById context
    }

