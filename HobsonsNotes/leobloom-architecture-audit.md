# LeoBloom Architecture Audit

**Auditor:** Hobson
**Date:** 2026-08-10
**Subject:** `/media/dan/fdrive/codeprojects/LeoBloom/` — F# / .NET 10 / PostgreSQL
**Purpose:** Identify architectural lessons for SonOfLeo. What to carry forward, what to leave behind.

---

## Summary of Key Takeaways

LeoBloom is better-structured than most personal projects of its size. The domain
boundaries are real, the dependency graph is a tree rather than a lattice, the
migrations are genuinely reversible, and there are 1,100 tests against a real
database. The architectural decisions were written down as ADRs *before* the audit
that prompted them. That is not nothing.

The failures are of a consistent kind: **the design documents describe a system
that the code does not implement, and the gap was never closed.** Three of the four
ADRs are contradicted by the code or by production reality.

### The five findings that matter most

1. **ADR-003 is inverted in practice.** It states "The CLI layer never opens
   connections or manages transactions." There are 64 `BeginTransaction` call sites
   in `LeoBloom.CLI`. The unit of work is defined by the presentation layer, which
   means no service composes and no second consumer is possible without
   reimplementing transaction handling. → §4, §6

2. **ADR-001's premise is factually false in production.** It justifies having zero
   CHECK constraints on the grounds that "there is no direct SQL access in
   production, no REST API, no secondary consumers hitting the database." Python
   scripts in LeoBloomOps write directly to `portfolio.position` (three importers)
   and `ops.invoice` (`generate_bills.py:459`), and an entire `stage` schema exists
   outside the migration chain with foreign keys into `ledger.journal_entry`. The
   ADR's own stated trigger for revisiting the decision has already fired. → §3

3. **The error channel is `string`, so presentation leaked into the domain.**
   Ten sites in `LeoBloom.Ledger` and `LeoBloom.Ops` embed CLI flag names and
   command-line syntax in error messages, including a full copy-pasteable
   `leobloom ledger reverse --journal-entry-id %d` inside `JournalEntryService.fs:166`.
   This is not sloppiness — it is the only way to produce a useful message when the
   error type carries no structure. → §4

4. **The CLI boundary type is `obj`.** `OutputFormatter.write` takes
   `Result<obj, string list>` and dispatches on 21 runtime type tests. It did not
   hold: there are now 24 `write*` functions, several with comments explicitly
   naming "F# type erasure issues" as the reason they exist. Domain records are
   serialized directly to JSON, making internal field names the public API contract
   for the COYS bots. → §4

5. **`Result` is never composed.** Zero uses of `Result.bind`, a `result`
   computation expression, or any combinator anywhere in the codebase. Every service
   destructures by hand with nested `match`. `ObligationPostingService.fs` reaches
   48 columns of indentation — twelve levels deep. This is the single most repeated
   defect and the cheapest one to fix in a rewrite. → §2, §6

### What SonOfLeo should preserve

Domain-based project splitting (ADR-002 was right and the code honours it), the pure
`LeoBloom.Domain` project with no I/O dependency, reversible migrations with real
`DOWN` scripts, the `ExtractTypes` wire-contract pattern, DB-backed tests over mocks,
and the discipline of writing ADRs at all. Details in §7.

---

## 1. Project Structure

### Layout

Nine projects under `Src/`, ~36k lines of F# (of which ~19k is tests):

```
LeoBloom.Domain      →  (nothing)                 3 files,  1,008 lines
LeoBloom.Utilities   →  Domain                    3 files,    147 lines
LeoBloom.Ledger      →  Domain, Utilities        19 files,  1,896 lines
LeoBloom.Portfolio   →  Domain, Utilities        10 files,    847 lines
LeoBloom.Reporting   →  Domain, Utilities,       12 files,    937 lines
                        Ledger
LeoBloom.Ops         →  Domain, Utilities,       15 files,  1,568 lines
                        Ledger
LeoBloom.CLI         →  all of the above         16 files,  4,169 lines
LeoBloom.Migrations  →  (nothing — standalone)    1 file,      94 lines
LeoBloom.Tests       →  all                      63 files, 19,000+ lines
```

### What's right

The dependency graph is a **tree, not a lattice**. `Ops → Ledger` is one-directional
and enforced by the `.fsproj` files; `Ledger` genuinely does not reference `Ops`.
ADR-002 chose domain-based splitting over layer-based splitting and gave a coherent
reason ("every feature touches three projects" under layering). The code honours it.
This was the right call and SonOfLeo should keep it.

`LeoBloom.Migrations` correctly has **zero project references** and builds its own
connection string (`Migrations/Program.fs:9-11`, with a comment explaining why).
Schema management is properly isolated from the application. Keep this.

`LeoBloom.Domain` has **no `PackageReference` at all** — not even Npgsql. It is
provably pure. That is a genuine achievement in a data-heavy application and the
single best structural property of the codebase.

### What's wrong

**1.1 — `LeoBloom.Reporting` has a hidden dependency on `LeoBloom.Portfolio`.**

`LeoBloom.Reporting.fsproj` references Domain, Utilities, and Ledger. It does *not*
reference Portfolio. But `LeoBloom.Reporting/NetWorthRepository.fs:29-33` joins five
portfolio tables directly in SQL:

```sql
FROM portfolio.position p
JOIN portfolio.investment_account ia ON ia.id = p.investment_account_id
JOIN portfolio.tax_bucket tb ON tb.id = ia.tax_bucket_id
JOIN portfolio.fund f ON f.symbol = p.symbol
LEFT JOIN portfolio.dim_investment_type it ON it.id = f.investment_type_id
```

Schema access per project, extracted from the SQL literals:

| Project | Schemas touched | Project references |
|---|---|---|
| Ledger | `ledger` | Domain, Utilities |
| Ops | `ledger`, `ops` | Domain, Utilities, Ledger |
| Portfolio | `portfolio` | Domain, Utilities |
| **Reporting** | **`ledger`, `portfolio`** | **Domain, Utilities, Ledger** |

**Consequence:** the compiler cannot see this edge. Rename a column in
`portfolio.fund` and `LeoBloom.Reporting` still compiles cleanly, then fails at
runtime in the net worth report. The project reference graph is not a truthful map
of the coupling. *For SonOfLeo: coupling that exists only inside SQL string
literals is invisible coupling. Either the schema owner exposes a typed read
function, or the dependency goes in the .fsproj.*

**1.2 — Fiscal period close is split across two projects, and the wrong door is
still open.**

`LeoBloom.Ledger/FiscalPeriodService.fs:69` defines `closePeriod`. But closing a
period requires validating open obligations, which is Ops knowledge — so
`LeoBloom.Ops/FiscalPeriodCloseService.fs:25` defines `closePeriodWithValidation`,
which wraps it. The CLI correctly calls the Ops one
(`PeriodCommands.fs:150`).

**Consequence:** `FiscalPeriodService.closePeriod` remains public and callable, and
calling it skips every pre-close check. It is a loaded gun left on the table because
the layering forced the correct entry point into the downstream project. This is
the structural cost of `Ops → Ledger` being one-directional: the operation belongs
to Ledger but its invariants live in Ops.

*For SonOfLeo: when an operation's invariants live downstream of the operation, the
upstream primitive must be made inaccessible — a private module, or an
`internal`-style access boundary, or the operation moves. Leaving both public and
documenting "use the other one" is not a boundary.*

**1.3 — `LeoBloom.CLI` is 4,169 lines and the largest non-test project.**

ADR-003 predicted "CLI projects are thin. Most are a single file per command group."
The single-file-per-group part held. The thin part did not: the CLI is larger than
Ledger and Ops combined (3,464 lines). Two files dominate — `OutputFormatter.fs`
(1,178) and `ObligationCommands.fs` (579). See §2 and §4.

---

## 2. God Types and Accreted Responsibilities

**2.1 — `LeoBloom.CLI/OutputFormatter.fs` (1,178 lines) is the clearest god module.**

It holds, in one file: JSON serializer configuration, 40+ human-readable formatters
for every type in the system, a 21-branch runtime type dispatch, stdout/stderr
writing, and exit-code mapping. Four distinct responsibilities.

The growth pattern is legible in the code. The original design was one function:

```fsharp
let write (isJson: bool) (result: Result<obj, string list>) : int      // line 757
```

By the end there are **24 `write*` functions**, and two of them carry this comment
verbatim:

```fsharp
/// Dedicated write function for Invoice list to avoid F# type erasure
/// issues with generic list pattern matching in formatHuman.
let writeInvoiceList (isJson: bool) (invoices: Invoice list) : int     // line 787
```

`obj`-based dispatch cannot see inside a generic list, so every list-returning
command needed a bespoke escape hatch. The abstraction failed and was worked around
25 times rather than replaced. See §4.1 for the fix.

**2.2 — `LeoBloom.Domain/Ledger.fs` (410 lines) mixes four concerns.**

It contains: persistence entity records (`Account`, `JournalEntry`), pure validators
(`validateBalanced`, `validateCommand`), command DTOs (`PostJournalEntryCommand`),
and **report result types** (`TrialBalanceReport`, `BalanceSheetReport`,
`IncomeStatementReport`, `SubtreePLReport`, `PeriodDisclosure`).

The report types are the odd ones out — and note that *other* report types
(`ScheduleEReport`, `GeneralLedgerReport`, `NetWorthReport`) live in
`LeoBloom.Reporting/ReportingTypes.fs`. There is no principle distinguishing the two
sets. Trial balance is in Domain; net worth is in Reporting. Both are reports.

**Consequence:** "where does this type go?" has no answer, so it goes wherever the
author was working that day. The split will keep drifting.

**2.3 — `ObligationPostingService.postToLedger` is a 130-line pyramid.**

Maximum indentation by file (leading spaces):

| File | Max indent | Approx. nesting |
|---|---|---|
| `LeoBloom.Ops/ObligationPostingService.fs` | 48 | 12 levels |
| `LeoBloom.Ops/BalanceProjectionService.fs` | 44 | 11 levels |
| `LeoBloom.Ledger/JournalEntryService.fs` | 42 | 10 levels |
| `LeoBloom.Ledger/OpeningBalanceService.fs` | 41 | 10 levels |
| `LeoBloom.Ops/ObligationInstanceService.fs` | 40 | 10 levels |

Root cause, and this is worth stating plainly: **the codebase uses `Result` as a
return type but never as a monad.** A grep for `Result.bind`, `result {`, `>>=`, or
any `ResultBuilder` across all non-test source returns **zero hits**. Every single
sequencing step is written as a hand-rolled `match`, so each additional
"load-a-thing-and-check-it" step costs one more indentation level.

`postToLedger` performs eleven sequential fallible steps (load instance → check
active → check status → check amount → check confirmed date → load agreement →
check source account → check dest account → load fiscal period → idempotency check
→ post → transition), and pays a nesting level for each.

*For SonOfLeo: adopt a `Result` computation expression (or FsToolkit.ErrorHandling)
on day one. This one decision flattens the five worst files in the codebase to
roughly two levels each.*

**2.4 — Minor: `TestHelpers.InsertHelpers` has combinatorial insert functions.**

`insertAccount`, `insertAccountWithParent`, `insertAccountWithSubType`,
`insertAccountWithParentAndSubType` (`TestHelpers.fs:40-88`). Each new optional
column doubles the function count. A single record-with-defaults parameter would
collapse all four.

---

## 3. Database

### Schema shape

Three schemas — `ledger`, `ops`, `portfolio` — created by 27 Migrondi migrations in
`Src/LeoBloom.Migrations/Migrations/`. Core ledger tables:

- `ledger.account_type` — five seeded rows (asset/liability/equity/revenue/expense)
  with `normal_balance`
- `ledger.account` — code, name, `account_type_id` FK, self-referencing `parent_id`,
  nullable `account_subtype` varchar, nullable non-unique `external_ref`
- `ledger.fiscal_period` — `period_key`, date range, `is_open`, close metadata
- `ledger.journal_entry` — header, `fiscal_period_id` FK, `voided_at`/`void_reason`,
  `adjustment_for_period_id`
- `ledger.journal_entry_line` — `account_id` FK, `numeric(12,2)` amount,
  `entry_type varchar(6)`
- `ledger.journal_entry_reference` — polymorphic `(reference_type, reference_value)`
  pair
- `ledger.fiscal_period_audit` — close/reopen audit trail

The double-entry model itself is textbook and correct: header/line separation,
positive amounts with an explicit debit/credit discriminator (rather than signed
amounts), void-not-delete, `ON DELETE RESTRICT` everywhere.

### What's right

**Migrations are genuinely reversible.** Every migration has a real `DOWN` script,
including the hard ones. `1712000019000_EliminateLookupTables.sql` drops four lookup
tables and its `DOWN` recreates them *with seed data* and backfills the FK columns
from the varchar values. `1712000022000_ReplaceParentCodeWithParentId.sql` migrates
`parent_code` → `parent_id` and back, with backfill in both directions. Most projects
write `-- no rollback` and move on. This is real discipline; keep it.

**Indexes were added deliberately** (`1712000020000_AddSecondaryIndexes.sql`),
including a partial index `ON ledger.journal_entry (id) WHERE voided_at IS NULL`
that matches the actual query shape.

### What's wrong

**3.1 — ADR-001's premise is false, and the consequence it predicted has occurred.**

ADR-001 (`Documentation/ADR/ADR-001-no-business-logic-in-db.md`) removes all CHECK
constraints on enum columns. Its rationale:

> LeoBloom has a single entry point through the application. The CLI (P036-P042)
> will be the only interface. There is no direct SQL access in production, no REST
> API, no secondary consumers hitting the database.

And its stated trigger for revisiting:

> If the architecture changes (multiple consumers, direct DB access), this decision
> should be revisited.

The architecture changed. In `/mnt/media/BusinessRecords/LeoBloomOps/Scripts/`:

| Script | Writes directly to |
|---|---|
| `Imports/trowe.py:413` | `INSERT INTO portfolio.position` |
| `Imports/fidelity_positions.py:183` | `INSERT INTO portfolio.position` |
| `Imports/healthequity.py:515` | `INSERT INTO portfolio.position` |
| `Property/generate_bills.py:459` | `INSERT INTO ops.invoice` |

Grep for `leobloom ` invocations across the same tree returns **zero** — none of
these scripts go through the CLI.

The invoice case is the sharpest. `LeoBloom.Domain/Ops.fs` defines
`InvoiceValidation.validateCommand`, which enforces
`validateTotalEqualsComponents` (total must equal rent + utility), two-decimal
rounding, tenant length, and a positive fiscal period. `generate_bills.py` inserts
into `ops.invoice` with none of it. The rule exists, is tested, and is bypassed in
production.

**3.2 — There is a fourth schema that the migration chain has never heard of.**

`/mnt/media/BusinessRecords/LeoBloomOps/Scripts/Imports/create-stage-schema.sql`
creates a `stage` schema in `leobloom_prod`. Its own header says:

```sql
-- Hobson-owned. Not part of BD's migration chain.
-- Run against leobloom_prod as leobloom_hobson.
```

And its tables carry foreign keys *into the migrated schema*:

```sql
journal_entry_id      int          REFERENCES ledger.journal_entry(id),
```

**Consequences, both concrete:**
- Rebuilding the database from `LeoBloom.Migrations` produces a database the
  importers cannot run against. The migration chain does not describe the database.
- The dependency runs `stage → ledger`, so a ledger schema change can silently break
  out-of-repo Python. Nothing in the F# build will notice.

**3.3 — Enums-as-varchar has already drifted.**

`1712000019000_EliminateLookupTables.sql` replaced FK-constrained lookup tables with
bare varchars (`obligation_type`, `cadence`, `payment_method`, `status`). No CHECK
constraint replaced the FK, per ADR-001.

The evidence that this drifted is migration `1712000025000_AddIrregularCadence.sql`:

```sql
UPDATE ops.obligation_agreement SET cadence = 'irregular' WHERE cadence = 'tri_annual';
```

`'tri_annual'` was never inserted by any migration and appears in no F# `fromString`.
It entered production by hand and had to be cleaned up by a migration. That is
precisely the failure mode ADR-001 accepted, occurring within three weeks of the
decision.

**3.4 — The schema is inconsistent with itself about enums.**

Within `ledger` alone there are two strategies for the same problem:
- `account_type` — remains a lookup table with an FK from `ledger.account`, modelled
  in F# as a record `{ id; name; normalBalance }` (`Domain/Ledger.fs:19`)
- `account_subtype` — a bare varchar column, modelled in F# as a DU with hand-written
  `toDbString`/`fromDbString` (`Domain/Ledger.fs:24-56`)

Both are closed sets of values. They are stored, typed, and validated in two
completely different ways.

**3.5 — The DU-to-string mapping has an uncontrolled second implementation.**

`AccountSubType.toDbString` exists specifically to produce the DB string. But raw
literals appear in SQL:

- `LeoBloom.Reporting/NetWorthRepository.fs:60` — `WHERE a.account_subtype = 'Cash'`
- `LeoBloom.Reporting/CashFlowRepository.fs:47` — `WHERE cash_acct.account_subtype = 'Cash'`
- `LeoBloom.Reporting/CashFlowRepository.fs:97` — same
- `LeoBloom.Reporting/NetWorthRepository.fs:87` — `WHERE at.name = 'liability'`

Rename the `Cash` case and the compiler updates `toDbString` and every `match`, but
not these four strings. The reports go quietly empty.

**3.6 — Chart-of-accounts knowledge is hardcoded in three incompatible places.**

1. **The seed migration** — `1712000006000_SeedChartOfAccounts.sql`
2. **F# source** — `LeoBloom.Reporting/ScheduleEMapping.fs:16-46` maps 20 literal
   account codes to IRS Schedule E line numbers; `NetWorthRepository.fs:114` defines
   "frozen assets" as `WHERE a.code = '1150'`, a magic number in a SQL string
3. **Production, edited by hand** — per standing practice, COA renames and additions
   are applied as direct `UPDATE`/`INSERT` on `ledger.account`, deliberately not as
   migrations

**Consequence:** adding an expense account to the COA is a direct prod edit, but
making it appear on Schedule E requires editing F#, recompiling, and redeploying.
The two halves of one operation live in different worlds with no link between them.
Nothing detects that code `5175` was added and never mapped.

*For SonOfLeo: report-line mapping is configuration, not code. It belongs in a table
next to the accounts it maps, so a COA change and its reporting treatment are one
transaction.*

---

## 4. Boundaries: CLI ↔ Domain

### The stated design

ADR-003 (`Documentation/ADR/ADR-003-cli-architecture.md`) is precise:

> **The CLI layer is parse → call → format → exit code.** CLI commands do not
> contain business logic. […] The service layer already manages connections,
> transactions, validation, and error handling. […] **The CLI layer never opens
> connections or manages transactions.**

This is a good design. It is also not the one that was built.

### 4.1 — The boundary type is `obj`

```fsharp
let write (isJson: bool) (result: Result<obj, string list>) : int    // OutputFormatter.fs:757
```

Every CLI handler boxes its result: `write isJson (result |> Result.map (fun v -> v :> obj))`.
There are **40 `:> obj` / `box` sites** in `LeoBloom.CLI`. `formatHuman` then
recovers the type with 21 runtime type tests and a `| _ -> sprintf "%A" value`
fallback (`OutputFormatter.fs:726-750`).

**Consequences:**
- The compiler cannot tell you a formatter is missing. A new report type silently
  falls through to `sprintf "%A"` and prints an F# record dump to the user.
- Generic collections are invisible to type-testing, so 24 bespoke `write*`
  functions exist to route around it — with comments admitting as much.
- `formatJson` serializes **the boxed domain record directly** with a camelCase
  policy. So `leobloom account show --json` publishes the field names of
  `LeoBloom.Domain.Ledger.Account` as an API contract to the COYS bots. Renaming a
  domain field is a breaking change to consumers with nothing to catch it.

**The counter-example is in the same repo and it is done correctly.**
`LeoBloom.Reporting/ExtractTypes.fs` defines dedicated wire records with explicit
`[<JsonPropertyName("account_id")>]` attributes — a real, versioned, snake_case
contract decoupled from the domain. That pattern should have been applied to *all*
JSON output; instead it was applied only to the four extract commands.

*For SonOfLeo: one output DU (`type CommandOutput = | Entry of EntryView | Report of ReportView | ...`)
so the compiler enforces exhaustive formatting, and explicit wire types for every
JSON surface. Never serialize a domain record.*

### 4.2 — The error channel is `string`, so the domain knows about the CLI

Errors are `string list` (61 signatures) or, inconsistently, `string` (13
signatures — see §5.3). Unstructured either way. A caller cannot branch on *why*
something failed, and cannot re-render the message for a different consumer.

The result is that useful messages could only be written by putting CLI vocabulary
into the domain. Ten sites:

| File:line | Leaked content |
|---|---|
| `Ledger/TrialBalanceService.fs:48, 71` | `"Cannot use --as-originally-closed on an open period"` |
| `Ledger/IncomeStatementService.fs:43, 66` | same |
| `Ledger/SubtreePLService.fs:48, 74` | same |
| `Ledger/BalanceSheetService.fs:62` | same |
| `Ops/FiscalPeriodCloseService.fs:35, 36` | `"--note is required when using --force"` |
| `Ledger/JournalEntryService.fs:166` | a full copy-pasteable `leobloom ledger reverse --journal-entry-id %d` invocation |

`LeoBloom.Ledger` has no reference to `LeoBloom.CLI` — and yet it knows the CLI's
flag names and command syntax. The dependency is real; it just isn't in the build
graph.

*For SonOfLeo: `Result<'T, DomainError>` where `DomainError` is a DU. The domain
says `PeriodNotClosed of periodKey: string`. The CLI decides that renders as
"--as-originally-closed requires a closed period". This is the single highest-value
change on this list — it fixes a boundary violation, an inconsistency, and a
testability problem at once.*

### 4.3 — The command types are good, and should be kept

`PostJournalEntryCommand`, `CreateObligationAgreementCommand`, `TransitionCommand`
etc. (`Domain/Ledger.fs:169-183`, `Domain/Ops.fs:145-172`) are proper command DTOs
distinct from the entity records. The CLI parses argv into a command, the service
validates and executes it. That half of ADR-003 works and is worth preserving.

Two modelling defects to fix:

**`UpdateAccountCommand` cannot clear a field.** (`Domain/Ledger.fs:190-194`)

```fsharp
type UpdateAccountCommand =
    { accountId: int
      name: string option
      subType: AccountSubType option
      externalRef: string option }
```

The comment says "None on each field means 'don't change this field.'" But
`external_ref` and `account_subtype` are *nullable columns*. `option` here conflates
"leave alone" with "set to NULL", and the repository resolves it in favour of
"leave alone" (`AccountRepository.fs:181-186` only adds a SET clause when
`IsSome`). **There is no way to clear an account's `external_ref` through the
application.** Use a three-state DU: `Unchanged | SetTo of 'a | Clear`.

**`CloseFiscalPeriodCommand` carries a `force: bool` that one of its two consumers
silently ignores.** `Ops/FiscalPeriodCloseService.closePeriodWithValidation` honours
it; `Ledger/FiscalPeriodService.closePeriod` — which receives the same command type
— never reads it. A field that means something to one handler and nothing to another
is a trap.

### 4.4 — Boolean-blind service signatures

```fsharp
let getByPeriodId (txn) (fiscalPeriodId: int) (asOriginallyClosed: bool) : Result<TrialBalanceReport, string>
```

`asOriginallyClosed` is a CLI flag threaded as a positional `bool` through six
service functions. At the call site it reads `getByPeriodId txn id true`. In F#,
with DUs free, this should be `ReportBasis = Current | AsOriginallyClosed`.

---

## 5. Coupling: What Would Be Painful to Change

### 5.1 — Transaction management is welded to the CLI

64 `BeginTransaction` sites in `LeoBloom.CLI`, distributed:

| File | `openConnection` calls |
|---|---|
| `ObligationCommands.fs` | 11 |
| `ReportCommands.fs` | 11 |
| `PortfolioCommands.fs` | 10 |
| `PeriodCommands.fs` | 6 |
| `AccountCommands.fs`, `TransferCommands.fs` | 5 each |
| `LedgerCommands.fs`, `ExtractCommands.fs`, `PortfolioReportCommands.fs` | 4 each |
| `InvoiceCommands.fs` | 3 |
| `DiagnosticCommands.fs` | 1 |

Every one repeats the same eleven-line shape (`LedgerCommands.fs:194-206`):

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

**Consequences:**
- ADR-003's "the CLI layer never opens connections or manages transactions" is
  inverted. Services take `NpgsqlTransaction` as their *first parameter* — the
  entire service API is stated in terms of an Npgsql type.
- No second consumer is possible without reimplementing this. If SonOfLeo ever grows
  an HTTP surface or an in-process agent API, it starts by rewriting 64 blocks.
- Read-only commands open and commit write transactions (`handleShow`,
  `LedgerCommands.fs:210-222`) for no reason.
- The rollback-on-`Error` policy is a per-call-site convention. Nothing enforces it;
  one forgotten `Rollback()` leaks a transaction.

*For SonOfLeo: a `withTransaction : (Txn -> Result<'a,'e>) -> Result<'a,'e>` combinator
that owns commit/rollback, and an abstract connection/transaction type in the service
signatures rather than `NpgsqlTransaction`. Eleven lines becomes one.*

### 5.2 — `DataSource` is a static module with eager initialization

`LeoBloom.Utilities/DataSource.fs` builds its connection string and pool in
module-level `let private` bindings, so both run once at first touch, process-wide.

**Consequences:**
- **You cannot point the application at a second database at runtime.** No dry-run
  against a copy, no side-by-side comparison, no multi-tenant anything, no test that
  exercises two databases.
- Configuration failure surfaces as a `TypeInitializationException` from whatever
  code happened to touch the module first. `Program.fs`'s top-level handler prints
  `ex.Message`, which for a type-initializer failure is the generic wrapper text —
  the real reason is only in the Serilog file sink.
- The `leobloom_dev` safety guard (`DataSource.fs:53-57`) is inside `#if DEBUG`, so
  Release builds have no guard at all. Environment selection then rests entirely on
  the `LEOBLOOM_ENV` environment variable. Given that this application posts to real
  financial records, that is a thin margin.

### 5.3 — Two error types in one codebase

61 signatures use `Result<_, string list>`; 13 use `Result<_, string>`. The split
tracks roughly to reporting services (singular) vs. everything else (list). The CLI
pays for it with `Result.mapError (fun e -> [e])` adapters — 5 in `ReportCommands.fs`,
2 in `AccountCommands.fs`.

### 5.4 — The `id-or-key` problem doubled the service API

The CLI accepts either a fiscal period id or a period key. Rather than resolving once
at the boundary, every reporting service grew two near-identical entry points:

- `TrialBalanceService.getByPeriodId` / `.getByPeriodKey`
- `IncomeStatementService.getByPeriodId` / `.getByPeriodKey`
- `SubtreePLService.getByAccountCodeAndPeriodId` / `.getByAccountCodeAndPeriodKey`

Compare `TrialBalanceService.fs:38-59` and `:61-82`: 22 lines each, byte-identical
except for the resolution step and the error wording. The entire
`asOriginallyClosed` disclosure block — six lines of real business logic — is
duplicated across all six functions.

And the pattern still fails at the edges. `BalanceSheetService` never got a `ByKey`
variant, so `ReportCommands.fs:289-293` reaches around the service layer and calls
`LeoBloom.Ledger.PeriodDisclosureRepository.getDisclosureByKey` directly from the
CLI to resolve the key itself.

*For SonOfLeo: resolve identifiers to a `PeriodId` at the CLI boundary. Services take
one identifier type. This removes six functions and six copies of a business rule.*

### 5.5 — Repositories index result columns by ordinal

Every reader does `reader.GetInt32(0)`, `reader.GetString(2)`, etc.
(`JournalEntryRepository.fs:13-22` and roughly 40 similar sites). The mapping is
positionally coupled to the column order in an adjacent SQL string literal.

**Consequence:** inserting a column into a `SELECT` list silently shifts every
subsequent field. `GetString` on an `int` column throws; `GetInt32` on an adjacent
`int` column does not — it returns the wrong number. In a ledger, that is a silent
wrong balance rather than a crash. Ordinal access is a poor trade in financial code;
use `GetOrdinal(name)` or a mapping library.

### 5.6 — Two performance shapes that will bite at scale

- `JournalEntryRepository.insertLines` (`:43`) issues one round-trip **per line**
  rather than a single multi-row `INSERT`.
- `getEntryById` (`:145`, `:166`) accumulates with `lines <- lines @ [x]` inside a
  `while` loop — O(n²) list append.

Neither matters at Dan's data volume today. Both are the sort of thing that is free
to do correctly the first time and annoying to find later.

---

## 6. Layering: Does Business Logic Leak?

**Mostly no, with four exceptions — and the CLI leak is the significant one.**

### DAL → Service separation: mostly held

Repository/service pairing is consistent across Ledger, Ops, Portfolio, and
Reporting. Of ~25 service modules, **four contain raw SQL**:

- `LeoBloom.Ledger/JournalEntryService.fs:18, 38` — `lookupFiscalPeriod`,
  `lookupAccountActivity`
- `LeoBloom.Ledger/OpeningBalanceService.fs`
- `LeoBloom.Ops/TransferService.fs`
- `LeoBloom.Ops/ObligationAgreementService.fs`

`JournalEntryService.lookupFiscalPeriod` is the notable one: it defines its own
`FiscalPeriodCheck` record because it wants a different projection than
`FiscalPeriodRepository` offers, and writes its own `SELECT` rather than adding a
repository function. `FiscalPeriodRepository` already exists in the same project.

`LeoBloom.Ops/FiscalPeriodValidation.fs` also holds four SQL commands despite being
named as a validation module.

### Service → CLI: leaks in both directions

**Domain knows the CLI:** ten sites embedding flag names and command syntax (§4.2).

**CLI knows the DAL:** three sites bypass the service layer to call repositories
directly:

- `LedgerCommands.fs:163` — `FiscalPeriodRepository.findOpenPeriodForDate`
- `ExtractCommands.fs:126` — `PeriodDisclosureRepository.getDisclosure`
- `ReportCommands.fs:293` — `PeriodDisclosureRepository.getDisclosureByKey`

Each exists because the service layer lacked the exact function the command needed.
Three violations out of sixty-odd handlers is a boundary that mostly held under
pressure — but it did bend, and it bent where the service API was incomplete.

**CLI holds a business rule:** `ReportCommands.fs:283-285` decides that
`--as-originally-closed` requires `--period`. That is arguably argument validation
and defensible at the CLI. Worth noting that the *same* rule family is enforced
inside the services (`"Cannot use --as-originally-closed on an open period"`), so
the rule is split across layers.

### The CLI leak that matters

Connection and transaction lifecycle in 64 CLI call sites (§5.1). This is not a leak
of business logic *into* the CLI so much as a leak of infrastructure *out of* the
services — but the effect is the same: the layer boundary does not hold, and ADR-003
says in writing that it should.

### A silent-failure pattern worth flagging

`LedgerCommands.handlePost` validates all parsed inputs, collects errors, returns
early if any — then, in the success branch, **re-extracts the values with unsafe
defaults**:

```fsharp
let (acctId, amt) = Result.defaultValue (0, 0m) r        // LedgerCommands.fs:148
let entryDate = match dateParsed with Ok d -> d | _ -> DateOnly.MinValue   // :155
let refs = refsParsed |> List.map (fun r -> Result.defaultValue { referenceType = ""; referenceValue = "" } r)  // :157
```

It is correct today only because the guard above cannot be false. If anyone reorders
that function, it posts a journal entry against **account 0 for $0.00 dated
0001-01-01** rather than failing. Parse-don't-validate: the guard should produce the
typed values, not be a gate that the code then walks past and re-derives.

---

## 7. What Works — Preserve These

**7.1 — A pure `Domain` project with zero package references.**
`LeoBloom.Domain.fsproj` declares no NuGet dependencies. The domain is provably free
of I/O. Every validator (`validateBalanced`, `validateCommand`,
`ObligationAgreementValidation`, `InvoiceValidation`) and the entire recurrence-date
generator (`ObligationInstanceSpawning.generateExpectedDates`) is a pure function.
This is the best structural property in the codebase.

**7.2 — Domain-based project splitting (ADR-002).**
The reasoning in ADR-002 is sound and the code honours it. Ledger/Ops/Portfolio are
real fault lines; `Ops → Ledger` is one-directional and enforced by the build. Keep
the shape. Fix the two places where it strains (§1.1, §1.2).

**7.3 — Reversible migrations.**
Every one of the 27 migrations has a working `DOWN`, including data-migrating ones.
`EliminateLookupTables` and `ReplaceParentCodeWithParentId` both round-trip. Hold
SonOfLeo to this standard.

**7.4 — `ExtractTypes` as an explicit wire contract.**
`LeoBloom.Reporting/ExtractTypes.fs` — dedicated records, `[<JsonPropertyName>]`
snake_case attributes, decoupled from domain records. This is exactly right. The
lesson is to apply it to *every* machine-readable surface, not just the four extract
commands.

**7.5 — The double-entry model itself.**
Positive amounts plus an explicit `EntryType` discriminator (not signed amounts).
Header/line/reference separation. Void-not-delete with a mandatory reason.
`ON DELETE RESTRICT` throughout. `numeric(12,2)` — never floating point. Reversing
entries for closed periods rather than retroactive edits. This is correct accounting
modelling and none of it should change.

**7.6 — Idempotency guards on cross-domain posting.**
`ObligationPostingService` checks
`JournalEntryRepository.findNonVoidedByReference txn "obligation" (string inst.id)`
before posting, and on a hit still transitions the instance using the existing JE
(`ObligationPostingService.fs:82-110`). `FiscalPeriodService.closePeriod` and
`.reopenPeriod` are idempotent and skip the audit row on a no-op. That is careful
thinking about re-runs, which matters for cron-driven agents.

**7.7 — Explicit status transition table.**
`Domain/Ops.fs:StatusTransition.allowedTransitions` is a `Map` of legal state
transitions with `isValidTransition`. Declarative, testable, in one place. Textbook.

**7.8 — DB-backed tests over mocks.**
1,100 tests; 55 of 63 test files run against a real PostgreSQL instance, each in a
rolled-back transaction with GUID-prefixed unique test data
(`TestHelpers.TestData.uniquePrefix`) for parallel safety. This catches
constraint violations, SQL errors, and type-mapping bugs that mocks would hide.
Correct call — keep it.

**7.9 — Gherkin specs with a coverage checker.**
`Specs/` holds 40+ `.feature` files across Behavioral/CLI/Ledger/Ops/Portfolio/
Structural, plus `Scripts/check-gherkin-coverage.fsx`. Specs written before code and
mechanically checked for coverage.

**7.10 — ADRs.**
Four ADRs recording decision, rationale, and *consequences*, each naming the trigger
for revisiting. ADR-004 (external_ref not unique) is a genuinely good piece of
domain reasoning — it works through the T. Rowe Price 401(k) and HealthEquity HSA
cases and correctly concludes the constraint would force the data to lie.

The practice is excellent. The gap is that **nothing checks the code against the
ADRs.** ADR-001's premise went stale, ADR-003's central rule was inverted, and both
were discovered by this audit rather than by CI. *For SonOfLeo: an ADR with a
testable claim should have a test. "The CLI opens no connections" is a grep. There
is already a `LogModuleStructureTests.fs` and a `DataSourceEncapsulationTests.fs`
proving this pattern works — extend it to the load-bearing ADR claims.*

---

## Recommended Priority for SonOfLeo

Ordered by value-per-unit-effort, on the assumption that the rewrite starts clean.

| # | Change | Fixes |
|---|---|---|
| 1 | `Result<'T, DomainError>` with a DU error type; CLI owns rendering | §4.2, §5.3, and 10 leak sites |
| 2 | Adopt a `result` computation expression on day one | §2.3 — flattens the 5 worst files |
| 3 | `withTransaction` combinator; abstract the txn type out of service signatures | §5.1 — 64 call sites, unblocks any second consumer |
| 4 | One typed output DU + explicit wire records for all JSON | §4.1 — 24 write functions → 1 |
| 5 | Decide the external-writer question *before* writing ADR-001's successor | §3.1, §3.2 — the whole constraint strategy hangs on it |
| 6 | Report-line mapping (Schedule E, net worth groupings) as data in the DB | §3.6 — decouples COA edits from redeploys |
| 7 | Resolve id-or-key at the boundary; one identifier type into services | §5.4 — removes 6 functions and 6 rule copies |
| 8 | Column access by name, not ordinal | §5.5 — silent wrong balances are the worst failure mode here |
| 9 | Three-state update commands (`Unchanged \| SetTo \| Clear`) | §4.3 — restores the ability to clear nullable fields |
| 10 | Structural tests asserting the load-bearing ADR claims | §7.10 — stops the doc/code gap reopening |

---

## Appendix: Files Cited

**Source**
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Domain/Ledger.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Domain/Ops.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Domain/Portfolio.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Utilities/DataSource.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ledger/JournalEntryRepository.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ledger/JournalEntryService.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ledger/FiscalPeriodService.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ledger/TrialBalanceService.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ledger/AccountRepository.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ops/ObligationPostingService.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Ops/FiscalPeriodCloseService.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Reporting/NetWorthRepository.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Reporting/ScheduleEMapping.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Reporting/ExtractTypes.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.CLI/OutputFormatter.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.CLI/LedgerCommands.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.CLI/ReportCommands.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.CLI/Program.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Migrations/Program.fs`
- `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Tests/TestHelpers.fs`

**Migrations** — `/media/dan/fdrive/codeprojects/LeoBloom/Src/LeoBloom.Migrations/Migrations/`
(27 files; `1712000019000_EliminateLookupTables.sql`, `1712000021000_AddAccountSubType.sql`,
`1712000022000_ReplaceParentCodeWithParentId.sql`, `1712000025000_AddIrregularCadence.sql` cited)

**ADRs** — `/media/dan/fdrive/codeprojects/LeoBloom/Documentation/ADR/` (ADR-001 … ADR-004)

**External writers** — `/mnt/media/BusinessRecords/LeoBloomOps/Scripts/`
(`Imports/create-stage-schema.sql`, `Imports/trowe.py`, `Imports/fidelity_positions.py`,
`Imports/healthequity.py`, `Property/generate_bills.py`)
