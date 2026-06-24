module Tests.Isolated.Model.Ledger.AccountComponent

open System
open Xunit
open Model.Ledger.Accounts.AccountComponent
open Utilities

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
    let result = AccountCode.create (String('A', 11))
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.3 AccountCode accepts string at exactly 10 chars`` () =
    let result = AccountCode.create (String('A', 10))
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
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Asset`` () =
    Assert.True(Result.isOk (AccountType.fromString "Asset"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Liability`` () =
    Assert.True(Result.isOk (AccountType.fromString "Liability"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Equity`` () =
    Assert.True(Result.isOk (AccountType.fromString "Equity"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Revenue`` () =
    Assert.True(Result.isOk (AccountType.fromString "Revenue"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Expense`` () =
    Assert.True(Result.isOk (AccountType.fromString "Expense"))

[<Fact>]
let ``REQ-AC-2.4 AccountType fromString rejects invalid type name`` () =
    Assert.True(Result.isError (AccountType.fromString "Valley Girl"))

[<Fact>]
let ``REQ-SYS-1.1 AccountType fromString trims input before matching`` () =
    let trimmed = AccountType.fromString "Revenue"
    let untrimmed = AccountType.fromString " Revenue   "
    Assert.Equal(trimmed, untrimmed)

// =============================================================================
// AccountSubtype
// =============================================================================

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts Cash`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "Cash"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts CurrentLiability`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "CurrentLiability"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts FixedAsset`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "FixedAsset"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts Investment`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "Investment"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts LongTermLiability`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "LongTermLiability"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OperatingExpense`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "OperatingExpense"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OperatingRevenue`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "OperatingRevenue"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OtherRevenue`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "OtherRevenue"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OtherExpense`` () =
    Assert.True(Result.isOk (AccountSubtype.fromString "OtherExpense"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString rejects invalid subtype name`` () =
    Assert.True(Result.isError (AccountSubtype.fromString "Ladies' lingerie"))

[<Fact>]
let ``REQ-SYS-1.1 AccountSubtype fromString trims input before matching`` () =
    let trimmed = AccountSubtype.fromString "OtherExpense"
    let untrimmed = AccountSubtype.fromString " OtherExpense   "
    Assert.Equal(trimmed, untrimmed)

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type can be matched with Cash subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Cash")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with CurrentLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "CurrentLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type can be matched with FixedAsset subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "FixedAsset")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type can be matched with Investment subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Investment")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with LongTermLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "LongTermLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OperatingExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OperatingRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OtherRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OtherExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with Cash subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Cash")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type can be matched with CurrentLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "CurrentLiability")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with FixedAsset subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "FixedAsset")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with Investment subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Investment")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type can be matched with LongTermLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "LongTermLiability")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OperatingExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OperatingRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OtherRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OtherExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with Cash subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Cash")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with CurrentLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "CurrentLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with FixedAsset subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "FixedAsset")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with Investment subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Investment")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with LongTermLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "LongTermLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OperatingExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OperatingRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OtherRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OtherExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with Cash subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Cash")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with CurrentLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "CurrentLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with FixedAsset subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "FixedAsset")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with Investment subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Investment")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with LongTermLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "LongTermLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with OperatingExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type can be matched with OperatingRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingRevenue")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type can be matched with OtherRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherRevenue")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with OtherExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherExpense")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with Cash subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Cash")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with CurrentLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "CurrentLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with FixedAsset subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "FixedAsset")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with Investment subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "Investment")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with LongTermLiability subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "LongTermLiability")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type can be matched with OperatingExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingExpense")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with OperatingRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OperatingRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with OtherRevenue subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherRevenue")
         Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type can be matched with OtherExpense subtypes`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = Result.defaultWith failwith (AccountSubtype.fromString "OtherExpense")
         Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.19.1 Asset type can be matched with a subtype of null`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Asset")
         let st = None
         Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19.1 Liability type can be matched with a subtype of null`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Liability")
         let st = None
         Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19.1 Equity type can be matched with a subtype of null`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Equity")
         let st = None
         Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19.1 Revenue type can be matched with a subtype of null`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Revenue")
         let st = None
         Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19.1 Expense type can be matched with a subtype of null`` () =
         let t = Result.defaultWith failwith (AccountType.fromString "Expense")
         let st = None
         Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

// =============================================================================
// AccountExternalReference
// =============================================================================

[<Fact>]
let ``REQ-AC-1.49 REQ-SYS-1.3 AccountExternalReference rejects empty input`` () =
    let result = AccountExternalReference.create String.Empty
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.49 REQ-SYS-1.3 AccountExternalReference rejects whitespace-only input`` () =
    let result = AccountExternalReference.create "     "
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.20 AccountExternalReference rejects strings exceeding 50 chars`` () =
    let result = AccountExternalReference.create  (String('A', 51))
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.20 AccountExternalReference allows strings of exactly 50 chars`` () =
    let result = AccountExternalReference.create  (String('A', 50))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 AccountExternalReference trims leading and trailing whitespace`` () =
    let trimmed = "Sufferin' Succotash"
    let result = AccountExternalReference.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail e
    | Ok a -> Assert.Equal(trimmed, (AccountExternalReference.value a))

// =============================================================================
// AccountActivityPeriod
// =============================================================================

[<Fact>]
let ``REQ-AC-1.46 AccountActivityPeriod rejects activeEnd earlier than activeBegin`` () =
    let ab = Calendar.today()
    let ae = Some (ab.PlusDays(-1))
    AccountActivityPeriod.create ab ae |> Result.isError |> Assert.True

[<Fact>]
let ``REQ-AC-1.46 AccountActivityPeriod accepts activeEnd equal to activeBegin`` () =
    let ab = Calendar.today()
    let ae = Some ab
    AccountActivityPeriod.create ab ae |> Result.isOk |> Assert.True

[<Fact>]
let ``REQ-AC-1.45 AccountActivityPeriod accepts null activeEnd`` () =
    let ab = Calendar.today()
    let ae = None
    AccountActivityPeriod.create ab ae |> Result.isOk |> Assert.True

[<Fact>]
let ``REQ-AC-1.42 REQ-AC-1.43 AccountActivityPeriod accepts valid begin and end`` () =
    let ab = Calendar.today()
    let ae = Some (ab.PlusDays(1))
    AccountActivityPeriod.create ab ae |> Result.isOk |> Assert.True
