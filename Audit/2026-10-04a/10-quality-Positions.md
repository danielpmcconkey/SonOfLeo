# positions-spec-quality-auditor

## POS-CONTRIB-1 — contradiction
- **Location:** Specs/Behavioral/Positions.md REQ-POS-6.4, REQ-POS-11.3, REQ-POS-5.3 (cf. REQ-POS-11.4, REQ-POS-11.7); Specs/Behavioral/SystemWide.md REQ-SYS-2.1
- **Summary:** A tax-treatment update that REQ-POS-11.3 allows can leave already-stored Account Snapshots carrying a contribution basis on a non-Roth account, which REQ-POS-6.4 declares illegal, and the spec gives no rule for that case.
- **Resolution:** dan-decides

REQ-POS-6.4 says a non-null contribution basis requires the account's tax treatment to be 'Roth'. REQ-POS-11.3 allows the tax treatment to be updated. The spec spells out what happens to dependent records for every other update that could make them illegal: REQ-POS-5.3 (tax treatment versus Holdings' basis method), REQ-POS-11.4 (active period versus snapshot dates) and REQ-POS-11.7 (property dates versus Valuations). It says nothing about tax treatment versus snapshots' contribution basis. REQ-SYS-2.1 says no operation may persist or return an entity in an illegal data state. The code follows the gap. InvestmentOrchestration.fs (update path, around lines 382-413) checks owners (around line 388) and Holdings (confirmHoldingsAllowTaxTreatment) but never checks snapshots. Contribution basis is only checked when a snapshot is recorded (AccountSnapshotOrchestration.confirmContributionBasisAllowed, around line 70). Reading a snapshot back (AccountSnapshotHeader.fs, around lines 95-107) does not check it against the account's treatment. Concrete case: a Roth account has a snapshot with contribution basis 5,000.00, and its tax treatment is changed to 'TaxDeferred'. The update succeeds. From then on, holdings-as-of (REQ-POS-8.2) and net worth (REQ-RPT-8.5) report a contribution basis on a TaxDeferred account.

**Action:** Dan to decide which rule holds, then add it to Positions.md. Either (a) a REQ-POS-5.3 sibling: changing tax treatment away from 'Roth' is rejected while any snapshot of the account carries a contribution basis, and the error names the offending snapshot dates; or (b) amend REQ-POS-6.4 so it is checked only when a snapshot is recorded.

**Why:** The spec handles every other dependent-record case explicitly, so the missing one reads as an oversight rather than a choice. As written, a permitted operation produces stored data the spec itself calls invalid, which breaks REQ-SYS-2.1. The contribution basis is the figure REQ-POS-6.4's rationale says must stay reliable (the withdrawable amount), so a stray one on a non-Roth account would mislead.

---

## POS-ENTITY-1 — contradiction
- **Location:** Specs/Behavioral/Positions.md §1 (Account Snapshot lines), REQ-POS-6.1, REQ-POS-7.2; Specs/Definitions.md 'Entity'; Specs/Behavioral/SystemWide.md REQ-SYS-3.1; DbMigration/Scripts/202610041020-CreatePositionsTables
- **Summary:** Account Snapshot lines are entities under the Definitions.md litmus test, but they have no created/modified timestamps and no definition exempts them, which contradicts REQ-SYS-3.1.
- **Resolution:** dan-decides

Definitions.md 'Entity' says: 'does any user action ever insert or update a row? Yes → entity.' Recording a snapshot (REQ-POS-7.1, 7.2) inserts snapshot lines on the user's behalf. Definitions.md exempts three kinds of record from entity policies: Staged entry, Staged line, and Insert-only log records. Account Snapshot lines are not among them, and Positions.md never says lines are not entities. REQ-SYS-3.1 requires every persisted entity to carry created-at and modified-at. In the schema, positions.account_snapshot_line has no created_at or modified_at, while account_snapshot, holding, valuation and the other Positions entities do. The owner and mortgage link rows (investment_account_owner, property_owner, property_mortgage_account) are also inserted by user actions and have no timestamps either. Precedent DB-STAGE-1 overruled a timestamp finding only because Definitions.md explicitly carves staged lines out. No such carve-out exists here, so that precedent does not match.

**Action:** Dan to decide: either add a Definitions.md entry (or a Positions.md design note) saying snapshot lines and owner/mortgage link rows are parts of their parent record rather than entities, as was done for Staged line; or add the timestamp columns.

**Why:** The Entity definition exists to decide which system-wide policies apply. Leaving these records unclassified means REQ-SYS-3.1 is silently not met for them. It also leaves the next domain's author unsure whether child rows of an aggregate need timestamps.

---

## POS-PEER-1 — statement-delta
- **Location:** Specs/Behavioral/Positions.md intro paragraph and REQ-POS-4.9; Src/Business.CrossDomainOrchestration/PositionsLedgerLinks.fs (confirmNotLinkedElsewhere), called from InvestmentOrchestration.fs (lines ~305, ~418) and RealEstateOrchestration.fs (lines ~133, ~209)
- **Summary:** Dan says investments and real estate never reference each other and only net worth combines them, but REQ-POS-4.9 states one link rule across both peers, and every investment and property link operation looks up the other peer's records.
- **Resolution:** dan-decides

Dan's statement: 'Inside of the positions domain, investments and real estate are unordered peers. Neither may reference the other, and net worth is the orchestration that combines them.' The spec intro agrees: 'neither depends on the other, and anything that combines them (net worth, investable wealth) is a cross-domain computation'. However, REQ-POS-4.9 still states one rule over both peers: 'A ledger Account may be linked to at most one Investment Account and to at most one Property.' Its 2026-10-04 amendment notes that one Account cannot be linked to both, because 4.8 requires the 'Investment' subtype, 9.7 requires 'FixedAsset', and subtype is immutable under REQ-AC-4.22. The code still treats links as one shared pool. PositionsLedgerLinks.confirmNotLinkedElsewhere calls both InvestmentAccount.fetchByLedgerAccountId and Property.fetchByLedgerAssetAccountId on every link, from both InvestmentOrchestration and RealEstateOrchestration (code comment: 'An Investment Account's ledger account and a Property's asset account share one pool'). So outside net worth there is a second orchestration that combines the peers, and by the amendment's own reasoning the cross-peer branch can never fire.

**Action:** Dan to decide whether REQ-POS-4.9 should be split into two per-peer uniqueness rules (an Investment Account's ledger account is unique among Investment Accounts; a Property's asset account is unique among Properties), so that only net worth combines the peers. Otherwise, amend the intro and his mental model to allow this shared link check.

**Why:** Dan's architecture statement and the spec's intro claim a strict peer separation that REQ-POS-4.9's wording and the implementation do not keep. Since the amendment shows the cross-peer case cannot occur, the coupling serves no purpose.

---

## POS-DELETE-1 — statement-delta
- **Location:** Specs/Behavioral/Positions.md §11 (REQ-POS-11.1, 11.2, 11.3, 11.5, 11.6); Specs/Behavioral/SystemWide.md §4 Deletion
- **Summary:** Dan describes the slice as implementing 'all the CRUD operations', but Positions.md gives no delete for Dimension Values, Securities, Investment Accounts, Holdings or Properties, and does not state the deletion decision that SystemWide §4 says each entity spec makes.
- **Resolution:** dan-decides

SystemWide.md §4: 'No system-wide deletion policy. Whether an entity's records may be hard-deleted is a domain-level decision, made in each entity's spec (for Accounts, see REQ-AC-5.1).' Person.md makes that decision explicitly ('There is no means to delete a Person ...'). Positions.md provides delete only for Account Snapshots (REQ-POS-7.4) and Valuations (REQ-POS-11.8). For Dimension Value (11.1), Security (11.2), Investment Account (11.3), Holding (11.5) and Property (11.6) it provides create, update and list, and says nothing about deletion either way. The registered routes match the spec: the DimensionValue, Security, InvestmentAccount, Holding and Property route lists in PositionsRoutes.fs have no Delete verb. Dan's statement says 'This slice implements all the CRUD operations for dealing with such data.' For five of the seven Positions entities, the D is neither provided nor ruled out. For example, a Holding created against the wrong Security cannot be removed and stays in the REQ-POS-11.5 Holdings list forever.

**Action:** Dan to state the deletion decision for each of the five entities in Positions.md, as Person.md does (for example, a 'Deletion.' paragraph saying they are not deletable because of their history, or adding delete operations). Then either update the 'all CRUD' description or add the missing operations.

**Why:** SystemWide §4 places the deletion decision in each entity spec. Without it, the spec does not say whether the missing delete is deliberate, and Dan's own description of what the slice does is wrong for most of its entities.

---

## POS-7.5-RANGE — ambiguity
- **Location:** Specs/Behavioral/Positions.md REQ-POS-7.5
- **Summary:** REQ-POS-7.5 lists snapshot dates 'between two Calendar Dates inclusive' but does not say what happens when the end date is before the begin date, while the comparable report range (REQ-RPT-9.1) rejects that case.
- **Resolution:** fix-spec

REQ-POS-7.5: '... a read-only means to list an Investment Account's snapshot dates, with each one's provenance, between two Calendar Dates inclusive, in date order.' There is no rule for an end date earlier than the begin date. REQ-RPT-9.1, the other Positions-driven date range, states 'The end date may not be earlier than the begin date.' Two reasonable developers would differ: one rejects the reversed range (following RPT-9.1 and the system's no-silent-surprise stance in REQ-SYS-6.1's rationale), the other returns an empty list. The implementation returns an empty list: AccountSnapshotOrchestration.listSnapshotDates passes the dates straight to AccountSnapshotHeader.fetchByInvestmentAccountBetween ('snapshot_date between @begin_date and @end_date'), and neither the route nor the orchestration checks the order. A caller who swaps the arguments is told the account has no snapshots in the range.

**Action:** Amend REQ-POS-7.5 to say either 'the end date may not be earlier than the begin date; otherwise the operation fails with a typed error' (matching REQ-RPT-9.1) or 'a reversed range returns no dates'.

**Why:** The two behaviours produce different results for the user (an error versus an empty answer that looks valid). The spec's own sibling range requirement chose to reject, so leaving this one open lets the two ranges behave inconsistently without anyone having decided it.

---

