# test-efficacy-Reporting

## EFF-RPT-8.6-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/PositionsReportRoutes.fs:141-195 (REQ-RPT-8.6, REQ-RPT-8.5); Tests/Tests.Integrated/InterfaceBridge/PositionsReportRoutes.fs:366-388; Src/Ui.InterfaceBridge/BoundaryConverters/ReportConverters.fs:193
- **Summary:** No test checks the owned-property mortgages total in the NetWorth route's data-only payload. A wrong mapping of totalOwnedPropertyMortgages at the boundary would pass every test.
- **Resolution:** fix-test

REQ-RPT-8.5 (amended 2026-10-05, after disposition #198 of audit 2026-10-04a) adds 'mortgages of owned Properties' to the totals that net worth returns. REQ-RPT-8.6 requires the data-only output mode. NetWorthReturnRow carries the field (ReportsContracts.fs:207), filled by ReportConverters.fs:193 (`totalOwnedPropertyMortgages = netWorth.totalOwnedPropertyMortgages |> Money.amount`).

The data-only route test 'REQ-RPT-8.6 the NetWorth route in data-only mode returns the same rows and totals as the net worth computation…' compares totals at lines 177-182:
```
Assert.Equal<decimal list>(
    [ computed.totalLedgerAssets; computed.totalInvestments; computed.totalPropertyValues
      computed.totalLiabilities; computed.netWorth; computed.investableWealth ] |> List.map Money.amount,
    [ row.totalLedgerAssets; row.totalInvestments; row.totalPropertyValues; row.totalLiabilities; row.netWorth
      row.investableWealth ])
```
This list has six of the seven totals and leaves out totalOwnedPropertyMortgages. The pre-ledger data-only test (lines 366-388) checks only isPreLedger, the dated account rows and absentComponents. It checks no totals.

A grep of Tests/ for totalOwnedPropertyMortgages finds it only in the NetWorthHistory route test (lines 432/440), which uses a different converter (ReportConverters.fs:224). The rendered totals test (line 295) reads the HTML that NetWorthWriter produces, not the data-only payload.

Smell test: change line 193 to `totalOwnedPropertyMortgages = netWorth.totalLiabilities |> Money.amount`, or to 0. The whole suite stays green.

The fiscal-period data-only test also never asserts row.isPreLedger = false or row.absentComponents. Those two fields are checked only on the pre-ledger date.

**Action:** In the REQ-RPT-8.6 data-only route test, add computed.totalOwnedPropertyMortgages and row.totalOwnedPropertyMortgages to the totals comparison. Its fixture value as of monthEnd3 is 480,000.00, so the check is not vacuous. Also assert row.isPreLedger = false and row.absentComponents = [] on that fiscal-period date.

**Why:** The totals line was added to REQ-RPT-8.5 so that the reported totals add up to net worth. The data-only payload is what the operator and any downstream consumer read. Without this assertion, a regression in that field would ship silently. This is the same pattern as last audit's #082: a route test that compares every field but one leaves that one field's converter unverified.

---

## EFF-RPT-8.5-ORDER — missing-requirement
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.5, REQ-RPT-8.8; Tests/Tests.Integrated/CrossDomainOrchestration/NetWorth.fs:118,186-198,247-252,331; Tests/Tests.Integrated/CrossDomainOrchestration/NetWorthPreLedger.fs:121,130,187,196,205; Tests/Tests.Integrated/InterfaceBridge/PositionsReportRoutes.fs:412; Src/Business.CrossDomainOrchestration/NetWorth.fs:130,159,167,173,205,234-241
- **Summary:** The tests assert the order of every list in the net worth result, but REQ-RPT-8.5 does not specify any order, so the tests pin behaviour no requirement states.
- **Resolution:** dan-decides

REQ-RPT-8.5 says what the net worth computation returns. It never says in what order. Contrast REQ-RPT-6.3 (period activity), REQ-RPT-7.6 (pre-posting review) and REQ-POS-8.4 (holdings as of), which each state their ordering.

The code imposes these orders:
- Asset and Liability accounts by code (NetWorth.fs:167, 173).
- Owned Properties by name (line 159).
- Mortgage accounts by code (line 205).
- Totals by tax treatment and by account group via `List.sortBy fst` (line 130). For tax treatment this is the order the DU cases are declared (Taxable, TaxDeferred, Roth, Hsa). For account group it is an ordinal string sort.
- Absent components in a fixed list order (lines 234-241).

The tests assert these orders with whole-list Assert.Equal:
- NetWorth.fs:118 `[ PF.residence; PF.rental ]`.
- NetWorth.fs:186 sorts the expected rows by code, then compares the whole list at 197-198.
- NetWorth.fs:247-252 `[ Taxable…; TaxDeferred…; Roth…; Hsa… ]` and `[ "Brokerage"; "Health"; "Retirement" ]`.
- NetWorthPreLedger.fs:121 and 130 (asset code order).
- NetWorthPreLedger.fs:187, 196 and 205 (absent-component order).
- PositionsReportRoutes.fs:412 (the rendered absent note's exact text order).

The order is visible to users in the rendered report and in the data-only payload. Two reasonable implementations, for example tax treatments sorted alphabetically, or properties in acquisition order, would fail these tests while still satisfying every REQ. Tests/README says: 'If the code you are testing does something uncited by the REQs, stop and point that out. Likely an REQ needs to be added.'

**Action:** Dan decides one of two things. (a) Add an ordering clause to REQ-RPT-8.5 (and to 8.8 for the absent components) that states the orders the code uses, so the existing tests cite it. (b) Rule ordering unspecified, and have the tests compare these lists as sets or maps.

**Why:** An assertion that pins behaviour no requirement states is an undocumented contract. The next change to the sort, made for a legitimate reason, breaks tests that cite REQ-RPT-8.5 for something 8.5 never said. A reader of the spec also cannot tell whether the order of the tax-treatment totals is deliberate or accidental.

---

