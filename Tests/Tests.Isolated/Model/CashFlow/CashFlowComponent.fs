module Tests.Isolated.Model.CashFlow.CashFlowComponent

open App.Utility.IAppError
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.CashFlow.CashFlowError
open Xunit

(* The typed error names the raw value and the bound it broke: 1 for too few days, 365 for too many. *)
[<Theory>]
[<InlineData(0)>]
[<InlineData(-1)>]
[<InlineData(366)>]
let ``REQ-CF-7.1 for each of 0, -1 and 366, a sweep horizon of that many days is rejected with a typed error`` (days: int) =
    let rejectedWithTheBound =
        match days |> ProjectionHorizonInDays.create with
        | Error (AsError (CashflowProjectionHorizonInDaysBelowMin (raw, 1))) -> days < 1 && raw = days
        | Error (AsError (CashflowProjectionHorizonInDaysExceededMax (raw, 365))) -> days > 365 && raw = days
        | _ -> false
    Assert.True(rejectedWithTheBound)
