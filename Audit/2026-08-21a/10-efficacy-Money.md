# money-efficacy

## TEST-MON-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Money.fs, line 220 (REQ-MON-2.5.1)
- **Summary:** REQ-MON-2.5.1 tests only the max-exceed boundary; the reachable min-exceed case via add is untested.
- **Resolution:** fix-test

REQ-MON-2.5.1 says 'the system will ensure the result is valid according to all rules stated in section 1.' Section 1 includes both REQ-MON-1.2 (max 9,999,999,999.99) and REQ-MON-1.3 (min -9,999,999,999.99). The test at line 220 sends maxMoney + 0.01M and asserts MoneyFailedToConvertExceededMax. The symmetric case — adding two large negatives that breach min (e.g., add(minMoney, -0.01M) producing -10,000,000,000.00) — is reachable and untested. Compare REQ-MON-2.9.1 (lines 283-304), which uses the identical 'all rules stated in section 1' language and tests both directions: one test for exceeding max, one for falling below min. The add test covers only one direction.

**Action:** Add a test: add(minMoney, fromDecimal(-0.01M)) must return Error(MoneyFailedToConvertBelowMin _), mirroring the pattern already established by the two REQ-MON-2.9.1 tests.

**Why:** The REQ makes an 'all rules' claim. Testing one boundary but not the other means a regression that dropped the min check from the add path would pass the suite. The in-file precedent (2.9.1) already demonstrates the standard: both directions tested.

---

## TEST-MON-2 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Money.fs, line 244 (REQ-MON-2.6.1)
- **Summary:** REQ-MON-2.6.1 tests only the min-exceed boundary; the reachable max-exceed case via subtract is untested.
- **Resolution:** fix-test

REQ-MON-2.6.1 says 'the system will ensure the result is valid according to all rules stated in section 1.' The test at line 244 sends subtractVal1FromVal2(0.01M, minMoney) and asserts MoneyFailedToConvertBelowMin. The symmetric case — subtracting a negative from a large positive to breach max (e.g., subtractVal1FromVal2(fromDecimal(-0.01M), fromDecimal(maxMoney)) producing 10,000,000,000.00) — is reachable and untested. Same comparison to REQ-MON-2.9.1 applies: the sister requirement tests both directions for the same 'all rules stated in section 1' language.

**Action:** Add a test: subtractVal1FromVal2(fromDecimal(-0.01M), fromDecimal(maxMoney)) must return Error(MoneyFailedToConvertExceededMax _).

**Why:** Mirror of TEST-MON-1. The add suite and subtract suite each test only one boundary direction while their sister requirement (2.9.1) tests both. A code change that broke max validation in the subtract path would not be caught.

---

