module Tests.Isolated.Model.Ledger.AccountComponent


open System
open Model.Audit
open Xunit
open Model.Ledger.Account
open Model.Ledger.AccountComponent
open NodaTime

// =============================================================================
// AccountCode
// =============================================================================


[<Fact>]
let ``REQ-AC-1.2 REQ-SYS-1.2 AccountCode rejects empty input`` () =
    let result = AccountCode.create String.Empty
    Assert.True(Result.isError result)
    

[<Fact>]
let ``REQ-AC-1.2 REQ-SYS-1.2 AccountCode rejects whitespace-only input`` () =
    let result = AccountCode.create "     "
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.3 AccountCode rejects strings exceeding 10 chars`` () =
    let result = AccountCode.create  (String('A', 11))
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.3 AccountCode accepts string at exactly 10 chars`` () =
    let result = AccountCode.create  (String('A', 10))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 AccountCode trims leading and trailing whitespace`` () =
    let trimmed = "1010"
    let result = AccountCode.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail e
    | Ok a -> Assert.Equal(trimmed, (AccountCode.value a))

[<Fact>]
let ``REQ-AC-1.3 REQ-SYS-1.1 AccountCode length check applies post-trim`` () =
    let result = AccountCode.create "   0123456789   "
    Assert.True(Result.isOk result)


// =============================================================================
// AccountName
// =============================================================================

[<Fact>]
let ``REQ-AC-1.7 REQ-SYS-1.2 AccountName rejects empty and whitespace-only input`` () =
    let result = AccountName.create "      "
    Assert.True(Result.isError result)
    
[<Fact>]
let ``REQ-AC-1.8 AccountName rejects strings exceeding 100 chars`` () =
    let result = AccountName.create (String('A', 101))
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.8 AccountName accepts string at exactly 100 chars`` () =
    let result = AccountName.create (String('A', 100))
    Assert.True(Result.isOk result)
    
[<Fact>]
let ``REQ-SYS-1.1 AccountName trims leading and trailing whitespace`` () =
    let trimmed = String('A', 25)
    let result = AccountName.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail e
    | Ok a -> Assert.Equal(trimmed, (AccountName.value a))


// =============================================================================
// AccountType
// =============================================================================

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts all valid type names`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-2.4 AccountType fromString rejects invalid type name`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-1.1 AccountType fromString trims input before matching`` () =
    Assert.Fail "not implemented"

// =============================================================================
// AccountSubtype
// =============================================================================

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts all valid subtype names`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString rejects invalid subtype name`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-1.1 AccountSubtype fromString trims input before matching`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type accepts only Cash FixedAsset Investment subtypes`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type accepts only CurrentLiability LongTermLiability subtypes`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.32 Equity type accepts only null subtype`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type accepts only OperatingRevenue OtherRevenue subtypes`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type accepts only OperatingExpense OtherExpense subtypes`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.28-1.36 type-subtype mismatch is rejected`` () =
    Assert.Fail "not implemented"

// =============================================================================
// AccountExternalReference
// =============================================================================

[<Fact>]
let ``REQ-AC-1.49 REQ-SYS-1.3 AccountExternalReference rejects empty and whitespace-only input`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.20 AccountExternalReference rejects strings exceeding 50 chars`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-SYS-1.1 AccountExternalReference trims leading and trailing whitespace`` () =
    Assert.Fail "not implemented"

// =============================================================================
// AccountActivityPeriod
// =============================================================================

[<Fact>]
let ``REQ-AC-1.46 AccountActivityPeriod rejects activeEnd earlier than activeBegin`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.46 AccountActivityPeriod rejects activeEnd equal to activeBegin`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.45 AccountActivityPeriod accepts null activeEnd`` () =
    Assert.Fail "not implemented"

[<Fact>]
let ``REQ-AC-1.42 REQ-AC-1.43 AccountActivityPeriod accepts valid begin and end`` () =
    Assert.Fail "not implemented"
