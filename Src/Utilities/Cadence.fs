module Model.CashFlow.Cadence

open System
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper

type WeekDay =
    | Sunday
    | Monday
    | Tuesday
    | Wednesday
    | Thursday
    | Friday
    | Saturday

module WeekDay =
    let fromString str =
        match str with
        | "Sunday" -> Ok Sunday
        | "Monday" -> Ok Monday
        | "Tuesday" -> Ok Tuesday
        | "Wednesday" -> Ok Wednesday
        | "Thursday" -> Ok Thursday
        | "Friday" -> Ok Friday
        | "Saturday" -> Ok Saturday
        | _ -> Error (CashflowInvalidWeekDay str)
    let toString weekDay =
        match weekDay with
        | Sunday -> "Sunday"
        | Monday -> "Monday"
        | Tuesday -> "Tuesday"
        | Wednesday -> "Wednesday"
        | Thursday -> "Thursday"
        | Friday -> "Friday"
        | Saturday -> "Saturday"
    
    let toIsoDayOfWeek weekDay =
        match weekDay with
        | Sunday -> IsoDayOfWeek.Sunday
        | Monday -> IsoDayOfWeek.Monday
        | Tuesday -> IsoDayOfWeek.Tuesday
        | Wednesday -> IsoDayOfWeek.Wednesday
        | Thursday -> IsoDayOfWeek.Thursday
        | Friday -> IsoDayOfWeek.Friday
        | Saturday -> IsoDayOfWeek.Saturday
    
    let fromIsoDayOfWeek weekDay =
        match weekDay with
        | IsoDayOfWeek.Sunday -> Sunday
        | IsoDayOfWeek.Monday -> Monday
        | IsoDayOfWeek.Tuesday -> Tuesday
        | IsoDayOfWeek.Wednesday -> Wednesday
        | IsoDayOfWeek.Thursday -> Thursday
        | IsoDayOfWeek.Friday -> Friday
        | IsoDayOfWeek.Saturday -> Saturday
        | _ -> raise (ArgumentException "I can't imagine how we got here, but you invented a new day of the week.")

type Month =
    | January
    | February
    | March
    | April
    | May
    | June
    | July
    | August
    | September
    | October
    | November
    | December

module Month =
    let toString m =
        match m with
        | January -> "January"
        | February -> "February"
        | March -> "March"
        | April -> "April"
        | May -> "May"
        | June -> "June"
        | July -> "July"
        | August -> "August"
        | September -> "September"
        | October -> "October"
        | November -> "November"
        | December -> "December"
    let fromString str =
        match str with
        | "January" -> Ok January
        | "February" -> Ok February
        | "March" -> Ok March
        | "April" -> Ok April
        | "May" -> Ok May
        | "June" -> Ok June
        | "July" -> Ok July
        | "August" -> Ok August
        | "September" -> Ok September
        | "October" -> Ok October
        | "November" -> Ok November
        | "December" -> Ok December
        | _ -> Error (CashflowInvalidMonth str)
    let toMonthNum m =
        match m with
        | January -> 1
        | February -> 2
        | March -> 3
        | April -> 4
        | May -> 5
        | June -> 6
        | July -> 7
        | August -> 8
        | September -> 9
        | October -> 10
        | November -> 11
        | December -> 12
    let toAbbreviation m =
        match m with
        | January -> "Jan"
        | February -> "Feb"
        | March -> "Mar"
        | April -> "Apr"
        | May -> "May"
        | June -> "Jun"
        | July -> "Jul"
        | August -> "Aug"
        | September -> "Sep"
        | October -> "Oct"
        | November -> "Nov"
        | December -> "Dec"

type DateInMonthNumber = private DateInMonthNumber of int

module DateInMonthNumber =
    let value (DateInMonthNumber i) = i
    let fromInt i =
        // we fail anything > 28 because not all months have 29+ days
        if i > 0 && i < 29 then Ok (DateInMonthNumber i) else Error (CashflowInvalidDateInMonthNumber i)

type WeekInMonthNumber = private WeekInMonthNumber of int

module WeekInMonthNumber =
    let value (WeekInMonthNumber i) = i
    let fromInt i =
        if i > 0 && i < 5 then Ok (WeekInMonthNumber i) else Error (CashflowInvalidWeekInMonthNumber i)

type MonthDay =
    | DateInMonth of DateInMonthNumber
    | NthWeekDay of WeekInMonthNumber * WeekDay
    | Last

type CadenceType =
    | Daily
    | Weekly of WeekDay
    | EveryOtherWeek of WeekDay
    | Monthly of MonthDay
    | Annually of Month * MonthDay

type CadenceNextInstance = { nextInstance: LocalDate }

type Cadence = private { cadenceType: CadenceType; nextInstance: CadenceNextInstance }

let cadenceType c = c.cadenceType

let nextInstance c = c.nextInstance

let confirmWeekDay
    (weekday: WeekDay)
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    if weekday |> WeekDay.toIsoDayOfWeek = nextInstance.nextInstance.DayOfWeek then Ok()
    else Error AccountBalanceFetchInvalidArguments // todo: simian write a real error message here

let confirmDateInMonth
    (dateInMonthNumber: DateInMonthNumber)
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    if dateInMonthNumber |> DateInMonthNumber.value = nextInstance.nextInstance.Day then Ok()
    else Error AccountBalanceFetchInvalidArguments // todo: simian write a real error message here

let confirmNthWeekDayInMonth
    (weekInMonthNumber: WeekInMonthNumber)
    (weekday: WeekDay)
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    let nextInstanceMonth = nextInstance.nextInstance.Month
    let nextInstanceYear = nextInstance.nextInstance.Year
    let isoWeekDay = weekday |> WeekDay.toIsoDayOfWeek
    let n = weekInMonthNumber |> WeekInMonthNumber.value
    let nextInstanceShouldBe = LocalDate.FromYearMonthWeekAndDay(nextInstanceYear, nextInstanceMonth, n, isoWeekDay)
    if nextInstanceShouldBe = nextInstance.nextInstance then Ok()
    else Error AccountBalanceFetchInvalidArguments // todo: simian write a real error message here

let confirmLastDayOfMonth
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    let nextInstanceMonth = nextInstance.nextInstance.Month
    let nextInstanceYear = nextInstance.nextInstance.Year
    let daysInMonth = CalendarSystem.Iso.GetDaysInMonth(nextInstanceYear, nextInstanceMonth)
    let nextInstanceShouldBe = LocalDate(nextInstanceYear, nextInstanceMonth, daysInMonth)
    if nextInstanceShouldBe = nextInstance.nextInstance then Ok()
    else Error AccountBalanceFetchInvalidArguments // todo: simian write a real error message here

let confirmMonthDay
    (monthDay: MonthDay)
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    match monthDay with
    | DateInMonth dateInMonthNumber -> nextInstance |> confirmDateInMonth dateInMonthNumber
    | NthWeekDay (weekInMonthNumber, weekday) ->
        nextInstance |> confirmNthWeekDayInMonth weekInMonthNumber weekday
    | Last -> nextInstance |> confirmLastDayOfMonth

let confirmMonth
    (month: Month)
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    if nextInstance.nextInstance.Month = (month |> Month.toMonthNum) then Ok ()
    else Error AccountBalanceFetchInvalidArguments // todo: simian write a real error message here

let confirmAnnually
    (month: Month)
    (monthDay: MonthDay)
    (nextInstance: CadenceNextInstance)
    : Result<unit, AppError> =
    let monthDayResult = nextInstance |> confirmMonthDay monthDay
    let monthResult = nextInstance |> confirmMonth month
    match monthDayResult, monthResult with
    | Ok _, Ok _ -> Ok()
    | _ -> Error AccountBalanceFetchInvalidArguments // todo: simian write a real error message here
    
let confirmNextInstance
    (cadence: Cadence)
    : Result<unit, AppError> =
    match cadence.cadenceType with
    | Daily -> Ok ()
    | Weekly weekDay -> cadence.nextInstance |> confirmWeekDay weekDay
    | EveryOtherWeek weekDay -> cadence.nextInstance |> confirmWeekDay weekDay
    | Monthly monthDay -> cadence.nextInstance |> confirmMonthDay monthDay
    | Annually(month, monthDay) -> cadence.nextInstance |> confirmAnnually month monthDay

let create
    (cadenceType: CadenceType)
    (nextInstance: CadenceNextInstance)
    : Result<Cadence, AppError> =
    result {
        let cadence = { cadenceType = cadenceType; nextInstance = nextInstance }
        do! cadence |> confirmNextInstance
        return cadence
    }

let private monthDayToColumns md =
    match md with
    | DateInMonth d -> Some(d |> DateInMonthNumber.value), None, None
    | NthWeekDay(w, wd) -> None, Some(w |> WeekInMonthNumber.value), Some(wd |> WeekDay.toString)
    | Last -> None, None, None

let cadenceToColumns (cadence: Cadence) =
    let nextInstance = cadence.nextInstance.nextInstance
    match cadence.cadenceType with
    | Daily -> "Daily", None, None, None, None, nextInstance
    | Weekly wd -> "Weekly", None, None, Some(wd |> WeekDay.toString), None, nextInstance
    | EveryOtherWeek wd -> "EveryOtherWeek", None, None, Some(wd |> WeekDay.toString), None, nextInstance
    | Monthly md ->
        let dateInMonth, weekInMonth, weekDay = md |> monthDayToColumns
        "Monthly", dateInMonth, weekInMonth, weekDay, None, nextInstance
    | Annually(month, md) ->
        let dateInMonth, weekInMonth, weekDay = md |> monthDayToColumns
        "Annually", dateInMonth, weekInMonth, weekDay, Some(month |> Month.toString), nextInstance

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

let reconstitute
    (cadenceName: string)
    (cadenceWeekDay: string option)
    (cadenceDateInMonth: int option)
    (cadenceWeekInMonth: int option)
    (cadenceMonth: string option)
    (nextInstance: LocalDate)
    : Result<Cadence, AppError> =
    result {
        let! cadenceType =  
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
        return { cadenceType = cadenceType; nextInstance = {nextInstance = nextInstance} }
    }

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
    (dateInMonthNumber: DateInMonthNumber)
    : LocalDate =
    let dateInMonthInt = dateInMonthNumber |> DateInMonthNumber.value
    let priorMonthInt = priorDate.Month
    let priorYearInt = priorDate.Year
    let newYear, newMonth =
        if priorMonthInt = 12
        then (priorYearInt + 1), 1
        else priorYearInt, priorMonthInt + 1
    LocalDate(newYear, newMonth, dateInMonthInt)

let private incrementNthWeekday
    (priorDate: LocalDate)
    (weekInMonthNumber: WeekInMonthNumber)
    (weekday: WeekDay)
    : LocalDate =
    let priorMonthInt = priorDate.Month
    let priorYearInt = priorDate.Year
    let isoWeekDay = weekday |> WeekDay.toIsoDayOfWeek
    let n = weekInMonthNumber |> WeekInMonthNumber.value
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
    (monthDay: MonthDay)
    (priorDate: LocalDate)
    : LocalDate =
    match monthDay with
    | DateInMonth dateInMonthNumber -> incrementDateInMonthNumber priorDate dateInMonthNumber
    | NthWeekDay (weekInMonthNumber, weekday) -> incrementNthWeekday priorDate weekInMonthNumber weekday
    | Last -> incrementLastDayOfMonth priorDate

let private incrementAnnually
    (monthDay: MonthDay)
    (priorDate: LocalDate)
    : LocalDate =
    match monthDay with
    | DateInMonth _ -> priorDate.PlusYears(1)
    | NthWeekDay (weekInMonthNumber, weekday) ->
        let newYear = priorDate.Year + 1
        let newMonth = priorDate.Month
        let isoWeekDay = weekday |> WeekDay.toIsoDayOfWeek
        let n = weekInMonthNumber |> WeekInMonthNumber.value
        LocalDate.FromYearMonthWeekAndDay(newYear, newMonth, n, isoWeekDay)
    | Last -> 
        let newYear = priorDate.Year + 1
        let newMonth = priorDate.Month
        let daysInMonth = CalendarSystem.Iso.GetDaysInMonth(newYear, newMonth)
        LocalDate(newYear, newMonth, daysInMonth)
    
let determineNextDateFromPrior
    (priorDate: LocalDate)
    (cadenceType: CadenceType)
    : LocalDate =
    match cadenceType with
    | Daily -> priorDate |> incrementDaily
    | Weekly _ -> priorDate |> incrementWeekly
    | EveryOtherWeek _ -> priorDate |> incrementBiWeekly
    | Monthly monthDay -> priorDate |> incrementMonthly monthDay
    | Annually (_, monthDay) -> priorDate |> incrementAnnually monthDay
