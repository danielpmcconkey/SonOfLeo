module Tests.Integrated.CrossDomainOrchestration.AgreementCreation

open Tests.Helpers
open Xunit

[<Collection("SharedTestData")>]
type AgreementCreationTests(fixture: TestDataFixture) =

    // =========================================================================
    // REQ-CF-2.20 through 2.24, REQ-SYS-5.1 — a created agreement reads back as created
    // =========================================================================

    [<Fact>]
    member _.``REQ-CF-2.20 REQ-CF-2.21 REQ-CF-2.22 REQ-CF-2.24 REQ-SYS-5.1 an agreement created with no end date and no memo reads back with its start and next-instance dates unchanged and no end date or memo`` () =
        Assert.Fail "not implemented"

    [<Fact>]
    member _.``REQ-CF-2.20 REQ-CF-2.21 REQ-CF-2.22 REQ-CF-2.24 REQ-SYS-5.1 an agreement created with an end date and a memo reads back with start date, next-instance date, end date and memo exactly as created`` () =
        Assert.Fail "not implemented"
