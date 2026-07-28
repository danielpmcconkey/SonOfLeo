module Tests.Integrated.ModelOrchestrator.FiscalPeriodCreation

open Logger.Audit
open Model.Ledger.FiscalPeriods
open ModelOrchestrator
open Tests.Integrated.InterfaceBridge._routeResolver
open Tests.Integrated.Railroad
open Xunit
open Utilities.AppError

[<Fact>]
let ``REQ-FP-1.4 REQ-FP-1.5 REQ-FP-2.3 Fiscal period start and end date are derived from the key`` () =
    let keyString = "1974-06"
    let expectedMonth = 6
    let expectedStartDay = 1
    let expectedEndDay = 30
    let key =
        keyString
        |> FiscalPeriodKey.fromString
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    runFuncAndAutoRollback FiscalPeriodCreate (fun context ->
        let fp =
            key
            |> FiscalPeriodCreation.constructNewAndSaveToDb context
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let startDate = FiscalPeriod.startDate fp
        let endDate = FiscalPeriod.endDate fp
        Assert.Equal(expectedMonth, startDate.Month)
        Assert.Equal(expectedStartDay, startDate.Day)
        Assert.Equal(expectedEndDay, endDate.Day)
        Ok ()
    ) |> railroadWrapper
