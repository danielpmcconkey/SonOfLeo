# SonOfLeo — House Patterns (DRAFT — SUPERSEDED)

**Status:** Superseded 2026-07-25 by `patterns.md` (the confirmed canonical document).
This file is retained as the Phase 2 discussion record — Dan's inline dispositions live here.

**How to annotate:** drop `[Dan]your disposition[/Dan]` under any item. Anything in
Part 1 with no annotation will be treated as confirmed as-written when we finalize.
Part 2 items each need an explicit disposition — they're where I have F# commentary
or found inconsistency.

Once confirmed, this file becomes the canonical house-style document. Skills and
guardrails will reference it, not restate it.

---

# Part 1 — Batch-confirm patterns

The things the code does uniformly and that I have no quarrel with. Read, nod, or annotate.

## 1. Architecture & layering

**P1.1 — Five-project layering with one-way dependencies.**
`Utilities` ← `Model` ← `ModelOrchestrator` ← `InterfaceBridge` ← `SonOfLeoCli`.
- **Utilities**: domain-agnostic infrastructure (errors, results, DAL, time, FieldUpdate). References nothing internal.
- **Model**: one module per persisted entity. Owns type definitions, per-field validation, single-entity persistence (insert/fetch/update of its own table). No cross-entity business rules.
- **ModelOrchestrator**: cross-entity workflows and business validation (JE balancing, account deactivation checks, aggregate balance queries). Owns transactions that span entities. Also owns read-model types that don't map 1:1 to a table (`AccountActivity`, `AccountBalance`, `JournalEntry` composite).
- **InterfaceBridge**: JSON contracts, boundary converters (string codes ↔ typed IDs), and command routes. No business logic — it validates *shape*, converts, delegates, converts back.
- **SonOfLeoCli**: a 28-line `main`. Reads stdin, routes `<domain> <verb>`, prints JSON to stdout / error to stderr, exit code 0/1. Nothing else lives here.

**P1.2 — F# compile order is load-bearing and hand-maintained in the `.fsproj`.**
Files are listed in dependency order in each `.fsproj` `<Compile Include>` list. A new file
must be inserted at the correct position, never appended blindly. (Note the orchestrator's
JE files compile Comment/ExternalReference/Line/Header *before* `JournalEntryOrchestration`.)

**P1.3 — The model deals in UUIDs; the boundary deals in human keys.**
Account codes, fiscal period keys, etc. exist for humans. `InterfaceBridge` converts
code→ID on the way in and ID→code on the way out (via `LookupCache`). Model/orchestrator
functions accept and return typed IDs. The blessed exception is documented in code:
`FiscalPeriod.fetchIdByKey` ("should only be used sparingly, as it goes against the doctrine").

**P1.4 — CLI contract.** JSON payload on stdin, `<Domain> <Verb>` args, JSON on stdout,
`AppError.toMessage` on stderr, exit 1 on any `Error`. Domain and verb are case-sensitive.
Routes are declared as `CommandRoute` records (domain, verb, description, input/output
type names, handler) in per-domain lists concatenated in `Program.fs`.

## 2. Centralized infrastructure — "there's already a function for that"

The catalog. BD reinvents any of these at his peril.

**P2.1 — `Utilities.AppError`** — the single application-wide error DU. Every fallible
function returns `Result<'T, AppError>`. Every case carries its context payload; the
companion `AppError.toMessage` is the *only* place error strings live. Cases are prefixed
by domain (`Dal*`, `Account*`, `FiscalPeriod*`, `JournalEntry*`, `Money*`, `InterfaceBridge*`,
`Cli*`). `TestingError of string` exists solely for test plumbing and is **banned in `Src/`**
(stated in code).

**P2.2 — `Utilities.ResultHelper`** —
- `result { }` computation expression (hand-rolled `ResultBuilder`: Bind/Return/ReturnFrom/Zero).
- `convertListOfResultsToResultsList` : `Result<'T,_> list → Result<'T list,_>` (first error wins, order preserved).
- `convertOptionToDesiredTypeWithFallibleConverter` : maps `'a option` through a fallible converter to `Result<'b option,_>`.

**P2.3 — `Utilities.DAL`** — the *only* place Npgsql is touched. Provides:
- `QueryParameterValue` DU (typed params incl. `Nullable*` variants) and `QueryParameter` records — all SQL is parameterized, never value-interpolated.
- `AcceptableExpectedRows` (`Zero | ExactlyOne | OneOrMany | AnyQuantityIsAcceptable`) — every execution declares its row-count expectation and gets `DalResultantRowsDidntMatchExpectation` on violation. This is the optimistic-concurrency backstop (e.g. voiding an already-voided JE updates 0 rows → typed error).
- `DbTransaction` (private record) + `createDbTransaction` / `commitDbTransactionAndDisposeConnection` / `rollbackDbTransactionAndDisposeConnection`.
- `executeNonQuery`, `executeReaderQuery` (mapRaw + constructFromRaw pipeline), `executeScalar` (+ per-type unboxing functions).
- `RowReader` module — typed column getters (`getUuid`, `getStringOption`, …).
- `buildReadQuery` — assembles select/from/join/where/limit/group/order.
- Connection string comes from an env var *named* in appsettings (`ConnectionStringEnvVar`); the DAL refuses a literal connection string in config.

**P2.4 — `Utilities.Clock` / `Utilities.Calendar`** — `Clock.now()` (Instant truncated to
DB-storable precision — the doc comment explains why) and `Calendar.today()` /
`dateFromInstant` (America/New_York). **Never** `DateTime.Now`/`DateTime.UtcNow`/
`SystemClock` directly outside these modules.

**P2.5 — `Utilities.FieldUpdate`** — `NoChange | SetTo of 'a` distinguishes "don't touch"
from "set to null" in updates. Companion converters (plain/fallible × option/non-option).
Update functions build their SET clause by pattern-matching each `FieldUpdate` into an
optional `(sqlFragment, parameter)` pair, `List.choose id`, concat — with a NoOp error
if the list is empty.

**P2.6 — `Model.LookupCache`** — generic `Cache<'K,'V>` (load-all at init, load-one on
miss) with four instances: account code↔ID, fiscal period key↔ID. Used by boundary
converters. Never write a one-off "select unique_id from … where code = …" — it exists.

**P2.7 — `Model.Audit.AuditEnvelope`** — every mutating operation takes an envelope
(`AuditableAction` + Instant + Guid), created at the route handler. `createdAt`/`modifiedAt`
are always stamped from `AuditEnvelope.instant`, never from a fresh `Clock.now()` call
inside the operation — one instant per user action.

**P2.8 — `Model.Money`** — private record over decimal. 2dp enforced (rejects, not
rounds), min/max caps, `add`/`subtractVal1FromVal2`/`sumList`/`splitByN` (remainder to
first share, reconciliation check). All arithmetic on money goes through it.

**P2.9 — `InterfaceBridge.Json`** — single `JsonSerializerOptions` (FSharp + NodaTime
converters) behind `Json.fromJson<'T>` / `Json.toJson<'T>`, both returning `Result`.

## 3. Domain modeling patterns

**P3.1 — Private record + companion module.** Every entity is a record with a `private`
constructor, a companion module of the same name providing: per-field accessor functions
(`let code (a:Account) = a.code`), a `create` (plain constructor over already-validated
component types), and persistence functions. Smart constructors on component types do the
validation; the entity-level `create` composes valid parts.

**P3.2 — Entity ID wrapper.** `type AccountId = private AccountId of Guid` with module
providing `create()` (new Guid), `fromGuid`, `value`. One wrapper per entity. IDs are
generated app-side at construction, never by the DB.

**P3.3 — Validated string types.** Single-case private DU (`AccountCode`, `JournalEntryDescription`,
`CommentText`, …) with `create : string → Result<_, AppError>` that trims first
(REQ-SYS-1.1), rejects empty/whitespace, enforces max length; plus `value` to unwrap.
Max length lives in the module (sometimes as `maxLength`/`max` binding).

**P3.4 — Enum-like DUs with string boundaries.** `AccountType`, `AccountSubtype`,
`JournalEntryLineType`: plain DU + `fromString` (trims, returns Result, case-sensitive) +
`toString` (exhaustive match). Cross-DU rules expressed as total functions
(`AccountSubtype.validFor`, `validWith`, `AccountType.normalBalance`).

**P3.5 — Component-module split.** When an entity file gets big, its component types move
to a sibling `*Component.fs` compiled first (`AccountComponent`, `JournalEntryComponent`).

**P3.6 — `EntryDate` invariant type.** A date that must fall in a known fiscal period is
its own type carrying both the `LocalDate` and the `FiscalPeriodId`, resolvable only via
DB lookup at `create` — plus an `internal createWithFiscalPeriodId` escape hatch for
reconstitution (with a WARNING doc comment).

**P3.7 — `JournalEntry` composite.** Header/lines/references/comments are separate
entities with their own modules; the orchestrator composes them into a private
`JournalEntry` record and enforces collection-level rules (≥2 lines, debits = credits).

## 4. Persistence patterns (per entity module)

**P4.1 — The four-function read stack.** Each entity has:
1. `mapRawForDbRead : RowReader → tuple` — column extraction only, no validation, no logic.
2. `reconstitute : tuple → Result<Entity, AppError>` — rebuilds domain types from trusted
   primitives via smart constructors; **no DB calls allowed inside** (it runs inside an
   open reader — documented in code).
3. `readRowsFromDb` (usually private) [Dan]usually? I probably forgot to add a private on one. They should *always* be private unless there's some dumb edge case that needed me to expose it as public.[/Dan] — fixed select/from + caller-supplied
   predicate/limit/orderBy/parameters/expectedRows/transaction, via `buildReadQuery` + `executeReaderQuery`.
4. Public `fetchByX` functions — build predicate + params, declare expected rows,
   `|> Result.map List.head` for single-row fetches.

**P4.2 — Split-query fetch for composites.** `JournalEntry.fetchFiltered` fetches headers
first (filter applied, deduped after joins), then fetches lines / references / comments
each by **header-ID list** (`fetchByJournalEntryHeaderIdList` with numbered `in (@id1, @id2…)`
params), then composes in memory (`composeFromFetchedLists`). No row-multiplying joins
for child collections.
[Dan]this one is a compromise. I don't like how the expected rows enforcement comes before the deduplication.[/Dan]

**P4.3 — Writes assume validated input.** `insertNewToDb` takes a fully-constructed
entity, does a parameterized INSERT with `ExactlyOne`, and states its assumption in a doc
comment. All *new-entity* validation lives in the orchestrator's `constructNewAndSaveToDb`.

**P4.4 — `constructNewAndSaveToDb` naming.** The orchestrator function that validates
the whole, generates the ID, stamps audit instants, inserts, and returns the entity. The
name is uniform across Account / FiscalPeriod / JE header / line / comment / ext-ref.

**P4.5 — Guarded UPDATEs.** State transitions enforce preconditions in the WHERE clause
(`and is_open = @enforcedCurrentValue`, `and voided_at is null`) and rely on `ExactlyOne`
to convert "0 rows" into a typed error. Updates always set `modified_at` from the envelope,
then re-fetch and return the fresh entity.
[Dan]I'm also not a fan of the re-fetch. But it's harmless unless we're doing batch updates, which I have no plans to do. I'd like your thoughts on that.[/Dan]

**P4.6 — Transactions.** Every model/orchestrator function threads
`(transaction: DbTransaction option)`; `None` means autocommit. Multi-step orchestrations
(`JournalEntry.constructNewAndSaveToDb`, `voidJournalEntry`) create the transaction,
run the railroad, commit on Ok / rollback on Error, and dispose either way.
[Dan]I want to change how this works. Right now, the ModelOrchestrator determines when a transaction is needed. That should go up a level to the interface bridge. Transactions should be decided at the use case level and the interface bridge is your 1:1 with use cases. This will come in handy when I eventually do batch imports. Thoughts?[/Dan]

**P4.7 — Dynamic filters.** Optional-filter fetches build
`(clause, parameter) option list |> List.choose id`, concat clauses under `where 1 = 1`,
with every value still a named parameter. SQL string interpolation is used **only** for
structural fragments (clause lists, parameter-name lists, DU-derived literals), never for
user-supplied values.

## 5. Error handling

**P5.1 — Railway everywhere.** `Result<_, AppError>` end to end; composition via the
`result { }` CE; `do!` for unit-returning validations; conversion functions named
`confirmX` / `validateX` return `Result<unit, AppError>`.
[Dan]I seem to have a naming crisis. For functions that only check success or failure, with no return other than unit, I need to pick exactly one of these names. I think "confirm" is probably the better verb.[/Dan]

**P5.2 — Exceptions stop at the impure boundary.** try/with wrapping .NET I/O lives in
the DAL (and Json), converting to `Dal*`/`InterfaceBridge*` error cases; the block comment
explaining this appears at each site. Domain code neither throws nor catches.

**P5.3 — Error translation at the right altitude.** Callers pattern-match specific error
cases to re-brand infrastructure errors as domain errors
(`DalResultantRowsDidntMatchExpectation` → `JournalEntryLineAccountDoesntExist`,
`AccountCodeDoesntMatchAccountId` → `AccountParentCodeInvalid`), passing all other errors
through unchanged.
[Dan]I eventually want to ensure that every AppError has a test. I might even write a test suite just for that purpose. (One place where I'd say it's okay to test the same fail vector multiple times.)[/Dan]

**P5.4 — `failwith` only at composition roots for can't-proceed infrastructure failure.**
`Result.defaultWith (fun e -> failwith (AppError.toMessage e))` appears where a Result
cannot be threaded further: transaction creation at orchestration roots, `LookupCache`
initial load, and test unwrapping. Never in ordinary domain flow. *(See D9 — I want to
talk about the orchestration-root instance.)*
[Dan]In the Src (code base proper) I only allow this on transaction creation. In the Tests, I'm more lax and allow it all over the place. However, I think the one thing that ties both together is that I use this when I want to fail loudly. If a test fixture is faulty, all of my tests are faulty. If a transaction creation is faulty, NOTHING below it should be allowed.[/Dan]


## 6. Style & naming

**P6.1 — Naming.** Modules/types/DU cases PascalCase; functions and record fields
camelCase. Fetches: `fetchById`, `fetchByX`, `fetchAll`. Writes: `insertNewToDb`,
`updateXById`, `constructNewAndSaveToDb`. Converters: `mapRawForDbRead`, `reconstitute`.
Validators: `confirmX` (single predicate), `validateX` (composite). Test helpers:
`createTestXFromPrimitives`, `cleanUpX`.

**P6.2 — Backtick names for boundary converters.** InterfaceBridge converters are named
in prose: ``` ``convert AccountId Option to AccountCodeString Option`` ```. Ordinary
(non-backtick) camelCase names appear for converter *lambdas* (`fallibleConverterAccountCodeToAccountId`).
This convention is InterfaceBridge-only — Model/Orchestrator functions use standard names.
[Dan]The reason I did it this way is because, as a human, I needed a way to rapidly scan the conversions to see if I'd already built what I needed. I will still need to do that when BD takes over code authoring because I fear DRY violations.[/Dan]

**P6.3 — Signature formatting.** Multi-parameter public functions put each parameter on
its own line, indented 8 spaces, each with a type annotation, followed by an annotated
return type on its own line. Short functions may stay on one line. Public/API functions
are near-universally fully type-annotated (a deliberate readability choice; inference is
allowed for small private helpers like accessors and `reconstitute raw`).
[Dan]this is for ease of human scannability.[/Dan]

**P6.4 — Record literals.** Opening brace on the first line, one field per line,
`field = value` with no alignment padding required (though some older blocks align).
Construction of large records via the module `create` function with one argument per line.

**P6.5 — Pipelines.** Data-first piping is the default idiom: `x |> f |> g`, including
single-step pipes for readability (`accountId |> AccountId.value`). Match expressions
prefer `match x with` over `function` except in small lambdas.
[Dan]I actually prefer "function" but I learned about it after I'd written matches everywhere. Though "match" is a very explicit keyword that works perfectly. "function" as a keyword is horrible.[/Dan]

**P6.6 — Comment philosophy.** Three sanctioned kinds:
1. `// REQ-XX-N.N` traceability tags on the line implementing a requirement (ubiquitous, load-bearing — they map code to Specs).
2. `///` doc comments on infrastructure/API functions explaining *purpose and contract* (why it exists, what it assumes), not mechanics.
3. `(* … *)` block comments for local design rationale (impure-boundary explanation, "why no circular-ancestry check").
No narration comments. `// todo:` markers are acceptable breadcrumbs.

**P6.7 — Indentation** 4 spaces; continuation lines 8. No tabs.
[Dan]I'm considering moving to 2-space indentations, but it's a lot of work in a language that signifies white-space[/Dan]

## 7. Test patterns

**P7.1 — Two projects, one criterion.** `Tests.Isolated` = pure functions only, no DB,
module-level `[<Fact>] let`-style tests. `Tests.Integrated` = anything touching the DB
(model persistence, orchestrations, routes, CLI process), class-based tests in
`[<Collection("SharedTestData")>]` sharing one `TestDataFixture`.

**P7.2 — Test naming.** ``REQ-IDs + behavior``: ``` ``REQ-JE-1.12 constructNewAndSaveToDb rejects entry with fewer than 2 lines`` ```.
Every test name starts with the requirement ID(s) it verifies. Section banners
(`// ===== AccountCode =====`) group tests by unit under test.
[Dan]that last bit about grouping tests with large comments is not good to me. I generally like to have a 1:1 between test files and code files. That's my C# thinking persisting, where, in C#, I prefer small classes. So my small classes never resulted in large test files. In F#, your code files map closer to a full domain vs a feature. So I think I want to split out my tests so 1 code file can be supported by multiple test files. That's a future consideration though.[/Dan]

**P7.3 — The fixture.** `TestDataFixture` stages a known world (accounts of every type,
open + closed periods, JEs incl. voided / closed-period / shared-ref cases), exposes both
the staged entities and **derived aggregate counts**, and truncates all ledger tables on
dispose. Fixture account codes are `F-` prefixed; ad-hoc test codes use the REQ ID
(`"AC-4.8"`).
[Dan]It's important to me that these fixtures move forward through time. No hard-wired dates. I'm actually hoping that I'll run theses tests on the first day of the month, when "yesterday" is in a different FP than today, and it shows me that something broke.[/Dan]

**P7.4 — Expected values derive from fixture data, never hard-coded counts.**
`let expected = fixture.Data.accounts |> List.filter … |> List.length` — the recent
rewrite specifically replaced hard-wired constants with fixture-derived values so fixture
growth can't silently break or weaken tests.

**P7.5 — Write-test hygiene, two blessed forms:**
- *Rollback form*: open an explicit transaction, run the operation `(Some transaction)`, assert, `rollbackDbTransactionAndDisposeConnection` in `finally`.
- *Cleanup form* (when the op manages its own transaction, e.g. full JE creation): `let mutable idToCleanUp = None`, capture the ID immediately on success, delete via `_Cleanup` helpers in `finally` (children before parents; helpers take options so they no-op on early failure).
  [Dan]there's a pattern I like, but I'm too lazy to implement. I recently had tests fail because one of them wasn't cleaning up properly. If I had given each entity-to-be-cleaned some unique name, it'd help me locate which test is failing its clean-up. BD can't be lazy, so we should enforce this practice.[/Dan]

**P7.6 — Assertion style.**
- Happy path: run the railroad inside `result { … Assert … }`; end with `match railroad with | Ok _ -> () | Error e -> Assert.Fail (AppError.toMessage e)` so a leaked error *fails with its message* rather than passing silently.
- Sad path: match the **typed DU case** — `| Error (JournalEntryDebitCreditMismatch _) -> ()` — with two mandatory escape arms: wrong error → `Assert.Fail $"Wrong error. {…}"`, and `| Ok _ -> Assert.Fail "Expected failure; got success"` (capturing the ID for cleanup if it accidentally succeeded).
- Assert on domain *values* (names, amounts, dates round-tripped), on membership (`expectedIds |> List.forall (fun id -> fetched |> List.exists …)`), and on counts only in addition to values.

**P7.7 — Route tests vs CLI tests.** Route-level tests call `routeUiCommandForTesting`
(in-process resolver mirroring `Program.fs`) with real JSON payloads, asserting on
deserialized returns and typed errors. A thin `ProgramTests` class exercises the actual
process boundary (`CliExecutor.runCli`: exit codes, stdout payload, stderr message,
case-sensitivity) — process tests verify *plumbing*, not business logic.
[Dan]this enables 2 very important things. 1) I can't step debug through an external process executor, so I try to have very few tests go through the full runCli path. 2) running through the routeUiCommandForTesting allows me to rollback transactions. [/Dan]

**P7.8 — What is deliberately not tested.** Private accessors, `create` pass-throughs,
DAL internals in isolation (exercised via every integrated test), and JSON serialization
per se. Boundary-length tests always test the exact boundary (max accepted, max+1 rejected).

---

# Part 2 — Discussion items (one at a time in Phase 2)

Where I have F# commentary, or where the codebase disagrees with itself. Ordered roughly
by consequence. Each needs a `[Dan]…[/Dan]`.

**D1 — The flat `AppError` mega-DU.**
*Observed:* one DU, ~100 cases, all domains, with a single `toMessage`.
*F# take:* unconventional. The community norm is per-domain error DUs (`DalError`,
`AccountError`…) nested into a top-level union, keeping each module's error surface
local and the giant file from becoming a merge magnet. **But** your choice is defensible
and I'd even say right for this codebase: one error type means the `result { }` CE
composes across layers with zero mapping ceremony, and `toMessage` centralizes all UI
strings. The cost is a file that every feature touches and no compiler help stopping,
say, a Model function returning a `Cli*` error. Verdict: unconventional-but-defensible;
keep, but the guardrails should enforce the naming prefix discipline and "new case +
toMessage arm together, grouped by domain."
[Dan]I'm torn on this one. I wish F# supported breaking out a module across files, but it doesn't. My biggest concern with domain-specific errors is Java. Forcing every function to declare which types of errors it throws and forcing every function to "handle" downstream errors, is a giant waste of time. I'm never handling the error, just sending it back up the stack. To force me to turn an AccountCoreError into an AccountOrchestrationError is horseshit. But yeah. Merge magnet is a legit concern. So is actually working in that file. It sucks. Thoughts?[/Dan]

**D2 — `of unit` DU cases.**
*Observed:* `| DalConnectionStringIsEmpty of unit`, `| AccountUpdateNoOp of unit`, etc.,
requiring `DalConnectionStringIsEmpty ()` at construction and `_` in matches.
*F# take:* this is genuinely fighting the language — nullary cases (`| DalConnectionStringIsEmpty`)
are the idiom and remove the `()` noise. If the uniform `of` shape was deliberate
(pattern-matching symmetry?), say so and it becomes house style; otherwise I'd bless the
nullary form *for new cases* and leave the existing ones alone.
[Dan]good call. I legit didn't think to do this and should've. I made a quick change to DalConnectionStringEnvVarNotFound to test it out. Yeah. way better[/Dan]

**D3 — Hand-rolled result plumbing vs FsToolkit.ErrorHandling.**
*Observed:* your own `ResultBuilder`, `convertListOfResultsToResultsList`
(= `List.sequenceResult`), `convertOptionToDesiredTypeWithFallibleConverter`
(= `Option.traverseResult`).
*F# take:* the community reaches for FsToolkit here. Your versions are small, correct,
and dependency-free — and for an LLM-developed codebase, *owning* the four functions
beats importing three hundred. I'd keep them. [Dan]I don't know what else FsToolkit gives me. I'm generally a fan of "buy over build" and I want to know the paradigmatic tools in case I ever decide to work on an F# team. We should explore this unless it wants to turn my error handling into Java.[/Dan] Two sub-questions: (a) do you want the
long descriptive names blessed as-is (they're greppable and self-teaching — I lean yes), [Dan]I often fight other programmers over my naming conventions. I don't pay by the character, but I do pay by the hour when my report writer mistakes appctn_ct (application count) for appcnt_ct (applicant count) in an Oracle database. F# allowing the backtick function names is brilliant and I plan to use it to my most verbose advantage.[/Dan]
(b) first-error-wins is your semantic everywhere; accumulating validation errors
(Validation applicative) is the alternative — do you ever want "all the reasons this
input is bad" for UI purposes, or is first-error final? This decision affects the CLI's
UX contract, so it's worth deciding once. [Dan]this will become meaningful when I add batch importers. but that's such a different use case that I can handle it through having a Result<JournalEntryHeaderId, AppError> list as opposed to a Result<JournalEntryHeaderId list, AppError>. In just about every other circumstance, I want it to fail fast, and report the fast failure. A roundtrip 3 times through the CLI to fix 3 input errors should be rare.[/Dan]

**D4 — Parameter order is inconsistent — needs one canonical rule.**
*Observed:*
- `Account.insertNewToDb (account) (transaction)` but `JournalEntryLine.insertNewToDb (transaction) (line)`.
- `Account.fetchAll (activeOnly) (transaction)` but `FiscalPeriod.fetchAll (transaction) (openOnly)`.
- fetches: `fetchById (transaction) (id)` — transaction first, consistently.
- orchestrations: envelope and transaction usually last (`… auditEnvelope transaction`), but `AccountDeactivation.deactivateAccount (transaction) (envelope) (explicitEnd) (account)` puts them first, and `JournalEntryVoiding.voidJournalEntry` takes no transaction at all (creates its own) while `updateComment` takes `envelope` first and `transaction` last.
*F# take:* pipeline style wants the "subject" last (`accountId |> Account.fetchById transaction`) —
your fetches already follow this. I propose the canonical rule: **context first
(transaction, then envelope), subject last**; writes taking the entity as the pipeline
subject (`account |> Account.insertNewToDb transaction`). Whatever you pick, this becomes
a review checklist item; I would *not* churn existing signatures now — flag-on-touch.
  [Dan]Agreed. And I do strive for this everywhere. I just have some old code where my mindset was tied to C#. The one gotcha is functions that operate on multiple "subjects". I tend to be lax there, allowing for updateJournalEntryReference to take FI and referenceText before the transaction and audit arguments.[/Dan]

**D5 — `LookupCache`: global mutable caches with eager DB load and `failwith`.**
*Observed:* four module-level `Cache` instances; first touch loads the whole table and
`failwith`s on DB failure; misses fall through to load-one; **nothing invalidates** —
a renamed account code or an account created by another process serves stale/missing
answers for the CLI process lifetime.
*F# take:* global mutable state is the thing F# people avoid, but a per-invocation CLI
process makes the blast radius one command — this is fine *because* the CLI is
short-lived. That assumption is the load-bearing part, and it's undocumented. If SonOfLeo
ever grows a long-running UI host, this becomes a bug factory. I want to (a) document the
process-lifetime assumption in the module, (b) add "cache staleness" to the guardrail
checklist for any future long-lived host.
[Dan]I agree with the documentation intent. At some point, I plan on building a web UI. That will cause me lots of cache invalidation headaches. But there's no good solution to this. If I want to deal in human-readable surrogates at the boundary and UUIDs at the core, this becomes the necessary evil. If there's a better approach, I'm listening.[/Dan]

**D6 — Big positional tuples between `mapRawForDbRead` and `reconstitute`.**
*Observed:* Account threads an 11-tuple; AccountActivity an 18-tuple. Order is the only
thing connecting extraction to destructuring.
*F# take:* works, and your paired-function convention keeps the two sites adjacent — but
an inserted column shifts everything after it, and the compiler only saves you if types
differ. The idiomatic upgrade is an internal "raw row" record (or anonymous record) per
entity. Cheap insurance, mechanical change. I'd bless the record form for *new* entities
and any entity whose tuple exceeds ~8 fields, migrate opportunistically. Your call —
this is exactly the kind of thing BD would otherwise "fix" unprompted.
[Dan]I don't disagree. It's just a function of whether the juice is worth the squeeze. I tend to think my tests should backstop that particular failure. And then there's the fact that you're still dealing in primitives. Take my Account queries for example. They typically pull a varchar from the database for type and subtype, one after the other. If a bad query transposes those two in the select statement, my "we don't like tuples" type will still happily allow it. The code would blow up at the same place as it does today, when I run "Cash" |> AccountType.fromString [/Dan]

**D7 — Backtick prose names outside tests.**
*Observed:* the whole BoundaryConverters layer (P6.2).
*F# take:* the community reserves double-backtick names for tests; using them in
production code is rare and some tooling (C# interop, some analyzers) dislikes it. Here
they're confined to one layer, they read beautifully, and they make converter intent
unmistakable — unconventional-but-defensible, and honestly a good LLM guardrail (the
name *is* the spec). Confirm it's InterfaceBridge-only house style, and confirm the
bracket dialect: I see both ``convert X to Y`` and ``convert [X list] to [Y list]`` —
the square-bracket flavor is newer. Pick one for new code.
[Dan]I prefer the square brackets, with the type, option, string, list keywords all separated by spaces. ``covert [Account code string option list] to [AccountId option list]`` [/Dan]

**D8 — `readRowsFromDb` visibility, and `mapRawForDbRead` visibility, vary.**
*Observed:* private in most modules; public in `JournalEntryHeader` (the JE orchestrator
needs it); `FiscalPeriod.mapRawForDbRead` public, `Account`'s private.
*F# take:* fine mechanically, but the rule should be explicit: **private by default,
widened only when a same-layer composite needs it** — otherwise BD will cargo-cult public.
[Dan]the broader rule is that functions should always be private by default. Obvious interface functions are public without question. Functions whose analogs in other domains are private need to be private unless accompanied by a documented approved-by-Dan rationale[/Dan]

**D9 — `failwith` on transaction-creation failure inside Result-returning functions.**
*Observed:* `JournalEntry.constructNewAndSaveToDb` and `voidJournalEntry` panic via
`failwith` if `createDbTransaction()` fails, despite returning `Result`.
*F# take:* mildly inconsistent — the caller gets a typed error for every failure except
the one where the DB is down, which explodes as an exception the CLI doesn't catch. A
`result { let! transaction = … }` wrapper would keep it on the railway (the
rollback/commit `failwith`s are more defensible — those are genuine "state unknown"
panics). Was "can't even open a transaction = crash" a deliberate policy? If yes I'll
write it down; if no, small fix.
[Dan]It's deliberate. certain things need to fail loudly. If I ever build a web UI, I will probably revisit that.[/Dan]

**D10 — Two structural dialects for component modules.**
*Observed:* `AccountComponent` = one `module AccountComponent` containing all types;
`FiscalPeriodComponent.fs` = namespace with top-level types/modules;
`JournalEntryComponent` = top-level `module …JournalEntryComponent`. Consumers `open`
them differently.
*F# take:* all three work; pick one for future component files (I'd pick the
`JournalEntryComponent` top-level-module form — one `open`, no nesting).
[Dan]I don't understand the point about opening them differently. However, I'm also feeling like I'm on shaky ground with namespace vs module vs type naming so maybe that's contributing to the problem?[/Dan]

**D11 — Duplicated route resolver.**
*Observed:* `Tests.Integrated/InterfaceBridge/_routeResolver.fs` re-implements
`Program.fs`'s `route` (verbatim concat + tryFind). Divergence risk is real but tiny
(and `ProgramTests` covers the real one end-to-end).
*Option:* move `route` into InterfaceBridge (say, `CommandRoute.route commandRoutes`)
and have both Program and tests call it. Or accept the duplication as the cost of keeping
the CLI project trivial. Mild preference for the former; not urgent.
[Dan]If you're talking about the 9 lines of code shared between Src/SonOfLeoCli/Program.fs and Tests/Tests.Integrated/InterfaceBridge/_routeResolver.fs It's a good idea, but I'm not worried about it. My original thought was to have it in the CLI to allow flexibility between CLI, Web UI, reporting, and other interface layer apps. I'm not sure I'll need that flexibility and the fact that I baked it into the tests means that I've probably decided that I won't. But it's not too egregious and it allows me to punt that decision for a while.[/Dan]

**D12 — Test-side `Result.defaultWith (fun e -> failwith (AppError.toMessage e))`.**
*Observed:* verbatim ~60 times across tests.
*Option:* a single `unwrap` helper in each test project's GenericTestProperties
(`let unwrap r = r |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))`).
Pure ergonomics; the current explicitness is also a legitimate choice. Decide so BD
doesn't invent a third way.
[Dan]It's a good call out. But I really don't like code that's only used for testing. Granted I probably have similar DRY violations with my transaction handling. But I also seem to remember there are a few flavors of this, depending on whether I'm in a result CTE or not.[/Dan]

**D13 — First-error-wins semantics of `convertListOfResultsToResultsList`** —
folded into D3(b); listed here so it isn't lost.[Dan]See my comment on D3[/Dan]

**D14 — `Tests.Isolated` contains DB-adjacent names.**
*Observed:* `Tests.Isolated/Model/Ledger/FiscalPeriod.fs` tests only `FiscalPeriodKey.fromString`
(pure — fine), but the file is named for the entity, and `JournalEntryComponent.fs` (isolated)
reaches into `ModelOrchestrator.JournalEntryLineOrchestration.confirmAmountIsPositive`.
Both are pure functions, so the isolation criterion holds — but the *file naming* implies
broader coverage than exists, and an orchestrator import inside an isolated Model test
blurs the layer story. Low stakes; worth a naming rule ("isolated test files are named
for the module whose functions they exercise").[Dan]I need you to give me file and line number examples here[/Dan]

---

# Part 3 — Observations (not patterns; noted while reading)

Bugs/oddities found during extraction. Not Phase 1 scope — parking them so they're not lost.
Say the word and any of these becomes a fix ticket.

- **O1** `Money.splitByN` reconciliation error passes `(sumTotal, amount m)` into
  `MoneySplitFailedReconciliation`, whose `toMessage` destructures `(originalAmount, sumTotal)` —
  the two values render swapped in the message. `Money.fs:49` / `AppError.fs:209`.[Dan]good catch, I fixed it[/Dan]
- **O2** `Money.fromDecimal` returns `Ok (create raw)` — `raw`, not `rounded`; harmless
  today (raw = rounded when Ok) but brittle if the precision rule ever loosens. `Money.fs:26`. [Dan]how is this brittle when the whole intent is to ensure that raw and rounded are identical? We only round so we have a "known good" to compare to.[/Dan]
- **O3** `JournalEntryComment.fetchByJournalEntryHeaderIdList` filters
  `journal_primary_entry_id` only, while `fetchByJournalEntryId` includes secondary.
  `composeFromFetchedLists` matches primary-only, so the composite is internally
  consistent — but a JE referenced only as *secondary* won't show that comment in
  `JournalEntry.fetchFiltered` output. Possibly intended; worth a doc comment either way. [Dan]good catch. I'd think it should be if the ID is referenced as either primary or secondary[/Dan]
- **O4** `AccountActivity.reconstitute` uses `Option.get` on the detail fields inside the
  `Some lid` branch — safe given the SQL shape (left-join nulls travel together), but it's
  the only `Option.get` cluster in the codebase; a comment stating the invariant would
  spare a future reviewer the archaeology.[Dan]good call out. I added the comment[/Dan]
- **O5** `AppError.toMessage` for `DalErrorDuringDecimalOptionUnboxing` says "Database
  error decimal string option unboxing" (typo/copy-paste).[Dan]fixed[/Dan]
- **O6** `JournalEntryHeader.fs` opens `Utilities.ResultHelper` twice.[Dan]fixed[/Dan]
- **O7** `Program.fs` `route` passes `rest` to handlers; all handlers ignore it (`payload _`).
  Presumably reserved for future flags — fine, just noting the convention is "second arg
  reserved, ignore with `_`".[Dan]yep. this is future proofing[/Dan]
- **O8** `EntryDate.create` formats the key with `entryDate.Month.ToString("D2")` while
  `FiscalPeriodCreation` parses with substrings — both fine; the key format lives in three
  places (regex, formatter, parser). A `FiscalPeriodKey.fromDate` would centralize; only
  worth it if a fourth site appears.[Dan]the two are different. FiscalPeriodCreation is trying to discern start and end dates from a type-approved key. EntryDate create is trying to create the key from a LocalDate. I don't see the overlap[/Dan]

---

# Part 4 — Coverage note

Read in full: every file in `Src/` (all five projects), both test projects' fixtures and
infrastructure, and these test classes: AccountComponent, Money, FiscalPeriod,
JournalEntryComponent (isolated); JournalEntryCreation, JournalEntryVoiding, Account,
AccountRoutes, CliExecutor, Program (integrated). The remaining integrated test files
(FiscalPeriod, JournalEntryFetching, AccountBalance/Activity/Creation/Deactivation,
the JE orchestration slice tests, JournalEntry/FiscalPeriod routes) were pattern-sampled,
not line-audited — nothing in them was needed to establish a pattern not already covered,
but if a Phase 2 dispute hinges on one of them, I'll pull the file.
