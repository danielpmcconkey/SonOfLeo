# SonOfLeo architecture auditor (ArchiMate model vs Src)

## ARCH-TIER-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs:10-11,16-31,44; model ApplicationFunction 'Create and persist a new fiscal period'
- **Summary:** The fiscal-period creation orchestrator now reads a Positions entity (PreLedgerBalance) and knows a Positions concept and label, so a fiscal-period-rung piece understands a higher domain.
- **Resolution:** dan-decides

FiscalPeriodCreation.fs opens Business.FinancialServices.Positions and Positions.PositionsError (lines 10-11). Its private confirmNoPreLedgerBalanceStranded (16-31) calls PreLedgerBalance.fetchOnOrAfter and PreLedgerBalance.dateRange, and constructNewAndPersist runs it before every new period (44). That includes ensureFiscalPeriods, the weekly Saturday run. The model puts this component on the fiscal-period rung: FiscalPeriodCreation realizes 'Create and persist a new fiscal period' and 'Ensure fiscal periods exist for a range of months', both composed under 'Fiscal period functions', which realizes 'The Fiscal Period domain owns fiscal period capabilities'. The model's own documentation of that function now reads 'refusing one that would start on or before the date of a pre-ledger balance', so the model itself writes a Positions concept into a fiscal-period capability.

Rules involved:
- 'This application is structured in domain tiers and we are strictly forbidden from altering or circumventing the tier hierarchy' says 'no lower tier has any understanding of higher tier modules, types, or even labels'.
- 'The Fiscal Period domain is a lower tier than the Journal Entry domain', and so on up to positions, places fiscal period far below positions.
- 'Domains Build Upward' lets a container use anything in a container below it. The Positions project is below orchestration, so the reference is legal by direction. But the principle also ranks pieces inside orchestration by concept, and this fiscal-period-level piece now carries positions knowledge.
- 'Out-of-Order Is a Design Smell' says such a reach is reported to Dan, never quietly accommodated.

Under 'For cross domain use cases, the use case should be considered to be a component of the domain tier that best aligns to the use case', the stranding rule is a pre-ledger-balance (positions) invariant. It is the same rule that PositionsLedgerLinks.confirmCarriesNoPreLedgerBalance enforces for ledger links. Here it is enforced from inside a fiscal-period use case instead.

**Action:** Present to Dan. Either approve this as a named exception under 'Out-of-Order Is a Design Smell', or move the guard to the positions rung: for example, a positions-level orchestrator that wraps fiscal-period creation (and ensure) and that the FiscalPeriod routes call. Then remove the pre-ledger clause from the fiscal-period capability's documentation in the model.

**Why:** The tier rule exists so that lower concepts never need to know higher ones. Once fiscal-period creation depends on a Positions entity, every future change to pre-ledger balances can break the ledger's most basic use case, and the weekly ensure run now reads the positions schema.

---

## ARCH-ERR-1 — architecture
- **Location:** Src/Business.FinancialServices.Positions/PositionsError.fs:92,239; Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs:29
- **Summary:** The error that refuses creating a fiscal period is defined in PositionsError, not in the fiscal-period domain's error vocabulary.
- **Resolution:** dan-decides

PositionsPreLedgerBalancesBlockFiscalPeriod (PositionsError.fs:92, message at 239: 'Fiscal period {period} cannot be created: ...') is returned by FiscalPeriodCreation.constructNewAndPersist (FiscalPeriodCreation.fs:29), and so by the FiscalPeriod Create and Ensure routes. 'Each domain defines its own errors', Ex 2, says an error refusing an account use case because of journal-entry facts 'is nevertheless defined in the LedgerErrors module as it is an account-domain error'. By that rule, refusing a fiscal-period creation is a fiscal-period-domain error and belongs in LedgerError, next to FiscalPeriodEnsureStartAfterEnd, which the same file raises at line 63. Moving it there would put the label 'pre-ledger balance' into Ledger, which the tier principle forbids (see ARCH-TIER-1). That conflict is itself the signal that the use case sits on the wrong rung.

**Action:** Decide together with ARCH-TIER-1. If the guard stays in the fiscal-period use case, the error moves to LedgerError, and Dan approves the higher-tier label in it. If the guard moves to a positions-level orchestrator, the error stays in PositionsError and is raised from there.

**Why:** An error's home tells a maintainer which domain owns the refusal. A fiscal-period refusal with a Positions error splits ownership of one use case across two vocabularies.

---

## ARCH-TYPE-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:46-62,64-66
- **Summary:** Slice 2 defines two new component-style discriminated unions (NetWorthComponent with a toString module, and NetWorthDate) in Business.CrossDomainOrchestration.
- **Resolution:** dan-decides

NetWorth.fs:46 defines type NetWorthComponent (CountedLedgerAssets | Investments | PropertyValues | Liabilities | OwnedPropertyMortgages) with module NetWorthComponent.toString. NetWorth.fs:64 defines type NetWorthDate (FiscalPeriodDate | PreLedgerDate). NetWorthHistory, ReportConverters, NetWorthWriter and NetWorthHistoryWriter consume both. These are component types (closed vocabularies with a string conversion), not composite or report-row records. The rules that apply:
- 'Any component type in the cross domain orchestration or higher is a signal that we're violating the tiering or domain principles somewhere'.
- 'Component types are defined in a components module in the lowest tier of their business domain', which realizes 'A type lives in the lowest compile tier that can see everything it references'.

There is direct precedent. Audit 2026-10-04a #111 (ARCH-POS-8), accepted, moved WealthGrouping, WealthGroup and PropertyValueSource out of orchestration into PositionsComponent, where they now sit (PositionsComponent.fs:406-433). This diff removes PropertyValueSource from NetWorth.fs and adds these two in the same place. The REPORT-RECORDS ruling covers public report or view row records, not discriminated-union vocabularies, so it does not apply.

**Action:** Move NetWorthComponent (with its toString) and NetWorthDate into Business.FinancialServices.Positions/PositionsComponent.fs, beside WealthGrouping, WealthGroup and PropertyValueSource.

**Why:** Placing a vocabulary in the components module of its domain keeps it reusable below orchestration and keeps orchestration limited to composing. Dan accepted the same correction a week ago for the sibling types.

---

## ARCH-CAP-1 — architecture
- **Location:** Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs:519-525 (Holding Delete), 616-622 (Property Delete); model ApplicationFunctions 'Maintain holdings' and 'Maintain properties'
- **Summary:** The operator routes Holding Delete and Property Delete offer use cases that no capability in the model describes.
- **Resolution:** dan-decides

The model's 'Maintain holdings' reads 'Create holdings, change their basis method within the tax-treatment rule, and list them.' The code also offers Holding Delete: HoldingOrchestration.deleteHolding, which is refused while any snapshot line or Activity references the holding. That refusal is extended in slice 2 through InvestmentActivity.existsForHolding. The model's 'Maintain properties' reads 'Create, update and list properties ...', but the code also offers Property Delete (RealEstateOrchestration.deleteProperty, refused while any Valuation exists). Other capabilities in the same family do name their delete: 'Record account snapshots', 'Record property valuations' and 'Record pre-ledger balances' all say 'delete'. model_drift.py reports no drift because it cannot see capabilities. This gap was found by comparing route verbs against ApplicationFunction documentation, as the audit method requires.

**Action:** Add to the model. Extend 'Maintain holdings' to 'Create, change the basis method of, delete (refused while a snapshot line or activity names it) and list holdings'. Extend 'Maintain properties' with 'delete (refused while it has a valuation)'.

**Why:** The model is the written architecture. A use case the code offers but the model omits cannot be checked against domain-ownership rules, and readers of the model won't know that hard deletes exist.

---

## ARCH-MODEL-1 — architecture
- **Location:** Model Principle 'Domains Build Upward' (Positions bullet); Src/Business.FinancialServices.Positions/PreLedgerBalance.fs; Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs
- **Summary:** 'Domains Build Upward' orders Positions internally as investments and real estate only, so it cannot place pre-ledger balances, the third Positions concept slice 2 added.
- **Resolution:** dan-decides

The principle states: 'Inside Business.FinancialServices.Positions: investments and real estate are unordered peers. Neither may use the other; anything that combines them (net worth, investable wealth) is orchestration.' Slice 2 adds PreLedgerBalance.fs to the Positions project, compiled after Valuation.fs. A pre-ledger balance is neither an investment nor real estate: it is a ledger account's balance before the first fiscal period, per Positions spec §1's separate 'Pre-ledger balances' sub-head and §15. In orchestration, PositionsLedgerLinks (used by both peers) and PreLedgerBalanceOrchestration (which reads both InvestmentAccount and Property) depend on it. Because the model gives the concept no rung, the auditor cannot tell whether an investment or real-estate piece may use PreLedgerBalance, or whether PreLedgerBalance may use either peer. Today it opens neither, which is consistent with any reading.

**Action:** Add to the model. Give pre-ledger balances a stated place in the Positions bullet of 'Domains Build Upward', for example 'a third unordered peer that uses neither investments nor real estate', or 'below both peers'. Update the Positions functions summary to mention activity and pre-ledger balances.

**Why:** Without a rung, the hierarchy rule cannot judge the newest Positions concept, and the next slice has no rule to follow when it touches pre-ledger balances.

---

## ARCH-PEER-1 — enforcement-gap
- **Location:** Checks/check-peer-guard.sh:11-14
- **Summary:** The peer guard that enforces 'investments and real estate are unordered peers' does not list the slice-2 investment modules, so a violation in them would pass the check.
- **Resolution:** dan-decides

check-peer-guard.sh lists the investment modules as: DimensionValue Security InvestmentAccount Holding AccountSnapshotHeader AccountSnapshotLine DimensionValueOrchestration SecurityOrchestration InvestmentAccountOrchestration HoldingOrchestration AccountSnapshotOrchestration HoldingsAsOf InvestmentWealthHistory. It omits AccountSnapshotLot and InvestmentActivity (Positions project), and InvestmentActivityOrchestration and UnitRollForward (orchestration). The check classifies files by these lists, so a real-estate file that names AccountSnapshotLot or InvestmentActivity, or one of these four files naming Property, Valuation or RealEstateOrchestration, would go unreported. I grepped for both directions and found no violation today. This finding is about the enforcement, not current code. The rule it protects is in 'Domains Build Upward': 'Neither may use the other'.

**Action:** Add AccountSnapshotLot, InvestmentActivity, InvestmentActivityOrchestration and UnitRollForward to the investment list in Checks/check-peer-guard.sh. Decide where PreLedgerBalance and PreLedgerBalanceOrchestration sit (see ARCH-MODEL-1).

**Why:** A mechanical guard that silently stops covering new files gives false assurance. The peer rule will be broken first in exactly the modules the check does not see.

---

## ARCH-NAME-1 — idiom
- **Location:** Src/Business.FinancialServices.Positions/PreLedgerBalance.fs:188-205; Src/Business.CrossDomainOrchestration/PreLedgerBalanceOrchestration.fs:87
- **Summary:** PreLedgerBalance's only database update is a bespoke replaceBalance rather than an 'update' backbone, unlike the Valuation and AccountSnapshotHeader replace-on-re-record paths.
- **Resolution:** dan-decides

PreLedgerBalance.replaceBalance (188-205) issues 'update positions.pre_ledger_balance set balance = @balance, modified_at = @modified', and PreLedgerBalanceOrchestration.recordOne calls it when an account and date are recorded again (line 87). It is the entity's only update. The rules involved:
- 'Name functions that provide the basic database update "backbone" for an entity type "update"'.
- 'Prefix function names with "update" when they call their module's update function to update DB rows in specific manners'.
- 'Update functions should fail when the result would be no-op', associated with the update backbone.

The two existing replace-on-re-record paths both go through the module's update backbone with FieldUpdates and its no-op guard: Valuation.update (Valuation.fs:173-200, called from RealEstateOrchestration.recordValuation:320) and AccountSnapshotHeader.update (called from AccountSnapshotOrchestration.recordOne). replaceBalance sidesteps the naming convention and the FieldUpdate and no-op structure.

**Action:** Rename or restructure: give PreLedgerBalance an 'update' backbone that takes a PreLedgerBalanceFieldUpdates (balanceUpdate: FieldUpdate<Money>) with the empty-update guard, and have recordOne call it with SetTo balance, as recordValuation does. Or record Dan's approval of 'replaceBalance' as an exception.

**Why:** The shared names (create, reconstitute, query, persist, update) are how a maintainer finds an entity's write paths. A one-off name for the only update hides it and drops the no-op guard the convention attaches to updates.

---

## ARCH-ENC-1 — architecture
- **Location:** Src/Business.FinancialServices.Positions/AccountSnapshotLot.fs:139, InvestmentActivity.fs:249, PreLedgerBalance.fs:118 (new in slice 2); also Positions DimensionValue.fs:75, Security.fs:99, InvestmentAccount.fs:185, Holding.fs:87, AccountSnapshotHeader.fs:115, AccountSnapshotLine.fs:145, Property.fs:188, Valuation.fs:121; Business.General/Person.fs:76
- **Summary:** Every Positions entity's query function, including the three added in slice 2, is public, though nothing outside its own module calls any of them.
- **Resolution:** dan-decides

The model constraint 'Query functions should only be called by functions within the same business domain' (which realizes 'Encapsulation conventions are enforced' and 'Business domains are real boundaries') says: 'Most query functions should be private. however, some use cases require that orchestration functions call them ... Though this should be rare.' I grepped Src for qualified '<Module>.query' calls and for opens of entity modules. The only external callers are MasterAgreement (CashFlowCompositeFetcher), StageEntryHeader (StageEntryOrchestration), JournalEntryHeader (JournalEntryOrchestration) and ClassificationRule (ClassificationOrchestration). No Positions query and not Person.query is called outside its module. Yet all twelve Positions modules and Person.fs declare 'let query' public. The Ledger modules (Account, FiscalPeriod, JournalEntryLine, and others) and the ingestion modules (StageEntryLine, IngestionSource) declare 'let private query' where nothing outside needs it. Slice 2 continued the public pattern in AccountSnapshotLot, InvestmentActivity and PreLedgerBalance. No ruling in resolved-findings.md covers query visibility.

**Action:** Make query private in each Positions module and in Person.fs; none has an external caller, so nothing else changes. Leave public only the four with a cross-module caller. The same pattern exists in CashFlow modules other than MasterAgreement, and in RuleMatch, if Dan wants it swept too.

**Why:** A public query hands any caller a raw SQL-fragment interface to another domain's table, which is the encapsulation the rule protects. Keeping it private where unused makes a future cross-domain caller a visible, reviewable change rather than a silent one.

---

## ARCH-REQ-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PrePostingReview.fs:18,29,87-88,93,181; BalanceSheetIntegrity.fs:78; TrialBalance.fs:28,34; StageEntryOrchestration.fs:639
- **Summary:** Source comments carry site-tag REQ annotations ('REQ-RPT-7.5: ...', '(REQ-RPT-7.1)'), which the model constraint 'No REQ annotations in source' retires.
- **Resolution:** dan-decides

The model constraint 'No REQ annotations in source' reads: 'Tests map to REQs. Tests own whether the code meet the spec.' CompoundedLearnings/articles/architecture/no-req-annotations-in-source.md permits a REQ ID only in rationale ('REQ-X is why this looks wrong but isn't') and retires traceability tags ('this site enforces REQ-X'). These comments are tags, not rationale:
- PrePostingReview.fs:87-88: '/// fetchPrePostingReview returns every postable staged entry (REQ-RPT-7.1), line by line (REQ-RPT-7.2 to 7.4), in entry date, source name, fi_reference order (REQ-RPT-7.6). A line with no account fails it (REQ-RPT-7.5).'
- PrePostingReview.fs:93: '// REQ-RPT-7.5: the first line with no account ... fails the review'
- PrePostingReview.fs:181: '// REQ-RPT-7.5 was checked above ...'
- PrePostingReview.fs:18 and 29: '(REQ-RPT-7.4)'
- BalanceSheetIntegrity.fs:78: '/// REQ-RPT-5.3: an imbalance or a non-zero residual is data, not an error'
- TrialBalance.fs:28 and 34: 'is REQ-RPT-1.6's sibling rule / depth-first order'
- StageEntryOrchestration.fs:639: '// only batch post moves an entry to Posted (REQ-STG-4.8)'

They have the same 'REQ-RPT-x:' shape that audit 2026-10-03a #303 (CT-REQ-TAGS-IN-WRITERS, accepted) removed from PeriodActivityWriter and PrePostingReviewWriter as 'site tags, not rationale'. Comments I judged to be rationale are excluded, for example FiscalPeriodCreation.fs:51 explaining why missing periods are not a no-op error, and JournalEntryVoiding.fs:78-79.

**Action:** Remove the REQ IDs from the listed comments and keep the plain descriptions, as was done for the writers under 2026-10-03a #303.

**Why:** Nothing executes a tag, so it rots unnoticed, and an incomplete set reads as complete. Dan settled this on 2026-07-31, and the model records it as a constraint.

---


