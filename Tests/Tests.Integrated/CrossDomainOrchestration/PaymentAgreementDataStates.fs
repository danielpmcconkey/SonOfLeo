module Tests.Integrated.CrossDomainOrchestration.PaymentAgreementDataStates

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PaymentAgreementDataStatesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-3.3 a Payment Agreement created with its agreement is stored referencing that agreement's ID`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.3 writing a Payment Agreement at the model level whose Master Agreement ID names no stored agreement is rejected with a typed error and no Payment Agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.4 REQ-CF-3.5 a Payment Agreement created with existing debit and credit accounts is stored referencing exactly those accounts on their respective sides`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Debit")>]
    [<InlineData("Credit")>]
    member _.``REQ-CF-3.4 REQ-CF-3.5 REQ-CF-3.11 for each of the debit side and the credit side, creating an agreement whose Payment Agreement names an account that does not exist on that side is rejected with a typed error naming that side, and no agreement is stored`` (side:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.7 an agreement whose Payment Agreement has no expected amount is created and its Payment Agreement is stored with no expected amount`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("0.00")>]
    [<InlineData("-0.01")>]
    member _.``REQ-CF-3.7 for each of 0.00 and -0.01, creating an agreement whose Payment Agreement has that expected amount is rejected with a typed error and no agreement is stored`` (amount:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.7 an agreement whose Payment Agreement has an expected amount of 0.01 is stored with 0.01`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.7 creating an agreement whose Payment Agreement expected amount has a fraction of a cent is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.8 a Payment Agreement with no memo is stored with no memo`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.8 a Payment Agreement memo of exactly 2000 characters is stored unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.8 creating an agreement whose Payment Agreement memo is 2001 characters is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-3.8 for each of the empty string, a single space and a string of spaces and tabs, creating an agreement whose Payment Agreement memo is that string is rejected with a typed error and no agreement is stored`` (text:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.8 REQ-SYS-1.1 a Payment Agreement memo with leading and trailing spaces is stored and returned with them removed and its interior unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.8 REQ-SYS-1.1 a Payment Agreement memo of 2000 characters padded with leading and trailing spaces is stored as the 2000 characters`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-3.9 for each of null, the empty string, a single space and a string of spaces and tabs, creating an agreement whose Payment Agreement name is that value is rejected with a typed error and no agreement is stored`` (text:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 a Payment Agreement name of exactly 250 characters is stored with that name`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 creating an agreement whose Payment Agreement name is 251 characters is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 REQ-SYS-1.1 a Payment Agreement name with leading and trailing spaces is stored and returned with them removed and its interior unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 REQ-SYS-1.1 a Payment Agreement name of 250 characters padded with leading and trailing spaces is stored as the 250 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 creating an agreement whose Payment Agreement has the name of a Payment Agreement of another agreement is rejected with a typed error, no second agreement is stored and the existing Payment Agreement is unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 REQ-SYS-1.1 creating an agreement whose Payment Agreement name differs from an existing Payment Agreement's name only by leading and trailing spaces is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.9 creating an agreement with two Payment Agreements of the same name is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(365)>]
    member _.``REQ-CF-3.10 for each of 0 and 365, an agreement whose Payment Agreement has that days-due value is created and the Payment Agreement is stored with that value`` (days:int) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(-1)>]
    [<InlineData(366)>]
    member _.``REQ-CF-3.10 for each of -1 and 366, creating an agreement whose Payment Agreement has that days-due value is rejected with a typed error and no agreement is stored`` (days:int) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-3.10 an agreement whose Payment Agreement has no days-due value is created and its Payment Agreement is stored with none`` () =
        Assert.Fail "not implemented"
