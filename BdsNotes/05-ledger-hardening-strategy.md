# Ledger Hardening Strategy

Dan's strategy, stated 2026-07-31. BD's pushback is in the last section and is
**not** endorsed — it is a set of open questions for Dan to rule on.

## Why

The ledger is the foundation of this application. What we do with accounts, fiscal
periods, and journal entries will have lasting impact. The refactoring has run this
long because the ledger is the keystone of SonOfLeo. Nothing that comes after can be
allowed to break that foundation, so it gets over-tested. Every possible angle.

## Scope — what this campaign is and is not

**In scope:** proving that what is *already implemented* is bulletproof. Accounts,
fiscal periods, journal entries. The campaign ends when that ledger is unassailable.

**Out of scope:** rules Dan knows he still needs to implement. Those live in the audit
backlog (`Skills/SonOfLeoRequirementsAudit/Runs/2026-07-06a/action-items.md`, 93
CONFIRMED + 5 DEFERRED as of 2026-07-31). Dan owns that list, remembers the journey,
and believes most of it is obsolete after the last two weeks of refactoring. He
circles back after this campaign and decides what still needs building — and that work
then gets treated as foundation, held to the same bar.

**Standing rule:** Phase 3 will keep surfacing "here is a rule that was never written."
Log it and move on. Do not chase it. The campaign bleeds into the backlog otherwise and
never finishes.

## The four phases

**Phase 1 — a unique error code for every interface contract input.**
`AccountCodeTooLong` works across multiple routes (fetch account by code, update
account, and so on) but it does not distinguish the account code from the parent
account code. That is the granularity target: the error names the field position,
not just the primitive type.

- **1A — identification of gaps.** BD's work.
- **1B — coding each.** Dan's work.

**Phase 2 — every error code has a test.** Including backstops, which should be
possible to produce directly. Dan reserves judgment case by case.

**Phase 3 — every observable behavior has an REQ.**

**Phase 4 — every REQ has a test, or a waiver.**

## Where this came from

Writing the two multi-fail theories is what surfaced the need for better errors —
Dan added them as he built the theories out. The test pattern is a design
instrument, not just verification. That is the loop worth preserving.

The two theories:

- `Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs` lines 92–190
- `Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs` lines 327–406

## Evidence the granularity gap is live, not theoretical

`AccountRoutes.fs` rows 328–329 test `accountCode` empty and too-long. Rows 336–337
test `accountParentCode` empty and too-long. Both pairs expect the same two error
names. **That theory would pass if the route validated the wrong field entirely.**

## Phase 1A instruments

Two static checks over the `InlineData` attributes. No runtime, no database.

1. **Ambiguity detector.** Within one theory, two rows with different `field` values
   expecting the same error name = a granularity gap. This is the direct finder.
2. **Field enumeration.** Every field on the input record needs at least one row.
   The ambiguity detector cannot see a field that is not tested at all —
   `AccountRoutes.fs` has input-builder branches for `journalEntryId` and
   `description` with no rows driving them.

## Known debt in the existing pattern

- `AccountRoutes.fs` line 399 handles `DalResultantRowsDidntMatchExpectation`; no
  `InlineData` row produces it. Dead branch. The hand-maintained `elif` chain has
  already drifted from the row list, two examples in.
- The `elif` chains (JE lines 164–183, Account lines 390–403) restate the case name
  the runtime already knows. `FSharpValue.GetUnionFields` collapses each to one
  comparison. Cost: no reflection exists anywhere in `Src/` or `Tests/` today, so
  this introduces a new idiom — a CodeReviewer conversation, not a free win.
- `AccountRoutes.fs` line 344 carries `// todo: ask claude to provide the correct
  REQ #`, and the test is named `REQ-JE-3.9` on an Account route.

## BD's pushback — open, unresolved

**1. Case explosion vs. payload. Decide before 1B, not during.**
98 `AppError` cases today. Field-position granularity across every input contract
could push that several times higher. The alternative is a case that carries the
field path as data rather than a new case per field. That fork determines whether
Phase 1B is fifty edits or several hundred, and it changes the test helper —
comparing the case name alone stops being sufficient.

**2. Phase 3 is arguably in the wrong position.**
Phases 1 and 2 create a large amount of new observable behavior. Phase 3 then
retrofits REQs to behavior that already exists, which produces requirements that
describe the implementation rather than state intent — and a requirement derived
from code cannot catch that the code is wrong. Writing the REQ in the same edit
that adds the error code costs almost nothing; deriving it three phases later means
reconstructing intent. This may collapse Phases 3 and 4 into 1 and 2.

**Resolved 2026-07-31 — objection withdrawn.** Two answers. First, iterating per use
case puts Phase 3 days after Phase 1 for a given slice, so intent is still fresh.
Second, and decisive: surfacing unimplemented rules is not this campaign's job. Dan
holds that list. Retro-spec's blindness to omission does not matter when omissions are
tracked elsewhere by the person who remembers why they were deferred. Dan's other
counter also stands — you cannot know the right granularity until you have built it.

**3. Phase 2 and the backstops pull against each other.**
Dan's stated goal elsewhere is to *remove* backstops — they exist because a more
meaningful error has not been built yet. If Phase 1 succeeds, most backstops become
unreachable, and forcing them into existence for a test is work Phase 1 was designed
to obsolete. Suggested default: delete rather than test, with Dan's case-by-case
judgment as the override.

**4. Phase 1B is a broad `Src/` change gated by the very tests it rewrites.**
The multi-fail theories are the only thing covering that surface, and a global sweep
would invalidate them in lockstep. Safer shape is the loop Dan already found
organically: per route, add the error, update the rows, state the REQ, in one change.
1A stays a global pass; 1B goes incremental.

## Instruments that already exist, and what they are worth

- `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` — 97 of 319 active
  requirements untested and unwaived. The number is unreliable in both directions:
  it grants credit by grepping REQ IDs anywhere under `Tests/` (over-credit), and a
  theory covering eighteen behaviors under a name citing two REQs gets credit for
  two (under-credit).
- `Checks/check-apperror-coverage.sh` — 45 of 98 cases referenced in tests. Same
  defect in a different form: it treats backstops as equally deserving of tests.
- Line coverage (coverlet, free) — the only instrument that can see `Src/` at all
  now that REQ annotations are retired. Finds code no test reaches, which no other
  check here can. Narrow but real; a quarterly run, not a CI gate.
