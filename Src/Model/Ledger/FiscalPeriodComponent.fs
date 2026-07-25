namespace Model.Ledger.FiscalPeriods

open System
open System.Text.RegularExpressions
open Utilities.AppError

type FiscalPeriodId = private FiscalPeriodId of Guid

module FiscalPeriodId =
    let create () : FiscalPeriodId = FiscalPeriodId(Guid.NewGuid())
    let fromGuid g = FiscalPeriodId g
    let value (FiscalPeriodId g) : Guid = g

type FiscalPeriodKey = private FiscalPeriodKey of string

module FiscalPeriodKey =
    let validationRegex = @"^\d{4}-(0[1-9]|1[0-2])$" // REQ-FP-1.2
    let isValidString (s: string) : bool = Regex.IsMatch(s, validationRegex)
    let fromString (raw: string) : Result<FiscalPeriodKey, AppError> =
        let trimmed = raw.Trim() // REQ-SYS-1.1
        match trimmed |> isValidString with
        | false -> Error(FiscalPeriodInvalidKeyString raw)
        | true -> Ok(FiscalPeriodKey trimmed)

    let internal reconstitute (raw: string) = raw |> FiscalPeriodKey

    let value (FiscalPeriodKey pk) = pk
