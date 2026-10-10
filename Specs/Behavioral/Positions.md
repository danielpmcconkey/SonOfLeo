# Positions

Behavioral specs for the positions domain: what is held, by whom, and what it was worth on a
date. Positions has two sub-domains, **investments** (accounts at institutions holding
securities) and **real estate** (property). They are peers: neither depends on the other, and
anything that combines them (net worth, investable wealth) is a cross-domain computation
(Reporting §8, §9, §10). Positions also keeps **pre-ledger balances** (§15): what ledger
accounts held on dates before the ledger began, so net worth can reach back before it.
(Pre-ledger balances added 2026-10-10.)

**Design note — relationship to the ledger.** Positions are not ledger records. The ledger is
cash basis and carries an investment or a property, if at all, at cost or as the net of the
money that crossed into and out of it; Positions carries what it is worth. Realised gains and
losses are not ledger events. (Amended 2026-10-10.) No Positions operation creates, alters or reads a journal entry. A ledger account
that stands for an investment account or a property is *linked* to it, and the link has one
job: when net worth is computed, each linked account's cost balance is replaced by the market
value Positions holds, so nothing is counted twice (REQ-RPT-8.2). (2026-10-04; "each" amended
2026-10-05 — a Property may link several ledger asset accounts, REQ-POS-9.7.)

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

**Design note — lots are reported, not derived.** The open tax lots of a taxable holding are
recorded as the institution reports them, on the snapshot line (§6). The system does not work
out lots from purchases and sales. The institution's lots are what its tax reporting is computed
from, so lots derived here could only agree with them or be wrong. A lot's acquired date and
quantity are facts. Its cost is read through the Holding's basis method: under 'SpecificLot' the
lot's reported cost basis is that lot's cost; under 'AverageCost' the institution prints the
pooled average on every lot, and the recorded figure is that, not a cost the lot had of its own.
Holding periods, gains and which shares a sale consumes are computed by whatever plans from the
data, not here. (2026-10-10; replaces "not yet modelled", 2026-10-04.)

**Design note — activity is history, not a lot engine.** Each Investment Account's activity
(§12) is recorded as the institution reported it: contributions by source, rollovers, transfers,
dividends, fees, withdrawals, purchases and sales. Nothing derives holdings, lots or cost from
it. It answers what the snapshots cannot: how much of an account's growth was money put in, and
whether the shares that moved between two snapshots account for the difference between them
(§14). (2026-10-10)

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
- **Lot.** One open tax lot of a snapshot line, as the institution reported it: when it was
  acquired, how many units, and its reported cost basis. Only lines of taxable accounts carry
  lots. (2026-10-10)
- **Activity.** One thing the institution reported happening in an Investment Account on a date:
  a contribution, a purchase, a dividend, a fee. (2026-10-10)

References: an Investment Account references Persons (owners) and optionally a ledger Account; a
Holding references an Investment Account and a Security; an Account Snapshot references an
Investment Account; each snapshot line references a Holding of that account; each Lot belongs to
one snapshot line; an Activity references an Investment Account and, optionally, a Holding of
that account.

### Pre-ledger balances

- **Pre-ledger Balance.** What one ledger Account held on one date before the ledger began
  (§15). (2026-10-10)

References: a Pre-ledger Balance references a ledger Account.

### Real estate

- **Property.** A piece of real estate. Has owners, a use (primary residence or rental), an
  acquisition date and purchase basis, an optional disposal date, and optional links to its
  ledger asset accounts and its mortgage liability accounts.
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
- **REQ-POS-3.4** For each of the seven dimensions, a Security references at most one Dimension Value of that dimension, and may reference none. (Amended 2026-10-04 — the clause rejecting a value placed in another dimension's slot is struck: values are given by dimension and name (REQ-POS-11.2), so no operation can place one there.)

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
- **REQ-POS-4.9** A ledger Account may be linked to at most one Investment Account. A second link is rejected with a typed error naming the account code and the Investment Account already linked to it. (Amended 2026-10-04 — "or one of each" is struck: an Investment Account links only to an 'Investment' subtype (REQ-POS-4.8) and a Property only to a 'FixedAsset' subtype (REQ-POS-9.7), and an Account's subtype never changes, so one Account cannot be linked to both. Amended 2026-10-05 — the Property half moved to REQ-POS-9.7: investments and real estate are peers, and each states its own link rule.)
  - *Why:* net worth replaces a linked account's balance with the market value of what it is linked to (REQ-RPT-8.2). An account standing for two things would have its balance removed once and two values added. (2026-10-04)

## 5. Valid and invalid data states — Holding

- **REQ-POS-5.1** A Holding references an existing Investment Account and an existing Security. No two Holdings may reference the same Investment Account and Security.
- **REQ-POS-5.2** A Holding in an account whose tax treatment is 'Taxable' has a basis method, one of 'AverageCost' or 'SpecificLot'. A Holding in an account of any other tax treatment has no basis method. A Holding that breaks either rule is rejected with a typed error naming the account, the security and the account's tax treatment.
  - *Why per holding:* an institution elects the basis method for each security in an account, not for the account. One taxable account can hold average-cost funds and specific-lot funds side by side. Outside a taxable account, no sale is a taxable event, so basis method means nothing. (2026-10-04)
- **REQ-POS-5.3** Changing an Investment Account's tax treatment is rejected when a Holding in that account would then break REQ-POS-5.2. The error names every such Holding's security.
- **REQ-POS-5.4** Changing an Investment Account's owners is rejected when the result would break REQ-POS-4.5 or REQ-POS-4.6, including when a tax-treatment change in the same operation would.
- **REQ-POS-5.5** Changing an Investment Account's tax treatment away from 'Roth' is rejected while any of its Account Snapshots carries a contribution basis. The error names the account and the earliest and latest such snapshot dates. (2026-10-05)
  - *Why not clear the basis instead:* a contribution basis is a figure the institution reported, recorded verbatim (design note above). Clearing it would destroy evidence; keeping it on a non-Roth account would break REQ-POS-6.4. (2026-10-05)
- **REQ-POS-5.6** Changing an Investment Account's tax treatment away from 'Taxable' is rejected while any line of its Account Snapshots carries a Lot. The error names the account and the earliest and latest such snapshot dates. (2026-10-10)
  - *Why:* for the reason given at REQ-POS-5.5; keeping the Lots would break REQ-POS-6.9. (2026-10-10)

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
- **REQ-POS-6.9** A line may carry zero or more Lots. A line of an Investment Account whose tax treatment is not 'Taxable' that carries a Lot is rejected with a typed error naming the account and the security. (2026-10-10)
  - *Why only taxable:* outside a taxable account no sale is a taxable event, so when a unit was acquired means nothing (as REQ-POS-5.2 says of basis method). (2026-10-10)
- **REQ-POS-6.10** A line with no Lots records that the institution's lots were not supplied for that line on that date. It does not mean the holding has no lots. (2026-10-10)
  - *Why:* institutions report lots separately from positions, and not every week; a holding such as a money-market fund has none to report. The weekly figures must not wait on the lot report. (2026-10-10)
- **REQ-POS-6.11** Each Lot carries an acquired date (a Calendar Date no later than the snapshot date), a quantity (a Quantity greater than zero) and a reported cost basis (a Money value that is not negative, or null when the institution reports none). A Lot whose acquired date is later than the snapshot date is rejected with a typed error naming the account, the snapshot date, the security and the acquired date. (2026-10-10)
- **REQ-POS-6.12** When a line carries Lots, their quantities sum exactly to the line's quantity. A line whose Lots sum to anything else is rejected with a typed error naming the account, the snapshot date, the security, the sum of the Lots and the line's quantity. (2026-10-10)
  - *Why exact:* the lots and the position are the same shares counted twice by the institution. A difference means lots are missing or extra, and a snapshot is evidence that must not hold a contradiction (REQ-POS-6.8). (2026-10-10)
  - *Why cost is not checked the same way:* each lot's cost is rounded to the cent on its own, so their sum may honestly differ from the line's reported cost basis by a few cents. Both are recorded as reported. (2026-10-10)
- **REQ-POS-6.13** Two Lots of one line may share an acquired date, a quantity and a cost basis; each is recorded. A line's Lots are kept in the order supplied. (2026-10-10)
  - *Why:* two purchases on one day are two lots. Merging them, or dropping one as a duplicate, would change what the institution reported. (2026-10-10)

## 7. Account Snapshot operations

- **REQ-POS-7.1** The system must provide a means to record one or more Account Snapshots in a single atomic operation. An Investment Account and snapshot date may appear at most once in one operation; a repeat is rejected with a typed error naming both.
- **REQ-POS-7.2** Recording an Account Snapshot for an Investment Account and date that already has one replaces the existing snapshot entirely: its provenance, contribution basis and every line, with each line's Lots. (Lots added 2026-10-10) This is a deliberate exception to REQ-SYS-6.1, made under REQ-SYS-6.1.1.
  - *Why:* re-recording a week's figures is how a corrected or re-downloaded report is applied, and the institution's latest statement of a date's figures is the authority. Rejecting it would make every correction a delete followed by a record. (2026-10-04)
- **REQ-POS-7.3** Recording returns each Account Snapshot as stored, with whether it replaced an existing one.
- **REQ-POS-7.4** The system must provide a means to delete the Account Snapshot for a given Investment Account and date. A date with no snapshot fails with a typed not-found error naming the account and date.
- **REQ-POS-7.5** The system must provide a read-only means to fetch the Account Snapshot for a given Investment Account and date (with every line's Lots; amended 2026-10-10), and a read-only means to list an Investment Account's snapshot dates, with each one's provenance, between two Calendar Dates inclusive, in date order. The end date may not be earlier than the begin date; otherwise the listing fails with a typed error naming both dates. (End-date rule added 2026-10-05)

## 8. Holdings as of a date

The one read that answers "what was held, and what was it worth, on this date", across every
account. Reports (Reporting §8, §9) are built on it, and it is the extract anything that plans
from the data consumes.

- **REQ-POS-8.1** The system must provide a read-only means to fetch holdings as of a Calendar Date. For every Investment Account active on that date (REQ-POS-4.7) that has at least one Account Snapshot dated on or before it, the result includes that account's latest such snapshot. An account active on the date with no snapshot on or before it is not included.
  - *Why active, not merely snapshotted:* an account that has ended still has a last snapshot. Ending the account is what takes that last value out of every later figure. (2026-10-04)
- **REQ-POS-8.2** Each included account carries: name, institution, account group, tax treatment, owners' names, the linked ledger Account's code and name when there is one, and the snapshot's date, provenance and contribution basis.
  - *Why the snapshot date:* an account whose latest snapshot is weeks older than the as-of date is shown as such, not passed off as current. (2026-10-04)
- **REQ-POS-8.3** Each line carries: the Security's name and ticker; the name of the Dimension Value it references in each of the seven dimensions, or nothing for a dimension with none; the Holding's basis method; the line's quantity, price, market value and reported cost basis as recorded; and the line's Lots, each with its acquired date, quantity and reported cost basis, in the order supplied (REQ-POS-6.13). (Lots added 2026-10-10)
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
- **REQ-POS-9.7** A Property may be linked to zero or more ledger asset Accounts. Each must exist, be of type 'Asset' and have subtype 'FixedAsset'; otherwise the operation fails with a typed error naming the account code and what is wrong. The same ledger Account may not appear twice among one Property's asset Accounts; a repeat is rejected with a typed error naming the account code. A ledger Account may be an asset Account of at most one Property; a second is rejected with a typed error naming the account code and the Property already linked. (Amended 2026-10-05 — a set of asset Accounts, not one; and the at-most-one rule, formerly shared with Investment Accounts under REQ-POS-4.9, is stated here.)
  - *Why a set:* the ledger may carry one property across several FixedAsset accounts (the purchase, capitalised costs, depreciation). The Property's value replaces all of them in net worth (REQ-RPT-8.2); any left unlinked would be counted on top of it. (2026-10-05)
- **REQ-POS-9.8** A Property may be linked to zero or more ledger mortgage Accounts. Each must exist and be of type 'Liability'; otherwise the operation fails with a typed error naming the account code. The same ledger Account may not appear twice among one Property's mortgage Accounts; a repeat is rejected with a typed error naming the account code. A ledger Account may be a mortgage Account of at most one Property; a second is rejected with a typed error naming the account code and the Property already linked. (Repeat rule added 2026-10-05)

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
- **REQ-POS-11.4** An update to an Investment Account's active period is rejected when any of its Account Snapshots, or any of its Activities (REQ-POS-12.8), would then fall outside it. The error names the earliest and latest offending snapshot dates, and the earliest and latest offending activity dates. (Activities added 2026-10-10)
- **REQ-POS-11.5** The system must provide a means to create a Holding (Investment Account and Security by name, and basis method), to change a Holding's basis method subject to REQ-POS-5.2, and a read-only means to list Holdings, optionally limited to one Investment Account, ordered by account name and then Security name, each with its basis method.
- **REQ-POS-11.6** The system must provide a means to create a Property; to update its name, use, owners (given as the complete new set), acquisition date, disposal date (set or clear), purchase basis, asset Accounts (given as the complete new set; amended 2026-10-05, formerly one Account set or cleared) and mortgage Accounts (given as the complete new set), the Property addressed by its current name; and a read-only means to list every Property ordered by name, with its owners' names and its linked Accounts' codes and names.
- **REQ-POS-11.7** An update to a Property's acquisition or disposal date is rejected when any of its Valuations would then fall outside the range REQ-POS-10.3 allows. The error names the earliest and latest offending valuation dates.
- **REQ-POS-11.8** The system must provide a means to record a Valuation. Recording a Valuation for a Property and date that already has one replaces its value and basis; this is a deliberate exception to REQ-SYS-6.1, made under REQ-SYS-6.1.1, for the reason given at REQ-POS-7.2. The system must provide a means to delete a Valuation, and a read-only means to list a Property's Valuations in date order.
- **REQ-POS-11.9** A Dimension Value, Security, Investment Account or Property name given to any operation that does not match an existing record fails with a typed error naming the kind of record and the name. A Dimension Value is matched within the dimension given.
- **REQ-POS-11.10** The system must provide a means to delete a Holding, given by Investment Account and Security name. A Holding that any Account Snapshot line or any Activity references is not deleted; the operation fails with a typed error naming the account and the security. (2026-10-05; Activity added 2026-10-10)
- **REQ-POS-11.11** The system must provide a means to delete a Property, addressed by name. A Property that has any Valuation is not deleted; the operation fails with a typed error naming the Property. Deleting a Property removes its owners and ledger links with it. (2026-10-05)
- **REQ-POS-11.12** The system must not provide a user interface for hard-deleting a Dimension Value. (2026-10-05)
- **REQ-POS-11.13** The system must not provide a user interface for hard-deleting a Security. (2026-10-05)
- **REQ-POS-11.14** The system must not provide a user interface for hard-deleting an Investment Account. (2026-10-05)
  - *Why no delete for these three:* each is referenced by recorded history (Securities by Holdings and snapshot lines, Investment Accounts by snapshots), and an account that has closed is ended by its active end (REQ-POS-4.7), not removed. The rare mistaken record is corrected directly in the database. (2026-10-05)

## 12. Valid and invalid data states — Activity

- **REQ-POS-12.1** An Activity references an existing Investment Account and carries an activity date (a Calendar Date), a kind, a description, an optional source, an optional Security, an optional quantity, an optional price and an amount. (2026-10-10)
- **REQ-POS-12.2** Kind must be one of the following. The kind fixes which way the units of the named Security move (§14):
  - units in: 'Contribution', 'RolloverIn', 'TransferIn', 'Purchase', 'Reinvestment', 'AdjustmentIn';
  - units out: 'Withdrawal', 'RolloverOut', 'TransferOut', 'Sale', 'Fee', 'AdjustmentOut';
  - no units move: 'Dividend', 'Interest', 'CapitalGainDistribution'. (2026-10-10)
  - *Why a fixed set:* contributions against growth, and the units that moved between two snapshots, can only be totalled if every Activity says what kind of event it was. Institutions word the same event a dozen ways; the description keeps their words (REQ-POS-12.3). (2026-10-10)
  - *Why 'AdjustmentIn' and 'AdjustmentOut':* corporate actions (a split, a share-class change, a merger) move units with no money changing hands. A share-class change is units out of one Security and units in to another, which is two Activities. (2026-10-10)
- **REQ-POS-12.3** Description cannot be null or whitespace only (post-trim, per REQ-SYS-1.1), and its length cannot exceed 500 characters. It is the institution's own wording of the event. (2026-10-10)
- **REQ-POS-12.4** Source may be null. When non-null it cannot be whitespace only (post-trim), and its length cannot exceed 100 characters. It is the institution's name for where the money came from, such as an employee deferral or an employer match. (2026-10-10)
- **REQ-POS-12.5** The Security, when given, must be held in the Activity's own Investment Account: a Security with no Holding in that account fails the operation with a typed error naming the account and the security. The system must not create the Holding. (2026-10-10)
  - *Why:* as for snapshot lines (REQ-POS-6.5). A Holding long since sold still exists, so history can name it. (2026-10-10)
- **REQ-POS-12.6** An Activity of kind 'Purchase', 'Sale', 'Reinvestment', 'AdjustmentIn' or 'AdjustmentOut' must name a Security and carry a quantity. An Activity of kind 'Dividend', 'Interest' or 'CapitalGainDistribution' may name a Security (the one that paid it) and must not carry a quantity. Any other Activity may name a Security, and carries a quantity exactly when it does. An Activity that breaks this rule is rejected with a typed error naming the account, the activity date, the kind and what is missing or not allowed. (2026-10-10)
  - *Why a contribution may name no Security:* money paid into an account's cash, rather than straight into a fund, moves no units of anything the account holds. (2026-10-10)
- **REQ-POS-12.7** Quantity, when present, is a Quantity greater than zero. Price may be null; when non-null it is a Price, and it may be present only when a quantity is. Amount is a Money value that is not negative. (2026-10-10)
  - *Why unsigned:* the kind says which way units and money moved. Institutions sign their figures inconsistently with one another, and the supplier normalises them. (2026-10-10)
  - *Why quantity times price is not checked against amount:* an activity's amount can include commissions, fees or accrued interest that its quantity and price do not. (2026-10-10)
- **REQ-POS-12.8** The activity date must fall within the Investment Account's active period (REQ-POS-4.7) and cannot be later than the current date (the calendar date of the operation's initiation instant, REQ-SYS-3.4). Either failure names the account and the date. (2026-10-10)
- **REQ-POS-12.9** Two Activities of one account may agree in every field; each is recorded. Activities of one account on one date are kept in the order supplied. (2026-10-10)
  - *Why:* two identical reinvestments on one day are two events. The system never deduplicates Activity; the replacement rule (REQ-POS-13.2) is what keeps a re-supplied period from being counted twice. (2026-10-10)

## 13. Activity operations

- **REQ-POS-13.1** The system must provide a means to record the activity of one or more Investment Accounts in a single atomic operation. Each account's activity is supplied for a begin and an end Calendar Date, inclusive, with every Activity of that account dated within them. The end date may not be earlier than the begin date, and may not be later than the current date (the calendar date of the operation's initiation instant). An Activity dated outside its range is rejected with a typed error naming the account, the range and the date. (2026-10-10)
- **REQ-POS-13.2** Recording replaces every Activity of the account dated within the supplied range with the Activities supplied, which may be none. Activity dated outside the range is untouched. This is a deliberate exception to REQ-SYS-6.1, made under REQ-SYS-6.1.1. (2026-10-10)
  - *Why replace a range:* institutions give activity no stable identifier, and their exports overlap from one week to the next. Replacing a range with the institution's latest statement of it is how a period is supplied again without counting it twice, and how a correction is applied, for the reason given at REQ-POS-7.2. An empty range clears it. (2026-10-10)
- **REQ-POS-13.3** One operation may not supply two ranges for the same Investment Account that overlap. A repeat is rejected with a typed error naming the account and both ranges. (2026-10-10)
- **REQ-POS-13.4** Recording returns, for each account and range, how many Activities were removed and how many recorded. (2026-10-10)
- **REQ-POS-13.5** The system must provide a read-only means to list an Investment Account's Activities between two Calendar Dates inclusive, each with everything REQ-POS-12.1 lists, ordered by activity date and then in the order supplied (REQ-POS-12.9). The end date may not be earlier than the begin date; otherwise the listing fails with a typed error naming both dates. (2026-10-10)

## 14. Unit roll-forward

The check that the units which moved between two snapshots explain the difference between them.
A snapshot that silently omits a holding, or an activity the institution never reported, shows
up here.

- **REQ-POS-14.1** The system must provide a read-only means to roll an Investment Account's units forward between two of its Account Snapshots, given the account and the two snapshot dates. The first date must be earlier than the second, and each must have an Account Snapshot of that account; otherwise the computation fails with a typed error naming the account and the date or dates at fault. (2026-10-10)
- **REQ-POS-14.2** The result has one row for every Security that is on a line of either snapshot, or that an Activity of a units-moving kind (REQ-POS-12.2) names and that is dated after the first date and on or before the second. Each row carries: the Security's name; its quantity on the first snapshot (0 when absent); the total quantity of its units-in Activities and of its units-out Activities in that window; the expected quantity (first quantity plus units in, less units out); its quantity on the second snapshot (0 when absent); and the difference (second quantity less expected). Rows are ordered by Security name. (2026-10-10)
  - *Why after the first date and on or before the second:* a snapshot reports the account at the end of its date, so that date's activity is already in it. (2026-10-10)
  - *Why expected and difference are not Quantities:* a missing activity can make the expected quantity negative, and a difference has a sign. Both are exact decimals. (2026-10-10)
- **REQ-POS-14.3** A non-zero difference is returned as data, not raised as an error. The roll-forward makes no judgment. (2026-10-10)

## 15. Pre-ledger balances

The ledger holds nothing before its first fiscal period. A Pre-ledger Balance records what a
ledger Account held on a date before then, taken from older records, so net worth can be
computed for those dates (Reporting REQ-RPT-8.7).

- **REQ-POS-15.1** A Pre-ledger Balance references an existing ledger Account, by code, of type 'Asset' or 'Liability', and carries a balance date (a Calendar Date) and a balance (a valid Money value, which may be negative or zero). The balance is stated in the account's normal-balance direction: an asset's holding, a liability's amount owed. No two Pre-ledger Balances may share a ledger Account and balance date. A ledger Account of any other type fails the operation with a typed error naming the account code and its type. (2026-10-10)
  - *Why zero is allowed:* a loan paid off or an account closed before the ledger began is recorded with a zero balance on the day it ended. Without it, its last balance would count on every later date (REQ-RPT-8.7). (2026-10-10)
- **REQ-POS-15.2** The balance date must be earlier than the start date of the earliest fiscal period. When no fiscal period exists, the operation fails. Either failure is a typed error naming the account code and the date. (2026-10-10)
  - *Why:* a date never draws on both the ledger and these balances (Reporting REQ-RPT-8.7). A balance dated on or after the ledger began could only disagree with it. (2026-10-10)
- **REQ-POS-15.3** A ledger Account linked to an Investment Account (REQ-POS-4.8), or an asset Account of a Property (REQ-POS-9.7), may not carry a Pre-ledger Balance. The operation fails with a typed error naming the account code and what it is linked to. (2026-10-10)
  - *Why:* net worth counts what such an account stands for from Positions, at market value, and never reads its balance (Reporting REQ-RPT-8.2). A balance recorded for it would silently count for nothing. A Property's mortgage Accounts are not linked in this sense and may carry Pre-ledger Balances; that is how a property's equity is known before the ledger began. (2026-10-10)
- **REQ-POS-15.4** The system must provide a means to record one or more Pre-ledger Balances in a single atomic operation. A ledger Account and balance date may appear at most once in one operation; a repeat is rejected with a typed error naming both. Recording a Pre-ledger Balance for an Account and date that already has one replaces its balance; this is a deliberate exception to REQ-SYS-6.1, made under REQ-SYS-6.1.1, for the reason given at REQ-POS-7.2. Recording returns each Pre-ledger Balance as stored, with whether it replaced an existing one. (2026-10-10)
- **REQ-POS-15.5** The system must provide a means to delete the Pre-ledger Balance for a given ledger Account and date. A date with no Pre-ledger Balance fails with a typed not-found error naming the account code and date. (2026-10-10)
- **REQ-POS-15.6** The system must provide a read-only means to list Pre-ledger Balances, optionally limited to one ledger Account, between two Calendar Dates inclusive, ordered by account code and then balance date, each with the account's code and name. The end date may not be earlier than the begin date; otherwise the listing fails with a typed error naming both dates. (2026-10-10)

## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-POS-11.12 | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Dan, 2026-10-05 (audit 2026-10-04a #007) |
| REQ-POS-11.13 | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Dan, 2026-10-05 (audit 2026-10-04a #007) |
| REQ-POS-11.14 | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Dan, 2026-10-05 (audit 2026-10-04a #007) |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
|  |  |  |
