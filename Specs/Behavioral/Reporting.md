# Reporting

Behavioral specs for the reporting domain. Reports are read-only computations over the ledger and positions (Positions.md), producing structured data and optional rendered output. Reports never modify ledger or positions state. (Positions added 2026-10-04)

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
- **REQ-RPT-2.4** The report output path is constructed from a caller-provided base directory and file name. The caller's file name carries no extension; the system appends `.html`. When date interpolation is requested, the as-of date in yyyy-MM-dd format, prefixed with a hyphen, is appended to the file name before the extension. (Extension rule added 2026-09-26)
- **REQ-RPT-2.5** If writing the report file fails, the operation must fail with a typed AppError.
- **REQ-RPT-2.6** Report operations are read-only. No database transaction is required and no ledger state is modified.
  - *Why:* Reports query the ledger; they do not participate in it. A report that fails mid-render leaves no dirty state to roll back. (2026-08-07)

## 3. HTML rendering

REQ-RPT-3.1, 3.2 and 3.5 apply to every rendered report (trial balance, balance-sheet integrity, period activity, pre-posting review, net worth, investment wealth history), and the waivers on 3.2 and 3.5 cover every rendered report. REQ-RPT-3.3, 3.4 and 3.6 describe trial-balance account rows and apply to the trial balance only, as do their waivers. (Scope stated 2026-10-03; net worth and investment wealth history added 2026-10-04; amended 2026-10-05 — 3.3, 3.4 and 3.6 narrowed to the trial balance, which is the only report with trial-balance account rows.)

- **REQ-RPT-3.1** The rendered HTML report must contain a header section displaying the report title and the as-of Calendar Date.
- **REQ-RPT-3.2** The rendered HTML report must contain a footer section displaying the instant at which the report was generated: the initiation instant of the operation that rendered it (REQ-SYS-3.4). (Amended 2026-10-03)
- **REQ-RPT-3.3** Each account row must carry a CSS class indicating its hierarchical depth (generation), enabling depth-based visual styling.
- **REQ-RPT-3.4** Each monetary value in an account row must carry a CSS class indicating its sign (positive, negative, or zero), enabling sign-based visual distinction.
- **REQ-RPT-3.5** The rendered HTML must include print-optimized CSS.
- **REQ-RPT-3.6** Each account row must display three labeled monetary values: total credits, total debits, and net balance.


## 4. Reconciliation

Reconciliation compares ledger balances to balances captured from each institution at extract time. The comparison is arithmetic, so it is computed, not judged; judgment applies only to explaining a non-zero delta.

A clearing account, such as the one transfers between imported accounts post through (DataIngestion, Transfers), is reconciled by supplying an external balance of zero. A non-zero delta is a transfer still in transit, or a side that is missing or doubled. (2026-09-27)

- **REQ-RPT-4.1** The system must provide a reconciliation computation. Input: a list of (account code, external balance, as-of Calendar Date). Output, one row per input: account code, account name, as-of date, external balance, ledger net balance as of that date, and delta (external minus ledger).
- **REQ-RPT-4.2** The ledger net balance follows the trial balance rules: voided entries excluded (REQ-RPT-1.8), entries dated after the as-of date excluded (REQ-RPT-1.9), net computed in the account's normal-balance direction (REQ-RPT-1.10). A parent account's balance includes its descendants (REQ-RPT-1.5). External balances are supplied in the same direction — for example, a credit card balance owed is positive.
- **REQ-RPT-4.3** An account code that does not resolve to an existing account, or that appears more than once in the input, fails the computation with a typed error naming the code.
- **REQ-RPT-4.4** The system must provide a means to run the reconciliation computation against the ledger as it would stand after posting every postable staged entry, leaving no change to ledger or staging data; any staging writes roll back with the ledger. Posting is simulated exactly as in shadow post (REQ-STG-8.2, REQ-STG-8.4). (Amended 2026-10-03)
  - *Why:* The shadow recon loop is what keeps errors out of an indelible ledger. It must compare against the simulated post, and the comparison must be mechanical, so "reconciled" is a computed fact rather than a model's reading of two tables. (2026-09-26)
- **REQ-RPT-4.5** Reconciliation is read-only and makes no judgment. A non-zero delta is output, not an error.
- **REQ-RPT-4.6** The shadow variant (REQ-RPT-4.4) writes to the ledger inside a transaction that is always rolled back; it is therefore a command operation, not a report, and REQ-RPT-2.6 does not apply to it. The non-shadow variant, integrity, and period activity are read-only reports.

## 5. Balance-sheet integrity

- **REQ-RPT-5.1** The system must provide a balance-sheet integrity computation that accepts an as-of Calendar Date and returns: total debits and total credits across all non-voided journal entry lines dated on or before that date, and whether they are equal.
  - *Why:* The only thing that can truly break the books is a one-legged journal entry. This is that assertion. (2026-09-26)
- **REQ-RPT-5.2** The computation also returns, as of the same date, the net balance of each account type (Asset, Liability, Equity, Revenue, Expense) in its normal-balance direction; net income (Revenue minus Expense); and the residual: Assets minus (Liabilities plus Equity plus net income).
  - *Why:* With no closing entries, lifetime net income sits in the revenue and expense accounts, so Assets = Liabilities + Equity does not hold on its own. That is correct, not a defect. Returning the full identity lets anyone confirm it at a glance and stops the net-income gap from being re-investigated as a discrepancy. (2026-09-26)
- **REQ-RPT-5.3** Unequal debits and credits, or a non-zero residual, are returned as data, not raised as an error. The caller decides whether to stop.
- **REQ-RPT-5.4** The computation also returns every Account that is deactivated (REQ-AC-1.48) as of the current date (the calendar date of the operation's initiation instant, REQ-SYS-3.4) and whose balance (REQ-AC-3.13.1) as of that date is not zero. Each such Account is reported with its code, name, active-end date and balance, and with the journal entries touching it that were posted, or voided, after its active-end date. An empty list means every deactivated account holds a zero balance. Like REQ-RPT-5.3, a listed Account is data, not an error. (2026-10-03)
  - *Why:* Deactivation checks the balance only at the moment it happens (REQ-AC-4.4). A later backdated post or a void can leave money on a retired account that no active-account view shows. Blocking those posts was rejected; the weekly look-back catches the residue instead. (2026-10-03)

## 6. Period activity

The spending view: what came in and what went out over a date range, with the transactions behind each total.

- **REQ-RPT-6.1** The system must provide a period activity computation that accepts a begin and an end Calendar Date (inclusive) and returns, for every Revenue and Expense account with lines of its own in the range: account code, account name, net total for the range in the account's normal-balance direction, and each contributing journal entry line (entry date, journal entry ID, journal entry description, line type, amount, memo). Each account shows its own lines only: there is no roll-up into parent accounts, and a parent with no lines of its own in the range does not appear. (Amended 2026-10-03)
- **REQ-RPT-6.2** Voided journal entries contribute nothing to period activity.
- **REQ-RPT-6.3** Accounts are ordered as in the trial balance (REQ-RPT-1.6); lines within an account are ordered by entry date, then journal entry ID.
- **REQ-RPT-6.4** Balance-sheet integrity (§5) and period activity (§6) support the output modes of §2: data-only, and rendered HTML written to a caller-provided path. For period activity, date interpolation (REQ-RPT-2.4) appends the begin and end dates as `-yyyy-MM-dd_yyyy-MM-dd`, and the rendered header (REQ-RPT-3.1) shows the range. Reconciliation (§4) is data-only.
  - *Why:* Reconciliation feeds the operator and the Saturday summary, not a printed page; each row carries its own as-of date, so a single header date does not describe it. (2026-09-26)


## 7. Pre-posting review

What is about to post, line by line, with how each line was classified and what obligation it pays. Saturday fixes errors before they post (DataIngestion REQ-STG-4.2); this is where the operator sees them.

- **REQ-RPT-7.1** The system must provide a pre-posting review computation covering every postable staged entry (DataIngestion REQ-STG-4.4): exactly the entries batch post would post if run now. It takes no date range.
  - *Why:* The review must show the whole of what the next post will write, no more and no less. It is for the operator's judgment, so it runs after shadow post and reconciliation come back clean (DataIngestion §8, §4 here): mechanical failures are fixed before anyone reads the list. (2026-09-27)
- **REQ-RPT-7.2** For each entry it returns: entry date, description, source name, fi_reference, and status (`'Classified'` or `'Reviewed'`). Then each of its lines, debits before credits, each carrying: line type, amount, memo, account code and account name.
  - *Why:* Status separates what the classifier decided from what the operator decided. Balance is not shown: shadow post has already proved every entry balances. (2026-09-27)
- **REQ-RPT-7.3** Each line also carries the name of the classification rule that gave it its account: only account-claimant rule matches are considered. Among the classification runs that recorded such a match for the line (DataIngestion REQ-STG-5.10) with the line's current account, take the most recent. When several rules in that run claim the line's current account, name the one with the lowest priority value (the winning priority, REQ-CR-1.6), then the first by rule name. When no such match exists (the account came from the parser or the operator), the field is empty. (Amended 2026-10-03)
  - *Why:* A wrong account is usually a wrong rule. Naming the rule means fixing it once, not correcting the same line every week. (2026-09-27)
- **REQ-RPT-7.4** When a line is linked to a Payment Agreement (CashFlow §12), the line carries the Payment Agreement's name and its Master Agreement's name. The line also carries every Payment that references it, each with the Payment's amount, its Invoice's invoice date, due date, amount and payment state, and its Instance's date, ordered by Instance date, then Invoice due date. A linked line that no Payment references shows the agreement and nothing more. (Amended 2026-10-03)
  - *Why:* An obligation matched to the wrong line, or not matched at all, is visible before the cash posts. (2026-09-27)
- **REQ-RPT-7.5** A line with no account fails the review with a typed error naming its entry and line.
  - *Why:* Shadow post has already failed on such a line (DataIngestion REQ-STG-9.4), so reaching the review with one means the order was skipped. (2026-09-27)
- **REQ-RPT-7.6** Entries are ordered by entry date, then source name, then fi_reference.
- **REQ-RPT-7.7** The pre-posting review is a read-only report (REQ-RPT-2.6) and supports the output modes of §2. Date interpolation (REQ-RPT-2.4) appends the date the report runs, and the rendered header (REQ-RPT-3.1) shows that date and the number of entries and lines.

## 8. Net worth

What the household is worth on a date: the ledger's assets and liabilities, with investments and
property at market value in place of whatever cost the ledger carries for them.

- **REQ-RPT-8.1** The system must provide a net worth computation that accepts an as-of Calendar Date. The date must fall within an existing fiscal period; otherwise the computation fails with a typed error naming the date.
  - *Why:* liabilities come only from the ledger, and the ledger holds nothing before its first fiscal period. A net worth for an earlier date would count every asset and no debt. Investment wealth over time (§9) has no such limit. (2026-10-04)
- **REQ-RPT-8.2** Net worth is the sum of:
  - the net balance, as of the date, of every Asset account that is not linked to an Investment Account or a Property (Positions REQ-POS-4.8, REQ-POS-9.7). A Property may link several asset accounts; its value replaces all of them (amended 2026-10-05);
  - the market value of every line in the holdings as of the date (Positions REQ-POS-8.1);
  - the value on the date (Positions REQ-POS-10.4) of every Property owned on the date (Positions REQ-POS-9.4);

  less the net balance, as of the date, of every Liability account. Balances follow the trial balance rules (REQ-RPT-1.8 to 1.10), each account's own balance only, with no roll-up into parents, so no amount is counted twice.
  - *Why linked accounts are left out:* a linked ledger account carries an investment or a property at cost; its market value is already counted from Positions. (2026-10-04)
- **REQ-RPT-8.3** A Property's equity on the date is its value less the net balance of each of its mortgage Accounts (Positions REQ-POS-9.8) as of the date.
- **REQ-RPT-8.4** Investable wealth is net worth less the equity of the Property whose use is 'PrimaryResidence' and that is owned on the date, if there is one.
  - *Why equity, not value:* investable wealth answers "what would we be worth if the house and its loan did not exist". Removing the value but keeping the mortgage would charge the household for a debt secured by an asset no longer counted. (2026-10-04)
- **REQ-RPT-8.5** The computation returns:
  - the as-of date;
  - each counted Asset account (code, name, balance) and each Liability account not a mortgage Account of a Property owned on the date (code, name, balance) (amended 2026-10-04: a mortgage of a Property no longer owned still counts in net worth under REQ-RPT-8.2, so it is listed with the other liabilities);
  - each included Investment Account (name, owners' names, account group, tax treatment, snapshot date, provenance, total market value, contribution basis);
  - each owned Property (name, use, owners' names, value, the date of the Valuation it came from or an indication that it is the purchase basis, each mortgage Account with code, name and balance, and equity);
  - totals: counted ledger assets, investments, property values, liabilities (the Liability accounts listed above), mortgages of owned Properties, net worth, and investable wealth. Counted ledger assets plus investments plus property values, less liabilities and less mortgages of owned Properties, equals net worth (amended 2026-10-05: the owned-property mortgages total is added so the totals add up to net worth);
  - investment market value totalled by tax treatment, and by account group.
  - *Why the tax-treatment totals:* whether tax is owed on all of a balance, only its growth, or none of it is the first thing anyone settling the household's affairs needs to know. (2026-10-04)
- **REQ-RPT-8.6** Net worth is a read-only report (REQ-RPT-2.6) and supports the output modes of §2: data-only, and rendered HTML written to a caller-provided path. Date interpolation (REQ-RPT-2.4) appends the as-of date.

## 9. Investment wealth history

How invested wealth has grown, month by month, split along any one line.

- **REQ-RPT-9.1** The system must provide an investment wealth history computation that accepts a begin and an end Calendar Date and a grouping, one of: account, account group, tax treatment, owners, or any of the seven allocation dimensions (Positions REQ-POS-2.1). The end date may not be earlier than the begin date.
- **REQ-RPT-9.2** The computation returns one point for every month-end date (the last day of a calendar month) on or after the begin date and on or before the end date, in date order. At each point, the holdings as of that date (Positions REQ-POS-8.1) are totalled by market value for each value of the grouping, with a total across all of them. Every grouping value that has holdings at any point in the range appears at every point; at a point where it has no holdings its total is 0.00, not absent. (Amended 2026-10-05)
  - *Why month-ends:* snapshots arrive weekly; a monthly series is what shows growth over years without weekly noise. (2026-10-04)
- **REQ-RPT-9.3** Grouping by owners groups each account by its complete set of owners, so a jointly owned account is its own group, not split between its owners. Grouping by a dimension groups each line by its Security's value in that dimension; lines with no value in it are grouped as unassigned.
- **REQ-RPT-9.4** Investment wealth history is not limited to fiscal periods. A point with no holdings is reported with a zero total.
  - *Why:* investment history reaches back years before the ledger began. (2026-10-04)
- **REQ-RPT-9.5** Investment wealth history is a read-only report (REQ-RPT-2.6) and supports the output modes of §2. Rendered, it is a table with one row per point and one column per group value; a group with no holdings at a point shows 0.00, not a blank cell (REQ-RPT-9.2). Date interpolation (REQ-RPT-2.4) appends the begin and end dates as `-yyyy-MM-dd_yyyy-MM-dd`. The rendered header (REQ-RPT-3.1) shows the begin and end dates and the grouping in place of an as-of date. (Amended 2026-10-05)

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

