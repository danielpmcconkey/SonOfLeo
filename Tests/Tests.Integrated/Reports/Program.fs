module Tests.Integrated.Reports.Program

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open App.Utility.Json.Json
open Tests.Helpers
open Tests.Helpers.CliExecutor
open Tests.Helpers.Railroad
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility.Result
open Xunit

[<Collection("SharedTestData")>]
type ProgramTests(fixture: TestDataFixture) =

    let nextMonth = Calendar.today().PlusMonths(1)
    let standardInput = { asOf = { asOf = nextMonth }; reportOutput = OutputSpecifier.DataOnly }
    let badPathRoot = "/spaghetti"
    let badPathFile = "deleteme"
    let badPathInput = {
        asOf = { asOf = nextMonth }
        reportOutput = (OutputSpecifier.Report {baseDir = badPathRoot; interpolateAsOf = false; fileName = badPathFile})
    }
    
    [<Fact>]
    member _.``REQ-NGUI-1.3 System responds with a failure code when failing``() =
        let args = [ "TrialBalance" ]
        let badPayload = "{}"
        let exitCode, _, _ = runCli Reports args badPayload
        (exitCode = 1) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-1.3, REQ-NGUI-4.4 System responds with a success code when succeeding``() =
        let args = [ "TrialBalance" ]
        let payload =
            standardInput
            |> toJson<TrialBalanceReportInput>
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let exitCode, _, _ = runCli Reports args payload
        (exitCode = 0) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-1.3.1, REQ-NGUI-4.4 The stderr will comprise the error message``() =
        // intentionally get the file write to throw the same error you're expecting
        let textToWrite = "this was supposed to fail fail. If you can read this, something is broke in SonOfLeo"
        result {
            let! path = File.createFullPath badPathRoot $"{badPathFile}.html"
            do! match textToWrite |> File.writeTextFile path with
                | Ok _ -> Error (TestingError "expected failure but got success")
                | Error intendedError -> 
                    let expectedErrorMessage = $"{intendedError.ToMessage()}{Environment.NewLine}"
                    let args = [ "TrialBalance" ]
                    let payload =
                        badPathInput
                        |> toJson<TrialBalanceReportInput>
                        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
                    let _, _, e = runCli Reports args payload
                    Assert.Equal(expectedErrorMessage, e)
                    Ok()
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-NGUI-4.4 System responds with the payload via stdout upon success``() =
        let args = [ "TrialBalance" ]
        let payload =
            standardInput
            |> toJson<TrialBalanceReportInput>
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let exitCode, p, _ = runCli Reports args payload
        Assert.Equal(0, exitCode)
        result {
            let! fetched = p |> fromJson<TrialBalanceReportReturn>
            Assert.True(fetched.IsDataOnly)
            return ()
        } |> railroadWrapper

    [<Fact>]
    member _.``REQ-NGUI-4.2 The name argument is case sensitive``() =
        let args = [ "trialbalance" ]
        let payload =
            standardInput
            |> toJson<TrialBalanceReportInput>
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let exitCode, _, _ = runCli Reports args payload
        (exitCode = 1) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-4.5 Incorrect routes must exit with an appropriate error``() =
        let expected = "Unknown report: RopaInterior."
        let args = [ "RopaInterior"; ]
        let payload =
            standardInput
            |> toJson<TrialBalanceReportInput>
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let exitCode, _, e = runCli Reports args payload
        (exitCode = 1) |> Assert.True
        Assert.Equal(expected, e.Trim())


