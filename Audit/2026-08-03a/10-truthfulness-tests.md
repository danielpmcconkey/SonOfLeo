# test-truthfulness-auditor

## TT-SYS51-ACCT — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs line 63, REQ-SYS-5.1
- **Summary:** The Account round-trip test tagged REQ-SYS-5.1 never performs a subsequent read -- it only checks the in-memory return from constructNewAndSaveToDb.
- **Resolution:** fix-test

The test `REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record` at Account.fs:63 asserts code and name on the value returned by AccountCreation.constructNewAndSaveToDb. However, constructNewAndSaveToDb (at Src/ModelOrchestrator/AccountCreation.fs:115) returns the in-memory validAccount object directly (`return validAccount`); it never reads back from the database. The test body contains no Account.fetchById call, so no round-trip through persistence is verified. The test name promises 'create account and fetch by ID returns identical record' but no fetchById occurs. Compare with the comment and external-reference round-trip tests at JournalEntryComment.fs:221 and JournalEntryExternalReference.fs:129, which correctly create, then fetchById, then compare all fields. No other Account test performs a full write-then-read-then-compare cycle.

**Action:** Add a fetchById call after constructNewAndSaveToDb in the test body and compare all Account properties between the in-memory created entity and the fetched entity, following the pattern established in the JournalEntryComment and JournalEntryExternalReference round-trip tests.

**Why:** REQ-SYS-5.1 requires that persisted entities can be 'perfectly reconstituted upon subsequent read.' A test that never reads from the database cannot verify this. If a serialization bug corrupted a field during INSERT, this test would still pass because it only inspects the pre-INSERT in-memory object.


## TT-JE148-SCOPE — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/JournalEntryCreation.fs line 258, REQ-JE-1.48
- **Summary:** The test tagged REQ-JE-1.48 creates duplicate external references on the same entry, but the requirement addresses duplicates across different entries.
- **Resolution:** fix-test

REQ-JE-1.48 states: 'Duplicate (source_fi, reference) pairs across different journal entries are permitted (uniqueness is not enforced across entries).' The test `REQ-JE-1.48 constructNewAndSaveToDb accepts duplicate source_fi/reference pairs across entries` creates a single journal entry with explicitlySame = [('TestBank', 'F-SHARED-001'); ('TestBank', 'F-SHARED-001')] -- two identical refs on the SAME entry. This proves same-entry duplicate refs are not rejected, but says nothing about cross-entry uniqueness. A database unique constraint scoped to (source_fi, reference, journal_entry_id) would pass this test but violate REQ-JE-1.48. The cross-entry case IS covered elsewhere: the fixture (TestDataStage.fs lines 452-474) creates two separate entries with the same 'F-SHARED-001' reference, and `REQ-JE-3.5 REQ-JE-1.48 fetchByReference returns multiple entries when reference is shared` (JournalEntryFetching.fs:132) validates they both exist. The creation-level test is tagged and named incorrectly relative to what it actually verifies.

**Action:** Rename the test to reflect what it actually tests (same-entry duplicate refs are accepted), or rewrite it to create two separate journal entries with the same (source_fi, reference) pair and verify both are created successfully.

**Why:** A test that claims to verify cross-entry behavior but only exercises same-entry behavior gives false confidence. If a future migration added a cross-entry unique constraint, this test would not catch the regression.


## TT-JE410-CLOSED — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/JournalEntryExternalReference.fs, REQ-JE-4.10
- **Summary:** REQ-JE-4.10 explicitly permits appending references when the fiscal period is closed, but no test verifies the closed-period half.
- **Resolution:** fix-test

REQ-JE-4.10 states: 'A reference may be appended regardless of whether the entry is voided or its fiscal period is closed (mirrors REQ-JE-5.5 for comments).' The voided half is tested at line 111: `REQ-JE-4.10 appending a reference is permitted on a voided entry`. No test appends a reference to a journal entry whose fiscal period is closed. Contrast with the mirrored REQ-JE-5.5 (comment behaviors), which has BOTH halves tested: `REQ-JE-5.5 constructNewAndSaveToDb allows comment on a voided entry` at JournalEntryComment.fs:129 and `REQ-JE-5.5 constructNewAndSaveToDb allows comment when fiscal period is closed` at JournalEntryComment.fs:149. The fixture data includes fixture.Data.jeInClosedPeriodId, which could serve as the target entry for such a test.

**Action:** Add a test that appends an external reference to fixture.Data.jeInClosedPeriodId and asserts success, mirroring the pattern at JournalEntryComment.fs:149.

**Why:** The requirement explicitly calls out closed-period independence because it diverges from the general pattern (JE posting is blocked in closed periods). Without a test, a future developer adding a period-is-open guard to the reference-append path would not see any test fail.


## TT-JE49-STATE — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/JournalEntryExternalReference.fs, REQ-JE-4.9
- **Summary:** REQ-JE-4.9 explicitly permits reference updates regardless of voided status or closed period, but neither clause is tested.
- **Resolution:** fix-test

REQ-JE-4.9 states: 'The FI and value may be updated regardless of whether the entry is voided or its fiscal period is closed (mirrors REQ-JE-4.10 and REQ-JE-5.5).' Both existing REQ-JE-4.9 tests (lines 20 and 44) operate on fixture.Data.jeWithRefExtRefId, which belongs to jeWithRef -- an entry created yesterday in an open fiscal period that is not voided. Neither the voided-entry nor the closed-period clause is tested. This is a gap relative to the sibling requirements: REQ-JE-4.10 tests the voided half (line 111), and REQ-JE-5.5 tests both halves (JournalEntryComment.fs:129, 149).

**Action:** Add two tests: (1) update FI/value on an external reference belonging to a voided entry (use fixture.Data.voidedJeId after appending a ref, or create a new voided entry with a ref); (2) update FI/value on an external reference belonging to an entry in a closed fiscal period (use fixture.Data.jeInClosedPeriodId similarly).

**Why:** The requirement explicitly calls out state independence because it is a deliberate policy exception: posting and voiding are blocked in closed periods, but reference metadata updates are not. Without tests, these exceptions are unguarded against regression.
