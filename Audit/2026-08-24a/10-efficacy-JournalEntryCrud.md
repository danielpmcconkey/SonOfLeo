# JournalEntryCrud Test-Efficacy Auditor

## IDIOM-JE-2 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Ledger/JournalEntryComponent.fs, lines 121-146 (REQ-JE-1.25)
- **Summary:** The fromString "Credit" acceptance test asserts only Result.isOk; no test in the suite verifies the returned value is the Credit DU case.
- **Resolution:** fix-test

Five tests cite REQ-JE-1.25 ("entry type must be one of Debit or Credit"). The two acceptance tests (lines 121-126) each assert only Assert.True(Result.isOk ...). The round-trip test at line 139 value-verifies only the Debit case: it sends Debit through toString then fromString and asserts the round-tripped value equals Debit. No corresponding round-trip or value assertion exists for Credit. If fromString returned Ok Debit for all valid inputs, the accepts-Credit test (isOk) would pass, the Debit round-trip would pass, and the two rejection tests ("Refund" and "debit") are irrelevant to this failure mode. The integration tests would catch this incidentally because a journal entry with all-Debit lines cannot satisfy the balanced-entry check (REQ-JE-1.13), but the isolated test — the lowest layer for this verification — does not catch it on its own.

**Action:** Add a value assertion to the Credit acceptance test (e.g. match the Ok arm and Assert.Equal(Credit, result)), or add a Credit round-trip test symmetric with the Debit one.

**Why:** Per the smell test: if fromString returned garbage of the right shape (Ok Debit for "Credit"), this test would pass. Acceptance of an input and correct mapping of that input are two different properties; the Debit case verifies both, the Credit case verifies only the first. The lowest-possible-layer principle says the isolated test should catch this, not a downstream integration test.

---

## COVER-JE-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/AccountActivity.fs (REQ-JE-3.9)
- **Summary:** REQ-JE-3.9 lists four enriched fields (entry_date, description, source, voided_at) but no test anywhere verifies the source enrichment on the returned AccountActivityDetail.
- **Resolution:** fix-test

REQ-JE-3.9 requires that retrieved journal entry lines be "enriched with their parent entry's entry_date, description, source, and voided_at." The AccountActivityDetail type (Src/ModelOrchestrator/AccountActivity.fs line 26) carries journalEntrySource as an Option field, and the SQL query includes je_source (line 56 of that file). However, across all ten REQ-JE-3.9-citing tests in AccountActivity.fs and AccountRoutes.fs, no assertion reads or checks the journalEntrySource field on any returned row. Description is verified by the filter-by-description test (line 260), voidedAt is verified by the unVoidedOnly test (line 52), and entry_date is exercised by the sort tests (line 166). Source is absent. A bug in the SQL join that omitted or mis-mapped je_source would leave every test green. The route-level validation test for source (AccountRoutes.fs, InlineData "source", "") tests that an empty source filter is rejected, not that source values are correctly enriched in results.

**Action:** Add an assertion in one of the existing REQ-JE-3.9 tests that checks journalEntrySource on a returned activity detail against the fixture entry's known source value. The fixture would need at least one entry with a non-null source; if one exists, use it, otherwise add a fixture entry with a source.

**Why:** REQ-JE-3.9 explicitly names source as an enriched field. Three of the four enriched fields are verified somewhere in the test suite; source is the exception. Per the smell test, garbage or null in the source column would not cause any test to fail.

---
