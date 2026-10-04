# test-efficacy-money

## EFF-MON-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/Money.fs:82-98 (REQ-MON-2.2.1 / REQ-MON-1.2 / REQ-MON-1.3) vs :41-53
- **Summary:** The two REQ-MON-2.2.1 boundary-rejection tests repeat the REQ-MON-1.2 and 1.3 literal-limit tests exactly, but take the boundary from the constants under test, so they add nothing and are the weaker copy.
- **Resolution:** fix-test

Line 42 `REQ-MON-1.2 fromDecimal rejects 10,000,000,000.00 as exceeding the maximum` calls `fromDecimal 10000000000.00M` and expects `MoneyFailedToConvertExceededMax`. Line 83 `REQ-MON-2.2.1 REQ-MON-1.2 fromDecimal rejects amount exceeding maxMoney` calls `fromDecimal (maxMoney + 0.01M)`. That is the same 10000000000.00M input, against the same function, expecting the same typed error. Lines 49 and 92 are the same pair for the minimum (`-10000000000.00M` vs `minMoney - 0.01M`). The line-83 and line-92 versions are also Specimen-6 shaped. They derive the bad input from `maxMoney`/`minMoney`, which are the constants `fromDecimal` itself checks against (Src/Business.FinancialServices/Money.fs:10-11, 28-29). If `maxMoney` were wrongly set to 99,999,999,999.99, line 84 would still build an input one cent over that wrong limit and pass. Only the literal tests at 42 and 49 would catch it. The 2.2.1 citation therefore sits on two tests that duplicate stronger ones (Tests/README.md, "Do not test the same thing twice").

**Action:** Delete the two tests at Money.fs:82-98 and add REQ-MON-2.2.1 to the names of the literal-limit tests at lines 42 and 49, which already exercise fromDecimal's section-1 validation with spec-literal inputs.

**Why:** A test whose expected boundary comes from the production constant only checks that the code agrees with itself. The literal tests already cover these inputs independently. Keeping the copies doubles the citation count without adding coverage, which makes REQ-MON-2.2.1 look more thoroughly tested than it is.

---

## EFF-MON-2 — test-gap
- **Location:** REQ-MON-1.4 / REQ-MON-2.2.1; Tests/Tests.Isolated/Model/Money.fs:56-80; Src/Business.FinancialServices/Money.fs:21-30
- **Summary:** fromDecimal accepts a decimal that has more than two digits of scale but a two-place value, such as 3.990M, and keeps the three-digit scale. No test covers this, and the suite's scale-blind decimal equality could not catch it.
- **Resolution:** dan-decides

fromDecimal compares `Math.Round(raw, 2, AwayFromZero)` with `raw` using decimal `<>`. That comparison ignores scale, so 3.990M passes the precision gate, and `create raw` (line 30) stores the scale-3 value. Money.amount then returns 3.990M, which `ToString` and JSON serialisation render as "3.990". Every acceptance assertion in the Money tests (lines 38, 59, 65, 71, 108, 145, and the split/add/sum tests) is `Assert.Equal(decimal, decimal)`, which also ignores scale. A Money holding a three-place scale therefore passes all of them. The only REQ-MON-1.4 rejection test (line 74) uses 3.998M, a value with a real sub-cent part. No test sends a trailing-zero three-place input. On the observable side, the ledger columns are numeric(12,2) (DbMigration/Scripts/202609071105-CreateLedgerTables.sql:98), so persisted values are normalised. Values that never round-trip through the database, such as in-memory results and route echoes, would keep the extra scale. Whether that breaches "Money must never be expressed with a numeric precision greater than two decimal places" is a reading of the spec, not something the tests settle. Today nothing pins the behaviour either way. (Not covered by the CV-2 ruling, which is about the rounding mode used as a precision gate, not about the scale of the stored value.)

**Action:** Dan decides whether 3.990M is a REQ-MON-1.4 violation. If it is, fromDecimal should either reject it or normalise it to scale 2, and an isolated test should pin that with a scale-sensitive assertion such as `(amount m).ToString(CultureInfo.InvariantCulture) = "3.99"` or a check on `Decimal.GetBits` scale. If it is not, add a test that documents the acceptance.

**Why:** An equality assertion is only as strong as the equality it uses. Decimal equality treats 3.99 and 3.990 as equal, so this whole category of outcome is invisible to the suite. A requirement about how Money is expressed needs an assertion that can see the representation.

---


