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

    let value (FiscalPeriodKey pk) = pk

    /// the key's month, built from a date
    let ofDate (date: NodaTime.LocalDate) : FiscalPeriodKey =
        FiscalPeriodKey $"{date.Year:D4}-{date.Month:D2}"

    /// the first day of the key's month
    let startDate (FiscalPeriodKey pk) : NodaTime.LocalDate =
        NodaTime.LocalDate(int pk[0..3], int pk[5..6], 1)

    /// the last day of the key's month
    let endDate (key: FiscalPeriodKey) : NodaTime.LocalDate =
        (key |> startDate).PlusMonths(1).PlusDays(-1)
