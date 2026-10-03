# test-efficacy-auditor-Money

## EFF-MON-1 — test-gap
- **Location:** REQ-MON-1.2, REQ-MON-1.3; Tests/Tests.Isolated/Model/Money.fs lines 51, 60, 91, 100, 239, 251, 276, 288, 327, 339; Src/Business.FinancialServices/Money.fs lines 10-11
- **Summary:** No test pins the spec's literal limits (9,999,999,999.99 / -9,999,999,999.99). Every boundary test takes its expected limit from the production constants maxMoney/minMoney.
- **Resolution:** fix-test

REQ-MON-1.2 and 1.3 give exact numbers. Every boundary test builds its input from `maxMoney`/`minMoney`, which it imports from the module under test (`open Business.FinancialServices.Money`). Examples: line 51 `let amount_d = maxMoney + 0.01M`, line 60 `minMoney - 0.01M`, lines 239/251 `let d1 = maxMoney` / `minMoney`, and the same at 91, 100, 276, 288, 327, 339. The expected boundary therefore comes from the code being tested (Specimen 6, the fox guarding the hen house, here applied to the boundary rather than the result). If someone changed `maxMoney` to 999.99M or 99999999999.99M, every MON test would still pass, and the system would accept or reject amounts the spec says it must not. Acceptance of exactly 9,999,999,999.99 is only exercised indirectly, through `Result.defaultWith failwith` setup calls on that same constant. No test anywhere in the file contains the literal 9999999999.99M. (The DB columns are numeric(12,2), which agrees with the spec today, but the Money tests do not check that.)

**Action:** Add isolated tests that use literals: fromDecimal 9999999999.99M returns Ok with amount = 9999999999.99M, fromDecimal 10000000000.00M returns Error MoneyFailedToConvertExceededMax, and the same pair for -9999999999.99M / -10000000000.00M with MoneyFailedToConvertBelowMin.

**Why:** The test oracle has to be independent of the code under test. A limit the spec states as a number is only verified when a test states that number. Taking it from the production constant only shows the code agrees with itself.

---

## EFF-MON-2 — test-gap
- **Location:** REQ-MON-2.3.2 (and REQ-MON-2.3); Tests/Tests.Isolated/Model/Money.fs lines 107-113 and 67-78
- **Summary:** The order-preservation test uses input that is already in ascending order, so a fromDecimalList that sorts its output would pass it.
- **Resolution:** fix-test

REQ-MON-2.3.2: 'The system will preserve the sort / positional order when doing so.' The test at line 108 uses `[ -3.99M; 12.24M; 27194338M ]`, which is already sorted ascending, and asserts `List.zip list_d list_m |> List.iter(fun (d, m) -> Assert.Equal(d, amount m))`. A `fromDecimalList` that returned `List.sort` of the converted values would produce the same list and pass. Answer to the smell test ('garbage of the right shape'): no, this test would not fail. The REQ-MON-2.3 test at lines 71-75 uses the identical input and an identical positional assertion (`Assert.Equal<decimal list>(list_d, list_m |> List.map amount)`), so the suite runs the same weak check twice under two IDs. Tests/README.md lists 'Do not test the same thing twice' as a bullshit test practice.

**Action:** Change the REQ-MON-2.3.2 test input to a list that is out of order and contains a duplicate, e.g. [12.24M; -3.99M; 27194338M; 0.01M; 12.24M], and keep the positional equality assertion.

**Why:** An order-preservation test only has power when the input is in an order that a plausible wrong implementation (sorting, set-based dedup) would change. Ascending input cannot tell 'preserves order' apart from 'sorts'.

---

## EFF-MON-3 — test-gap
- **Location:** REQ-MON-2.4.5; Tests/Tests.Isolated/Model/Money.fs lines 201-219 (also 120-143, 187-199); Src/Business.FinancialServices/Money.fs lines 46-48
- **Summary:** REQ-MON-2.4.5 says the remainder is 'either added or subtracted', but every split test has a rounded share above the exact quotient, so only the subtract case is tested. The test also checks just 2 of the 30 shares.
- **Resolution:** fix-test

Every splitByN input in the file rounds up: 111.17/3 gives 37.06, diff +0.01, first share 37.05 (lines 121, 134). 1.05/2 gives 0.53, diff +0.01 (line 188). 419.97/30 gives 14.00, diff +0.03, first share 13.97 (line 202). No test uses an amount whose share rounds down, such as 100.00/3 = 33.33 with diff -0.01, where the first share must be 33.34. An implementation computing `firstShare = fractions - abs diff`, or one that only ever subtracts the remainder, would pass every test in the file. The test at 202-219 also asserts only `shares[0]` and `shares[1]` (lines 215-216). Shares 2 through 29 are never inspected, so 'to the first share only and in its entirety' is checked on 1 of the 29 non-first shares. The splitByN tests also never assert the full share list for any input.

**Action:** Add a split case where the rounded share is below the exact quotient (e.g. 100.00M / 3 expecting exactly [33.34M; 33.33M; 33.33M]). Change the 419.97/30 test to assert the whole list: head = 13.97M and every element of the tail = 14.00M.

**Why:** When a REQ names two branches ('added or subtracted'), a test of one branch is partial coverage. Checking only selected elements lets the unchecked elements be anything.

---

## EFF-MON-4 — test-gap
- **Location:** REQ-MON-2.4.4; Tests/Tests.Isolated/Model/Money.fs lines 187-199; Src/Business.FinancialServices/Money.fs line 46
- **Summary:** The rounding test only uses a positive midpoint, so it cannot tell the required away-from-zero rounding apart from round-toward-positive-infinity. No test in the file splits a negative amount.
- **Resolution:** fix-test
- **Prior ruling:** CV-2 (Money.fromDecimal rounding mode) was overruled because fromDecimal's rounding is only a precision gate. This finding is about splitByN's share rounding (REQ-MON-2.4.4), which is real arithmetic, so CV-2 does not match exactly.

REQ-MON-2.4.4 requires 'mid-point away from zero rounding'. The only test splits 1.05 two ways and asserts the second share is 0.53M. Its comment says this rules out banker's rounding, and it does. But MidpointRounding.ToPositiveInfinity also gives 0.53 for 0.525, so swapping the rounding mode at Money.fs line 46 to ToPositiveInfinity would leave the test green. The two modes only differ on negative midpoints: -0.525 gives -0.53 away from zero and -0.52 toward +infinity. No splitByN test in the file uses a negative source amount. The test name 'rounds using midway rounding up' states the weaker property ('up') rather than the spec's ('away from zero'). Prior ruling CV-2 covers fromDecimal's precision gate, not splitByN's arithmetic rounding, so it does not apply.

**Action:** Add a theory row splitting -1.05M two ways and assert the second share = -0.53M and the first share = -0.52M, next to the existing positive row.

**Why:** 'Away from zero' is a rule about sign symmetry, and a test with positive inputs only cannot show symmetry. Pick inputs where the plausible wrong implementations give different results.

---

## EFF-MON-5 — test-gap
- **Location:** REQ-MON-2.4, REQ-MON-2.4.1; Tests/Tests.Isolated/Model/Money.fs lines 120-131 and 133-143
- **Summary:** The REQ-MON-2.4 and REQ-MON-2.4.1 tests run the same check: the same input (111.17 split 3 ways) and the same sum-equals-original assertion.
- **Resolution:** fix-test

Line 123/128: expected = 111.17M, `splitByN source 3`, `Assert.Equal(expected, shares |> List.sumBy amount)`. Line 135/140: expected = 111.17M, `splitByN source 3`, `sumList` then `Assert.Equal(expected, amount sumTotal)`. The second test adds nothing; it only routes the sum through `sumList` instead of `List.sumBy`. Tests/README.md: 'Do not test the same thing twice.' Meanwhile REQ-MON-2.4 ('split a Money value N ways') is exercised only at N=3 (plus N=2 and N=30 in the rounding tests), and the sum-reconciliation property is checked for one input only. Note: the MoneySplitFailedReconciliation branch (Money.fs lines 52-57) cannot be reached with exact decimal arithmetic, so a property-style sum check is the right way to test 2.4.1. It just needs inputs that differ from the 2.4 test.

**Action:** Turn REQ-MON-2.4.1 into a Theory over several (amount, n) pairs: a positive remainder, a negative remainder, a negative amount, 0.01M split 3 ways, and an amount near maxMoney. Assert the sum equals the original for each. Leave REQ-MON-2.4 asserting count and full share values for one case.

**Why:** Two tests running the same assertion on the same input count as coverage twice and verify once. The spare test should cover the input space the first one leaves out.

---

## EFF-MON-6 — test-gap
- **Location:** REQ-MON-1.4, REQ-MON-2.2.1; Src/Business.FinancialServices/Money.fs lines 21-29; Tests/Tests.Isolated/Model/Money.fs lines 40-47
- **Summary:** fromDecimal accepts a 2dp value written with extra trailing-zero scale (e.g. 3.990M) and stores that raw scale. No test decides whether this is allowed under REQ-MON-1.4.
- **Resolution:** dan-decides

fromDecimal compares `Math.Round(raw, 2, AwayFromZero)` with `raw`. .NET decimal equality ignores scale, so 3.990M (scale 3) equals 3.99M and passes the gate. `create raw` then stores the scale-3 value; the comment at lines 23-25 says passing raw is deliberate. `Money.amount` returns 3.990M, and its default ToString/JSON form is '3.990'. REQ-MON-1.4 says Money 'must never be expressed with a numeric precision greater than two decimal places.' The only precision test (line 42) uses 3.998M, a value that really has 3dp, so the trailing-zero case is never exercised in either direction. Persistence is not at risk: the amount columns are numeric(12,2), and the report writers use C2/N2. Whether a scale-3 representation of a 2dp value breaks 1.4 is a question of interpreting the spec. It is not fixed by the code or by any test.

**Action:** Dan decides whether 3.990M is a 1.4 violation. If it is, Src must normalize or reject it, and a test should assert fromDecimal 3.990M yields an amount with scale 2 (or a MoneyFailedToConvertImproperPrecision error). If it is not, add a test pinning that 3.990M is accepted as value-equal to 3.99M, so the behavior is deliberate and visible.

**Why:** An untested edge of a validation rule is an implicit decision made by the implementing agent. Under a fully agentic workflow, those implicit decisions are what this audit exists to surface.

---

## EFF-MON-7 — missing-requirement
- **Location:** Src/Business.FinancialServices/Money.fs lines 18-19; Src/Ui.InterfaceBridge/ReportWriters/{TrialBalanceWriter.fs:170, PrePostingReviewWriter.fs:118,131, PeriodActivityWriter.fs:114, BalanceSheetIntegrityWriter.fs:82}
- **Summary:** Money has two public display formatters, toCurrencyString (C2) and toAccountingString (N2). Four report writers use them, but no REQ in Money.md or Reporting.md covers them and no test cites them.
- **Resolution:** dan-decides

Money.fs defines `toCurrencyString m = m.amount.ToString("C2", en-US)` and `toAccountingString m = m.amount.ToString("N2", en-US)`. TrialBalance and PrePostingReview display amounts as currency (C2, with '$' and the .NET en-US negative pattern). PeriodActivity and BalanceSheetIntegrity display them as plain N2 numbers. So report output follows two different money-display conventions, and nothing specifies either one. grep found no REQ about money display format in Money.md or Reporting.md (the nearest is REQ-RPT-3.4, a CSS sign class), and no test references either function. Tests/README.md says code doing something uncited by any REQ needs a REQ.

**Action:** Dan decides whether money display format (currency symbol, negative-sign convention, grouping, and whether reports may differ) is a requirement. If it is, add REQ(s) to Money.md or Reporting.md and isolated tests on both formatters with positive, negative, and zero values.

**Why:** Display format is behavior that report readers can see. Untested and unspecified, an agent could change the negative pattern (e.g. from '-$1.00' to '($1.00)') or the culture, and nothing would fail.

---


