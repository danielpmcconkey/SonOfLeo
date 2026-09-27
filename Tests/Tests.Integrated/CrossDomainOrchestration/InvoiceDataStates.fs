module Tests.Integrated.CrossDomainOrchestration.InvoiceDataStates

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InvoiceDataStatesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-5.2 two Invoices created through CreateInvoice carry distinct, non-empty Invoice IDs`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.3 a CreateInvoice payload whose Instance ID names no stored Instance is rejected with a typed error and no Invoice is stored anywhere for that Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.3 adding an Invoice at the model level whose Instance ID names no stored Instance is rejected with a typed error and no Invoice is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.3 REQ-CF-5.4 an Invoice created through CreateInvoice is stored referencing the Instance and the Payment Agreement it named`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.4 a CreateInvoice payload naming a Payment Agreement that does not exist is rejected with a typed error and no Invoice is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.4 adding an Invoice at the model level whose Payment Agreement ID names no stored Payment Agreement is rejected with a typed error and no Invoice is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.5 a CreateInvoice payload naming a Payment Agreement that belongs to a different Master Agreement than the Instance's is rejected with a typed error and no Invoice is stored`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Missing")>]
    [<InlineData("Foreign")>]
    member _.``REQ-CF-5.4 REQ-CF-5.5 for each of a Payment Agreement that does not exist and one belonging to a different Master Agreement, a CreateInstance payload carrying an Invoice naming it is rejected with a typed error and no Instance is stored`` (paymentAgreement:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("0.00")>]
    [<InlineData("-0.01")>]
    member _.``REQ-CF-5.6 for each of null, zero and minus one cent, a CreateInvoice payload with that amount is rejected with a typed error and no Invoice is stored`` (amount:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.6 a CreateInvoice payload with an amount of three decimal places is rejected with a typed error and no Invoice is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.6 a CreateInvoice payload with an amount of one cent is stored with that amount`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(null)>]
    [<InlineData("0.00")>]
    [<InlineData("-0.01")>]
    member _.``REQ-CF-5.6 for each of null, zero and minus one cent, an UpdateInvoice payload setting the amount to that value is rejected with a typed error and the Invoice is unchanged`` (amount:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("invoiceDate")>]
    [<InlineData("dueDate")>]
    member _.``REQ-CF-5.7 REQ-CF-5.8 for each of the invoice date and the due date, a CreateInvoice payload with that date null is rejected with a typed error and no Invoice is stored`` (field:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.7 REQ-CF-5.8 a CreateInvoice payload with different invoice and due dates is stored with each date in its own field exactly as given`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("invoiceDate")>]
    [<InlineData("dueDate")>]
    member _.``REQ-CF-5.7 REQ-CF-5.8 for each of the invoice date and the due date, an UpdateInvoice payload setting that date to null is rejected with a typed error and the Invoice is unchanged`` (field:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("Paid")>]
    [<InlineData("invoicereceived")>]
    member _.``REQ-CF-5.9 for each of the empty string, 'Paid' and 'invoicereceived', a CreateInvoice payload with that invoice state is rejected with a typed error and no Invoice is stored`` (state:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("InvoiceGenerated")>]
    [<InlineData("InvoiceSent")>]
    [<InlineData("InvoiceExpected")>]
    [<InlineData("InvoiceReceived")>]
    member _.``REQ-CF-5.9 for each of the four invoice states, a CreateInvoice payload with that state on an agreement of the direction it suits is stored with that state`` (state:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("Paid")>]
    [<InlineData("invoicereceived")>]
    member _.``REQ-CF-5.9 for each of the empty string, 'Paid' and 'invoicereceived', an UpdateInvoice payload setting that invoice state is rejected with a typed error and the Invoice is unchanged`` (state:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.13 a CreateInvoice payload with no blocker is stored with no blocker`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("NoFunds")>]
    [<InlineData("Irresponsible")>]
    [<InlineData("NeedsDecision")>]
    [<InlineData("Other")>]
    member _.``REQ-CF-5.13 for each of NoFunds, Irresponsible, NeedsDecision with a note and Other with a note, a CreateInvoice payload with that blocker is stored with that blocker and note`` (blocker:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData("Broke")>]
    [<InlineData("nofunds")>]
    member _.``REQ-CF-5.13 for each of the empty string, 'Broke' and 'nofunds', a CreateInvoice payload with that blocker state is rejected with a typed error and no Invoice is stored`` (blocker:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("NeedsDecision", null)>]
    [<InlineData("NeedsDecision", "")>]
    [<InlineData("NeedsDecision", " \t ")>]
    [<InlineData("Other", null)>]
    [<InlineData("Other", "")>]
    [<InlineData("Other", " \t ")>]
    member _.``REQ-CF-5.14 for each of NeedsDecision and Other, a CreateInvoice payload giving that blocker with no note, an empty note, or a whitespace-only note is rejected with a typed error and no Invoice is stored`` (blocker:string, note:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("NeedsDecision")>]
    [<InlineData("Other")>]
    member _.``REQ-CF-5.14 for each of NeedsDecision and Other, a CreateInvoice payload giving that blocker with a 501-character note is rejected with a typed error and no Invoice is stored, while a 500-character note is stored`` (blocker:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("NoFunds")>]
    [<InlineData("Irresponsible")>]
    member _.``REQ-CF-5.14 for each of NoFunds and Irresponsible, a CreateInvoice payload giving that blocker with a note attached is rejected with a typed error and no Invoice is stored`` (blocker:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.14 an UpdateInvoice payload setting the blocker to NeedsDecision without a note is rejected with a typed error and the Invoice is unchanged`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("NeedsDecision")>]
    [<InlineData("Other")>]
    member _.``REQ-CF-5.14 for each of NeedsDecision and Other, an UpdateInvoice payload clearing the blocker of an Invoice holding that blocker with a note is accepted, and the Invoice read back from the store has no blocker and no note`` (blocker:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("NoFunds")>]
    [<InlineData("Irresponsible")>]
    member _.``REQ-CF-5.14 for each of NoFunds and Irresponsible, an UpdateInvoice payload setting that blocker with a note attached is rejected with a typed error and the Invoice is unchanged`` (blocker:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.15 a CreateInvoice payload with no memo is stored with no memo`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-5.15 for each of the empty string, a single space and a string of spaces and tabs, a CreateInvoice payload with that memo is rejected with a typed error and no Invoice is stored`` (memo:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.15 a CreateInvoice payload with a 2001-character memo is rejected with a typed error and no Invoice is stored, while a 2000-character memo is stored`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("")>]
    [<InlineData(" ")>]
    [<InlineData(" \t  ")>]
    member _.``REQ-CF-5.15 for each of the empty string, a single space and a string of spaces and tabs, an UpdateInvoice payload setting that memo is rejected with a typed error and the Invoice is unchanged`` (memo:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.16 a CreateInvoice payload for a Payment Agreement that already has an Invoice on the Instance is rejected with a typed error and the Instance still has exactly its first Invoice for that Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.16 a CreateInvoice payload for a Payment Agreement that has an Invoice on another Instance of its Master Agreement is stored, and both Instances each read back exactly one Invoice for that Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-5.16 a CreateInstance payload carrying two Invoices for the same Payment Agreement is rejected with a typed error and no Instance is stored`` () =
        Assert.Fail "not implemented"
