module Business.General.BizGeneralError

open NodaTime
open App.Utility.IAppError

type BizGeneralError =
    | ActiveEndBeforeBegin of LocalDate * LocalDate option
    | CadenceDateNotLastDayOfMonth of LocalDate
    | CadenceDateNotNthWeekDayInMonth of LocalDate * int * string
    | CadenceDateNotOnAnnualDate of LocalDate * string * string
    | CadenceDateNotOnDateInMonth of LocalDate * int
    | CadenceDateNotOnMonth of LocalDate * string
    | CadenceDateNotOnWeekDay of LocalDate * string
    | InvalidCadenceRow of string
    | InvalidDateInMonthNumber of int
    | InvalidMonth of string
    | InvalidWeekDay of string
    | InvalidWeekInMonthNumber of int
    
    interface IAppError with
        member this.ToMessage() =
            match this with        
            | ActiveEndBeforeBegin(activeBegin, activeEnd) -> $"An activity period's active end ({activeEnd}) cannot be before its active begin ({activeBegin})."
            | CadenceDateNotLastDayOfMonth date -> $"{date} does not fit the cadence, which falls on the last day of the month."
            | CadenceDateNotNthWeekDayInMonth(date, weekInMonth, weekDay) -> $"{date} does not fit the cadence, which falls on {weekDay} number {weekInMonth} of the month."
            | CadenceDateNotOnAnnualDate(date, monthDay, month) -> $"{date} does not fit the annual cadence, which falls on {monthDay} of {month}."
            | CadenceDateNotOnDateInMonth(date, dateInMonth) -> $"{date} does not fit the cadence, which falls on day {dateInMonth} of the month."
            | CadenceDateNotOnMonth(date, month) -> $"{date} does not fit the cadence, which falls in {month}."
            | CadenceDateNotOnWeekDay(date, weekDay) -> $"{date} does not fit the cadence, which falls on a {weekDay}."
            | InvalidCadenceRow reason -> $"Invalid Cadence row: {reason}."
            | InvalidDateInMonthNumber i -> $"Invalid DateInMonthNumber of \"{i}\"."
            | InvalidMonth str -> $"Invalid Month of \"{str}\"."
            | InvalidWeekDay str -> $"Invalid WeekDay of \"{str}\"."
            | InvalidWeekInMonthNumber i -> $"Invalid WeekInMonthNumber of \"{i}\"."


let toMessage (e: BizGeneralError) = (e :> IAppError).ToMessage()

let convertListOfResultsToResultsList<'T>
    (wrapError: IAppError -> BizGeneralError)
    (listOfResults: Result<'T, BizGeneralError> list)
    : Result<'T list, BizGeneralError> =
    listOfResults
    |> List.map (Result.mapError (fun e -> e :> IAppError))
    |> App.Utility.Result.convertListOfResultsToResultsList
    |> Result.mapError wrapError

    
