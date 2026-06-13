namespace Model.Money

open System
open Utilities.ResultCE

type Money = private { amount: decimal }

module Money =
    
    let private maxMoney:decimal = 9999999999.99M
    let private minMoney:decimal = -9999999999.99M

    
    let amount (m:Money) = m.amount
    
    let private create (validD: decimal) : Money =
        {amount = validD}
        
    let fromDecimal (raw: decimal) : Result<Money, string> =
        let rounded = Math.Round(raw,2)
        match rounded with
        | x when x <> raw -> Error $"Failed to convert {raw} to Money record due to improper decimal precision"
        | x when x > maxMoney -> Error $"Failed to convert {raw} to Money record as value exceeds the maximum allowable value of {maxMoney}"
        | x when x < minMoney -> Error $"Failed to convert {raw} to Money record as value falls below the minimum allowable value of {minMoney}"
        | _ -> Ok (create raw)
    
    let fromDecimalList (l: decimal list) : Result<Money list, string> =
        l
        |> List.map fromDecimal
        |> List.foldBack (fun createResult acc ->
            match createResult, acc with
            | Ok validCr, Ok validAcc -> Ok (validCr :: validAcc)
            | Error e, _ -> Error e
            | _, Error e -> Error e
            ) <| Ok []
    
    /// splitByN allows the caller to split a Money amount into N mostly-equal parts
    /// and returns a list of valid Money records. It is important to note that, in
    /// instances where the input Money record's amount cannot divide evenly (to the
    /// penny) by N, one record will have the difference added. The higher N is, the
    /// greater the possibility for the residual to grow.
    let splitByN (m: Money) (n: int) : Result<Money list, string> =
        match n with
        | a when a < 0 -> Error "Cannot split money negative ways"
        | 0 -> Error "Cannot split money when 0 ways"
        | 1 -> Error "Cannot split money 1 way"
        | _ ->
            let fractions = Math.Round(m.amount / decimal n, 2, MidpointRounding.AwayFromZero)
            let diff = (fractions * decimal n) - m.amount
            let firstShare = fractions - diff
            let remainingShares = fractions
            result {
                let dList = firstShare :: List.replicate (n - 1) remainingShares
                return! fromDecimalList dList
            }
    
    let add (m: Money) (n: Money): Result<Money, string> =
        fromDecimal (m.amount + n.amount)

    /// subtracts n from m
    let subtract (m: Money) (n: Money): Result<Money, string> =
        fromDecimal (m.amount - n.amount)