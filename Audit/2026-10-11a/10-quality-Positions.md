# spec-quality-positions

## DEF-POS-LOT-1 — contradiction
- **Location:** Specs/Definitions.md 'Component rows' (line 55); Specs/Behavioral/Positions.md §1 Lot, REQ-POS-6.9..6.13; DbMigration/Scripts/202610101000-CreateAccountSnapshotLotTable; Specs/Behavioral/SystemWide.md REQ-SYS-3.1
- **Summary:** A Lot is treated as a component row with no timestamps, but Definitions.md does not list it as one, so by the Entity definition it is an entity that REQ-SYS-3.1 requires to carry created/modified timestamps.
- **Resolution:** fix-spec

Definitions.md 'Component rows' lists, by table, exactly what is exempt from the Entity definition and REQ-SYS-3.1: account_snapshot_line, investment_account_owner, property_owner, property_asset_account and property_mortgage_account. It was not updated in slice 2. The new table positions.account_snapshot_lot is created by a user action (recording a snapshot, REQ-POS-7.1/7.2). Under the Entity litmus test ('does any user action ever insert or update a row? Yes -> entity') it is therefore an entity, and REQ-SYS-3.1 requires 'created at' and 'modified at'. The migration's header comment says 'A lot is a component of its line, like the line is of its snapshot: no timestamps', and the table has neither column. Definitions.md (authority 2) and the schema (authority 5) disagree. REQ-SYS-3.1 is waived from testing, so no test would catch the gap. The other two new slice-2 tables, investment_activity and pre_ledger_balance, carry both timestamps, which is consistent with treating them as entities.

**Action:** Add positions.account_snapshot_lot (part of an account snapshot line) to the Component rows entry in Specs/Definitions.md, with a date.

**Why:** Definitions.md exists so that whether a record is an entity decides which system-wide policies apply. Leaving a component-shaped table off the enumerated list makes the schema look like a REQ-SYS-3.1 violation to every later auditor, and leaves the exemption resting on a migration comment rather than on authority.

---

## AMB-POS-LOTCOST-1 — contradiction
- **Location:** Specs/Behavioral/Positions.md design note 'lots are reported, not derived' (lines 41-49); REQ-POS-11.5; REQ-POS-8.3; REQ-POS-5.2
- **Summary:** The meaning of a recorded Lot's cost depends on the Holding's basis method, which REQ-POS-11.5 lets the operator change at any time, so changing it alters how every historical Lot of that Holding is read.
- **Resolution:** dan-decides

The design note says a Lot's cost 'is read through the Holding's basis method: under SpecificLot the lot's reported cost basis is that lot's cost; under AverageCost the institution prints the pooled average on every lot, and the recorded figure is that, not a cost the lot had of its own.' The basis method lives on the Holding, not on the snapshot (REQ-POS-5.2). REQ-POS-11.5 lets it change, limited only by REQ-POS-5.2; HoldingOrchestration.changeHoldingBasisMethod has no other guard. REQ-POS-8.3, the extract the simulator consumes, returns 'the Holding's basis method' (its current value) next to every historical snapshot's Lots. Institutions do let a holder switch a security from average cost to specific lot going forward. After such a switch, Lots recorded while the Holding was AverageCost, whose figures are the pooled average, are presented under 'SpecificLot' and would be read as each lot's own cost. Nothing records which basis method was in effect on a snapshot date. This conflicts with the design note's premise ('A snapshot is evidence of what the institution said'). Two developers building the simulator's reader would diverge: one trusts the current basis method for all history, the other cannot tell which method applies.

**Action:** Dan to decide one of: (a) the basis method in effect is recorded per snapshot line (or per snapshot); (b) changing a Holding's basis method is rejected while any of its snapshot lines carries Lots; or (c) the design note states that the basis method applies retroactively to all recorded Lots and the consumer must accept that.

**Why:** Dan's stated purpose for slice 2 is that 'the simulator can work out real gains and long- vs short-term treatment' from the Lots. A Lot cost that changes meaning when mutable state elsewhere changes produces wrong gains without any error, which is the kind of fidelity loss the product vision says the ledger must avoid.

---

## CON-POS-FP-1 — contradiction
- **Location:** Specs/Behavioral/Positions.md REQ-POS-15.7; Specs/Behavioral/FiscalPeriodCrud.md REQ-FP-2.7 (line 30), REQ-FP-2.4
- **Summary:** REQ-FP-2.7 says without condition that missing periods are created, but REQ-POS-15.7, which FiscalPeriodCrud.md never mentions, rejects the ensure operation when a missing month would strand a Pre-ledger Balance.
- **Resolution:** fix-spec

REQ-FP-2.7 says: 'Missing periods are created open (REQ-FP-2.6). Existing periods, open or closed, are left unchanged... A start month later than the end month fails with a typed error.' The only failure it names is start > end. REQ-POS-15.7 rejects 'Creating a fiscal period whose start date is on or before the balance date of any Pre-ledger Balance.' The code applies this inside ensureFiscalPeriods (FiscalPeriodCreation.fs, fold over constructNewAndPersist, stopping at the first refusal). A test pins it: 'REQ-POS-15.7 ensuring fiscal periods exist for a range of months whose first missing month starts on or before a pre-ledger balance date is rejected ... and no fiscal period in the range is created'. So the whole ensure operation fails. It does not skip the refused month. FiscalPeriodCrud.md was not amended (per scout, its text is unchanged since the last audit), and none of its requirements cites §15 of Positions. A reader of the FP spec alone would implement REQ-FP-2.7 as always creating every missing month. A reader of REQ-POS-15.7 alone cannot tell whether a refused month fails the ensure operation or is skipped; the spec text supports both, since 'ensure' already tolerates months it does not create.

**Action:** Amend REQ-FP-2.7 (and REQ-FP-2.2/2.4 as needed) to cite REQ-POS-15.7 as an additional create-time rejection. State in either requirement that a refused month fails the whole ensure operation and nothing is created.

**Why:** A rule that constrains one entity's creation but lives only in another domain's spec leaves the owning spec's flat statement false. REQ-FP-2.7 is the requirement the weekly Saturday run relies on, so it should state every way it can fail.

---


