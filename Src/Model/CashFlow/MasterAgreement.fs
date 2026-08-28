module Model.CashFlow.MasterAgreement

open Model.CashFlow.CashFlowComponent
open Model.CashFlow.PaymentAgreement
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open DataAccessLayer.DbTransaction
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters


type Flow = {
    direction: FlowDirection
    expectedTransactions: PaymentAgreement list
}

type MasterAgreement = private {
    agreementID: MasterAgreementId
    agreementName: AgreementName
    flow: Flow
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
    flowUpdate: FieldUpdate<Flow>
    cadenceUpdate: FieldUpdate<Cadence>
    counterpartyUpdate: FieldUpdate<Counterparty>
    startDateUpdate: FieldUpdate<LocalDate>
    endDateUpdate: FieldUpdate<LocalDate option>
    memoUpdate: FieldUpdate<AgreementMemo option>
}
        
let agreementID m = m.agreementID
let agreementName m = m.agreementName
let flow m = m.flow
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
    (flow: Flow)
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
       flow = flow
       cadence = cadence
       counterparty = counterparty
       startDate = startDate
       endDate = endDate
       memo = memo
       createdAt = createdAt
       modifiedAt = modifiedAt }

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
        let flowDirection = masterAgreement.flow.direction |> FlowDirection.toString
        let monthDayParts md =
            match md with
            | DateInMonth d -> Some(d |> DateInMonthNumber.value), None, None
            | NthWeekDay(w, wd) -> None, Some(w |> WeekInMonthNumber.value), Some(wd |> WeekDay.toString)
            | Last -> None, None, None
        let cadenceName, cadenceDateInMonth, cadenceWeekInMonth, cadenceWeekDay, cadenceMonth =
            match masterAgreement.cadence with
            | Daily -> "Daily", None, None, None, None
            | Weekly wd -> "Weekly", None, None, Some(wd |> WeekDay.toString), None
            | EveryOtherWeek wd -> "EveryOtherWeek", None, None, Some(wd |> WeekDay.toString), None
            | Monthly md ->
                let dateInMonth, weekInMonth, weekDay = md |> monthDayParts
                "Monthly", dateInMonth, weekInMonth, weekDay, None
            | Annually(month, md) ->
                let dateInMonth, weekInMonth, weekDay = md |> monthDayParts
                "Annually", dateInMonth, weekInMonth, weekDay, Some(month |> Month.toString)
        let counterparty = masterAgreement.counterparty |> Counterparty.value
        let memo = masterAgreement.memo |> Option.map AgreementMemo.value
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@agreement_name"; value = CharString(agreementName) }
              { name = "@flow_direction"; value = CharString(flowDirection) }
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

