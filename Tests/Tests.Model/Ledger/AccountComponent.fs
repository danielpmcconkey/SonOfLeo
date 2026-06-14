module Tests.Model.Ledger.AccountComponent

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
    let result = AccountCode.create "12345678910"
    Assert.True(Result.isError result)

[<Fact>]
let ``REQ-AC-1.3 AccountCode accepts string at exactly 10 chars`` () =
    let result = AccountCode.create "0123456789"
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
