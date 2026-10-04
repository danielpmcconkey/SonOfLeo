# Brief — test agent, Positions slice 1

Written 2026-10-04 by Hobson for a Claude Code session that can see this repository and
nothing else. **Entry point is `HobsonsNotes/plan-positions-2026-10-04.md` §0**, which says when
to work from this brief: Part A names before any Src is read, the tests themselves after the
Src work. This brief is README step 5. It says **what** the system now does in business terms
and where the spec stands, never **how** the code does it. Where this brief and
`Specs/Behavioral/` disagree, the spec wins. Tell Dan.

## 0. Ground rules

- **Branch:** `positions`. Run `git pull --rebase --autostash` before every commit. The same
  session implements Src from the plan, in the order its §0 sets.
- **While working from this brief, edit `Tests/` only.** Never edit `Specs/` or `Checks/`; Src
  fixes a test exposes go through the plan. If a spec looks wrong, or a test can't be written
  honestly against it, stop and report it with the REQ ID. Never edit the spec, and never
  weaken a test to get a pass.
- **The three load-bearing constraints (README):**
  1. For Part A, draft test names from the spec alone, before you read any Src.
  2. Run the name-quality check, then commit the names as failing placeholders
     (`Assert.Fail "Not yet implemented"`). The committed name is the contract. Only after
     that do you read Src. Src can change how a test reaches a behaviour, never what it
     asserts. To renegotiate a name, say so out loud.
  3. Watch every new assertion fail before you call it done: perturb the expected value, run
     it, read the failure, restore it, and report the output.
- **Lowest layer that can carry the failure vector** (`Tests/README.md`, "Hierarchy of testing
  layers"). Construction rules of a value type are isolated tests. A rule that needs the
  database is tested once, at the orchestration layer. Routes get the happy path, the
  conversions, and the failure vectors only a caller can produce.
- **Public repository:** no institution names, real tickers, people, account numbers or real
  amounts in tests, fixtures or commit messages. Invent them: "Alex Example", "Sam Example",
  "Example Total Market Index Fund" (ticker "EXTMX"), "Example Brokerage", "12 Example Street".
- **Running tests:** `Tests.Isolated` needs no database. `Tests.Integrated` needs the
  `sonofleo_test` database. If it isn't there, say so. Don't skip, fake or weaken anything.
- **Commit subjects** describe behaviour, not the edit. Follow the repo's existing style.

**Read first:** `README.md`, `Specs/README.md`, `Tests/README.md` and the README in each test
directory you touch, `Skills/TestWriter/SKILL.md` and
`references/bullshit-test-specimens.md`, `Skills/TestNameReview/SKILL.md` (the name-quality
check), and `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, which records rulings you
must not re-litigate (see ROUTE-ORACLE).

---

## Part A — Requirements that are new

Everything in this slice is new; there are no existing tests for any of it. The full text is
`git show a3cb261 -- Specs/`: `Specs/Behavioral/QuantityAndPrice.md`, `Person.md`,
`Positions.md`, and §8–§9 of `Reporting.md`. Read each REQ in the spec; the summaries below only
tell you what the slice is about and where the edges are. **Every active REQ ID in those files
needs at least one citing test** (or a waiver Hobson approves — propose it in your report,
don't write it into the spec).

### A.1 The business picture

The household's ledger has, until now, known only cash and debts. This slice records **what
the household holds and what it is worth**:

- **People** who own things. A Person is a name and a birthdate. A Person is not a user of the
  system.
- **Investment accounts** at institutions — a brokerage account, a retirement account, a
  health savings account — each with its owners, its tax treatment, a reporting label (account
  group), the period it was open, and optionally the one ledger account that stands for it.
- **Securities** — the funds and stocks those accounts can hold — each tagged along seven
  fixed allocation dimensions (investment type, market cap, index type, sector, region,
  objective, and the benchmark index it tracks).
- **Holdings** — "this account holds this security" — with the cost basis method the
  institution uses, which only matters in a taxable account.
- **Account snapshots** — once a week, what the institution said the account held: each
  security's quantity, price, market value and cost basis, exactly as reported. A snapshot is
  the whole account: a security missing from it is not held that day.
- **Properties** — a primary residence, a rental — with owners, purchase date and price, an
  optional sale date, links to the ledger's asset and mortgage accounts, and dated
  valuations.

Two reports sit on top: **net worth** on a date (ledger cash and debts, plus investments and
property at market value instead of the cost the ledger carries), and **investment wealth
history** (month-end totals over a span of years, split one way at a time).

### A.2 Quantity and Price (REQ-QP-1.1–3.5)

Share counts and per-share prices are their own kinds of number, never Money. Both are exact,
not negative, at most 9,999,999,999.999999, and at most six decimal places — **a seventh decimal
place is rejected, never rounded**. They convert to and from .NET decimal, compare, and a
Quantity times a Price gives the exact product as a decimal (not Money).

- **New:** isolated theories over the boundaries of each rule for each type (zero, the maximum,
  one step over, six vs seven decimal places, negative), the round trip, every comparison, and
  an exact product that Money could not hold (more than two decimal places).

### A.3 Person (REQ-PER-1.1–2.4)

Create, update (addressed by current name), list ordered by name. Name trimmed, not blank, at
most 100 characters, unique (exact, case-sensitive). Birthdate required and not in the future
— "today" being the date of the operation's own start. A name that matches nobody fails naming
it. There is no way to delete a Person.

- **New:** each rule, at its lowest layer; the list's content and order from fixtures.

### A.4 Dimension Values and Securities (REQ-POS-2.1–3.4, 11.1, 11.2, 11.9)

The seven dimensions are fixed; their values are the operator's to create and rename. A value's
name is unique within its dimension only. A Security has a unique name, an optional unique
ticker, and at most one value per dimension; putting a dimension's value in another dimension's
slot is rejected with an error naming the value and both dimensions.

- **New:** each rule; the same name in two dimensions is fine; a Security with no ticker and
  with no dimension values is fine; set and clear of ticker and of each dimension on update;
  list content and order.

### A.5 Investment Accounts (REQ-POS-4.1–4.9, 5.3, 5.4, 11.3, 11.4)

- Tax treatment is one of Taxable, TaxDeferred, Roth, Hsa. Only a Taxable account may have more
  than one owner; every account has at least one, and no owner twice.
- Active period: begin required, end optional, both inclusive.
- Ledger link: optional, to an existing Asset account of subtype Investment, given by code.
  **One ledger account may stand for at most one investment account or property** — two
  investment accounts, two properties, or one of each, all rejected.
- Updates: owners are given as the whole new set. Changing owners or tax treatment (or both at
  once) is judged on the result. Narrowing the active period so that an existing snapshot falls
  outside it is rejected, naming the earliest and latest offending dates.

- **New:** each rule. Make sure the owner-count rule is tested for a change of tax treatment and
  owners *in the same update*, both the rejected combination and the accepted one.

### A.6 Holdings (REQ-POS-5.1–5.3, 11.5)

A Holding is one security in one account, at most once. In a Taxable account it must carry a
basis method (AverageCost or SpecificLot); in any other account it must carry none. Changing an
account's tax treatment is refused while a Holding would then break that rule, naming every
such security. A consequence worth knowing: once an account has Holdings, its tax treatment
cannot move between Taxable and anything else. That is intended.

- **New:** each rule; the error naming every offending security (use two); list content and
  order, with and without the one-account filter.

### A.7 Account snapshots (REQ-POS-6.1–7.5)

- One snapshot per account per date; the date inside the account's active period and not in the
  future. Provenance is Reported or Imported. Contribution basis only on a Roth account, and
  never negative.
- Lines: each names a security that must already be **held in that account** — the system never
  creates the Holding — at most once per snapshot. Quantity above zero, market value and cost
  basis not negative, cost basis may be absent. **Quantity × price must agree with market value
  within 0.05 USD**, or the line is rejected naming the account, date, security, product and
  market value.
- Recording takes one or more snapshots at once, all or nothing; the same account and date twice
  in one request is rejected. Recording a date that already has a snapshot **replaces it
  entirely** — provenance, contribution basis, every line — and says it replaced one. A snapshot
  with no lines is valid (an account that holds nothing).
- Delete by account and date; a date with nothing to delete fails. Fetch one; list an account's
  snapshot dates with provenance between two dates, inclusive, in order.
- **The figures come back exactly as sent.** Nothing is recalculated or rounded.

- **New:** each rule. The tolerance at exactly 0.05 (accepted) and just over (rejected), in both
  directions. Atomicity: a request whose second snapshot is bad records neither. Replacement:
  a line present before and absent after is gone, and provenance changes stick. Round-trip
  fidelity on a quantity and price with six decimal places.

### A.8 Holdings as of a date (REQ-POS-8.1–8.4)

One read across every account: for each account **active on the date** with a snapshot on or
before it, that account's latest such snapshot, with the account's details, its owners' names,
its linked ledger account's code and name, the snapshot's own date and provenance, and every
line with the security's name, ticker, dimension value names, basis method and the four
reported figures. Accounts by name, lines by security name.

- **New:** an account whose latest snapshot is weeks before the date is included with its own
  (older) snapshot date; a snapshot dated after the as-of date is ignored; an account ended
  before the date is excluded even though it has snapshots; an account with no snapshot yet is
  excluded; an empty snapshot is included with no lines; full field content and order.

### A.9 Properties and Valuations (REQ-POS-9.1–10.4, 11.6–11.8)

- Use is PrimaryResidence or Rental. Owners as for accounts (any number, at least one).
  Purchase basis above zero. A property is owned from its acquisition date up to, **but not
  including**, its disposal date.
- **At most one primary residence owned on any date.** Selling one and buying the next on the
  same day is fine; overlapping by a day is not.
- Ledger asset link: optional, an Asset account of subtype FixedAsset, and subject to the same
  one-thing-per-ledger-account rule as A.5. Mortgage links: zero or more Liability accounts,
  each the mortgage of at most one property.
- Valuations: one per property per date, value above zero, a basis (how it was arrived at),
  dated within the ownership range (the disposal date itself allowed) and not in the future.
  Re-recording a date replaces it. Narrowing a property's dates so a valuation falls outside
  is rejected, naming the earliest and latest offending dates.
- **A property's value on a date** is its latest valuation on or before that date; with none,
  its purchase basis.

- **New:** each rule, including both primary-residence boundary cases and the value-on-a-date
  fallback.

### A.10 Net worth (REQ-RPT-8.1–8.6)

Accepts an as-of date, which must fall inside an existing fiscal period. Net worth = ledger
asset balances for asset accounts **not** linked to an investment account or property + market
value of the holdings as of the date + the value of every property owned on the date −
every liability account's balance. Each ledger account counts its own balance only, by the
trial balance rules (voids excluded, nothing after the date). A property's equity is its value
less its mortgage balances. **Investable wealth** is net worth less the primary residence's
**equity** (not its value). The report also totals investments by tax treatment and by account
group. Data-only and rendered HTML; the as-of date may be appended to the file name.

- **New:** derive every expected figure from the fixture's own numbers by hand, never from the
  code under test (Specimen 6). Prove a linked ledger account's cost balance is left out and
  its market value counted instead; prove a mortgage is subtracted once; prove investable
  wealth removes equity, with a primary residence that has a mortgage; a date outside every
  fiscal period fails; both output modes.

### A.11 Investment wealth history (REQ-RPT-9.1–9.5)

Accepts a begin and end date and one grouping: account, account group, tax treatment, owners,
or one of the seven dimensions. One point per month-end in range, each totalling holdings as
of that date by group, plus a grand total. A jointly owned account is its own owners-group, not
split. Lines with no value in the chosen dimension are grouped as unassigned. A month-end with
no holdings is a zero point. Not limited to fiscal periods. Rendered as a table, one row per
month-end; file name may carry `-begin_end`.

- **New:** month-end selection at the edges (a range starting mid-month, a range ending on a
  month-end, a February); each grouping kind at least once, with owners and unassigned
  explicitly; a pre-ledger date succeeds; end before begin fails; both output modes.

### A.12 The interface (REQ-NGUI, applied to the new routes)

Every maintenance and read operation above is reachable through the operator CLI, and the two
reports through the Reports CLI. Records are addressed by name, ledger accounts by code, and
every ledger account in a returned payload carries its name beside its code. An update naming
no field is rejected; re-sending a field at its stored value succeeds (REQ-SYS-6.1).

- **New:** a route-level happy path per operation; the account-code-not-found vector; a
  no-field update per updatable record kind; account names beside codes in every payload that
  carries a ledger account.

---

## Part B — Test infrastructure

1. **Fixture truncation.** `Tests.Helpers/TestDataStage.fs` truncates every table before a run.
   Add the `general` and `positions` tables (children before parents is irrelevant under
   `CASCADE`, but list them all).
2. **Fixture data.** Stage what the read tests need, fictional throughout: two or three
   Persons; dimension values in several dimensions; securities with and without tickers and
   with gaps in their dimensions; investment accounts of every tax treatment, one joint and
   taxable, one ended, one linked to a ledger Asset/Investment account; holdings in each;
   snapshots across several weeks and months including an Imported one and an empty one; a
   primary residence with a mortgage and a rental, one with valuations and one without. Add the
   ledger accounts the links need (an Asset/Investment, an Asset/FixedAsset, a Liability for the
   mortgage) with journal entries that give them known balances inside a fiscal period. Keep
   expected values derivable by hand from the fixture.

---

## Final report (append it to §8 of the plan, with the Src report)

- What you did, by Part A item and Part B item, and what you did not do, with the reason.
- Test status: which suites ran, where, and the pass/fail counts. If `sonofleo_test` was
  absent, say so.
- Every assertion's fail-then-pass evidence (constraint 3), summarised.
- Every step-11 failure: the test, the REQ, and whether the bug is in **Src** or the **spec**.
- Any waiver you propose, with the REQ and the reason.
- Output of `bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` on the branch:
  Invariant 1 (no citation of an unknown or withdrawn ID) and Invariant 2 (every active REQ is
  tested or waived). List any untested active REQ in REQ-QP, REQ-PER, REQ-POS, REQ-RPT-8 or
  REQ-RPT-9.
- Output of `bash Checks/run-all.sh`.
