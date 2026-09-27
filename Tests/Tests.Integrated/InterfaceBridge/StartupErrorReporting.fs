namespace Tests.Integrated.InterfaceBridge

open Xunit

type StartupErrorReportingTests() =

    [<Fact>]
    member _.``REQ-NGUI-1.3 REQ-NGUI-1.3.2 an operation that throws a system exception exits with a failure code and an error payload carrying the exception's message plus its stack trace, including the frame that threw``() =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-NGUI-1.3.1 REQ-NGUI-1.3.2 an operation that fails with a typed error exits with a failure code and an error payload consisting of that error's message and nothing else, with no stack trace``() =
        Assert.Fail "not implemented"
