namespace Model

open System
open Utilities.AppError
open Utilities.ResultHelper

type Money = private { amount: decimal }

module Money =

    let maxMoney: decimal = 9999999999.99M // REQ-MON-1.2
    let minMoney: decimal = -9999999999.99M // REQ-MON-1.3


    let amount (m: Money) = m.amount // REQ-MON-2.8

    let private create (validD: decimal) : Money = { amount = validD }

    let fromDecimal (raw: decimal) : Result<Money, AppError> = // REQ-MON-2.2
        let rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero) // REQ-MON-1.4, REQ-MON-2.2.1
        // note, rounded is only used here as a known good to confirm that raw
        // is correct. Therefore, passing raw to the create function is
        // appropriate and preferred.
        match rounded with
        | x when x <> raw -> Error(MoneyFailedToConvertImproperPrecision raw) // REQ-MON-1.4, REQ-MON-2.2.1
        | x when x > maxMoney -> Error(MoneyFailedToConvertExceededMax(raw, maxMoney)) // REQ-MON-1.2, REQ-MON-2.2.1
        | x when x < minMoney -> Error(MoneyFailedToConvertBelowMin(raw, minMoney)) // REQ-MON-1.3, REQ-MON-2.2.1
        | _ -> Ok(create raw)

    let fromDecimalList (l: decimal list) : Result<Money list, AppError> = // REQ-MON-2.3
        l
        |> List.map fromDecimal // REQ-MON-2.3.1
        |> convertListOfResultsToResultsList // REQ-MON-2.3.2 (fold back enables the order preservation)

    /// splitByN allows the caller to split a Money amount into N mostly-equal parts
    /// and returns a list of valid Money records. It is important to note that, in
    /// instances where the input Money record's amount cannot divide evenly (to the
    /// penny) by N, one record will have the difference added. The higher N is, the
    /// greater the possibility for the residual to grow.
    let splitByN (m: Money) (n: int) : Result<Money list, AppError> = // REQ-MON-2.4
        match n with
        | a when a <= 1 -> Error(MoneyImproperSplit a) // REQ-MON-2.4.6, REQ-MON-2.4.2, REQ-MON-2.4.3
        | _ ->
            let fractions = Math.Round(m.amount / decimal n, 2, MidpointRounding.AwayFromZero) // REQ-MON-2.4.4
            let diff = (fractions * decimal n) - m.amount
            let firstShare = fractions - diff // REQ-MON-2.4.5
            let remainingShares = fractions
            result {
                let dList = firstShare :: List.replicate (n - 1) remainingShares // REQ-MON-2.4.5
                let sumTotal = dList |> List.sum
                do!
                    if sumTotal = amount m then
                        Ok()
                    else
                        Error(MoneySplitFailedReconciliation(amount m, sumTotal)) // REQ-MON-2.4.1
                return! fromDecimalList dList
            }

    let add (m: Money) (n: Money) : Result<Money, AppError> = // REQ-MON-2.5
        fromDecimal(m.amount + n.amount) // REQ-MON-2.5.1

    let subtractVal1FromVal2 (val1: Money) (val2: Money) : Result<Money, AppError> = // REQ-MON-2.6
        fromDecimal(val2.amount - val1.amount) // REQ-MON-2.6.1

    let sumList (l: Money list) : Result<Money, AppError> = // REQ-MON-2.9
        let sum_d = l |> List.sumBy amount
        fromDecimal sum_d // REQ-MON-2.9.1
