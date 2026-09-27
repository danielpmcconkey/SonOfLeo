namespace Tests.Helpers

open System.Diagnostics

type ExecutableCliForTesting =
    | SonOfLeoCli
    | Reports

module ExecutableCliForTesting =
    let toString e =
        match e with
        | SonOfLeoCli -> "Ui.OperatorCli.dll"
        | Reports -> "Ui.ReportCli.dll"

module CliExecutor = 
    let testBinDir =
        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
        
    /// runCliIn runs a CLI from binDir with extra environment variables, for tests that give it a different
    /// configuration from the test run's own.
    let runCliIn
        (binDir: string)
        (environment: (string * string) list)
        (exe: ExecutableCliForTesting)
        (args: string list)
        (payload: string) =
        let psi = ProcessStartInfo()
        psi.FileName <- "dotnet"
        psi.Arguments <-
            sprintf "%s %s" (System.IO.Path.Combine(binDir, exe |> ExecutableCliForTesting.toString)) (String.concat " " args)
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        for name, value in environment do
            psi.Environment[name] <- value

        use proc = Process.Start(psi)
        proc.StandardInput.Write(payload)
        proc.StandardInput.Close()
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        (proc.ExitCode, stdout, stderr)

    let runCli (exe: ExecutableCliForTesting) (args: string list) (payload: string) =
        runCliIn testBinDir [] exe args payload
