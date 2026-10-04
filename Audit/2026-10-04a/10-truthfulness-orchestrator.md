# code-truthfulness-auditor (Business.CrossDomainOrchestration)

## SYS-6.1-DV-RENAME — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:61-71 (renameDimensionValue), :34-46 (confirmDimensionValueNameFree); REQ-SYS-6.1 (amended 2026-10-03), REQ-POS-11.1, REQ-POS-2.3
- **Summary:** Renaming a Dimension Value to the name it already has fails with PositionsDimensionValueAlreadyExists. REQ-SYS-6.1 says an update that sets a field to the value it already holds succeeds.
- **Resolution:** fix-code

renameDimensionValue resolves the value by (dimension, currentName) and then calls `confirmDimensionValueNameFree context dimension newName`. That helper has no `self` parameter: if any Dimension Value in the dimension has newName, it returns PositionsDimensionValueAlreadyExists. When newName = currentName, the value it finds is the one being renamed, so the request is refused as a duplicate. The REQ-SYS-6.1 sub-bullet reads: "A field set to the value it already holds counts as named, and the update succeeds. This is the system default for every update operation; an entity spec need not restate it." Every other name-uniqueness check in the slice excludes the record being updated: confirmSecurityNameFree (line 93, `self`), confirmInvestmentAccountNameFree (line 253, `Some self`), confirmPropertyNameFree (RealEstateOrchestration.fs:56) and PersonOrchestration.confirmNameFree (`except`). Only the Dimension Value rename leaves it out. The DimensionValue Rename route (Ui.InterfaceBridge/Routes/PositionsRoutes.fs:35-44) passes currentName and newName straight through and does not short-circuit, so the error reaches the operator. No test covers renaming to the current name (SecurityMaintenance.fs:98-132 covers a successful rename, a rename to another value's name, and a rename in the wrong dimension).

**Action:** Give confirmDimensionValueNameFree a self/except DimensionValueId option, as the other confirm*NameFree helpers have, and pass the resolved value's id from renameDimensionValue. Add a REQ-SYS-6.1 / REQ-POS-11.1 test that renames a Dimension Value to its current name and expects success.

**Why:** REQ-SYS-6.1 was amended 2026-10-03 so that re-sending a value the record already holds is not treated as a caller error. This rename is the one update path in the slice that still treats it as one, and it reports a misleading 'already exists' about the record the caller is renaming.

---

## POS-ROTH-CB-TAXCHANGE — missing-requirement
- **Location:** Specs/Behavioral/Positions.md REQ-POS-6.4, REQ-POS-5.3, REQ-POS-11.3/11.4; Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:370-431 (updateInvestmentAccount)
- **Summary:** Changing an Investment Account's tax treatment away from 'Roth' is accepted when its stored snapshots carry a contribution basis. Those snapshots then break REQ-POS-6.4, and holdings-as-of and net worth report a contribution basis on a non-Roth account.
- **Resolution:** dan-decides

REQ-POS-6.4 allows a contribution basis only when the account's tax treatment is 'Roth'. The spec protects the other snapshot- and holding-dependent invariants from later account updates: REQ-POS-5.3 rejects a tax-treatment change that would break a Holding's basis method, and REQ-POS-11.4 rejects an active-period change that would leave snapshots outside it. No requirement does the same for a tax-treatment change against snapshots with a contribution basis. The code matches the spec. updateInvestmentAccount runs confirmHoldingsAllowTaxTreatment (holdings only) and confirmPeriodKeepsSnapshots (dates only), and nothing in InvestmentOrchestration.fs or Positions/InvestmentAccount.fs looks at contribution_basis. Concrete case: a Roth account has snapshots with a contribution basis and holdings with no basis method (allowed under REQ-POS-5.2). The operator updates its tax treatment to 'TaxDeferred'. REQ-POS-5.3 finds nothing wrong (the basis method is still None), so the update succeeds. HoldingsAsOf (REQ-POS-8.2) and NetWorth (REQ-RPT-8.5, `contributionBasis` on each NetWorthInvestmentAccount) then report a contribution basis on a TaxDeferred account, a state REQ-POS-6.4 says can never be recorded.

**Action:** Dan decides whether a tax-treatment change from 'Roth' is rejected while any of the account's snapshots carries a contribution basis (with an error naming the offending snapshot dates, as REQ-POS-11.4 does). If yes, add it as a REQ-POS-5.x/11.x requirement, then enforce it in updateInvestmentAccount and test it.

**Why:** Positions treats a snapshot as verbatim evidence that must never hold a contradiction (design note, 'reported figures, recorded verbatim'). Today the snapshot can be valid when recorded and invalid after a later account edit, and the reports would show a Roth-only figure under a tax treatment where it means nothing. The tax-treatment totals are, in the spec's words, 'the first thing anyone settling the household's affairs needs to know'.

---

## RPT-8.5-LIABILITIES-TOTAL — ambiguity
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.2, REQ-RPT-8.5; Src/Business.CrossDomainOrchestration/NetWorth.fs:115-120, 174-181, 197; Src/Ui.InterfaceBridge/ReportWriters/NetWorthWriter.fs:134-143
- **Summary:** The net worth report's 'Liabilities' total leaves out the mortgages of owned Properties, while net worth subtracts them, so the totals block does not add up to the net worth it shows.
- **Resolution:** dan-decides

REQ-RPT-8.2 defines net worth as unlinked assets plus holdings plus property values, 'less the net balance, as of the date, of every Liability account'. REQ-RPT-8.5 lists the totals as 'counted ledger assets, investments, property values, liabilities, net worth, and investable wealth' and does not say whether the 'liabilities' total is every Liability account (the amount REQ-RPT-8.2 subtracts) or only the listed liability accounts. The code takes the second reading. `totalLiabilities` sums only `liabilityAccounts`, which excludes the mortgages of owned Properties (line 118). Net worth subtracts `allLiabilities = totalLiabilities + totalMortgages` (lines 178-181). The data-only payload (ReportConverters.fs:189) and the rendered Totals block (NetWorthWriter.fs:141, 'Liabilities') show `totalLiabilities`. In the hand-derived fixture (Tests.Integrated/CrossDomainOrchestration/NetWorth.fs), the totals block shows ledger assets 5,000.00, investments 12,920.00, property values 670,000.00, liabilities -25.00 and net worth 207,945.00. 5,000 + 12,920 + 670,000 - (-25) = 687,945, so the 480,000.00 of mortgages appears only inside each Property's table. The test at line 136 (`Assert.Equal(-25.00M, result.totalLiabilities ...)`) fixes this reading in place. A developer reading 'liabilities' through REQ-RPT-8.2's definition would total every Liability account.

**Action:** Dan decides which total REQ-RPT-8.5's 'liabilities' means. Either amend REQ-RPT-8.5 to say the liabilities total covers every Liability account (or add a separate mortgages total) so the totals add up, and change NetWorth.totalLiabilities and the test; or amend it to say the total covers only the listed liability accounts, so the current behavior is specified rather than incidental.

**Why:** A totals block whose lines do not add up to the total under them reads as an arithmetic error to anyone checking the report by hand. This report is meant for the household's overall financial picture, and the person settling the household's affairs is the reader the spec names.

---

