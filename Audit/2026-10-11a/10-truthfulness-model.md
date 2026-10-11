# code-truthfulness-business-domains

## POS-LOT-SYS31-1 — contradiction
- **Location:** DbMigration/Scripts/202610101000-CreateAccountSnapshotLotTable.sql; Src/Business.FinancialServices.Positions/AccountSnapshotLot.fs:15-22; Specs/Definitions.md:54-55 (Component rows); REQ-SYS-3.1
- **Summary:** positions.account_snapshot_lot has no created_at/modified_at columns, and the AccountSnapshotLot type has no timestamps either. Lots are not on Definitions.md's closed list of component rows, so under the Entity definition REQ-SYS-3.1 applies to them.
- **Resolution:** fix-spec
- **Prior ruling:** Disposition 2026-10-04a #097 (canonical for #126/#169/#180/#194) settled this for the slice 1 tables by listing each one in Definitions.md. Lots did not exist then, so that ruling does not cover them. The same remedy applies.

Definitions.md 'Component rows' is the only exemption from REQ-SYS-3.1 for user-written rows. It names its tables one by one: positions.account_snapshot_line, investment_account_owner, property_owner, property_asset_account and property_mortgage_account (added 2026-10-05 under disposition 2026-10-04a #097). The slice 2 spec commit (95df0e4) touched Definitions.md but only to add 'Pre-ledger date'. It did not add positions.account_snapshot_lot to the list. The lot migration says 'A lot is a component of its line, like the line is of its snapshot: no timestamps', and AccountSnapshotLot.fs has no createdAt/modifiedAt. Code and schema therefore treat lots as component rows, but the governing definition does not. A user action (recording a snapshot) inserts lot rows, so by Definitions.md's Entity litmus test they are entities, and REQ-SYS-3.1 says every persisted entity carries both timestamps. Lots do behave like component rows: the code writes and removes them only together with their line (AccountSnapshotOrchestration.recordOne deletes them via AccountSnapshotLot.deleteByAccountSnapshot and re-inserts them) and never updates them in place. The fix is most likely to the definition, not the schema.

**Action:** Add positions.account_snapshot_lot (part of an account snapshot line) to the Component rows list in Specs/Definitions.md, as #097 did for snapshot lines.

**Why:** Specs outrank code. The schema relies on an exemption that the definition does not grant, so the next auditor or agent reading REQ-SYS-3.1 against the definition will either re-raise this or add timestamps for nothing. #097 settled this exact question for snapshot lines by naming each table, which makes the list closed, not illustrative.

---

## POS-LOT-BASIS-1 — missing-requirement
- **Location:** Specs/Behavioral/Positions.md design note 'lots are reported, not derived'; REQ-POS-11.5; REQ-POS-8.3; Src/Business.FinancialServices.Positions/Holding.fs (update, HoldingFieldUpdates.basisMethodUpdate)
- **Summary:** The spec reads a lot's recorded cost through the Holding's current basis method, and REQ-POS-11.5 lets that basis method change with no guard. A change from SpecificLot to AverageCost, or the reverse, silently changes what every previously recorded lot's cost basis means.
- **Resolution:** dan-decides

The design note says a lot's 'cost is read through the Holding's basis method: under SpecificLot the lot's reported cost basis is that lot's cost; under AverageCost the institution prints the pooled average on every lot, and the recorded figure is that'. The Holding stores one basis_method with no history (positions.holding; Holding.fs update writes basis_method in place). REQ-POS-11.5 allows changing it subject only to REQ-POS-5.2. REQ-POS-8.3 (holdings as of a date) returns each line's lots next to the Holding's current basis method, not the method in force on the snapshot date. Example: a taxable Holding is SpecificLot and its 2026-03 snapshots carry lots with per-lot costs. The operator then switches it to AverageCost, which institutions allow. Holdings-as-of for 2026-03 now pairs those lots with AverageCost, so a consumer following the design note reads per-lot costs as a pooled average. No error is raised and nothing in the data shows the change. Lots are the reason slice 2 exists (Dan: 'so the simulator can work out real gains'), and the spec stresses that a snapshot is evidence that must not hold a contradiction (REQ-POS-6.8, 6.12). REQ-POS-5.5 already refuses a similar change (leaving Roth while snapshots carry a contribution basis), but no rule covers a basis-method change while lots exist.

**Action:** Dan decides one of: (a) add a REQ refusing a basis-method change while any snapshot line of that Holding carries a Lot, naming the earliest and latest such snapshot dates (as REQ-POS-5.5 does); (b) record the basis method on the snapshot line or lot when it is recorded; or (c) state in the design note that a lot's cost is read through the basis method in force on its snapshot date and that a change applies only going forward, with its consequences.

**Why:** Recorded lot costs are verbatim evidence whose meaning depends on a mutable attribute of another record. Without a rule, an ordinary operator action silently changes what historical lots mean to the simulator that consumes them, with no error and no trace. This is the silent-wrong-data class the spec guards against elsewhere (REQ-POS-5.5, 6.8, 6.12).

---

