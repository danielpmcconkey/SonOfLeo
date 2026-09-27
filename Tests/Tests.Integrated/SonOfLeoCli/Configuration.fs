module Tests.Integrated.SonOfLeoCli.Configuration

open System
open System.IO
open System.Net
open System.Net.Sockets
open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Json.Json
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Tests.Helpers
open Tests.Helpers.CliExecutor
open Xunit

(* Each test runs both CLIs from a copy of the test bin directory whose appsettings.json the test controls. The
   connection string the copy would use points at a local listener that never answers, so "no data read or written"
   is observed directly: if either CLI tried to reach a database, the listener has a pending connection. *)

type private ConfigFile =
    | NoFile
    | Contents of string

let private connectionStringEnvVar = "SONOFLEO_CONFIG_TEST_CONNSTR"

let private settings (connectionStringSetting: string option) (timeZoneSetting: string option) =
    [ connectionStringSetting |> Option.map (fun v -> $"\"ConnectionStringEnvVar\": \"{v}\"")
      timeZoneSetting |> Option.map (fun v -> $"\"LocalizedTimeZone\": \"{v}\"") ]
    |> List.choose id
    |> String.concat ", "
    |> sprintf "{ %s }"
    |> Contents

let private validConnectionStringSetting = Some connectionStringEnvVar
let private validTimeZoneSetting = Some "America/New_York"

let private toJsonOrFail<'T> (value: 'T) =
    value |> toJson<'T> |> Result.defaultWith (fun (e: IAppError) -> failwith (e.ToMessage()))

// each would read the database if it were allowed to run
let private commands =
    [ SonOfLeoCli, [ "Account"; "FetchAll" ], toJsonOrFail<AccountFetchAllInput> { activeOnly = true }
      Reports,
      [ "TrialBalance" ],
      toJsonOrFail<TrialBalanceReportInput> {
          asOf = { asOf = LocalDate(2050, 9, 1) }
          reportOutput = OutputSpecifier.DataOnly } ]

// The copy runs in a child process. Copying a hundred files in-process tiers up the runtime's file-open path, which
// changes the stack trace another test (Reports.Program, "The stderr will comprise the error message") compares
// against the report CLI's.
let private copyDirectory (source: string) (target: string) =
    let psi = Diagnostics.ProcessStartInfo("cp")
    psi.ArgumentList.Add "-R"
    psi.ArgumentList.Add source
    psi.ArgumentList.Add target
    psi.UseShellExecute <- false
    use proc = Diagnostics.Process.Start psi
    proc.WaitForExit()
    if proc.ExitCode <> 0 then failwith $"copying {source} to {target} failed with exit code {proc.ExitCode}"

type private CliRun =
    { exe: ExecutableCliForTesting
      exitCode: int
      stdout: string
      stderr: string
      configFilePath: string
      triedTheDatabase: bool }

let private runBothCliesWith (configFile: ConfigFile) : CliRun list =
    let binDir = Path.Combine(Path.GetTempPath(), $"sonofleo-config-{Guid.NewGuid()}")
    let listener = TcpListener(IPAddress.Loopback, 0)
    try
        copyDirectory testBinDir binDir
        let configFilePath = Path.Combine(binDir, "appsettings.json")
        match configFile with
        | NoFile -> File.Delete configFilePath
        | Contents text -> File.WriteAllText(configFilePath, text)
        listener.Start()
        let port = (listener.LocalEndpoint :?> IPEndPoint).Port
        let environment =
            [ connectionStringEnvVar,
              $"Host=127.0.0.1;Port={port};Database=nothing;Username=nobody;Password=nothing;Timeout=2" ]
        commands
        |> List.map (fun (exe, args, payload) ->
            let exitCode, stdout, stderr = runCliIn binDir environment exe args payload
            { exe = exe
              exitCode = exitCode
              stdout = stdout
              stderr = stderr
              configFilePath = configFilePath
              triedTheDatabase = listener.Pending() })
    finally
        listener.Stop()
        if Directory.Exists binDir then Directory.Delete(binDir, true)

let private confirmRefused (expectedInMessage: CliRun -> string) (runs: CliRun list) =
    Assert.Equal(2, runs.Length)
    for run in runs do
        let label = run.exe |> ExecutableCliForTesting.toString
        Assert.True(run.exitCode <> 0, $"{label} exited 0")
        Assert.False(run.triedTheDatabase, $"{label} tried to reach the database")
        Assert.Contains(expectedInMessage run, run.stderr)
        Assert.DoesNotMatch(@"(?m)^\s+at \S", run.stderr)
        Assert.Equal("", run.stdout)

[<Collection("SharedTestData")>]
type ConfigurationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-DAL-1.3 started without the configuration file, the operator CLI and the report CLI each exits non-zero with a message naming the file, no stack trace, and no data read or written`` () =
        runBothCliesWith NoFile |> confirmRefused _.configFilePath

    [<Fact>]
    member _.``REQ-DAL-1.3 started with a configuration file that exists but cannot be parsed, the operator CLI and the report CLI each exits non-zero with a message naming the file, no stack trace, and no data read or written`` () =
        runBothCliesWith (Contents "{ this is not json") |> confirmRefused _.configFilePath

    [<Fact>]
    member _.``REQ-DAL-1.3 started with the connection string setting absent, the operator CLI and the report CLI each exits non-zero with a message naming that setting, no stack trace, and no data read or written`` () =
        runBothCliesWith (settings None validTimeZoneSetting) |> confirmRefused (fun _ -> "ConnectionStringEnvVar")

    [<Fact>]
    member _.``REQ-DAL-1.3 started with the connection string setting empty, the operator CLI and the report CLI each exits non-zero with a message naming that setting, no stack trace, and no data read or written`` () =
        runBothCliesWith (settings (Some "") validTimeZoneSetting) |> confirmRefused (fun _ -> "ConnectionStringEnvVar")

    [<Fact>]
    member _.``REQ-SYS-7.1 started with a time zone setting that is not a recognised identifier, the operator CLI and the report CLI each exits non-zero with a message naming that setting and runs no command under any other time zone`` () =
        runBothCliesWith (settings validConnectionStringSetting (Some "Mars/Olympus_Mons"))
        |> confirmRefused (fun _ -> "LocalizedTimeZone")

    [<Fact>]
    member _.``REQ-SYS-7.1 started with no time zone setting, the operator CLI and the report CLI each exits non-zero with a message naming that setting and runs no command under any other time zone`` () =
        runBothCliesWith (settings validConnectionStringSetting None) |> confirmRefused (fun _ -> "LocalizedTimeZone")

    [<Fact>]
    member _.``REQ-SYS-7.1 an Instant late in the evening in the configured time zone, already the next day in UTC, converts to the configured zone's date`` () =
        // the test run is configured for America/New_York; 02:30 UTC on 10 March 2050 is 21:30 on 9 March there
        let instant = Instant.FromUtc(2050, 3, 10, 2, 30)
        Assert.Equal(LocalDate(2050, 3, 10), instant.InUtc().Date)
        Assert.Equal(LocalDate(2050, 3, 9), instant |> Calendar.dateFromInstant)
