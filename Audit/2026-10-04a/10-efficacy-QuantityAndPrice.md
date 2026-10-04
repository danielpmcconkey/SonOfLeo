# test-efficacy-QuantityAndPrice

## EFF-QP-1 — test-gap
- **Location:** REQ-QP-1.3, REQ-QP-2.3; Tests/Tests.Isolated/Model/QuantityAndPrice.fs:53-60 and :94-101; Src/Business.FinancialServices/Quantity.fs:14-20, Price.fs:43-49; DbMigration/Scripts/202610041020-CreatePositionsTables.sql:220-221
- **Summary:** A value with a seventh decimal place that is zero (e.g. 1.5000000M) is accepted by Quantity/Price.fromDecimal and keeps scale 7. No test covers that case, though the precision tests' names say any value with a seventh decimal place is rejected.
- **Resolution:** dan-decides

REQ-QP-1.3 says a Quantity 'must never be expressed with a numeric precision greater than six decimal places' (REQ-QP-2.3 says the same for Price). The precision tests (QuantityAndPrice.fs:56, :97) are named '... rejects a value with a seventh decimal place ...' but only use 0.0000001 and 1.2345675, where the seventh digit is not zero. The code checks `x <> Math.Round(raw, 6, ...)`, and decimal equality ignores scale. So `Quantity.fromDecimal 1.5000000M` returns Ok and stores a decimal with scale 7. `Quantity.amount` then gives back text '1.5000000', which has seven decimal places. The scale is not just cosmetic in this suite: the REQ-QP-3.3 test (line 109) asserts that scale survives a round trip ('42.500000' -> '42.500000'). The Quantity/Price columns are numeric(16,6), so once persisted the same value reads back as 1.500000. A value the type accepted therefore does not come back exactly as received, which conflicts with the spec's design note that these types 'give them back exactly as received'. Money.fromDecimal uses the same pattern, so this may be intended. Even so, no test pins the intended answer for the trailing-zero case, and the test names claim behavior the code does not have.

**Action:** Dan rules whether 'expressed with' in REQ-QP-1.3/2.3 means scale or significant digits. If scale: add 1.5000000 to both precision theories, expecting the typed ImproperPrecision error, and fix fromDecimal. If significant digits: rename the two theories to 'a non-zero seventh decimal place' and add a 1.5000000 accept case that asserts what comes back.

**Why:** A test name is a claim. Right now it promises rejection of any seventh decimal place, while the code admits one kind. A reader trusting the name would believe a seven-place-scale value can never become a Quantity, and that value later loses its scale in the database without any error.

---

## EFF-QP-2 — test-gap
- **Location:** REQ-QP-3.5; Tests/Tests.Isolated/Model/QuantityAndPrice.fs:146-154 (InlineData rows 147-152)
- **Summary:** The amended REQ-QP-3.5 promises an exact product for every result below 10^16. The largest product tested is about 2.1e10 with 23 significant digits, so the part of the range where decimal's 28-29 digit capacity matters is never tested.
- **Resolution:** fix-test

The 2026-10-04 amendment to REQ-QP-3.5 promises the product is 'exact whenever the product is less than 10^16'. Its rationale says the tight case is decimal's 28-29 significant digits. The theory's biggest case is 9999999999.999999 x 2.123456 = 21234559999.999997876544, which has 23 significant digits. A product close to the bound with all 12 decimal places, e.g. 9999999999.999999 x 999999.999999 = 9999999989999999.000000000001 (28 significant digits), is where the promise is at risk, and no test has one. Smell test: an implementation that silently rounded the product to 24-27 significant digits would pass all six rows and the Fact at line 142. The test name says it covers 'the maximum Quantity at a six-place Price', which suggests worst-case coverage. But a Price of 2.123456 is far from the price that takes the product toward 10^16. The current implementation (plain decimal `*`, Price.fs:59) is correct; the gap is that the tests do not hold the amended clause in place.

**Action:** Add a row to the REQ-QP-3.5 theory with a product just below 10^16 that has 12 decimal places, e.g. ('9999999999.999999', '999999.999999', '9999999989999999.000000000001'). Expect the hand-derived literal.

**Why:** The bound is the part of the requirement that was amended this run. A requirement clause that no test reaches can regress without the suite going red. It only regresses near the capacity limit, which is the region the amendment was written about.

---

