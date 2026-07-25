module Tests.Integrated.SonOfLeoCli.CliExecutor

open System.Diagnostics

let private cliPath =
    let testDir =
        System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location)
    System.IO.Path.Combine(testDir, "SonOfLeoCli.dll")


let runCli (args: string list) (payload: string) =
    let psi = ProcessStartInfo()
    psi.FileName <- "dotnet"
    psi.Arguments <- sprintf "%s %s" cliPath (String.concat " " args)
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
