# statement-delta

## SD-0-CONFIRMATIONS — other
- **Location:** Specs/Behavioral/Positions.md §6, §12-§15, design notes; Specs/Behavioral/Reporting.md §8.7, §10; Audit/2026-10-04a/99-disposition.md; Src/Business.CrossDomainOrchestration/{FiscalPeriodCreation,PositionsLedgerLinks,InvestmentAccountOrchestration,RealEstateOrchestration,UnitRollForward}.fs; Src/Ui.InterfaceBridge/Routes/{PositionsRoutes,ReportRoutes}.fs; Src/Ui.OperatorCli/Program.fs
- **Summary:** Informational only. Every claim in Dan's statement checked out against the repo, and none is contradicted.
- **Resolution:** dan-decides

(1) Last audit: Audit/2026-10-04a/99-disposition.md has 164 accepted and 33 overruled rows. I spot-checked accepted code actions and all of them are applied: #002 (Zero/OneOrMany removed from the DAL), #003 (getBoolOption and NullableBoolean gone), #013 (Security.dimensionValueIn gone), #020 (PositionsSnapshotListIsEmpty is raised at AccountSnapshotOrchestration.fs:242), #100 (Checks/check-peer-guard.sh), #104 (LinkingRecord gone), #106 (NewInvestmentAccount gone), #108 (checkFigures gone), #114 (Checks/check-open-order.sh), #145 (REQ-CR-5.4 amended and ClassificationOrchestration orders ties by rule name), #190 (Positions enum fromString trims), #197 (confirmNoContributionBasisStranded, InvestmentAccountOrchestration.fs:161/240). This was a sample, not all 164 rows.
(2) Lots: REQ-POS-6.9 limits lots to lines of 'Taxable' accounts. REQ-POS-6.11/6.13 and the design note 'lots are reported, not derived' say lots are stored as reported. HoldingsAsOf carries lots (REQ-POS-8.3). The withdrawal of REQ-POS-5.6 is justified in the Withdrawn table.
(3) Activity: §12/§13 cover recording, replace-by-range and listing. The kinds include Contribution, Purchase, Sale and Dividend, among others. §14 defines a roll-forward between two snapshots that returns differences as data. UnitRollForward.fs reads Activity only to total units in and out.
(4) Nothing derives lots or cost from activity. Outside its own module and its routes, InvestmentActivity is used only by UnitRollForward (unit totals), HoldingOrchestration (existsForHolding guard) and InvestmentAccountOrchestration (active-period guard). No snapshot, lot or cost path reads it. The spec design note says the same.
(5) Pre-ledger guards: REQ-POS-15.7 is enforced in FiscalPeriodCreation.confirmNoPreLedgerBalanceStranded, which runs inside constructNewAndPersist. ensureFiscalPeriods goes through it too, so both creation paths are guarded. REQ-POS-15.8 is enforced in PositionsLedgerLinks.confirmCarriesNoPreLedgerBalance, which both InvestmentAccount link confirmation and Property asset-link confirmation call; mortgage links do not. RPT-8.7 and NetWorth/NetWorthHistory cover pre-ledger dates.
(6) Tests: I ran Skills/SonOfLeoRequirementsAudit/traceability-audit.sh. Invariants 1 and 2 are both clean, so every active requirement is cited by a test or waived. Leaving loud-only error cases untested matches the project's established no-bubblewrap rule, which appears in waiver reasons such as REQ-CR-1.2/1.17 and in 10-04a disposition rulings such as #016 and #071. I did not count it as a contradiction.
(7) Wiring: positionsDomainCommandRoutes contains InvestmentActivity Record/List, InvestmentAccount RollForward, PreLedgerBalance Record/Delete/List, AccountSnapshot Record and Holding FetchAsOf (which carry lots). Ui.OperatorCli/Program.fs commandRoutes includes positionsDomainCommandRoutes. ReportRoutes registers NetWorth (with pre-ledger dates) and NetWorthHistory, and Ui.ReportCli resolves reportingRoutes by name.

**Action:** None. Informational confirmation for Dan.

**Why:** Dan's statement is the baseline the other auditors judge against. Confirming it holds means their findings can rely on it without re-verifying.

---
