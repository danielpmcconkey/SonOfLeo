# Positions

Behavioral specs for the positions domain: what is held, by whom, and what it was worth on a
date. Positions has two sub-domains, **investments** (accounts at institutions holding
securities) and **real estate** (property). They are peers: neither depends on the other, and
anything that combines them (net worth, investable wealth) is a cross-domain computation
(Reporting §8, §9).

**Design note — relationship to the ledger.** Positions are not ledger records. The ledger is
cash basis and carries an investment or a property, if at all, at cost; Positions carries what
it is worth. No Positions operation creates, alters or reads a journal entry. A ledger account
that stands for an investment account or a property is *linked* to it, and the link has one
job: when net worth is computed, the linked account's cost balance is replaced by the market
value Positions holds, so nothing is counted twice (REQ-RPT-8.2). (2026-10-04)

**Design note — reported figures, recorded verbatim.** Every quantity, price, market value and
cost basis in an account snapshot is the figure the institution reported, stored exactly as
supplied. The system derives nothing into a snapshot and corrects nothing in one; a figure it
cannot accept is rejected (§6). A snapshot is evidence of what the institution said, and later
work reconciles against it, so it must never contain a value the institution did not state.
(2026-10-04)

**Design note — a snapshot is the whole account.** An account snapshot lists every holding in
the account on its date. A holding missing from the set is not held on that date. This is how
a fully sold position drops out: the next snapshot omits it. An account that holds nothing is
recorded as a snapshot with no lines. (2026-10-04)

**Design note — share classes.** When an institution changes a fund's share class, the share
count and price are re-denominated with no exchange between them. Each class is a separate
Security. One Security spanning two classes would splice two price histories into one. (2026-10-04)

**Design note — benchmark, not asset class.** The system records which index a Security tracks
(the Benchmark dimension, §2), not a modelled asset class. How a benchmark maps to a return
series is a modelling choice that belongs to whatever simulates the future, and it will change;
the benchmark a fund tracks is a fact. (2026-10-04)

**Design note — not yet modelled.** Purchases, sales, tax lots and realised gains are out of
scope for this specification. Basis method is recorded on each Holding now (§5) because later
work depends on it. (2026-10-04)

**Design note — naming.** Identifiers in this spec name domain concepts for readability. They do
not prescribe variable, function, property or table names.

**Addressing.** Persons, Dimension Values, Securities, Investment Accounts and Properties are
addressed by name at the boundary; ledger accounts by account code (REQ-NGUI-1.4).

## 1. Entity model

### Investments

- **Dimension Value.** One value in one of the seven allocation dimensions. The dimensions are
  fixed; their values are maintained by the operator.
- **Security.** Something that can be held: a fund, a stock, a money-market fund. Carries at most
  one value per dimension.
- **Investment Account.** An account at an institution that holds securities. Has owners (one or
  more Persons), a tax treatment, an account group (a reporting label), an active period, and
  optionally a link to a ledger account.
- **Holding.** A Security held in an Investment Account. Carries the basis method the institution
  applies to it.
- **Account Snapshot.** What the institution reported for one Investment Account on one date:
  one line per Holding held, and, for a Roth account, the contribution basis.

References: an Investment Account references Persons (owners) and optionally a ledger Account; a
Holding references an Investment Account and a Security; an Account Snapshot references an
Investment Account; each snapshot line references a Holding of that account.

### Real estate

- **Property.** A piece of real estate. Has owners, a use (primary residence or rental), an
  acquisition date and purchase basis, an optional disposal date, and optional links to its
  ledger asset account and its mortgage liability accounts.
- **Valuation.** What a Property was judged to be worth on a date, and on what basis (an
  estimate, an appraisal).

References: a Property references Persons (owners) and optionally ledger Accounts; a Valuation
references a Property.

## 2. Valid and invalid data states — Dimension Value

- **REQ-POS-2.1** A Dimension Value belongs to exactly one dimension, one of: 'InvestmentType', 'MarketCap', 'IndexType', 'Sector', 'Region', 'Objective', 'Benchmark'.
  - *Why seven, fixed:* the allocation views chart every one of them over time. The values inside a dimension change ("Large cap", "Technology", a newly held index); the set of dimensions is part of the design. (2026-10-04)
- **REQ-POS-2.2** Dimension Value name cannot be null or whitespace only (post-trim, per REQ-SYS-1.1), and its length cannot exceed 100 characters.
- **REQ-POS-2.3** No two Dimension Values in the same dimension may share a name. Names compare exactly: case-sensitive, after trimming. The same name may appear in two different dimensions.

## 3. Valid and invalid data states — Security

- **REQ-POS-3.1** Security name cannot be null or whitespace only (post-trim, per REQ-SYS-1.1), and its length cannot exceed 200 characters.
- **REQ-POS-3.2** No two Securities may share a name. Names compare exactly: case-sensitive, after trimming.
- **REQ-POS-3.3** Security ticker may be null. When non-null it cannot be whitespace only (post-trim), its length cannot exceed 20 characters, and no two Securities may share it (exact, case-sensitive comparison).
  - *Why optional:* some holdings have no public ticker, such as an institutional share class inside an employer plan. (2026-10-04)
- **REQ-POS-3.4** For each of the seven dimensions, a Security references at most one Dimension Value, and may reference none. A Dimension Value assigned to a Security's slot for a different dimension is rejected with a typed error naming the value and both dimensions.

## 4. Valid and invalid data states — Investment Account

- **REQ-POS-4.1** Investment Account name cannot be null or whitespace only (post-trim, per REQ-SYS-1.1), its length cannot exceed 100 characters, and no two Investment Accounts may share it (exact, case-sensitive comparison).
- **REQ-POS-4.2** Institution cannot be null or whitespace only (post-trim), and its length cannot exceed 100 characters.
- **REQ-POS-4.3** Account group cannot be null or whitespace only (post-trim), and its length cannot exceed 100 characters. It is a reporting label: reports group accounts whose labels match exactly.
- **REQ-POS-4.4** Tax treatment must be one of 'Taxable', 'TaxDeferred', 'Roth', 'Hsa'.
- **REQ-POS-4.5** An Investment Account has one or more owners, each an existing Person. The same Person may not appear twice among one account's owners.
- **REQ-POS-4.6** An Investment Account whose tax treatment is not 'Taxable' has exactly one owner.
  - *Why:* retirement accounts and health savings accounts are individual by law; only a taxable account may be held jointly. (2026-10-04)
- **REQ-POS-4.7** Active begin cannot be null; active end may be null. Both are Calendar Dates, and active end may not be earlier than active begin. An Investment Account is active on a date when active begin is on or before it and active end is null or on or after it. Both boundaries are inclusive.
- **REQ-POS-4.8** An Investment Account may be linked to one ledger Account. The linked Account must exist, be of type 'Asset' and have subtype 'Investment'; otherwise the operation fails with a typed error naming the account code and what is wrong.
- **REQ-POS-4.9** A ledger Account may be linked to at most one Investment Account or Property, never to two of them, whether both are Investment Accounts, both are Properties, or one of each. A second link is rejected with a typed error naming the account code and the record already linked to it.
  - *Why:* net worth replaces a linked account's balance with the market value of what it is linked to (REQ-RPT-8.2). An account standing for two things would have its balance removed once and two values added. (2026-10-04)

## 5. Valid and invalid data states — Holding

- **REQ-POS-5.1** A Holding references an existing Investment Account and an existing Security. No two Holdings may reference the same Investment Account and Security.
- **REQ-POS-5.2** A Holding in an account whose tax treatment is 'Taxable' has a basis method, one of 'AverageCost' or 'SpecificLot'. A Holding in an account of any other tax treatment has no basis method. A Holding that breaks either rule is rejected with a typed error naming the account, the security and the account's tax treatment.
  - *Why per holding:* an institution elects the basis method for each security in an account, not for the account. One taxable account can hold average-cost funds and specific-lot funds side by side. Outside a taxable account, no sale is a taxable event, so basis method means nothing. (2026-10-04)
- **REQ-POS-5.3** Changing an Investment Account's tax treatment is rejected when a Holding in that account would then break REQ-POS-5.2. The error names every such Holding's security.
- **REQ-POS-5.4** Changing an Investment Account's owners is rejected when the result would break REQ-POS-4.5 or REQ-POS-4.6, including when a tax-treatment change in the same operation would.

## 6. Valid and invalid data states — Account Snapshot

- **REQ-POS-6.1** An Account Snapshot references an existing Investment Account and carries a snapshot date (a Calendar Date), a provenance, an optional contribution basis, and zero or more lines. No two Account Snapshots may share an Investment Account and snapshot date.
- **REQ-POS-6.2** Provenance must be one of 'Reported' (recorded from an institution's report in the ordinary course) or 'Imported' (history loaded from an earlier system, not checked against an institution's report when it was loaded).
  - *Why:* imported history is useful for seeing growth over time, but nothing later may mistake it for a figure that was checked at the time. (2026-10-04)
- **REQ-POS-6.3** The snapshot date must fall within the Investment Account's active period (REQ-POS-4.7) and cannot be later than the current date (the calendar date of the operation's initiation instant, REQ-SYS-3.4). Either failure names the account and the date.
- **REQ-POS-6.4** Contribution basis may be null. When non-null it must be a valid Money value that is not negative, and the Investment Account's tax treatment must be 'Roth'; a contribution basis on any other account is rejected with a typed error naming the account.
  - *Why:* contributions to a Roth account can be withdrawn before retirement age without tax or penalty; earnings cannot. Recording the contribution basis separately from the account's value keeps that distinction. (2026-10-04)
- **REQ-POS-6.5** Each line references a Holding of the snapshot's own Investment Account, given by the Security's name. A Security with no Holding in that account fails the operation with a typed error naming the account and the security. The system must not create the Holding.
  - *Why:* a Holding carries a basis method that only the operator can decide (REQ-POS-5.2). An unexpected security in a report is something to look at, not to absorb. (2026-10-04)
- **REQ-POS-6.6** A Security appears on at most one line of an Account Snapshot.
- **REQ-POS-6.7** Each line carries a quantity (a Quantity greater than zero), a price (a Price), a market value (a Money value that is not negative), and a reported cost basis (a Money value that is not negative, or null when the institution reports none).
  - *Why greater than zero:* a holding not held is omitted from the snapshot, not listed at zero (design note above). (2026-10-04)
- **REQ-POS-6.8** For each line, the product of quantity and price (REQ-QP-3.5) and the market value may differ by at most 0.05 USD. A line that differs by more is rejected with a typed error naming the account, the snapshot date, the security, the product and the market value.
  - *Why:* institutions' own figures agree to within a cent in practice. A larger gap means a column misread or a value carried over from another week while the shares moved, and a snapshot is evidence that must not hold a contradiction. (2026-10-04)

## 7. Account Snapshot operations

- **REQ-POS-7.1** The system must provide a means to record one or more Account Snapshots in a single atomic operation. An Investment Account and snapshot date may appear at most once in one operation; a repeat is rejected with a typed error naming both.
- **REQ-POS-7.2** Recording an Account Snapshot for an Investment Account and date that already has one replaces the existing snapshot entirely: its provenance, contribution basis and every line. This is a deliberate exception to REQ-SYS-6.1, made under REQ-SYS-6.1.1.
  - *Why:* re-recording a week's figures is how a corrected or re-downloaded report is applied, and the institution's latest statement of a date's figures is the authority. Rejecting it would make every correction a delete followed by a record. (2026-10-04)
- **REQ-POS-7.3** Recording returns each Account Snapshot as stored, with whether it replaced an existing one.
- **REQ-POS-7.4** The system must provide a means to delete the Account Snapshot for a given Investment Account and date. A date with no snapshot fails with a typed not-found error naming the account and date.
- **REQ-POS-7.5** The system must provide a read-only means to fetch the Account Snapshot for a given Investment Account and date, and a read-only means to list an Investment Account's snapshot dates, with each one's provenance, between two Calendar Dates inclusive, in date order.

## 8. Holdings as of a date

The one read that answers "what was held, and what was it worth, on this date", across every
account. Reports (Reporting §8, §9) are built on it, and it is the extract anything that plans
from the data consumes.

- **REQ-POS-8.1** The system must provide a read-only means to fetch holdings as of a Calendar Date. For every Investment Account active on that date (REQ-POS-4.7) that has at least one Account Snapshot dated on or before it, the result includes that account's latest such snapshot. An account active on the date with no snapshot on or before it is not included.
  - *Why active, not merely snapshotted:* an account that has ended still has a last snapshot. Ending the account is what takes that last value out of every later figure. (2026-10-04)
- **REQ-POS-8.2** Each included account carries: name, institution, account group, tax treatment, owners' names, the linked ledger Account's code and name when there is one, and the snapshot's date, provenance and contribution basis.
  - *Why the snapshot date:* an account whose latest snapshot is weeks older than the as-of date is shown as such, not passed off as current. (2026-10-04)
- **REQ-POS-8.3** Each line carries: the Security's name and ticker; the name of the Dimension Value it references in each of the seven dimensions, or nothing for a dimension with none; the Holding's basis method; and the line's quantity, price, market value and reported cost basis as recorded.
- **REQ-POS-8.4** Accounts are ordered by name; lines within an account by Security name.

## 9. Valid and invalid data states — Property

- **REQ-POS-9.1** Property name cannot be null or whitespace only (post-trim, per REQ-SYS-1.1), its length cannot exceed 100 characters, and no two Properties may share it (exact, case-sensitive comparison).
- **REQ-POS-9.2** Use must be one of 'PrimaryResidence' or 'Rental'.
- **REQ-POS-9.3** A Property has one or more owners, each an existing Person. The same Person may not appear twice among one Property's owners.
- **REQ-POS-9.4** Acquisition date cannot be null; disposal date may be null. Both are Calendar Dates, and disposal date may not be earlier than acquisition date. A Property is owned on a date when its acquisition date is on or before it and its disposal date is null or later than it.
  - *Why the disposal date is not inclusive:* on the day a property is sold it is no longer held at the end of the day, and the sale proceeds are in the ledger instead. (2026-10-04)
- **REQ-POS-9.5** Purchase basis cannot be null and must be a valid Money value greater than zero.
- **REQ-POS-9.6** At most one Property whose use is 'PrimaryResidence' may be owned on any one date. A create or update that would make two primary residences owned on the same date is rejected with a typed error naming both.
  - *Why:* investable wealth excludes the primary residence (REQ-RPT-8.4). Two of them on one date would mean one is mislabelled. (2026-10-04)
- **REQ-POS-9.7** A Property may be linked to one ledger asset Account. The linked Account must exist, be of type 'Asset' and have subtype 'FixedAsset'; otherwise the operation fails with a typed error naming the account code and what is wrong. REQ-POS-4.9 applies.
- **REQ-POS-9.8** A Property may be linked to zero or more ledger mortgage Accounts. Each must exist and be of type 'Liability'; otherwise the operation fails with a typed error naming the account code. A ledger Account may be a mortgage Account of at most one Property; a second is rejected with a typed error naming the account code and the Property already linked.

## 10. Valid and invalid data states — Valuation

- **REQ-POS-10.1** A Valuation references an existing Property and carries a valuation date (a Calendar Date), a value (a valid Money value greater than zero) and a basis. No two Valuations of one Property may share a valuation date.
- **REQ-POS-10.2** Valuation basis cannot be null or whitespace only (post-trim), and its length cannot exceed 100 characters. It says how the value was arrived at (an online estimate, an appraisal, a sale price).
- **REQ-POS-10.3** The valuation date must fall on or after the Property's acquisition date and, when the Property has a disposal date, on or before it, and cannot be later than the current date (the calendar date of the operation's initiation instant). Either failure names the Property and the date.
- **REQ-POS-10.4** A Property's value on a date is the value of its latest Valuation dated on or before that date. When it has none, its value is its purchase basis.
  - *Why:* the purchase price is the first known value of a property. A property with no valuation yet is worth what was paid for it, not nothing. (2026-10-04)

## 11. Maintenance operations

- **REQ-POS-11.1** The system must provide a means to create a Dimension Value, to rename one (addressed by dimension and current name), and a read-only means to list the values of one dimension ordered by name.
- **REQ-POS-11.2** The system must provide a means to create a Security; to update its name, ticker (set or clear) and each dimension's value (set or clear), the Security addressed by its current name; and a read-only means to list every Security ordered by name, each with its ticker and the name of its value in each dimension. Dimension Values are given by dimension and name.
- **REQ-POS-11.3** The system must provide a means to create an Investment Account; to update its name, institution, account group, tax treatment, owners (given as the complete new set), active begin, active end (set or clear) and linked ledger Account (set or clear), the account addressed by its current name; and a read-only means to list every Investment Account ordered by name with everything REQ-POS-8.2 lists for an account except the snapshot fields, plus its active begin and active end. Owners are given by Person name and the ledger Account by code.
- **REQ-POS-11.4** An update to an Investment Account's active period is rejected when any of its Account Snapshots would then fall outside it. The error names the earliest and latest offending snapshot dates.
- **REQ-POS-11.5** The system must provide a means to create a Holding (Investment Account and Security by name, and basis method), to change a Holding's basis method subject to REQ-POS-5.2, and a read-only means to list Holdings, optionally limited to one Investment Account, ordered by account name and then Security name, each with its basis method.
- **REQ-POS-11.6** The system must provide a means to create a Property; to update its name, use, owners (given as the complete new set), acquisition date, disposal date (set or clear), purchase basis, linked ledger asset Account (set or clear) and mortgage Accounts (given as the complete new set), the Property addressed by its current name; and a read-only means to list every Property ordered by name, with its owners' names and its linked Accounts' codes and names.
- **REQ-POS-11.7** An update to a Property's acquisition or disposal date is rejected when any of its Valuations would then fall outside the range REQ-POS-10.3 allows. The error names the earliest and latest offending valuation dates.
- **REQ-POS-11.8** The system must provide a means to record a Valuation. Recording a Valuation for a Property and date that already has one replaces its value and basis; this is a deliberate exception to REQ-SYS-6.1, made under REQ-SYS-6.1.1, for the reason given at REQ-POS-7.2. The system must provide a means to delete a Valuation, and a read-only means to list a Property's Valuations in date order.
- **REQ-POS-11.9** A Dimension Value, Security, Investment Account or Property name given to any operation that does not match an existing record fails with a typed error naming the kind of record and the name. A Dimension Value is matched within the dimension given.

## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
|  |  |  |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
|  |  |  |
