namespace Tests.Integrated.InterfaceBridge

open System
open System.IO
open System.Runtime.CompilerServices
open App.Utility.IAppError
open Tests.Helpers.TestError
open Xunit

(* These drive Startup.run, the interfaces' outermost frame, in process, and read what it writes to stdout and stderr.
   Tests run one at a time, so redirecting the console for the length of one call captures only that call. *)
module private StartupHarness =

    let exceptionMessage = "REQ-NGUI-1.3.2 thrown by the command"

    /// Not inlined, so its frame is on the stack when it throws and the trace can be checked for it.
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let throwingCommand () : Result<string, IAppError> =
        raise (InvalidOperationException exceptionMessage)

    let typedError: IAppError = TestingError "REQ-NGUI-1.3.1 typed failure from the command"

    let failingCommand () : Result<string, IAppError> = Error typedError

    let run (command: unit -> Result<string, IAppError>) : int * string * string =
        let stdout = new StringWriter()
        let stderr = new StringWriter()
        let originalOut = Console.Out
        let originalError = Console.Error
        Console.SetOut stdout
        Console.SetError stderr
        try
            let exitCode = Ui.InterfaceBridge.Startup.run "StartupErrorReportingTests" command
            exitCode, stdout.ToString(), stderr.ToString()
        finally
            Console.SetOut originalOut
            Console.SetError originalError

type StartupErrorReportingTests() =

    [<Fact>]
    member _.``REQ-NGUI-1.3 REQ-NGUI-1.3.2 an operation that throws a system exception exits with a failure code and an error payload carrying the exception's message plus its stack trace, including the frame that threw``() =
        let exitCode, stdout, stderr = StartupHarness.run StartupHarness.throwingCommand
        Assert.Equal(1, exitCode)
        Assert.Equal("", stdout)
        Assert.Contains(StartupHarness.exceptionMessage, stderr)
        Assert.Matches(@"(?m)^\s+at \S", stderr)
        Assert.Contains(nameof StartupHarness.throwingCommand, stderr)

    [<Fact>]
    member _.``REQ-NGUI-1.3.1 REQ-NGUI-1.3.2 an operation that fails with a typed error exits with a failure code and an error payload consisting of that error's message and nothing else, with no stack trace``() =
        let exitCode, stdout, stderr = StartupHarness.run StartupHarness.failingCommand
        Assert.Equal(1, exitCode)
        Assert.Equal("", stdout)
        Assert.Equal(StartupHarness.typedError.ToMessage() + Environment.NewLine, stderr)
        Assert.DoesNotMatch(@"(?m)^\s+at \S", stderr)
