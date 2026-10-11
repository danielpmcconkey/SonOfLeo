# test-efficacy-auditor-Money

_No findings._

## Reasoning

Scope: REQ-MON tests. A grep of Tests/ finds REQ-MON cited in only one file, /home/claude/SonOfLeo/Tests/Tests.Isolated/Model/Money.fs (32 test attributes). I read it side by side with Specs/Behavioral/Money.md and with the code under test, Src/Business.FinancialServices/Money.fs. I also read the specimens doc, Tests/README.md, resolved-findings.md (CV-2, CV-4, AMB-13, MON-2, MON-3 and the trailing-zero scale ruling) and every audit-conduct article.

1. BEHAVIORAL COVERAGE: 24 active Money requirements need tests. That is 29 active, minus the waived 2.1, 2.7 and 2.7.1, minus the unenforceable 1.1 and 2.1.1. Every one of the 24 has a citing test, and that test exercises the exact property the requirement states. If the function returned garbage of the right shape, each test would fail:
- 1.2/1.3: both literal limits round-trip by value (line 36), and one cent past each limit is rejected with the typed MoneyFailedToConvertExceededMax / BelowMin (lines 42, 49).
- 1.4/2.2.1: 3.998 is rejected with MoneyFailedToConvertImproperPrecision (line 74).
- 2.2: positive, negative and zero values round-trip by value.
- 2.3: list values are compared element by element, not just counted (line 90).
- 2.3.1: precision, max and min are each rejected, with a typed error, inside a list.
- 2.3.2: an unsorted list with duplicates keeps exact positional order (line 127).
- 2.4: an exact list of shares is checked.
- 2.4.1: an 8-row theory checks the share count and the exact sum, including at both limits. The internal check behind MoneySplitFailedReconciliation is unreachable with exact decimal arithmetic, because firstShare = fractions - (fractions*n - amount), so asserting the outcome is the only observable test.
- 2.4.4: the cases are chosen so they tell rounding modes apart. I worked them by hand. For 1.05/2, banker's rounding would give [0.53; 0.52] against the asserted [0.52; 0.53]. For -1.05/2, rounding toward positive infinity would give [-0.53; -0.52] against the asserted [-0.52; -0.53]. A wrong rounding mode therefore fails the test.
- 2.4.5: one test covers each remainder direction, with exact share lists (419.97/30 and 100.00/3).
- 2.4.2/2.4.3/2.4.6: each is rejected with the typed MoneyImproperSplit.
- 2.5/2.6/2.9: happy paths are exact values. 2.5.1/2.6.1/2.9.1 cover overflow in both directions with typed errors. A precision violation cannot come from adding or subtracting two-decimal values, so only the range checks of section 1 are reachable.
- 2.8: round-trip.
- 2.10: a 7-row theory gives a verdict for all five comparison functions on every row, and checks that "less than" and "greater than" are consistent with each other.
- 2.11: a 5-row theory checks exactly one of positive / zero / negative per value, including the limits.

2. ASSERTION QUALITY:
- Specimen 1: no hard-wired fixture counts. These are isolated Form 1 tests, and their literal expected values are worked out by hand from the requirement's arithmetic, not taken from fixture data.
- Specimen 2: no inequalities.
- Specimen 3: no count-only assertions. Counts in 2.3 and 2.4.1 always come alongside value assertions.
- Specimen 4: every sad path matches the typed BizFinServError case through AsError, with both escape arms. No isError and no string Contains.
- Specimen 5: not applicable, since these are not CLI tests.
- Specimen 6: expected values come from literals or primitive decimal +/- and List.sum, not from Money functions. The expected values in the 2.3.1/2.5.1 tests use maxMoney/minMoney, which are constants restating the spec literals. The 2.10 consistency check (line 367) compares the function with itself, but only as an extra assertion; the truth-table assertion on line 366 carries the weight.
- Specimen 7: no test lacks an assertion.

3. NEGATIVE COVERAGE: every rejection requirement has a typed negative test:
- 1.2, 1.3 and 1.4 through 2.2.1 and 2.3.1
- 2.4.2, 2.4.3 and 2.4.6
- 2.5.1, 2.6.1 and 2.9.1, in both directions

4. UNCITED BEHAVIOR: Money.fs exports three functions that no Money requirement covers.
- floorAtZero is backed by REQ-CF-8.9 ("outstanding amount ... floored at zero"), which is CashFlow scope.
- toCurrencyString and toAccountingString are formatting for report and UI output, which the Reporting and UI specs govern. They are not Money arithmetic.
- sumList on an empty list returns zero. That is the obvious meaning of a sum, not uncited logic (reasonable-person standard).
I did not re-raise the overflow of intermediate sums (MON-2), the integer type of N (MON-3), the rounding inside fromDecimal (CV-2), or trailing-zero scale, because each has a matching ruling.

Dan's statement that every active requirement is tested holds for Money, so there is no statement-delta.
