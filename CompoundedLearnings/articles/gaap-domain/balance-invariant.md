# Balance Invariant

**Source:** Decisions.md, 2026-06-11

Every journal entry's lines must sum to exactly zero — debits and credits balance to the penny. This is exact and tolerance-free.

A difference between numbers this system computed is a bug, not a materiality judgment. There is no epsilon, no rounding tolerance, no "close enough" for intra-system arithmetic.

## Why exact

The system wrote both sides of the entry. If it can't make them agree exactly, something is wrong with the arithmetic — not with the precision. Tolerances exist only at the reconciliation boundary (see reconciliation-vs-balance).
