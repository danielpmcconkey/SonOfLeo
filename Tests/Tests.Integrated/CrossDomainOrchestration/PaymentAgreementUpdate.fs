module Tests.Integrated.CrossDomainOrchestration.PaymentAgreementUpdate

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PaymentAgreementUpdateTests(fixture: TestDataFixture) =
    // Placeholders named from the spec before the implementation was read (audit 2026-10-03a, brief Part A).

    [<Fact>]
    member _.``REQ-CF-14.8 for each field (name, expected amount, days-due, memo, debit account, credit account), updating a Payment Agreement's field stores the new value and leaves every other field unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.8 updating a Payment Agreement's debit or credit account to a code no account holds is rejected with a typed error naming the code, and the agreement is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.8 REQ-CF-3.6 updating a Payment Agreement's debit account to its own credit account is rejected with a typed error and the agreement is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.8 updating a Payment Agreement's expected amount and accounts leaves the amount of every existing Invoice and the pointer of every existing Payment unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.8 an update to a Payment Agreement that names no field to change is rejected with a typed error and the agreement is unchanged`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-SYS-6.1 REQ-CF-14.8 an update that sets a Payment Agreement's name to the name it already holds succeeds and advances its modified-at`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.9 adding a Payment Agreement to an existing Master Agreement stores it under that agreement with every field given, alongside the agreement's existing legs`` () =
        Assert.Fail "Not yet implemented"

    [<Fact>]
    member _.``REQ-CF-14.9 REQ-CF-3.6 adding a Payment Agreement whose debit and credit accounts are the same is rejected with a typed error and the Master Agreement keeps exactly its original legs`` () =
        Assert.Fail "Not yet implemented"
