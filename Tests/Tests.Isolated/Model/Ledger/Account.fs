module Tests.Isolated.Model.Ledger.Account


open Xunit
open Model.Ledger.Account
open NodaTime

// =============================================================================
// constructNew
// =============================================================================

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID and sets timestamps from AuditEnvelope`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-2.1 constructNew rejects invalid account code`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-2.1 constructNew rejects invalid type-subtype combination`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-2.18 constructNew rejects activeEnd not later than activeBegin`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-2.10 constructNew rejects invalid subtype string`` () =
    Assert.Fail "not implemented"

// =============================================================================
// reconstitute
// =============================================================================

[<Fact>]
let ``REQ-SYS-2.1 reconstitute validates all fields on read-from-persistence`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-2.1 reconstitute rejects invalid data state`` () =
    Assert.Fail "not implemented"

// =============================================================================
// isActive
// =============================================================================

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when begin <= ref and no end`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when begin <= ref and end > ref`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.48 isActive returns false when end <= ref (deactivated)`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.50 isActive returns false when ref precedes begin (not yet started)`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.50 isActive boundary - ref exactly equals begin is active`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.48 isActive boundary - ref exactly equals end is inactive`` () =
    Assert.Fail "not implemented"
