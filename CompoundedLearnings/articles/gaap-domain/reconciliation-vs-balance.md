# Reconciliation vs Balance

**Source:** Specs/Archive/Decisions.md, 2026-06-11

Internal arithmetic and external reconciliation follow different rules. Do not conflate them.

## Internal: exact

Numbers this system computed must agree exactly. The balance invariant (see balance-invariant) has no tolerance. A penny off in a journal entry is a bug.

## External: domain-specced tolerance

Reconciliation compares our books against an external statement — two bookkeepers legitimately disagreeing. Tolerances here are domain data: specced thresholds per account class, not code epsilons.

When a discrepancy is accepted, it is posted as an explicit adjustment entry. The books stay exactly balanced. The tolerance is never silently absorbed — it leaves an audit trail.
