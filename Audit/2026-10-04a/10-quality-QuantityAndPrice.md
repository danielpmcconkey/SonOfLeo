# qp-spec-auditor

## QP-DEF-1 — contradiction
- **Location:** Specs/Behavioral/QuantityAndPrice.md REQ-QP-3.5 (and intro paragraph) vs Specs/Definitions.md "Price", "Quantity", "Money"
- **Summary:** REQ-QP-3.5 says a Quantity times a Price "is not Money", but Definitions.md, which outranks it, says that same product is Money.
- **Resolution:** dan-decides

Definitions.md, Price: "its only arithmetic role is converting a Quantity into Money by multiplication." Definitions.md, Quantity: "it becomes Money only by multiplication with a Price." Definitions.md, Money: "An amount denominated purely in currency (USD)". Its note says the definition covers more than the system's Money type ("this definition encompasses the system's Money type *and* future 'Money' types"). REQ-QP-3.5 says: "returns the product as a .NET decimal ... The product is not Money; a caller that needs Money converts it under REQ-MON-2.2." USD per share times shares is an amount in USD, so under the Definitions it is Money (as a variety of number). The spec means only that it is not the system's Money *type*, which Money.md already words carefully: REQ-MON-2.1 separates 'values that meet the Definitions.md definition for Money' from the Money type. Also, the intro's "neither is ever Money" refers to Quantity and Price themselves, which is consistent with the Definitions. The only clash is the claim about the product. Code follows the spec: Price.multiplyQuantity returns decimal, with the doc comment "The exact product, not Money." The terms disagree, and the higher authority (Definitions) says the opposite of the lower one.

**Action:** Reword REQ-QP-3.5 to "The product is not the system's Money type (REQ-MON-2.2 converts it when one is needed)", or amend the Price/Quantity definitions to say multiplication produces a currency amount that becomes Money only when converted. Dan picks which.

**Why:** Definitions.md exists so that capitalised terms decide which requirements apply. If the Definitions call a value Money and a behavioral spec says it is not, the reader cannot tell whether REQ-MON-2.1-style rules apply. That matters now that positions values (market value, product) flow into net worth and, later, into the retirement engine.

---

## QP-AMB-1 — ambiguity
- **Location:** Specs/Behavioral/QuantityAndPrice.md REQ-QP-1.3, REQ-QP-2.3 (with the 'exact values' design note and REQ-QP-3.3)
- **Summary:** Two readings of "expressed with a numeric precision greater than six decimal places" give different results for trailing zeros, and this spec's 'exactly as received' promise makes the difference visible.
- **Resolution:** dan-decides

A decimal such as 1.5000000 has a value with one significant decimal place but is expressed at scale 7. The implementation checks the value: Quantity.fromDecimal/Price.fromDecimal compare raw with Math.Round(raw, 6) and accept 1.5000000. A reading based on scale (decimal.Scale > 6) rejects it. Both are reasonable readings of 'expressed'. In Money.md the difference was harmless. Here, though, the design note promises the system will "give them back exactly as received", and the REQ-QP-3.3 test treats the scale as part of exactness ("gives the identical decimal including its scale", comparing ToString text such as "42.500000"). Under the value reading, 1.5000000 is accepted and handed back at scale 7 by the type. The persisted columns (positions quantity/price numeric(16,6) in 202610041020-CreatePositionsTables.sql) store it at scale 6, so once stored it no longer comes back exactly as received. Under the scale reading it would have been rejected at the boundary.

**Action:** Amend REQ-QP-1.3/2.3 to say whether the limit applies to the value's significant decimal places (trailing zeros beyond the sixth allowed) or to the decimal's scale. Then say whether 'exactly as received' in the design note covers scale or only numeric value.

**Why:** Two competent .NET developers would implement this differently (a Math.Round equality check versus a decimal.Scale check). The spec's own emphasis on exactness, including the test's attention to scale, means the choice changes behavior a user can see.

---

