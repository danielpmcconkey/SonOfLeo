module Tests.Integrated.CrossDomainOrchestration.FiscalPeriodCreation

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.FiscalPeriodComponent
open Tests.Helpers.Railroad
open Xunit
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility.Result
open Business.FinancialServices.Ledger.LedgerError

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
        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
    runCommandRouteAndAutoRollback FiscalPeriodCreate (fun context ->
        result {
            let! fp = key |> FiscalPeriodCreation.constructNewAndPersist context
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


let private keyOf keyString =
    keyString |> FiscalPeriodKey.fromString |> Result.defaultWith (fun (e: IAppError) -> failwith (e.ToMessage()))

let private keysOf periods = periods |> List.map (FiscalPeriod.periodKey >> FiscalPeriodKey.value)

let private fetchByKey context keyString =
    keyString |> FiscalPeriod.fetchIdByKey context |> Result.bind (FiscalPeriod.fetchById context)

[<Fact>]
let ``REQ-FP-2.7 ensuring periods from one month to another creates each missing month, open, and returns exactly the periods it created`` () =
    runCommandRouteAndAutoRollback FiscalPeriodEnsure (fun context ->
        result {
            // across a year end, so the month arithmetic has to roll the year
            let! created = FiscalPeriodCreation.ensureFiscalPeriods context (keyOf "2061-11") (keyOf "2062-02")
            let expectedKeys = [ "2061-11"; "2061-12"; "2062-01"; "2062-02" ]
            Assert.Equal<string list>(expectedKeys, created |> keysOf |> List.sort)
            Assert.All(created, fun fp -> Assert.True(fp |> FiscalPeriod.isOpen))
            let! stored = expectedKeys |> List.map (fetchByKey context) |> convertListOfResultsToResultsList
            Assert.Equal<FiscalPeriod.FiscalPeriod list>(
                created |> List.sortBy (FiscalPeriod.periodKey >> FiscalPeriodKey.value),
                stored |> List.sortBy (FiscalPeriod.periodKey >> FiscalPeriodKey.value))
        })
    |> railroadWrapper

[<Fact>]
let ``REQ-FP-2.7 periods already in the range are left unchanged and are not returned, whether open or closed`` () =
    runCommandRouteAndAutoRollback FiscalPeriodEnsure (fun context ->
        result {
            let! _ = keyOf "2062-05" |> FiscalPeriodCreation.constructNewAndPersist context
            let! closing = keyOf "2062-06" |> FiscalPeriodCreation.constructNewAndPersist context
            let! _ = closing |> FiscalPeriod.fiscalPeriodId |> FiscalPeriod.closeFiscalPeriod context
            let! openBefore = fetchByKey context "2062-05"
            let! closedBefore = fetchByKey context "2062-06"
            let ensureContext = context |> Context.updateInitiationInstant
            let! created = FiscalPeriodCreation.ensureFiscalPeriods ensureContext (keyOf "2062-04") (keyOf "2062-07")
            Assert.Equal<string list>([ "2062-04"; "2062-07" ], created |> keysOf |> List.sort)
            let! openAfter = fetchByKey ensureContext "2062-05"
            let! closedAfter = fetchByKey ensureContext "2062-06"
            Assert.Equal(openBefore, openAfter)
            Assert.Equal(closedBefore, closedAfter)
            Assert.False(closedAfter |> FiscalPeriod.isOpen)
        })
    |> railroadWrapper

[<Fact>]
let ``REQ-FP-2.7 when every month in the range already has a period, the operation succeeds and returns an empty list`` () =
    runCommandRouteAndAutoRollback FiscalPeriodEnsure (fun context ->
        result {
            let! _ = keyOf "2063-01" |> FiscalPeriodCreation.constructNewAndPersist context
            let! _ = keyOf "2063-02" |> FiscalPeriodCreation.constructNewAndPersist context
            let! before = FiscalPeriod.fetchAll context false
            let! created = FiscalPeriodCreation.ensureFiscalPeriods context (keyOf "2063-01") (keyOf "2063-02")
            Assert.Empty(created)
            let! after = FiscalPeriod.fetchAll context false
            Assert.Equal(before |> List.length, after |> List.length)
        })
    |> railroadWrapper

[<Fact>]
let ``REQ-FP-2.7 a start month later than the end month fails with a typed error, and nothing is created`` () =
    runCommandRouteAndAutoRollback FiscalPeriodEnsure (fun context ->
        result {
            let! before = FiscalPeriod.fetchAll context false
            do!
                match FiscalPeriodCreation.ensureFiscalPeriods context (keyOf "2064-03") (keyOf "2064-01") with
                | Error (AsError (FiscalPeriodEnsureStartAfterEnd ("2064-03", "2064-01"))) -> Ok()
                | Error e -> Error(TestingError $"Wrong error message. {e.ToMessage()}")
                | Ok _ -> Error(TestingError "Expected failure; got success")
            let! after = FiscalPeriod.fetchAll context false
            Assert.Equal<string list>(before |> keysOf |> List.sort, after |> keysOf |> List.sort)
        })
    |> railroadWrapper
