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

Written 2026-10-11 by the Claude Code session that did this plan. Everything is on
`audit-fixes-2026-10-11a`; nothing was merged.

### 5.1 Outcome

- Build: 0 warnings, 0 errors (`dotnet build SonOfLeo.slnx`).
- `Tests.Isolated`: 530 before, 530 after, all passing.
- `Tests.Integrated` against `sonofleo_test`: 1715 before, 1751 after, all passing.
- `Checks/run-all.sh`: 12 passed, 0 failed, 1 skipped. The skip is
  check-traceability, which only runs on main.
- `traceability-audit.sh .`:
  - Invariant 1: clean.
  - Invariant 2: exactly REQ-POS-13.6 and REQ-POS-13.7 (2 of 812 active).
- `validate.py`: VALID, 0 findings.
- `model_drift.py`: it first reported two missing serving edges,
  LedgerError → FetchFilterAndSort (A3) and AccountSnapshotLot →
  HoldingOrchestration (A4). Both edges are now in the model, and it reports
  NO DRIFT.
- No `Not yet implemented` placeholder remains.
- ArchReviewer was run over `git diff eeecb32 HEAD -- Src Checks` (see 5.5).

### 5.2 Per item

**Part A** (names committed first as placeholders, in d0b64d7)

1. **#073 (every leg must be invoiced).** `deriveIsFulfilled` now also
   requires an Invoice on every Payment Agreement of the Master Agreement.
   - The ProjectionRules test that pinned the old rule is reversed. An
     un-invoiced leg keeps the Instance open and in the bills to chase.
   - Three DerivedStateRules tests built a one-leg Instance on a two-leg
     agreement and expected it fulfilled. They now invoice and pay both legs.
   - One test was deleted: "adding a new unpaid Invoice to a fulfilled
     Instance". Its premise needs an un-invoiced leg on a fulfilled Instance,
     which the new rule forbids. Its case is now the "none" row of the theory.
2. **#079 (memo in line pairing).** `isSameLine` now compares memo, and a null
   memo pairs only with a null memo. Test added. See 5.4 on the plan's premise.
3. **#071 (reversed date ranges).** There is one check, in
   `getDateRangeFromTemporalFilter`, raising a new
   `FetchFilterDateRangeEndBeforeBegin of LocalDate * LocalDate` in
   LedgerError. It is tested through account activity and through the
   staged-entry fetch.
4. **#087 (basis method locked while Lots exist).**
   - New error `PositionsHoldingBasisMethodChangeStrandsLots` names the
     account, the security, and the earliest and latest snapshot dates.
   - New `AccountSnapshotLot.fetchSnapshotDatesWithLotsForHolding`.
   - New guard `HoldingOrchestration.confirmNoLotsReinterpreted`.
   - Tests cover refusal (three Lot dates out of order plus a later Lot-less
     snapshot) and acceptance once no line carries a Lot.
5. **#064 (replaced Lot counts).** `RecordedSnapshot.replacedLineLots`
   (security, lot count), lines by Security name, is carried through
   `RecordedAccountSnapshotReturn`. Orchestrator and route tests added.
6. **#094 (agreement names after a rename).** The Agreement return is built
   from the written entities, not from the `idToName` caches. A route test
   renames a master agreement with legs and checks the new names, both in the
   return and when read back.
7. **#090/#037 (list order).** Order tests were added for every list
   REQ-RPT-8.5 names and for REQ-RPT-9.2 grouping values, including owners.
   The code already agreed with the spec, so no Src change was made.
8. **#083 (exact external-reference lookup).** Two tests, one for source FI
   and one for reference value. Each checks an exact match, a case variant
   and a containing string.
9. **#084 (comments on locked entries).** Two tests amend a comment's text
   and re-point its secondary entry: one where the primary entry is voided,
   one where its period is closed. Both check the returned value and read it
   back.
10. **#015.** The REQ-CR-1.24 raw-INSERT theory was deleted.

**Part B**

11. **#047/#057 (peer guard).**
    - The four modules are added to `investment=`.
    - A `shared=` allowlist holds PositionsComponent, PositionsError,
      PositionsAuditableAction and PreLedgerBalance, the last with a D40
      comment.
    - Any Positions module in no list now fails the check.
12. **#059 (private `query`).** `let private query` in 10 Positions modules
    and in `Person.fs`. PreLedgerBalance is untouched.
13. **#060 (REQ tags).** REQ tags were stripped from Src comments. One was
    kept: `AccountCreation.fs` cites REQ-AC-4.22 as the reason a circular
    ancestry cannot arise, so it explains why rather than tagging a site.
    `Src/README.md` mentions were left alone (documentation, not source).
14. **#075 (source-pattern filter).** New `SourcePatternFilter` in
    ClassificationComponent: trimmed, non-empty, at most 500 characters. It
    has two new ClassificationError cases with messages and replaces
    `StringSearchPattern` in the filter, the orchestrator, the converter and
    the tests.
15. **#002.** `FiscalPeriod.idToKey` was deleted.
16. **#096.** The message now reads "Failed to serialize a value of type …
    to a JSON string."

**Part C** (17–35)

17. **#001.** `totalOwnedPropertyMortgages` is in both totals comparisons.
18. **#004.** New test: two ranges sharing a day are rejected with the typed
    error naming the account and both ranges, and stored activity equals the
    fixture's.
19. **#005.** Both case variants resolve through `Account.codeToId` to their
    own accounts. A never-created third variant does not resolve to the
    incumbent.
20. **#009.**
    - The oracle is now fixture-derived (unvoided only).
    - A guard checks that the fixture holds a voided entry on both accounts.
    - `sumJournalEntryLinesByAccountIdAndType` was deleted.
21. **#011/#012.** The hollow orchestrator REQ-CF-8.11 test was deleted. One
    committed route test now builds data in reverse of the returned order and
    asserts exact accounts, flows, invoices and bills, plus a second identical
    call. See 5.4.
22. **#013.** The REQ-CF-4.7 sweep test was deleted, along with the
    `setNextInstance` scenario member that only it used.
23. **#014.** The TransitionPaymentsToPosted payload's row for the Payment is
    asserted whole: payment, agreement name, amount and journal line.
24. **#016/#017.** The active-only expectation is derived from the fixture.
    Four exact counts are now `>= 2` floors with messages.
25. **#018.** A theory over entry date, ingestion source, FI reference,
    source file and a balanced line-type swap, each read back.
26. **#021.** The dedup test asserts, per file, whether each entry is
    Duplicate and how many Duplicate transitions it has. The second file's
    entry is the Duplicate.
27. **#024.** REQ-FP-3.5 compares sorted lists, not sets.
28. **#025.** New multi-day REQ-JE-3.7 test with entries at begin−1, begin,
    begin+1, end and end+1.
29. **#026.**
    - The PostNew sad paths capture the posted ID on the Ok arm.
    - AddExternalReference and AddComment delete what they appended before
      failing.
    - The Void theory posts its own entry for every scenario. For the
      closed-period row it opens a month −6 period, posts, then closes it.
      `finally` deletes the entry and then the period.
30. **#030.** New test: a Person update re-sending the Person's own name,
    with a new past birthdate, succeeds and reads back.
31. **#033.** New test: International gets activity only on d1 and on d3+1,
    and the rows equal the hand-derived rows. The fixture's own activity on
    those days is re-recorded alongside it.
32. **#035.** Activity tests compare stored values, not counts.
33. **#039.** New REQ-SYS-3.3 theory with seven rows: Instance update and
    cancel, Invoice update and cancel, Payment update, period close and
    period reopen. Each asserts (createdAt, modifiedAt) = (creating instant,
    updating instant).
34. **#046.** Investment Activity is a row of the REQ-SYS-3.2 create theory,
    and the theory's name now lists it.
35. **#051.** The two-row update test creates a temporary table inside the
    rolled-back transaction, inserts two rows and updates exactly those, so it
    no longer touches `ledger.account`.

ArchReviewer follow-ups (3f86982): two comments were reworded or added. See
5.5.

### 5.3 Fail-then-pass evidence

Every new or changed assertion was seen failing under a Src perturbation (or
a perturbed expectation) and then passing after the restore.

**A1 (#073).**
- Every-leg clause → `true`: the theory rows and the reversed ProjectionRules
  test fail.
- Expected values flipped: they fail.

**A2 (#079).** See 5.4.
- Memo clause removed alone: still passes, in both supplied orders.
- Journal lines also reversed:
  - memo key kept: 49/49 pass;
  - memo key removed: all 4 rows fail, e.g. Expected
    [(first, first); (second, second)] Actual
    [(first, second); (second, first)].

**A3 (#071).**
- Guard → `when false`: both tests fail "Expected failure; got success".
- Expected dates swapped: both fail.

**A4 (#087).**
- Guard disabled: the refusal test fails.
- Expected earliest date moved to d2: fails (10 Jul vs 10 Jun).
- The d2 re-record removed: the acceptance test fails.

**A5 (#064).**
- Count forced to 0 in orchestration: the orchestrator and route tests fail.
- Count forced to 0 at the route only: the route test fails.
- A dummy row on the no-replacement path: the not-a-replacement test fails.

**A6 (#094).** Converter reverted to the cache lookup: the return carried the
old names.

**A7–A9.** Twelve perturbations; every one failed:
- properties, assets, liabilities, mortgages, totals and investment accounts
  unsorted;
- wealth zero-fill appended unsorted, and wealth with no sort;
- FI match and reference match made partial and case-insensitive;
- comment text update dropped, and secondary update dropped.

**B11.**
- A planted unlisted `Planted.fs`: exit 1.
- A planted `Property.propertyName` in UnitRollForward: exit 0 under the old
  guard, exit 1 under the new one.
- Clean tree: exit 0.

**Part C.** Thirty-one runs; every one failed the named test(s).

| Item | Perturbation | Failed |
|---|---|---|
| C17 | `totalOwnedPropertyMortgages` → `totalLiabilities` | 1 |
| C18 | overlap check removed | 1 |
| C32 | every stored description replaced | 11 |
| C19 | code lookup made case-insensitive | 1 |
| C20 | voided lines counted | 1 |
| C21 | accounts unsorted | 1 |
| C21 | invoices unsorted | 1 |
| C21 | bills unsorted | 1 |
| C21 | inflows reported as outflows | 1 |
| C23 | returned line ID → payment ID | 1 |
| C24 | active filter also drops 'Acme%' rules | 1 |
| C25 | entry date SET removed | 1 row |
| C25 | source SET removed | 1 row |
| C25 | FI reference SET removed | 1 row |
| C25 | source file SET removed | 1 row |
| C25 | line type SET removed | 1 row |
| C26 | dedup order reversed (earliest flagged) | 6, incl. the test |
| C27 | open list repeats its head | 1 |
| C28 | range begins at its end date | 2 |
| C29 | void allowed in a closed period | 1 |
| C29 | empty comment accepted | 4 |
| C29 | empty FI accepted | 4 |
| C30 | own name counted as taken | 1 |
| C31 | window opens on the first date | 8 |
| C31 | window closes a day late | 7 |
| C33 | Instance update/cancel stamped from the clock | 2 rows |
| C33 | Invoice update/cancel stamped from the clock | 2 rows |
| C33 | Payment update stamped from the clock | 1 row |
| C33 | period close/reopen stamped from the clock | 2 rows |
| C34 | activity insert stamped from the clock | 1 row |
| C35 | `ExactlyOne` accepts two rows | 2 |

For the three C29 runs, the ledger's row counts were the same before and
after each perturbed run: je=26, comments=5, refs=10, periods=10. So the
sad-path tests removed what the wrongly accepted calls had written.

### 5.4 Disagreements, notes and things to look at

- **Plan vs. code, A2 (#079).** The plan asks for legs "supplied in the order
  the old key would mis-pair". No such order exists. Posting creates the
  journal lines in staged-line order and pairs against them in hand, so the
  old key never mis-paired through any real path. The test is kept: it fails
  once the journal-line order is perturbed (5.3). The memo clause is
  defensive today.
- **Possible spec gap, REQ-CF-4.9 with REQ-CF-14.9.** Adding a Payment
  Agreement to an existing Master Agreement does not re-derive is-fulfilled
  on its existing Instances. Under the every-leg rule, an Instance that was
  fulfilled now has an un-invoiced leg but stays fulfilled. Neither
  requirement says whether it should flip. Code and spec are left as they
  are; Dan to rule.
- **REQ-CF-8.11.** The spec requires only a stable order. The C21 test pins
  the converter's order (accounts by code, invoices by due date, bills by
  instance date then Payment Agreement name), because #011 asked for exact
  order.
- **A4 (#087).** Setting the basis method to the value it already holds
  skips the guard. That write changes nothing, so it strands no Lot.
- **A1.** One test was deleted and three setups changed (5.2 item 1).
- **Public-repo hygiene, not swept.** Pre-existing fixture and test data use
  a real company name in a staged description: the "DD …" dedup rows in
  `StageEntryIngestion.fs`, and the classifier tests in `Tests.Isolated`.
  It was outside this plan, so it is flagged here and not changed.

### 5.5 ArchReviewer

The verdict was "no structural problems".

- **The `security` field** on `ReplacedLineLots` and its contract: the
  reviewer asked for `securityName`. I kept `security` on purpose. The same
  file's `SnapshotLineView` already has a `securityName` label, and F# record
  inference would then pick the wrong record at existing construction sites.
  Renaming means annotating those sites. Dan's call.
- **`ReplacedLineLots` as a record, not a tuple:** kept, for the named
  fields in the contract mapping. It is optional.
- **The `FiscalPeriodCreation.fs` comment** named "the Saturday run". It now
  reads "callers run it repeatedly without checking first" (3f86982).
- **The agreement converter's name-map indexer** gained a comment saying why
  it cannot miss (3f86982).
- **The peer-guard D40 comment** was kept, because the plan asked for it.
- **Two private converters** don't use the `convert [X] to [Y]` style. Left
  as they are; this is optional.

### 5.6 Names written after reading Src

All Part A names were committed before any Src was opened (d0b64d7), except
the A7 rename below.

- **A7.** The RPT-8.5 tax-treatment and account-group totals test dropped
  its "absent component" clause after Src was read. The
  NetWorthPreLedger REQ-RPT-8.8 tests already pin that case.
- **Part C** names written or renamed with Src already read:
  - C18 "REQ-POS-13.3 two ranges for one account that share a day are
    rejected …"
  - C19 REQ-AC-1.5 rename ("… each code resolves through the code lookup to
    its own account …")
  - C21 "REQ-CF-8.11 REQ-CF-8.7 REQ-CF-8.10 REQ-CF-8.4 a ProjectCashFlow
    payload over data built in reverse of the order it returns …"
  - C23 REQ-CF-6.4 rename (adds REQ-CF-10.7 and the transition payload)
  - C25 the REQ-STG-6.2 UpdateStageEntry theory
  - C28 "REQ-JE-3.7 fetchByDateRange over a range of several days …"
  - C30 "REQ-PER-2.2 REQ-SYS-6.1 an update that sets a Person's name to the
    name it already holds …"
  - C31 "REQ-POS-14.2 a Security on neither snapshot whose only
    units-moving activities are dated on the first snapshot date and on the
    day after the second has no row"
  - C33 the new REQ-SYS-3.3 cash-flow and period theory
  - C34 the REQ-SYS-3.2 theory rename (adds Investment Activity)

### 5.7 Grader pass on the Part A names

One independent pass over the names, using spec text only.

- **JE-3.5:** split into separate FI and reference-value tests, each with an
  exact-match positive.
- **RPT-9.2/9.3 owners:** now includes a jointly owned group.
- **POS-7.3 route:** now "says it replaced", with distinct non-zero counts.
- **CF-4.9/8.4:** the bill now carries the agreement name, the Payment
  Agreement name, the instance date and the cadence.
- **RPT-8.5 totals:** stored-order guard added, and "more than one component
  absent".
- **RPT-9.2:** stored-order guard added, and unassigned last.
- **CF-14.2:** the fetch by the new name reads back.
- **POS-7.3/7.2:** "as a replacement".
- **CF-4.9/9.10 add:** now a paid-in-full vs. paid-in-part theory.
- **JE-5.3:** "re-pointing its secondary entry".
- **POS-5.7:** three out-of-order Lot dates.
- All other names scored 88–92 and were kept.
