# SystemWide Test Efficacy Auditor

## SYS-5.1-PARTIAL — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs, line 61 — REQ-SYS-5.1
- **Summary:** Account REQ-SYS-5.1 round-trip test asserts only 2 of 10 entity properties, failing the smell test for the 8 unchecked fields.
- **Resolution:** fix-test

The test `REQ-AC-2.14 REQ-SYS-5.1 create account and fetch by ID returns identical record` creates an Account with known values (code="AC-2.14", name=custom, type=genericAccountType (Revenue), activityPeriod=genericAccountActivityPeriod, subtype=None, parentId=None, reference=None), fetches it by ID, and asserts only `code` and `accountName`. The Account entity has 10 properties: accountId, code, accountName, accountType, activityPeriod, accountSubType, parentId, externalReference, createdAt, modifiedAt. Eight are unchecked.

REQ-SYS-5.1 states: "The persistence layer must persist all entity properties in such a way that the entity type can be perfectly reconstituted upon subsequent read." The resolved finding AMB-6 confirms "perfectly" is the intended standard.

The other two REQ-SYS-5.1 tests in the codebase set the bar: the Comment round-trip (JournalEntryComment.fs line 219) checks all 6 Comment fields, and the ExternalReference round-trip (JournalEntryExternalReference.fs line 195) checks all 6 ExternalReference fields. The Account test falls below the standard those siblings establish.

Smell test: if fetchById returned garbage for accountType, activityPeriod, accountSubType, parentId, externalReference, createdAt, or modifiedAt while preserving code and name, this test would pass green.

Additionally, all three optional fields (subtype, parentId, externalReference) are created as None. Even if checked, asserting None round-trips to None only tests the null path. A full-fidelity test would populate at least one optional field with a non-None value and verify it survives the trip. The generic helpers `genericAccountSubtypeNonNull` and fixture parent IDs already exist for this purpose.

**Action:** Expand the test body to assert all 10 Account properties against their known input values. Populate at least one optional field (e.g., parentId from a fixture account, subtype via genericAccountSubtypeNonNull, externalReference via a test string) to exercise the non-None persistence path.

**Why:** A persistence-fidelity test that checks 20% of an entity's fields cannot catch 80% of the reconstitution bugs it exists to prevent. The test name claims "returns identical record" and the requirement demands "all entity properties" -- the body must match both claims or it provides false coverage confidence.

---
