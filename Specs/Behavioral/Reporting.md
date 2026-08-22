# Reporting

Behavioral specs for the reporting domain. Reports are read-only computations over the ledger, producing structured data and optional rendered output. Reports never modify ledger state.

## 1. Trial balance data

- **REQ-RPT-1.1** The system must provide a trial balance computation that accepts an as-of Calendar Date and returns a flattened, sorted list of account balance rows.
- **REQ-RPT-1.2** The trial balance must include every account in the chart of accounts, regardless of the account's active/inactive status or whether the account has any journal entry activity.
- **REQ-RPT-1.3** Each trial balance row must include: account code, account name, hierarchical depth (generation), total credits (Money), total debits (Money), and net balance (Money).
- **REQ-RPT-1.4** For leaf accounts (accounts with no children in the chart of accounts hierarchy), the row's credit, debit, and net balance values reflect only the account's own balance data.
- **REQ-RPT-1.5** For parent accounts, the row's total credits, total debits, and net balance must equal the account's own values plus the sum of all descendant accounts' corresponding values. The roll-up is recursive: a grandparent's totals include its children's rolled-up totals.
- **REQ-RPT-1.6** The result list must be sorted in depth-first tree order: top-level accounts are sorted by account code, and within each parent, children are sorted by account code. A parent account's row appears immediately before its children's rows.
  - *Why:* A flat code sort interleaves unrelated subtrees (e.g., child 5311 sorts after sibling parent 5300, breaking the hierarchy). Depth-first code-ordered traversal preserves the outline structure that makes a trial balance readable. (2026-08-10)
- **REQ-RPT-1.7** Top-level accounts (those with no parent) are at generation 0. Each level of nesting increments the generation by 1.
- **REQ-RPT-1.8** Voided journal entries do not contribute to the trial balance. Their amounts are treated as zero.
- **REQ-RPT-1.9** Only journal entries with an entry date on or before the as-of Calendar Date contribute to the trial balance. Entries dated after the as-of date are excluded.
  - *Why:* The trial balance is a point-in-time snapshot. (2026-08-07)
- **REQ-RPT-1.10** Net balance computation respects the account type's normal balance direction: debit-normal accounts compute net as debits minus credits; credit-normal accounts compute net as credits minus debits.
- **REQ-RPT-1.11** An account with no qualifying journal entry activity as of the report date must appear in the result with zero Money values for total credits, total debits, and net balance.
- **REQ-RPT-1.12** Stricken.

## 2. Report output

- **REQ-RPT-2.1** Report operations must support an output specifier that determines how results are delivered: data-only or rendered report.
- **REQ-RPT-2.2** In data-only mode, the trial balance operation returns the computed data as a list of boundary-type rows suitable for JSON serialization. Each row includes: account code (string), account name (string), generation (int), total credits (decimal), total debits (decimal), and net balance (decimal).
- **REQ-RPT-2.3** In report mode, the operation renders the trial balance to an HTML file and returns the fully qualified file path to the written file.
- **REQ-RPT-2.4** The report output path is constructed from a caller-provided base directory and file name. When date interpolation is requested, the as-of date in yyyy-MM-dd format is appended to the file name, prefixed with a hyphen, before the file extension.
- **REQ-RPT-2.5** If writing the report file fails, the operation must fail with a typed AppError.
- **REQ-RPT-2.6** Report operations are read-only. No database transaction is required and no ledger state is modified.
  - *Why:* Reports query the ledger; they do not participate in it. A report that fails mid-render leaves no dirty state to roll back. (2026-08-07)

## 3. Trial balance HTML rendering

- **REQ-RPT-3.1** The rendered HTML report must contain a header section displaying the report title and the as-of Calendar Date.
- **REQ-RPT-3.2** The rendered HTML report must contain a footer section displaying the instant at which the report was generated.
- **REQ-RPT-3.3** Each account row must carry a CSS class indicating its hierarchical depth (generation), enabling depth-based visual styling.
- **REQ-RPT-3.4** Each monetary value in an account row must carry a CSS class indicating its sign (positive, negative, or zero), enabling sign-based visual distinction.
- **REQ-RPT-3.5** The rendered HTML must include print-optimized CSS.
- **REQ-RPT-3.6** Each account row must display three labeled monetary values: total credits, total debits, and net balance.


## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-RPT-1.1 | Too broadly scoped — any trial balance test exercises this | Dan, 2026-08-07 |
| REQ-RPT-1.3 | Too broadly scoped — every test that examines a row proves it implicitly | Dan, 2026-08-07 |
| REQ-RPT-2.1 | Too broadly scoped — the data-only and report-mode tests exercise both branches | Dan, 2026-08-07 |
| REQ-RPT-2.5 | File I/O failure depends on OS state; verified by code review of the error-handling branch | Dan, 2026-08-07 |
| REQ-RPT-2.6 | Architectural constraint (NoTransaction, FetchOnly context) — verified by code review | Dan, 2026-08-07 |
| REQ-RPT-3.1 | HTML structure verified by code review and visual inspection of rendered output | Dan, 2026-08-07 |
| REQ-RPT-3.2 | HTML structure verified by code review and visual inspection of rendered output | Dan, 2026-08-07 |
| REQ-RPT-3.3 | CSS class assignment verified by code review and visual inspection of rendered output | Dan, 2026-08-07 |
| REQ-RPT-3.4 | CSS class assignment verified by code review and visual inspection of rendered output | Dan, 2026-08-07 |
| REQ-RPT-3.5 | CSS content verified by code review and visual inspection of rendered output | Dan, 2026-08-07 |
| REQ-RPT-3.6 | HTML structure verified by code review and visual inspection of rendered output | Dan, 2026-08-07 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| | | |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
| REQ-RPT-1.12 | The balance computation underlying the trial balance must accept an optional account filter. When no filter is provided, balances are returned for all accounts. An explicitly empty filter (a list of zero account identifiers) is invalid and must fail with a typed AppError. | Trial balance must have 100% of accounts to actually confirm balance. |

