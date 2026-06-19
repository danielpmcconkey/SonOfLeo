namespace Model.Money

open System
open Utilities.ResultCE

type Money = private { amount: decimal }

module Money =
    
    let maxMoney:decimal = 9999999999.99M // REQ-MON-1.2
    let minMoney:decimal = -9999999999.99M // REQ-MON-1.3

    
    let amount (m:Money) = m.amount // REQ-MON-2.8
    
    let private create (validD: decimal) : Money =
        {amount = validD}
        
    let fromDecimal (raw: decimal) : Result<Money, string> = // REQ-MON-2.2
        let rounded = Math.Round(raw, 2, MidpointRounding.AwayFromZero) // REQ-MON-1.4, REQ-MON-2.2.1
        match rounded with
        | x when x <> raw -> Error $"Failed to convert {raw} to Money record due to improper decimal precision" // REQ-MON-1.4, REQ-MON-2.2.1
        | x when x > maxMoney -> Error $"Failed to convert {raw} to Money record as value exceeds the maximum allowable value of {maxMoney}" // REQ-MON-1.2, REQ-MON-2.2.1
        | x when x < minMoney -> Error $"Failed to convert {raw} to Money record as value falls below the minimum allowable value of {minMoney}" // REQ-MON-1.3, REQ-MON-2.2.1
        | _ -> Ok (create raw)
    
    let fromDecimalList (l: decimal list) : Result<Money list, string> = // REQ-MON-2.3
        l
        |> List.map fromDecimal
        |> List.foldBack (fun createResult acc -> // REQ-MON-2.3.2
            match createResult, acc with
            | Ok validCr, Ok validAcc -> Ok (validCr :: validAcc) // REQ-MON-2.3.1
            | Error e, _ -> Error e
            | _, Error e -> Error e
            ) <| Ok []
    
    /// splitByN allows the caller to split a Money amount into N mostly-equal parts
    /// and returns a list of valid Money records. It is important to note that, in
    /// instances where the input Money record's amount cannot divide evenly (to the
    /// penny) by N, one record will have the difference added. The higher N is, the
    /// greater the possibility for the residual to grow.
    let splitByN (m: Money) (n: int) : Result<Money list, string> =  // REQ-MON-2.4
        match n with
        | a when a < 0 -> Error "Cannot split money negative ways" // REQ-MON-2.4.6
        | 0 -> Error "Cannot split money when 0 ways" // REQ-MON-2.4.2
        | 1 -> Error "Cannot split money 1 way" // REQ-MON-2.4.3
        | _ ->
            let fractions = Math.Round(m.amount / decimal n, 2, MidpointRounding.AwayFromZero) // REQ-MON-2.4.4
            let diff = (fractions * decimal n) - m.amount
            let firstShare = fractions - diff  // REQ-MON-2.4.5
            let remainingShares = fractions
            result {
                let dList = firstShare :: List.replicate (n - 1) remainingShares // REQ-MON-2.4.5
                let sumTotal = dList |> List.sum 
                let! _ = if sumTotal = amount m then Ok () else Error $"sum of all shares {sumTotal} does not match original amount {amount m}" // REQ-MON-2.4.1
                return! fromDecimalList dList
            }
    
    let add (m: Money) (n: Money): Result<Money, string> = // REQ-MON-2.5
        fromDecimal (m.amount + n.amount) // REQ-MON-2.5.1

    /// subtracts n from m
    let subtract (m: Money) (n: Money): Result<Money, string> = // REQ-MON-2.6
        fromDecimal (m.amount - n.amount) // REQ-MON-2.6.1
    
    let sumList (l: Money list): Result<Money, string> = // REQ-MON-2.9
        let sum_d = l |> List.sumBy(amount)
        fromDecimal sum_d // REQ-MON-2.9.1