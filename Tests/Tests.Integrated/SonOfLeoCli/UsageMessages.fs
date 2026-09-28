module Tests.Integrated.SonOfLeoCli.UsageMessages

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type UsageMessagesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-NGUI-3.11 for each of no arguments and one argument, the CLI writes a usage message to stderr, nothing to stdout, and exits with a non-zero code`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-NGUI-4.6 the Reports CLI run with no arguments writes a usage message to stderr, nothing to stdout, and exits with a non-zero code`` () =
        Assert.Fail "not implemented"
