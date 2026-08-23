# money-efficacy-auditor

## AQ-MON-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Money.fs, lines 60-63 and 106-113; REQ-MON-2.3, REQ-MON-2.4
- **Summary:** REQ-MON-2.3 and REQ-MON-2.4 happy-path tests assert only Assert.True(result.IsOk) without examining the returned value.
- **Resolution:** fix-test

Two tests cite parent REQ IDs while providing no behavioral verification beyond 'the function did not error.'

REQ-MON-2.3 (line 60): calls fromDecimalList with [ -3.99M; 12.24M; 27194338M ] and asserts Assert.True(result.IsOk). If fromDecimalList returned Ok [] (empty list) or Ok [money(999.99M)] (wrong count, wrong values), this test passes. The function under test is never asked to prove it converted anything.

REQ-MON-2.4 (line 106): calls splitByN source 3 and asserts Assert.True(result.IsOk). If splitByN returned Ok [] or Ok [source] (no split at all), this test passes.

Smell test: 'if the function under test returned garbage of the right shape, would this test fail?' For both tests, no. An Ok wrapping any list (including empty) satisfies the assertion.

The sub-requirements provide the actual coverage: REQ-MON-2.3.2 (line 93) verifies the exact same input list produces correct values in order via List.zip and Assert.Equal. REQ-MON-2.4.1 through 2.4.6 comprehensively verify split behavior with exact value assertions and typed error matching. No behavioral gap exists at the suite level.

The issue is assertion quality per the project's own test standard. Tests/README.md states: 'Assert on domain values -- names, amounts, dates round-tripped -- and on membership. Counts only in addition to values, never instead of them.' And: 'Asserting equality is preferred to asserting truth. You should know what you expect and you should assert the outcome is exactly what you expect.' Both tests use Assert.True (truth, not equality) and assert on zero domain values. The pattern is a weakened form of Specimen 3 -- the output is never examined.

**Action:** Either strengthen both tests to assert on domain values (e.g., for 2.3: unwrap the Ok, zip with the input list, and Assert.Equal each value -- which is exactly what 2.3.2 already does), or delete them as redundant with their sub-requirement siblings. If the parent REQ is meant to be covered by its sub-requirements' tests, a section-header comment naming the parent REQ without REQ- prefix would be cleaner than a test that cites it while verifying nothing.

**Why:** A test that cites a REQ ID registers as coverage in the traceability audit. When that test's sole assertion is Assert.True(result.IsOk), the coverage claim is hollow -- the traceability system reports the requirement as tested, but the test cannot distinguish a correct implementation from one that returns Ok with garbage content. The project's test standard explicitly warns against this pattern.

---
