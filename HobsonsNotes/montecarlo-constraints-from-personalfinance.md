# Monte Carlo — Constraints on SonOfLeo, Read Out of PersonalFinance

**Written:** 2026-07-26. Source: a read-through of
`/media/dan/fdrive/codeprojects/PersonalFinance` — `Lib/MonteCarlo` (~8k lines),
`Lib/DataTypes`, `MonteCarloCLI`, `ModelTrainer`. Last commit there: 2026-02-20
(`49ff6c0`), plus 23 uncommitted files on top of it. The dual clone at
`ai-sandbox/workspace/PersonalFinance` is at the same commit but clean, and is
therefore strictly older — read the `codeprojects/` copy.

**Purpose.** Dan intends to rebuild the Monte Carlo retirement simulator *on top of*
SonOfLeo. This document is not a plan for that rebuild and not a defect list for the
C# code. It exists to answer one question: **what must SonOfLeo avoid foreclosing
now, so the simulator can land on it later without a demolition?**

**Status of the C# repo: frozen, reference only.** Dan's position, 2026-07-26: the next
time this functionality runs, it runs in F#. PersonalFinance is therefore a specimen to
read, not a system to maintain. Its test project is deliberately broken at
`Lib.Tests/Utils/MathFuncTests.cs` (commit `3272559`) to keep an unattended test run off
the live database; that guard is permanent and should never be lifted. Nothing in §5
below is a to-do.

Annotate inline with `[Dan]...[/Dan]`. Closes audit action item #104a.

---

## 1. What the existing system actually is

Three stacked systems, not one:

| Layer | What it does | Where |
|---|---|---|
| **Scenario generator** | Fits a VAR(3) model to post-1980 monthly S&P / CPI / treasury data, then generates synthetic correlated economic lifetimes | `Lib/MonteCarlo/Var/` |
| **Life simulator** | Steps one simulated life month-by-month from sim start to sim end: interest, payday, debt, rebalance, spend, tax, RMDs | `Lib/MonteCarlo/LifeSimulator.cs` |
| **Optimiser** | A genetic algorithm breeding strategy parameters across six isolated clades, selecting on simulated outcomes | `StaticFunctions/SimulationTrigger.cs`, `ModelTrainer/` |

The intended data flow, per Dan (2026-07-26):

1. Load current financial state from SonOfLeo, in SonOfLeo types
2. Transform into Monte Carlo domain types
3. Run thousands of sims, varying inputs
4. Write results to an **adjacent** database
5. Human reads results, makes decisions, and those re-enter SonOfLeo as ordinary
   journal entries

**The property that makes this safe is that it is a one-way flow with a human in the
only path that writes back.** Everything below is in service of not breaking that.

---

## 2. Constraints on SonOfLeo

### C1 — The read surface must support a consistent point-in-time extract

Step 1 is a snapshot. It must be internally consistent (no torn read across accounts)
and it must be identifiable after the fact, or no simulation run can ever be explained.

Current surface is close: `Account FetchAll`, `Account FetchBalances`,
`JournalEntry FetchByDateRange`, and the as-of-date balance fetch added under
REQ-JE-3.6.2. Between them a whole-position snapshot is expressible.

- **Keep the as-of-date read path.** It is what makes a run reproducible.
- **Keep `ExistingTransaction`.** A multi-verb extract wants one transaction spanning
  the whole read, and that DU case is the seam that permits it.
- **Don't add pagination that breaks snapshot consistency.** If volume ever forces
  paging, it pages *within* one transaction or not at all.

### C2 — The CLI is the seam; the read verbs are the contract

Per the standing no-API decision, the simulator calls the SonOfLeo CLI and parses JSON.
SonOfLeo must never reference the simulator — dependency runs one way, exactly as P1.1
governs inside the solution.

Consequence: anything the simulator needs must be expressible as `<Domain> <Verb>` plus
a JSON payload. That is a real constraint on the read surface, and a healthy one. It
also means the simulator can live in its own repo and solution from day one.

### C3 — `Money` stays strict; the simulator brings its own numeric type

`Money` is a private record over `decimal`, 2dp enforced by rejection, min/max capped
(P2.8). That is correct at a ledger boundary and actively wrong in a simulation hot
loop: decimal arithmetic is roughly an order of magnitude slower than `double`, and
re-proving validation invariants a few million times per run is pure waste on values
already proved at the boundary.

The existing code demonstrates both the right and wrong instinct in one file: the VAR
generator computes in `double` and converts to `decimal` only at the edge
(`Var/VarLifetimeGenerator.cs:81-83`), while everything downstream of it runs `decimal`
end to end.

**The pressure will come to loosen `Money` — a rounding constructor, a "loose" variant,
a bypass for projections. Refuse it.** The conversion is a boundary concern and belongs
in the simulator's own transform layer, which is exactly what step 2 of the flow is for.
If anyone calls this a DRY violation, it isn't: two domains with genuinely different
obligations.

### C4 — Nothing simulated ever writes to the ledger

No route may accept projected, derived, or simulated values. Step 5 is a human posting a
real journal entry through the ordinary write path.

**The corner to watch for:** the day the simulator is good, it becomes tempting to let
it post the recommended rebalance itself. If a verb is ever specced that ingests
simulator output, that is the moment the books acquire a stochastic author. The answer
is no.

Corollary: the adjacent database is a **separate database**, not another schema beside
the ledger. Simulation output is high-volume, derived, and truncate-and-regenerate; the
ledger is none of those things and has a different retention and backup posture.

### C5 — `Context` marks the single-threaded shell

The simulator is the first thing in Dan's world that will run genuinely parallel. Two
facts about SonOfLeo that must not be forgotten when it does:

- `DbTransaction` wraps `NpgsqlConnection` / `NpgsqlTransaction`, neither of which is
  thread-safe. A `Context` carrying a live transaction must never cross a thread
  boundary.
- `LookupCache` is mutable by construction — load-all at init, load-one on miss (D5,
  #124a). Two threads missing simultaneously is a race.

**Adopt the rule now, while it costs nothing: anything taking a `Context` is shell and
runs single-threaded. Anything that doesn't is core and may be parallelised freely.**
The extract and load phases take a Context. The compute phase must not — and if a
simulation function's signature ever grows one, that is the alarm bell, because it means
either it is reaching for the database mid-computation or it is carrying a transaction
it must not touch.

This rule has a pleasant side effect: it gives `Context`'s presence in a signature real
information content, which partly answers the objection that a context container makes
signatures less legible.

### C6 — Tax constants are year-versioned data, not code

**The existing tax forms are 2024 vintage** and the constants are `static readonly`
fields: federal standard deduction `29200`, NC standard deduction `25500`, SS worksheet
thresholds `32000` / `12000`, Schedule D capital loss limit `-3000`. The abandoned
BRD-0001 work was inflating these by a CPI multiplier at point of use — which is a
reasonable *simulation* approximation and is not a tax *fact*.

Those are two different things wearing one name, and the distinction matters because
they have different owners:

- **Actual constants for years that happened** — real tax law, needed for real
  reporting. LeoBloom's stated purpose #1 is business-expense tracking for tax
  reporting, so this is SonOfLeo's problem eventually, not only the simulator's.
- **Projected constants for years that haven't** — a modelling assumption, owned
  entirely by the simulator, and legitimately CPI-inflated.

**The constraint:** if SonOfLeo ever grows a tax capability, tax rules enter as
year-and-jurisdiction-keyed *data*, not as a static class. Build it that way and the
simulator can share the engine, supplying projected rows for future years while the
ledger uses actual rows for past ones. Build it as constants again and the two use cases
fork permanently.

This is the single largest piece of unbudgeted work in the eventual port. The 2024 forms
are not a small refresh.

### C7 — Open question: the simulator needs data the ledger stopped tracking

Flagging rather than resolving. The simulator consumes investment positions and cost
basis to compute capital gains. Current posture on the finance side is that investment
transactions, paycheck line items and inter-account transfers are no longer tracked in
the transaction table, and IRAs are excluded from the ledger entirely (portfolio schema
only).

That is a deliberate decision and probably still the right one — but it means step 1's
extract cannot come wholly from the ledger. Either the simulator reads the portfolio
schema alongside SonOfLeo, or part of the snapshot is out-of-band.

**Worth settling before the read surface hardens, not after.**

---

## 3. Requirements for the adjacent database

Cheap to build in now, unreconstructable later. Every simulation run gets a **run
header** recording:

- the as-of date of the SonOfLeo snapshot it consumed
- the RNG seed (see below — the existing system already has this right)
- the parameter set / model identity
- the code version that produced it

Without this, "why did last month's run disagree with this one" is unanswerable, and it
will be asked at a moment when the answer matters.

---

## 4. What to salvage from the C# code

| Asset | Where | Note |
|---|---|---|
| **VAR fitter + lifetime generator** | `Lib/MonteCarlo/Var/` | The crown jewel. Cholesky-decomposed residuals so the three series shock together; Ornstein-Uhlenbeck mean reversion on the treasury rate with the raw delta fed back into the lag buffer so reversion isn't double-counted; floor/ceiling clamps from real historical extremes. Already numeric and pure — ports to F# nearly verbatim. |
| **The 1980 structural-break rationale** | `StaticFunctions/Pricing.cs:22-54` | Fifty lines explaining *why* the cutoff exists — Bretton Woods, Volcker, price controls, oil shocks. Carry the comment across with the code. |
| **Deterministic seeding** | `Var/VarLifetimeGenerator.cs:16` | `new Random(lifeIndex)` — same life index, same economic history, forever. The reproducibility problem is already solved; don't re-solve it. |
| **Tax forms** | `Lib/MonteCarlo/TaxForms/` | Form 1040, Schedule D, qualified dividends worksheet, SS benefits worksheet, NC D-400. Pure functions over numbers, well tested, mechanical to port — but see C6 on the 2024 problem. |
| **Percentile function** | `StaticFunctions/Simulation.cs:48` | Matches Google Sheets PERCENTILE.INC. Small, correct, annoying to rederive. |
| **The static-functions-over-records style** | throughout | `Spend`, `Payday`, `Tax`, `Simulation` are already close to what F# wants. The port is less painful than 12k lines suggests. |

**Architectural note for the rebuild:** `LifeSimulator.Run()`
(`LifeSimulator.cs:123-172`) is already a fold — mutable state threaded through ordered
month-steps. In F# that is `List.fold` over a date sequence with an immutable `SimData`,
which deletes an entire class of bug by construction rather than by discipline.

---

## 5. Failure modes the port must not reproduce

These were found while reading and are recorded as evidence, not as debt. The C# repo is
frozen (see header), so none of them will ever be fixed there — their whole value is as a
list of things the F# rebuild must be designed not to allow. Read them as requirements
wearing the costume of bugs.

| # | Finding | Where |
|---|---|---|
| MC-1 | **Static mutable memoisation under `Parallel.For`.** `_hasCalculatedSpendablePay` / `_spendablePay` are computed once per *process* from whichever person and date arrives first; every subsequent life reuses another life's spendable pay to decide its income inflection point. A correctness race, not a crash. | `StaticFunctions/Simulation.cs:12-13` vs `SimulationTrigger.cs:49` |
| MC-2 | **Plain `Dictionary` caches written from parallel threads.** `_varModelCache` and `_hypotheticalPricingCache`. `Dictionary` offers no concurrency guarantee; concurrent writes can corrupt or throw. | `StaticFunctions/Pricing.cs:11,81,98` |
| MC-3 | **The fitness function lives in a SQL `ORDER BY`.** The most consequential business logic in the system — fun points at P50, then bankruptcy rate, then net worth at P50 — is an untestable, untyped string. Same query interpolates `{majorVersion}` and `{clade}` directly. | `SimulationTrigger.cs:210-240` |
| MC-4 | **EF entities are the domain types.** `Model` is simultaneously an EF-mapped table and the thing the GA breeds and mutates. | `DataTypes/MonteCarlo/Model.cs:14-19` |
| MC-5 | **Five months of uncommitted work in the tree.** 23 modified files (+667/-767) from 2026-02-20, mid-Phase-3 of BRD-0001 (inflation tracking through spend, tax constants, Medicare premiums, fun points). Reaches into all seven tax form files and `TaxConstants.cs`, not just the spend functions. Tests written, also uncommitted. `Pipeline/0001-status.md` has Phase 3 at IN PROGRESS. **Lives only in the `codeprojects/` clone** — BD's `ai-sandbox/workspace/PersonalFinance` is at the same commit but clean, so this work exists in exactly one place and is not backed up by a remote. | `codeprojects/PersonalFinance` |

MC-1 matters most, and not only as a defect: the simulation results Dan was pleased with
a year ago may have been computed under it, depending on the `ShouldRunParallel` setting
at the time. Treat the old outputs as indicative, not as a baseline to reproduce.

**What each one demands of the port:**

- **MC-1, MC-2** → no mutable process-level state anywhere the parallel phase can reach.
  Memoisation is either passed in as a value or it doesn't exist. This is the same rule
  as C5, arrived at from the other direction — and the fold-shaped simulator below makes
  it structural rather than a matter of vigilance.
- **MC-3** → the fitness function is a typed, tested F# function. Selection criteria are
  the most consequential logic in the system and must be the most legible.
- **MC-4** → persistence types and domain types are separate, which step 2 of the flow
  already provides for.
- **MC-5** → commit early. Five months of work survived on one disk by luck.

## 5a. Environment separation — the before picture

Worth recording precisely because SonOfLeo already got this right and must not regress.

`Lib/PgContext.cs:52-62` hardcodes `Host=localhost; Username=dansdev;
Database=householdbudget`. There is no test database, no config key, no per-configuration
override. Every test, CLI run and training session connects to the live database as a
write-capable user — and `SimulationTrigger.CleanUpModelAndRunResultsData` issues
`RemoveRange` against it by design.

The test suite happens to be harmless today: zero `SaveChanges`, zero
`TRUNCATE`/`DELETE`/`RemoveRange`, and only two files touch `PgContext` at all, both
reading. But that is a property of what has been written so far, not of the design.

**Note which secret went where.** The password is read from a `PGPASS` environment
variable; the *target database* is a string literal in source. That is exactly backwards
for safety: the password is what you protect from disclosure, the database name is what
you protect from mistakes.

SonOfLeo inverts this correctly — connection string from an environment variable *named*
in appsettings, a literal connection string in config rejected outright (P2.3), and a
distinct env var name per build configuration (REQ-DAL-1.20). That is the property that
lets its integrated tests truncate freely without anyone's stomach turning over. Do not
trade it away for convenience when the simulator needs a second data source.

---

## 6. Summary — the five things not to foreclose

1. A consistent, as-of-dated, single-transaction bulk read (C1)
2. The CLI as the only integration seam, dependency one-way (C2)
3. `Money` strict and unloosened; conversion lives in the simulator (C3)
4. No write path from simulated data into the ledger, ever (C4)
5. Tax rules as year-versioned data if they ever arrive (C6)

Plus one rule to adopt now while it is free: **`Context` in a signature means shell,
means single-threaded** (C5).

And one property already held that must not be traded away: **the target database comes
from the environment, not from source** (§5a).
