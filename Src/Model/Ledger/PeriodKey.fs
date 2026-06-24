namespace Model.Ledger.FiscalPeriods

open System.Text.RegularExpressions

type PeriodKey = private PeriodKey of string

module PeriodKey =
    let validationRegex = @"^\d{4}-(0[1-9]|1[0-2])$" // REQ-FP-1.2
    let isValidString (s: string) : bool = Regex.IsMatch(s, validationRegex)
    let fromString (raw: string) : Result<PeriodKey, string> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        match trimmed |> isValidString with
        | false -> Error $"Passed string \"{raw}\" is invalid as a Period Key."
        | true -> Ok (PeriodKey trimmed)

    let value (PeriodKey pk) = pk