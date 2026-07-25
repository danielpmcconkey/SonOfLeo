# SonOfLeo — House Patterns (CONFIRMED)

**Status:** Confirmed 2026-07-25. Extracted by Hobson from `Src/` and `Tests/` at commit
`ef2c18c`; dispositions by Dan recorded in `HobsonsNotes/patterns-draft.md` (kept as the discussion record).

**This is the canonical house-style document.** Skills, review checklists, and guardrails
reference it; they do not restate it. To amend: raise the pattern with Dan, get a
disposition, update this file. Patterns marked **[pending: #NNNa]** describe the *target*
state; the referenced action item in
`Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md` closes the gap.

---

# 1. Architecture & layering

**P1.1 — Five-project layering with one-way dependencies.**
`Utilities` ← `Model` ← `ModelOrchestrator` ← `InterfaceBridge` ← `SonOfLeoCli`.
- **Utilities**: domain-agnostic infrastructure (errors, results, DAL, time, FieldUpdate). References nothing internal.
- **Model**: one module per persisted entity. Owns type definitions, per-field validation, single-entity persistence (insert/fetch/update of its own table). No cross-entity business rules.
- **ModelOrchestrator**: cross-entity workflows and business validation (JE balancing, deactivation checks, aggregate balances). Owns read-model types that don't map 1:1 to a table (`AccountActivity`, `AccountBalance`, the `JournalEntry` composite).
- **InterfaceBridge**: JSON contracts, boundary converters (string codes ↔ typed IDs), command routes. No business logic — it validates *shape*, converts, delegates, converts back. Owns transaction scope per P4.6.
- **SonOfLeoCli**: a ~28-line `main`. Reads stdin, routes `<domain> <verb>`, prints JSON to stdout / error to stderr, exit code 0/1. Nothing else lives here.

**P1.2 — F# compile order is load-bearing and hand-maintained in the `.fsproj`.**
Files are listed in dependency order in each `<Compile Include>` list. A new file must be
inserted at the correct position, never appended blindly.

**P1.3 — The model deals in UUIDs; the boundary deals in human keys.**
Account codes and fiscal period keys exist for humans. `InterfaceBridge` converts code→ID
inbound and ID→code outbound (via `LookupCache`). Model/orchestrator functions accept and
return typed IDs. The one blessed exception is documented in code:
`FiscalPeriod.fetchIdByKey` ("use sparingly; goes against the doctrine").

**P1.4 — CLI contract.** JSON payload on stdin, `<Domain> <Verb>` args (case-sensitive),
JSON on stdout, `AppError.toMessage` on stderr, exit 1 on any `Error`. Routes are
`CommandRoute` records in per-domain lists concatenated in `Program.fs`. The handler's
second argument (`rest`) is reserved for future flags; handlers ignore it with `_`.

# 2. Centralized infrastructure — "there's already a function for that"

The catalog. Reinventing any of these is a review rejection.

**P2.1 — `Utilities.AppError`** — the single application-wide error DU. Every fallible
function returns `Result<'T, AppError>`. Cases carry their context payload; the companion
`AppError.toMessage` is the *only* place error strings live. Case-name prefixes follow the
domain (`Dal*`, `Account*`, `FiscalPeriod*`, `JournalEntry*`, `Money*`, `InterfaceBridge*`,
`Cli*`); cases stay grouped by domain, and every new case lands with its `toMessage` arm.
`TestingError of string` exists solely for test plumbing and is **banned in `Src/`**.
Rules from review:
- The DU stays **flat** — no per-domain nesting, no wrap/unwrap ceremony at layer boundaries.
- **Payload-less cases are nullary** (`| DalConnectionStringEnvVarNotFound`), never `of unit`. Existing `of unit` cases migrate on touch.
- **`toMessage` never grows a wildcard arm.** Its exhaustive match is the compiler-enforced guarantee that every case has a message.
- Goal (action #125a): every case exercised by at least one test asserting that specific case.

**P2.2 — `Utilities.ResultHelper`** —
- `result { }` computation expression (hand-rolled: Bind/Return/ReturnFrom/Zero).
- `convertListOfResultsToResultsList` : `Result<'T,_> list → Result<'T list,_>` — first error wins, order preserved.
- `convertOptionToDesiredTypeWithFallibleConverter` : maps `'a option` through a fallible converter to `Result<'b option,_>`.
Deliberately hand-rolled; FsToolkit.ErrorHandling is **not** a dependency and won't become
one. (Rosetta stone for F#-team life: ours = `List.sequenceResult` / `Option.traverseResult`.)
First-error-wins is the house semantic — fail fast, report the fast failure. Batch
importers, when they exist, report per-item as `Result<_,_> list`, not by accumulating
errors inside one Result.

**P2.3 — `Utilities.DAL`** — the *only* place Npgsql is touched. Provides:
- `QueryParameterValue` DU (typed params incl. `Nullable*` variants) + `QueryParameter` — all SQL parameterized, never value-interpolated.
- `AcceptableExpectedRows` (`Zero | ExactlyOne | OneOrMany | AnyQuantityIsAcceptable`) — every execution declares a row-count expectation; violations yield `DalResultantRowsDidntMatchExpectation`. This is the optimistic-concurrency backstop.
- `DbTransaction` (private) + create / commitAndDispose / rollbackAndDispose. **[pending: #118a]** a `withTransaction` bracket helper joins them.
- `executeNonQuery`, `executeReaderQuery` (mapRaw + constructFromRaw pipeline), `executeScalar` (+ typed unboxing functions).
- `RowReader` module — typed column getters.
- `buildReadQuery` — assembles select/from/join/where/limit/group/order.
- Connection string comes from an env var *named* in appsettings (`ConnectionStringEnvVar`); a literal connection string in config is rejected.

**P2.4 — `Utilities.Clock` / `Utilities.Calendar`** — `Clock.now()` (Instant truncated to
DB-storable precision; the doc comment explains why) and `Calendar.today()` /
`dateFromInstant` (America/New_York). **Never** `DateTime.Now`/`UtcNow`/`SystemClock`
outside these modules.

**P2.5 — `Utilities.FieldUpdate`** — `NoChange | SetTo of 'a` distinguishes "don't touch"
from "set to null". Companion converters (plain/fallible × option/non-option). Update
functions build their SET clause by matching each `FieldUpdate` into an optional
`(sqlFragment, parameter)` pair, `List.choose id`, concat — NoOp error if the list is empty.
SET-clause items are built **inline as pipeline expressions** via
`FieldUpdate.mapNoChangeToOptionWithConversion` (FieldUpdate → `(sqlFragment, parameter) option`),
one blank line between multiline items — the same idiom as P4.7 dynamic filters. A `match`
never appears as a direct list item (the one shape Fantomas formats unstably; resolved #126a).
Example: `HobsonsNotes/fantomas-pilot/named-clause-example.fs`. The FieldUpdate module also
provides `map` (Option.map analog, stays in FieldUpdate) and the fallible/option converter
family — check there before writing any FieldUpdate plumbing.

**P2.6 — `Model.LookupCache`** — generic `Cache<'K,'V>` (load-all at init, load-one on
miss); instances for account code↔ID and fiscal period key↔ID. Used by boundary
converters. Never hand-write a code-to-ID lookup query — it exists. Design assumption
(action #124a documents it in code): one CLI invocation = one cache lifetime; there is no
invalidation *by design*. A future long-lived host revisits this, with "drop the cache and
query" as the default posture — not invalidation machinery.

**P2.7 — `Model.Audit.AuditEnvelope`** — every mutating operation takes an envelope
(`AuditableAction` + Instant + Guid) created at the route handler. `createdAt`/`modifiedAt`
are stamped from `AuditEnvelope.instant`, never a fresh `Clock.now()` inside the
operation — one instant per user action.

**P2.8 — `Model.Money`** — private record over decimal. 2dp enforced (rejects, does not
round — the internal rounding exists only to manufacture a known-good for the equality
check), min/max caps, `add` / `subtractVal1FromVal2` / `sumList` / `splitByN` (remainder
to first share, reconciliation check). All money arithmetic goes through it.

**P2.9 — `InterfaceBridge.Json`** — single `JsonSerializerOptions` (FSharp + NodaTime
converters) behind `Json.fromJson<'T>` / `Json.toJson<'T>`, both returning `Result`.

# 3. Domain modeling

**P3.1 — Private record + companion module.** Every entity is a record with a `private`
constructor and a same-named companion module providing per-field accessors, a `create`
(plain constructor over already-validated component types), and persistence functions.
Smart constructors on component types do the validation; entity `create` composes valid parts.

**P3.2 — Entity ID wrapper.** `type AccountId = private AccountId of Guid` with
`create()` / `fromGuid` / `value`. One wrapper per entity. IDs are generated app-side at
construction, never by the DB.

**P3.3 — Validated string types.** Single-case private DU with
`create : string → Result<_, AppError>` that trims first (REQ-SYS-1.1), rejects
empty/whitespace, enforces max length; `value` unwraps. Max length lives in the module.

**P3.4 — Enum-like DUs with string boundaries.** Plain DU + `fromString` (trims, Result,
case-sensitive) + `toString` (exhaustive match). Cross-DU rules are total functions
(`AccountSubtype.validFor`, `validWith`, `AccountType.normalBalance`).

**P3.5 — Component-module split.** When an entity file gets big, component types move to
a sibling `*Component.fs` compiled first.

**P3.6 — Module & namespace organization.**
- A companion module sharing its type's name is the F# idiom (FSharp.Core precedent:
  `List`, `Result`, `Option`) — blessed, not weird.
- **Simple entity** (e.g. Money): one file, `namespace <layer path>` (e.g. `namespace Model`),
  containing `type X` + `module X`. The namespace form is required here precisely because
  the type and module share the file's name.
- **Large domain** (e.g. Accounts, Journaling): one domain namespace
  (`Model.Ledger.Accounts`, `Model.Ledger.Journaling`) spanning multiple files, whose
  modules take apt names (`Account`, `AccountComponent`, `JournalEntryHeader`, …).
  Component grab-bag files are `module XComponent` under the namespace.
- Existing file-level-module spellings (`module Model.Ledger.Journaling.JournalEntryComponent`)
  are functionally equivalent and may stay; new files use the namespace form.

**P3.7 — Invariant-carrying types.** A value whose validity depends on other data gets its
own type carrying both (e.g. `EntryDate` = `LocalDate` + `FiscalPeriodId`, resolvable only
via DB lookup at `create`), with an `internal` reconstitution escape hatch bearing a
WARNING doc comment.

**P3.8 — Composites.** Child entities keep their own modules; the orchestrator composes
them into a private composite record (`JournalEntry`) and enforces collection-level rules
(≥2 lines, debits = credits).

# 4. Persistence (per entity module)

**P4.1 — The four-function read stack.**
1. `mapRawForDbRead : RowReader → tuple` — column extraction only; **private**.
2. `reconstitute : tuple → Result<Entity, AppError>` — rebuilds domain types from trusted
   primitives via smart constructors; **no DB calls inside** (runs inside an open reader);
   **private**.
3. `readRowsFromDb` — fixed select/from + caller-supplied predicate/limit/orderBy/params/
   expectedRows/transaction via `buildReadQuery` + `executeReaderQuery`; **private**.
   (Sole widened instance: `JournalEntryHeader.readRowsFromDb`, needed by the JE composite
   fetch — carries a documented rationale per P6.8.)
4. Public `fetchByX` functions — build predicate + params, declare expected rows,
   `|> Result.map List.head` for single-row fetches.
Tuples (not records) carry the raw row: keep `mapRawForDbRead` and `reconstitute`
**adjacent** in the file, columns in **table order**. The round-trip tests backstop
transposition.

**P4.2 — Split-query fetch for composites.** Fetch headers first (filter applied, deduped
after joins), then fetch each child collection by **header-ID list** (numbered
`in (@id1, @id2…)` params), then compose in memory. No row-multiplying joins for child
collections. **[pending: #119a]** row-count expectation is enforced *after* dedup.

**P4.3 — Writes assume validated input.** `insertNewToDb` takes a fully-constructed
entity, parameterized INSERT, `ExactlyOne`, and states its assumption in a doc comment.
New-entity validation lives in the orchestrator's `constructNewAndSaveToDb`.

**P4.4 — `constructNewAndSaveToDb` naming.** The orchestrator function that validates the
whole, generates the ID, stamps audit instants, inserts, and returns the entity. Uniform
across all entities.

**P4.5 — Guarded UPDATEs with re-fetch.** State transitions enforce preconditions in the
WHERE clause (`and is_open = @enforcedCurrentValue`, `and voided_at is null`) and rely on
`ExactlyOne` to turn "0 rows" into a typed error. Updates set `modified_at` from the
envelope, then **re-fetch and return the fresh entity** — the DB's truth, not our
reconstruction of it. Confirmed deliberate; revisit only if set-based batch updates ever
arrive (they'd use `returning` clauses, a different pattern).

**P4.6 — Transactions are decided at the use-case level.** Every model/orchestrator
function threads `(transaction: DbTransaction option)`; `None` means autocommit.
**[pending: #118a]** Transaction *ownership* belongs to the InterfaceBridge route handler
(1:1 with use cases), via the `withTransaction` bracket helper; orchestrator functions are
participants, never owners. (Current code still has `JournalEntry.constructNewAndSaveToDb`
and `voidJournalEntry` creating their own — that is the gap #118a closes.)

**P4.7 — Dynamic filters.** Optional-filter fetches build
`(clause, parameter) option list |> List.choose id`, concat under `where 1 = 1`, every
value a named parameter. SQL string interpolation is for **structural fragments only**
(clause lists, parameter-name lists, DU-derived literals) — never user-supplied values.
`Option.map` pipeline items and `if`-items stay inline in the list (Fantomas handles them
uniformly); separate multiline items with a blank line where boundaries should be
scannable — Fantomas preserves it. Only `match`-items are banned from list literals (P2.5).

**P4.8 — Parameter order canon.** **Context first (transaction, then auditEnvelope),
subject last**, so the subject rides the pipeline: `accountId |> Account.fetchById transaction`,
`account |> Account.insertNewToDb transaction`. Functions operating on multiple subjects
may place the subjects together before the context args. Existing nonconforming
signatures migrate on touch, not in a sweep.

# 5. Error handling

**P5.1 — Railway everywhere.** `Result<_, AppError>` end to end; composition via
`result { }`; `do!` for unit-returning checks. Unit-returning check functions are named
**`confirmX`** — `validateX` is retired (rename sweep: action #123a).

**P5.2 — Exceptions stop at the impure boundary.** try/with wrapping .NET I/O lives in
the DAL (and Json), converting to `Dal*`/`InterfaceBridge*` cases; the explanatory block
comment appears at each site. Domain code neither throws nor catches.

**P5.3 — Error translation at the right altitude.** Callers pattern-match specific cases
to re-brand infrastructure errors as domain errors
(`DalResultantRowsDidntMatchExpectation` → `JournalEntryLineAccountDoesntExist`), passing
all other errors through unchanged.

**P5.4 — Fail loudly, by policy.** `Result.defaultWith (fun e -> failwith (AppError.toMessage e))`
is reserved for failures that invalidate everything downstream:
- In `Src/`: **transaction creation only.** If a transaction can't open, nothing below it
  may be allowed to run.
- In `Tests/`: allowed liberally — a faulty fixture means every test is faulty.
Never in ordinary domain flow. Deliberate; revisit if a web UI host arrives.

# 6. Style & naming

**P6.1 — Naming.** Modules/types/DU cases PascalCase; functions and record fields
camelCase. Fetches: `fetchById` / `fetchByX` / `fetchAll`. Writes: `insertNewToDb`,
`updateXById`, `constructNewAndSaveToDb`. Read stack: `mapRawForDbRead`, `reconstitute`,
`readRowsFromDb`. Checks: `confirmX`. Test helpers: `createTestXFromPrimitives`, `cleanUpX`.
Verbosity in names is a feature, not a cost — names exist to prevent the
`appctn_ct`/`appcnt_ct` class of misreading.

**P6.2 — Backtick prose names for boundary converters.** InterfaceBridge converters are
named in prose so a human (or BD) can rapidly scan for an existing conversion before
writing a duplicate — this convention exists to prevent DRY violations. **Dialect for new
names:** square brackets around each side, keywords spelled out and space-separated:
``` ``convert [Account code string option list] to [AccountId option list]`` ```.
Older bracketless names migrate on touch. InterfaceBridge-only; Model/Orchestrator use
standard camelCase names.

**P6.3 — Signature formatting.** Multi-parameter public functions: one parameter per
line, each type-annotated, annotated return type on its own line. Short functions may
stay on one line. Public/API functions are fully type-annotated — for human
scannability; inference is fine for small private helpers.
**[pending: #127a]** Formatting mechanics defer to Fantomas with the repo `.editorconfig`
(adopted 2026-07-25, pilot in `HobsonsNotes/fantomas-pilot/`): parameter continuations
indent 4, and signatures under 120 chars may collapse to one line.

**P6.4 — Record literals.** Opening brace on the first line, one field per line,
`field = value`. Large records constructed via the module `create` with one argument per line.
**[pending: #127a]** Exact brace/indent mechanics defer to Fantomas; small records
(≤120 chars) stay on one line per `fsharp_max_record_width`. Aligned trailing-comment
columns do not survive formatting — accepted as the cost of mechanical uniformity.

**P6.5 — Pipelines and matches.** Data-first piping is the default idiom, including
single-step pipes for readability. `match x with` is the house default; `function` is
permitted in small lambdas.

**P6.6 — Comment philosophy.** Three sanctioned kinds:
1. `// REQ-XX-N.N` traceability tags on the implementing line (load-bearing — they map code to Specs).
2. `///` doc comments on infrastructure/API functions: purpose and contract, not mechanics.
3. `(* … *)` block comments for local design rationale.
No narration comments. `// todo:` breadcrumbs acceptable.

**P6.7 — Indentation.** 4 spaces, no tabs; continuation depth per Fantomas
**[pending: #127a]**. (2-space migration considered and parked — see Deferred.)

**P6.8 — Visibility.** **Private by default.** Obvious interface functions are public
without question. A function whose analogs in other domains are private must be private —
unless accompanied by a documented, Dan-approved rationale at the definition site.

# 7. Tests

**P7.1 — Two projects, one criterion.** `Tests.Isolated` = pure functions only, no DB,
module-level `[<Fact>] let` tests. `Tests.Integrated` = anything touching the DB,
class-based tests in `[<Collection("SharedTestData")>]` sharing one `TestDataFixture`.
Isolated test files are named for the module whose functions they exercise (cleanup:
action #121a).

**P7.2 — Test naming.** Every test name starts with the requirement ID(s) it verifies,
followed by the behavior: ``REQ-JE-1.12 constructNewAndSaveToDb rejects entry with fewer
than 2 lines``. Section banners group tests within a file for now; the longer-term
direction is splitting test files so one code file may be served by several test files
(deferred — see Deferred list).

**P7.3 — The fixture.** `TestDataFixture` stages a known world (accounts of every type,
open + closed periods, JEs incl. voided / closed-period / shared-ref cases), exposes the
staged entities **and derived aggregate counts**, and truncates all ledger tables on
dispose. Fixture codes are `F-` prefixed; ad-hoc test codes use the REQ ID (`"AC-4.8"`).
**Fixtures move forward through time — no hard-wired dates, ever.** Dates derive from
`Calendar.today()` offsets, deliberately, so month-boundary runs can expose real bugs.

**P7.4 — Expected values derive from fixture data, never hard-coded counts.**
`let expected = fixture.Data.accounts |> List.filter … |> List.length`. Hard-wired
constants were purged in the 2026-07 rewrite; they do not come back.

**P7.5 — Write-test hygiene, two blessed forms.**
- *Rollback form*: open an explicit transaction, run the operation `(Some transaction)`,
  assert, rollback-and-dispose in `finally`.
- *Cleanup form* (when the op owns its transaction): `let mutable idToCleanUp = None`,
  capture the ID immediately on success, delete via `_Cleanup` helpers in `finally`
  (children before parents; helpers take options so they no-op on early failure).
**Every entity a test creates carries a unique, test-identifying name/code** so a failed
cleanup points at its test. Mandatory for BD — laziness is a human privilege.
Tests never mutate fixture data without rollback or self-cleanup (audit item #65).

**P7.6 — Assertion style.**
- Happy path: railroad inside `result { … Assert … }`, ending
  `match railroad with | Ok _ -> () | Error e -> Assert.Fail (AppError.toMessage e)` —
  a leaked error fails with its message, never passes silently.
- Sad path: match the **typed DU case** — `| Error (JournalEntryDebitCreditMismatch _) -> ()` —
  with two mandatory escape arms: wrong error → `Assert.Fail $"Wrong error. {…}"`;
  `| Ok _ -> Assert.Fail "Expected failure; got success"` (capturing the ID for cleanup).
- Assert on domain **values** (names, amounts, dates round-tripped) and membership;
  counts only in addition to values, never instead of them.

**P7.7 — Route tests vs CLI tests.** Route-level tests drive `routeUiCommandForTesting`
with real JSON payloads, asserting on deserialized returns and typed errors. A thin
`ProgramTests` class exercises the true process boundary (exit codes, stdout/stderr,
case-sensitivity). Two reasons this split is load-bearing: the in-process path is
step-debuggable, and it permits transaction rollback. Process tests verify plumbing, not
business logic — keep them few.

**P7.8 — Deliberately untested.** Private accessors, `create` pass-throughs, DAL
internals in isolation (exercised by every integrated test), JSON serialization per se.
Boundary-length tests always test the exact boundary (max accepted, max+1 rejected).

---

# Resolved decisions (Phase 2 record, condensed)

| ID | Decision |
|----|----------|
| D1 | AppError stays one flat DU. Mitigations: prefix discipline, domain grouping, no wildcard in `toMessage`. Java-style per-layer error translation is explicitly rejected. |
| D2 | Payload-less error cases are nullary, not `of unit`. Migrate on touch. |
| D3 | Keep hand-rolled result plumbing; no FsToolkit dependency. Verbose descriptive names blessed. First-error-wins everywhere; batch importers report per-item lists. |
| D4 | Parameter order canon: context first (transaction, envelope), subject last; multi-subject functions may group subjects. Migrate on touch. (→ P4.8) |
| D5 | LookupCache kept as the necessary evil of human-readable surrogates at the boundary; process-lifetime assumption documented (#124a); long-lived host defaults to dropping the cache, not invalidating it. |
| D6 | Positional tuples stay for the mapRaw/reconstitute seam; adjacency + table-column order are the rules; tests backstop transposition. |
| D7 | Backtick converter names: InterfaceBridge-only; square-bracket dialect for new names. (→ P6.2) |
| D8 | Private by default; publicity of an analog-private function needs a documented, Dan-approved rationale. (→ P6.8) |
| D9 | `failwith` on transaction creation is deliberate fail-loudly policy. Revisit with a web UI. (→ P5.4) |
| D10 | Module/namespace rule per P3.6: simple entity = namespace + same-named type/module in one file; large domain = domain namespace with aptly-named modules. |
| D11 | Route-resolver duplication (9 lines, Program.fs vs test resolver) accepted; consolidating is punted deliberately. |
| D12 | Test-side `unwrap` helper approved (#120a); CE and railroad-match flavors unchanged. |
| D13 | Folded into D3. |
| D14 | Misplaced isolated test files to be cleaned up (#121a); isolated-file naming rule in P7.1. Test-file structure rethink deferred. |
| O1–O8 | O1/O4/O5/O6 fixed by Dan in-session. O2, O8 withdrawn by Hobson. O3 → #122a. O7 documented in P1.4. |

# Deferred / future

- **2-space indentation** — considered, parked (whitespace-significant migration cost).
- **Test-file structure** — move toward 1 code file : N test files; needs its own design
  discussion. Framework extensibility is a core tenet — whatever Phase 3 builds must
  accommodate this restructuring without rework.
- **Web-UI host** — reopens: LookupCache strategy (D5), fail-loudly posture (D9),
  route-resolver location (D11).
- **Batch importers** — per-item `Result` list reporting; atomicity via the #118a
  transaction seam.

# Coverage note

Extraction read every file in `Src/` line-by-line, both test projects' infrastructure,
and ten representative test classes; the remaining integrated test files were
pattern-sampled. If a future dispute hinges on an unaudited file, pull the file — don't
argue from this document alone.
