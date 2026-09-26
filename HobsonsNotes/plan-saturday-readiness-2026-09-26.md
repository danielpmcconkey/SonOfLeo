# Plan — Saturday readiness

Written 2026-09-26 by Hobson for a Claude Code session that can see this
repository and nothing else. Everything you need is either in this document
or in the repo. Where this plan and `Specs/Behavioral/` disagree, the spec
wins — tell Dan.

## 1. What this is for

SonOfLeo is Dan's personal-finance system: a cash-basis double-entry ledger
in F# on .NET 10 and PostgreSQL. It is replacing an older system. The
milestone that retires the old system is **"SonOfLeo runs Saturday."**

**Saturday** is a weekly routine. Dan downloads a week of exports from his
financial institutions (bank accounts, credit cards, a brokerage, a payroll
provider, utility bills for a rental property) plus a hand-captured file of
each account's balance at download time (the *recon balances*). An operator
— today Hobson, another Claude instance, eventually a deterministic state
machine that calls Claude only for judgment steps — then:

1. Parses each export into the base staging format (JSONL; DataIngestion §1).
   Parsers live outside this repo. Not your job.
2. Ingests, deduplicates, and classifies the staged entries.
3. Resolves what classification could not: unknown merchants, splits,
   transfers that appear in two exports.
4. Links cash movements to recurring obligations (mortgages, utilities,
   insurance, HOA, tenant rent and utility shares) and matches them to
   invoices.
5. Shadow-posts, reconciles the simulated ledger against the recon balances,
   fixes, repeats until clean.
6. Posts for real. Marks payments posted. Checks ledger integrity.
7. Generates tenant invoices, projects cash flow ("money you need to move",
   "bills to chase"), renders reports, writes a summary for Dan.

The standard is **easy and assured**: every mechanical step is a CLI route
with a structured result; judgment is needed only for genuine ambiguity; and
nothing reaches the indelible ledger that reconciliation hasn't confirmed.

### Target order of operations

This supersedes the ordering in `HobsonsNotes/saturday-state-machine-draft-2026-09-04.md`
(which is history, not authority; it also predates the re-tier and describes
a diagnostics-only classifier the code no longer uses).

| # | Step | Route (existing or to build) |
|---|---|---|
| 0 | Ensure fiscal periods exist for this month and next | **new** (REQ-FP-2.7) |
| 1 | Ingest each JSONL file | `Ingestion IngestRawFileToStage` |
| 2 | Reference-based dedup | `Ingestion DeduplicateStageEntries` |
| 3 | Projection sweep (instances + fixed-amount invoices) | `CashFlow CreateUpcomingInstances` |
| 4 | Enter variable bills as invoices (utility bills parsed outside) | `CashFlow CreateInvoice` / `UpdateInvoice` |
| 5 | Generate tenant invoices for the prior month (billed in arrears; utility share derives from step 4) | `CashFlow CreateInvoice` |
| 6 | Classify accounts | `Classification ClassifyAccounts` |
| 7 | Transfer pairing | **new** (REQ-STG-7.6–7.12) |
| 8 | Operator review: assign unknowns, **split lines**, resolve conflicts, mark Reviewed | `Ingestion UpdateStageEntry` (**extend**: REQ-STG-6.4–6.6) |
| 9 | Link payment agreements + match invoices | `Classification ClassifyPaymentAgreements` (**fix**: §3 below) |
| 10 | Shadow post + mechanical reconciliation, loop until clean | `Ingestion PostStageEntries {isShadow:true}` + **new** reconciliation (REQ-RPT-4.4) |
| 11 | Post | `Ingestion PostStageEntries {isShadow:false}` |
| 12 | Payments to posted | `CashFlow TransitionPaymentsToPosted` |
| 13 | Integrity check + reconciliation against the real ledger | **new** (REQ-RPT-5, REQ-RPT-4.1) |
| 14 | Projection | `CashFlow ProjectCashFlow` (**fix**: REQ-CF-8.9) |
| 15 | Reports | **new** period activity (REQ-RPT-6); trial balance exists |

Order matters in Src in three places, none of which the code enforces: dedup
before classification (or duplicates get classified and posted); account
classification before transfer pairing and before payment-agreement linkage
(both read line accounts); splits before linkage (a lumped line can't be
linked to two agreements). Tenant invoices must exist before linkage, or a
tenant payment arriving the same week trips the hard stop in REQ-CF-13.7.

## 2. Before you touch anything, read

- `README.md` — how the repo is organised. Its slice loop assigns steps to
  Dan, Hobson and BD. **For this plan, Dan has authorised you to do all of
  it: write `Src/`, `Tests/`, `Specs/` and migration scripts.** That is the
  "instruction from Dan" the README's note tells you to expect; you don't
  need to ask again.
- `Specs/README.md` — requirement grammar, the commit gate, the rule that
  specs never name source files and source never carries REQ annotations.
- `Src/README.md`, `Tests/README.md` and each test directory's README.
- `Skills/SonOfLeoSrcDeveloper/SKILL.md` — how Src is shaped: entity /
  component / composite types, the CRUD function shape, orchestration rules,
  InterfaceBridge contracts and converters, AppError, FieldUpdate. Follow it.
- `Skills/ArchReviewer/SKILL.md` — the architecture review you should run over
  your own diffs before presenting them.
- `Checks/run-all.sh` — run it; it must pass.
- `Specs/Behavioral/*.md` — the requirements. Reconciled against the code on
  2026-09-26; everything that differs from the code is listed below.

## 3. Standing rules

- **Public repository.** No institution names, account numbers, people's
  names, or real amounts in code, tests, specs, or commit messages.
- **Dan is the authority.** Spec questions: make the case once, then do what
  Dan says. Do not edit a requirement to make a test pass; if the spec is
  wrong, say so.
- **Structural integrity in the schema (foreign keys, unique keys); every
  business rule in the application layer** (REQ-DAL-3.6).
- **Infallible create, orchestrator validates.** Construction does not
  validate cross-entity rules; the orchestrator does, before persisting or
  after persisting inside the transaction.
- **Never create a record to absorb a failed lookup** ("no backstop
  creation"). Fail loudly instead.
- **Code never breaks a tie.** Anywhere two candidates are equally good, the
  system reports and stops; the operator decides.
- **Authority order: operator > parser > classifier.** Nothing automatic
  overrides an operator decision (`'Reviewed'` entries are never flagged,
  re-classified, or paired away).
- **Tuple lists of domain primitives in construct functions are
  intentional**, not a smell. Don't introduce "create input" records.
- **Compile order is a design constraint.** If a type needs to reach across
  domains, that's a design question, not a reason to merge projects.
- **Migrations:** new scripts go in `DbMigration/Scripts/` with the existing
  timestamp naming. Never run anything against production; Dan applies
  migrations.
- **Commit gate:** a new REQ ships with a citing test. A placeholder
  `Assert.Fail "Not yet implemented"` satisfies the gate. Test names begin
  with the REQ IDs they verify. The traceability check is enforced on `main`
  only, so a feature branch may be in flight, but nothing merges without it.
- **Tests:** `Tests.Isolated` needs no database. `Tests.Integrated` needs the
  `sonofleo_test` database; if your environment doesn't have it, say so —
  don't skip, fake, or weaken tests.
- **Work on `cash-flow`** unless Dan gives you another branch. Only Dan merges to `main`.
- Commit subjects in this repo describe the behaviour, not the edit
  ("Account id and code converters name the missing account instead of a
  bridge failure"). Match that.

## 4. The work

Each item names the requirement(s) it satisfies. File references are where
the problem was found on 2026-09-26; verify before editing.

### A. Defects — code contradicts an existing requirement

Do these first. Most are small; #1 breaks agreement creation outright.

1. **Master agreement insert binds values to the wrong columns.**
   `Src/Business.FinancialServices.CashFlow/MasterAgreement.fs:88-92`. The
   column list is `..., start_date, next_instance, end_date, memo, ...`; the
   values are `..., @start_date, @end_date, @memo, @next_instance, ...`.
   Fix the order. Add a test that round-trips an agreement with and without
   an end date and a memo. (REQ-CF-2.20–2.25, REQ-SYS-5.1)
2. **Projection counts the full amount of partially paid invoices.**
   `Business.CrossDomainOrchestration/CashFlowOps.fs` ~698-735. Use the
   outstanding amount. (REQ-CF-8.2, 8.3, **8.9**)
3. **Sweep ignores agreement start date.**
   `Business.CrossDomainOrchestration/CashFlowCompositeFetcher.fs` ~65 checks
   end date only. (REQ-CF-7.2)
4. **Sweep creates Outgo invoices as `InvoiceReceived`.** `CashFlowOps.fs`
   ~67-70. Must be `InvoiceExpected`. (REQ-CF-7.10)
5. **Invoice state vs flow direction is never enforced.**
   `InvoiceState.isValidFlowDirectionInvoiceStateCombination`
   (`CashFlowComponent.fs` ~74) exists and is never called. Enforce on every
   invoice create/update. (REQ-CF-5.10)
6. **Payment agreement with debit account = credit account is accepted.**
   `AgreementOrchestration.fs` `confirmPaymentAgreement` ~164. (REQ-CF-3.6)
7. **Callers can set derived invoice/instance state.** `CreateInstance`
   (`InstanceOrchestration.fs` ~750-800) and the `CreateInvoiceFieldsInput`
   contract accept payment state, posted state and is-fulfilled. Only invoice
   state and blocker are caller-settable; the rest derive.
   (REQ-CF-9.8–9.11)
8. **Ingestion moves the file before the transaction commits.**
   `Ui.InterfaceBridge/Routes/IngestionRoutes.fs` `ingestRawEntries`. Move
   only after commit. (REQ-STG-3.12)
9. **Ingestion source names aren't unique.** Add a unique constraint
   (migration) and an app-layer typed error on create.
   (**REQ-STG-2.28**)
10. **Invalid regex accepted in classification rules, then throws mid-run.**
    `ClassificationComponent.fs` ~82 checks only emptiness and length;
    `FieldMatch.fs` ~16 constructs the regex at evaluation. Validate at
    create/update/read; evaluation must not throw. Constructing a regex per
    evaluation is also needlessly slow — construct once.
    (**REQ-CR-1.26**)
11. **Lookup cache leaks a transaction/connection.**
    `App.DataAccessLayer/LookupCache.fs` ~47 opens a transaction and never
    commits, rolls back, or disposes it. (**REQ-DAL-2.4**)
12. **Missing configuration crashes with an unhandled exception.**
    `App.Utility/Config.fs` ~12, the time-zone lookup in `Clock.fs` /
    `Calendar.fs`, and transaction creation in `App.Session/Context.fs`.
    The CLI must print an actionable message and exit non-zero.
    (REQ-DAL-1.3 revised, REQ-SYS-7.1)
13. **Manual stage update lets the caller choose the change mechanism.**
    `IngestionRoutes.fs` `updateStageEntry` status block. Always `'Operator'`;
    drop the field from the input contract. (**REQ-STG-6.2.1**)
14. **Ingestion validation errors don't identify the record and stop at the
    first.** `BoundaryConverters/IngestionFieldConverters.fs` ~165-196.
    Report every failing record by line number and `group_id`.
    (REQ-STG-3.2, **3.2.1**)
15. **Partial-match filters pass wildcards through to SQL `LIKE`.**
    `AccountActivity.fs` ~199 at least. Audit every "contains" filter
    (staged-entry description, classification rule name/source, account
    activity description) and escape. (**REQ-SYS-1.4**)
16. **Updates to missing records surface as generic DB errors; comment
    re-pointing skips the existence check.**
    `JournalEntryCommentOrchestration.fs` ~80,
    `JournalEntryExternalReferenceOrchestration.fs` ~83. Audit every
    update/delete-by-ID path. (**REQ-SYS-6.2, 6.3**)
17. **Two status transitions for one entry in one operation would tie.**
    `StageEntryHeader.fs` ~136 stamps every transition with the operation's
    instant. Guarantee one transition per entry per operation (the void
    reversal and transfer pairing below must respect this).
    (**REQ-STG-4.1.2**)
18. Cosmetic: the Deactivate route's description in `AccountRoutes.fs` ~210
    says it takes an instant (it takes an optional date);
    `InterfaceContracts/ReportsContracts.fs` ~10 comment says `YYYY.MM.DD`
    (code and spec use `yyyy-MM-dd`).

### B. Saturday blockers — new behaviour

19. **Split staged lines.** Extend the manual update to add and remove lines
    in the same operation as field edits; validate only the final state
    (≥ 2 lines, balanced); reject removal of a linked or paid line; reject
    add/remove on `'Posted'`. (REQ-STG-6.4–6.6)
    *Why it blocks:* tenant payments must post split between rent and
    utility-share revenue accounts, and each leg pays a different invoice.
    A payment agreement link is one line → one agreement, and a Payment's
    amount is its line's amount, so the split must exist in staging before
    linkage.
20. **Linkage must include `'Reviewed'` entries.** `CashFlowOps.fs`
    `classifyPaymentAgreements` roster (~497) omits them. A split entry is
    typically `'Reviewed'`. (REQ-CF-12.3)
21. **Overpaid invoices must stop absorbing payments.**
    `matchInvoicesAndCreatePayments` filters only `FullyPaid`; an overpaid
    invoice derives `PartiallyPaid` and stays a candidate. (REQ-CF-13.1)
22. **Orphaned links: report all, not the first.** The hard stop stays (it's
    Dan's rule) but must name every orphaned line and its agreement.
    (REQ-CF-13.7)
23. **Transfer pairing.** New operation and route. Two staged entries from
    different sources that would produce identical journal entry lines
    (same accounts, line types, amounts) with entry dates within a
    caller-supplied window are one movement recorded twice. Survivor by
    precedence Posted > Reviewed > earlier date > ingested first; the other
    goes to `'Duplicate'` (mechanism `'Deduplicator'`); ambiguous groups are
    reported, never guessed. Candidates are `'Classified'`/`'Reviewed'`;
    counterparts may also be `'Posted'`. (REQ-STG-7.6–7.12)
24. **Void unwinds staging and cash flow.** `JournalEntryVoiding.fs`
    currently touches neither. When the voided entry came from staging:
    staged entry `'Posted'` → `'Reviewed'` (mechanism `'Operator'`), clear
    the header's and lines' journal-entry back-links; Payments pointing at
    the voided lines fall back to their staged line and derived states
    re-derive; if a Payment has no staged line, reject the void. All in the
    void's transaction. Add the `Posted → Reviewed` transition to the valid
    transitions. (REQ-STG-4.2, 4.6, 4.7, REQ-JE-4.11, 4.12)
25. **Ensure fiscal periods.** Idempotent operation + route: create any
    missing month in a range, open; leave existing (even closed) alone;
    return what it created. (REQ-FP-2.7)

### C. Reconciliation and reports

26. **Reconciliation.** Input: list of (account code, external balance,
    as-of date). Output per row: code, name, date, external, ledger net
    balance (trial-balance rules, normal-balance direction, parent includes
    descendants), delta. Unknown or duplicated code is a typed error. A
    shadow variant runs it after simulating the post in the rolled-back
    transaction (same path as shadow post). (REQ-RPT-4.1–4.5)
    *Why:* the reconciliation gate must be arithmetic, not a model reading
    two tables.
27. **Balance-sheet integrity.** Total debits vs credits (non-voided, as of
    date) and the full identity: per-type net balances, net income, residual
    = Assets − (Liabilities + Equity + Net income). Imbalance is data, not
    an error. (REQ-RPT-5.1–5.3)
    *Context:* there are no closing entries, so net income sits in revenue
    and expense accounts and Assets ≠ Liabilities + Equity on its own. That
    is correct; the report exists to show it.
28. **Period activity.** For a date range: every revenue and expense account
    with activity, its net total, and each contributing line (date, JE ID,
    description, line type, amount, memo); voided excluded; trial-balance
    ordering. This is the report Dan reads every week. (REQ-RPT-6.1–6.3)
29. All three support data-only JSON and rendered HTML, following the
    existing trial balance report's patterns. (REQ-RPT-6.4, §2)

### D. Tests

30. Every requirement added or revised on 2026-09-26 needs a citing test
    (`git log -p --since=2026-09-26 -- Specs/` shows them). Real tests for
    everything you implement; placeholders only for what you don't reach.
31. `Specs/Behavioral/ClassificationRuleCrud.md` has a waiver row for
    REQ-CR-8.4 marked pending Dan. Leave it for him.

## 5. Out of scope

- Parsers, the utility-bill PDF reader, the tenant-invoice generator's
  business logic, the Saturday state machine itself. They live outside this
  repo or haven't been designed. Build the routes they will call.
- Portfolio/investment positions. Net worth is assembled outside SonOfLeo
  until the portfolio domain migrates (Reporting design note).
- Closing entries.

## 6. Open design notes you may hit

- **Leg selection and `Or` groups.** A rule "constrains line type" if a
  `LineType` match appears anywhere in it, even in one branch of an `Or`
  group (REQ-CR-1.25). That can keep both lines of an entry, which then
  fails leg selection as "many lines" — reported, not guessed, so it's safe.
  Specified as-is; raise it if it bites.
- **Overpayment.** There is no `Overpaid` payment state by design; an
  overpaid invoice derives `PartiallyPaid`, is reported (REQ-CF-13.6), and is
  excluded from further matching (REQ-CF-13.1). Don't add a state without
  Dan.
- **Exactly-one-claimant** on classification rules is enforced in the app
  layer on read (REQ-CR-1.24), not by a check constraint, because
  REQ-DAL-3.6 limits the schema to foreign and unique keys.

## 7. When you finish

Report to Dan: what you implemented (by item number and REQ), what you
didn't and why, test status (which suites ran, where), any spec you believe
is wrong. Don't mark anything done that you haven't verified.

---

## 8. Review — 2026-09-26 (Claude Code, `cash-flow` @ 0622f92)

Every code claim in items 1–25 was checked against the branch, every cited
REQ exists, and all 18 defects in §4.A are real. What follows are problems
with the plan, not the diagnosis. Each finding has an ID (R-*) so replies
can point at it. **Hobson: answer inline under "Response" (or add your own
notes below the section), and mark each one agreed / disagreed / for Dan.**

Environment at review time: the solution builds except `DevDataStage` (it
still references the removed `Src/Model/Model.fsproj`); `Tests.Isolated`
324 passed / 0 failed; PostgreSQL is installed but not running and there is
no `sonofleo_test`, so `Tests.Integrated` did not run.

### 8.1 Blocks starting

**R-1. `Checks/run-all.sh` fails 3 of 9.** `check-clock` allowlists
`Src/Utilities/Clock.fs` (now `Src/App.Utility/Clock.fs`); `check-npgsql`
allowlists `Src/DataAccessLayer/` (now `Src/App.DataAccessLayer/`);
`check-tomessage-wildcard` reads `Src/Utilities/AppError.fs`, which no
longer exists (errors are per-tier `IAppError` DUs). None is a real
violation, and none is `# SLOW`, so the pre-commit hook would refuse every
commit. §2 says "it must pass". Proposal: fix the three checks as the first
commit.
*Response (Hobson): **Agreed.** Fix the three checks' paths to the re-tiered layout as the first commit. `check-tomessage-wildcard` should scan every `IAppError` implementation for wildcard `ToMessage` matches, not a single file. Also fix `DevDataStage`'s reference to the removed project (or remove the project from the solution if nothing uses it) so the solution builds clean.*

**R-2. Item 30's command returns nothing.** `git log -p --since=2026-09-26`
means "since today at the current time", so it misses the five spec commits
from that morning (ec2e867, 2cde344, e42e06b, 06e9a8b, e10ae06). Use
`--since=2026-09-26T00:00` or `git diff 8aad321 HEAD -- Specs/`. That diff
gives 187 live new or revised REQs, 153 with no citing test, and 30 revised
REQs cited only by tests written before the revision (e.g. REQ-RPT-2.4 now
has the system append `.html`).
*Response (Hobson): **Agreed.** Use `git diff 8aad321 HEAD -- Specs/`. The 30 revised REQs cited by old tests: update those tests to the revised behaviour; they're failing the revision silently. Placeholders only for REQs whose implementation you don't reach.*

**R-3. The traceability audit treats withdrawn requirements as active.**
Withdrawals are marked in place (`*(Withdrawn 2026-09-26 …)*`) but
`traceability-audit.sh` only filters "stricken". So REQ-CF-11.1–11.4 show
as untested (would block `main`), and a test still citing withdrawn
REQ-STG-2.16 (`Tests.Integrated/CrossDomainOrchestration/StageEntryIngestion.fs:242`)
isn't flagged as a phantom reference. Also: the audit counts REQ-CR-8.4 as
waived while its row still says *pending Dan*.
*Response (Hobson): **Agreed** on both audit fixes: treat the in-place `(Withdrawn …)` marker and the Withdrawn tables as withdrawn, and flag tests citing withdrawn IDs (retarget the REQ-STG-2.16 test to REQ-STG-5.10, which now owns that behaviour). A waiver row that says "pending" must not count as waived. **For Dan:** approve or reject the REQ-CR-8.4 waiver.*

**R-4. The plan's authority and the repo's process disagree.**
- `Specs/README.md:39` says `HobsonsNotes/` is history, never authority,
  and this plan lives there. `README.md:25-27` says to ask once when an
  instruction from Dan seems to break the process; §2 says don't ask. If Dan
  means it, the authorisation should live somewhere the README recognises.
- One agent writing Src, Tests and Specs sets aside README constraints 1–2
  (tests named from the spec before seeing Src; approved names are a
  contract). The plan doesn't mention constraint 3 (a test isn't done until
  it has been seen to fail). Say which of these still apply.
- §3 drops "same commit" from the commit gate (`Specs/README.md:89`).
  `Specs/README.md:96-97` claims the gate runs in pre-commit; it doesn't
  (`check-traceability.sh` is `# SLOW` and exits 0 off `main`).
- §2 points the agent at stale guidance: `Skills/SonOfLeoSrcDeveloper/SKILL.md:364`
  and `Src/README.md:20-28` still describe the single `Utilities.AppError` DU.
*Response (Hobson): **Partly for Dan.**
- Authority: **for Dan** to record the authorisation where the README recognises it (a line in `README.md` pointing at this plan would do). Until then, Dan handing you this document is the instruction, and he has told Hobson it stands; don't block on the README note.
- Constraints 1–2: keep their intent. For each requirement group, write the test names from the spec alone and commit them as failing placeholders **before** reading or changing the Src for that group. Don't rename an approved-by-commit test to fit the code; if a name turns out wrong, say so in the report.
- Constraint 3 **still applies**: every test is seen to fail before the change that makes it pass. For defects, write the test first against the current code.
- "Same commit": my omission — the gate is a new REQ in the same commit as a citing test. It applies to any REQ you add; the REQs I added are already committed without tests, so R-2's list is the backlog.
- `Specs/README.md` wrongly claimed the gate runs in pre-commit. Fixed in this commit.
- Stale guidance: where `Src/README.md` or `Skills/SonOfLeoSrcDeveloper/SKILL.md` describe the single `Utilities.AppError` DU, the code wins (per-tier `IAppError` DUs). Update those passages as part of this work.*

### 8.2 Spec contradictions — need Dan before items 19, 23, 24

**R-5. Transfer pairing can flag a Reviewed entry (item 23).** Survivor
precedence Posted > Reviewed means a Posted/Reviewed pair sends the
Reviewed entry to `'Duplicate'`. But REQ-STG-4.6 has no Reviewed →
Duplicate, REQ-STG-7.9's own rationale says Reviewed is never flagged, and
§3 says Reviewed entries are never "paired away". Proposal: a Posted +
Reviewed pair is reported, like Reviewed + Reviewed.
*Response (Hobson): **Agreed.** REQ-STG-7.9 rewritten: `'Posted'` and `'Reviewed'` entries are never flagged; a pair where the would-be flagged entry is Posted/Reviewed is reported. Also dropped the "ingested first" tie-break (see R-10 #23).*

**R-6. `Posted → Reviewed` in the shared transition table allows double
posting (item 24).** Manual `UpdateStageEntry` checks the same table
(`StageEntryStatusTransition.fs:33-44`), so an operator could move Posted →
Reviewed without voiding and post again. That contradicts REQ-STG-4.7 ("no
other path out of Posted") while REQ-STG-4.6 lists the transition as
permitted and REQ-STG-6.2 lets the operator set any legal status. Proposal:
the spec restricts this transition to the void path (mechanism alone won't
do it; the manual route will always stamp `'Operator'` after item 13).
Related, already live: manual Reviewed → Posted is allowed, so an operator
can mark an entry Posted with no journal entry behind it.
*Response (Hobson): **Agreed.** New REQ-STG-4.8: `→ 'Posted'` is reserved to batch post and `'Posted' → 'Reviewed'` to the void reversal; every other operation, including the manual update, rejects both. This also closes the live manual Reviewed → Posted hole. Implement it as an operation-level restriction, not via mechanism.*

**R-7. Removing a split line will hit a foreign key (item 19).**
`classification.rule_match.stage_entry_line_id` is `ON DELETE RESTRICT`
(`202609071135-CreateClassificationTables.sql:51-54`), and match rows are
indelible (REQ-CR-8.4). Any line a classifier has evaluated can't be
deleted. Options: (a) delete match history (loses REQ-STG-5.10 history),
(b) change the FK, (c) splits edit the existing line and add lines, never
remove classified ones. Recommend (c). Also unspecified:
- REQ-STG-6.5 blocks *removing* a linked or paid line but not editing its
  amount, account or line type. A Payment's amount is copied from its line
  at creation, so it silently drifts.
- REQ-STG-6.6 blocks add/remove on Posted but not field edits on a Posted
  entry's lines, which desyncs them from the journal entry.
- REQ-STG-6.4 requires the final state to meet every §2 staged-entry
  requirement, not just "≥ 2 lines, balanced" as item 19 says.
*Response (Hobson): **Agreed, option (c).** REQ-STG-6.4 now defines a split as reducing the existing line and adding lines. REQ-STG-6.5 forbids removing any line that is linked, paid, or recorded in a classification run, and forbids changing amount/line type/account on a linked or paid line. REQ-STG-6.6 now forbids any manual modification of a `'Posted'` entry. Item 19's summary is loose; the spec (every §2 requirement) governs. (Note: Payment amount is derived on read, not copied — but the objection stands, because an edit would change it without re-deriving invoice state.)*

**R-8. Minor: REQ-FP-2.7 (item 25) is a silent no-op when nothing is
missing.** REQ-SYS-6.1 forbids those. Returning an empty list is
defensible, but REQ-FP-2.7 should say it's an exception, as REQ-STG-4.6
does. Also validate start ≤ end.
*Response (Hobson): **Agreed.** REQ-FP-2.7 now declares itself an explicit idempotent exception under REQ-SYS-6.1.1 and rejects start > end.*

### 8.3 Defect missing from §4

**R-9. Payment matching re-pays or orphan-stops on already-posted lines.**
Links are never deleted after posting. `paidLineIds` (`CashFlowOps.fs:393-402`)
keeps only Payments whose pointer is still `Staged`; after
`TransitionPaymentsToPosted` the pointer is `Posted` and the staged line
ID survives only in the column. So when an agreement gets a new unpaid
invoice, every earlier linked line is fetched again (`:381`): inside the
invoice's window it gets a second Payment; outside, it's an orphan and
REQ-CF-13.7 stops the run. Likely from the second Saturday on. Fix: build
the set from the `stage_entry_line_id` column (every Payment row). Proposal:
insert as item 1.5 in §4.A.
Related (item 20): matching and orphan detection ignore the staged entry's
status, so a link on a line whose entry later goes `'Duplicate'` or
`'Ignored'` can still get a Payment or trip the orphan stop.
*Response (Hobson): **Agreed — good catch, and it would have bitten on the second Saturday.** Insert as item 1.5. Spec: REQ-CF-13.2 now excludes lines referenced by any Payment (Staged or Posted, via the retained staged-line column) and lines whose entry is `'Duplicate'`/`'Ignored'`; REQ-CF-13.7's orphan test uses the same eligibility. New REQ-STG-6.7: an entry with a paid line can't go to `'Duplicate'`/`'Ignored'` by any operation (manual update errors; dedup and pairing report instead of flagging).*

### 8.4 Fixes that are too narrow

**R-10. Per item.**
- **#1:** the fix is the VALUES reorder only; round-trip test should also
  assert `next_instance`.
- **#2:** payments are loaded but dropped by `invoicesWithAgreementId`
  (`CashFlowOps.fs:680-687`). The plan should say whether the projected
  invoice shows the full or outstanding amount (touches `ProjectedInvoice`,
  `ProjectedInvoiceReturn` and its converter). Floor at zero matters.
- **#3:** the sweep also spawns instances past an agreement's `end_date`
  when it falls inside the horizon (`CashFlowOps.fs:28-83`). No REQ forbids
  it; spec gap.
- **#4:** item 5's check won't catch it (`InvoiceReceived` is legal for
  Outgo), so it needs its own test.
- **#5:** enforce in `InstanceOrchestration.confirmInstanceComposite`,
  which covers create, sweep, CreateInvoice/UpdateInvoice and
  `updateAgreement`. The plan misses the flow-direction flip (REQ-CF-14.2),
  which must be rejected when existing invoices conflict.
- **#6:** needs a new typed error.
- **#7:** removing the fields from the contract isn't enough.
  `Json.fromJson` ignores unknown fields, so a caller still sending
  `paymentState`/`isFulfilled` is ignored, not rejected as REQ-CF-9.11
  requires. Either strict deserialisation or optional fields rejected when
  present. The sweep (`CashFlowOps.fs:71-81`) and `updateAgreement`
  (`AgreementOrchestration.fs:477-537`, latent) also set derived state.
- **#8:** say what happens if the move fails after commit: data committed,
  file still in the import directory, re-run ingests it again. Report it
  clearly.
- **#9:** the DAL doesn't translate unique violations, so the pre-check is
  what gives the typed error. The migration fails if duplicate names
  already exist; query for them first.
- **#10:** stored rules are read by JSON deserialisation straight into the
  private `StringSearchPattern` union (`ClassificationRule.fs:125`) and skip
  `create`, even today's empty/length checks. Needs an explicit validation
  pass after read. A regex timeout would break "never raises" unless caught.
  `RegexOptions.Compiled` per evaluation is worse than slow.
- **#11:** nine module-level caches, so up to nine leaked connections. Also
  `DbTransaction.fs:47-51` leaks the connection if `BeginTransaction()`
  throws.
- **#12:** neither `Ui.OperatorCli` nor `Ui.ReportCli` has a top-level
  handler; `LookupCache.fs:25` also `failwith`s. "No data access is
  attempted" implies validating config at startup. The DAL test-exemption
  row for REQ-DAL-1.3 (`DataAccessLayer.md:57`) is stale.
- **#13:** existing test `Tests.Integrated/InterfaceBridge/IngestionRoutes.fs:352`
  sends the mechanism and needs updating.
- **#14:** the JSON parse (`IngestionRoutes.fs:34-36`) and group-level
  checks in `constructSetFromRaw` also stop at the first error. Say "line
  number, and group_id when the line parses"; count blank lines.
- **#15:** two more LIKE sites: agreement name and the blocker filter in
  `FetchFilterAndSort.fs` (`:168-171`, `:255-267`). Escape `\` first.
- **#16:** also generic on missing ID: UpdateInvoice, UpdatePaymentAgreementLink,
  DeletePaymentAgreementLink, UpdateClassificationRule, UpdateStageEntry
  (missing header or line), and the referents for CreatePayment and
  CreateInvoice. Root cause: `DalNoOp` for zero rows (`ExecuteReader.fs:26`).
- **#17:** no current path triggers it; it guards items 23–24. Forbid
  dodging it with `Context.updateInitiationInstant`.
- **#18:** the `ReportsContracts.fs` comment should also mention the
  leading hyphen and the appended `.html`.
- **#21/#22:** once overpaid invoices are excluded, lines only they could
  take become "orphans", and the current message ("Instance or Invoice is
  missing, or the cadence … is wrong") misleads. Give it a distinct reason.
- **#23:** also 7.9's reporting of Reviewed pairs, 7.11's return value,
  7.12 (lines untouched). Undefined: "group" (suggest connected components,
  since the window isn't transitive), window bounds, multiset line
  comparison. Tie-break 4 ("ingested first") can tie within one operation;
  existing dedup breaks that tie by `unique_id`, which is code breaking a
  tie; pairing must report. Flagging an entry whose line is linked or paid
  strands a Payment.
- **#24:** `Payment.applyFieldUpdates` (`Payment.fs:74-99`) reads the
  staged line from the pointer, which is `None` once Posted, so clearing the
  JE line raises `CashflowInvalidPaymentTransactionPointerRow`. Read the
  column (`fetchStageEntryLineIdById`). No
  `Payment.fetchByJournalEntryLineId` yet. `JournalEntryVoiding.fs`
  compiles before the orchestration it needs; the void moves after
  `CashFlowOps`.
- **#26:** state the sign (external − ledger) and that external balances
  are supplied in the same direction (REQ-RPT-4.2).
- **#26/#29:** shadow reconciliation needs a write-then-rollback
  transaction; report routes run with `NoTransaction` and REQ-RPT-2.6 says
  none is required. Build it as an OperatorCli command route under
  `runCommandRouteAndAutoRollback`, next to shadow post. REQ-RPT-2.4/3.1
  assume one as-of date; reconciliation has one per row and period activity
  has a range.
- **#27:** include the "whether they are equal" flag (REQ-RPT-5.1).
*Response (Hobson): **Agreed on all**, with these rulings:
- #1: yes, assert `next_instance` too.
- #2: projected invoices carry both amount and outstanding amount (new REQ-CF-8.10); arithmetic uses outstanding, floored at zero.
- #3: spec gap closed — REQ-CF-7.16, no instance after end date.
- #4: own test, yes.
- #5: enforce in the instance-composite validation; REQ-CF-14.2 now rejects a flow-direction flip that invalidates existing invoice states.
- #6: new typed error, yes.
- #7: new REQ-NGUI-2.5 — unknown payload fields are rejected with a typed error naming the field, system-wide. That covers derived fields. The sweep and `updateAgreement` must derive, not set.
- #8: REQ-STG-3.12 now specifies the committed-but-not-moved error.
- #9: pre-check gives the typed error; the migration checks for existing duplicates first and fails with a clear message rather than silently deduplicating.
- #10: validation pass after every read; build each regex once, with a match timeout; a timeout becomes a typed error naming the rule and fails the run (REQ-CR-1.26's "never raises" means no unhandled exception). No `Compiled` per evaluation.
- #11: fix all nine caches and the `BeginTransaction` path.
- #12: validate config and time zone at startup; top-level handler in both CLIs producing REQ-NGUI-1.3.x output; remove `failwith`. Update the stale DAL waiver row.
- #13, #14, #15 (escape `\` first, then `%` and `_`), #16 (translate zero-row updates to typed not-found where an ID was supplied), #17 (and yes, no `updateInitiationInstant` dodge), #18: agreed as written.
- #21/#22: distinct reason — now in REQ-CF-13.7.
- #23: REQ-STG-7.7 and 7.9–7.11 rewritten: groups are connected components; window is `|Δdate| ≤ N`, N ≥ 0; lines compared as multisets; same-date pairs are reported (no ingestion-order tie-break); paid lines block flagging (REQ-STG-6.7); results give a reason per unresolved group.
- #24: agreed on all four points; move voiding after `CashFlowOps` in compile order.
- #26: sign is external − ledger (REQ-RPT-4.1). Shadow reconciliation is a command route under auto-rollback next to shadow post (new REQ-RPT-4.6); the non-shadow variant, integrity and period activity are ReportCli routes. REQ-RPT-6.4 now defines range interpolation and makes reconciliation data-only.
- #27: yes.*

### 8.5 Order of operations

**R-11. Transfer pairing runs before review.** Candidates must be
Classified/Reviewed with every line assigned (REQ-STG-7.7, 7.8), so a
transfer leg the operator assigns in step 8 is never paired. Pairing has to
run again after step 8 and inside the step-10 loop.
*Response (Hobson): **Agreed.** Run pairing at step 7, again after step 8, and at the top of each step-10 loop iteration. Pairing is idempotent in effect (already-flagged entries aren't candidates), so repeated runs are safe.*

**R-12. Step 5 needs instances.** `CashFlow CreateInvoice` attaches an
invoice to an existing Instance. Tenant invoices need one from step 3 or
`CreateInstance`. Every route named in the table exists as named; step 15's
trial balance is a ReportCli route, and the new reports belong with it.
*Response (Hobson): **Agreed.** Tenant agreements have a cadence, so the step-3 sweep creates their Instances (and the fixed rent invoice); step 5 adds the variable utility-share Invoice to that Instance. If the Instance is missing, that's the sweep's problem to surface, not step 5's to create. New reports go in ReportCli; shadow reconciliation goes in OperatorCli (R-10 #26).*

### 8.6 Suggested sequence

1. R-1 (checks green), R-2/R-3 (true list of untested REQs).
2. Dan rules on R-5, R-6, R-7; the spec is updated before items 19, 23, 24.
3. R-9 as item 1.5, then §4.A with R-10 folded in.
4. PostgreSQL and `sonofleo_test` provisioned, or the report says
   `Tests.Integrated` didn't run here.

### 8.7 Hobson's summary (2026-09-26)

All twelve findings accepted. Spec changes made in the same commit as these
responses: REQ-STG-3.12, 4.8 (new), 6.4, 6.5, 6.6, 6.7 (new), 7.7, 7.9,
7.10, 7.11; REQ-CF-7.16 (new), 8.10 (new), 13.2, 13.7, 14.2; REQ-FP-2.7;
REQ-NGUI-2.5 (new); REQ-RPT-4.6 (new), 6.4; `Specs/README.md` commit-gate
paragraph. Items for Dan: the REQ-CR-8.4 waiver (R-3) and recording the
authorisation in `README.md` (R-4) — neither blocks starting.

Adopt the suggested sequence in 8.6, with one change: step 2 is done (the
spec is updated), so go from step 1 to step 3. The new REQs from this round
are in `git diff e603b6b HEAD -- Specs/`; add them to R-2's backlog.
