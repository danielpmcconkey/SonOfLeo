module Tests.Integrated.ModelOrchestrator.FiscalPeriodCreation

open Model.Ledger.FiscalPeriods
open ModelOrchestrator
open Utilities.DAL
open Xunit
open Utilities.AppError
open Tests.Integrated.GenericTestProperties
    
[<Fact>]
let ``REQ-FP-1.4 REQ-FP-1.5 REQ-FP-2.3 Fiscal period start and end date are derived from the key`` () =    
    let keyString = "1974-06"
    let expectedMonth = 6
    let expectedStartDay = 1
    let expectedEndDay = 30
    let key = keyString |> FiscalPeriodKey.fromString |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
    let transaction = createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e)) // if this fails, nothing can proceed
    try
        let fp = FiscalPeriodCreation.constructNewAndSaveToDb key genericAuditEnvelope (Some transaction)
                 |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let startDate = FiscalPeriod.startDate fp
        let endDate = FiscalPeriod.endDate fp
        Assert.Equal(expectedMonth, startDate.Month)
        Assert.Equal(expectedStartDay, startDate.Day)
        Assert.Equal(expectedEndDay, endDate.Day)
    finally
        rollbackDbTransactionAndDisposeConnection transaction |> ignore
