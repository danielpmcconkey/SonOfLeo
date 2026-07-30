# Documentation Disposition — 2026-07-29

First pass by Hobson. Dan overrides anything he disagrees with by editing this file.
**Nothing has been moved, edited, or deleted.**

## Dan's rule, as applied

| Verdict | Test |
|---|---|
| **WRONG** | Contradicts the code as it stands today. Jettison. |
| **OBVIOUS** | Correct, but any agent reading the code would conclude it unaided. Jettison. |
| **KEEP** | Correct, and a legit confusion vector if BD never sees it. Needs a home. |
| **ARCHAEOLOGY** | Correct as history. Label as history, move out of the working path. |

Sharpening applied throughout: *obvious* means obvious to BD, not to Dan. A rule visible in
one place is obvious. A rule the code follows in five places consistently is **not** —
BD cannot distinguish deliberate convention from coincidence without being told.

## Headline

| | Files | Lines |
|---|---|---|
| Corpus vetted | 103 | 10,146 |
| ARCHAEOLOGY (audit runs) | 30 | ~7,000 |
| Live corpus actually vetted | 73 | ~3,100 |
| Proposed to survive | 34 | ~1,400 |

Roughly 55% of the live corpus goes. Dan's "less than 20% survives" was pessimistic, but
only because CompoundedLearnings is in better shape than `Specs/Conventions/`, which is
close to a total loss.

---

# 1. WRONG — contradicts current code

| Claim | Source | Evidence |
|---|---|---|
| "Every entity type has exactly one private function called `validateThenConstruct`" | `Specs/Conventions/Doctrines.md` §1 | Zero occurrences in `Src/` or `Tests/` |
| "…returns `Result<T, string>`" | `Specs/Conventions/Doctrines.md` §1 | Zero `Result<_, string>` in `Src/`; errors are `AppError` |
| "No record literals may appear anywhere outside `validateThenConstruct`" | `Specs/Conventions/Doctrines.md` §1 | Rule references a function that does not exist |
| "`constructNewAndSaveToDbUsingParentCode` is preferred over `create`" | `Doctrines.md` §2; `CL/coding/descriptive-naming.md` | `create` is the blessed constructor name. Directly contradicts `Conventions/Naming.md` seven lines away |
| "Types are grouped into namespaces by domain slice… `Model.Ledger.Periods`" | `Doctrines.md` §1 | Namespace is `Model.Ledger.FiscalPeriods` |
| "Dan attests that, once the system is complete, he will work with Hobson to create a SonOfLeoAudit skill" | `Specs/Conventions/Traceability.md` | The skill exists, with completed runs |
| "Cleanup is `TRUNCATE … CASCADE` on all ledger tables — no per-entity tracking needed" | `Skills/TestWriter/SKILL.md` | Contradicts the bullet three lines above it, which mandates per-entity cleanup |
| "The dispose-time TRUNCATE is a backstop" | `Skills/TestWriter/SKILL.md` | `IDisposable` removed from the fixture 2026-07-29; truncation runs at staging |
| "Consumable victims… see the void victims and the CLI update victim in `_TestDataStage.fs`" | `TestWriter/SKILL.md`, `references/test-fixture-design.md`, `references/test-patterns.md` | Zero occurrences of "victim" in the fixture. The entities described do not exist |
| "`Dispose(): TRUNCATE CASCADE on all ledger tables`" (architecture diagram) | `TestWriter/references/test-fixture-design.md` | Same as above |
| "`AuditEnvelope.create` captures `Clock.now()` which uses real `DateTimeOffset.UtcNow`" | `TestWriter/SKILL.md` | `Clock.now()` uses `SystemClock.Instance.GetCurrentInstant()` |
| `DAL.rollbackDbTransactionAndDisposeConnection` in every code example | `TestWriter/references/test-patterns.md` | API renamed; the bracket is now `runFuncAndAutoRollback` |
| Assertion shape as a trailing `match railroad with …` | `TestWriter/SKILL.md`, `test-patterns.md`, `PATTERNS.md` P7.6 | Superseded by `railroadWrapper`. **The old form is the exact hazard `railroadWrapper` exists to close** — a `[<Fact>]` returning `Result` is silently discarded by xUnit 2.9.3 |
| "Five-project layering: `Utilities ← Model ← ModelOrchestrator ← InterfaceBridge ← SonOfLeoCli`" | `PATTERNS.md` P1.1 | Eight projects in `Src/`. DataAccessLayer, Context and Logger are absent from the document |
| "`Utilities.DAL` — the only place Npgsql is touched" | `PATTERNS.md` P2.3 | It is its own project, `DataAccessLayer` |
| "`Model.Audit.AuditEnvelope`" | `PATTERNS.md` P2.7 | Now `Logger.Audit` |
| "Transaction ownership belongs to the route handler **[pending: #118a]**" | `PATTERNS.md` P2.3, P4.6 | Shipped in `3ebdb3d`. Marked pending in two places |
| "~20 `of unit` AppError cases migrate on touch" | `PATTERNS.md` P2.1, D2 | Zero remain |
| "The test database is truncated at the end of every test execution" | `Tests/README.md` | Truncation moved to the start, 2026-07-29 |
| "Wrap it all in a try / **with** to ensure clean-up" (×3) | `Tests/README.md` | `with` catches; it does not guarantee cleanup. Code correctly uses `finally` (9 / 5 / 3 across the route test files) |
| "There are exactly four [document species]. Do not invent a fifth" | `Specs/README.md` | `PATTERNS.md`, `CompoundedLearnings/` and `Skills/` were all added without amendment |
| Temporal entry: "no date-only values anywhere in the system" | `Specs/Decisions.md` | Overturned — retracted by a trailing note on the same line |
| NodaTime entry: "its injectable `IClock` is what makes the audit-timestamp requirements testable" | `Specs/Decisions.md` | Rejected in favour of `AuditEnvelope` — retracted by a trailing note |
| "Tests.Integrated.**Infer**faceBridge" | `Tests/Tests.Integrated/InterfaceBridge/README.md` | Typo in the heading |

**Whole files that are majority-wrong and should go entire:**
`Specs/Conventions/Doctrines.md`, `Specs/Conventions/Traceability.md`,
`Skills/TestWriter/references/test-patterns.md`,
`Skills/TestWriter/references/test-fixture-design.md`.

---

# 2. OBVIOUS — true, but the code says it louder

| Claim | Source |
|---|---|
| The four-function read stack: `mapRawForDbRead` → `reconstitute` → `readRowsFromDb` → `fetchByX` | `PATTERNS.md` P4.1 |
| Private record + companion module per entity | `PATTERNS.md` P3.1 |
| Entity ID wrapper per entity (`AccountId` etc.) | `PATTERNS.md` P3.2 |
| Validated string types trim, reject empty, enforce max length | `PATTERNS.md` P3.3 |
| Enum-like DUs get `fromString` / `toString` | `PATTERNS.md` P3.4 |
| Component-module split when a file gets big | `PATTERNS.md` P3.5 |
| 4-space indent, no tabs | `PATTERNS.md` P6.7 — Fantomas owns this and enforces it mechanically |
| Record literal / signature formatting mechanics | `PATTERNS.md` P6.3, P6.4 — same; `check-format` is the authority |
| Data-first piping is the default idiom | `PATTERNS.md` P6.5 |
| "Variables must be obviously named… no single letters outside short lambdas" | `Doctrines.md` §2, `CL/coding/descriptive-naming.md` |
| Isolated vs integrated split is decided by database access | `TestWriter/SKILL.md`, `Tests/README.md`, `PATTERNS.md` P7.1 — the two projects and their contents make this unmissable |
| "Every fallible function returns `Result<'T, AppError>`" | `PATTERNS.md` P2.1, P5.1 — every signature in `Src/` says so |
| Test names start with the REQ ID | `PATTERNS.md` P7.2 — every test name in the repo |
| REQ ID grammar (`REQ-<DOMAIN>-<n>.<n>`, permanent, never reused) | Duplicated in `Specs/README.md` **and** `Conventions/Traceability.md` — keep one, and it should be `Specs/README.md` |

**Borderline — I ruled KEEP, flag if you disagree:**
`P4.8` parameter order (context first, subject last) and `P6.8` visibility default. Both are
followed consistently in code, which by the sharpened test makes them *not* obvious — BD
would read consistency as coincidence. Both are also cheap to state in one line each.

---

# 3. KEEP — correct and a real confusion vector

Grouped by what they'd become. Homes are proposals, not decisions.

## 3a. "There is already a function for that" — the infrastructure index

The single most valuable thing in `PATTERNS.md`, and the only part I'd fight for. It is not
a rule set; it is an **inventory**. BD will hand-roll a lookup query, a result fold, or a
field-update helper because he does not know they exist — and no amount of reading exemplars
tells him what he did not think to look for.

| Item | Currently in |
|---|---|
| `AppError` + `toMessage` is the only place error strings live | P2.1 |
| `ResultHelper` — `result { }`, `convertListOfResultsToResultsList`, `convertOptionToDesiredTypeWithFallibleConverter` | P2.2 |
| `DataAccessLayer` — `QueryParameterValue`, `AcceptableExpectedRows`, `buildReadQuery`, `RowReader` | P2.3 |
| `Clock.now()` / `Calendar.today()` — never `DateTime.Now` | P2.4 (also enforced by `check-clock.sh`) |
| `FieldUpdate` — `NoChange \| SetTo` | P2.5 |
| `LookupCache` — never hand-write a code→ID query | P2.6 |
| `AuditEnvelope` — one instant per user action | P2.7 |
| `Money` — all money arithmetic | P2.8 |
| `InterfaceBridge.Json` — single `JsonSerializerOptions` | P2.9 |

[Dan]then you should also add the mapping functions in Src/Utilities/FieldUpdate.fs[/Dan]

**Proposed home:** one short index. This is also the most checkable thing in the corpus —
a script can verify every named module still exists, which makes it the one document that
cannot silently rot.

[Dan]It seems silly to me to write a script for this. We will have periodic audits that will pick out any egregious stink.[/Dan]

## 3b. Domain knowledge — not derivable from code at all

| Item | Currently in | Verdict |
|---|---|---|
| Money / Price / Quantity / Rate taxonomy | `Specs/Definitions.md`; `CL/gaap-domain/numeric-type-taxonomy.md` | KEEP — dedupe to one |
| Entity vs lookup, two litmus questions | `Specs/Definitions.md` | KEEP |
| Instant vs Date as separate algebras | `Specs/Definitions.md`; `CL/coding/temporal-arithmetic.md` | KEEP — dedupe |
| Cash-basis means cash moved | `CL/gaap-domain/cash-basis-ledger.md` | KEEP | [Dan]isn't this pretty standard GAAP terminology?[/Dan]
| Balance invariant is exact, tolerance-free | `CL/gaap-domain/balance-invariant.md` | KEEP |
| Reconciliation tolerance is domain data, never a code epsilon | `CL/gaap-domain/reconciliation-vs-balance.md` | KEEP |
| Voiding, not reversing — no formal reversal mechanism | `CL/gaap-domain/voiding-not-reversing.md` | KEEP |
| Half-up rounding, explicitly passed (`.NET` defaults to banker's) | `Decisions.md`, `Conventions/Money.md`, `CL/money-arithmetic-boundaries.md` | KEEP — **written 3×, dedupe to 1** |
| Split allocation must sum exactly; residual forced into one part | same three | KEEP — dedupe |
| USD-only, no currency indicator anywhere | `Conventions/Money.md`, `CL/money-type-enforcement.md` | KEEP — dedupe |

## 3c. Rules the code cannot show you

| Item | Currently in | Why it survives |
|---|---|---|
| **The five test forms** | *nowhere* | Dan articulated these 2026-07-29. The single highest-value missing page. A five-branch decision tree, not prose |
| Why `FieldUpdate` has no `Clear` case | `CL/coding/field-update-pattern.md`, `Decisions.md` | The *absence* of a case is invisible in code |
| `create` vs `fromString` — and why they don't unify | `Conventions/Naming.md`, `CL/descriptive-naming.md` | Both appear in code; the distinction reads as arbitrary without the rule |
| F#-vs-SQL validation: the two-condition test | `Doctrines.md` §3, `CL/coding/validation-location.md` | Genuinely non-obvious. **Salvage this from Doctrines before deleting the file** |
| "Does it orchestrate?" — the layer boundary test | `CL/architecture/orchestration-layer.md` | Best statement of the boundary anywhere |
| Interface contracts: return types may be shared, input types never | `CL/architecture/type-taxonomy.md` | Exists nowhere else |
| No `now()` in DB defaults/triggers — app is sole originator | `Conventions/Temporal.md`, `CL/temporal-persistence.md` | An omission, not a presence. Dedupe |
| `--artifacts-path /tmp/sonofleo-build` inside the container | `Conventions/BuildAndEnvironment.md`, `CL/container-build-discipline.md` | Purely operational, unguessable. Dedupe |
| Debug never touches prod; agents never edit config/env/connection chain | `Conventions/BuildAndEnvironment.md`, `CL/debug-release-access.md` | Safety boundary. Dedupe |
| A check verdict is evidence, not truth | `CL/process/a-check-verdict-is-evidence-not-truth.md` | KEEP |
| Checks read the tree; git records the index | `CL/process/checks-read-the-tree-not-the-commit.md` | KEEP |
| The xUnit silent-pass hazard and `railroadWrapper` | *nowhere except commit messages* | **The most important untold fact in the repo** |
| Bullshit-test specimens (5 worked before/after examples) | `Skills/TestWriter/references/bullshit-test-specimens.md` | KEEP unchanged — the best artifact in the corpus |

## 3d. Different reader — auditor, not BD

`CL/articles/audit-conduct/` — 12 articles. All correct, all still useful, none of it in
BD's path. They encode your overrules from the 2026-07-06a audit and stop an auditor
re-litigating settled ground.

**Verdict: KEEP as a set, out of the development path.** Read only by the audit workflow.

## 3e. Live tooling

`Skills/SonOfLeoRequirementsAudit/` README + `resolved-findings.md` + the workflow +
`traceability-audit.sh`. Working tooling, not documentation. KEEP.

---

# 4. ARCHAEOLOGY

| What | Lines | Note |
|---|---|---|
| `Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/` (30 files) | ~7,000 | Per your rule: one place, labelled, never read as authority |
| `Audit/2026-06-13-171150` | — | Same |
| `Specs/Decisions.md` | 64 | Correct as a record; dangerous as authority — two entries already retracted in place. Its live content survives in the gaap-domain articles |
| `HobsonsNotes/`, `BdsNotes/` | — | Already so labelled |

Note on `Decisions.md`: if it becomes archaeology, the append-only rule must be replaced by
a hard one — **an overturned entry gets a new dated entry, never an in-place note.** The
current form states a rule confidently and retracts it in a trailing clause, which is the
one shape BD is guaranteed to misread.

---

# 5. Consequences worth ruling on before anything moves

1. **`PATTERNS.md` dies, minus §2.** Nothing in the code or `Checks/` cites it; only my own
   skills do, and you had never read it. The infrastructure index (3a) is the salvage.

2. **`Specs/Conventions/` dissolves.** Seven files: two are majority-wrong, one is an index
   for a directory that would no longer exist, and the other four are each duplicated in a
   CompoundedLearnings article.

3. **The 2026-07-25 CompoundedLearnings triage inherits a withdrawn authority.** It kept 28
   articles largely on the grounds that "PATTERNS.md references but doesn't spell out." If
   PATTERNS.md goes, that justification goes with it — but in every case I re-checked, the
   article stands on its own merits. No re-triage needed; the reasoning just needs restating.

4. **Every catalog in CompoundedLearnings cites `PATTERNS.md`** in its header, as do both
   CodeReviewer and TestWriter. Six files need rewiring whatever you decide.

5. **`Skills/TestWriter` needs a rewrite, not an edit.** The SKILL and two of its three
   references describe fixture entities that do not exist and an assertion form that is
   actively hazardous. Only `bullshit-test-specimens.md` survives intact.

6. **Two things worth writing that do not exist yet:** the five test forms, and the xUnit
   silent-pass hazard. Both currently live only in commit messages and Dan's head.

[Dan]I've read it all. You can assume I agree with everything that I didn't comment on. Also, in case you lost it, the test forms are:

1. Tests that check the rules around converting primitives to model base types.

3. Tests that check standard model DB interactivity that don't need to write anything to the DB, thus don't need to be cleaned up after.

4. Tests that check model write functions where the model write function doen't need to manage its own transaction (meaning the tests can roll back after the fact)

5. Tests that check model write functions that *do* manage their own transaction, meaning we can't rollback their write ops and need to clean-up manually

6. Tests of the CLI calls themselves that execute in their own process, so we need to clean them up manually.

[/Dan]
