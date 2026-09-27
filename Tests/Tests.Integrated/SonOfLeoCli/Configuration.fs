module Tests.Integrated.SonOfLeoCli.Configuration

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type ConfigurationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-DAL-1.3 started without the configuration file, the operator CLI and the report CLI each exits non-zero with a message naming the file, no stack trace, and no data read or written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-DAL-1.3 started with a configuration file that exists but cannot be parsed, the operator CLI and the report CLI each exits non-zero with a message naming the file, no stack trace, and no data read or written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-DAL-1.3 started with the connection string setting absent, the operator CLI and the report CLI each exits non-zero with a message naming that setting, no stack trace, and no data read or written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-DAL-1.3 started with the connection string setting empty, the operator CLI and the report CLI each exits non-zero with a message naming that setting, no stack trace, and no data read or written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-7.1 started with a time zone setting that is not a recognised identifier, the operator CLI and the report CLI each exits non-zero with a message naming that setting and runs no command under any other time zone`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-7.1 started with no time zone setting, the operator CLI and the report CLI each exits non-zero with a message naming that setting and runs no command under any other time zone`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-SYS-7.1 an Instant late in the evening in the configured time zone, already the next day in UTC, converts to the configured zone's date`` () =
        Assert.Fail "not implemented"
