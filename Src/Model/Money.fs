namespace Model

open System
open Utilities.AppError
open Utilities.ResultHelper

type Money = private { amount: decimal }

module Money =

    let maxMoney: decimal = 9999999999.99M
    let minMoney: decimal = -9999999999.99M
    let usFormatProvider: IFormatProvider =
      System.Globalization.CultureInfo("en-US") :> IFormatProvider

    let amount (m: Money) = m.amount

    let private create (validD: decimal) : Money = { amount = validD }
    let toCurrencyString m = m.amount.ToString("C2", usFormatProvider)
    let toAccountingString m = m.amount.ToString("N2", usFormatProvider)

    let fromDecimal (raw: decimal) : Result<Money, AppError> =
        let rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero)
        // note, rounded is only used here as a known good to confirm that raw
        // is correct. Therefore, passing raw to the create function is
        // appropriate and preferred.
        match rounded with
        | x when x <> raw -> Error(MoneyFailedToConvertImproperPrecision raw)
        | x when x > maxMoney -> Error(MoneyFailedToConvertExceededMax(raw, maxMoney))
        | x when x < minMoney -> Error(MoneyFailedToConvertBelowMin(raw, minMoney))
        | _ -> Ok(create raw)

    let fromDecimalList (l: decimal list) : Result<Money list, AppError> =
        l
        |> List.map fromDecimal
        |> convertListOfResultsToResultsList

    /// splitByN allows the caller to split a Money amount into N mostly-equal parts
    /// and returns a list of valid Money records. It is important to note that, in
    /// instances where the input Money record's amount cannot divide evenly (to the
    /// penny) by N, one record will have the difference added. The higher N is, the
    /// greater the possibility for the residual to grow.
    let splitByN (m: Money) (n: int) : Result<Money list, AppError> =
        match n with
        | a when a <= 1 -> Error(MoneyImproperSplit a)
        | _ ->
            let fractions = Math.Round(m.amount / decimal n, 2, MidpointRounding.AwayFromZero)
            let diff = (fractions * decimal n) - m.amount
            let firstShare = fractions - diff
            let remainingShares = fractions
            result {
                let dList = firstShare :: List.replicate (n - 1) remainingShares
                let sumTotal = dList |> List.sum
                do!
                    if sumTotal = amount m then
                        Ok()
                    else
                        Error(MoneySplitFailedReconciliation(amount m, sumTotal))
                return! fromDecimalList dList
            }

    let add (m: Money) (n: Money) : Result<Money, AppError> =
        fromDecimal(m.amount + n.amount)

    let subtractVal1FromVal2 (val1: Money) (val2: Money) : Result<Money, AppError> =
        fromDecimal(val2.amount - val1.amount)

    let sumList (l: Money list) : Result<Money, AppError> =
        let sum_d = l |> List.sumBy amount
        fromDecimal sum_d
