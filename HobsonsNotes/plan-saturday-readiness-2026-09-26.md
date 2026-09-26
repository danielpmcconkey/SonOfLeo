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
