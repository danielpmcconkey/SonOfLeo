module ModelOrchestrator.CashFlowOps

open System
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open ModelOrchestrator
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.ResultHelper

(*
    CashFlowOps represents the activities that the operator will perform every time we run finances (the saturday
    routine)
*)

let private incrementDaily
    (priorDate: LocalDate)
    : LocalDate =
    priorDate.PlusDays(1)

let private incrementWeekly
    (priorDate: LocalDate)
    : LocalDate =
    priorDate.PlusDays(7)

let private incrementBiWeekly
    (priorDate: LocalDate)
    : LocalDate =
    priorDate.PlusDays(14)

let private incrementDateInMonthNumber
    (priorDate: LocalDate)
    (dateInMonthNumber: Cadence.DateInMonthNumber)
    : LocalDate =
    let dateInMonthInt = dateInMonthNumber |> Cadence.DateInMonthNumber.value
    let priorMonthInt = priorDate.Month
    let priorYearInt = priorDate.Year
    let newYear, newMonth =
        if priorMonthInt = 12
        then (priorYearInt + 1), 1
        else priorYearInt, priorMonthInt + 1
    LocalDate(newYear, newMonth, dateInMonthInt)

let private incrementNthWeekday
    (priorDate: LocalDate)
    (weekInMonthNumber: Cadence.WeekInMonthNumber)
    (weekday: Cadence.WeekDay)
    : LocalDate =
    let priorMonthInt = priorDate.Month
    let priorYearInt = priorDate.Year
    let isoWeekDay = weekday |> Cadence.WeekDay.toIsoDayOfWeek
    let n = weekInMonthNumber |> Cadence.WeekInMonthNumber.value
    let newYear, newMonth =
        if priorMonthInt = 12
        then (priorYearInt + 1), 1
        else priorYearInt, priorMonthInt + 1
    LocalDate.FromYearMonthWeekAndDay(newYear, newMonth, n, isoWeekDay)

let private incrementLastDayOfMonth
    (agreementStart: LocalDate)
    : LocalDate =
    let priorMonthInt = agreementStart.Month
    let priorYearInt = agreementStart.Year
    let newYear, newMonth =
        if priorMonthInt = 12
        then (priorYearInt + 1), 1
        else priorYearInt, priorMonthInt + 1
    let daysInMonth = CalendarSystem.Iso.GetDaysInMonth(newYear, newMonth)
    LocalDate(newYear, newMonth, daysInMonth)

let private incrementMonthly
    (monthDay: Cadence.MonthDay)
    (priorDate: LocalDate)
    : LocalDate =
    match monthDay with
    | Cadence.DateInMonth dateInMonthNumber -> incrementDateInMonthNumber priorDate dateInMonthNumber
    | Cadence.NthWeekDay (weekInMonthNumber, weekday) -> incrementNthWeekday priorDate weekInMonthNumber weekday
    | Cadence.Last -> incrementLastDayOfMonth priorDate

let private incrementAnnually
    (monthDay: Cadence.MonthDay)
    (priorDate: LocalDate)
    : LocalDate =
    match monthDay with
    | Cadence.DateInMonth _ -> priorDate.PlusYears(1)
    | Cadence.NthWeekDay (weekInMonthNumber, weekday) ->
        let newYear = priorDate.Year + 1
        let newMonth = priorDate.Month
        let isoWeekDay = weekday |> Cadence.WeekDay.toIsoDayOfWeek
        let n = weekInMonthNumber |> Cadence.WeekInMonthNumber.value
        LocalDate.FromYearMonthWeekAndDay(newYear, newMonth, n, isoWeekDay)
    | Cadence.Last -> 
        let newYear = priorDate.Year + 1
        let newMonth = priorDate.Month
        let daysInMonth = CalendarSystem.Iso.GetDaysInMonth(newYear, newMonth)
        LocalDate(newYear, newMonth, daysInMonth)
    
let private determineNextDateFromPrior
    (priorDate: LocalDate)
    (cadenceType: Cadence.CadenceType)
    : LocalDate =
    match cadenceType with
    | Cadence.Daily -> priorDate |> incrementDaily
    | Cadence.Weekly _ -> priorDate |> incrementWeekly
    | Cadence.EveryOtherWeek _ -> priorDate |> incrementBiWeekly
    | Cadence.Monthly monthDay -> priorDate |> incrementMonthly monthDay
    | Cadence.Annually (_, monthDay) -> priorDate |> incrementAnnually monthDay
    
let rec private fillInstanceDatesToCutOff
    (nextDate: LocalDate)
    (cutOffDate: LocalDate)
    (cadenceType: Cadence.CadenceType)
    (accumulator: LocalDate list)
    : LocalDate list =
    if nextDate > cutOffDate then accumulator // break out of the recursion
    else 
    let nextNextDate = determineNextDateFromPrior nextDate cadenceType
    fillInstanceDatesToCutOff nextNextDate cutOffDate cadenceType (nextDate::accumulator)

let private spawnInstancesFromAgreement
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    (agreement: AgreementOrchestration.Agreement)
    : Result<AgreementOrchestration.Agreement, AppError> =
    result {
        let cutOffDate = Calendar.today().PlusDays(daysOut |> ProjectionHorizonInDays.value)
        let master = agreement |> AgreementOrchestration.masterAgreement
        let agreementId = master |> MasterAgreement.agreementID
        let cadence = master |> MasterAgreement.cadence
        let cadenceType = cadence |> Cadence.cadenceType
        let nextInstance = cadence |> Cadence.nextInstance
        let nextInstanceDate = nextInstance.nextInstance
        let neededDates = fillInstanceDatesToCutOff nextInstanceDate cutOffDate cadenceType []
        do! neededDates
            |> List.map(fun neededDate ->
                let instanceId = InstanceId.create()
                let now = context |> Context.getInitiationInstant
                let newInstance = Instance.create instanceId agreementId neededDate false now now
                newInstance |> Instance.insertNewToDb context
                )
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        // you are here. Up next you need to find the latest date you just added, then derive the next date after that
        // so you can update the MasterAgreement's cadence's next date 
        raise(NotImplementedException())
        return agreement
    }

let private spawnInstancesFromAgreements
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    (agreements: AgreementOrchestration.Agreement list)
    : Result<AgreementOrchestration.Agreement list, AppError> =
    agreements
    |> List.map (spawnInstancesFromAgreement context daysOut)
    |> convertListOfResultsToResultsList
    

let ProjectionSweep
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    : Result<AgreementOrchestration.Agreement list, AppError> =
    result {
    // Takes a horizon (days). Walks every active agreement's cadence, creates missing Instances, creates Invoices for
    // fixed-amount PAs. Returns what it created and what already existed.
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        return! agreements |> spawnInstancesFromAgreements context daysOut
    }
    

let Projection() =
    // Takes a horizon. Reads ledger balances + open invoices. Returns per-account `{ currentBalance, knownInflows,
    // knownOutflows, projectedLow }` + `billsToChase` (instances with no invoice).
    raise(NotImplementedException())

let TransitionPaymentsToPosted() =
    // No input. For every Payment pointing at a staged entry that now has a JE, transitions pointer to Posted + updates
    // invoice posted state. Returns the list.
    raise(NotImplementedException())

let StagedEntryMatchCandidates() =
    // Given an invoice or agreement, returns unlinked staged entries matching the PA's account pattern within the
    // instance's date window. Candidates only — Hobson decides. 
    raise(NotImplementedException())

let AgreementSummary() =
    // Full tree view: master → PAs → recent instances → invoices → payments. 
    raise(NotImplementedException())
