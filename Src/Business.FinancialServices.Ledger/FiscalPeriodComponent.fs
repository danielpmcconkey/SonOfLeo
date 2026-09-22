module Business.FinancialServices.Ledger.FiscalPeriodComponent

open System
open System.Text.RegularExpressions
open App.Utility.IAppError
open Business.FinancialServices.Ledger.LedgerError

type FiscalPeriodId = private FiscalPeriodId of Guid

module FiscalPeriodId =
    let create () : FiscalPeriodId = FiscalPeriodId(Guid.NewGuid())
    let fromGuid g = FiscalPeriodId g
    let value (FiscalPeriodId g) : Guid = g

type FiscalPeriodKey = private FiscalPeriodKey of string

module FiscalPeriodKey =
    let validationRegex = @"^\d{4}-(0[1-9]|1[0-2])$"
    let isValidString (s: string) : bool = Regex.IsMatch(s, validationRegex)
    let fromString (raw: string) : Result<FiscalPeriodKey, IAppError> =
        let trimmed = raw.Trim()
        match trimmed |> isValidString with
        | false -> Error(FiscalPeriodInvalidKeyString raw)
        | true -> Ok(FiscalPeriodKey trimmed)

    let internal reconstitute (raw: string) = raw |> FiscalPeriodKey

    let value (FiscalPeriodKey pk) = pk
