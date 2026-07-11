module Tests.Isolated.Model.Ledger.Account

open System
open Model.Audit
open Xunit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Utilities

let genericCode = "GenCode"
let genericName = "Gen account name"
let genericAccountTypePrimitive = "Revenue"
let genericAccountType = AccountType.fromString genericAccountTypePrimitive |> Result.defaultWith failwith
let genericActiveBegin = Calendar.today().PlusYears(-1)
let genericActiveEnd = None
let genericSubtype = None
let genericSubtypeText = "Cash"
let genericSubtypeNonNull = AccountSubtype.fromString genericSubtypeText |> Result.defaultWith failwith
let genericParentId = None
let genericReference= None
let genericEnvelope = AuditEnvelope.create AccountCreate

// =============================================================================
// constructNew
// =============================================================================

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID`` () =
    Account.constructNew genericCode genericName genericAccountTypePrimitive genericActiveBegin
        genericActiveEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.accountId
    |> AccountId.value
    |> fun id -> Assert.NotEqual(Guid.Empty, id)

[<Fact>]
let ``REQ-AC-2.13 REQ-SYS-3.2 constructNew sets timestamps from AuditEnvelope`` () =
    let expected = AuditEnvelope.instant genericEnvelope
    let account =
        Account.constructNew genericCode genericName genericAccountTypePrimitive genericActiveBegin
            genericActiveEnd genericSubtype genericParentId genericReference genericEnvelope
        |> Result.defaultWith failwith
    Assert.Equal(expected, Account.createdAt account)
    Assert.Equal(expected, Account.modifiedAt account)

[<Fact>]
let ``REQ-SYS-2.1 constructNew rejects invalid account code`` () =
    let badCode = String('A', 100)
    Account.constructNew badCode genericName genericAccountTypePrimitive genericActiveBegin
        genericActiveEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.isError |> Assert.True

[<Fact>]
let ``REQ-SYS-2.1 constructNew rejects invalid type-subtype combination`` () =
    let badSubtype = Some "OperatingExpense"
    Account.constructNew genericCode genericName genericAccountTypePrimitive genericActiveBegin
        genericActiveEnd badSubtype genericParentId genericReference genericEnvelope
    |> Result.isError |> Assert.True

[<Fact>]
let ``REQ-AC-2.18 constructNew rejects activeEnd earlier than activeBegin`` () =
    let badEnd = Some(genericActiveBegin.PlusDays(-1))
    Account.constructNew genericCode genericName genericAccountTypePrimitive genericActiveBegin
        badEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.isError |> Assert.True

[<Fact>]
let ``REQ-AC-2.18 constructNew accepts activeEnd equal to activeBegin`` () =
    let goodEnd = Some genericActiveBegin
    Account.constructNew genericCode genericName genericAccountTypePrimitive genericActiveBegin
        goodEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.isOk |> Assert.True

[<Fact>]
let ``REQ-AC-2.10 constructNew rejects invalid subtype string`` () =
    let badSubtype = Some "Halifax, Nova Scotia"
    Account.constructNew genericCode genericName genericAccountTypePrimitive genericActiveBegin
        genericActiveEnd badSubtype genericParentId genericReference genericEnvelope
    |> Result.isError |> Assert.True

// =============================================================================
// reconstitute
// =============================================================================

(*
 * Note: skipping these. There is no separate reconstitution logic. It takes
 * primitives from the database (already validated) and runs them through the
 * full create omni stack, using domain types whose validation has been
 * elaborated upon elsewhere in this solution. 
 *)

// =============================================================================
// isActive
// =============================================================================

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when begin <= ref and no end`` () =
    let explicitBegin = Calendar.today().PlusDays(-1)
    let explicitEnd = None
    Account.constructNew genericCode genericName genericAccountTypePrimitive explicitBegin
        explicitEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.isActive ((AuditEnvelope.instant genericEnvelope) |> Calendar.dateFromInstant)
    |> Assert.True

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when begin <= ref and end > ref`` () =
    let explicitBegin = Calendar.today().PlusDays(-1)
    let explicitEnd = Some (Calendar.today().PlusDays(1))
    Account.constructNew genericCode genericName genericAccountTypePrimitive explicitBegin
        explicitEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.isActive ((AuditEnvelope.instant genericEnvelope) |> Calendar.dateFromInstant)
    |> Assert.True

[<Fact>]
let ``REQ-AC-1.48 isActive returns false when end < ref (deactivated)`` () =
    let explicitBegin = Calendar.today().PlusDays(-2)
    let explicitEnd = Some (Calendar.today().PlusDays(-1))
    Account.constructNew genericCode genericName genericAccountTypePrimitive explicitBegin
        explicitEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.isActive ((AuditEnvelope.instant genericEnvelope) |> Calendar.dateFromInstant)
    |> Assert.False

[<Fact>]
let ``REQ-AC-1.50 isActive returns false when ref precedes begin (not yet started)`` () =
    let explicitBegin = Calendar.today().PlusDays(1)
    let explicitEnd = None
    Account.constructNew genericCode genericName genericAccountTypePrimitive explicitBegin
        explicitEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.isActive ((AuditEnvelope.instant genericEnvelope) |> Calendar.dateFromInstant)
    |> Assert.False

[<Fact>]
let ``REQ-AC-1.50 isActive returns true when the reference point exactly equals begin`` () =
    let explicitBegin = Calendar.today()
    let explicitEnd = None
    Account.constructNew genericCode genericName genericAccountTypePrimitive explicitBegin
        explicitEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.isActive explicitBegin
    |> Assert.True

[<Fact>]
let ``REQ-AC-1.48 isActive returns true when the reference point exactly equals end`` () =
    let explicitBegin = Calendar.today().PlusDays(-1)
    let now = Calendar.today()
    let explicitEnd = Some now
    Account.constructNew genericCode genericName genericAccountTypePrimitive explicitBegin
        explicitEnd genericSubtype genericParentId genericReference genericEnvelope
    |> Result.defaultWith failwith
    |> Account.isActive now
    |> Assert.True
