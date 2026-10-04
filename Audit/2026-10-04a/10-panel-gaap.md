# gaap-domain-expert

## GAAP-FP-1 — gaap-gap
- **Location:** Specs/Behavioral/FiscalPeriodCrud.md REQ-FP-4.1, REQ-FP-4.2, REQ-FP-2.7; Specs/Behavioral/JournalEntryCrud.md REQ-JE-2.7, REQ-JE-4.5; Src/Business.FinancialServices.Ledger/FiscalPeriod.fs (update, lines 144-160); Src/Ui.InterfaceBridge/Routes/FiscalPeriodRoutes.fs close/reopen
- **Summary:** Each period opens and closes on its own, with no rule on order, so closing a month does not freeze its ending balances: posting or voiding in an earlier month that is still open changes them.
- **Resolution:** dan-decides
- **Prior ruling:** GAAP-CLOSE (deferred, 2026-08-02) says that closing a fiscal period is a posting lock only, and that the lack of GAAP closing entries is not to be flagged. This finding does not ask for closing entries. It says the posting lock, as specified, does not lock the closed period's ending balances, because the lock has no order across periods. That is a different point, so it is raised again.

Balances are cumulative since inception (REQ-AC-3.13.1, REQ-RPT-1.9, and the domain-terminology article). REQ-JE-2.7 and REQ-JE-4.5 gate posting and voiding only on the open flag of the entry's own derived period. REQ-FP-4.1 and REQ-FP-4.2 close and reopen any period by key, with no condition on its neighbours. FiscalPeriod.closeFiscalPeriod and reopenFiscalPeriod (called from FiscalPeriodRoutes.fs:62/72) only flip is_open. REQ-FP-2.7 creates any missing period open, including back-filled history months. So September can be closed while August is open (or August can be reopened later). An August post, or an August void, then changes the cumulative balance as of 30 September for every Asset, Liability and Equity account, even though September is 'closed'. A reconciliation (REQ-RPT-4.1) or trial balance (REQ-RPT-1.1) taken at the September close then no longer reproduces. Under GAAP, closing a period makes its ending balances final, and that only holds when no earlier period can still accept postings. The period-close and trial-balance work Dan named as next will rest on closed-period ending balances being stable. Today nothing guarantees that.

**Action:** Dan to decide: (a) add REQ-FP requirements that a period can close only when every earlier period is closed, and can reopen only when every later period is open; or (b) record in FiscalPeriodCrud.md that close is a posting lock on that month's own entries only, and that its cumulative ending balances can still move.

**Why:** Period close is next on the roadmap. Retained-earnings sweeps, closed-period reconciliation and period-over-period comparisons all assume a closed period's ending balances are final. If the lock is per month without order, closing entries computed at year end can be invalidated silently by a back-dated post to an earlier month that is still open.

---

## GAAP-AC-1 — gaap-gap
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-4.1, REQ-AC-4.4; Src/Business.CrossDomainOrchestration/AccountDeactivation.fs confirmZeroBalanceBeforeDeactivation (lines 47-65)
- **Summary:** There are no closing entries, so a Revenue or Expense account keeps its lifetime balance, and under REQ-AC-4.4 any such account that has ever been used can never be deactivated.
- **Resolution:** dan-decides
- **Prior ruling:** AMB-AC-2 (overruled) settles what balance means in REQ-AC-4.4, and this finding accepts that meaning. GAAP-CLOSE (deferred) covers the absence of closing entries. This finding is about a consequence that exists today: REQ-AC-4.1 and REQ-AC-4.4 together make deactivation unavailable for nominal accounts. Neither ruling addresses that, so it is raised again.

REQ-AC-4.4 rejects deactivation when the account has a non-zero balance. Per the AMB-AC-2 ruling, balance means the cumulative net of all non-voided lines since inception. confirmZeroBalanceBeforeDeactivation sums every non-voided line with no date bound. REQ-RPT-5.2's own rationale states that 'with no closing entries, lifetime net income sits in the revenue and expense accounts'. So a used expense category (for example an old daycare or utility account) or a revenue account (for example a sold rental's rent income) carries a non-zero lifetime balance forever. REQ-AC-4.1 promises a means to deactivate any Account, but for nominal accounts that path is closed. The only way to retire one today is a manual entry moving its balance to Equity, which is a closing entry done by hand outside any spec. Under GAAP, temporary accounts are zeroed every fiscal year, and the zero-balance retirement rule is meant for permanent (balance-sheet) accounts.

**Action:** Dan to decide: either restrict REQ-AC-4.4 to Asset, Liability and Equity accounts and state that Revenue and Expense accounts may be deactivated with a lifetime balance, or state in AccountCrud.md that nominal accounts stay undeactivatable until closing entries exist (and tie that to the GAAP-CLOSE trigger).

**Why:** Chart-of-accounts upkeep (retiring obsolete categories so they stop appearing as posting targets and as classification-rule targets) is impossible for exactly the accounts that change most often. Otherwise the operator is pushed into ad hoc equity entries that a later closing-entries slice would have to recognise and work around.

---

## GAAP-NW-1 — gaap-gap
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.2, REQ-RPT-8.5; Specs/Behavioral/Positions.md REQ-POS-8.1, REQ-POS-9.4; Src/Business.CrossDomainOrchestration/NetWorth.fs computeNetWorth (lines 99-112)
- **Summary:** Net worth always drops a linked ledger account's cost balance, but adds market value only when the investment account has a snapshot on or before the date, or the property is owned on it, so value can vanish with no warning.
- **Resolution:** fix-spec

In NetWorth.fs, linkedAssetIds is built from every InvestmentAccount and every Property, with no condition on the date. Every such ledger account is removed from assetAccounts. The replacement value comes only from holdings as of the date (REQ-POS-8.1: an investment account that is active on the date and has a snapshot on or before it) and from ownedProperties (REQ-POS-9.4). Cases where both sides drop out: (1) a brokerage account funded through the ledger (Debit the linked Investment asset, Credit checking) before its first snapshot. Checking falls, the linked account is excluded and there is no holdings line, so net worth falls by the amount transferred. The same happens for any date before the first snapshot that historical import supplies, while the ledger did carry cost. (2) An earnest-money deposit or closing cost booked to a Property's linked FixedAsset account before the acquisition date. (3) A linked account that ended while its ledger cost was not yet cleared. REQ-RPT-8.5 lists only the included investment accounts and owned properties, so the omitted value leaves no trace on the report. The Positions design note says the link's one job is that the cost balance is 'replaced by the market value Positions holds, so nothing is counted twice'. As implemented, it can also mean nothing is counted at all.

**Action:** Amend REQ-RPT-8.2: a linked account's ledger balance is excluded only when its replacement value is counted on the date (an investment account included in the holdings as of the date; a property owned on the date). Otherwise the ledger balance is counted, or the account is listed as excluded without replacement. Add a test for each case.

**Why:** Net worth is a GAAP-adjacent statement of financial position, and its data feeds the retirement-planning engine. An omission is worse than a double count here: it is silent, and in a history series it shows up as fake drops in wealth at the date an account was funded or a property bought.

---

## GAAP-NW-2 — gaap-gap
- **Location:** Specs/Behavioral/Reporting.md REQ-RPT-8.2; Specs/Behavioral/Positions.md REQ-POS-4.8, REQ-POS-9.7; Specs/Behavioral/AccountCrud.md REQ-AC-2.20; Src/Business.CrossDomainOrchestration/NetWorth.fs (lines 99-112)
- **Summary:** Only the linked ledger account itself is left out of net worth, so cost carried in that account's child accounts is still counted as well as the Positions market value.
- **Resolution:** dan-decides

REQ-RPT-8.2 counts 'every Asset account that is not linked' at 'each account's own balance only, with no roll-up into parents, so no amount is counted twice'. NetWorth.fs filters on the exact linked IDs (linkedAssetIds), with no handling of descendants. REQ-AC-2.20 requires only that a child's type matches its parent's, so an Asset/Investment parent (for example 'Fidelity') can have Asset children (for example 'Fidelity Roth', 'Fidelity cash'). REQ-POS-4.8 and REQ-POS-9.7 check only the linked account's own type and subtype, and allow linking a parent. If Dan links the parent and books cost in its children, the children's cost balances are counted and the snapshot market value is counted too. The double count the no-roll-up wording was written to prevent then happens through the hierarchy instead.

**Action:** Dan to decide: either require a linked account to have no children (a REQ-POS rule enforced at link time and at child creation), or amend REQ-RPT-8.2 to exclude the linked account together with all its descendants. Then cover it with a test.

**Why:** A chart of accounts that groups an institution's accounts under one parent is a normal pattern, and the double count would inflate both net worth and investable wealth by the full cost basis without any sign on the report.

---


