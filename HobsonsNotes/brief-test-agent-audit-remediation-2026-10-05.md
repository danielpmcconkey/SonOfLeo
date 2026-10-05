# Brief — test agent, audit 2026-10-04a remediation

Written 2026-10-05 by Hobson for a Claude Code session that can see this repository and
nothing else. **Entry point is `HobsonsNotes/plan-audit-remediation-2026-10-05.md` §0**, which
says when to work from this brief: Part A names before any Src is read, the tests themselves
after the Src work. This brief is README step 5. It says **what** changed in business terms and
where the spec now stands, never **how** the code does it. Where this brief and
`Specs/Behavioral/` disagree, the spec wins. Tell Dan.

## 0. Ground rules

- **Branch:** `audit-remediation-2026-10-05`. Run `git pull --rebase --autostash` before every
  commit. The same session implements Src from the plan, in the order its §0 sets.
- **While working from this brief, edit `Tests/` only.** Never edit `Specs/`. Src fixes a test
  exposes go through the plan. If a spec looks wrong, or a test can't be written honestly
  against it, stop and report it with the REQ ID. Never edit the spec, and never weaken a test
  to get a pass.
- **The three load-bearing constraints (README):**
  1. For Part A, draft test names from the spec alone, before you read any Src.
  2. Run the name-quality check (`Skills/TestNameReview/`), then commit the names as failing
     placeholders (`Assert.Fail "Not yet implemented"`). The committed name is the contract.
     Only after that do you read Src. Src can change how a test reaches a behaviour, never
     what it asserts. To renegotiate a name, say so out loud.
  3. Watch every new assertion fail before you call it done: perturb the expected value, run
     it, read the failure, restore it, and report the output.
- **Public repository:** no institution names, people, account numbers or real amounts in
  tests, fixtures or commit messages. Use the existing fictional fixtures.
- **Running tests:** `Tests.Isolated` needs no database. `Tests.Integrated` needs the
  `sonofleo_test` database. If it isn't there, say so. Don't skip, fake or weaken anything.
- **Settled rulings you must not re-litigate** are in
  `Skills/SonOfLeoRequirementsAudit/resolved-findings.md` — note ROUTE-ORACLE, UNIQUE-DB
  (no update-path uniqueness tests, no pinning a DB-constraint rejection to a typed error),
  DECIMAL-SCALE, and the no-corrupted-row rule (don't hand-edit rows to test reconstitute).
- **Loud failures earn no test.** If a row below would have you test something that already
  fails with a typed error and writes nothing, and the row doesn't say otherwise, skip it and
  say so.

**Read first:** `README.md`, `Specs/README.md`, `Tests/README.md` and the README in each test
directory you touch, `Skills/TestWriter/SKILL.md` and
`references/bullshit-test-specimens.md`, `Skills/TestNameReview/SKILL.md`, and
`git show 22af7f4 -- Specs/` (every spec change this brief refers to).

---

## Part A — Requirements that changed

Read each REQ in the spec; the summaries below only tell you where to look. Each item ends
with the **test action**. "New" means it needs citing tests.

### A.1 Positions

1. **Roth guard** (REQ-POS-5.5, new). An Investment Account whose snapshots carry a
   contribution basis cannot have its tax treatment changed away from Roth; the error names
   the account and the earliest and latest such snapshot dates. **New:** the refusal (with two
   such snapshots, so earliest and latest differ), nothing changed after it, and the change
   succeeding once no snapshot carries a basis.
2. **Deletes** (REQ-POS-11.10, 11.11, new). A Holding can be deleted unless any snapshot line
   references it; a Property can be deleted unless it has any Valuation, and deleting it removes
   its owners and ledger links. **New:** each delete succeeding (and the record gone, with a
   Property's links released so the same ledger account can then link elsewhere), each
   refusal with its typed error, nothing changed after a refusal. REQ-POS-11.12–11.14 (no
   delete for Dimension Value, Security, Investment Account) are waived — no tests.
3. **A Property's asset accounts are a set** (REQ-POS-9.7, REQ-POS-11.6, amended). A Property
   links zero or more FixedAsset ledger accounts, updated as a complete new set. A repeat in
   one request is rejected naming the code; an account already linked to another Property is
   rejected naming the code and that Property; wrong type or subtype is rejected. **New** for
   each clause, at the route level, plus create and update with two asset accounts round-
   tripping. Existing tests that create or update a Property's single asset account need
   their payloads moved to the set shape (mechanical).
4. **Repeated mortgage account** (REQ-POS-9.8, amended). **New:** a repeat in one request is
   rejected naming the code, nothing stored.
5. **Per-peer link rule** (REQ-POS-4.9, amended). Now Investment Accounts only; the Property
   half is in 9.7. Re-check the existing 4.9 tests still describe what they assert; re-cite
   any Property-side test to REQ-POS-9.7.
6. **Snapshot recording and listing** (REQ-POS-7.1 with REQ-SYS-6.1; REQ-POS-7.5, amended).
   **New:** recording an empty list of snapshots is refused with a typed error; listing
   snapshot dates with the end before the begin is refused with a typed error naming both.

### A.2 Reports

7. **Net worth and asset-account sets** (REQ-RPT-8.2, amended). A Property's value replaces
   all of its linked asset accounts. **New:** a Property with two linked asset accounts that
   both carry ledger balances — neither appears among counted ledger assets on a date the
   Property is owned; net worth counts the Property's value once.
8. **Net worth totals add up** (REQ-RPT-8.5, amended). A separate owned-property mortgages
   total; counted ledger assets + investments + property values − liabilities − owned-property
   mortgages = net worth. **New:** the new total's exact value from fixture data, and the
   identity holding on the fixture date (data and rendered Totals block).
9. **Investment wealth history** (REQ-RPT-9.2, 9.5, amended). Every group value with holdings at
   any point in the range appears at every point, 0.00 where it has none — in the data and in
   the rendered table (not a blank cell). The header shows begin date, end date and grouping
   (re-cite the existing range-header test to 9.5 as well as 3.1). **New:** a range where one
   group has holdings at the first point and none at the last (or the reverse).

### A.3 Ingestion, cash flow, classification

10. **Same-file duplicate key** (REQ-STG-1.18, new). Two groups in one file sharing
    `fi_source` and `fi_reference` reject the whole file with a typed error naming both
    groups; nothing is staged. **New.**
11. **Source filter** (REQ-STG-10.2, 10.7, amended). The ingestion-source filter is by source
    name; a name matching no source is a typed error. **New:** the error, and confirm an
    existing source-filter test passes a name.
12. **External invoice ID** (REQ-CF-5.20, new). Optional; when given, not whitespace-only and
    at most 100 characters. **New:** isolated rejections for `"   "` and 101 characters, and
    exactly 100 accepted (row #011).
13. **Projection repeatability** (REQ-CF-8.11, new; REQ-CF-8.5 now waived). Re-cite the
    existing repeat-and-compare projection test from 8.5 to 8.11. No new test.
14. **Re-derivation triggers** (REQ-CF-9.10, amended). Payment state, posted state and
    is-fulfilled are re-derived also when an Invoice's amount changes, an Invoice is added to
    an Instance, or an Invoice is cancelled. **New:** one test per new trigger — amount raised
    on a FullyPaid Invoice, a new Invoice on a fulfilled Instance, a cancel that makes the
    Instance fulfilled.
15. **Cancelling an Instance with a cancelled Invoice** (REQ-CF-4.12, amended). An Invoice
    already cancelled keeps its own reason note. **New.**
16. **Payment Agreement under another Master Agreement** (REQ-CF-14.8, amended). An update
    naming a leg of a different Master Agreement is refused with a typed error; both
    agreements unchanged. **New** (route level).
17. **Rule listing order** (REQ-CR-5.4, amended). Ties on the sort key break by rule name.
    **New:** two rules sharing an account code, and two sharing a priority, in exact order.
    Tighten the existing "adjacent" assertion to an exact order (#145).

### A.4 System-wide

18. **Enum inputs are trimmed** (REQ-SYS-1.1). **New:** isolated tests per operator-facing enum
    parser in Positions, the wealth grouping, CashFlow, Classification and DataIngestion, that
    a padded value (e.g. `" Roth "`) parses to the same case (precedent: the Ledger enum trim
    tests).
19. **Dimension Value renamed to its own name** (REQ-SYS-6.1, REQ-POS-11.1). **New:** succeeds,
    stored value unchanged.
20. **Not-found by name** (REQ-SYS-6.2, amended). Covers records addressed by name or by
    parent and date. Re-cite the Valuation-delete miss test from REQ-SYS-6.1 to 6.2 (#181).
21. **Timestamps on the new slice** (REQ-SYS-3.2, 3.3; row #085). Add Person and every
    Positions entity (not component rows — Definitions) to the REQ-SYS-3.3 theory, and assert
    created = modified = the creating instant in one create test per entity.

### A.5 Withdrawals, waivers and re-citations (the traceability gate fails until these are done)

- **REQ-JE-3.8 withdrawn.** Three tests in `JournalEntryFetching.fs` cite it: convert the FI test
  to header-id set equality and re-cite to REQ-JE-3.5; re-cite the reference-only test to 3.5
  with set equality; re-cite the both-None test to 3.5.1 (rows #059, #061, #156). Delete any
  that then duplicate an existing test at the same layer.
- **REQ-JE-5.6 waived.** Delete its test (it cannot fail; #058).
- **REQ-NGUI-1.4 waiver restored.** Re-cite the three tests that cite it to REQ-NGUI-1.5,
  keeping their REQ-POS IDs (#065).
- **REQ-STG-1.16 waiver removed.** Add REQ-STG-1.16 to the name of the test that shows a
  `group_id` reused in a later file makes a separate staged entry (#052).
- **REQ-CF-8.5 waived.** See item 13.

When you finish, `bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh .` must show
Invariants 1 and 2 clean and no stale waivers.

---

## Part B — Existing-test repairs (disposition rows owned by test-agent)

Read each row in `Audit/2026-10-04a/99-disposition.md`; the ruling is the instruction. In
order of file area:

- **Routes and CLI:** #006 (route table from the shipped program), #066, #067, #070, #072
  (exact messages from the input type's `FullName`), #071.
- **Account:** #024, #025, #026, #027, #028, #029, #031, #032, #088 (exact balance, not a sign).
- **CashFlow:** #033, #035, #037.
- **Classification:** #038.
- **DAL:** #040, #041, #042 (delete the uncited 27-case theory — Dan's ruling), and the rows
  #002/#003 leave for you (malformed-SQL tests switch to `ExactlyOne`; theory rows for deleted
  unboxers go with #042).
- **Ingestion:** #012, #043, #044, #045, #046, #048, #049, #050, #051, #053.
- **Fiscal period:** #055, #056.
- **Journal entry:** #057, #060, #061, #062.
- **Money and Quantity/Price:** #063, #077 (the extreme product row — use the row's literal,
  which was checked).
- **Person:** #068, #069.
- **Positions:** #016, #018 (disposed Property with a mortgage — also covers #078, #095), #075.
- **Reports:** #079, #080, #081, #082, #083, #084.

---

## Final report (append it to §8 of the plan, with the Src report)

Per Part A item and Part B row: the tests added, changed or deleted, by name; the
watched-failure output for every new assertion (constraint 3); any name renegotiated at step 9,
and why; any row you skipped, and why; every spec finding with its REQ ID; and the
traceability audit output.
