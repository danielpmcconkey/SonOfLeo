# customer-hobson

## CUST-POS-1 — customer-gap
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.2, REQ-RPT-8.5; Src/Business.CrossDomainOrchestration/NetWorth.fs:99-112,122
- **Summary:** Net worth silently leaves out a linked ledger account's balance even when nothing from Positions takes its place, and it never shows which accounts it left out.
- **Resolution:** dan-decides

REQ-RPT-8.2 counts 'every Asset account that is not linked to an Investment Account or a Property'. The exclusion has no conditions. NetWorth.fs:99-102 builds linkedAssetIds from InvestmentAccount.fetchAll and Property.fetchAll, which return every record. They are not filtered to accounts active on the date with a snapshot on or before it, or to properties owned on the date. Market value, though, comes only from fetchHoldingsAsOf (line 122). That read includes an account only when it is active on the date AND has a snapshot on or before it (REQ-POS-8.1; HoldingsAsOf.fs:152-154). Properties come only from ownedProperties (line 103). So the ledger balance disappears and nothing replaces it whenever: (a) an Investment Account is linked but has no snapshot yet on or before the date. Example: Hobson creates a new brokerage account with its ledger link, and the funding transfer posts the week before the first snapshot is recorded. Net worth then drops by the whole transfer. (b) Net worth is run for a date before the account's first recorded or imported snapshot. (c) The account is outside its active period but its ledger account still carries a balance. (d) A linked Property has been disposed of, or is not yet acquired, while its asset account still carries cost. The opposite mistake is equally silent. An Asset/'Investment' ledger account that the operator forgot to link is counted at cost, and its market value is counted again from Positions. The NetWorthReturnRow contract (ReportsContracts.fs:189-203) lists counted asset accounts, investment accounts and properties. It does not list the linked accounts that were left out or their balances, so the operator cannot see either kind of misstatement in the output. Net worth is the one report Dan reads every week (cli-requirements-from-leobloom-usage.md §1, 'report net-worth --json'). The NetWorth tests cover linked accounts only where a snapshot exists (NetWorth.fs tests 'an Asset account linked to an Investment Account is absent...').

**Action:** Dan decides one of two options. (1) Amend REQ-RPT-8.2/8.5 so a linked account is left out only when its Investment Account is in the holdings as of the date, or its Property is owned on the date. Otherwise it is counted at its ledger balance. (2) Keep the exclusion, but have REQ-RPT-8.5 return each linked account that was left out (code, name, balance, and what it is linked to, or 'nothing counted in its place'). That makes a gap or a double count visible in the output.

**Why:** Net worth is the headline number of the weekly routine, and it is what the planning engine's 'current financial state' extract will start from. A figure that can be silently wrong by the size of a whole account, with nothing in the output to show it, breaks the 'easy and assured' standard. Hobson would find it only by reconciling by hand against the old system.

---

## CUST-POS-2 — customer-gap
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-9.2; Specs/Behavioral/Positions.md REQ-POS-8.2 (its 'Why the snapshot date'); Src/Business.CrossDomainOrchestration/InvestmentWealthHistory.fs:76-90; ReportsContracts.fs:219-232
- **Summary:** Investment wealth history presents stale snapshots as current month-end values, and nothing in the result shows it. That is the outcome REQ-POS-8.2's rationale exists to prevent.
- **Resolution:** dan-decides

REQ-POS-8.2 carries the snapshot date 'so an account whose latest snapshot is weeks older than the as-of date is shown as such, not passed off as current.' Wealth history computes each month-end point from fetchHoldingsAsOf (InvestmentWealthHistory.fs:81). It then reduces every account to (group, marketValue) (groupedValues, lines 57-74). WealthPointReturnRow is { monthEnd; totals; total }, and WealthGroupTotalReturnRow is { group; marketValue }. Neither carries a snapshot date, provenance, or any indication that a value is older than the month-end. So an account still active whose snapshots stopped carries its last value into every later point and appears as a flat line. Causes include a parser breaking for one institution, or an account that was closed without its active end being set. The account's earliest point after a gap in imported history looks the same way. REQ-POS-6.2's provenance distinction (Imported vs Reported) is also dropped from the series. This is the monthly growth series Dan says the retirement-planning engine and allocation views are built on.

**Action:** Dan decides whether REQ-RPT-9.2 should carry, at each point, the oldest snapshot date contributing to it, or list the accounts whose latest snapshot is more than N days older than the month-end. Dan also decides whether each point should say if any contributing snapshot is 'Imported'.

**Why:** A series of month-end values is only as good as its least current contributor. The spec already decided that staleness must be visible in the per-date read (REQ-POS-8.2). Losing that in the multi-year view, which is built for long-horizon analysis, means a broken feed shows up as a market plateau instead of a data gap.

---

## DELTA-POS-1 — statement-delta
- **Location:** Dan's statement ('This slice implements all the CRUD operations'); Specs/Behavioral/Positions.md §11; Specs/Behavioral/Person.md 'Deletion'; Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs:305-456; HobsonsNotes/plan-positions-2026-10-04.md §3
- **Summary:** Only Account Snapshots and Valuations can be deleted. Persons, Dimension Values, Securities, Investment Accounts, Holdings and Properties have create, update and list only.
- **Resolution:** dan-decides

Dan's statement says the slice 'implements all the CRUD operations'. The route table (PositionsRoutes.fs:305-456) has Delete only for AccountSnapshot and Valuation. DimensionValue has Create/Rename/List; Security, InvestmentAccount and Property have Create/Update/List; Holding has Create/UpdateBasisMethod/List/FetchAsOf; Person has Create/Update/List. This is deliberate in the spec: Person.md says so outright, Positions §11 provides no 'means to delete', and the plan says 'There is no delete for a Person, Dimension Value, Security, Investment Account, Holding or Property... Don't add one.' Even so, Dan's mental model differs from the repo. For the operator, a mistake can only be corrected around: a Holding created in the wrong account, or a Security or Investment Account created by mistake, stays in every List forever. Neutralising a mistaken Investment Account or Property depends on date tricks: active end equal to active begin, or a disposal date equal to the acquisition date. That works for a Property because 'owned' is half-open, but an Investment Account stays active for its one inclusive day. Under the standing 'no direct DB writes' policy, the only other route is raw SQL by Dan.

**Action:** Dan confirms that no-delete for these six entities is intended, and corrects the 'all the CRUD operations' wording. Or he specifies deletes, at least for a Holding or Security that no snapshot references.

**Why:** The statement of position is what later audits and agents take as ground truth. If it says full CRUD, a mistake gets handled as though delete were available. In practice the CLI's only remedy is a workaround or a raw-SQL exception, which is the situation cli-requirements-from-leobloom-usage.md warns about ('A complete CLI is what makes the policy livable').

---

## DELTA-JSON-1 — statement-delta
- **Location:** Dan's statement ('App.Utility.Json.fromJson... now names the field a payload actually left out'); Src/App.Utility/Json.fs missingFieldMessage; Src/Ui.InterfaceBridge/InterfaceContracts/PositionsContracts.fs AccountSnapshotRecordInput/AccountSnapshotInput
- **Summary:** The fromJson correction only covers the top-level record. In nested records, such as every field of the weekly AccountSnapshot Record payload, the error can still name the wrong field.
- **Resolution:** dan-decides

missingFieldMessage<'T> corrects the library message only when it starts with 'Missing field for record type {typeof<'T>.FullName}', and it checks only the root object's property names (document.RootElement.EnumerateObject()). The implementer's report (plan-positions-2026-10-04.md §8.4) says: 'A missing field in a nested record (for example, a snapshot line) still gets the library's message.' The most frequent Positions write is AccountSnapshot Record. Its top-level contract, AccountSnapshotRecordInput, is just { snapshots }, so every field a parser can leave out sits in a nested record. AccountSnapshotInput declares the option contributionBasis before lines. A parser that sends contributionBasis: null, which is normal for every non-Roth account, and leaves out lines (for example, on an empty account) gets a message naming contributionBasis. That is the exact mislabelling the fix was meant to remove. Dan's statement describes the fix as general; it is not.

**Action:** Dan either accepts the top-level-only scope and corrects his understanding, or extends the correction to nested records (resolve the failing record type from the library message and walk the JSON path).

**Why:** Scripts drive this CLI more than humans do, and 'machine-friendly errors' is a standing lesson. The external parsers that feed snapshots are exactly the callers who will hit a misnamed field, and they will be debugging against the most nested payload in the system.

---

