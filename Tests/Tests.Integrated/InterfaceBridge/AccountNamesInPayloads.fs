module Tests.Integrated.InterfaceBridge.AccountNamesInPayloads

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type AccountNamesInPayloadsTests(fixture: TestDataFixture) =
    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-NGUI-1.6 for each route whose return payload carries an account code in any role (the account itself, its parent, an agreement's debit or credit account), the payload carries beside each code the name of the account that code identifies`` () =
        Assert.Fail "Not yet implemented"
