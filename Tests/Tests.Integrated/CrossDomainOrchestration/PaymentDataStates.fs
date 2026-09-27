module Tests.Integrated.CrossDomainOrchestration.PaymentDataStates

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type PaymentDataStatesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-6.2 two Payments created through CreatePayment carry distinct, non-empty Payment IDs`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.3 a CreatePayment payload whose Invoice ID names no stored Invoice is rejected with a typed error and no Payment is stored pointing at its line`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.3 with two Invoices stored, a Payment created through CreatePayment is stored on the Invoice it named and the other Invoice's Payments are unchanged`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.4 for each of a journal entry line and a staged line, a CreatePayment payload pointing at that line is stored with exactly that pointer and no pointer of the other kind`` (pointer:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.4 a CreatePayment payload with a null transaction pointer is rejected with a typed error and no Payment is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.4 REQ-CF-6.5 REQ-CF-6.10 a Payment that pointed at a staged line, once that line is posted and payments transition, is stored with both the journal entry line and the original staged line, reads as Posted, and takes its amount and posted-to-ledger date from the journal entry line`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.5 for each of a Posted and a Staged pointer, a CreatePayment payload giving an amount different from its line's is read back with the line's amount`` (pointer:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.6 a CreatePayment payload with no posted-to-FI date is stored with none`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.6 a CreatePayment payload with a posted-to-FI date is stored with exactly that date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.7 a CreatePayment payload with no memo is stored with no memo`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-6.7 for each of the empty string, a single space and a string of spaces and tabs, a CreatePayment payload with that memo is rejected with a typed error and no Payment is stored`` (memo:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.7 a CreatePayment payload with a 2001-character memo is rejected with a typed error and no Payment is stored, while a 2000-character memo is stored with exactly that memo`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.7 a CreatePayment payload whose memo has text surrounded by spaces is stored rather than rejected as whitespace-only`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Income", "OtherAccount")>]
    [<InlineData("Income", "UnusedAccount")>]
    [<InlineData("Outgo", "OtherAccount")>]
    [<InlineData("Outgo", "UnusedAccount")>]
    member _.``REQ-CF-6.9 for each of Income and Outgo, and each of a line on the Payment Agreement's other account and a line on an account the agreement doesn't use, a CreatePayment payload pointing at that line is rejected with a typed error and no Payment is stored`` (direction:string, line:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.9 for each of a journal entry line and a staged line on the Payment Agreement's other account, a CreatePayment payload pointing at it is rejected with a typed error and no Payment is stored`` (pointer:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Income", "Debit")>]
    [<InlineData("Income", "Credit")>]
    [<InlineData("Outgo", "Debit")>]
    [<InlineData("Outgo", "Credit")>]
    member _.``REQ-CF-6.9 for each of Income and Outgo, and each of a Debit line and a Credit line on the account the direction requires, a CreatePayment payload pointing at that line is stored`` (direction:string, lineType:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("WrongAccount")>]
    [<InlineData("NoSuchLine")>]
    member _.``REQ-CF-6.9 REQ-CF-6.11 for each of a line on the wrong account and a journal entry line ID that names no line, a CreateInvoice payload carrying a Payment pointing at it is rejected with a typed error and no Invoice is stored`` (bad:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("WrongAccount")>]
    [<InlineData("NoSuchLine")>]
    member _.``REQ-CF-6.9 REQ-CF-6.11 for each of a line on the wrong account and a journal entry line ID that names no line, a CreateInstance payload carrying a Payment pointing at it is rejected with a typed error and nothing from the payload is stored`` (bad:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving no posted-to-ledger date and pointing at a journal entry line reads back with a posted-to-ledger date equal to that entry's entry date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.10 a Payment pointing at a staged line reads back with no posted-to-ledger date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving its journal entry's own date as the posted-to-ledger date is stored and reads back with that date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving a posted-to-ledger date different from its journal entry's date is rejected with a typed error and no Payment is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-6.10 a CreatePayment payload giving a posted-to-ledger date for a staged line is rejected with a typed error and no Payment is stored`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Posted")>]
    [<InlineData("Staged")>]
    member _.``REQ-CF-6.11 for each of a journal entry line ID and a staged line ID that names no stored line, a CreatePayment payload pointing at it is rejected with a typed error and no Payment is stored`` (pointer:string) =
        Assert.Fail "not implemented"
