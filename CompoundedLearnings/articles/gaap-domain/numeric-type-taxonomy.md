# Numeric Type Taxonomy

**Source:** Specs/Definitions.md — Money, Price, Quantity, Rate

The system distinguishes four kinds of numeric values. Each has different arithmetic rules and different roles. Choosing the wrong one produces type errors or, worse, semantically wrong calculations.

| Type | What it is | Sums? | Ledger-eligible? | Example |
|---|---|---|---|---|
| **Money** | Amount denominated purely in currency (USD) | Yes | Yes | $600.06 at the liquor store |
| **Price** | Ratio of currency to a non-currency unit | No | No | $152.30 per share |
| **Quantity** | Count in units other than currency | No | No | 10 shares purchased |
| **Rate** | Dimensionless proportion (usually a percentage) | No | No | 4.5% APR |

## Relationships

- Price x Quantity = Money (e.g., share price x shares = purchase amount)
- Rate x Money = Money (e.g., interest rate x principal = interest payment)
- Money + Money = Money (the only type that sums meaningfully)

## Where they live

Money lives in the ledger at penny precision (`numeric(12,2)`). Price, Quantity, and Rate live in their own domains (portfolio, obligations) with their own precision rules. Sub-cent precision never enters the ledger.
