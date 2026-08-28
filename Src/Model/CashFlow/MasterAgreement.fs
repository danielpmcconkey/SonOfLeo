module Model.CashFlow.MasterAgreement

open Model.CashFlow.CashFlowComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters

type MasterAgreement = private {
    agreementID: MasterAgreementId
    agreementName: AgreementName
    direction: FlowDirection
    cadence: Cadence
    counterparty: Counterparty
    startDate: LocalDate
    endDate: LocalDate option
    memo: AgreementMemo option
    createdAt: Instant
    modifiedAt: Instant
}

type MasterAgreementFieldUpdates = {
    agreementIDToUpdate: MasterAgreementId
    agreementNameUpdate: FieldUpdate<AgreementName>
    directionUpdate: FieldUpdate<FlowDirection>
    cadenceUpdate: FieldUpdate<Cadence>
    counterpartyUpdate: FieldUpdate<Counterparty>
    startDateUpdate: FieldUpdate<LocalDate>
    endDateUpdate: FieldUpdate<LocalDate option>
    memoUpdate: FieldUpdate<AgreementMemo option>
}
        
let agreementID m = m.agreementID
let agreementName m = m.agreementName
let direction m = m.direction
let cadence m = m.cadence
let counterparty m = m.counterparty
let startDate m = m.startDate
let endDate m = m.endDate
let memo m = m.memo
let createdAt m = m.createdAt
let modifiedAt m = m.modifiedAt

let create 
    (agreementID: MasterAgreementId)
    (agreementName: AgreementName)
    (direction: FlowDirection)
    (cadence: Cadence)
    (counterparty: Counterparty)
    (startDate: LocalDate)
    (endDate: LocalDate option)
    (memo: AgreementMemo option)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : MasterAgreement =
    {  agreementID = agreementID
       agreementName = agreementName
       direction = direction
       cadence = cadence
       counterparty = counterparty
       startDate = startDate
       endDate = endDate
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
        let uuid = masterAgreement.agreementID |> MasterAgreementId.value
        let agreementName = masterAgreement.agreementName |> AgreementName.value
        let direction = masterAgreement.direction |> FlowDirection.toString
        let cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth =
            masterAgreement.cadence |> cadenceToColumns
        let counterparty = masterAgreement.counterparty |> Counterparty.value
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
              { name = "@start_date"; value = DbLocalDate(masterAgreement.startDate) }
              { name = "@end_date"; value = NullableDbLocalDate(masterAgreement.endDate) }
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
        let! memo = memoStr |> convertOptionToDesiredTypeWithFallibleConverter AgreementMemo.create
        return
            create
                agreementID
                agreementName
                direction
                cadence
                counterparty
                startDate
                endDate
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

