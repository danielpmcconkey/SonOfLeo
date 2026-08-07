namespace Tests.Helpers

open System.Diagnostics

type ExecutableCliForTesting =
    | SonOfLeoCli
    | Reports

module ExecutableCliForTesting =
    let toString e =
        match e with
        | SonOfLeoCli -> "SonOfLeoCli.dll"
        | Reports -> "Reports.dll"

module CliExecutor = 
    let testBinDir =
        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
        
    let private cliPath exe =
        System.IO.Path.Combine(testBinDir, exe |> ExecutableCliForTesting.toString)

    let runCli (exe: ExecutableCliForTesting) (args: string list) (payload: string) =
        let psi = ProcessStartInfo()
        psi.FileName <- "dotnet"
        psi.Arguments <- sprintf "%s %s" (cliPath exe) (String.concat " " args)
        psi.RedirectStandardInput <- true
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false

        use proc = Process.Start(psi)
        proc.StandardInput.Write(payload)
        proc.StandardInput.Close()
        let stdout = proc.StandardOutput.ReadToEnd()
        let stderr = proc.StandardError.ReadToEnd()
        proc.WaitForExit()
        (proc.ExitCode, stdout, stderr)
