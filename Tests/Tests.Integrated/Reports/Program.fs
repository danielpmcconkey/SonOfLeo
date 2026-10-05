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
open Tests.Helpers.RouteResolver
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
                    (* The message embeds the exception's stack trace. Its frame lines come from the runtime and
                       differ between the CLI's process and this one, so they are stripped from both sides and
                       everything else is compared whole. No requirement asks for a trace, so none is asserted. *)
                    let withoutFrames (text: string) =
                        text.Split('\n')
                        |> Array.map (fun line -> line.TrimEnd('\r'))
                        |> Array.filter (fun line ->
                            not (Text.RegularExpressions.Regex.IsMatch(line, @"^\s+at \S"))
                            && not (line.TrimStart().StartsWith("--- End of")))
                        |> String.concat "\n"
                        |> fun s -> s.TrimEnd()
                    let expected = intendedError.ToMessage() |> withoutFrames
                    let args = [ "TrialBalance" ]
                    let payload =
                        badPathInput
                        |> toJson<TrialBalanceReportInput>
                        |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
                    let exitCode, _, e = runCli Reports args payload
                    Assert.Equal(1, exitCode)
                    Assert.NotEmpty(expected)
                    Assert.Equal(expected, e |> withoutFrames)
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
        (* The trial balance computation has its own fixture-derived tests, so the route is the oracle for what the
           CLI's stdout carries (ROUTE-ORACLE). *)
        let rowsOf (returned: TrialBalanceReportReturn) =
            match returned with
            | TrialBalanceReportReturn.DataOnly rows ->
                Ok(rows |> List.map (fun r -> r.accountCode, r.accountName, r.totalDebits, r.totalCredits))
            | TrialBalanceReportReturn.Report _ -> TestError.error (TestingError "Expected DataOnly but got Report")
        result {
            let! fetched = p |> fromJson<TrialBalanceReportReturn> |> Result.bind rowsOf
            let! routed =
                routeReportingCommandForTesting "TrialBalance" [] payload
                |> Result.bind fromJson<TrialBalanceReportReturn>
                |> Result.bind rowsOf
            Assert.True(routed.Length >= 2, "the route returned fewer than two trial balance rows")
            Assert.Equal<(string * string * decimal * decimal) list>(routed, fetched)
            return ()
        } |> railroadWrapper

    [<Fact>]
    member _.``REQ-NGUI-4.2 The name argument is case sensitive``() =
        let args = [ "trialbalance" ]
        let payload =
            standardInput
            |> toJson<TrialBalanceReportInput>
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        let exitCode, _, e = runCli Reports args payload
        Assert.Equal(1, exitCode)
        Assert.Equal("Unknown report: trialbalance.", e.Trim())

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


