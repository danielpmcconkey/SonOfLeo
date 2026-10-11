# fsharp-ddd-reviewer

## FSDDD-S2-1 — idiom
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:17-22 (LedgerAccountBalance.balanceDate), :64-66 (NetWorthDate), :101-121 (balancesOn); Src/Ui.InterfaceBridge/ReportWriters/NetWorthWriter.fs:99, :149-151; REQ-RPT-8.7
- **Summary:** Where a ledger balance came from is stored twice: once in NetWorth.netWorthDate, a DU with no data, and again in each row's `balanceDate: LocalDate option`. Because the two are separate, the types allow combinations the domain forbids, and the report writer has to handle them.
- **Resolution:** fix-code

`NetWorthDate = FiscalPeriodDate | PreLedgerDate` carries no data. The fact that depends on it, the date of the Pre-ledger Balance a figure came from, sits in `LedgerAccountBalance.balanceDate: LocalDate option`. The doc comment on lines 15-16 states the rule that links them: Some on a pre-ledger date, None on a fiscal-period date. Only the way `balancesOn` happens to be built enforces it. The type system does not. NetWorthWriter shows the cost. `balanceDateText` (line 99) falls back to "" for a None that cannot happen. The mortgage cell (lines 149-151) matches `PreLedgerDate, Some d` and sends both `(PreLedgerDate, None)` and `(FiscalPeriodDate, Some _)` to a wildcard. A later change to `balancesOn` could produce either of those, and the report would show a blank instead of failing. The same file already uses the better pattern. `PropertyValueSource = ValuationDated of LocalDate | PurchaseBasisValue` (PositionsComponent.fs:406-408) puts the date inside the case that has one, and the writer matches it exhaustively with no wildcard (NetWorthWriter.fs:141-143).

**Action:** Replace `balanceDate: LocalDate option` with a source DU on each row, e.g. `balanceSource: LedgerBalanceSource` where `LedgerBalanceSource = LedgerAsOf | PreLedgerBalanceOf of LocalDate`, following PropertyValueSource. Have the writer match it exhaustively. NetWorth.netWorthDate can stay as the date-level classification, which REQ-RPT-10.2 needs.

**Why:** This is the principle of making illegal states unrepresentable. When one field's presence depends on another field's value, put both in one DU case so the compiler enforces the link. A payload-less DU next to an Option elsewhere is the 'boolean plus nullable' anti-pattern under different names. Every consumer then has to write wildcard branches for states that 'can't happen', and those branches silently absorb the day one of them does happen.

---

## FSDDD-S2-2 — idiom
- **Location:** Src/Business.FinancialServices.Positions/InvestmentActivity.fs:16-30 (record fields quantity/price), :103-122 (confirmShape), :206-216 (reconstitute); REQ-POS-12.7
- **Summary:** An Activity's quantity and price are two separate options, so 'a price with no quantity' can be represented. The rule against it (REQ-POS-12.7) is enforced only by `confirmShape`, at runtime, on the write path and in reconstitute.
- **Resolution:** dan-decides

REQ-POS-12.7: "Price may be null; when non-null it is a Price, and it may be present only when a quantity is." The entity is `quantity: Quantity.Quantity option; price: Price.Price option`, and `InvestmentActivity.create` is public and accepts `None, Some price`. `confirmShape` rejects that combination with `PositionsActivityPriceWithoutQuantity`, and reconstitute repeats the check. The house's own field-update article (CompoundedLearnings/articles/coding/field-update-pattern.md, 'Bundled') sets the test: sub-parts that 'are only ever meaningful together' stay one value, because splitting them 'would let the caller write nonsensical partial states' (precedent: Blocker, Cadence). A price is meaningful only alongside a quantity, so `units: (Quantity * Price option) option` bundles them. The boundary converter, whose contract (PositionsContracts.fs:193+) has separate fields, would still raise the typed error when it pairs them, so REQ-POS-12.7's rejection is unchanged. The rest of REQ-POS-12.6 (which kinds must or must not name a Security or carry a quantity) can stay with the orchestrator, per validation-layers layer 4. Only the price/quantity dependency has a cheap, local type encoding.

**Action:** Dan decides whether to bundle price with quantity in InvestmentActivity's type, e.g. `units: (Quantity.Quantity * Price.Price option) option`, with the boundary converter building the pair and raising PositionsActivityPriceWithoutQuantity there. If he keeps two options, nothing changes.

**Why:** This is the type-design half of 'parse, don't validate'. When one value has no meaning without another, nest it inside the other's Option. Then construction cannot produce the illegal pair, and every later reader (reconstitute, roll-forward, reports) is spared the check. Dan's orchestration-layer note says he wants such constraints to 'ALWAYS resolve to true... wherever possible', and a type is the one place a rule holds unconditionally.

---

## FSDDD-S2-3 — architecture
- **Location:** Src/Business.FinancialServices.Positions/AccountSnapshotLot.fs:46-60 (confirmLot), :31-44 (create); Src/Business.FinancialServices.Positions/InvestmentActivity.fs:100-122 (confirmShape); compare PositionsComponent.fs:349-368 (ContributionBasis, PurchaseBasis, ValuationValue); CompoundedLearnings/articles/coding/validation-layers.md layer 2; REQ-POS-6.11, REQ-POS-12.7
- **Summary:** Slice 2's single-value sign rules are checked by orchestrator-time `confirm*` functions that return `Result<unit>` and then pass on the unwrapped Quantity/Money. These are lot quantity > 0, lot cost basis ≥ 0, activity quantity > 0 and activity amount ≥ 0. Positions already wraps the same kind of rule in component smart constructors, and the validation-layers article puts per-value rules there.
- **Resolution:** dan-decides

validation-layers.md layer 2: "Component smart constructors — per-value validation in a component's create/fromString, returning a Result... A value that exists has passed." Layer 4, the orchestrator, is for cross-field, composite and state-dependent rules. 'Quantity greater than zero' and 'Money not negative' depend on one value only. Positions follows layer 2 for Money: `ContributionBasis.create` rejects a negative, and `PurchaseBasis.create` and `ValuationValue.create` reject a non-positive. Slice 2 departs from that. `AccountSnapshotLot.confirmLot` checks `Quantity.isPositive` and `Money.isNegative` and returns unit, and `AccountSnapshotLot.create` (public) then accepts any `Quantity`, including 0. `InvestmentActivity.confirmShape` does the same for quantity and amount. So the proof is thrown away: the entity's type says 'a Quantity' (≥ 0 by REQ-QP), not 'a positive quantity'. A future in-codebase importer (Dan's vision puts import inside the codebase) that builds lots without going through `confirmLots` compiles. The snapshot line has the same shape from slice 1 (AccountSnapshotLine.confirmFigures), and slice 2 repeated it rather than following the component-primitive precedent. 10-04a #094 put the line's checks into reconstitute. It ruled on the read path, not on where a single-value rule lives, so it does not decide this.

**Action:** Dan decides whether positive-quantity and non-negative-money rules on Positions records become component primitives (e.g. `PositiveQuantity`, `NonNegativeMoney`, or per-concept wrappers like `LotQuantity`, following ContributionBasis). If not, he decides whether validation-layers should record that Positions keeps sign rules on raw Quantity/Money as orchestrator confirmations.

**Why:** 'Parse, don't validate': a check that returns `unit` leaves no evidence in the type, so every later consumer must either trust that the check ran or run it again. A smart constructor turns the check into a type the compiler tracks. The codebase already does this for Money with ContributionBasis and its siblings. Two styles for the same kind of rule in one bounded context weakens the ubiquitous language: a reader can't tell from a signature which values are constrained.

---

## FSDDD-S2-4 — stale-reference
- **Location:** Specs/Definitions.md:55 (Component row); DbMigration/Scripts/202610101000-CreateAccountSnapshotLotTable.sql; Src/Business.FinancialServices.Positions/AccountSnapshotLot.fs:15-22; REQ-SYS-3.1
- **Summary:** positions.account_snapshot_lot has no created_at/modified_at, and the migration says a lot is 'a component of its line'. But Definitions.md's Component row entry names its tables one by one and does not name the lot table. As written, a lot is an Entity and REQ-SYS-3.1 applies.
- **Resolution:** fix-spec
- **Prior ruling:** Audit 2026-10-04a #097 (SYS31-POS-1), accepted: Hobson added the Component row Definitions entry for snapshot lines, owner sets and property account sets. Same class, but the ruling covered a list of tables, and the lot table is new in slice 2. It is not on that list, so the ruling does not cover it without stretching.

Definitions.md:55 defines a component row as "A record that exists only as a part of its parent: an account snapshot line (`positions.account_snapshot_line`...), an owner set (...) and a property's ledger-account sets (...)". The list is closed and names tables. The lot migration's opening comment says: "A lot is a component of its line, like the line is of its snapshot: no timestamps." Lots do behave like component rows: AccountSnapshotOrchestration.recordOne (lines 211-213) and deleteSnapshot (271-272) write and delete them only with their line, never update them in place, and replace them as a set. So the design is sound and the code matches it. Only the authority is missing. By the Entity litmus test (Definitions.md:31, 'does any user action ever insert or update a row? Yes → entity'), a lot is an entity. REQ-SYS-3.1 says 'Every persisted entity must carry a "created at" and a "modified at" timestamp', and the lot table has neither. The Positions slice 2 spec commit (95df0e4) edited Definitions.md to add 'Pre-ledger date' but did not extend this list.

**Action:** Add `positions.account_snapshot_lot` (part of an account snapshot line) to the Component row entry in Specs/Definitions.md.

**Why:** In DDD, whether a record is an Entity (with its own identity and lifecycle) or part of an aggregate (whose lifecycle belongs to the root) is a modelling decision, and the ubiquitous language has to say so explicitly. Here that language is Definitions.md, which outranks code. When the code makes the right aggregate call but the definition doesn't name it, the higher authority contradicts the implementation, and a later auditor or agent may 'fix' it by adding timestamps.

---

## FSDDD-S2-5 — maintainability
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:89-98 (classifyDate, beforeLedger); Src/Business.FinancialServices.Positions/PreLedgerBalance.fs:49-57 (confirmBeforeLedger); Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs:158-173 (confirmNoPreLedgerBalanceStranded); Specs/Definitions.md:42-43 'Pre-ledger date'
- **Summary:** 'Pre-ledger date' is now a defined term (Definitions.md, 2026-10-10), but no function is named for it. Its predicate (date earlier than the earliest fiscal period's start; no date qualifies when no fiscal period exists) is written out inline in NetWorth.classifyDate and PreLedgerBalance.confirmBeforeLedger, and its inverse in FiscalPeriodCreation.
- **Resolution:** fix-code

Commit 94978e6 says 'ledger start has one definition', and `FiscalPeriod.earliestStart` (FiscalPeriod.fs:50) is that single source. The term built on it is not single-sourced. NetWorth.fs:92-95 computes `match earliestStart with Some s -> date < s | None -> false`. PreLedgerBalance.fs:54-57 matches the same option again, giving `None -> error` and `Some start when balanceDate >= start -> error`. FiscalPeriodCreation.fs:165-166 selects balances `>= earliestStart`, the complement. All three are correct today and agree with Definitions.md:43. Each also re-encodes the 'no fiscal period' clause separately: in one it is false, in another a distinct error, in the third an empty list. If Definitions changes the term (say, to start at the first fiscal period with a posting), three sites must change together, and only the REQ-POS-15.x and REQ-RPT-8.7/10.3 tests would show that one was missed.

**Action:** Add one named predicate for the defined term, e.g. `FiscalPeriod.isPreLedgerDate : FiscalPeriod list -> LocalDate -> bool` beside `earliestStart`. Have classifyDate, confirmBeforeLedger and the stranding guard go through it, or through a `ledgerStart`-returning companion whose semantics are documented once.

**Why:** In DDD's ubiquitous language, a term the spec defines should map to exactly one named construct in the code, so the domain word and its implementation can't drift apart. Writing out a defined predicate at each use site makes the definition an emergent property of three call sites instead of a function a reader can find, test and change in one place.

---


