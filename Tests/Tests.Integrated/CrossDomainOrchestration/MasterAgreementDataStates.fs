module Tests.Integrated.CrossDomainOrchestration.MasterAgreementDataStates

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type MasterAgreementDataStatesTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-CF-2.3 a CreateAgreement payload with a null agreement name is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.17 a CreateAgreement payload with a null counterparty is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.8 a CreateAgreement payload naming a cadence other than Daily, Weekly, EveryOtherWeek, Monthly and Annually is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.4 REQ-SYS-1.1 an agreement name with leading and trailing spaces is accepted and stored trimmed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.5 REQ-SYS-1.1 an agreement name of 100 characters padded with spaces on both sides is accepted and stored as the 100 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.18 REQ-SYS-1.1 a counterparty with leading and trailing spaces is accepted and stored trimmed`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.19 REQ-SYS-1.1 a counterparty of 250 characters padded with spaces on both sides is accepted and stored as the 250 characters`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.6 creating an agreement with the name of an existing agreement is rejected with a typed error, no second agreement is stored and the existing one is unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.6 updating an agreement's name to the name of another existing agreement is rejected with a typed error and both stored agreements are unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.9 a CreateAgreement payload with a Weekly cadence and no week day is rejected with a typed error and no agreement is stored, while the same payload with a week day is created`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.9 a CreateAgreement payload with an EveryOtherWeek cadence and no week day is rejected with a typed error and no agreement is stored, while the same payload with a week day is created`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.10 a CreateAgreement payload with a Monthly cadence and no month day is rejected with a typed error and no agreement is stored, while the same payload with a month day is created`` () =
        Assert.Fail "not implemented"

    [<Theory>]
    [<InlineData("DateInMonth")>]
    [<InlineData("NthWeekDay")>]
    [<InlineData("Last")>]
    member _.``REQ-CF-2.10 for each of a date-in-month, an nth weekday and Last, a CreateAgreement payload with a Monthly cadence and that month day is created with that month day`` (monthDay:string) =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.10 a CreateAgreement payload with a Monthly nth-weekday month day that has a week number and no week day is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.11 a CreateAgreement payload with an Annually cadence and no month is rejected with a typed error and no agreement is stored, while the same payload with a month is created`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.11 a CreateAgreement payload with an Annually cadence and no month day is rejected with a typed error and no agreement is stored, while the same payload with a month day is created`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.12 a CreateAgreement payload with a Daily cadence and no week day, month or month day creates the agreement with a Daily cadence`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.23 after an EveryOtherWeek agreement whose start date is on the off week of its next-instance date gets two successive Instances, its next-instance dates are exactly 14 and then 28 days after the original`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.25 creating an agreement whose next-instance date does not fit its cadence is rejected with a typed error and no agreement is stored`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.25 updating an agreement's next-instance date to one that does not fit its cadence is rejected with a typed error and the stored agreement is unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.26 creating an agreement whose start date is after today stores it with that start date`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.26 updating an agreement whose start date is after today succeeds and stores the change`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.26 creating an agreement whose end date is yesterday is rejected with a typed error and no agreement is stored, while one whose end date is today is created`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.26 updating an agreement's end date to yesterday is rejected with a typed error and its stored end date is unchanged, while updating it to today succeeds`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.26 updating a field other than the end date of an agreement whose stored end date is before today is rejected with a typed error and the agreement is unchanged`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.27 creating an agreement with no Payment Agreements is rejected with a typed error and no agreement is stored, while the same agreement with exactly one Payment Agreement is created`` () =
        Assert.Fail "not implemented"
