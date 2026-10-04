module Tests.Integrated.SonOfLeoCli.FileArgumentPosition

open System
open System.IO
open App.Utility
open App.Utility.IAppError
open App.Utility.Json.Json
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Tests.Helpers
open Tests.Helpers.CliExecutor
open Xunit

(* Each CLI is given a valid payload on stdin and a file holding a payload that can be told apart from it: for the
   main CLI, a lookup of a different account; for the Reports CLI, a request for the rendered report where stdin asks
   for data only. The same file placed straight after the verb (or report name) is also run, to show the file is read
   there and would change the outcome. *)

let private orFail (r: Result<string, IAppError>) = r |> Result.defaultWith (fun e -> failwith (e.ToMessage()))

let private withFile (contents: string) (test: string -> unit) =
    let path = Path.Combine(Path.GetTempPath(), $"file-position-{Guid.NewGuid():N}.json")
    File.WriteAllText(path, contents)
    try test path finally File.Delete path

[<Collection("SharedTestData")>]
type FileArgumentPositionTests(fixture: TestDataFixture) =

    [<Theory>]
    [<InlineData("main")>]
    [<InlineData("Reports")>]
    member _.``REQ-NGUI-3.10 for each CLI (main and Reports), --file placed after an ordinary additional argument is not read as the payload source, and the payload is read from stdin`` (cli: string) =
        match cli with
        | "main" ->
            let stdinPayload = { code = "F-1280" } |> toJson<AccountFetchByCodeInput> |> orFail
            let filePayload = { code = "F-2230" } |> toJson<AccountFetchByCodeInput> |> orFail
            withFile filePayload (fun path ->
                let _, fromFile, _ = runCli SonOfLeoCli [ "Account"; "FetchByCode"; "--file"; path ] stdinPayload
                Assert.Contains("F-2230", fromFile)
                let exitCode, stdout, stderr = runCli SonOfLeoCli [ "Account"; "FetchByCode"; "extra"; "--file"; path ] stdinPayload
                Assert.Equal(0, exitCode)
                Assert.Contains("F-1280", stdout)
                Assert.DoesNotContain("F-2230", stdout)
                Assert.Equal<string>("", stderr))
        | _ ->
            let stdinPayload =
                { asOf = { asOf = Calendar.today () }; reportOutput = OutputSpecifier.DataOnly } |> toJson<TrialBalanceReportInput> |> orFail
            let outputDir = Directory.CreateDirectory("/tmp/son-of-leo-test-output").FullName
            let reportName = $"file-position-{Guid.NewGuid():N}"
            let reportPath = Path.Combine(outputDir, $"{reportName}.html")
            let filePayload =
                { asOf = { asOf = Calendar.today () }
                  reportOutput = OutputSpecifier.Report { baseDir = outputDir; interpolateAsOf = false; fileName = reportName } }
                |> toJson<TrialBalanceReportInput> |> orFail
            try
                withFile filePayload (fun path ->
                    let fromFileExit, fromFile, _ = runCli Reports [ "TrialBalance"; "--file"; path ] stdinPayload
                    Assert.Equal(0, fromFileExit)
                    match fromJson<TrialBalanceReportReturn> fromFile with
                    | Ok (TrialBalanceReportReturn.Report written) -> Assert.Equal(reportPath, written.fullyQualifiedPath)
                    | Ok (TrialBalanceReportReturn.DataOnly _) -> Assert.Fail "the file's payload asked for a report; stdin's data-only payload was read"
                    | Error e -> Assert.Fail $"stdout is not a trial balance return. {e.ToMessage()}"
                    File.Delete reportPath
                    let exitCode, stdout, stderr = runCli Reports [ "TrialBalance"; "extra"; "--file"; path ] stdinPayload
                    Assert.Equal(0, exitCode)
                    match fromJson<TrialBalanceReportReturn> stdout with
                    | Ok (TrialBalanceReportReturn.DataOnly rows) -> Assert.NotEmpty(rows)
                    | Ok (TrialBalanceReportReturn.Report _) -> Assert.Fail "the file's report payload was read; stdin's data-only payload should have been"
                    | Error e -> Assert.Fail $"stdout is not a trial balance return. {e.ToMessage()}"
                    Assert.False(File.Exists reportPath, "the file's payload was read and wrote its report")
                    Assert.Equal<string>("", stderr))
            finally
                File.Delete reportPath
