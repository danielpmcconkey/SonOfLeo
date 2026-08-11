# LeoBloom — F# / DDD Audit

**Auditor:** Hobson
**Date:** 2026-08-10
**Subject:** `/media/dan/fdrive/codeprojects/LeoBloom/` — production cash-basis GAAP ledger, F# on .NET 10, PostgreSQL, CLI-driven
**Purpose:** Establish what LeoBloom gets right and wrong in F#/DDD terms, so SonOfLeo can inherit the former and refuse the latter.

Scope: all `.fsproj` files, all `.fs` under `Src/` excluding `obj/` and `bin/` (~13,000 lines of non-test source, ~23,000 lines of tests, skimmed for caller ergonomics).

---

## Summary of Key Takeaways

LeoBloom is a **well-organised C# program written in F# syntax**. Its architecture is sound, its layering is disciplined, and its domain vocabulary is genuinely accountant-legible — which is the hardest part of DDD and it is done. What it does not do is let the type system carry the weight. Invariants are enforced by convention (free validator functions that callers are trusted to invoke) rather than by construction, and control flow is expressed as nesting depth rather than composition.

**The five things that matter most for SonOfLeo:**

1. **There is no `Money` type and no ID wrapper types.** Every identity is `int`, every amount is `decimal`. In a double-entry ledger this is the single highest-value omission — `sourceAccountId` and `destAccountId` are the same type and nothing prevents transposition. (§1.1, §1.2, §5.2)
2. **There are no smart constructors.** Zero private types, zero `RequireQualifiedAccess`, zero `create : Cmd -> Result<T, Error>`. Every record is publicly constructible in any state, including illegal ones. Validation is a function you are trusted to call. (§1.3)
3. **Optionality models the database schema, not the domain state machine.** `ObligationInstance` carries `amount option`, `confirmedDate option`, `journalEntryId option` because the *columns* are nullable — even though the status DU already says exactly when each is required. The cost is visible as runtime `elif` ladders followed by `.Value` dereferences. (§1.4)
4. **No railway.** Zero computation expressions, 31 `Result.map`/`bind` calls in 13k lines. `ObligationPostingService.postToLedger` is 128 lines and ~30 columns of indentation for eight sequential fallible steps. `AccountService.createAccount` duplicates its entire insert block verbatim in two branches purely because a `match` was in the way. (§2.1)
5. **Errors are strings.** `Error [ "does not exist" ]` and `Error [ "Persistence error: connection reset" ]` are the same type, so the CLI cannot distinguish a validation failure from a database outage — both exit `1`. Worse, a service in the Ledger project embeds a literal CLI invocation string in an error message. (§2.3, §2.4)

**And the things worth protecting.** Accumulating (applicative) validation, closed DUs with `Result`-returning parsers at the persistence boundary, two-phase validation (pure then contextual), command types distinct from entities, a Domain project with *zero* infrastructure dependencies, and `PeriodDisclosure` / "as originally closed" — which is real GAAP fidelity, not a technical convenience. Full list at §6.

---

## 1. Types — Are Illegal States Unrepresentable?

**Verdict: No. But the raw material is there.**

### What works

Every domain enumeration is a closed DU with a paired `toString` / `fromString : string -> Result<_, string>` module: `EntryType`, `NormalBalance`, `AccountSubType`, `InstanceStatus`, `RecurrenceCadence`, `PaymentMethodType`, `TransferStatus`, `ObligationDirection`, `ProjectionDirection`, `ProjectionSourceType`, `OrphanCondition`, `ValidationCheck`, `FundDimensionFilter`. This is the best thing in the codebase and it is done consistently. `option` is used for nullability throughout the domain records — no nulls leak in.

`Ledger.resolveBalance : NormalBalance -> decimal -> decimal -> decimal` encodes the debit/credit sign rule once, as a pure total function.

`StatusTransition.allowedTransitions` (`Ops.fs:327`) is a `Map<InstanceStatus, Set<InstanceStatus>>` — the obligation state machine is **data**, not a scattering of `if`s. Preserve this pattern exactly.

### 1.1 — No wrapper types for identity

Every ID in the system is a bare `int`. `JournalEntryLine.accountId`, `JournalEntry.fiscalPeriodId`, `ObligationInstance.obligationAgreementId`, `Transfer.fromAccountId`/`toAccountId` — all `int`, all mutually assignable.

`ObligationPostingService.fs:59-60`:

```fsharp
let sourceAccountId = agr.sourceAccountId.Value
let destAccountId = agr.destAccountId.Value
```

These two are then placed into a debit line and a credit line respectively. Transposing them produces a ledger entry that is perfectly balanced, perfectly valid, and financially backwards. The compiler has no opinion.

**Why it matters:** *Make illegal states unrepresentable* is not only about DU cases — it covers the type-level distinction between values that are structurally identical but semantically different. `type AccountId = AccountId of int` costs nothing at runtime (and `[<Struct>]` removes even the allocation), and converts an entire class of transposition bug from a runtime accounting error into a compile error. In a system whose output is tax filings, that trade is not close.

### 1.2 — No `Money` type in a general ledger

`decimal` is used for amounts, balances, prices, *quantities*, and *percentages*. `Position` (`Portfolio.fs:35`) has `price`, `quantity`, `currentValue`, `costBasis` — all `decimal`, all adjacent. `GainLossRow` has `gainLoss: decimal` next to `gainLossPct: decimal`. Quantity is not money; multiplying price by quantity should not yield something assignable back to quantity.

USD-only is a stated constraint, so a currency tag is genuinely unnecessary. But **rounding is a domain rule and it currently lives nowhere.** `InvoiceValidation.validateAmount` (`Ops.fs:437`) checks `Math.Round(amount, 2, AwayFromZero)` — for invoices only. No other amount in the system is constrained to two decimal places, and `List.sumBy` over `decimal` will happily accumulate whatever the database returns.

**Why it matters:** A `Money` single-case DU with its own `+`, `-`, and a `sum` that rounds once at a defined boundary puts the rounding policy in exactly one place. Today it is in one place for invoices and no places for everything else, which is the worst of both.

### 1.3 — No smart constructors anywhere

A grep for `RequireQualifiedAccess`, `[<Struct>]`, `= private`, and `private (` across the entire source returns **nothing**. Every record type is fully public with fully public fields. There is no module anywhere exposing a `create` that returns `Result<T, _>`.

Validation exists — `Ledger.validateCommand`, `ObligationAgreementValidation.validateCreateCommand`, `InvoiceValidation.validateCommand` — but it is a **free function applied by convention**. `JournalEntryService.post` calls it. A future caller need not. Nothing in the type of `PostedJournalEntry` records that it was ever validated:

```fsharp
type PostedJournalEntry =
    { entry: JournalEntry
      lines: JournalEntryLine list
      references: JournalEntryReference list }
```

Construct one directly with a single line and unequal debits and credits, and you have an unbalanced journal entry as a first-class, type-checked value.

**Why it matters:** *Parse, don't validate.* Validation that returns `Result<unit, _>` proves something happened but produces no evidence; the caller is free to ignore it and proceed. Validation that returns `Result<BalancedEntry, _>`, where `BalancedEntry` has a private constructor and is the only type the repository accepts, makes the invariant structural. The proof travels with the value.

### 1.4 — Optionality models the schema, not the domain

`ObligationInstance` (`Ops.fs:114`):

```fsharp
{ status: InstanceStatus
  amount: decimal option
  confirmedDate: DateOnly option
  journalEntryId: int option
  ... }
```

The state machine already states the invariants: a `Confirmed` instance must have an amount and a confirmed date; a `Posted` instance must additionally have a journal entry; an `Expected` instance has none of them. The type expresses none of this, because the columns are nullable.

The cost is directly observable. `ObligationPostingService.fs:27-35`:

```fsharp
elif inst.status <> InstanceStatus.Confirmed then ...
elif inst.amount.IsNone then ...
elif inst.confirmedDate.IsNone then ...
```

followed thirty lines later (`:51, :58`) by:

```fsharp
let confirmedDate = inst.confirmedDate.Value
let amount = inst.amount.Value
```

A four-branch runtime ladder re-deriving what the status already implied, then two partial-function dereferences guarded only by the reader's willingness to scroll up. There are **15 `.Value` sites** in non-test source; `FiscalPeriodCloseService.fs:65` (`cmd.note.Value`) is another, guarded by an `if` forty lines earlier.

The DU-of-states shape removes all of it:

```fsharp
type ObligationInstance =
    | Expected  of Common
    | InFlight  of Common * amount: Money option
    | Confirmed of Common * amount: Money * confirmedDate: DateOnly
    | Posted    of Common * amount: Money * confirmedDate: DateOnly * je: JournalEntryId
    | Overdue   of Common
    | Skipped   of Common * reason: string
```

`postToLedger` then takes a `Confirmed` and cannot be called with anything else.

### 1.5 — `FiscalPeriod` encodes two states in three fields

```fsharp
{ isOpen: bool
  closedAt: DateTimeOffset option
  closedBy: string option
  ... }
```

Four of the eight combinations are nonsense, and the code must defend against them. `TrialBalanceService.fs:48-49`:

```fsharp
| Some d when d.isOpen -> Error "Cannot use --as-originally-closed on an open period"
| Some d when d.closedAt.IsNone -> Error "Period has no close timestamp"
```

That second branch handles a state that should not exist. This exact pair is repeated in `IncomeStatementService`, `BalanceSheetService`, and `SubtreePLService` — four copies of a guard against an unrepresentable-in-principle state. `type PeriodState = Open | Closed of closedAt: DateTimeOffset * closedBy: Actor` deletes all four.

### 1.6 — The most fundamental enumeration in the domain is a string

`AccountSubType` got a DU. Its **parent**, `AccountType`, did not — it is `{ id: int; name: string; normalBalance: NormalBalance }`, and every piece of account-type logic keys off that string:

| Location | Code |
|---|---|
| `Ledger.fs:62` | `match accountTypeName.ToLowerInvariant() with "asset" \| "liability" \| ...` |
| `TrialBalanceService.fs:10` | `accountTypeOrder = [ "asset"; "liability"; "equity"; "revenue"; "expense" ]` |
| `BalanceSheetService.fs:19-27` | `List.filter (fun (typeName, _) -> typeName = "asset")` ×3 |
| `IncomeStatementService.fs:18-22` | `typeName = "revenue"` / `"expense"` |
| `SubtreePLService.fs:18-22` | same, duplicated |
| `OpeningBalanceService.fs:90` | `if info.accountTypeName <> "equity" then` |
| `TransferService.fs:53` | `when typeName <> "asset"` |

The five account types are the most closed set in double-entry bookkeeping. Worse, `validSubTypesForAccountType` returns `[]` for an unrecognised name, so an unknown account type silently makes *every* subtype invalid rather than raising an error.

### 1.7 — Two different answers to DB corruption

`AccountRepository.fs:19-21` and `AccountBalanceRepository.fs:16-19`:

```fsharp
match AccountSubType.fromDbString (reader.GetString(5)) with
| Ok st -> Some st
| Error _ -> None          // corrupt value silently becomes "no subtype"
```

`ObligationInstanceRepository.fs:27`, `ObligationAgreementRepository.fs:21/25/31`, `TransferRepository.fs:21`:

```fsharp
| Error msg -> failwithf "Corrupt status in DB: %s" msg
```

Same question, same codebase, opposite answers — and the silent one is worse, because `isValidSubType` treats `None` as valid, so corruption round-trips as legitimate absence. Decide the policy once.

### 1.8 — DU case-name shadowing (minor)

`Ledger.fs:9-11`:

```fsharp
type EntryType = Debit | Credit
type NormalBalance = Debit | Credit
```

`NormalBalance`'s cases shadow `EntryType`'s, which is why every use site is written `EntryType.Debit` / `NormalBalance.Debit`. The types are distinct so nothing unsafe follows, but the qualification is currently a workaround rather than a rule. `[<RequireQualifiedAccess>]` on both makes it intentional and removes the shadowing hazard for future cases.

---

## 2. Composition — Error Handling and Railway Style

**Verdict: Consistent choice of `Result`, no composition of it.**

### What works

`Result<_, string list>` is the near-universal service return type, and the pure validators **accumulate** rather than short-circuit:

```fsharp
let allErrors =
    [ validateName cmd.name
      validateCounterparty cmd.counterparty
      validateAmount cmd.amount
      validateExpectedDay cmd.expectedDay ]
    |> List.collect (function Error errs -> errs | Ok _ -> [])
if allErrors.IsEmpty then Ok () else Error allErrors
```

Applicative accumulation for user-facing validation is the *correct* choice — a user submitting a bad journal entry wants all four problems, not the first one. This appears in `Ledger.validateCommand`, `ObligationAgreementValidation`, and `InvoiceValidation`, and it was clearly deliberate. **Keep it.**

The two-phase discipline is also right: pure validation, then DB-dependent validation, then persistence — with the phases named in comments (`JournalEntryService.post:96-102`). And the idempotency guards (`findNonVoidedByReference` before posting an obligation, a transfer, or a reversal) are a genuine domain concern handled explicitly rather than hoped away.

### 2.1 — No railway; nesting stands in for composition

**Zero** computation expressions in the entire repository. **31** total uses of `Result.map`/`bind`/`mapError` across ~13k lines of source, most of them trivial `Result.map Some` in CLI arg parsing.

The dominant shape is the pyramid. `ObligationPostingService.postToLedger` is 128 lines reaching roughly 30 columns of indentation, expressing eight sequential fallible steps: load instance → check active → check status → check amount → check confirmed date → load agreement → check both accounts → find fiscal period → guard idempotency → post JE → transition instance. As a `result { }` computation expression that is eight lines and reads top to bottom.

The clearest evidence of the cost is `AccountService.createAccount` (`AccountService.fs:33-55`). Because `match cmd.parentId with Some pid | None` sits in the middle of the flow, the **entire** insert-and-catch-duplicate-code block is duplicated verbatim in both branches:

```fsharp
| Some _ ->
    try
        let acct = AccountRepository.create txn cmd.code cmd.name cmd.accountTypeId cmd.parentId cmd.subType cmd.externalRef
        Log.info "Created account {AccountId}" [| acct.id :> obj |]
        Ok acct
    with
    | :? PostgresException as ex when ex.SqlState = "23505" ->
        Error [ sprintf "account with code '%s' already exists" cmd.code ]
| None ->
    try
        let acct = AccountRepository.create txn cmd.code cmd.name cmd.accountTypeId cmd.parentId cmd.subType cmd.externalRef
        Log.info "Created account {AccountId}" [| acct.id :> obj |]
        Ok acct
    with
    | :? PostgresException as ex when ex.SqlState = "23505" ->
        Error [ sprintf "account with code '%s' already exists" cmd.code ]
```

That is copy-paste driven by indentation. Monadic composition exists precisely so that control flow does not become layout.

**Note the tension SonOfLeo must resolve deliberately:** the validators want *applicative* accumulation (collect all errors), the service orchestration wants *monadic* short-circuit (stop at the first failed step — you cannot check an agreement you failed to load). These are different operations and both are needed. Pick a small error type, write `result { }` for the monadic path, keep the `List.collect` accumulator for the applicative path, and be explicit about which is which.

### 2.2 — Two incompatible error channels

Services return `Result<_, string list>` in the Ledger write path, all of Ops, all of Portfolio, and all of Reporting — but `Result<_, string>` (singular) in `AccountBalanceService`, `TrialBalanceService`, `IncomeStatementService`, `BalanceSheetService`, and `SubtreePLService`.

Callers then adapt by hand. `BalanceProjectionService.fs:34,37`:

```fsharp
| Error msg -> Error [ msg ]
```

Two channels meaning the same thing, reconciled at the seam. Pick one.

### 2.3 — Errors are strings, so nothing can act on them

```fsharp
Error [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
Error [ sprintf "Persistence error: %s" ex.Message ]
```

Same type. The CLI (`OutputFormatter.write`) therefore maps **every** `Error` to `ExitCodes.businessError = 1` — a validation failure and a database outage exit identically. A cron job cannot tell "you posted a bad entry" from "the database is down."

A `DomainError` DU costs one type declaration and gives exit codes, retry policy, log severity, and message text a single source:

```fsharp
type LedgerError =
    | NotFound of entity: string * id: int
    | PeriodClosed of periodKey: string * closedAt: DateTimeOffset
    | Unbalanced of debits: Money * credits: Money
    | InvalidTransition of from: InstanceStatus * to_: InstanceStatus
    | Persistence of exn
```

### 2.4 — Presentation baked into domain errors

`JournalEntryService.fs:166` — inside the **Ledger project**:

```fsharp
Error [
    sprintf "Cannot void JE %d — it belongs to closed period '%s' (closed %s).\n       Post a reversing entry in the current open period instead:\n       leobloom ledger reverse --journal-entry-id %d"
        cmd.journalEntryId fp.periodKey closedAtStr cmd.journalEntryId ]
```

The domain layer now knows the name of the executable, its argument syntax, and how many spaces to indent a hint. Rename the CLI and this string lies. The error should be `PeriodClosed (periodKey, closedAt)`; the CLI decides how to render it and what remedy to suggest.

### 2.5 — `try/with` impersonating `Result`

Roughly 40 sites of:

```fsharp
with ex ->
    Log.errorExn ex "..." [| ... |]
    Error [ sprintf "Persistence error: %s" ex.Message ]
```

Copy-pasted per function rather than factored into one `tryQuery : (unit -> 'a) -> Result<'a, LedgerError>` combinator, and it discards the exception type — a deadlock, a timeout, and a constraint violation all become the same opaque string.

**Worse:** several list-returning services swallow failure entirely.

```fsharp
// ObligationInstanceService.list, findUpcoming; TransferService.list; InvoiceService.listInvoices
with ex ->
    Log.errorExn ex "Failed to list ..." [||]
    []
```

A failed query is indistinguishable from an empty result set. "No obligations due this week" and "the database is unreachable" render identically to the operator. In a system whose purpose is making sure nothing bounces, that is a correctness hazard, not a style point.

### 2.6 — `Result.defaultValue` used to discard errors already detected

`LedgerCommands.handlePost:127-156`. The handler parses debits, credits, the date, and references into `Result` lists, collects the errors imperatively, confirms the error list is empty — and then **re-walks the same lists discarding the `Result`**:

```fsharp
let (acctId, amt) = Result.defaultValue (0, 0m) r
...
let entryDate = match dateParsed with Ok d -> d | _ -> DateOnly.MinValue
...
refsParsed |> List.map (fun r -> Result.defaultValue { referenceType = ""; referenceValue = "" } r)
```

Account 0, amount 0, and `DateOnly.MinValue` are sentinel values standing in for "this cannot happen." The compiler is being told to trust a fact it was never shown. One `traverseResult : ('a -> Result<'b,'e>) -> 'a list -> Result<'b list, 'e list>` written once turns the whole block into a single `match` on `Result<(AccountId * Money) list, string list>`, and the sentinels vanish because the failure case has no values to invent.

---

## 3. Module Boundaries

**Verdict: Excellent at the project level, leaky at the module level.**

### What works

The project graph is clean and acyclic:

```
Domain  (no dependencies at all)
  ↑
Utilities → Ledger → { Ops, Reporting }        Portfolio (parallel)
                              ↑                    ↑
                             CLI ──────────────────┘
```

`LeoBloom.Domain.fsproj` has **zero** `PackageReference` entries — no Npgsql, no Serilog, no configuration. That is the single most important boundary in the system and it holds absolutely. `Ledger.fs`'s header comment states the layering rule ("Ops can reference Ledger types. Ledger cannot reference Ops") and it is obeyed.

Compile order within each project is deliberate and readable — `Ledger.fs` before `Ops.fs` before `Portfolio.fs`; repositories before the services that use them.

The repository/service split is mechanical and predictable: repositories take an `NpgsqlTransaction` and return domain types or `option`; services own validation and orchestration; the CLI owns connection and transaction lifecycle. Every function takes `txn` first. That consistency is real discipline and it pays off in readability.

### 3.1 — The Domain project is one anaemic bag, not three bounded contexts

`Domain/Ledger.fs` is 410 lines containing accounts, account types, fiscal periods, journal entries, opening balances, command DTOs, **and** four report shapes — `TrialBalanceReport`, `IncomeStatementReport`, `BalanceSheetReport`, `SubtreePLReport`. Meanwhile `LeoBloom.Reporting/ReportingTypes.fs` holds `ScheduleEReport`, `GeneralLedgerReport`, `CashReceiptsReport`, `CashDisbursementsReport`, `NetWorthReport`.

There are two homes for report types, and the split is determined by *which project happens to compute them*, not by anything a domain expert would recognise.

Aggregate boundaries would say: `JournalEntry + lines + references` is one aggregate; `FiscalPeriod + audit trail` is another; `ObligationAgreement + its instances` is a third; reports are **read models** and belong outside all three.

### 3.2 — Query vocabulary is defined by the persistence layer

`ListInstancesFilter` is declared at `ObligationInstanceRepository.fs:9`. `ListTransfersFilter` at `TransferRepository.fs:8`. `ListAgreementsFilter` at `ObligationAgreementRepository.fs:8`. `ListInvoicesFilter` at `InvoiceRepository.fs:8`.

All four are **constructed by the CLI** and passed *through* the service into the repository. The shape of a domain query is therefore owned by the module that speaks SQL.

Also note the shape itself: `{ status: InstanceStatus option; dueBefore: DateOnly option; dueAfter: DateOnly option }` — all-`None` means "everything", and `{ dueBefore = Some x; dueAfter = Some y }` with `y > x` is a representable query that returns nothing. A DU of intents (`All | ByStatus of InstanceStatus | DueBetween of DateRange`) says what it means.

### 3.3 — Services reach around their own repositories

| Location | Violation |
|---|---|
| `ObligationInstanceService.fs:12` | `journalEntryExists` opens a raw `NpgsqlCommand` against `ledger.journal_entry` — from inside an **Ops** service, bypassing both its own repository and the entire Ledger project, which already has this capability |
| `FiscalPeriodValidation.fs:33-145` | Raw SQL against `ledger.journal_entry`, `ledger.journal_entry_line`, **and** `ops.obligation_instance` — four queries, no repository |
| `TransferService.fs:13` | `lookupAccountInfo` queries `ledger.account` directly |
| `JournalEntryService.fs:17,37` | `lookupFiscalPeriod` and `lookupAccountActivity` — SQL in a service, alongside a repository that exists |
| `OpeningBalanceService.fs:17` | Hand-rolls `lookupAccounts` against `ledger.account` |

The repository boundary is a naming convention, not a constraint. Every one of these is a place where a schema change breaks code nobody would think to grep.

### 3.4 — Ambient infrastructure dependencies

`DataSource` (`Utilities/DataSource.fs`) is a module-level `let private dataSource = ...` that, on first touch, reads configuration, opens a connection, executes `SELECT current_database()`, and `failwith`s if anything is wrong. Every module transitively referencing it is untestable without a live PostgreSQL — which is why the test suite (23k lines) is entirely integration-level, with hand-rolled `InsertHelpers` writing raw SQL to set up fixtures.

The `#if DEBUG` guard (`DataSource.fs:49-54`) verifying the connection is to `leobloom_dev` is a genuinely good safety instinct — implemented in the one place in the system that cannot be exercised by a test.

`Log` (`Utilities/Log.fs`) has the same shape: a module-level `mutable initialized` flag over Serilog's global static. Every service function logs on entry, so no service function is referentially transparent or silently testable.

The code **already threads `txn` through every function**. Threading a connection factory or a logger the same way costs one more parameter and removes the ambient dependency entirely.

---

## 4. Idiom — Where It Thinks Functionally, Where It Thinks Imperatively

**Verdict: Functional in the small, imperative in the large.**

### What works

- **Currying and parameter order.** Every repository and service function takes context first, subject last (`txn` → filters → id). `f txn` is a usable partially-applied query. This is real F# discipline, applied consistently across ~60 modules.
- **List-comprehension conditionals for error collection** — idiomatic and pleasant:
  ```fsharp
  [ if String.IsNullOrWhiteSpace name then "name is required and cannot be empty"
    if name.Length > 100 then "name must not exceed 100 characters" ]
  ```
  `TransferService.initiate` uses this for both `pureErrors` and `dbErrors`, the latter with `match ... | _ -> ()` inside the comprehension — genuinely nice.
- **Pattern matching where it is used**: `resolveBalance`, the `toString`/`fromString` pairs, `formatEntryHeader`'s `match e.voidedAt with Some dt -> "VOIDED..." | None -> "POSTED"`.
- **`Map` and `Set` where appropriate**: `allowedTransitions`, `line19SubDetail`, `infoMap`.
- **Copy-and-update** (`{ d with asOriginallyClosed = true }`), records as values, structural equality.
- **`ScheduleEMapping`** is a small model of what a pure domain module should look like: data, no I/O, derived values computed from the data (`allMappedAccountCodes`).

### 4.1 — 75 `mutable` bindings in non-test source

The imperative accumulator is the *default* idiom for reading result sets. Roughly fifteen repositories contain:

```fsharp
let mutable results = []
while reader.Read() do
    results <- mapReader reader :: results
reader.Close()
results
```

— sometimes followed by `List.rev`, sometimes not (`JournalEntryService.lookupAccountActivity:50-54` doesn't; order is not load-bearing there, but the inconsistency is a latent bug in a system where report row order *is* load-bearing elsewhere).

Worse, `JournalEntryRepository.getEntryById:149,171`:

```fsharp
lines <- lines @ [ { ... } ]
```

List append inside a loop — quadratic. Twice in one function.

One helper — `readAll : (DbDataReader -> 'a) -> DbDataReader -> 'a list`, or `seq { while reader.Read() do yield map reader } |> List.ofSeq` — replaces all of them. This is the highest-volume, lowest-risk cleanup available.

### 4.2 — Two idioms for one job, ten files apart

`JournalEntryService.validateDbDependencies:56-91` accumulates errors imperatively:

```fsharp
let mutable errors = []
...
errors <- errors @ [ sprintf "Fiscal period with id %d does not exist" cmd.fiscalPeriodId ]
```

`OpeningBalanceService.validateCommand:42-63` does the same with `::` and a trailing `List.rev`. `AccountService`, `FiscalPeriodService`, and `PositionService` use `ResizeArray<string>` with `.Add` and `Seq.toList`. And `Domain/Ops.fs` does the same job declaratively with the list comprehension shown above.

**Four idioms for "collect validation messages"** in one codebase. Pick one — the comprehension — and use it everywhere.

### 4.3 — The purest function in the domain is a `while` loop

`ObligationInstanceSpawning.generateExpectedDates` (`Ops.fs:251-293`). Cadence + anchor day + date range → list of dates. No I/O, no state, total. It is written with `let mutable dates = []` and a `while` loop per cadence branch, four times.

This is the function a reader most wants to *see the rule in*. `List.unfold`, or `[ for m in 0 .. months -> ... ] |> List.filter inRange`, expresses monthly-with-day-clamping in three lines that read like the rule they implement.

### 4.4 — Side-effecting `List.map`

`OpeningBalanceService.fs:99-114`:

```fsharp
let mutable totalDebits = 0m
let mutable totalCredits = 0m
let entryLines =
    cmd.entries
    |> List.map (fun entry ->
        let entryType =
            match info.normalBalance with
            | "debit"  -> totalDebits  <- totalDebits + entry.balance;  EntryType.Debit
            | _        -> totalCredits <- totalCredits + entry.balance; EntryType.Credit
        { ... })
```

A mapping function mutating outer state. `List.map` promises a transformation, not a traversal with effects, and nothing at the call site warns the reader. Map first, then `List.sumBy` over the result partitioned by `entryType` — two passes over a list that is at most a few dozen elements, and it is *correct by construction*.

### 4.5 — `formatHuman : obj -> string` — twenty-case runtime type test

`OutputFormatter.fs:726`:

```fsharp
let formatHuman (value: obj) : string =
    match value with
    | :? PostedJournalEntry as p -> formatPostedEntry p
    | :? JournalEntry as e -> formatJournalEntry e
    ... 18 more ...
    | _ -> sprintf "%A" value
```

This is C#-in-F#, and it is self-inflicted. Every CLI call site does `result |> Result.map (fun v -> v :> obj)` to *enter* `obj`, and this function type-tests its way back out. It defeats exhaustiveness checking entirely — add a new report type and the code compiles, runs, and silently emits `sprintf "%A"`.

The consequences are visible in the same file. From line 787 onward there are **fourteen** "dedicated write functions", with this comment attached:

> `/// Dedicated write function for Invoice list to avoid F# type erasure`
> `/// issues with generic list pattern matching in formatHuman.`

`writeInvoiceList`, `writeTransferList`, `writeAgreementList`, `writeInstanceList`, `writeAccountList`, `writePeriodList`, `writeAuditList`, `writeSpawnResult`, `writePostResult`, `writeOverdueResult`, `writeOrphanedPostings`, `writeAllocationReport`, `writePortfolioSummary`, `writeGainsReport`… The design collapsed into per-type write functions for half the types anyway. Going the rest of the way — a `Renderable` DU, or simply per-type `write` functions throughout — costs less code than the workaround does.

### 4.6 — The transaction ceremony, forty times

Every CLI handler:

```fsharp
use conn = DataSource.openConnection()
use txn = conn.BeginTransaction()
try
    let result = SomeService.doThing txn cmd
    match result with
    | Ok _ -> txn.Commit()
    | Error _ -> txn.Rollback()
    write isJson (result |> Result.map (fun v -> v :> obj))
with ex ->
    try txn.Rollback() with _ -> ()
    reraise()
```

Roughly forty copies. Note the redundancy: `use txn` already rolls back on dispose, so the `with` clause is ceremony wrapped around ceremony — and `try txn.Rollback() with _ -> ()` silently swallows a rollback failure, which in a financial system is precisely the failure you most want to hear about.

One higher-order function:

```fsharp
let withTransaction (f: NpgsqlTransaction -> Result<'a, 'e>) : Result<'a, 'e> =
    use conn = DataSource.openConnection()
    use txn = conn.BeginTransaction()
    match f txn with
    | Ok v -> txn.Commit(); Ok v
    | Error e -> Error e          // dispose rolls back
```

Forty copies of a resource protocol is forty chances to get it wrong. Read-only handlers also `txn.Commit()` transactions that ran only `SELECT`s (`handleShow`, `handleAgreementShow`) — harmless, but it means "Commit" carries no information at the call site.

### 4.7 — `Choice` where a DU belongs

`CliHelpers.fs:10`:

```fsharp
let parsePeriodArg (raw: string) : Choice<int, string> =
    match Int32.TryParse(raw) with
    | true, id -> Choice1Of2 id
    | false, _ -> Choice2Of2 raw
```

`Choice` is the anonymous sum type — for when naming the cases isn't worth it. Here it very much is: `type PeriodRef = ById of FiscalPeriodId | ByKey of PeriodKey` is one line, and every match site stops reading like `Choice1Of2` and starts reading like accounting.

### 4.8 — Dynamic SQL fragments (minor, but worth naming)

`AccountRepository.create`/`update` assemble column and SET lists via `sprintf` to avoid combinatorial explosion. Values are all parameterised — no injection — and the reasoning is sound.

`DimensionRepository.fs:13` interpolates a **table name**:

```fsharp
sprintf "SELECT id, name FROM portfolio.%s ORDER BY id" table
```

All eight call sites pass literals, so it is safe today. But the function's signature (`string -> ...`) permits exactly what its safety depends on not happening. Take a DU of the eight dimension tables and the guarantee becomes structural rather than social.

---

## 5. DDD — Domain Language, Aggregates, Boundaries

**Verdict: The vocabulary is genuinely excellent. The aggregates are not enforced.**

### What works — and it is substantial

**The ubiquitous language is real.** `JournalEntry`, `JournalEntryLine`, `NormalBalance`, `TrialBalanceReport`, `FiscalPeriod`, `PeriodDisclosure`, `AdjustmentDetail`, `ObligationAgreement` / `ObligationInstance`, `ScheduleELineMapping`, `retainedEarnings`, `reopenedCount`. An accountant could read `Domain/Ledger.fs` and recognise their own job. This is the hardest part of DDD and LeoBloom does it well.

**The agreement/instance decomposition is a genuinely good piece of modelling.** Separating a recurring *obligation* from a dated *occurrence* of it is what makes spawning, overdue detection, posting, and cash projection four independent concerns over one concept. Keep it.

**Commands are explicit types distinct from entities** — `PostJournalEntryCommand`, `TransitionCommand`, `CloseFiscalPeriodCommand`, `SpawnObligationInstancesCommand`, `RecordInvoiceCommand`, `InitiateTransferCommand`. CQRS-flavoured and correct. `PostedJournalEntry` as the read-back counterpart is the right instinct.

**`PeriodDisclosure` with `asOriginallyClosed`** models the GAAP notion of the books as they stood at close, before subsequent adjustments — with `adjustmentCount`, `adjustmentNetImpact`, and the adjustment detail attached. That is real accounting fidelity that a lesser design would have skipped.

**Domain invariants are named in domain terms**: `validateBalanced`, `validateMinimumLineCount`, `validateTotalEqualsComponents`, and the four pre-close checks modelled as a DU (`TrialBalanceEquilibrium | BalanceSheetEquation | DataHygiene | OpenObligations`) with per-check results — that is a small, well-shaped piece of domain modelling.

### 5.1 — The journal-entry aggregate has no root and no enforced invariant

"Debits equal credits" is *the* invariant of double-entry bookkeeping. In LeoBloom it is a free function (`validateBalanced`) that one service (`JournalEntryService.post`) chooses to call. There is no type that means "a balanced entry," and `PostedJournalEntry` — the closest thing to an aggregate root — is a transparent record any code can construct in any state.

The aggregate should own its invariant, and the only path to a persistable value should run through the check.

### 5.2 — The debit/credit rule lives in three languages

| Where | How |
|---|---|
| F# domain | `Ledger.resolveBalance` — the canonical statement |
| SQL, ~6 places | `COALESCE(SUM(CASE WHEN jel.entry_type = 'debit' THEN jel.amount ELSE 0 END), 0) - COALESCE(SUM(CASE WHEN ... 'credit' ...), 0)` — `AccountBalanceRepository`, `PeriodDisclosureRepository`, `FiscalPeriodValidation`, `BalanceSheetRepository`, `IncomeStatementRepository`, `TrialBalanceRepository` |
| F#, stringly | `OpeningBalanceService.fs:104` — `match info.normalBalance with "debit" -> ... | _ -> ...` on a raw string read from `account_type` |

`AccountBalanceRepository.fs:123` even acknowledges it:

```fsharp
// SQL pre-computes raw_balance = debits - credits; resolveBalance(nb, raw, 0) is algebraically equivalent
```

That is an honest comment and also an admission: the most fundamental rule in the domain now lives in two languages and must be kept algebraically in step by hand, forever. Whichever side owns it, one side should own it.

### 5.3 — Two concepts, one duplicated posting flow, three magic strings

`TransferService.confirm` and `ObligationPostingService.postToLedger` are structurally identical: look up the source record → check active → check status → find the fiscal period for the confirmed date → build a two-line JE → attach a typed reference → check idempotency by that reference → post → update the source record. Sixty lines each, written twice.

The reference types are bare strings — `"obligation"`, `"transfer"`, `"reversal"` — appearing in `ObligationPostingService`, `TransferService`, `JournalEntryService.reverseEntry`, and `OrphanedPostingRepository`, with no shared declaration. `OrphanCondition.InvalidReference` exists precisely because `reference_value` might not parse as an integer for a given `reference_type` — an entire diagnostic category generated by the absence of a type:

```fsharp
type LedgerReference =
    | FromObligation of ObligationInstanceId
    | FromTransfer   of TransferId
    | Reversal       of JournalEntryId
```

That names the concept, makes `findNonVoidedByReference` total, deletes one of the four orphan conditions, and lets the shared posting flow be written once.

### 5.4 — Bounded-context placement

Ops depending on Ledger is correct and stated. Two placements are not:

- **`FiscalPeriodValidation` lives in Ops** but three of its four checks (trial balance equilibrium, balance sheet equation, data hygiene) are pure Ledger invariants. Only `checkOpenObligations` is Ops-flavoured. As it stands, "closing the books" — a Ledger concept — is owned by Ops.
- **CLI syntax lives in Ledger** (§2.4).

### 5.5 — Portfolio is a different quality tier

`Fund` is a bag of six `int option` foreign keys with no domain meaning attached. `AllocationRow` and `HistoryRow` use `category: string` and `(string * decimal) list` — the *filter* over dimensions is a proper DU (`FundDimensionFilter`), but the *result* is stringly-typed. `DimensionTable = { tableName: string; values: (int * string) list }` is a database row shape wearing a domain type's name.

And `PositionService.recordPosition` is the only service that never got a Command type:

```fsharp
recordPosition (txn) (investmentAccountId: int) (symbol: string) (positionDate: DateOnly)
               (price: decimal) (quantity: decimal) (currentValue: decimal) (costBasis: decimal)
```

Four adjacent `decimal` parameters. This is the single most transposable signature in the repository, and it is in the module that tracks the retirement portfolio.

---

## 6. What Works — Preserve These in SonOfLeo

Not everything is a finding. These are deliberate, correct, and worth carrying forward unchanged:

1. **Closed DUs for every domain enumeration**, each with a `toString` / `fromString : string -> Result<_, string>` module pair at the persistence boundary. Consistently applied across a dozen types.
2. **Accumulating (applicative) validation** for user-facing input — `List.collect (function Error e -> e | Ok _ -> [])`. Users get all their errors at once. Correct choice, deliberately made.
3. **List-comprehension conditionals** for building error lists — the cleanest of the four idioms present; standardise on it.
4. **Two-phase validation**: pure/structural first, contextual/DB second, persistence last. The phases are even named in comments.
5. **Command types distinct from entities.** `PostJournalEntryCommand` ≠ `JournalEntry`. CQRS-flavoured and right.
6. **`txn`-first parameter ordering, caller owns the transaction.** Consistent across ~60 modules; makes partial application useful and keeps transaction scope a caller decision.
7. **State machine as data** — `StatusTransition.allowedTransitions : Map<InstanceStatus, Set<InstanceStatus>>`. One place to read the rules, one place to change them.
8. **Idempotency by domain reference** — `findNonVoidedByReference` before every posting operation. A real, hard-won domain concern handled explicitly.
9. **A Domain project with literally zero infrastructure dependencies.** Protect this above all.
10. **`PeriodDisclosure` and "as originally closed."** Real GAAP fidelity; a lesser design would not have bothered.
11. **`ScheduleEMapping`** as a template for pure domain data modules — data, no I/O, derived values computed from the data.
12. **The obligation agreement/instance decomposition.** Good modelling.
13. **The `#if DEBUG` production-database guard.** Right instinct; move it somewhere testable.

---

## 7. Priority for SonOfLeo

If Dan changes five things and nothing else, these five, in this order:

1. **Wrap identities and money.** `AccountId`, `JournalEntryId`, `FiscalPeriodId`, `ObligationInstanceId`, `Money`. Single-case DUs, `[<Struct>]` where it matters. This is cheap, mechanical, and closes the largest correctness gap in the system.
2. **Adopt one `result { }` computation expression and one `DomainError` DU.** Together these delete the pyramid, the duplicated blocks, the sentinel values, the string-vs-string-list split, and the inability to distinguish a bad entry from a dead database.
3. **Model states as DUs, not as nullable columns.** `ObligationInstance`, `FiscalPeriod`, `Transfer`. Every `.Value` in the codebase disappears with them.
4. **Give the journal-entry aggregate a private constructor.** `create : PostJournalEntryCommand -> Result<BalancedEntry, DomainError>`, and make `BalancedEntry` the only thing the repository will accept. The invariant becomes structural.
5. **Make `AccountType` a DU** and decide, once, whether the debit/credit rule lives in F# or in SQL.

Everything else in this document is worth doing and none of it is urgent by comparison.

---

*Two closing observations, since they bear on how SonOfLeo should be built rather than what it should contain.*

*First: LeoBloom's flaws are remarkably consistent. The same four or five decisions — no wrapper types, no smart constructors, no railway, string errors, imperative accumulation — account for nearly every finding above. That is a good sign. It means SonOfLeo does not need a different architecture; it needs the same architecture with the type system switched on.*

*Second: LeoBloom is not badly written. It is legible, consistently structured, well-commented where comments earn their place, and it has run in production without incident. The critique above is a critique of a working system against a standard most working systems do not meet. Worth remembering when the rewrite gets tedious around week three.*
