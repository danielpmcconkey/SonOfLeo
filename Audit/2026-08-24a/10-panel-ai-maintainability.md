# agentic-readiness-auditor

## GATE-1 — enforcement-gap
- **Location:** Specs/Behavioral/DataIngestion.md, line 71; Skills/SonOfLeoRequirementsAudit/traceability-audit.sh, line 31
- **Summary:** REQ-STG-2.24 uses inline 'Withdrawn.' where the traceability parser expects 'Stricken.', breaking the traceability gate on main.
- **Resolution:** fix-spec

REQ-STG-2.24 was withdrawn as part of the Option 4 status redesign. Its entry in the active body section (line 71) reads '- **REQ-STG-2.24** Withdrawn. ~~...~~'. Every other inline-dead requirement in this spec (REQ-STG-2.8, 7.4, 9.6) uses the word 'Stricken.' on the same line. The traceability-audit.sh parser (line 31) filters inline-dead requirements using 'grep -iv stricken' — it recognizes 'Stricken' but not 'Withdrawn'. Result: the script counts REQ-STG-2.24 as an active, untested requirement and fails Invariant 2. Verified by running 'Checks/run-all.sh' on HEAD of main: check-traceability FAIL, 1 of 439 active requirements untested. The pre-commit hook skips this check (it is marked SLOW), so commits are not blocked, but the traceability gate — the mechanical guardrail the entire REQ-to-test linkage system rests on — reports a false failure. Any agent or operator running the full audit (or any audit workflow that calls traceability-audit.sh) gets a FAIL for a spec whose withdrawal is correct and complete. REQ-STG-2.24 also appears in the Withdrawn table (line 252), confirming the withdrawal was intentional; the inline marker just used the wrong word.

**Action:** Change 'Withdrawn.' to 'Stricken.' on line 71 of Specs/Behavioral/DataIngestion.md, consistent with every other inline-dead requirement in the spec. Alternatively, update the traceability-audit.sh parser to filter both 'stricken' and 'withdrawn' case-insensitively.

**Why:** The traceability audit is the foundation of the agentic contract: every active REQ is tested, every test citation is valid. A permanently-failing gate degrades trust in the check system — operators learn to ignore it, and agents that gate on it cannot proceed. For BD to write tests on main with the traceability gate green, this must be fixed first.

---

## GUARD-1 — enforcement-gap
- **Location:** Checks/ (absent); Tests/README.md (documents the hazard); Skills/CodeReviewer/SKILL.md, line 101 (manual review only)
- **Summary:** The silent-pass hazard — a result CE in a [<Fact>] without railroadWrapper — has no mechanical check, only manual code review.
- **Resolution:** fix-code

Tests/README.md documents the hazard clearly: 'A [<Fact>] whose body evaluates to Result<_, _> passes unconditionally. xUnit 2.9.3 discards a non-unit, non-Task return value without complaint, so the Error branch never reaches an assertion and the test reports green having verified nothing.' The CodeReviewer skill (Pass 4) includes railroadWrapper as a spot-check item. But neither produces a Checks/ script that enforces the rule mechanically. Every other critical coding discipline in this repo has a check script: clock discipline (check-clock.sh), npgsql isolation (check-npgsql.sh), compile order (check-compile-order.sh), validateX naming (check-confirm-naming.sh), hardwired dates (check-hardwired-dates.sh), AppError wildcard (check-tomessage-wildcard.sh), TestingError in Src (check-testingerror.sh). The railroadWrapper discipline is at least as dangerous as any of these — a missing railroadWrapper produces a test that passes silently forever, creating false coverage that hides real defects — but it is the only one enforced purely by human review. BD currently writes all tests. If BD omits railroadWrapper from a result-CE test body, the build passes, the tests pass, check-traceability passes (the REQ ID is in the name), and the only defense is a human reviewer running the CodeReviewer skill's Pass 4. The repo's own philosophy — Specs/README.md: 'Prefer the executable form of a rule. Where a rule cannot be executable, write it once, as close as possible to the thing it governs' — argues this should be a check script.

**Action:** Add a Checks/check-railroad-wrapper.sh script that scans Tests/ .fs files for [<Fact>] or [<Theory>] functions containing 'result {' without a corresponding 'railroadWrapper' in the same function scope. Exact heuristics vary, but a grep-based check (similar to check-clock.sh) that flags any test file containing 'result {' where the count of 'result {' exceeds the count of 'railroadWrapper' would catch the common case. Add it to the pre-commit hook (not SLOW — it is a grep, not a build).

**Why:** This is the highest-impact failure amplification vector in the agentic workflow. A single omission creates a test that claims coverage but verifies nothing. Unlike every other discipline gap, this one is invisible to the test runner, the traceability audit, and the build. It can only be caught by a human reading the test body — the most expensive and least reliable guardrail available. As BD takes over more test writing, the volume of tests increases and the per-test review attention decreases. The mechanical check is the mitigation that scales.

---

## TRACE-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs, line 135; REQ-STG-2.7
- **Summary:** The test citing REQ-STG-2.7 does not assert the status value — the name claims coverage the body does not provide.
- **Resolution:** fix-test

The test at line 135 is named 'REQ-STG-2.2 REQ-STG-2.3 REQ-STG-2.4 REQ-STG-2.5 REQ-STG-2.6 REQ-STG-2.7 REQ-STG-2.9 ingested entry has correct header fields'. Its body asserts entryDate (2.2), description (2.3), ingestionSource (2.4), fiReference (2.5), sourceFile (2.6), and line count >= 2 (2.9). It does not assert currentStatus or any status-related property. REQ-STG-2.7 states: 'Staged entry status cannot be null. Must be one of the values defined in section 4.' After the Option 4 status redesign, status is derived from the audit trail and typed as StagedEntryStatus option on StageEntryHeader. Status IS tested in a different test (line 114, under REQ-STG-3.9), which asserts 'Assert.Equal(Ingested, initialTransition |> StageEntryStatusTransition.toStatus)'. But the REQ-STG-2.7 citation on line 135 is what the traceability audit counts as coverage. This is the 'hollow name' problem documented in Tests/README.md: 'A name is a claim, not a label.' The traceability audit reports REQ-STG-2.7 as covered. An agent inventorying coverage gaps would skip it. The actual gap — no test asserts that currentStatus is Some (i.e., non-None) on a freshly ingested entry — stays hidden.

**Action:** Either add 'Assert.Equal(Some Ingested, header |> StageEntryHeader.currentStatus)' to the test body on line 135, or remove REQ-STG-2.7 from the test name and add a dedicated test that asserts the derived status is non-None and is Ingested after ingestion.

**Why:** Traceability is the agentic contract. BD inventories coverage by grepping for REQ IDs in test names. A hollow citation inflates coverage and suppresses the creation of a real test. If the audit trail invariant ever regressed (e.g., a new ingestion path that skips the initial audit record), the currentStatus would be None, violating REQ-STG-2.7 — and no test would catch it because the only test citing REQ-STG-2.7 does not check status.

---

## DELTA-1 — statement-delta
- **Location:** Dan's statement (this run); Src/Model/DataIngestion/StageEntryHeader.fs; DbMigrations/202608231305-StageEntryDropStatus.sql
- **Summary:** Dan states the header/status-table sync risk is not completely solved, but the status column removal eliminated it by construction.
- **Resolution:** dan-decides

Dan's statement: 'This doesn't completely solve the problem that we can have a header row and a status table that are out of sync in the database. If one write fails and the other succeeds, and the calling route doesn't use an auto-commit transaction, our data will be in a bad state.' The repo shows the concern no longer applies. Migration 202608231305-StageEntryDropStatus.sql dropped the status column from ingestion.staged_entry. The StageEntryHeader.insertNewToDb function (StageEntryHeader.fs line 133) inserts the header row and then calls updateHeaderStatus to insert the audit record — both operations execute within the same result CE and therefore within the same database transaction (since all mutating routes use runCommandRouteAndAutoCompleteTransaction). There is no longer a 'header row' and a 'status column' to drift — there is only a header row and an audit trail table. The only residual risk is: header inserted, audit record insert fails, no rollback. Verified that all three status-mutating routes (ingestRawEntries, updateStageEntry, post) use runCommandRouteAndAutoCompleteTransaction, which commits on success and rolls back on any error. Dan's follow-up — 'I believe all of the current routes that update status do use such a mechanism, but I haven't actually checked' — is confirmed correct by inspection.

**Action:** No code change needed. Update Dan's mental model: the Option 4 status redesign fully eliminated the column/audit sync risk. The residual risk Dan describes applied to the pre-Option-4 design where the staged_entry table carried its own status column alongside the audit trail. That column is gone.

**Why:** If Dan tasks BD with 'solve the header/status sync problem' based on his current mental model, BD would be working on a problem that no longer exists. Accurate mental models prevent misdirected work.

---
