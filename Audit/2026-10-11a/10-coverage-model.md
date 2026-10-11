# code-outward-coverage-business-tier

## DEAD-LEDGER-FP-1 — maintainability
- **Location:** Src/Business.FinancialServices.Ledger/FiscalPeriod.fs:193 (let idToKey = App.DataAccessLayer.LookupCache.idToStringCache "ledger.fiscal_period" "period_key")
- **Summary:** FiscalPeriod.idToKey is a public lookup cache that nothing in Src, Tests or DevDataStage uses. Its only user was a DAL test that commit 00277ef rewrote.
- **Resolution:** fix-code

`grep -rlw FiscalPeriod.idToKey` over Src, Tests and DevDataStage returns nothing. An unqualified `idToKey` appears only on its own definition line, FiscalPeriod.fs:193. Its sibling caches are all in use: FiscalPeriod.keyToId (FiscalPeriodFieldConverters.fs, SharedContractConverters.fs, two test files), Account.idToCode, Account.idToName, Account.codeToId, and MasterAgreement/PaymentAgreement nameToId/idToName (CashFlowLookupConverters.fs). The last caller ran until 2026-10-05. At commit 28c1155, Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:284 called `Business.FinancialServices.Ledger.FiscalPeriod.idToKey.fetch noTransaction missingId`. Commit 00277ef (2026-10-05) removed that line while rewriting the DAL tests. That rewrite followed disposition 2026-10-04a #041 (EFF-DAL-2.4-1), which says the REQ-DAL-2.4 test should build fresh caches through the LookupCache factories rather than use the process-wide module caches. Since then no route, converter, orchestrator or test resolves a fiscal period ID back to its key through this cache. I found this with a mechanical sweep of every public `let` in the business-tier scope. The sweep counted qualified uses, and unqualified uses in files that open the module. idToKey was the only binding with no user, apart from each error module's toMessage/toAppError boilerplate. This is not a re-raise: no resolved-findings.md entry covers it. The disposition precedent is 2026-10-04a #013 (DEAD-POS-1, accepted: delete Security.dimensionValueIn) and 2026-10-03a #017 (COV-DEAD-1): delete public business-tier functions that have no Src caller and no test.

**Action:** Delete FiscalPeriod.idToKey (FiscalPeriod.fs:193). If Dan wants it kept for later use, add a caller or a test that exercises it. Optionally, update the LookupCache row in Src/README.md if it lists this cache.

**Why:** FiscalPeriod.idToKey is public surface that no code uses and no test checks. It also creates a process-wide static cache over ledger.fiscal_period that would never be invalidated, and the 2026-10-04a DAL efficacy finding showed that kind of cache can make tests depend on run order. The precedent in #013 and 10-03a #017 limits the business tier's public API to behaviour that something actually uses.

---


