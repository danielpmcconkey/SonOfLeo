# Brief — test agent, audit 2026-10-03a remediation

Written 2026-10-03 by Hobson for a Claude Code session that can see this repository and
nothing else. **Entry point is `HobsonsNotes/plan-audit-remediation-2026-10-03.md` §0**, which
says when to work from this brief: Part A names before any Src is read, the tests themselves
after the Src work. This brief is README step 5. It says **what** changed in business terms and
where the spec now stands, never **how** the code does it. Where this brief and
`Specs/Behavioral/` disagree, the spec wins. Tell Dan.

## 0. Ground rules

- **Branch:** `audit-remediation-20261003`. Run `git pull --rebase --autostash` before every
  commit. The same session implements Src from the plan, in the order its §0 sets.
- **While working from this brief, edit `Tests/` only.** Never edit `Specs/` or `Checks/`
  (except the one switch in §B.10); Src fixes a test exposes go through the plan. If a spec looks wrong, or a test can't be written honestly
  against it, stop and report it to Dan/Hobson with the REQ ID. Never edit the spec, and never
  weaken a test to get a pass.
- **The three load-bearing constraints (README):**
  1. For Part A, draft test names from the spec alone, before you read any Src.
  2. Run the name-quality check, then commit the names as failing placeholders
     (`Assert.Fail "Not yet implemented"`). The committed name is the contract. Only after
     that do you read Src. Src can change how a test reaches a behaviour, never what it
     asserts. To renegotiate a name, say so out loud.
  3. Watch every new assertion fail before you call it done: perturb the expected value, run
     it, read the failure, restore it, and report the output.
- **Public repository:** no institution names, people, account numbers or real amounts in
  tests, fixtures or commit messages. Use the existing fictional fixtures (e.g. Acme Insurance).
- **Running tests:** `Tests.Isolated` needs no database. `Tests.Integrated` needs the
  `sonofleo_test` database. If it isn't there, say so. Don't skip, fake or weaken anything.
- **Commit subjects** describe behaviour, not the edit. Follow the repo's existing style.

**Read first:** `README.md`, `Specs/README.md`, `Tests/README.md` and the README in each test
directory you touch, `Skills/TestWriter/SKILL.md` and
`references/bullshit-test-specimens.md`, `Skills/TestNameReview/SKILL.md` (the name-quality
check), and `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, which records rulings you
must not re-litigate (see ROUTE-ORACLE).

---

## Part A — Requirements that changed

The full text is in `git show 3095019 -- Specs/`. Read each REQ in the spec; the summaries
below only tell you where to look. Each item ends with the **test action**. "New" means it
needs citing tests. "Retire" means delete the tests that cite only that ID, or re-cite them
where the item says so.

### A.1 Cutover-critical (do first)

1. **Cancelled Instances and Invoices** (REQ-CF-4.11–4.14, 5.17–5.19, 14.10; amended 4.9,
   7.14, 8.2–8.4, 9.10, 13.1).
   - What can be cancelled: an operator may cancel an Instance or an Invoice that will never
     be paid. A reason note is required: not blank, at most 500 characters.
   - When it is refused: while a Payment exists on the thing being cancelled.
   - Cascade: cancelling an Instance cancels its Invoices, and they carry the Instance's
     reason.
   - Cancellation is terminal: there is no un-cancel, no update, and no new Payments. A
     cancelled Invoice still holds its (Instance, Payment Agreement) slot.
   - What it drops out of: the projection's inflows and outflows, bills to chase, invoice
     matching, and every list of open Instances.
   - "Open" Instance: neither fulfilled nor cancelled.
   - "Fulfilled" Instance: at least one FullyPaid Invoice, and every Invoice is FullyPaid or
     cancelled.
   - **New tests** for every one of those clauses, including the exclusions, at the route
     level where an operator reaches it.
2. **Link guard** (REQ-CF-12.9, new). Re-pointing or deleting a Payment Agreement Link whose
   line a Payment references is refused with a typed error naming the Payment. Deleting the
   Payment (REQ-CF-14.6) removes the link. **New:** refused re-point, refused delete, nothing
   changed after either, and the delete-Payment-then-link-gone path.
3. **Payments-to-posted refuses a voided target** (REQ-CF-10.8, new; §10 preamble amended).
   Flow: a staged entry posts, its journal entry is voided, and then the operator runs the
   transition. The transition fails as a whole, names every affected Payment and its journal
   entry, and writes nothing. The preamble also says batch post does not touch Payments: until
   the transition runs, a posted line's Payment still resolves to Staged. **New:** the
   post→void→transition→refused path (with two affected Payments, so "every" is tested), and
   a check that batch post alone leaves Payments Staged.
4. **Leg selection** (REQ-CF-12.4, rewritten).
   - Which lines are kept: the lines the claiming rule matched. Description and source
     criteria apply to every line alike; amount and line type pick individual lines.
   - When more than one line is kept, the expected-account direction default breaks the tie.
     It never vetoes a single matched line.
   - Example: a split tenant payment, rent on one line and a utility share on another, links
     each line to its own agreement by amount.
   - **Re-examine** every existing REQ-CF-12.4 test against the new text.
   - **New:**
     - an amount-matched split;
     - an Outgo agreement taking a refund, where the rule matches the Credit line and that
       line is kept;
     - a multi-line tie resolved by the default;
     - a tie the default can't resolve, which is reported, not guessed.
5. **REQ-CF-6.9 withdrawn.** A Payment Agreement's accounts are an expectation, not a
   constraint: a Payment may point at a line on a different account than the agreement names.
   **Retire** the REQ-CF-6.9 citations in `LinkageAndMatching.fs` and `PaymentDataStates.fs`.
   Rows that asserted a rejection of a wrong-account line now describe withdrawn behaviour.
   Where the scenario still matters, rewrite it as an acceptance.
6. **Invoice matching** (REQ-CF-13.1, 13.4, 13.9, 12.7).
   - Tie-break: candidates with the same due date go in the order the Invoices were entered,
     first entered first.
   - Posted-to-FI date: a matching-created Payment takes its staged entry's date.
   - Blockers: when matching brings a blocked Invoice to FullyPaid, it clears the blocker and
     reports it.
   - Result: carries every link created with its ID, each decision's link ID, and every
     cleared blocker.
   - Creating a link on an already-linked line: refused, with an error naming the existing
     link's ID and its agreement name.
   - **New** for each of these.
7. **Dedup reports paid repeats** (REQ-STG-7.5.1, amended). Besides the Ingested entries, dedup
   now returns every repeat it declined to flag because a Payment references it, whatever the
   repeat's status. **New:** a theory over Classified, NoMatch, Conflict and Reviewed paid
   repeats, each appearing in the declined list.
8. **Pre-posting review: rule name and payments** (REQ-RPT-7.3, 7.4).
   - Rule name: only account-claimant matches count. Take the latest run that recorded such a
     match for the line's current account. Within that run the winner is the lowest priority
     value, then rule name.
   - Payments: a line carries every Payment referencing it, ordered by Instance date then due
     date.
   - **New:** a two-rule tie in which the **lower priority value** is named, and a line with
     two Payments in order. Part B #124/#314 fixes the existing 7.3 test, which named the
     wrong rule.

### A.2 New capabilities

9. **Payment Agreement update and add-a-leg** (REQ-CF-14.8, 14.9).
   - Updatable fields: name, expected amount, days-due, memo, and the debit and credit
     accounts (given by code), each subject to §3.
   - Existing Invoices and Payments are unchanged by an update.
   - A leg can be added to a Master Agreement. There is no means to remove one.
   - **New:** each field, an unknown account code, a §3 violation, existing Invoices
     unchanged, an added leg, and an update naming no field.
10. **List agreements and fetch open Instances** (REQ-CF-14.11, 14.12). Both are read-only
    listings with a fixed order; see the REQ text for the fields.
    - **New:** the full content and order of each, derived from fixtures, not from the code
      under test.
    - A cancelled or fulfilled Instance must be absent from the open list.
11. **Master Agreement update** (REQ-CF-14.2, amended; REQ-CF-4.6 scoped to creation).
    - A cadence change leaves existing Instances alone.
    - An updated next-instance date must be later than the latest existing Instance, or it is
      refused with a typed error.
    - **New:** both clauses.
12. **Money compare and sign** (REQ-MON-2.10, 2.11, new). Compare two Money values (equal,
    less, greater and the inclusive forms), and test sign (positive, zero, negative).
    **New:** isolated theories over boundary values, including max and min Money.
13. **Deactivated-account look-back** (REQ-RPT-5.4, new). The integrity computation also
    reports every account that is deactivated as of today with a non-zero balance. Each comes
    with its code, name, active-end date, balance, and the journal entries touching it that
    were posted or voided after its active end.
    - **New:** a backdated post onto a retired account appears.
    - **New:** a void that leaves a residue appears.
    - **New:** a retired account at zero does not appear.

### A.3 System-wide changes

14. **Same-value updates** (REQ-SYS-6.1). This is now the system default: an update naming no
    field is a no-op and is rejected; a field re-sent at its stored value counts as named and
    succeeds. **New:** at least one route-level case per updatable entity family (Account
    deactivate excepted), and co-cite REQ-SYS-6.1 on the existing CF-14.2, CR-6.2 and
    STG-6.3.2 same-value tests.
15. **Reads carry fetch-only** (REQ-SYS-3.4). Retire the tautological test (#136). Treat
    "writes" as including report contents and file names: a rendered report's footer instant
    and its run date both equal the operation's initiation instant (REQ-RPT-3.2, REQ-SYS-3.4).
    **New:** one footer-instant test and one processed-file-name test against the instant.
16. **Typed not-found errors** (REQ-SYS-2.1.2 now names REQ-SYS-6.2/6.3 as deliberate
    exceptions). Disposition rows #129/#130: an update by a
    fresh Guid for Master Agreement, Payment Agreement, Invoice, Create Payment, and Delete
    Payment Agreement Link each fail with the domain's not-found error. **New** for each.
17. **REQ-NGUI-1.6 waiver revoked** (#269). Every account reference in a return payload carries
    a name: the account itself, its parent, and an agreement's debit and credit accounts.
    **New:** one test over every return contract that carries an account code.
18. **REQ-DAL-2.2 waiver removed** (#239). Prefix the existing rows-affected DAL tests with
    REQ-DAL-2.2 (see Part B).

### A.4 Withdrawals, waivers and re-citations

19. **Memo no longer a match criterion.** REQ-CR-2.2 and REQ-CR-1.25 are withdrawn;
    REQ-CR-1.13 now lists four targets. Retire the REQ-CR-2.2 tests in
    `FieldMatchEvaluation.fs` and the REQ-CR-1.25 tests in `ClassificationClaimantsAndRuns.fs`.
20. **Journal entry reads superseded.** REQ-JE-3.6, 3.6.1, 3.6.2, 3.9, 3.9.1 and 3.9.3 are
    withdrawn, superseded by REQ-AC-3.12–3.12.4 and 3.13/3.13.1. In `AccountBalance.fs`,
    `AccountActivity.fs` and `AccountRoutes.fs`:
    - delete the 3.9.3 sort tests (#093);
    - retire the 3.6.2 tests or re-cite them to AC-3.13.1 (#094);
    - re-cite the FetchBalances route to AC-3.13 (#099).
    - A sign-direction test re-cited to AC-3.13 keeps its `> zero` assertion: that ruling
      (IDIOM-JE-1) carries over.
21. **Newly waived, so drop the citation:**
    - REQ-STG-2.22, in `StageEntryIngestion.fs` (#083);
    - REQ-JE-1.3, in the "accepts valid string" test in `JournalEntryComponent.fs` (#102).
    - The null clauses of JE-1.42, 1.44 and 1.54 are waived too. Their other clauses stay
      tested.
22. **Clarified or amended, needing a test where none pins the new wording:**
    - REQ-AC-3.12.3 (#212): an account whose lines are all voided is omitted under the
      unvoided-only flag, while a line-less account is still returned.
    - REQ-AC-4.5: a scheduled future end also blocks deactivation.
    - REQ-AC-4.6: lines of voided entries don't block deactivation (#038).
    - REQ-CR-5.4: payment-agreement-claimant rules sort after account-claimant rules in both
      directions, then by name. **New:** a sort test with a PA-claimant rule in the table.
    - REQ-CR-8.5: grouped by line, then priority, then name. Don't assert any order between
      lines.
    - REQ-CR-1.26: a pattern timeout fails the whole run with a typed error naming the rule.
      The existing test traces as it stands.
    - REQ-CR-1.18: a whitespace-only pattern is legal.
    - REQ-STG-6.2: the operator cannot set journal-entry IDs.
    - REQ-STG-3.4: source_file is the import path.
    - REQ-STG-10.2: every filter except description is an exact match.
    - REQ-STG-2.7: moved under §4 in derived form. Remove the test's reinterpretation comment.
    - REQ-STG-6.5: "recorded" governs (#072).
    - REQ-RPT-6.1: no roll-up into parents; a parent with no lines of its own does not appear.
23. **Definitions:** classification match rows and staged-entry transitions are insert-only log
    records, not entities. Don't write REQ-SYS-3.1 timestamp tests for them.

---

## Part B — Existing-test repairs (disposition rows owned by test-agent)

These fix tests that already exist, so naming files, error cases and specimens is fine. The
row numbers refer to `Audit/2026-10-03a/99-disposition.md`; read the full ruling there. Verify
the line numbers before editing.

### B.1 Cutover

- **#008** Classification verbs: route tests through `routeUiCommandForTesting` for
  create/re-point by Payment Agreement name, unknown name → typed error, delete returning the
  pre-delete row, and ClassifyPaymentAgreements' decision log, outcome mapping and open
  Instances.
- **#009** CashFlow `CreateUpcomingInstances` route: horizon in, InstanceCompositeReturn list
  out; an out-of-range horizon is rejected at the route.
- **#124/#314** (spec #271) REQ-RPT-7.3: the test currently names the losing rule as the
  winner. The priority-10 rule must be the expected name.

### B.2 Account (AC)

- **#024/#033** Type/subtype combination: integrated create with a disallowed pair (Equity+Cash,
  Liability/Cash) → `AccountInvalidTypeSubtypeCombo`, nothing stored.
- **#029** REQ-AC-3.9: expected active set from raw fixture dates with plain comparisons, not
  `ActivityPeriod.isActive`; ID-set equality.
- **#030** REQ-AC-3.7 and **#041** REQ-AC-3.6: ID-set equality against fixture-derived sets.
  Drop the hard-wired list.
- **#031** REQ-AC-3.13.3: match `AccountBalanceFetchInvalidArguments`, or re-cite the route
  theory's emptyList row.
- **#032** REQ-AC-2.4: type and subtype accept tests become theories asserting the exact case;
  add a wrong-case "asset" rejection.
- **#035** REQ-AC-1.5: assert a lower-cased code differs, create it, and resolve both codes to
  their own accounts.
- **#036/#090/#096** Specimen 2 "generates UUID": two creations, IDs differ from each other and
  from every fixture ID (accounts, fiscal periods, JE lines/references vs header).
- **#037** REQ-AC-4.3 positive: a parent whose only child ended before today deactivates.
- **#038** REQ-AC-4.6: inclusive boundary succeeds; a voided entry after the end doesn't block.
  Delete the stale to-do comment in the test.
- **#039** REQ-AC-4.4: zero unvoided net alongside a voided one-sided entry → succeeds.
- **#040** REQ-AC-2.21: route create sends every field, reads back independently, asserts each.

### B.3 CashFlow (CF)

- **#022/#046** REQ-CF-4.9: the three tests pass on the no-op error. Rewrite them to flip
  is-fulfilled through a real Payment add or delete and assert the stored flag, now including
  the cancelled clause from Part A item 1. Match typed cases for 4.3 and 4.10. Add a REQ-SYS-6.1
  UpdateInvoice no-op test.
- **#016** Annually with NthWeekDay and with (February, Last) rows in the REQ-CF-2.25 fit
  theory and the REQ-CF-7.3/4.8 sweep tests, including 2027-02-28 → 2028-02-29.
- **#042** Duplicate agreement names: rename the five tests so they don't promise a typed
  error, and match the specific DAL unique-violation case.
- **#043/#044/#045/#047/#048** Specimen 4 across the CF data-state suites: match the typed
  case named in each ruling. Where the real error is JSON deserialisation, match that and drop
  "typed error" from the name.
  - **#043's wrong-account rows assert REQ-CF-6.9, which is now withdrawn.** Handle them under
    Part A item 5; do not re-assert a rejection.
  - **#048's** 14.4/14.5 atomicity tests match the 9.3/9.4 case before asserting that nothing
    was written. Matching may now clear a blocker (Part A item 6), but a manual Payment add may not, so
    confirm against the spec.
- **#049** AgreementMemo: 2000 accepted, 2001 → TooLong, blank/whitespace → IsEmpty.
- **#050** CreateAgreement/UpdateAgreement end = start−1 rejected, end = start stored.
- **#051** REQ-CF-14.2 same value succeeds (co-cite REQ-SYS-6.1).
- **#052/#053/#054** Specimen 6/1 in the projection and open-Instance tests: derive the
  expected values from fixture data and scenario inputs, not from the code path under test.
- **#055** REQ-CF-8.4: the set of bill names equals the two leg names; assert instanceDate.
- **#056** REQ-CF-6.7: `Assert.Equal(Some "a memo", …)`.
- **#138** REQ-SYS-5.1: whole-record comparison for both agreement types; add created/modified
  round-trips for Instance, Invoice and Payment.
- **#134** REQ-SYS-1.1: rename (no typed error); match the DAL case whose PostgresException
  names the payment_agreement name key.
- **#135** REQ-SYS-1.1 trim: isolated exact-equality constructor tests for InvoiceMemo,
  BlockerNote, ExternalInvoiceId, AgreementMemo, PaymentMemo and SourceFile.

### B.4 Classification (CR)

- **#058** Invalid-pattern route test: name-filtered fetch plus `Assert.Empty`, not `isError`.
- **#059** REQ-CR-8.5: expected pairs from `first.results`; none of `second`'s IDs returned.
  Respect the amended 8.5 ordering (Part A item 22).
- **#060** REQ-CR-5.6: one error case per row, a Wrong-error arm, and collapse with the
  RevisedRequirementsClassification test.
- **#061** REQ-CR-4.5/6.5: timestamps equal the context's initiation instant exactly.
- **#062** Unknown account code on rule create/update → `AccountCodeDoesntMatchAccountId`
  carrying the code; nothing written or changed.

### B.5 Data access (DAL)

- **#066** REQ-DAL-2.4 success path through `runCommandRouteAndAutoCompleteTransaction`: no idle
  session, pool back at baseline, probe visible from a fresh context, cleanup in `finally`.
- **#067** REQ-DAL-2.4 exception path through `runCommandRouteAndAutoRollback`.
- **#068** Narrow the lookup-cache test name. Don't add a Src reset hook.
- **#239** Prefix the rows-affected tests (the Theory row, the "finds none returns DalNoOp"
  tests, the whenNoRows test) with REQ-DAL-2.2.

### B.6 Data ingestion (STG)

- **#070** The five Specimen 4 sites in `StagingIngestionRules.fs`: typed matches. The 6.7 site
  matches `IngestionPaidStageEntryCannotBeExcluded`.
- **#023/#073** Zero and negative amounts, at ingest and on manual update →
  `IngestionStageLineNonPositiveAmount`, with nothing staged or changed.
- **#074** Empty or whitespace group_id → `IngestionFileRejected` carrying
  `IngestionBaseStageEntryGroupIdIsEmpty`.
- **#072** (spec #244) Remove an unmatched line from a NoMatch entry after a run → succeeds.
- **#075** Co-cite REQ-STG-3.3/3.10 on the mixed-file route test; the orchestrator test becomes
  REQ-STG-1.14 only.
- **#076** REQ-STG-8.3 goes on the shadow route test; the StageEntryPosting test becomes
  REQ-STG-9.2/9.4.
- **#077** Shadow route post against the closed-period group → `JournalEntryHeaderEntryDateInvalid`.
  Rename the orchestrator test to batch post (REQ-STG-9.2).
- **#078** Move REQ-STG-4.5 onto the 7.2 Ignored-original test; delete the NotEmpty-only test.
- **#079** Delete the NotEmpty-only REQ-STG-5.1 test.
- **#080** Delete the "deliberately toothless" 9.1 orchestrator test.
- **#081** Compare grp-008's full line set against the input rows.
- **#082** Derive the staged count and the reference set from `buildTestRows`.
- **#084** Re-cite the CreateIngestionSource route test as REQ-STG-3.15.
- **#085** Recorded rule set equals winner plus loser (REQ-STG-5.10).
- **#025/#086** Add the REQ-STG-10.2 journal-entry ID and line ID filter tests; delete the
  commented-out rule-ID block.
- **#087/#105** Rewrite the REQ-ID comments without an ID the file backs, in prose
  (RevisedRequirementsStaging.fs:46, StagingIngestionRules.fs:182, TestDataStage.fs
  876/901/1039 and the #105 fixture comment).

### B.7 Fiscal period (FP)

- **#089** The route create test reads back start/end/isOpen against literals for a
  distant-year key; re-cite the invalid-key test to REQ-FP-1.2.
- **#091** Exact open-key set equality.
- **#092** Negative rows 226-01, 20266-01, 2026-1, 2026-011; the happy path asserts the value
  "2026-06".

### B.8 Journal entry (JE)

- **#093/#094/#099**: see Part A item 20.
- **#095** The whitespace-only tests pass `"     "`.
- **#097** REQ-JE-1.48: assert each entry's (fi, ref) list, duplicates included; fetch by
  fixture-derived ID sets.
- **#098** Take REQ-JE-2.12 off the in-transaction test; add a route-level unbalanced post read
  back through a fresh FetchOnly context (Form 4); co-cite on the route atomicity tests.
- **#100** REQ-JE-4.3: `voidedAt = Some(initiation instant)`.
- **#103** Replace isOk with a value railroad (e.g. `String('A', 1000)`).
- **#104/#133** Match `JournalEntryCommentPrimaryAndSecondaryIdsAreSame` with Wrong-error and Ok
  arms. `Context.updateInitiationInstant` is moving to Tests.Helpers (impl plan #004); follow
  it.
- **#132** REQ-SYS-8.1: match `DalErrorDuringAutoCompleteTransactionRun` with the exact message;
  capture the header ID inside the function and assert it is absent from a fresh context.

### B.9 Money, interface and reports

- **#106** Literal Money limits: 9999999999.99 Ok, 10000000000.00 ExceededMax, and the mirror
  pair for the minimum.
- **#107** REQ-MON-2.3.2: unsorted input containing a duplicate.
- **#108** 100.00/3 → [33.34; 33.33; 33.33]; the full share list for 419.97/30.
- **#109** −1.05/2 → [−0.52; −0.53]; rename the test to "away from zero".
- **#110** REQ-MON-2.4.1 sum-reconciliation theory (negative amounts, both remainder signs,
  0.01/3, near max).
- **#113** Delete the RouteResolver-copy REQ-NGUI-4.5 test.
- **#114** Drop the `Assert.Matches`; compare the full message with runtime frame lines stripped
  on both sides. After this lands, tell Hobson: NGUI-AQ-1 in resolved-findings gets rewritten.
- **#115** `Assert.Equal(1, exitCode)` in the NGUI-3.7 and 4.4 failure tests.
- **#116** Replace the lone `NotEqual(0, …)` in FileArgumentPosition.fs (see the ruling).
- **#117** Explicit match capturing the created account ID for cleanup; remove the unused
  context.
- **#012/#118/#119** Report headers: assert the trial balance and integrity title and as-of
  date (REQ-RPT-3.1), and the 7.7 run date plus the exact "<n> entries, <m> lines" derived from
  the staged data.
- **#120** Delete the target file first; assert the fully qualified exact path, the doctype and
  a fixture account code.
- **#121** Assert the boundary row's name and generation from the fixture.
- **#122** Drop the Specimen 6 set equality; make the exclusion theory a full eight-status table.
- **#123** Use the same description for all four entries, or reverse-ordered labels.
- **#125** Reconcile against an untouched account; `Assert.Equal` on the staged amount.
- **#127** Derive every (code, generation) pair by walking parentId.
- **#136** Delete the tautological REQ-SYS-3.4 test.

### B.10 `Result.isError` sweep (#149) — last

`bash Checks/check-result-iserror.sh` lists every `Result.isError` under Tests/, about 70
sites. Most of them are covered by the items above. Rewrite each remaining site to match the
specific typed case, with both escape arms failing. The list-filter use in
`MasterAgreementDataStates.fs:212` goes too. **When the count reaches zero,** change the
script's final `exit 0` to `exit 1` so it enforces from then on. That one-line change is the
only edit you make outside `Tests/`.

---

## Final report (append it to §8 of the plan, with the Src report)

- What you did, by Part A item and Part B row number, and what you did not do, with the reason.
- Test status: which suites ran, where, and the pass/fail counts. If `sonofleo_test` was
  absent, say so.
- Every assertion's fail-then-pass evidence (constraint 3), summarised.
- Every step-11 failure: the test, the REQ, and whether the bug is in **Src** or the **spec**.
- Output of `bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` on the branch:
  Invariant 1 (no citation of an unknown or withdrawn ID) and Invariant 2 (every active REQ is
  tested or waived). List any untested active REQ.
- Output of `bash Checks/run-all.sh`.
