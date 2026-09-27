module Tests.Integrated.CrossDomainOrchestration.MaintenanceOperations

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type MaintenanceOperationsTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-14.1 a CreateAgreement payload stores the Master Agreement and every Payment Agreement, each Payment Agreement with the accounts whose codes it gave`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.1 a CreateAgreement payload whose second Payment Agreement gives an account code that doesn't exist is rejected with a typed error and nothing is written, not even the first Payment Agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.3 FetchAgreementSummary by name returns the agreement with every one of its Payment Agreements, Instances, Invoices and Payments, and nothing of another agreement's`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.4 a CreateInstance payload stores the Instance on the named Master Agreement with every Invoice it carries and every Payment each Invoice carries`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.4 a CreateInstance payload carrying no Invoices stores the Instance with no Invoices`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.4 a CreateInstance payload whose second Invoice's Payments exceed its amount is rejected and nothing is written, not even the Instance or the first Invoice`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 a CreateInvoice payload stores the Invoice on the named existing Instance with every Payment it carries`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 a CreateInvoice payload carrying no Payments stores the Invoice with no Payments`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 a CreateInvoice payload whose Payments exceed its amount is rejected and nothing is written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 REQ-CF-9.3 a CreatePayment payload, valid on its own, that would make a blocked Invoice FullyPaid is rejected and nothing is written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 an UpdateInvoice payload changing the external invoice ID, invoice date, due date, amount, invoice state, blocker and memo stores every one of the new values`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 an UpdateInvoice payload that changes the memo and sets the amount below its Payments' sum is rejected and the Invoice is unchanged, memo included`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 a CreatePayment payload stores the Payment on the named existing Invoice`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.5 a CreatePayment payload that would take the Invoice's Payments past its amount is rejected and nothing is written`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.6 DeletePayment on the only Payment referencing a line deletes the Payment and the Payment Agreement Link that produced it, and the line is a linkage candidate again`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.6 DeletePayment on one of two Payments referencing the same line deletes only that Payment, keeps the Payment Agreement Link, and the line is not a linkage candidate`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-14.6 a DeletePayment payload whose Payment ID names no Payment is rejected with a typed error naming the ID and no Payment or Payment Agreement Link is deleted`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("FetchAgreementSummary")>]
    [<InlineData("CreateInstance")>]
    [<InlineData("UpdateAgreement")>]
    member _.``REQ-CF-14.7 for every route that looks up a Master Agreement by name (FetchAgreementSummary, CreateInstance, UpdateAgreement), a name that matches nothing fails with a typed error naming it`` (route:string) =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("CreateInstance")>]
    [<InlineData("CreateInvoice")>]
    member _.``REQ-CF-14.7 for every route whose payload names a Payment Agreement (CreateInstance, CreateInvoice), a Payment Agreement name that matches nothing fails with a typed error naming it`` (route:string) =
        Assert.Fail "not implemented"
