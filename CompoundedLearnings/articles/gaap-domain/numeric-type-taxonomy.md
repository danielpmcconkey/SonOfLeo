# Numeric Type Taxonomy

**Source:** Specs/Definitions.md — Money, Price, Quantity, Rate

The four numeric kinds are **defined** in `Specs/Definitions.md`. Read them there. This
article is the arithmetic that follows from the definitions, which is where the mistakes
actually happen: choosing the wrong kind produces type errors or, worse, a semantically
wrong calculation that compiles.

## The arithmetic

- Money + Money = Money — **Money is the only kind that sums meaningfully**
- Quantity x Price = Money in the Definitions sense (shares x share price = market value),
  but the product comes back as an exact decimal, not the system's Money type. A caller that
  needs the Money type converts it (REQ-MON-2.2); a check that only compares it against a
  Money value does not (REQ-QP-3.5, REQ-POS-6.8).
- Rate x Money = Money (interest rate x principal = interest payment)
- Price, Quantity and Rate never sum, and never appear in the ledger

## Where they live

Money lives at penny precision (`numeric(12,2)`) wherever the system holds it: the ledger,
staging, cash flow and Positions. Quantity and Price live in the Positions domain at six
decimal places (QuantityAndPrice.md). Rate has no home yet. **Sub-cent precision never enters
the ledger.**
