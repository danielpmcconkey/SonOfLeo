module Business.FinancialServices.BizFinServError

open App.Utility.IAppError

type BizFinServError =
    | MoneyFailedToConvertBelowMin of decimal * decimal
    | MoneyFailedToConvertExceededMax of decimal * decimal
    | MoneyFailedToConvertImproperPrecision of decimal
    | MoneyImproperSplit of int
    | MoneySplitFailedReconciliation of decimal * decimal
    
    interface IAppError with
        member this.DomainName = nameof BizFinServError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | MoneyFailedToConvertBelowMin(raw, min) -> $"Failed to convert {raw} to Money record as value falls below the minimum allowable value of {min}."
            | MoneyFailedToConvertExceededMax(raw, max) -> $"Failed to convert {raw} to Money record as value exceeds the maximum allowable value of {max}."
            | MoneyFailedToConvertImproperPrecision raw -> $"Failed to convert {raw} to Money record due to improper decimal precision."
            | MoneyImproperSplit n -> $"Improper Money split of {n}. Money can only be split by a positive integer, greater than 1."
            | MoneySplitFailedReconciliation(originalAmount, sumTotal) -> $"Sum of all shares {sumTotal} does not match original amount {originalAmount}."

let toMessage (e: BizFinServError) = (e :> IAppError).ToMessage()
let toAppError (e: BizFinServError) : IAppError = e :> IAppError
let error (e: BizFinServError) : Result<'T, IAppError> = Error (e :> IAppError)

