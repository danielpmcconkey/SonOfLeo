namespace Model.Ledger.FiscalPeriods

open System
open System.Text.RegularExpressions

type FiscalPeriodId = private FiscalPeriodId of Guid

module FiscalPeriodId =
    let create () : FiscalPeriodId = (FiscalPeriodId (Guid.NewGuid()))
    let fromGuid g = FiscalPeriodId g
    let value (FiscalPeriodId g) : Guid = g

type FiscalPeriodKey = private PeriodKey of string

module FiscalPeriodKey =
    let validationRegex = @"^\d{4}-(0[1-9]|1[0-2])$" // REQ-FP-1.2
    let isValidString (s: string) : bool = Regex.IsMatch(s, validationRegex)
    let fromString (raw: string) : Result<FiscalPeriodKey, string> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        match trimmed |> isValidString with
        | false -> Error $"Passed string \"{raw}\" is invalid as a Period Key."
        | true -> Ok (PeriodKey trimmed)

    let value (PeriodKey pk) = pk

