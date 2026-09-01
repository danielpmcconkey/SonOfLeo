module Tests.Isolated.Model.Ledger.AccountComponent

open System
open Model
open Xunit
open Model.Ledger.AccountComponent
open Utilities
open Utilities.AppError
open Tests.Helpers.SadPath
open Tests.Helpers.Railroad

// =============================================================================
// AccountCode
// =============================================================================

[<Fact>]
let ``REQ-AC-1.2 REQ-SYS-1.2 AccountCode rejects empty input`` () =
    isCorrectError (AccountCode.create String.Empty) AccountCodeIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.2 REQ-SYS-1.2 AccountCode rejects whitespace-only input`` () =
    isCorrectError (AccountCode.create "     ") AccountCodeIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.3 AccountCode rejects strings exceeding 10 chars`` () =
    isCorrectError (AccountCode.create(String('A', 11))) AccountCodeTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.3 AccountCode accepts string at exactly 10 chars`` () =
    let result = AccountCode.create(String('A', 10))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 AccountCode trims leading and trailing whitespace`` () =
    let trimmed = "1010"
    let result = AccountCode.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
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
    isCorrectError (AccountName.create "      ") AccountNameIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.8 AccountName rejects strings exceeding 100 chars`` () =
    isCorrectError (AccountName.create(String('A', 101))) AccountNameTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.8 AccountName accepts string at exactly 100 chars`` () =
    let result = AccountName.create(String('A', 100))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 AccountName trims leading and trailing whitespace`` () =
    let trimmed = String('A', 25)
    let result = AccountName.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok a -> Assert.Equal(trimmed, (AccountName.value a))
// =============================================================================
// AccountType
// =============================================================================

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Asset`` () =
    Assert.True(Result.isOk(AccountType.fromString "Asset"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Liability`` () =
    Assert.True(Result.isOk(AccountType.fromString "Liability"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Equity`` () =
    Assert.True(Result.isOk(AccountType.fromString "Equity"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Revenue`` () =
    Assert.True(Result.isOk(AccountType.fromString "Revenue"))

[<Fact>]
let ``REQ-AC-2.4 REQ-AC-1.10 AccountType fromString accepts Expense`` () =
    Assert.True(Result.isOk(AccountType.fromString "Expense"))

[<Fact>]
let ``REQ-AC-2.4 AccountType fromString rejects invalid type name`` () =
    isCorrectError (AccountType.fromString "Valley Girl") AccountTypeInvalid None
    |> railroadWrapper

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
    Assert.True(Result.isOk(AccountSubtype.fromString "Cash"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts CurrentLiability`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "CurrentLiability"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts FixedAsset`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "FixedAsset"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts Investment`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "Investment"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts LongTermLiability`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "LongTermLiability"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OperatingExpense`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "OperatingExpense"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OperatingRevenue`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "OperatingRevenue"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OtherRevenue`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "OtherRevenue"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString accepts OtherExpense`` () =
    Assert.True(Result.isOk(AccountSubtype.fromString "OtherExpense"))

[<Fact>]
let ``REQ-AC-1.18 AccountSubtype fromString rejects invalid subtype name`` () =
    isCorrectError (AccountSubtype.fromString "Ladies' lingerie") AccountSubtypeInvalid None
    |> railroadWrapper

[<Fact>]
let ``REQ-SYS-1.1 AccountSubtype fromString trims input before matching`` () =
    let trimmed = AccountSubtype.fromString "OtherExpense"
    let untrimmed = AccountSubtype.fromString " OtherExpense   "
    Assert.Equal(trimmed, untrimmed)

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type can be matched with Cash subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Cash")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with CurrentLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "CurrentLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type can be matched with FixedAsset subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "FixedAsset")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type can be matched with Investment subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Investment")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with LongTermLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "LongTermLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OperatingExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OperatingRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OtherRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.28 REQ-AC-1.29 Asset type cannot be matched with OtherExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with Cash subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Cash")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type can be matched with CurrentLiability subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "CurrentLiability")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with FixedAsset subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "FixedAsset")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with Investment subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Investment")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type can be matched with LongTermLiability subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "LongTermLiability")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OperatingExpense subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OperatingRevenue subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OtherRevenue subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.30 REQ-AC-1.31 Liability type cannot be matched with OtherExpense subtypes`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with Cash subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Cash")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with CurrentLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "CurrentLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with FixedAsset subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "FixedAsset")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with Investment subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Investment")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with LongTermLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "LongTermLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OperatingExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OperatingRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OtherRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.32 Equity type cannot be matched with OtherExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with Cash subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Cash")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with CurrentLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "CurrentLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with FixedAsset subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "FixedAsset")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with Investment subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Investment")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with LongTermLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "LongTermLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with OperatingExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type can be matched with OperatingRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingRevenue")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type can be matched with OtherRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherRevenue")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.33 REQ-AC-1.34 Revenue type cannot be matched with OtherExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherExpense")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with Cash subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Cash")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with CurrentLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "CurrentLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with FixedAsset subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "FixedAsset")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with Investment subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "Investment")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with LongTermLiability subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "LongTermLiability")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type can be matched with OperatingExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingExpense")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with OperatingRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OperatingRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type cannot be matched with OtherRevenue subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherRevenue")
    Assert.False(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.35 REQ-AC-1.36 Expense type can be matched with OtherExpense subtypes`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountSubtype.fromString "OtherExpense")
    Assert.True(AccountSubtype.validTypeSubtypeCombination t (Some st))

[<Fact>]
let ``REQ-AC-1.19 Asset type can be matched with a subtype of null`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Asset")
    let st = None
    Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19 Liability type can be matched with a subtype of null`` () =
    let t =
        Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Liability")
    let st = None
    Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19 Equity type can be matched with a subtype of null`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Equity")
    let st = None
    Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19 Revenue type can be matched with a subtype of null`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Revenue")
    let st = None
    Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

[<Fact>]
let ``REQ-AC-1.19 Expense type can be matched with a subtype of null`` () =
    let t = Result.defaultWith (fun e -> failwith(AppError.toMessage e)) (AccountType.fromString "Expense")
    let st = None
    Assert.True(AccountSubtype.validTypeSubtypeCombination t st)

// =============================================================================
// AccountExternalReference
// =============================================================================

[<Fact>]
let ``REQ-AC-1.49 REQ-SYS-1.3 AccountExternalReference rejects empty input`` () =
    isCorrectError (AccountExternalReference.create String.Empty) AccountExternalReferenceIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.49 REQ-SYS-1.3 AccountExternalReference rejects whitespace-only input`` () =
    isCorrectError (AccountExternalReference.create "     ") AccountExternalReferenceIsEmpty None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.20 AccountExternalReference rejects strings exceeding 50 chars`` () =
    isCorrectError (AccountExternalReference.create(String('A', 51))) AccountExternalReferenceTooLong None
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.20 AccountExternalReference allows strings of exactly 50 chars`` () =
    let result = AccountExternalReference.create(String('A', 50))
    Assert.True(Result.isOk result)

[<Fact>]
let ``REQ-SYS-1.1 AccountExternalReference trims leading and trailing whitespace`` () =
    let trimmed = "Sufferin' Succotash"
    let result = AccountExternalReference.create $"  {trimmed}   "
    match result with
    | Error e -> Assert.Fail(AppError.toMessage e)
    | Ok a -> Assert.Equal(trimmed, (AccountExternalReference.value a))

// =============================================================================
// ActivityPeriod
// =============================================================================

[<Fact>]
let ``REQ-AC-1.46 ActivityPeriod rejects activeEnd earlier than activeBegin`` () =
    let ab = Calendar.today()
    let ae = Some(ab.PlusDays(-1))
    isCorrectError (ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate)
        ActiveEndBeforeBegin None 
    |> railroadWrapper

[<Fact>]
let ``REQ-AC-1.46 ActivityPeriod accepts activeEnd equal to activeBegin`` () =
    let ab = Calendar.today()
    let ae = Some ab
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate |> Result.isOk |> Assert.True

[<Fact>]
let ``REQ-AC-1.45 ActivityPeriod accepts null activeEnd`` () =
    let ab = Calendar.today()
    let ae = None
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate |> Result.isOk |> Assert.True

[<Fact>]
let ``REQ-AC-1.42 REQ-AC-1.43 ActivityPeriod accepts valid begin and end`` () =
    let ab = Calendar.today()
    let ae = Some(ab.PlusDays(1))
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate |> Result.isOk |> Assert.True

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when begin <= ref and no end`` () =
    let ab = Calendar.today().PlusDays(-1)
    let ae = None
    let now = Calendar.today()
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    |> ActivityPeriod.isActive now
    |> Assert.True

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when begin <= ref and end > ref`` () =
    let ab = Calendar.today().PlusDays(-1)
    let ae = Some(Calendar.today().PlusDays(1))
    let now = Calendar.today()
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    |> ActivityPeriod.isActive now
    |> Assert.True

[<Fact>]

let ``REQ-AC-1.48 isActive returns false when end < ref (deactivated)`` () =
    let ab = Calendar.today().PlusDays(-2)
    let ae = Some(Calendar.today().PlusDays(-1))
    let now = Calendar.today()
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    |> ActivityPeriod.isActive now
    |> Assert.False

[<Fact>]
let ``REQ-AC-1.50 isActive returns false when ref precedes begin (not yet started)`` () =
    let ab = Calendar.today().PlusDays(1)
    let ae = None
    let now = Calendar.today()
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    |> ActivityPeriod.isActive now
    |> Assert.False

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when the reference point exactly equals begin`` () =
    let ab = Calendar.today()
    let ae = None
    let now = ab
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    |> ActivityPeriod.isActive now
    |> Assert.True

[<Fact>]
let ``REQ-AC-1.48 isActive returns true when the reference point exactly equals end`` () =
    let ab = Calendar.today().PlusDays(-1)
    let now = Calendar.today()
    let ae = Some now
    ActivityPeriod.create ab ae ActivityPeriod.NotConsideredAvailableBeforeBeginDate
    |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
    |> ActivityPeriod.isActive now
    |> Assert.True
