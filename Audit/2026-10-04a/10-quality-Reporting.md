# Reporting.md spec-quality auditor

## RPT-SCOPE-3 — contradiction
- **Location:** Specs/Behavioral/Reporting.md §3 preamble; REQ-RPT-3.3, 3.4, 3.6; Waived table rows for 3.3/3.4/3.6
- **Summary:** The §3 preamble says REQ-RPT-3.3 to 3.6 apply to the balance-sheet integrity, period activity and pre-posting review reports. The next sentence says 3.3, 3.4 and 3.6 describe trial-balance account rows, and the writers for those three reports do not implement them.
- **Resolution:** dan-decides

Preamble sentence 1 (2026-10-03): "REQ-RPT-3.1 to 3.6 apply to every rendered report (trial balance, balance-sheet integrity, period activity, pre-posting review), with 'account row' read as any row that shows an account. The waivers on 3.2 to 3.6 cover every rendered report." Preamble sentence 2 (2026-10-04) excludes net worth and wealth history because "3.3, 3.4 and 3.6 describe trial-balance account rows". That reasoning applies equally to period activity and pre-posting review. REQ-RPT-3.6 requires three labeled values on each account row: total credits, total debits and net balance. A period activity account row carries one net total (REQ-RPT-6.1). A pre-posting review line carries one line amount (REQ-RPT-7.2). Neither can satisfy 3.6. The code confirms this. In Src/Ui.InterfaceBridge/ReportWriters/PeriodActivityWriter.fs, accountElement renders only an "acct" div with a "Net" tab, so there is no generation/level CSS class (3.3) and no credits/debits/net trio (3.6). In PrePostingReviewWriter.fs, lineElement uses Class "val" with no sign class (3.4). Only TrialBalanceWriter.fs emits `level-{generation}` and the pos/neg/zero classes with the Credits/Debits/Net Balance labels. The Waived table says 3.3, 3.4 and 3.6 are "verified by code review and visual inspection of rendered output" for every rendered report. For the PA and PPR reports, that verification cannot pass.

**Action:** Amend the §3 preamble so that 3.3, 3.4 and 3.6 apply to the trial balance only, as the 2026-10-04 sentence already reasons. Alternatively, Dan rules which of them apply to the BSI, PA and PPR rows, and the waiver rows are restated for that scope.

**Why:** Waived requirements are enforced only by code review. A scope statement that the code visibly does not meet makes the waiver's "enforced" claim false, and no test will expose it. The two preamble sentences, a day apart, give opposite readings of what 3.3, 3.4 and 3.6 are about.

---

## RPT-3.1-IWH — contradiction
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-3.1, §3 preamble (2026-10-04 sentence), REQ-RPT-9.5
- **Summary:** REQ-RPT-3.1, which the preamble applies to investment wealth history, requires the header to show the as-of Calendar Date. The report has no as-of date, and 9.5, unlike 6.4 for period activity, never says what the header shows instead.
- **Resolution:** fix-spec

The preamble says "For net worth (§8) and investment wealth history (§9), REQ-RPT-3.1, 3.2 and 3.5 apply". REQ-RPT-3.1 reads: "header section displaying the report title and the as-of Calendar Date." Investment wealth history takes a begin date and an end date (REQ-RPT-9.1) and has no as-of date. For the same situation in period activity, REQ-RPT-6.4 explicitly overrides the header ("the rendered header (REQ-RPT-3.1) shows the range"), and REQ-RPT-7.7 does the same for pre-posting review. REQ-RPT-9.5 has no such clause. The code and test have filled the gap on their own. InvestmentWealthHistoryWriter.fs builds a "From {begin} to {end}, by {grouping}" subtitle. The test Tests/Tests.Integrated/InterfaceBridge/PositionsReportRoutes.fs:238 ("REQ-RPT-9.5 REQ-RPT-3.1 the rendered wealth history shows the report title and the begin and end dates in its header") cites 3.1 for a behavior 3.1 does not state.

**Action:** Add to REQ-RPT-9.5 the clause that 6.4 uses: the rendered header (REQ-RPT-3.1) shows the begin and end dates of the range. Optionally also the grouping, which the code already shows.

**Why:** A test currently claims to verify REQ-RPT-3.1 while asserting something 3.1 does not say. The requirement as written cannot be satisfied for this report. The sibling range reports (6.4, 7.7) already use the override pattern; §9 is missing it.

---

## RPT-8.5-LIAB — ambiguity
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.5 (totals bullet) vs REQ-RPT-8.2
- **Summary:** The "liabilities" total in REQ-RPT-8.5 can mean either all Liability accounts subtracted under 8.2, or only the listed non-mortgage liabilities. The code picks the second, so the printed totals do not add up to the printed net worth.
- **Resolution:** dan-decides

REQ-RPT-8.2 subtracts "the net balance ... of every Liability account", owned-property mortgages included. REQ-RPT-8.5 lists "each Liability account not a mortgage Account of a Property owned on the date" separately from the property mortgages, then requires "totals: counted ledger assets, investments, property values, liabilities, net worth". The bullet does not say whether "liabilities" means the 8.2 quantity or the sum of the listed liability rows. Reasonable developers would split on this. In Src/Business.CrossDomainOrchestration/NetWorth.fs, totalLiabilities sums only liabilityAccounts, which excludes ownedMortgageIds. Net worth is computed separately from allLiabilities = totalLiabilities + totalMortgages. The test at Tests/Tests.Integrated/CrossDomainOrchestration/NetWorth.fs:211 pins totalLiabilities to the hand-summed listed rows. NetWorthWriter.fs totalsBlock then prints "Counted ledger assets", "Investments", "Property values", "Liabilities" and "Net worth" one after another. Whenever an owned Property has a non-zero mortgage, the first four lines do not combine to the fifth. No mortgage total appears in the totals block, so a reader cannot see why.

**Action:** Dan states in REQ-RPT-8.5 which liabilities total is meant. Either all Liability accounts per 8.2, or non-mortgage liabilities with a separate mortgage total, so that the listed totals reconcile to net worth.

**Why:** The totals section of a net-worth statement is read as an arithmetic identity. A total named "liabilities" that silently omits the largest household liability (the mortgage) misleads anyone reconciling the report by eye. The spec wording supports both implementations.

---

## RPT-8.2-LINKED-NOVALUE — contradiction
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.2 and its "Why linked accounts are left out"; Positions.md design note "relationship to the ledger"
- **Summary:** REQ-RPT-8.2 drops every linked Asset account's balance. Its stated reason, that the market value is "already counted from Positions", is false when the linked Investment Account has no qualifying snapshot, is not active on the date, or the linked Property is not owned on the date. In those cases the ledger amount vanishes from net worth with nothing replacing it.
- **Resolution:** dan-decides

The rule says to exclude "every Asset account that is not linked to an Investment Account or a Property", unconditionally. The Why says: "a linked ledger account carries an investment or a property at cost; its market value is already counted from Positions." The Positions design note describes the link the same way, as replacing the cost balance with the market value. Under REQ-POS-8.1, however, an Investment Account contributes market value only when it is active on the date and has a snapshot on or before it. Under REQ-POS-9.4, a Property contributes only when owned on the date. Example: an Investment Account linked to ledger account X that carries cost from 2024, with its first snapshot dated 2026-09. Net worth as of 2026-03 (inside a fiscal period) excludes X's cost balance and adds no market value, so the holding is counted at zero. The code does exactly this. NetWorth.fs builds linkedAssetIds from all InvestmentAccounts and Properties regardless of holdings or ownership. The spec has already handled the analogous asymmetry on the liability side (the 2026-10-04 amendment to 8.5 keeps a disposed Property's mortgage in net worth). For the asset side, the rule and its rationale disagree, and no statement says the drop is intended.

**Action:** Dan rules on the asset side. Either keep the unconditional exclusion and reword the Why to admit the no-snapshot / not-owned case, or limit the exclusion to links whose Investment Account is included in the holdings as of the date, or whose Property is owned on the date.

**Why:** A requirement's rationale is how later readers judge edge cases. Here the rationale promises "nothing is counted twice", but the rule also produces "something is counted zero times". Historical dates before snapshots began are exactly where that happens.

---

## RPT-9.2-GROUPVALUES — insufficient-elaboration
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-9.2, REQ-RPT-9.5
- **Summary:** "Totalled ... for each value of the grouping" does not say whether a point carries every possible value (all four tax treatments, every Dimension Value) or only values present at that point. It also does not say how a group absent at a point is reported, as zero or omitted, in the data or in the table's "one column per group value".
- **Resolution:** fix-spec

REQ-RPT-9.2: "At each point, the holdings as of that date are totalled by market value for each value of the grouping, with a total across all of them." REQ-RPT-9.4 sets a zero only for the grand total of an empty point. REQ-RPT-9.5: "one column per group value." Two reasonable implementations give observably different output. (a) Each point lists every value of the grouping, zero-filled, so for tax treatment there are always four entries. (b) Each point lists only the groups with holdings. The code does (b). In InvestmentWealthHistory.fs, pointAt groups only the values present. InvestmentWealthHistoryWriter.fs builds columns from the union of groups seen at any point and renders an absent group as an empty cell, not 0.00. The test at InvestmentWealthHistory.fs:155 asserts "one total per tax treatment present". The data-only JSON shape, the column set and the empty-vs-zero cell all depend on a choice the spec does not make.

**Action:** Amend REQ-RPT-9.2/9.5 to state that a point carries only group values with holdings at that point, that the table's columns are the union across points, and that a group absent at a point is shown as blank (or zero), whichever Dan intends.

**Why:** This is the data series the future retirement-planning engine and growth charts read. Whether a missing group means "zero" or "not reported" changes how a consumer interpolates. Today the behavior is fixed only by the implementation and its test, not by the spec.

---

