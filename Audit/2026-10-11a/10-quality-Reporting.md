# Reporting.md spec-quality auditor

## RPT-8.8-ABSENT-COUNTED — contradiction
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.8 (line 134) and its Why (line 135); Src/Business.CrossDomainOrchestration/NetWorth.fs:162-173, 234-239
- **Summary:** REQ-RPT-8.8 gives two different tests for when the counted-ledger-assets component is absent, and its Why says a ledger component is never absent on a fiscal-period date. That claim is false under the rule's own liabilities clause, and under the code.
- **Resolution:** fix-spec

REQ-RPT-8.8 first says a component is absent "when nothing contributes to it on the date". It then spells the asset case out as "no Asset account has a balance (counted ledger assets)". The two tests differ. Under REQ-RPT-8.2, an Asset account linked to an Investment Account or a Property contributes nothing to counted ledger assets. Under REQ-RPT-8.7, on a fiscal-period date that same account still has a ledger balance. So on a fiscal-period date where every Asset account is linked, the first test makes the component absent and the parenthetical test does not. The code follows the first test: `CountedLedgerAssets, assetAccounts |> List.isEmpty`, where assetAccounts has already dropped linkedAssetIds (NetWorth.fs:162-166, 235). The liabilities clause is written in terms of what contributes ("no Liability account other than an owned Property's mortgage has a balance"), so the two halves of the bullet are also inconsistent with each other.

The Why then says: "On a date within a fiscal period every account has a ledger balance, so neither ledger component is absent while the chart of accounts has accounts of that type." That is false in two cases. (a) The chart has Asset accounts but all of them are linked: the code names CountedLedgerAssets absent. (b) The chart has Liability accounts but every one is a mortgage Account of a Property owned on the date: the rule's own text, and the code (NetWorth.fs:168-171, 238), name Liabilities absent. On pre-ledger dates the difference is moot, because REQ-POS-15.3 and 15.8 keep a linked account from carrying a Pre-ledger Balance. On fiscal-period dates it is observable in the absentComponents output, and that output feeds REQ-RPT-10.2 net worth history.

**Action:** Change REQ-RPT-8.8's asset parenthetical to "no Asset account counted under REQ-RPT-8.2 has a balance". Amend the Why to "...neither ledger component is absent while the chart of accounts has a counted Asset account, or a Liability account that is not an owned Property's mortgage". Both changes match NetWorth.fs as it stands.

**Why:** A rationale that contradicts its own rule is what the next developer or auditor reads first. A future edit that "fixes" the code to match the Why, or the parenthetical, would change which components are flagged absent. Nothing would be wrong enough to fail loudly; the report's completeness marker would just quietly stop telling the truth.

---

## RPT-8.5-ORDER — insufficient-elaboration
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.5 (lines 124-132), REQ-RPT-9.2 (line 143); Src/Business.CrossDomainOrchestration/NetWorth.fs:125-130, 159, 167, 173, 205; InvestmentWealthHistory.fs:52, 76
- **Summary:** REQ-RPT-8.5 lists several collections in the net worth result but never says what order they come in. REQ-RPT-9.2 is likewise silent on the order of group totals within a point. Every other list-returning report in this spec, and holdings as of a date, pins its order.
- **Resolution:** dan-decides

REQ-RPT-8.5 returns several lists: counted Asset accounts, listed Liability accounts, Investment Accounts, owned Properties with their mortgage Accounts, totals by tax treatment, and totals by account group. REQ-RPT-9.2 returns one total per grouping value at each point. Neither requirement states an order. Elsewhere this spec always does: trial balance (REQ-RPT-1.6), period activity (REQ-RPT-6.3), pre-posting review (REQ-RPT-7.6), and Positions holdings as of a date (REQ-POS-8.4). The code makes undocumented choices:
- ledger accounts and mortgage Accounts by code (NetWorth.fs:167, 173, 205);
- Properties by name (line 159);
- Investment Accounts in holdings order, which is by name (via REQ-POS-8.4);
- tax-treatment totals in the TaxTreatment union's declaration order, from `List.sortBy fst` on a DU (line 130);
- account-group totals by ordinal string order;
- wealth-history group values by structural comparison of the WealthGroup union (GroupName before GroupOwners before Unassigned, InvestmentWealthHistory.fs:52, 76).
A second developer could reasonably sort accounts by balance, or tax treatments alphabetically. That is observable in data-only JSON and in the rendered tables. No test pins any of these orders except where a test compares against the computation itself, so a reordering would pass.

**Action:** Dan decides whether order matters for these outputs. If it does, append to REQ-RPT-8.5 that ledger and mortgage accounts are ordered by code, and Investment Accounts and Properties by name. Specify the tax-treatment total order (e.g. the REQ-POS-4.4 enumeration order) and the account-group order. Append the group-value order within a point to REQ-RPT-9.2. Then add the matching tests. If order is deliberately unspecified, say so in the spec, so that tests and auditors do not treat the current order as a contract.

**Why:** Ordering is observable behaviour in a report's data contract and rendered page. This spec treats it as a requirement everywhere else, so leaving it out here is an elaboration gap that two implementers would fill differently.

---


