# code-truthfulness-business-domains

## POS-RECON-1 — enforcement-gap
- **Location:** Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs (reconstitute, ~L130-145; checkFigures ~L55-85); REQ-POS-6.7, REQ-POS-6.8, REQ-SYS-2.1
- **Summary:** AccountSnapshotLine.reconstitute does not re-check the row-local rules of REQ-POS-6.7/6.8, so a stored line with quantity zero, a negative market value or cost basis, or a product/market-value gap above 0.05 is returned as a valid line.
- **Resolution:** fix-code

The only line-level validation is AccountSnapshotLine.checkFigures. AccountSnapshotOrchestration.buildLine calls it on the write path only. The read path, reconstitute, runs Quantity.fromDecimal (which accepts 0), Price.fromDecimal, and Money.fromDecimal on market_value and reported_cost_basis (Money accepts values down to -9,999,999,999.99), then calls the infallible create. Nothing on the read path checks that quantity > 0 (POS-6.7), that market value >= 0 (POS-6.7), that cost basis >= 0 when present (POS-6.7), or that |quantity x price - market value| <= 0.05 (POS-6.8). The schema does not cover these either: account_snapshot_line has quantity numeric(16,6) and market_value/reported_cost_basis numeric(12,2) with no CHECK constraints, and the migration comment says 'every business rule (... the snapshot tolerance ...) is the application's'. All four rules can be decided from the row alone. REQ-SYS-2.1 requires every read-from-persistence operation to enforce the entity's legal data-state rules, and CompoundedLearnings/articles/coding/validation-layers.md layer 5 says 'reconstitute re-checks ... every cross-field constraint that can be decided from the row alone'. Contrast AccountSnapshotHeader.reconstitute and Valuation.reconstitute, which do re-run ContributionBasis.create and ValuationValue.create (sign rules) on read.

**Action:** In AccountSnapshotLine.reconstitute, after the component conversions, run the same checks as checkFigures (quantity positive, market value and cost basis non-negative, product within tolerance) and return a typed error when one fails. checkFigures needs account, date and security labels for its errors, so either reuse it with ID-based labels or add a row-local variant.

**Why:** A snapshot is 'evidence that must not hold a contradiction' (POS-6.8 rationale). If the read path does not re-validate, a row that was hand-edited or bulk-loaded outside the record path flows into holdings-as-of, net worth and wealth history without any error, which REQ-SYS-2.1 forbids.

---

## POS-RECON-2 — enforcement-gap
- **Location:** Src/Business.FinancialServices.Positions/InvestmentAccount.fs reconstitute (~L135-155); Src/Business.FinancialServices.Positions/Property.fs reconstitute (~L150-170); REQ-POS-4.5, REQ-POS-4.6, REQ-POS-9.3, REQ-SYS-2.1
- **Summary:** InvestmentAccount and Property reconstitute accept an owner list that breaks the specs: an empty list for either, and more than one owner on a non-Taxable investment account.
- **Resolution:** fix-code

Both fetches load the owner IDs into the row itself through a string_agg subquery (owner_ids), and reconstitute parses that with parseIdList, which returns [] when the aggregate is NULL. Neither reconstitute checks that the list is non-empty (POS-4.5 'one or more owners', POS-9.3 'one or more owners'). InvestmentAccount.reconstitute also does not check that a tax treatment other than Taxable has exactly one owner (POS-4.6), although tax_treatment and owner_ids are both in the same row. The schema cannot enforce either rule, because owners live in a child table with no minimum-cardinality constraint. The write path does enforce them in InvestmentOrchestration.resolveOwners and the NoChange owner-count check in updateInvestmentAccount. The read path is a gap against REQ-SYS-2.1 and validation-layers.md layer 5 ('reconstitute re-checks ... every cross-field constraint that can be decided from the row alone').

**Action:** In InvestmentAccount.reconstitute, return a typed error (for example PositionsInvestmentAccountHasNoOwners or PositionsInvestmentAccountOwnersNotAllowed) when the parsed owner list is empty or when tax treatment is not Taxable and there is more than one owner. In Property.reconstitute, return PositionsPropertyHasNoOwners when the owner list is empty.

**Why:** Holdings-as-of (POS-8.2 owners' names), the wealth-history 'owners' grouping (RPT-9.3), and every later retirement-planning consumer depend on ownership being valid. An ownerless or illegally joint retirement account read from storage would be reported without any error.

---

## POS-SYS31-1 — missing-requirement
- **Location:** DbMigration/Scripts/202610041020-CreatePositionsTables.sql (positions.account_snapshot_line; also investment_account_owner, property_owner, property_mortgage_account); Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs type AccountSnapshotLine; REQ-SYS-3.1; Specs/Definitions.md Entity / Insert-only log records
- **Summary:** positions.account_snapshot_line has no created_at/modified_at columns and the AccountSnapshotLine type has no timestamps, but under Definitions.md it is an entity and is not on the list of records exempt from REQ-SYS-3.1.
- **Resolution:** dan-decides

REQ-SYS-3.1 says 'Every persisted entity must carry a "created at" and a "modified at" timestamp.' Definitions.md defines an entity as a record type where 'any user action ever insert[s] or update[s] a row'. Recording a snapshot (POS-7.1/7.2) inserts account_snapshot_line rows, and the replace path deletes and re-inserts them. Definitions.md exempts only staged entries, staged lines, and the insert-only log records rule_match and staged_entry_audit (added 2026-10-03). account_snapshot_line is not among them. Every other line or child table in the system carries both columns: ledger.journal_entry_line, cashflow.payment_agreement_link, cashflow.payment. The same gap applies to the owner and mortgage-link tables (investment_account_owner, property_owner, property_mortgage_account), which user operations insert and replace and which carry no timestamps. Those can arguably be read as attributes of their parent rather than entities, but nothing in Definitions says so.

**Action:** Dan decides: either (a) add created_at/modified_at to account_snapshot_line (and to the owner and mortgage-link tables if they count as entities) and carry them on AccountSnapshotLine, or (b) amend Definitions.md to say that snapshot lines (and owner/link rows) are parts of their parent entity, or insert-only records, and are exempt from REQ-SYS-3.1.

**Why:** REQ-SYS-3.1 is waived from testing as 'too general', so only an audit will catch a new table that skips it. Positions is the first slice to add a line table without timestamps, and it sets a precedent the next slices will copy.

---

## POS-SYS61-1 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs renameDimensionValue (~L61-71) with confirmDimensionValueNameFree (~L34-46); REQ-SYS-6.1 (amended 2026-10-03), REQ-POS-11.1, REQ-POS-2.3
- **Summary:** Renaming a Dimension Value to the name it already has fails with PositionsDimensionValueAlreadyExists, but REQ-SYS-6.1 says an update that sets a field to its current value succeeds.
- **Resolution:** fix-code

renameDimensionValue calls confirmDimensionValueNameFree context dimension newName without passing the record's own ID. When newName equals the current name, fetchByDimensionAndName finds the record being renamed and the operation fails with 'A Dimension Value named "X" already exists in dimension D.' REQ-SYS-6.1 (amended 2026-10-03) says: 'A field set to the value it already holds counts as named, and the update succeeds. This is the system default for every update operation; an entity spec need not restate it.' POS-2.3 forbids two Dimension Values sharing a name, and a self-rename creates no second one. Every other rename-by-name path in this slice excludes the record itself: confirmSecurityNameFree (self), confirmInvestmentAccountNameFree (Some self), confirmPropertyNameFree (Some self), and PersonOrchestration.confirmNameFree (Some personId). The Dimension Value path is the only one that does not.

**Action:** Give confirmDimensionValueNameFree a self: DimensionValueId option parameter, as the Security/InvestmentAccount/Property versions have. Pass None from createDimensionValue and Some(id) from renameDimensionValue, and add a test that renaming to the current name succeeds.

**Why:** This contradicts a system-wide rule that was amended a day before the slice landed. It is also the only rename path in the slice that behaves differently from its siblings, so a caller re-sending an unchanged name gets a misleading 'already exists' error about the record it is editing.

---

