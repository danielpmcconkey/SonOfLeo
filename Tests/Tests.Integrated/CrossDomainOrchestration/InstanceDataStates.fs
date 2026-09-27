module Tests.Integrated.CrossDomainOrchestration.InstanceDataStates

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type InstanceDataStatesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-4.3 creating an Instance for a Master Agreement ID that names no stored agreement is rejected with a typed error and nothing is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.3 with two agreements stored, an Instance created for one of them is stored referencing that agreement's ID and not the other's`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.5 an Instance created by CreateInstance or by the sweep, with no is-fulfilled value supplied, is stored with is-fulfilled false`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("Weekly")>]
    [<InlineData("EveryOtherWeek")>]
    [<InlineData("MonthlyDateInMonth")>]
    [<InlineData("MonthlyNthWeekDay")>]
    [<InlineData("MonthlyLast")>]
    [<InlineData("Annually")>]
    member _.``REQ-CF-4.6 for every cadence rule but Daily, creating an Instance on a date that does not fit its agreement's cadence is rejected with a typed error naming the date and the rule, and nothing is stored`` (cadence:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.6 creating an Instance on a date that fits its agreement's cadence but is later than its next-instance date is stored with that date`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData(0)>]
    [<InlineData(-1)>]
    member _.``REQ-CF-4.7 for each of the latest existing Instance's date and a cadence date before it, creating an Instance on that date is rejected with a typed error naming the latest existing date, and nothing is stored`` (offsetWeeks:int) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.7 creating an Instance on the first cadence date after the latest existing Instance of the same agreement is stored with that date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.7 creating an Instance on a date earlier than the latest Instance of a different agreement, but later than every Instance of its own, is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.7 the sweep creates no Instance on a date earlier than or equal to the latest existing Instance of the agreement`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.9 an Instance composite update that sets is-fulfilled to true on an Instance with no Invoices is rejected with a typed error and the stored flag stays false`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.9 an Instance composite update that sets is-fulfilled to true on an Instance with at least one Invoice not FullyPaid is rejected with a typed error and the stored flag stays false`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.9 REQ-CF-9.10 an Instance composite update that sets is-fulfilled to false on an Instance whose every Invoice is FullyPaid is rejected with a typed error and the stored flag stays true`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.10 creating an Instance with two Invoices for the same Payment Agreement is rejected with a typed error and nothing is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.10 adding an Invoice for a Payment Agreement that already has an Invoice on the Instance is rejected with a typed error and the Instance's Invoices are unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.10 creating an Instance with an Invoice for a Payment Agreement of a different agreement is rejected with a typed error and nothing is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.10 adding an Invoice for a Payment Agreement of a different agreement to an existing Instance is rejected with a typed error and the Instance's Invoices are unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-4.10 an Instance holding one Invoice for each of several Payment Agreements of its own agreement is stored with every one of those Invoices`` () =
        Assert.Fail "not implemented"
