# Plan — Audit 2026-10-11a fixes (Src and tests)

Written 2026-10-11 by Hobson for a Claude Code session that can see this
repository and nothing else. Branch `audit-fixes-2026-10-11a`. Where this
plan and `Specs/Behavioral/` disagree, the spec wins — tell Dan.

## 0. Start here

Audit `2026-10-11a` ran 45 auditors over code, tests and specs. Every finding
is ruled in `Audit/2026-10-11a/99-disposition.md` on branch
`audit-2026-10-11a` (`git show audit-2026-10-11a:Audit/2026-10-11a/99-disposition.md`).
Hobson has already applied the spec side on this branch (commit `802db77` and
the model-rule commits after it). **This plan is everything owned by
`impl-agent` or `test-agent` in that record.** Items cite rows as `#NNN`; read
the row before starting an item — the ruling is the instruction. A duplicate
row is handled under its canonical row.

Not in this plan: the 2026-10-11b refactors (their own plans), and 11a #054
(the NetWorthComponent and NetWorthDate move), which goes with them.

1. **Name first.** Read §1, then the spec diff (`git diff main -- Specs/`).
   Before opening any Src, draft test names for every item in Part A from the
   spec alone, run the name-quality check, and commit them as failing
   placeholders (`Assert.Fail "Not yet implemented"`). Existing tests may be
   read for naming and fixture conventions.
2. Then Parts A, B and C in order, committing as you go.
3. Finish with §4 and write your report into §5.

A test that fails for a reason you did not expect is either a Src bug (fix
it) or a spec bug (record it in the report with the REQ ID; don't edit the
spec).

## 1. Read before you touch anything

- `README.md`, `Specs/README.md`, `Src/README.md`, `Tests/README.md`.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
- `Skills/ArchReviewer/SKILL.md` — run it over your own diffs before you
  report.
- `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, especially
  LOUD-FAILURE, ENTRYDATE-IO and REPORT-RECORDS.
- `CompoundedLearnings/` — especially `validation-layers.md`,
  `failure-vector-is-a-user-interaction.md`, `type-taxonomy.md`.
- `git diff main -- Specs/` on this branch. Read the whole of each amended
  requirement, not just the changed line.

## 2. Standing rules

- **You write `Src/`, `Checks/`, `Architecture/` (drift fixes only) and
  `Tests/`; never `Specs/`.** No migration is expected in this slice. If you
  find you need one, stop and say so in the report.
- `git pull --rebase --autostash` before every commit. Only Dan merges to
  `main`.
- **Public repository.** No institution names, real tickers, account numbers,
  people's names or real amounts anywhere: code, comments, fixtures, commit
  messages.
- **Dan is the authority.** If a ruling or requirement looks wrong, make the
  case once in your report; don't edit a spec, and don't quietly do something
  else.
- **No REQ annotations in source.** A comment may name a rule to explain why
  code is shaped a certain way; it never tags a site.
- **Loud beats bubblewrap.** A failure that already aborts with an error and
  writes nothing needs no new guard, no new error case and no new test. Only
  silent failures earn code and tests.
- **Code never breaks a tie** unless a requirement names the tie-break.
- **Counts only alongside values.** A test that checks what was stored
  compares values, not just how many.
- **Every new or changed assertion is seen failing before it passes**
  (README constraint 3). Perturb Src or the expected value, watch it fail,
  restore.
- **Every operation uses its context's initiation instant** (REQ-SYS-3.4).
- Commit subjects describe the behaviour, not the edit. Match the log.

## 3. The work

File references were checked on 2026-10-11; **verify before editing**.

### A. Behaviour: the amended specs require these (code and tests)

1. **An Instance is fulfilled only when every leg is invoiced.** (#073;
   REQ-CF-4.9, REQ-CF-9.10.) `deriveIsFulfilled`
   (`InstanceOrchestration.fs` ~475) counts only the Invoices that exist.
   It must also require an Invoice on every Payment Agreement of the
   Instance's Master Agreement. Reverse the test near
   `ProjectionRules.fs:366` that pins the old rule, and add a test where an
   un-invoiced leg keeps the Instance open and in the bills to chase.
2. **Posting pairs staged lines by memo too.** (#079; REQ-STG-9.10.) Add
   memo to `isSameLine` (`StageEntryOrchestration.fs` ~732); a null memo
   pairs only with a null memo. Test: two legs of one entry with the same
   account, line type and amount but different memos, supplied in the order
   the old key would mis-pair, then assert each staged line records the
   journal entry line carrying its own memo.
3. **A reversed date range is rejected.** (#071; REQ-AC-3.12.1,
   REQ-STG-10.2.) Put one check on the shared filter (`FilterDateRange` /
   `getDateRangeFromTemporalFilter` in `FetchFilterAndSort.fs`), not in each
   caller, with one typed error naming both dates in an error module that
   file can see. Today a reversed range returns nothing, silently. One test
   through account activity and one through the staged-entry fetch.
4. **A basis-method change is refused while the Holding has Lots.** (#087;
   REQ-POS-5.7.) Guard the Holding basis-method update. New `PositionsError`
   case naming the account, the security, and the earliest and latest
   snapshot dates whose line carries a Lot. Shape it like the REQ-POS-5.5
   guard. Tests: refused with Lots (on two dates, so earliest and latest
   differ, and the basis method is unchanged afterwards), and accepted once
   no line carries a Lot.
5. **A snapshot correction reports the Lots it replaced.** (#064;
   REQ-POS-7.3.) `RecordedSnapshot` (`AccountSnapshotOrchestration.fs` ~39)
   carries, when it replaced a snapshot, how many Lots each line of the
   replaced snapshot carried, by security. Carry it through the contract.
   Test: re-record a date whose lines had Lots, with one line re-supplied
   without Lots, and assert the counts per security and that the stored line
   now has none (REQ-POS-7.2).
6. **The agreement return shows the name just written.** (#094.) Build the
   `[Agreement] -> [AgreementReturn]` conversion from the entities already in
   hand, not from `MasterAgreement.idToName` / `PaymentAgreement.idToName`
   (`CashFlowLookupConverters.fs` ~38, ~74). A lookup cache reads committed
   rows on its own connection, so it returns the old name inside the
   renaming transaction. Do not make the cache transaction-aware. Route test:
   rename a Master Agreement that has legs, and assert the new name in the
   return.
7. **Order of the net worth and wealth history lists.** (#090, #037;
   REQ-RPT-8.5, REQ-RPT-9.2.) The orders are now stated, and the code
   already has them. Add order assertions for each list REQ-RPT-8.5 names
   and for the grouping values within a REQ-RPT-9.2 point (by name, by
   owners' names for the owners grouping, unassigned last). If the code
   disagrees with the spec, report it; don't change either.
8. **External reference lookup is exact.** (#083; REQ-JE-3.5.) One test: a
   case-variant source FI, and a substring of a reference value, each return
   nothing.
9. **Comment amendment on a voided entry or in a closed period.** (#084;
   REQ-JE-5.3.) Two tests: amend a comment's text and secondary entry when
   its primary entry is voided, and when the entry's period is closed. Both
   succeed and read back.
10. **REQ-CR-1.24 is waived.** (#015.) Delete its raw-INSERT test.

### B. Code conformance and dead code (no new tests unless stated)

11. **Peer guard.** (#047, #057.) In `Checks/check-peer-guard.sh`, add
    `AccountSnapshotLot InvestmentActivity InvestmentActivityOrchestration
    UnitRollForward` to `investment=`. Then make the check fail on any file in
    `Src/Business.FinancialServices.Positions/` that is in neither list and
    not on a short shared allowlist (`PositionsComponent`, `PositionsError`,
    `PositionsAuditableAction`, and `PreLedgerBalance` with a comment that the
    D40 slice removes it). Show it failing on a planted unlisted file.
12. **Private `query`.** (#059.) `let query` becomes private in each
    Positions entity module and in `Person.fs`, wherever nothing outside the
    module calls it. Leave `PreLedgerBalance.fs` alone (deferred to D40).
13. **REQ tags out of source.** (#060.) About 39 `REQ-` mentions remain in
    Src comments. Strip each one that tags a site and keep the description.
    A comment that explains why, and cites a rule to do so, may stay; say in
    the report which ones you kept and why.
14. **Source-pattern filter type.** (#075.) The REQ-CR-5.3 source-pattern
    filter gets its own classification-domain type and error (trimmed,
    non-empty, at most 500 characters) in place of `StringSearchPattern`. No
    test: an over-length filter is a loud refusal.
15. **Dead lookup.** (#002.) Delete `FiscalPeriod.idToKey`
    (`FiscalPeriod.fs` ~193).
16. **Error message.** (#096.) `JsonSerializationFailed`'s message
    (`UtilityError.fs` ~26) describes serialising a value to JSON, not the
    reverse.

### C. Test repairs (existing tests; rows say exactly what)

17. NetWorth route data-only payload: add `totalOwnedPropertyMortgages` to
    the totals comparison. (#001)
18. Two overlapping ranges for one account through `recordActivity` or the
    Record route: `PositionsActivityRangesOverlap`, stored activity
    unchanged. (#004)
19. REQ-AC-1.5: resolve both case-variant codes through `Account.codeToId`;
    a third variant does not resolve to the incumbent. (#005)
20. REQ-AC-3.13.1: fixture-only oracle, a guard that the fixture holds the
    voided lines, and delete `sumJournalEntryLinesByAccountIdAndType`. (#009)
21. ProjectCashFlow route: one combined test for REQ-CF-8.11 order and
    exact converted values. Re-cite or delete the hollow orchestrator test.
    (#011, #012)
22. Delete the REQ-CF-4.7 sweep test. (#013)
23. TransitionPaymentsToPosted route: assert the returned row. (#014)
24. Classification rule tests: derived active-only expectation (#016); `>= 2`
    guards in place of four hard-wired counts (#017).
25. UpdateStageEntry route theory over entryDate, ingestionSource,
    fiReference, sourceFile and a balanced line-type swap. (#018)
26. Dedup happy path: the second-file entry is the Duplicate. (#021)
27. REQ-FP-3.5: compare sorted lists, not sets. (#024)
28. REQ-JE-3.7: a multi-day range with entries at begin-1, begin, interior,
    end and end+1. (#025)
29. Sad-path route tests on committing routes: make cleanup real. (#026)
30. Person update re-sending its own name. (#030)
31. REQ-POS-14.2: a Security on neither snapshot whose only activity is
    outside the window gets no row. (#033)
32. Activity recording tests compare stored values. (#035)
33. REQ-SYS-3.3 theory rows for Instance update and cancel, Invoice update
    and cancel, Payment update, and fiscal period close and reopen. (#039)
34. Investment Activity in the REQ-SYS-3.2 create theory. (#046)
35. REQ-DAL-2.2: insert its own two rows in the rolled-back transaction, so
    it no longer depends on run order. (#051)

### Out of scope

- Every row ruled overruled or deferred.
- Rows owned by Hobson alone: spec, precedent and model edits.
- REQ-POS-13.6 and 13.7 (activity coverage). They are on this branch's spec
  but are the next slice, which builds on this branch. **Traceability
  Invariant 2 should therefore list exactly REQ-POS-13.6 and 13.7 when you
  finish, and nothing else.** Dan merges after that slice.

## 4. When you finish

- Build: 0 warnings, 0 errors.
- `Tests.Isolated` and `Tests.Integrated` pass, the latter against
  `sonofleo_test` (`Tests/Tests.Integrated/setup-throwaway-test-db.sh`). If
  you can't get the database, say so; don't skip or fake.
- `bash Checks/run-all.sh`, `Skills/SonOfLeoRequirementsAudit/traceability-audit.sh .`,
  `Skills/ArchiMate/validate.py` and `model_drift.py` (fix drift in the model
  if a function moved or its description is now wrong).
- No `Not yet implemented` placeholder left.
- Push to `audit-fixes-2026-10-11a` and write §5: what changed per item, test
  counts before and after, fail-then-pass evidence, the outputs above,
  anything you disagreed with or couldn't do, and every name you wrote after
  reading Src.

## 5. Report
