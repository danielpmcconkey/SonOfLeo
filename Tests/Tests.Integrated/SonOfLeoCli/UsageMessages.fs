module Tests.Integrated.SonOfLeoCli.UsageMessages

open System
open Tests.Helpers
open Tests.Helpers.CliExecutor
open Xunit

(* Both CLIs are run as processes, as an operator would run them. A usage message is recognised by the word "Usage",
   which is what the message is for; its exact wording is free to change. *)

let private assertUsage (exitCode: int, stdout: string, stderr: string) =
    Assert.NotEqual(0, exitCode)
    Assert.Equal<string>("", stdout)
    Assert.Contains("usage", stderr.ToLowerInvariant())

[<Collection("SharedTestData")>]
type UsageMessagesTests(fixture: TestDataFixture) =

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(1)>]
    member _.``REQ-NGUI-3.11 for each of no arguments and one argument, the CLI writes a usage message to stderr, nothing to stdout, and exits with a non-zero code`` (count: int) =
        let args = [ "Account" ] |> List.truncate count
        runCli SonOfLeoCli args "" |> assertUsage

    [<Fact>]
    member _.``REQ-NGUI-4.6 the Reports CLI run with no arguments writes a usage message to stderr, nothing to stdout, and exits with a non-zero code`` () =
        runCli Reports [] "" |> assertUsage
