module Tests.Integrated.SonOfLeoCli.FileArgumentPosition

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type FileArgumentPositionTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-NGUI-3.10 for each CLI (main and Reports), --file placed after an ordinary additional argument is not read as the payload source, and the payload is read from stdin`` () =
        Assert.Fail "not implemented"
