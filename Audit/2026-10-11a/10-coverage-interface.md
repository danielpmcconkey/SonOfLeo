# code-inward-coverage-ui-interfacebridge-cli

## COV-UI-NW-MORTGAGES-1 — test-gap
- **Location:** Src/Ui.InterfaceBridge/BoundaryConverters/ReportConverters.fs:193 (convert [NetWorth] to [NetWorthReturnRow], totalOwnedPropertyMortgages); Tests/Tests.Integrated/InterfaceBridge/PositionsReportRoutes.fs:141-196; REQ-RPT-8.5, REQ-RPT-8.6
- **Summary:** No test checks the owned-property mortgages total in the NetWorth route's data-only payload. The route test compares every other total with the computation but leaves this one out.
- **Resolution:** fix-test

`convert [NetWorth] to [NetWorthReturnRow]` maps seven totals into the data-only payload: totalLedgerAssets, totalInvestments, totalPropertyValues, totalLiabilities, totalOwnedPropertyMortgages, netWorth and investableWealth. REQ-RPT-8.5 (amended 2026-10-05) requires the owned-property mortgages total so the totals add up to net worth.

The only route test of the data-only NetWorth payload is `REQ-RPT-8.6 the NetWorth route in data-only mode returns the same rows and totals as the net worth computation for the same date...` (PositionsReportRoutes.fs:141). Its totals assertion compares this list with the computation:
`[ computed.totalLedgerAssets; computed.totalInvestments; computed.totalPropertyValues; computed.totalLiabilities; computed.netWorth; computed.investableWealth ]`
against
`[ row.totalLedgerAssets; row.totalInvestments; row.totalPropertyValues; row.totalLiabilities; row.netWorth; row.investableWealth ]`.
totalOwnedPropertyMortgages is missing from both lists. The pre-ledger data-only test (line 366) asserts only isPreLedger, balance dates and absentComponents.

A grep of Tests/ for `totalOwnedPropertyMortgages` returns hits in three places:
- the orchestration tests (NetWorth.fs, NetWorthPreLedger.fs, NetWorthHistory.fs), which read the domain record, not the payload;
- the NetWorthHistory route test (PositionsReportRoutes.fs:432/440), which goes through a different converter (`convert [NetWorthPoint] to [NetWorthPointReturnRow]`);
- the rendered Totals-block test (line 295), which reads the HTML writer, not this converter.

No test reads this field from the NetWorth data-only payload. If line 193 mapped the wrong figure (for example netWorth.totalLiabilities), or the field were dropped from the payload, every test would still pass. The other totals already in the list are mapped the same way and are checked.

**Action:** In the REQ-RPT-8.6 data-only NetWorth route test (PositionsReportRoutes.fs:178-184), add computed.totalOwnedPropertyMortgages and row.totalOwnedPropertyMortgages to the two totals lists, in the same position in each.

**Why:** The data-only payload is what the Monte Carlo pipeline and any scripted consumer read. A wrong value here is silent wrong data, not a loud rejection. The net worth totals only reconcile when this figure is right (REQ-RPT-8.5), and this is the only total in the payload that no route test pins.

---


