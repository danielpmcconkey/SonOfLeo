# Note to Hobson — 2026-08-20a

From BD. You are running the 2026-08-19a audit; this is everything from my side that bears on
it, written so Dan does not have to be the relay. Nothing here needs him to explain it.

Commit `3841857` on `main`. Suite green: 190 isolated + 376 integrated, 0 failing, 0 stubs.
`Checks/run-all.sh` → 9 passed. Traceability invariants 1 and 2 clean, from
`Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` run directly rather than the hook.

## What I closed

Ten items: 6, 10, 14, 15, 17, 19, 20, 23, 24, 36. Statuses and one-line resolutions are in
`Audit/2026-08-19a/action-items.md`. Every assertion I wrote or changed was mutation-tested —
expected value perturbed, test run, failure message read, perturbation reverted. Forty-five
assertions across eleven rounds, all fired for the reason their name claims.

## What is on you

**Item 18 is blocked and it is a spec question, not a test question.** The disposition says to
re-cite `JournalEntryCommentOrchestration.fs`'s no-op test from `REQ-JE-4.9` to `REQ-SYS-6.1`.
Two facts the auditor did not surface:

- `REQ-SYS-6.1` is **waived** in `SystemWide.md` — "This is a general requirement. Testing
  should be enforced by every individual write operation with a no-op possibility. Dan,
  2026-07-06."
- **No test in the repo cites it.** I grepped all of `Tests/`.

So doing it as written makes that test the first citation on a waived requirement. It also
contradicts the waiver's own rationale, which pushes enforcement down to per-entity REQs — and
`REQ-SYS-6.1`'s text names exactly those instances: `REQ-FP-4.1.1`, `REQ-FP-4.2.1`,
`REQ-AC-2.9`, `REQ-FP-2.2`, void-already-voided. Every no-op case in this system has a
per-entity REQ **except comment-update no-op**, which has none. That gap is the real finding.

My recommendation, for whatever it is worth from the test side: add a JE comment-update no-op
REQ in `JournalEntryCrud.md` and cite that. It matches every other no-op in the repo and leaves
the waiver honest. Un-waiving `REQ-SYS-6.1` would also work but changes a ruling of Dan's from
July. Citing it and leaving the waiver in place is the option I like least — it is the one that
puts dirt into the traceability data the next audit reads. Dan rules; the drafting is yours
either way.

**Item 33 is still pending and it is blocking three others.** The ClassificationRule spec does
not exist. Until it does, 34 (re-citing the `REQ-STG-5.3` test) and 35 (auditing classification
coverage) cannot start, and 31/32 are stuck behind the same wall. I can pick up 34 and 35 the
day the spec lands.

## What is on Dan, so do not chase it

**Item 6/23 is half-closed on purpose.** The trial balance sort test now derives depth-first
order from the fixture hierarchy and it demonstrably fires. But the fixture's account codes are
arranged so that a flat alphabetical sort and a depth-first walk produce the *identical*
sequence, so a flat-sort implementation would still pass the test. Closing that needs one
fixture archetype — a parentless account such as `F-5305`, which flat sort places between
`F-5300` and `F-5310` but a depth-first walk places after the whole `F-5000` subtree. A fixture
addition is shared state every other test reads, so it is Dan's call, not mine. Noted at the
bottom of `action-items.md` as well.

**Item 11 is unassigned.** Accepted in the table, blocked on nothing, not in the batch Dan
handed me. The `REQ-STG-4.2` test still wants replacing with a status-transition Theory. I left
it alone rather than assume.

## Two dispositions I did not follow literally — so the next audit does not re-flag them

**Item 10.** The disposition reads "add fetchByCode happy-path assertions in
`Tests/Tests.Integrated/Model/Ledger/Account.fs`, just under the REQ-AC-3.3 fetchById test."
That location came from the auditor citing `Account.fs:298-306` as a *pattern to copy*, not as
a destination — the hollow test it found is the route test in `AccountRoutes.fs`, and the
code-to-ID-to-fetch composition only exists at the route. I did both: the route test now
deserializes `AccountReturn` and asserts code, name and type, and a model-layer test using
`LookupCache.accountCodeToId` sits under the AC-3.3 test as instructed.

**Item 17.** The disposition says model orchestrator. `JournalEntryLine.fetchByAccountId` is a
`Src/Model` function and `JournalEntryLineOrchestration` has no fetch, so the test I wrote sits
in `Tests/Tests.Integrated/ModelOrchestrator/JournalEntryLineOrchestration.fs` — where Dan said,
but one layer above where the function lives. `Tests/Tests.Integrated/Model/Ledger/` has no
`JournalEntryLine.fs`, which is the cleaner home if anyone wants to move it. Flagged to Dan; he
has not ruled.

## Test names

Four new or changed names went in past step 7. Dan saw two of them in advance:

- `REQ-JE-3.4 fetchByAccountId returns every line posted to the account and no others` (new)
- `REQ-JE-4.9 updateFiAndReferenceText rejects no-op when both fields are NoChange` (new)
- `REQ-AC-3.4 fetching by account code returns the account carrying that code` (new)
- `REQ-RPT-1.6 each parent row is immediately followed by its children in code order`,
  replacing "result list is sorted by account code" — the old name claimed the flat sort the
  requirement explicitly calls wrong, and the rename was the auditor's own recommendation

## One thing about your last hand-off

The summary you gave Dan listed the `REQ-STG-4.4` / `9.3` / `9.4` spec rewrites, the 1.1 / 1.2 /
1.4 waivers, `c8e65b2` and `9bdbfda` as news for me. All of it was already in my clone before I
wrote the 2026-08-19a wakeup — I wrote the data-ingestion tests against those rewritten specs.
The only genuinely new item was the `install-hooks.sh` comment fix. Not a complaint; a
calibration note, so you do not spend hand-off space on things I have already consumed. Checking
`git log --oneline <my-last-commit>..origin/main` before writing the summary would scope it
exactly.
