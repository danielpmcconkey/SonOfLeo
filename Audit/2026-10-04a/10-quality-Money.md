# spec-quality-auditor:Money.md

## MON-SCOPE-POS-1 — stale-reference
- **Location:** Specs/Behavioral/Money.md REQ-MON-2.1 (amended 2026-10-03)
- **Summary:** REQ-MON-2.1 limits its rule to Money values in "the ledger, staging, cash flow or classification", so read literally it does not cover the Positions domain added on 2026-10-04, even though Positions holds and sums Money.
- **Resolution:** fix-spec

REQ-MON-2.1 was amended on 2026-10-03 to say that functions working with ledger currency values "anywhere in the ledger, staging, cash flow or classification" must take and return Money, and must do all arithmetic and comparison through the section-2 operations. The Positions slice landed the next day. It stores ledger-currency values in the Definitions.md sense of Money: market value and reported cost basis on snapshot lines, contribution basis on snapshot headers, purchase basis on properties, and valuation value on valuations. Net worth and investment wealth history also sum and subtract those values (Src/Business.CrossDomainOrchestration/NetWorth.fs lines 72, 88, 162, 180-184; InvestmentWealthHistory.fs lines 87-89). That code does use Money.sumList, add and subtractVal1FromVal2, so it follows the rule's intent. But the rule as written does not apply to it, and the only exemption the spec's own 'Why ledger currency only' note names is the Monte Carlo simulator's floating-point type. Dan's statement places Positions above cash flow in the architecture, and the spec has not caught up.

**Action:** Amend REQ-MON-2.1's scope list to include positions (or replace the list with 'anywhere in the system other than the simulator'), or, if Positions is meant to be exempt, say so explicitly next to the 'Why ledger currency only' note.

**Why:** A scope written as a closed list goes stale silently each time a domain is added. Today the Positions code happens to comply. Nothing in the spec makes it comply, so a future change in Positions could convert Money to decimal to add or compare, and no requirement would be broken.

---

## MON-CMP-QP-1 — contradiction
- **Location:** Specs/Behavioral/Money.md REQ-MON-2.1 vs Specs/Behavioral/QuantityAndPrice.md REQ-QP-3.5 and Specs/Behavioral/Positions.md REQ-POS-6.8
- **Summary:** REQ-MON-2.1 forbids converting a Money value to a decimal in order to compare or subtract it, with only two exceptions, yet REQ-POS-6.8 requires comparing a Money market value against the decimal product from REQ-QP-3.5, which fits neither exception.
- **Resolution:** dan-decides

REQ-MON-2.1 says: 'a value is not converted to a decimal to add, subtract, compare or test its sign (conversion is reserved for REQ-MON-2.1.1 boundaries and REQ-MON-2.7.1)'. REQ-QP-3.5 deliberately returns the quantity-times-price product as a non-Money decimal. REQ-POS-6.8 then requires checking that this product and the line's market value (a Money value) 'may differ by at most 0.05 USD'. There are two ways to implement that, and they behave differently. (a) Convert the product to Money under REQ-MON-2.2 and compare with Money operations. That conversion fails when the product has more than two decimal places or exceeds 9,999,999,999.99, so a line within tolerance could be rejected for the wrong reason. (b) Convert the Money to a decimal and subtract. That breaks REQ-MON-2.1 as worded, because it is not a boundary function (2.1.1) and not a multiplication or division (2.7.1). The code takes route (b) (Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs lines 63-79: `let marketValueAmount = marketValue |> Money.amount` ... `Math.Abs(product - marketValueAmount) > tolerance`), and its comment explains that route (a) would hide differences. That reasoning is sound, but REQ-MON-2.1 does not allow it, and it is only compliant today because finding MON-SCOPE-POS-1 leaves Positions outside 2.1's scope. Once the scope gap is closed, this becomes a direct conflict between the two specs.

**Action:** Add a third sanctioned conversion to REQ-MON-2.1 (for example: 'comparing a Money value against a non-Money decimal produced by another exact type, such as the REQ-QP-3.5 product'), or have Dan rule that REQ-POS-6.8's tolerance check counts as a boundary under REQ-MON-2.1.1.

**Why:** When REQ-QP-3.5 deliberately returns a decimal that callers must compare against Money, REQ-MON-2.1 has to say how that comparison is done. Without that, the two options look equally valid to a developer: one rejects legitimate snapshot lines, and the other quietly breaks REQ-MON-2.1.

---

## MON-WAIVER-271-1 — stale-reference
- **Location:** Specs/Behavioral/Money.md Waived table, REQ-MON-2.7.1 (reason restated 2026-10-03)
- **Summary:** The REQ-MON-2.7.1 waiver says Money's "only operations are add, subtract, sum and split", but Money also has comparison, sign tests and floorAtZero.
- **Resolution:** fix-spec

The waiver reads: 'Enforced by construction: Money is a private record whose only operations are add, subtract, sum and split, so converting to a decimal and back is the only route to a multiplication or division.' The Money module also exposes isEqual, isLessThan, isGreaterThan, isLessThanOrEqual and isGreaterThanOrEqual (REQ-MON-2.10), isPositive, isZero and isNegative (REQ-MON-2.11), and floorAtZero (Src/Business.FinancialServices/Money.fs lines 70-82). floorAtZero turns a negative Money into zero, and CashFlowOps.fs line 781 uses it. No MON requirement covers it. The waiver's conclusion still holds, since none of these operations multiplies or divides. But the reason is an exhaustive list that is now wrong, and it is the justification Dan approved for not testing 2.7.1. Each time a Money operation is added without updating this list, the claim 'only these operations' drifts further from the code.

**Action:** Restate the REQ-MON-2.7.1 waiver reason without the closed list, for example: 'Money is a private record and none of its operations multiplies or divides, so converting to a decimal and back is the only route to a multiplication or division.'

**Why:** A waiver replaces a test with a claim about how the code is built. If that claim lists specific facts that are false, nobody can tell whether it was ever re-checked, which weakens the three-state rule that tested, waived and unenforceable together cover every requirement.

---


