# Numeric Type Taxonomy

**Source:** Specs/Definitions.md — Money, Price, Quantity, Rate

The four numeric kinds are **defined** in `Specs/Definitions.md`. Read them there. This
article is the arithmetic that follows from the definitions, which is where the mistakes
actually happen: choosing the wrong kind produces type errors or, worse, a semantically
wrong calculation that compiles.

## The arithmetic

- Money + Money = Money — **Money is the only kind that sums meaningfully**
- Price x Quantity = Money (share price x shares = purchase amount)
- Rate x Money = Money (interest rate x principal = interest payment)
- Price, Quantity and Rate never sum, and never appear in the ledger

## Where they live

Money lives in the ledger at penny precision (`numeric(12,2)`). Price, Quantity and Rate
live in their own domains — portfolio, obligations — with their own precision rules.
**Sub-cent precision never enters the ledger.**
