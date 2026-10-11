# test-efficacy-FiscalPeriodCrud

## EFF-FP-1 — test-gap
- **Location:** REQ-FP-3.6; Src/Ui.InterfaceBridge/Routes/JournalEntryRoutes.fs:52-60 (fetchByPeriod); Src/Ui.InterfaceBridge/BoundaryConverters/FiscalPeriodFieldConverters.fs:25-32; Tests/Tests.Integrated/InterfaceBridge/JournalEntryRoutes.fs:247 (only FetchByPeriod route test)
- **Summary:** REQ-FP-3.6 says any operation that resolves a period by key must return a typed error for an unknown key, but the JournalEntry FetchByPeriod route resolves a period by key and has no test for an unknown key.
- **Resolution:** fix-test

REQ-FP-3.6: "When the caller provides a period key that does not match any existing fiscal period, the system must produce a typed error. This applies to any operation that resolves a period by key (retrieve, close, reopen)." Three Src paths resolve a caller-supplied period key: (a) the FiscalPeriod FetchByKey/Close/Reopen routes, through `convert FiscalPeriodKeyString to FiscalPeriodId`; (b) the Account activity temporal filter (SharedContractConverters.fs:20-28); (c) JournalEntry FetchByPeriod (JournalEntryRoutes.fs:56), through `convert [FiscalPeriodKeyString] to FiscalPeriod`. Tests assert the typed error FiscalPeriodNoPeriodMatchingKey for (a) at FiscalPeriodRoutes.fs:180/193/206 and for (b) at AccountRoutes.fs:531 (InlineData "periodKey:1974-03"). No test sends an unknown key to (c). A grep of Tests/ for FetchByPeriod finds only happy paths: JournalEntryRoutes.fs:247, JournalEntryCommentsAndReads.fs:155, and JournalEntryFetching.fs:104/130, which take a FiscalPeriod value, not a key. Under CompoundedLearnings/articles/testing/failure-vector-is-a-user-interaction.md, "a caller fetching journal entries for a period that doesn't exist" is its own user interaction. That article also asks for one InterfaceBridge case per (input field, converter) pair. The converter happens to be shared with the FiscalPeriod routes today, so the code would behave correctly now. But nothing pins that FetchByPeriod reaches the converter, or that it does not get a raw DAL ExactlyOne error instead. That is the failure Specimen 4's August 2026 note describes for an unknown bank name. Dan said error cases that only reject loudly are untested by design. This REQ demands a typed error, not just a loud rejection, so that exemption does not cover it.

**Action:** Add a JournalEntryRoutes test, citing REQ-FP-3.6 and REQ-JE-3.3, that sends FetchByPeriod a well-formed key with no period (e.g. "1850-01") and asserts the typed FiscalPeriodNoPeriodMatchingKey case, with both escape arms.

**Why:** The REQ names a class of operations, not just the three FiscalPeriod verbs. Coverage of that class is cited, but one member of the class is unexercised, so a regression to an untyped DAL error on that route would stay green.

---

## EFF-FP-2 — test-gap
- **Location:** REQ-FP-1.2; Src/Ui.InterfaceBridge/Routes/FiscalPeriodRoutes.fs:31-32 (ensure parses startPeriodKey/endPeriodKey); Tests/Tests.Integrated/InterfaceBridge/FiscalPeriodRoutes.fs:219, 232
- **Summary:** The FiscalPeriod Ensure route parses two caller-supplied period keys, startPeriodKey and endPeriodKey, but no test sends either one malformed and asserts FiscalPeriodInvalidKeyString.
- **Resolution:** fix-test

REQ-FP-1.2 requires a key to match YYYY-MM with month 01-12. The constructor is tested in isolation (Tests.Isolated/Model/Ledger/FiscalPeriod.fs:31, an 8-row theory with a typed match on FiscalPeriodInvalidKeyString). At the route layer, only the Create route's periodKey has a malformed-key case (FiscalPeriodRoutes.fs:219, payload "abc"). The Ensure route has its own parse calls: `input.startPeriodKey |> FiscalPeriodKey.fromString` and `input.endPeriodKey |> FiscalPeriodKey.fromString` (FiscalPeriodRoutes.fs:31-32). Its only test (FiscalPeriodRoutes.fs:232) is a happy path. The orchestration-level typed-error test (CrossDomainOrchestration/FiscalPeriodCreation.fs:115) covers start-after-end only; it builds keys with `keyOf`, which throws on a bad key, so it can never reach the parse. Under failure-vector-is-a-user-interaction.md ("One InterfaceBridge case per (input field, converter) pair, asserting the exact AppError case. It proves the converter is reached and that the error identifies the right field"), these are two uncovered route-level vectors. The same applies to the malformed-key branch of `convert FiscalPeriodKeyString to FiscalPeriodId` on FetchByKey, Close and Reopen: every route test there uses the well-formed "1850-01", so only the not-found branch is reached.

**Action:** Add a route theory for FiscalPeriod Ensure: one row with a malformed startPeriodKey and one with a malformed endPeriodKey, each asserting the typed FiscalPeriodInvalidKeyString case and that nothing was created. Optionally add a malformed-key row for each of FetchByKey, Close and Reopen.

**Why:** A correct constructor says nothing about whether a route calls it (the ingestRawEntries precedent in the cited article). The Ensure route is the Saturday-run entry point, and its key parsing is unexercised by any failing input.

---

## EFF-FP-3 — test-gap
- **Location:** REQ-FP-3.5; Tests/Tests.Integrated/Model/Ledger/FiscalPeriod.fs:150-163 (lines 156 and 160)
- **Summary:** The REQ-FP-3.5 open-only fetch test compares key sets, which discards how many times each key appears, so a fetch that returned each open period twice would pass.
- **Resolution:** fix-test

Expected: `fixture.Data.fiscalPeriods |> List.filter FiscalPeriod.isOpen |> List.map(...) |> Set.ofList` (line 156). Actual: `Assert.Equal<Set<string>>(expectedKeys, fetched |> List.map(...) |> Set.ofList)` (line 160). Set.ofList collapses duplicates, so `fetchAll context true` could return duplicated rows (e.g. from a join added to the shared `query`) and the test would stay green. This is the weakness Specimen 2 calls out ("It tolerates duplicates"). The sibling test REQ-FP-3.4 (line 136-147) avoids it: it compares sorted lists (`Assert.Equal<string list>(expectedKeys, ... |> List.sort)`), which keeps multiplicity. The extra `Assert.DoesNotContain(fixture.Data.closedFiscalPeriodId, ...)` at line 159 adds nothing beyond the set equality and does not restore the count. No route test covers openOnly=true; that is acceptable as a pass-through parameter, but it means this is the only test of the open-only filter.

**Action:** Change lines 156 and 160 to compare sorted lists (`List.sort` on both sides with `Assert.Equal<string list>`), as the REQ-FP-3.4 test does.

**Why:** "Exactly the fixture's open periods", as the test name says, means each one once. Set equality checks membership, not exactness, so the name promises more than the assertion checks.

---

