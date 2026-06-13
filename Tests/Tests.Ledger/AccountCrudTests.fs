module Tests.Ledger.AccountCrudTests

open Xunit
open Model.Ledger.Account
open NodaTime

// =============================================================================
// Create + Read round-trips
// =============================================================================

[<Fact>]
let ``REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-3.4 fetch by code returns correct account`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-3.5 fetch by parent ID returns all children`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-3.6 fetch by account type returns matching accounts`` () =
    Assert.Fail "not implemented"

// =============================================================================
// Create validations (DB-dependent)
// =============================================================================

[<Fact>]
let ``REQ-AC-1.4 REQ-AC-2.9 duplicate account code is rejected`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-2.6 parent ID must reference existing account`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-2.7 parent account must be active at AuditEnvelope instant`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-2.19 child AccountType must match parent AccountType`` () =
    Assert.Fail "not implemented"

// =============================================================================
// Deactivation
// =============================================================================

[<Fact>]
let ``REQ-AC-4.1 deactivateAccount sets active end and returns inactive account`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.2 deactivateAccount rejects end earlier than or equal to begin`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.3 deactivateAccount rejects when active children exist`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.5 deactivateAccount rejects already deactivated account`` () =
    Assert.Fail "not implemented"

// =============================================================================
// Updates
// =============================================================================

[<Fact>]
let ``REQ-AC-4.8 updateAccountName succeeds with valid name`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.8 REQ-SYS-2.1 updateAccountName rejects invalid name`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.9 updateExternalReference succeeds with valid reference`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.9 updateExternalReference can clear to None`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-3.3 update operations set modifiedAt from AuditEnvelope`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-4.19 update to deactivated account is permitted`` () =
    Assert.Fail "not implemented"
