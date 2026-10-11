# efficacy-QuantityAndPrice

_No findings._

## Reasoning

I found nothing to report. Scope: all 11 active REQ-QP requirements. The spec's waived, unenforceable and withdrawn tables are empty. Two test files cite REQ-QP: /home/claude/SonOfLeo/Tests/Tests.Isolated/Model/QuantityAndPrice.fs (17 test attributes) and /home/claude/SonOfLeo/Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs (lines 688 and 705). I read every test body next to the REQ text and next to the source, /home/claude/SonOfLeo/Src/Business.FinancialServices/Quantity.fs and Price.fs.

1. Behavioral coverage:
- QP-1.1 and 2.1: zero is accepted and comes back as 0. The negative test uses -0.000001, which passes the precision check, so the negative-value branch is the one that fires. It asserts the typed QuantityFailedToConvertNegative or PriceFailedToConvertNegative case and the raw value it carries.
- QP-1.2 and 2.2: the exact maximum 9999999999.999999 is accepted and its text comes back unchanged. 10000000000.000000, the smallest six-place value above the maximum, is rejected with the typed ExceededMax case and its raw value.
- QP-1.3 and 2.3: a six-place value comes back with every digit. Theory rows 0.0000001 and 1.2345675 are rejected with the typed ImproperPrecision case. The raw value's text is asserted, so a version that rounded and then reported the rounded value would fail. The route tests repeat this at the payload layer, which is a different failure vector. They assert the typed error carrying the raw value, and they assert that nothing was recorded (Assert.Empty on dates listed by account name, which is not the asserted field).
- QP-3.1 and 3.2: covered by the tests above, together.
- QP-3.3: the round-trip theory checks the decimal's exact text, scale included, for both types. Returning garbage would fail it.
- QP-3.4: every ordered pair over [0; 1.000001; 1.000002; 250], self-pairs included, is checked against native decimal comparison for all five comparators on both types. That makes it a full truth table, so Specimen 10 does not apply. The Assert.True(bool = bool) form does not match any specimen.
- QP-3.5: exact products are checked, including a zero price, the maximum Quantity at a six-place Price, a twelve-decimal product, and a product just below 10^16 (9999999999989999.000000000001). A multiplication that rounded, or that went through Money, would fail.

2. Assertion quality:
- None of the six specimen patterns appears.
- No hard-wired fixture counts, inequalities or count-only assertions. There is no Result.isError and no string Contains on error text.
- No exit-code assertions.
- No expected value is derived from the call chain under test. Expected values are literals, or native decimal operators for 3.4, which are not in the call chain.
- The route tests' expectError helper (Tests/Tests.Helpers/PositionsFixture.fs:530) has a wrong-error arm and an Ok arm, and both fail the test.

3. Negative coverage: every rejection rule (1.1, 1.2, 1.3, 2.1, 2.2, 2.3) has a test that sends a bad value and matches the typed DU case. QP-3.4 and 3.5 define no rejections.

4. Uncited behavior:
- Quantity.fs exposes two operations that no QP requirement names: Quantity.isPositive and Quantity.sum, the latter added in slice 2, commit 3f229db.
- Their behavior is in Positions requirements: quantity greater than zero (REQ-POS-6.7, 6.11, 12.7) and lot quantities summing exactly to the line quantity (REQ-POS-6.12). The roll-forward also uses sum.
- Their behavior is covered by REQ-POS requirements, so they are not missing requirements in this spec. Their tests belong to the Positions efficacy auditor.

Things I considered and set aside:
- Which error wins when a value breaks two rules at once (for example, negative with seven decimals): the spec does not state an order. Flagging it would be a pathological reading under the reasonable-person standard.
- Trailing-zero scale versus precision: settled precedent in resolved-findings.md line 229, "do not re-flag."
- Products at or above 10^16: the spec makes no exactness promise there, and REQ-POS-6.8 owns the rejection.
- Dan's line that "error cases that only reject loudly are untested by design": every QP rejection is in fact tested. This scope gives no basis for a statement-delta finding specific to QuantityAndPrice.