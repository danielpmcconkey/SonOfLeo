# Plan — Audit 2026-10-03a remediation (Src)

Written 2026-10-03 by Hobson for a Claude Code session that can see this
repository and nothing else. Everything you need is either in this document
or in the repo. Where this plan and `Specs/Behavioral/` disagree, the spec
wins — tell Dan.

## 0. Start here — one session does Src and tests, in this order

You are doing both halves of the slice loop: Src from this plan, Tests from
`HobsonsNotes/brief-test-agent-audit-remediation-2026-10-03.md` (the brief).
README constraint 1 says tests are named from the spec by someone who hasn't
seen the implementation. You can't un-see code, so the order is the
safeguard:

1. **Name first, from the spec alone.** Read §2's reading list *except* Src,
   then the brief's §0 and Part A. Draft test names for every Part A item,
   run the name-quality check, and commit them as failing placeholders
   (`Assert.Fail "Not yet implemented"`). **Open nothing under `Src/` until
   that commit is pushed.** Existing tests may be read for naming and
   fixture conventions.
2. **Implement** Part A of this plan, then Part B, then Part C. Commit as you go.
3. **Test.** Brief Part A: now read Src; if a committed name is aimed wrong,
   say so in your report and renegotiate it out loud (never quietly soften
   it). Write each test and watch every assertion fail before it passes
   (constraint 3). Then brief Part B (existing-test repairs), ending with the
   `Result.isError` sweep.
4. **Finish** with this plan's item 30 and the brief's final report, both
   into §8 of this file.

A test that fails in step 3 is either a Src bug (fix it) or a spec bug
(record it in the report with the REQ ID; don't edit the spec).

## 1. What this is for

SonOfLeo is Dan's personal-finance system: a cash-basis double-entry ledger
in F# on .NET 10 and PostgreSQL. The milestone is **"SonOfLeo runs
Saturday"** — Dan's weekly import, classify, reconcile, post and report
routine. The target order of operations is §1 of
`HobsonsNotes/plan-saturday-readiness-2026-09-26.md` (steps 11–13 amended
2026-10-03).

Audit `2026-10-03a` ran 39 auditors over the code, tests and specs. Every
finding has been ruled on in `Audit/2026-10-03a/99-disposition.md` (columns:
`#`, finding, owner, status, ruling). Hobson has already applied the spec
side (commit `3095019` on this branch). **This plan is the Src side:** every
row owned by `impl-agent`, plus the code the amended specs now require.
Items cite their disposition rows as `#NNN`; read the row before starting an
item — the ruling is the instruction.

## 2. Before you touch anything, read

- `README.md` — the slice loop and the three load-bearing constraints.
- `Specs/README.md` — requirement grammar, the traceability gate, the rule
  that specs never name source files and source never carries REQ
  annotations.
- `Src/README.md`, and `Tests/README.md` for what tests will hold you to.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped. Follow it.
  (Some of it still says `Model/` and `ModelOrchestrator`; read those as the
  Business-tier entity modules and `Business.CrossDomainOrchestration`.)
- `Skills/ArchReviewer/SKILL.md` — run it over your own diffs before you
  report.
- `CompoundedLearnings/` — settled judgment calls, especially
  `validation-layers.md`, `dal-errors-are-backstops.md`,
  `field-update-pattern.md`, `temporal-arithmetic.md` (all updated
  2026-10-03).
- `git show 3095019 -- Specs/` — exactly what changed in the spec.
- `Audit/2026-10-03a/99-disposition.md` — the rulings.

## 3. Standing rules

- **You write `Src/`, `DbMigration/Scripts/` and `Tests/`; never `Specs/`.**
  During implementation (§0 step 2), when a Src deletion, rename or move
  breaks the build of an existing test, make the minimal mechanical fix
  (re-point a call, delete a test of a deleted function) and list it; the
  real test work waits for step 3.
- **Branch `audit-remediation-20261003`.** `git pull --rebase --autostash`
  before every commit. Only Dan merges to `main`.
- **Public repository.** No institution names, account numbers, people's
  names, or real amounts in code, comments, migrations or commit messages.
- **Dan is the authority.** If a ruling or requirement looks wrong, make the
  case once in your report; don't edit a spec, and don't quietly do
  something else.
- **No REQ annotations in source.** A comment may name a rule to explain
  *why* code is shaped a certain way; it never tags a site.
- **Structural integrity in the schema (foreign keys, unique keys); every
  business rule in the application layer** (REQ-DAL-3.6).
- **Infallible create, orchestrator validates.** `create` is total;
  orchestrators validate. `reconstitute` validates everything that needs no
  database read (#311).
- **Loud beats bubblewrap.** A failure that already aborts with an error
  needs no new guard and no new error case. Only silent failures earn code.
- **Dead code goes.** No caller in Src and no requirement → delete it.
- **Never create a record to absorb a failed lookup.** Fail loudly.
- **Code never breaks a tie** unless a requirement names the tie-break.
- **Tuple lists of domain primitives in construct functions are
  intentional.** Don't introduce "create input" records.
- **Fully qualify common DU case names** (`JournalEntryLineType.Debit`,
  `StagedEntryStatus.Posted`).
- **All ledger currency arithmetic and comparison goes through `Money`**
  (REQ-MON-2.1, amended). No `Money.amount` to compare or test a sign.
- **Every operation uses its context's initiation instant** for every
  timestamp and every derived "current date" (REQ-SYS-3.4).
  `Checks/check-clock.sh` enforces it.
- **Migrations:** new scripts in `DbMigration/Scripts/` named
  `YYYYMMDDHHMM-PascalCaseDescription.sql`, with a header comment saying why.
  Never run anything against production; Dan applies migrations by hand.
- Commit subjects describe the behaviour, not the edit ("Payments-to-posted
  refuses a line whose journal entry was voided"). Match the log.
- **Before every hand-off:** green build, `Tests.Isolated` green, and
  `bash Checks/run-all.sh` green, with the output in your report.
  `Tests.Integrated` needs the `sonofleo_test` database; if you don't have
  it, say so — don't skip or fake.

## 4. The work

File references are where the problem was found on 2026-10-03; **verify
before editing**. Do Part A first, in order where an item says it depends on
another. Part B can follow in any order. Part C comes last: it moves and
reshapes code that A and B touch, so finishing A and B first avoids doing
them twice. All three parts are in scope for this branch.

### A. Cutover — Saturday needs these

1. **Fresh clock reads.** (#007, #290, #275, #302, #199, #140; REQ-SYS-3.4,
   REQ-RPT-3.2.) The pre-posting review's run date
   (`Ui.InterfaceBridge/Routes/ReportRoutes.fs:45`, `Calendar.today()`) comes
   from `context |> Context.getInitiationInstant |> Calendar.dateFromInstant`.
   The report footer (`ReportVisualizationAssets/ReportFooter.fs:7`,
   `Clock.now()`) takes the instant as a parameter. The processed-file prefix
   (`Routes/IngestionRoutes.fs:89`, `Clock.now()`) uses the ingest
   operation's initiation instant. Afterwards `check-clock` must pass — today
   it fails on exactly these three sites.

2. **Pre-posting review names the wrong rule.** (#124, #271, #314;
   REQ-RPT-7.3.) `Business.CrossDomainOrchestration/PrePostingReview.fs`
   `ruleNameForLine` (~56–83) sorts by `-(priority)`, so it names the
   *losing* rule. Sort ascending by priority, then rule name. Also check the
   run choice against the amended text: "most recent run" is the latest run
   that recorded an account-claimant match **with the line's current
   account**. Today `latestRun` is taken across all account matches before
   filtering to the current account — fix that if so.

3. **Pre-posting review: every Payment, and their IDs.** (#272, #175;
   REQ-RPT-7.4.) A line carries every Payment that references it, each with
   its Invoice and Instance fields, ordered by Instance date then due date.
   Add `paymentId` and `invoiceId` to `PrePostingPaymentReturnRow`
   (`InterfaceContracts/ReportsContracts.fs:98`).

4. **Rule-group persistence DTO, and Memo leaves the match criteria.**
   (#189, #234; REQ-CR-1.13, REQ-CR-2.2 withdrawn, REQ-STG-5.2.) Stored rule
   JSON freezes when rules load into production at cutover, so do this now.
   - Delete `FieldMatch.Memo` (`Business.FinancialServices.Classification/FieldMatch.fs:11`),
     its contract case (`InterfaceContracts/ClassificationContracts.fs:23`),
     its converters (`BoundaryConverters/ClassificationFieldConverters.fs:29, 82`)
     and its arm in `confirmStoredPatternsAreValid`
     (`ClassificationRule.fs:111`). Remove memo from the candidate a rule
     evaluates against if nothing else uses it.
   - Add an explicit persistence DTO for rule groups (plain records and
     strings) mapped through the smart constructors on read; point the JSONB
     SQL at its stable field names.
   - On read, validate stored amounts through the validating Money
     conversion as well as stored text patterns (REQ-CR-1.21 amended, #311).
     Today amounts deserialise straight into `Money`.

5. **Leg selection: the matched lines govern.** (#218, #234; REQ-CF-12.4
   rewritten, REQ-CR-1.25 withdrawn, REQ-CF-6.9 withdrawn.) Depends on 4.
   Read the new REQ-CF-12.4 in full. In `CashFlowOps.fs`
   `selectLegsOfClaimedEntries` (~199–260) the kept lines become the lines a
   claiming rule matched; the expected-account default (credit account and
   Credit line for Income, debit account and Debit line for Outgo) only
   breaks a tie among several kept lines. Delete
   `ClassificationRule.constrainsLineType` (`ClassificationRule.fs:226–`)
   and `isClaimedByLineTypeRule`. No kept line, or no single survivor, is
   reported with its reason.

6. **REQ-CF-6.9 is withdrawn: drop its enforcement.** (#014, #020, #057,
   #178.) A Payment Agreement's accounts are an expectation, not a
   constraint. `InstanceOrchestration.fs:~113` rejects a Payment whose line
   is not on the agreement's account
   (`CashflowPaymentLineNotOnAgreementAccount`). Delete the check and the
   case. Note for your report: disposition #043 asked the test agent to match
   that case; those rows will now succeed.

7. **Payment Agreement update and add-a-leg.** (#014, #020, #057, #178;
   REQ-CF-14.8, REQ-CF-14.9, REQ-SYS-6.1.) Depends on 6. The
   `UpdateAgreement` route passes an empty leg-update list
   (`Routes/CashFlowRoutes.fs:78`). Give the input contract optional
   Payment Agreement updates (name, expected amount, days-due, memo, debit
   and credit accounts by code) and new legs, subject to §3. A field re-sent
   at its stored value counts as named. Existing Invoices and Payments are
   untouched. No removal of a leg.

8. **Master Agreement update: next-instance date.** (#219 (a)(b);
   REQ-CF-14.2, REQ-CF-4.6.) An updated next-instance date must be later than
   every existing Instance's date; otherwise a typed error naming the latest
   existing date. A cadence change does not re-validate existing Instances.
   (#219 (c) is moot: it depended on REQ-CF-6.9.)

9. **Link guard.** (#011, #015; REQ-CF-12.9.) Re-pointing or deleting a
   Payment Agreement Link whose staged line a Payment references is rejected
   with a typed error naming the Payment. `PaymentAgreementLink.update`
   (~185) and `delete` (~226) have no guard today; put it in orchestration.
   `DeletePayment` already removes the link (REQ-CF-14.6) — leave that path
   alone.

10. **Link IDs in the linkage result and the already-linked error.** (#175;
    REQ-CF-12.7, REQ-CF-13.9.) `CashflowPaymentAgreementLinkLineAlreadyLinked`
    (`CashFlowError.fs:69`, raised at `CashFlowOps.fs:~640`) carries the
    existing link's ID and the agreement's *name*. The decision log carries
    the ID of every link it produced.

11. **Payments-to-posted refuses a voided target.** (#177, #202, #306, #317;
    REQ-CF-10.8.) `CashFlowOps.transitionPaymentsToPosted` (~886): when the
    journal entry line a staged line records belongs to a voided entry, the
    whole transition fails and rolls back, with a typed error naming
    **every** such Payment and its journal entry. Payments-to-posted stays a
    separate operation — do not fold it into batch post (#028).

12. **Dedup reports the paid repeats it declined to flag.** (#071, #245,
    #316; REQ-STG-7.5.1, REQ-STG-6.7.)
    `StageEntryOrchestration.deduplicateStagedEntries` (~340) returns only
    entries still `'Ingested'`, so a paid repeat that is
    `'Classified'`/`'NoMatch'`/`'Conflict'` vanishes. The result gains a
    second list: every repeat declined because of a Payment, whatever its
    status. Update the route's return contract.

13. **Cancelled Instances and Invoices.** (#176; REQ-CF-4.9, 4.11–4.14,
    5.17–5.19, 7.14, 8.2–8.4, 9.10, 13.1, 14.10.) Read all of these first.
    - Migration: a nullable cancellation reason note column (varchar 500) on
      `cashflow.instance` and on `cashflow.invoice`. Cancelled = the note is
      present. No CHECK constraints (REQ-DAL-3.6).
    - Domain: the note type (required, trimmed, ≤ 500); cancel operations
      for an Instance (cascades to its Invoices with the same note) and an
      Invoice; rejection while a Payment exists (typed error naming the
      Invoice); terminal (no un-cancel, no update, no Payment, nothing added
      to a cancelled Instance).
    - Derivations: fulfilled = at least one `'FullyPaid'` Invoice and every
      Invoice `'FullyPaid'` or cancelled. *Open* = neither fulfilled nor
      cancelled.
    - Exclusions: cancelled items drop out of the sweep's open-Instance
      result, projection inflows/outflows, bills to chase (a cancelled
      Invoice still counts as "has an Invoice"), matching candidates and
      every open-Instance list.
    - Routes: `CancelInstance` and `CancelInvoice` (or one verb, if the
      existing route style favours it).

14. **Invoice matching: tie-break and blocker.** (#220, #217, #026;
    REQ-CF-13.1, REQ-CF-13.4, REQ-CF-13.9.) `CashFlowOps.fs:~398` breaks a
    due-date tie by Invoice UUID; use the Invoice's `created_at`. When a
    matched Payment brings a blocked Invoice to `'FullyPaid'`, clear the
    blocker (state and note) and report it in the result. Confirm the
    Payment's posted-to-FI date is the staged entry's date (it should be
    already).

15. **Cash-flow updates set `modified_at`.** (#128, #183, #305, #315;
    REQ-SYS-3.3.) The MasterAgreement, PaymentAgreement, Instance, Invoice
    and Payment UPDATEs never set it. Add `modified_at` from the context's
    initiation instant, as `PaymentAgreementLink.update` does.

16. **Look-back integrity check.** (#204; REQ-RPT-5.4.) The balance-sheet
    integrity computation also returns every deactivated account with a
    non-zero balance as of the operation date: code, name, active-end date,
    balance, and the journal entries touching it posted or voided after its
    active-end date. Data, not an error. Data-only and HTML outputs.

17. **Read-only cash-flow routes.** (#179; REQ-CF-14.11, REQ-CF-14.12.)
    `ListAgreements` (every Master Agreement with its Payment Agreements,
    accounts by code and name, fixed order) and `FetchOpenInstances` (every
    open Instance with Master Agreement name, Invoices and Payments, fixed
    order). Both carry the fetch-only auditable action.

### B. Conformance, dead code, small fixes

18. **Typed not-found on cash-flow updates.** (#129, #130, #280;
    REQ-SYS-6.2, 6.3.) `whenNoRows` on `MasterAgreement.update`/`fetchById`
    (`CashflowMasterAgreementIdDoesntExist`), `PaymentAgreement.update`/
    `fetchById`, `Invoice.fetchById` (as used by the UpdateInvoice and
    CreatePayment converters) and `PaymentAgreementLink.fetchById`.

19. **ClassificationError.** (#018, #065, #151, #154, #174, #191, #236, #283,
    #308, #322.) Create a `ClassificationError` DU in the Classification
    project. Move the classification cases out of `DataIngestionError`, plus
    the DataIngestion cases that name upper-tier concepts.

20. **Money comparison and sign.** (#264, #194; REQ-MON-2.1, 2.10, 2.11.) Add
    compare (equal, <, >, ≤, ≥) and sign (positive, zero, negative)
    functions to `Business.FinancialServices/Money.fs`. Move every ledger
    call site that compares or sign-tests via `Money.amount` onto them.
    Replace the raw-decimal paid/outstanding arithmetic in `CashFlowOps`
    with `Money.sumList` / `subtractVal1FromVal2` and a Money floor-at-zero.

21. **Account names in return payloads.** (#180, #268, #270, #298, #299;
    REQ-NGUI-1.6.) `debitAccountName`/`creditAccountName` on
    `PaymentAgreementReturn` (`CashFlowContracts.fs:107`); `parentName` on
    `AccountReturn`; `accountParentName` on `AccountActivityReturn`. Then
    audit every other return contract: every account code carries a name.

22. **Reconstitute validates what needs no DB read.** (#311, #258.) Cadence
    and every cross-field constraint, in every `reconstitute`. FiscalPeriod:
    build the key through `fromString`, typed error when start/end don't
    match the key's month.

23. **Fiscal period Ensure is atomic.** (#010, #131, #257, #295;
    REQ-SYS-8.1.) `Routes/FiscalPeriodRoutes.fs:27` runs with
    `NoTransaction`; use `runCommandRouteAndAutoCompleteTransaction
    FiscalPeriodEnsure`. No new test.

24. **Configuration.** (#006.) Drop `.AddEnvironmentVariables()`
    (`App.Utility/Config.fs:13`).

25. **`updateInitiationInstant` moves to Tests.Helpers.** (#004, #199,
    #291.) `App.Session/Context.fs:30`. No Src caller; ~60 test call sites
    use it. Move it (this is a permitted mechanical test edit) and describe
    it there as "simulate a separate later operation on the same
    transaction".

26. **Classification rule sort.** (#063, #229; REQ-CR-5.4.) Under an
    account-code sort, payment-agreement claimants (no account code) come
    last in both directions, then by rule name: `NULLS LAST` plus the name
    tie-break (`ClassificationOrchestration.fs:~141`).

27. **Lookups use the trimmed value.** (#296, #297; REQ-SYS-1.1.)
    Agreement-name, payment-agreement-name and period-key converters look up
    the validated value, not the raw string; period key goes through
    `FiscalPeriodKey.fromString`. `sourceLike` goes through the Source smart
    constructor.

28. **Dead code.** (#003, #289, #013, #021, #318, #017, #019, #184, #027,
    #304, #313, #152.) Delete, per each row:
    - DAL: `ManualTransactionResult`, internal `isSome`,
      `ReaderFailedToConvertRawRows`; Execute* match on the Result rather
      than pre-check `isNone`.
    - Cash flow: the unused Payment/Instance field updates and their
      branches; the uncalled `AgreementFilter` predicates, their helpers,
      the unused amount/temporal filter fields and builders, and
      `InstanceOrchestration.fetchFiltered`.
    - `RuleMatch.fetchById`, `fetchByRunIdAndClaimantType`,
      `PaymentAgreement.fetchByName`, `Blocker.toString`,
      `Month.toAbbreviation`, the test-only `PaymentAgreementLink` fetchers,
      `AccountSubtype.validFor`.
    - `JournalEntryFetchFilter.source` and `.unVoidedOnly`.
    - `Ui.InterfaceBridge/BridgeError.fs` and its compile entry.
    - Never-raised error cases, including those listed in #313.
    Where a test calls a deleted function, delete or re-point that test
    mechanically and list it.

29. **Small fixes.**
    - #188 / REQ-CF-6.5: drop `PaymentAmount` from the new-payment input;
      keep the optional posted-to-ledger date (REQ-CF-6.10).
    - #182: reword the UpdateInvoice route description per REQ-CF-9.11.
    - #186: `FiscalPeriodKey.startDate`/`endDate`/`ofDate`; use them at the
      three string-slicing sites.
    - #193: translate only `FiscalPeriodNoPeriodMatchingKey` to
      `JournalEntryDateNotInFiscalPeriod`; pass the rest through.
    - #167: status and line-type strings as `@parameters`; delete the
      self-exempting comment.
    - #173 / #312: `insistBeginValidationBehavior` becomes
      `{ ap with beginValidationBehavior = b }`.
    - #300: `createFullPath` wraps `Path.GetFullPath`.
    - #301: HtmlEncode code and name in
      `TrialBalanceWriter.createAccountRowDomElement`.
    - #303: drop REQ IDs from the PeriodActivityWriter and
      PrePostingReviewWriter comments.
    - #310: the four stale comments named in the row.
    - #293: drop `Calendar.fs`'s unused `UtilityError` and `Config` opens.

30. **Finish.** `bash Checks/run-all.sh` — every check passes, `check-clock`
    included. `check-result-iserror` is report-only until the brief's
    §B.10 sweep brings it to zero; then switch it to `exit 1` (the brief says how).

### C. Architecture — after A and B

31. **Lookup caches move to their owning Business modules.** (#001, #155,
    #286.)
32. **Classification use cases move to classification-level
    orchestration.** (#156, #163.) `classifyAccounts`,
    `classifyPaymentAgreements`. Then the CDO compile reorder (#157).
33. **Compile, project-reference and open order; DU qualification.** (#158,
    #309, #159, #160, #161, #168.)
34. **Model shape.** (#164, #192 `update` backbones; #165 renames to
    `constructNewAndPersist`; #166 single-row `persist`; #169/#198 private
    wrappers; #170, #171 convenience types; #172 `ShadowReconcileInput`;
    #190 `claimant` on `PrioritizedMatch`; #195 one balanced-lines rule;
    #201 Posted pointer keeps staged provenance.)
35. **Business logic out of the UI tier.** (#162.)

## 5. Out of scope

- Specs (Hobson). The Saturday state machine,
  parsers and the tenant-invoice generator.
- Rows the disposition overruled or deferred.

## 6. Open design notes you may hit

- **A Payment Agreement's accounts are an expectation.** Nothing may reject
  a Payment, link or match because the line is on a different account than
  the agreement names. The expected-account default exists only to break a
  tie in leg selection.
- **Cancelled is terminal and still occupies its slot** under REQ-CF-5.16:
  there's no replacing a cancelled Invoice for the same leg.
- **Rule match rows are insert-only records, not entities** (Definitions):
  no `modified_at`.

## 7. When you finish

Report to Dan, at the bottom of this file under `## 8. Report`, together with
the brief's final report: what you
implemented (by item number, rows and REQs), what you didn't and why, every
test file you touched mechanically, the migration scripts Dan must apply,
test status (which suites ran, where), `run-all.sh` output, and any
requirement you believe is wrong. Don't mark anything done that you haven't
verified.

## 8. Report

Branch `audit-remediation-20261003`, written 2026-10-04 by the remediation session. Everything below is pushed.
Nothing has been merged to main and nothing was run against production.

### 8.1 Where it stands

- `dotnet build SonOfLeo.slnx`: green, no `warning FS`.
- Tests.Isolated: **395 / 395 pass.**
- Tests.Integrated, against the throwaway local `sonofleo_test` built by `setup-throwaway-test-db.sh`: **1339 / 1340 pass**,
  two consecutive full runs. The one failure is SweepBehaviour REQ-CF-7.15 (see 8.5, a decision for Dan).
- `bash Checks/run-all.sh`: 10 passed, 0 failed, 1 skipped. Traceability skips off main. `check-clock` passes.
  `check-result-iserror` reached 0 sites and now exits 1 when it finds one.
  `check-apperror-coverage` is report-only: 207 of 267 cases referenced. Repointing it is row #152, assigned to Hobson.
- `bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` on the branch:
  - Invariant 1 (phantoms): clean.
  - Invariant 2 (untested active REQs): clean.
  - Stale waivers: REQ-SYS-6.1. It is waived in SystemWide.md, but the brief asked for it to be cited (A.14). Hobson should drop the waiver row.
- `Skills/ArchiMate/validate.py`: VALID. `model_drift.py`: NO DRIFT.

### 8.2 Migrations Dan must apply

- `DbMigration/Scripts/202610031600-StoredRuleGroupShape.sql`. It converts the stored `rule_groups` JSON to the fixed shape (item 4) and stops rules matching on Memo.
- `DbMigration/Scripts/202610031700-CancellationReasonNote.sql`. It adds the cancellation reason note (item 13).

### 8.3 Src, by plan item

All items 1–29 and 31–35 are implemented. Item 30 is this section's checks.

| Item | Commit | Rows / REQs |
|---|---|---|
| 1 Fresh clock reads | fac9995 | REQ-SYS-3.4, RPT-3.2 |
| 2–3 Pre-posting review rule and payments | 35e6776 | REQ-RPT-7.3, 7.4 |
| 19 ClassificationError | cb74381 | |
| 4 Rule-group DTO, Memo out | 897faf5 | REQ-CR-1.13, CR-2.2/1.25 withdrawn |
| 5 Leg selection | 51bf462 | REQ-CF-12.4 |
| 6 REQ-CF-6.9 withdrawn | 5a06715 | |
| 7 Payment Agreement update and add-a-leg | 02c6056 | REQ-CF-14.8, 14.9 |
| 8 Next-instance date | fc02223 | REQ-CF-14.2 |
| 9–10 Link guard, link IDs | 04e1edd | REQ-CF-12.9, 12.7 |
| 11 Payments-to-posted refuses voided targets | 50b0665 | REQ-CF-10.8 |
| 12 Dedup paid repeats | 3620037 | REQ-STG-7.5.1 |
| 13 Cancellation | 63be282 | REQ-CF-4.11–4.14, 5.17–5.19, 14.10 |
| 14 Matching tie-break and blocker | cbfcd0b | REQ-CF-13.1, 13.4, 13.9 |
| 15 modified_at | 5f5673b | REQ-SYS-3.1 |
| 16 Look-back integrity | bd2b85e | REQ-RPT-5.4 |
| 17 Read-only cash-flow routes | 86bf2ee | REQ-CF-14.11, 14.12 |
| 18 Typed not-found | bd2409a | REQ-SYS-6.2/6.3 |
| 20 Money compare and sign | 08ece4e | REQ-MON-2.10, 2.11 |
| 21 Account names in returns | a092608 | REQ-NGUI-1.6 |
| 22 Reconstitute validates | 261dc92 | |
| 23 Ensure fiscal periods is atomic | f7397f1 | |
| 24 Configuration | 3370c99 | |
| 25 updateInitiationInstant moves to Tests.Helpers | 6b08833 | |
| 26 Rule sort | 4956017 | REQ-CR-5.4 |
| 27 Trimmed lookups | dcb34e7 | REQ-SYS-1.1 |
| 28 Dead code | c4dba72 | |
| 29 Small fixes | 9721815 | #188 and the rest |
| 31 Lookup caches move | 84b1d00 | |
| 32 Classification orchestration | 48530ec | |
| 33 Order and DU qualification | 5f2e26a | Verified IL-identical: 0 differing methods across all assemblies |
| 34 Model shape | a1c7b83, 0d2da3b, 1b0bbac, 8183f2f, 9bba5f4, 60a94c6, 96144dc, a6bb0c4, ae4d5d1 | #164/#192, #165, #166, #169/#198/#170, #171, #172, #190, #195, #201 |
| 35 Business logic out of the UI tier | d1d3b1d | #162 |
| Model | ee74cdb | ClassificationError component and capability; BridgeError removed; 53 serving edges added, 31 removed |

Design decisions worth knowing:

- **Cancellation (13):** cancelling an Instance leaves an Invoice that was already cancelled with its own note. A cancelled Instance takes no composite change at all.
- **Posted pointer (#201):** the Posted pointer is `Posted of JournalEntryLineId * StageEntryLineId option`, and `TransactionPointer.resolve` holds the rule that Posted takes precedence. The side queries `fetchStageEntryLineIdById` and `fetchReferencedStageEntryLineIds` are gone. The checks read the pointer instead.
- **UI tier (#162):**
  - `StageEntryOrchestration.ingestFile` accepts or rejects a file's records as one unit, replacing the test-only `ingestRawToStage`.
  - `postWithTrialBalances` takes the before and after snapshots. The route still decides commit or rollback.
  - `MasterAgreementFieldUpdates` now carries `activeBeginUpdate` and `activeEndUpdate` separately. `AgreementOrchestration.updateAgreement` checks the period they leave, carrying over the date not updated, so the converter no longer reads the agreement. The dead `PostStageEntriesTrialBalancesResult` contract is gone.
- **Item 21:** this item also carries B21's first bullet, debit and credit account names on PaymentAgreementReturn.

### 8.4 Tests

**Part A.** The names were committed as failing placeholders before any Src was read (b6d9df9; 101 names, graded with TestNameReview). After the Src work, five parallel workers implemented every placeholder, each in its own worktree with its own database. All their commits are on the branch, apart from one skipped commit described under "Merge note".

- No Part A name was changed.
- Zero placeholders remain.
- Every new or changed assertion was perturbed, seen failing for the right reason, then restored and seen passing.
- The mutation logs are in the session's scratchpad. For example, the link guard expecting `[Guid.Empty]` fails with "Collections differ".

Part A items:

- 1 Cancellation: 22 integrated and 3 isolated tests.
- 2 Link guard.
- 3 Voided target, with two affected Payments. Batch post alone leaves Payments Staged.
- 4 Leg selection. The amount-split, refund, tie-by-default and unresolvable-tie tests are all in. The old 12.4 tests 1, 2, 3, 5 and 6 were deleted because the new tests duplicate them.
- 5 REQ-CF-6.9 retired. The wrong-account scenarios are now REQ-CF-6.4 acceptances.
- 6 Matching.
- 7 Dedup: the declined-list theory covers Ingested, Classified, NoMatch and Conflict (see 8.6).
- 8 Pre-posting review.
- 9 Payment Agreement update.
- 10 Listings.
- 11 Master Agreement update.
- 12 Money compare and sign.
- 13 Look-back integrity.
- 14 Same-value updates, plus the REQ-SYS-6.1 co-cites.
- 15 Footer instant and processed-file name.
- 16 Typed not-found.
- 17 Account names in payloads: reflection finds all 14 return contracts that carry an account code, and 13 routes are exercised.
- 18 REQ-DAL-2.2.
- 19 Already retired in 897faf5.
- 20 JE reads re-cited and deleted per #093, #094 and #099.
- 21 Citations dropped.
- 22 Every listed clarification.
- 23 Checked: no REQ-SYS-3.1 tests exist on insert-only log records.

**Part B.** Every row the brief lists is done: B.1–B.9 and B.10.

- #051: already covered by the REQ-CF-14.2 same-name route test, so no duplicate was added.
- #136: deleted.
- #113: deleted.
- New files:
  - `Tests.Integrated/InterfaceBridge/PaymentAgreementLinkRoutes.fs` (#008)
  - `Tests.Integrated/InterfaceBridge/CashFlowRoutes.fs` (#009)
  - `Tests.Isolated/Model/CashFlow/TrimmedText.fs` (#135)
- **#114 has landed.** Hobson needs to rewrite NGUI-AQ-1 in resolved-findings.
- **B.10:** 0 `Result.isError` sites; the check flipped to `exit 1`. The #042/#134 constraint tests read the provider-neutral `DbException` (`SqlState` plus the constraint name in its message), so no test references Npgsql (dcb0a63).

**Merge note.** Worker C's 994dd21, typed matches in InstanceDataStates, was skipped. It conflicted with worker A's rewrite of the same REQ-CF-4.9 and 4.10 tests, which already match typed cases and drive is-fulfilled through real Payment adds and deletes, as #022/#046 ask.

**Previously failing tests, now resolved:**

- PaymentDataStates REQ-CF-6.9, 8 tests: retired as withdrawn, rewritten as acceptances.
- LinkageAndMatching, 3 tests: they were test errors under the new REQ-CF-12.4.
  - The already-linked test's second rule now matches only the Debit line.
  - The "one rule constrains line type, another doesn't" test now asserts no link, because the default finds no single line.
- PrePostingReview REQ-RPT-7.3, 2 tests: #124/#314 now expects the priority-10 rule. The stale "empty rule name even when an older run…" test was deleted; the new placeholder 4 supersedes it.

**Tests touched mechanically by the Src work**, by item:

- B21 (A2): AccountCreateActivityBalance.
- A4: FieldMatchEvaluation, RevisedRequirementsClassification, ClassificationRuleCrud, ClassificationRuleRoutes, ClassificationClaimantsAndRuns.
- A6: DerivedStateRules.
- A7: MasterAgreementDataStates, MaintenanceOperations, AgreementUpdate, RevisedRequirementsCashFlow.
- A10: LinkageAndMatching.
- A12: StagingIngestionRules.
- A13: SweepBehaviour, LinkageAndMatching.
- A14: InvoiceMatching.
- B19: ClassificationClaimantsAndRuns, ClassificationRuleCrud, StageEntryClassification, StageEntryUpdate, ClassificationRuleRoutes, Isolated ClassificationRuleComponent.
- B25: TestContext.fs, plus the call sites in 17 files.
- B27: ClassificationRuleCrud.
- B28: InstanceDataStates, JournalEntryVoiding, InvoiceDataStates, InvoiceStateByDirection, StageEntryUpdate, CashFlowMaintenance, DerivedStateRules, PaymentAgreementDataStates, MasterAgreementDataStates, LinkageAndMatching, InvoiceMatching. This removed the two cash-flow REQ-SYS-1.4 filter theories along with the filters they tested.
- B29 (#188), payment tuple: TestDataStage, CashFlowMaintenance, DerivedInvoiceState, DerivedStateRules, InstanceDataStates, InvoiceMatching, JournalEntryVoiding, LinkageAndMatching, PaymentsToPosted, PrePostingReview, ProjectionRules, RevisedRequirementsCashFlow, StageEntryUpdate, StagingIngestionRules, SweepBehaviour.
- C32: classify callers in 8 files.
- C34 #165: renames in about 30 files.
- C34 #169/#198/#170: private wrappers in about 28 files.
- C34 #190: RevisedRequirementsClassification, StageEntryClassification, Isolated Classifier.
- C34 #171: StageEntryUpdate.
- C34 #172: IngestionRoutes.
- C34 #201:
  - The Posted pattern and construction in 12 files.
  - The PaymentsToPosted REQ-CF-10.1 assertions now expect `Posted(je, Some staged)`.
  - The PaymentDataStates REQ-CF-6.4 tests read the pointer back.
- C35:
  - StageEntryIngestion: new `StageTestData.ingestRows` helper, which narrows a file rejection to its first record's error.
  - StageEntryFetching.
  - AgreementUpdate, MasterAgreementDataStates, RevisedRequirementsCashFlow and SweepBehaviour: the period field split.

### 8.5 Decision for Dan

**SweepBehaviour REQ-CF-7.15 "when an Invoice fails to be created after other Instances and Invoices were already created in the run…"** fails. Src is not wrong.

- The test provoked the failure by writing a zero expected amount on a leg.
- Since item 22, reading that leg back rejects the row before the sweep creates anything. The run therefore fails before anything exists, and "none of them remains" would pass vacuously.
- No other honest way was found to make Invoice creation fail part way through a sweep: the state comes from the direction, the legs are consistent by construction, and the database constraints match the domain.

Options:

- (a) waive or retire the "Invoice fails mid-sweep" clause; or
- (b) allow a test-only failure hook in Src.

The test is left failing, not weakened.

### 8.6 Spec and disposition findings

- **REQ-STG-7.5.1:** the brief's declined-list theory included Reviewed, but REQ-STG-7.2 says a Reviewed entry is never flagged. So a paid Reviewed repeat is not "declined because of a Payment". The theory uses Ingested, Classified, NoMatch and Conflict, and asserts that Reviewed is absent. The spec wins.
- **Disposition #070** names `IngestionPaidStageEntryCannotBeExcluded`. The real case is `ClassificationError.ClassificationPaidStageEntryCannotBeExcluded`, and the test matches that.
- **REQ-CF-12.4:** "no kept line … reported with the reason" looks unreachable when only one rule claims, because the kept lines are the matched lines. It is reachable when two claiming rules disagree on line type and the default finds no single line; worker B's test covers that case.
- **REQ-CF-4.9:** is-fulfilled is derived (REQ-CF-9.11). A caller's "set is-fulfilled" is ignored and refused as a composite no-op, so the 4.9 rule holds only through derivation.
- **InstanceOrchestration.constructNewAndPersist** returns a Payment without its posted-to-ledger date, because that date is derived on read. The created value differs from what is read back; what is stored is correct. Worth a look.
- **Footer test:** cites REQ-SYS-3.4 only, because REQ-RPT-3.2 is waived (HTML structure).
- **Leftovers:**
  - isolated AccountComponent.fs still has four `isOk` sites (about lines 41, 54, 72 and 524) that no row covered;
  - the AccountRoutes test "An account id that matches no account converts to AccountIdDoesntMatch…" cites no REQ;
  - the fixture's classification patterns use a real merchant name (DoorDash). It predates this work and is not an institution or person, but it is worth a look in a public repo;
  - worker E replaced real bank names in the isolated JournalEntryExternalReference tests with fictional ones.
- **Model:** Hobson aligns the capability ownership for the classification move (#156/#163), and for ingestion acceptance now living in StageEntryOrchestration. Drift checks structure, not capabilities.
