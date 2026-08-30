module Tests.Integrated.ModelOrchestrator.FiscalPeriodCreation

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.Ledger
open Model.Ledger.FiscalPeriodComponent
open ModelOrchestrator
open Tests.Helpers.Railroad
open Xunit
open Utilities.AppError
open Utilities.ResultHelper

(* REQ-FP-1.5 singles out February and leap years, so the derivation is exercised across
   all three month-end lengths rather than the one 30-day month it used to use. *)
[<Theory>]
[<InlineData("1974-07", 1974, 7, 31)>]
[<InlineData("1974-06", 1974, 6, 30)>]
[<InlineData("2050-02", 2050, 2, 28)>]
[<InlineData("2048-02", 2048, 2, 29)>]
let ``REQ-FP-1.4 REQ-FP-1.5 REQ-FP-2.3 fiscal period runs from the first of the keyed month to its last day, February and leap years included``
    (keyString: string)
    (expectedYear: int)
    (expectedMonth: int)
    (expectedEndDay: int)
    =
    let key =
        keyString
        |> FiscalPeriodKey.fromString
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
        result {
            let! fp = key |> FiscalPeriodCreation.constructNewAndSaveToDb context
            let startDate = FiscalPeriod.startDate fp
            let endDate = FiscalPeriod.endDate fp
            Assert.Equal(expectedYear, startDate.Year)
            Assert.Equal(expectedMonth, startDate.Month)
            Assert.Equal(1, startDate.Day)
            Assert.Equal(expectedYear, endDate.Year)
            Assert.Equal(expectedMonth, endDate.Month)
            Assert.Equal(expectedEndDay, endDate.Day)
        })
    |> railroadWrapper
