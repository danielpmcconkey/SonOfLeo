# Brief — test agent, Positions slice 2

Written 2026-10-10 by Hobson for a Claude Code session that can see this repository and
nothing else. **Entry point is `HobsonsNotes/plan-positions-2-2026-10-10.md` §0**, which says
when to work from this brief: Part A names before any Src is read, the tests themselves after
the Src work. This brief is README step 5. It says **what** the system now does in business
terms and where the spec stands, never **how** the code does it. Where this brief and
`Specs/Behavioral/` disagree, the spec wins. Tell Dan.

## 0. Ground rules

- **Branch:** `positions-slice-2`. Run `git pull --rebase --autostash` before every commit. The same
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
  layers"). A rule that needs no database (a lot's quantities summing to the line's, an
  activity kind that must carry a quantity) is an isolated test. A rule that needs the
  database is tested once, at the orchestration layer. Routes get the happy path, the
  conversions, and the failure vectors only a caller can produce.
- **Public repository:** no institution names, real tickers, people, account numbers or real
  amounts in tests, fixtures or commit messages. Invent them, as slice 1's fixtures do.
- **Running tests:** `Tests.Isolated` needs no database. `Tests.Integrated` needs the
  `sonofleo_test` database. If it isn't there, say so. Don't skip, fake or weaken anything.
- **Slice 1's tests are a regression net.** Net worth on a date inside a fiscal period must
  give exactly what it gave before. If a slice 1 test needs changing, that is a finding:
  report it, with why.
- **Commit subjects** describe behaviour, not the edit. Follow the repo's existing style.

**Read first:** `README.md`, `Specs/README.md`, `Tests/README.md` and the README in each test
directory you touch, `Skills/TestWriter/SKILL.md` and
`references/bullshit-test-specimens.md`, `Skills/TestNameReview/SKILL.md` (the name-quality
check), and `Skills/SonOfLeoRequirementsAudit/resolved-findings.md`, which records rulings you
must not re-litigate.

---

## Part A — Requirements that are new or changed

`git diff main -- Specs/` on this branch shows every change: `Specs/Behavioral/Positions.md`,
`Specs/Behavioral/Reporting.md` §3 and §8–§10, and `Specs/Definitions.md` (Pre-ledger date).
Read each REQ in the spec; the summaries below only tell you what the slice is about and where
the edges are. **Every new active REQ ID needs at least one citing test**, and every amended
one needs its new clause tested (or a waiver Hobson approves — propose it in your report,
don't write it into the spec).

### A.1 The business picture

Slice 1 records what the household holds each week. This slice adds four things:

- **Lots.** In a taxable account, the institution can say *when* each unit of a fund was
  bought: its open tax lots. The system records them on the week's snapshot, exactly as
  reported. It works nothing out from them; whatever plans from the data does that.
- **Activity.** What the institution says happened in an account: money paid in (and from what
  source, such as an employee deferral or an employer match), rollovers, transfers, purchases,
  sales, reinvested and cash dividends, interest, fees, withdrawals, and corporate actions such
  as a split. Each is tagged with one of a fixed set of kinds; the institution's own wording is
  kept beside it.
- **A unit roll-forward.** Between two snapshots of an account: what was held at the start,
  plus the units that came in, less the units that went out, should be what was held at the
  end. The roll-forward shows the difference for every fund. It judges nothing.
- **Net worth before the ledger, and over time.** The ledger only starts this year. Older
  records give some account balances on older dates: some cash, some debts, a mortgage.
  Recorded as *pre-ledger balances*, they let net worth be computed for those dates, and a
  new **net worth history** report gives month-end net worth across years, each point saying
  which parts of the picture are missing.

### A.2 Lots (REQ-POS-6.9–6.13, 5.6, and amended 7.2, 7.5, 8.3)

- A snapshot line may carry lots; each has an acquired date, a quantity above zero, and a cost
  basis that is not negative or is absent. **Only lines in taxable accounts may carry lots.**
- When a line carries lots, **their quantities add up exactly to the line's quantity** —
  not within a tolerance. The lots' costs are *not* checked against the line's cost.
- A lot cannot be acquired after the snapshot date.
- A line with no lots is fine in any account, every week. It means the lots were not supplied.
- Two identical lots on one line are two lots. Lots come back in the order they were sent.
- Re-recording a snapshot replaces its lots with the new ones (or with none).
- Fetching a snapshot, and holdings as of a date, return each line's lots.
- An account whose snapshot lines carry lots cannot be switched away from taxable.

- **New:** each rule; the sum off by the smallest step a Quantity allows (six decimal places),
  both directions; an acquired date equal to the snapshot date (accepted); identical lots
  preserved and ordered; replacement removing lots; lots unaffected by net worth (a line with
  lots gives the same net worth as without).

### A.3 Activity (REQ-POS-12.1–13.5, and amended 11.4, 11.10)

- Each activity: account, date, kind (one of fifteen), the institution's description, an
  optional source, an optional security, an optional quantity and price, and an amount that is
  never negative.
- **The kind decides the shape.** Purchases, sales, reinvestments and corporate-action
  adjustments must name a security and carry a quantity. Dividends, interest and capital-gain
  distributions may name the paying security but never carry a quantity. The other kinds
  (contributions, withdrawals, rollovers, transfers, fees) may name a security, and carry a
  quantity exactly when they do. A price only ever comes with a quantity.
- A named security must already be held in that account; the system never creates the holding.
- The date must fall in the account's active period and not in the future.
- **Recording is by range.** For each account, the caller sends a begin and end date and every
  activity in between. That range's existing activity is replaced, wholesale; outside the range
  nothing changes. An empty list clears the range. Several accounts in one request, all or
  nothing; two overlapping ranges for one account in one request are rejected; an activity
  dated outside its range is rejected. The result says how many were removed and recorded.
- Identical activities on one day are kept as separate activities, in the order sent.
- List an account's activities between two dates, in date order and then in the order sent.
- An account's active period can't be narrowed so its activities fall outside it, and a
  holding an activity names can't be deleted.

- **New:** every kind at least once through record and list; each shape rule's rejection; the
  three range rules; replacement of a range leaving neighbouring dates alone; re-recording an
  identical range (succeeds, removed = recorded); atomicity across two accounts.

### A.4 Unit roll-forward (REQ-POS-14.1–14.3)

Given an account and two of its snapshot dates: one row per fund on either snapshot or named
by a units-moving activity between them, each with start quantity, units in, units out,
expected, end quantity and the difference. Activity on the first date is *not* counted (the
first snapshot already includes it); activity on the second date *is*. A fund absent from a
snapshot counts as zero there. Differences are data, not errors, and can be negative, as can
the expected quantity.

- **New:** the window edges (activity on each snapshot date, one day either side); a fund
  bought in full between the snapshots; a fund sold in full; an activity kind that moves no
  units contributing nothing; a missing activity giving a non-zero difference; a negative
  expected quantity; the first date not earlier than the second; a date with no snapshot.

### A.5 Pre-ledger balances (REQ-POS-15.1–15.6)

- A dated balance of one ledger account, asset or liability only, for a date **before the
  ledger's first fiscal period**. Rejected if there is no fiscal period at all.
- Not for an account that stands for an investment account or a property's asset. A
  property's mortgage account *may* carry one; that is how old equity is known.
- The balance may be zero (an account that ended) or negative.
- One per account per date; recording again replaces it. Record many at once, all or nothing.
  Delete one. List them, optionally for one account, between two dates.

- **New:** each rule; a date one day before the first fiscal period (accepted) and on its
  first day (rejected); a mortgage account accepted where a property's asset account is not.

### A.6 Net worth on a pre-ledger date (REQ-RPT-8.1, 8.2, 8.3, 8.5, 8.7, 8.8)

- Net worth now works on **pre-ledger dates** as well as dates in a fiscal period. A date after
  the last fiscal period, or in a gap between two, still fails.
- On a pre-ledger date, each account's balance is its latest pre-ledger balance on or before
  the date. **An account with none is left out entirely, not shown as zero.** Investments and
  property are computed as before. A property's equity uses its mortgage's pre-ledger balance.
- The result says whether the date was pre-ledger, and gives each account's balance date.
- **Absent components.** Five parts of net worth (ledger assets, investments, property values,
  liabilities, mortgages of owned properties) may each be absent: nothing contributes to it on
  that date. Absent parts total zero and are named.

- **New:** derive every expected figure from the fixture by hand (Specimen 6). A pre-ledger
  date drawing on the latest of several balances; a later pre-ledger balance ignored; a recorded
  zero listed while a missing account is not; each component absent at least once; none absent
  on a fiscal-period date; a date in a fiscal period ignores pre-ledger balances entirely; the
  out-of-range date still fails.

### A.7 Net worth history (REQ-RPT-10.1–10.4)

Begin and end dates; one point per month-end in between, each with the pre-ledger flag, the
seven totals and the absent components. End before begin fails. Any month-end that is neither
in a fiscal period nor pre-ledger fails the whole report, naming the earliest. Data-only and
rendered HTML (`-begin_end` on the file name).

- **New:** a range spanning the last pre-ledger month-end and the first fiscal-period one;
  month-end selection at the edges, as for investment wealth history; the failure naming the
  earliest bad month-end; both output modes.

### A.8 The interface (REQ-NGUI, applied to new and changed routes)

The new operations are reachable through the operator CLI, and net worth history through the
Reports CLI. Accounts by name, ledger accounts by code with their name beside them in every
returned payload. Lots and activities go in and come back as ordered lists, unrounded.

- **New:** a route-level happy path per new operation; lots and activity order preserved
  through the boundary; a quantity with six decimal places round-tripped; an unknown activity
  kind rejected; the account-code-not-found vector for pre-ledger balances.

---

## Part B — Test infrastructure

1. **Fixture truncation.** Add the three new tables to `Tests.Helpers/TestDataStage.fs`.
2. **Fixture data.** Extend slice 1's fictional fixture: lots on a taxable holding (including
   two identical lots), a non-taxable account to attempt lots on; activity across several weeks
   for one account covering every kind, consistent with two of its snapshots so a roll-forward
   balances, and a second account whose roll-forward does not; ledger accounts with pre-ledger
   balances on several dates before the fixture's first fiscal period, including a zero, a
   negative, and a property's mortgage account; and a pre-ledger date where some components
   are absent. Keep expected values derivable by hand.

---

## Final report (append it to §8 of the plan, with the Src report)

- What you did, by Part A item and Part B item, and what you did not do, with the reason.
- Test status: which suites ran, where, and the pass/fail counts. If `sonofleo_test` was
  absent, say so.
- Every assertion's fail-then-pass evidence (constraint 3), summarised.
- Every failure: the test, the REQ, and whether the bug is in **Src** or the **spec**.
- Any slice 1 test you changed, and why.
- Any waiver you propose, with the REQ and the reason.
- Output of `bash Skills/SonOfLeoRequirementsAudit/traceability-audit.sh` on the branch:
  Invariant 1 and Invariant 2. List any untested active REQ in REQ-POS or REQ-RPT.
- Output of `bash Checks/run-all.sh`.
