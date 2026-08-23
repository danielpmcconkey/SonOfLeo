# money-spec-auditor

_No findings._

## Reasoning

Audited Money.md (27 active REQs: 22 tested, 4 waived, 1 unenforceable, 0 withdrawn) against the full checklist. Here is what I examined and why nothing rose to finding level.

CONSISTENCY WITH DEFINITIONS.MD: REQ-MON-2.1 explicitly references the Definitions.md "Money (as a variety of number)" definition, and its usage is consistent. REQ-MON-1.1 (USD denomination) aligns with Definitions.md's "An amount denominated purely in currency (USD)." The summation semantics in REQ-MON-2.5/2.9 align with Definitions.md's "Money is the only concept that sums meaningfully." No term drift.

INTERNAL CONSISTENCY: The split rejection requirements (2.4.2 for zero, 2.4.3 for one, 2.4.6 for negative) collectively cover all invalid split counts (N <= 1) without overlap or gap. REQ-MON-2.4.4 (rounding) and 2.4.5 (residual to first share) together fully specify split behavior without contradiction. Considered whether the informational parenthetical "(Except 1.1, which is unenforceable)" in REQ-MON-2.2.1 — absent from the analogous REQ-MON-2.3.1 and 2.9.1 — creates an inconsistency. It does not: REQ-MON-1.1 appears in the Unenforceable table regardless, so a reasonable developer would skip it in all three contexts. The parenthetical is a convenience annotation, not a normative exception.

CROSS-SPEC CONSISTENCY: Verified against SystemWide.md. REQ-SYS-2.1 (legal data-state enforcement) applies to entities; Money is a value type, not an entity per Definitions.md, but Money.md self-polices via its own section 1 rules referenced from section 2 operations. No contradiction. REQ-SYS-5.1 (persistence fidelity) applies to entities that contain Money values, not to Money directly. The only external cross-reference is REQ-CR-1.21 (ClassificationRuleCrud.md), which references REQ-MON-1.* for money search pattern amounts — consistent and correctly scoped.

COMPOUNDEDLEARNINGS ALIGNMENT: Read money-arithmetic-boundaries.md, money-type-enforcement.md, and numeric-type-taxonomy.md. The one divergence (learning says residual goes to "one of the resulting parts"; REQ-MON-2.4.5 pins it to "the first share only") is the expected requirement-narrows-learning pattern documented in the requirements-stricter-than-conventions conduct article. All other guidance (rounding mode, prohibition on Money multiplication/division, persistence as numeric(12,2), penny precision) aligns exactly.

AMBIGUITY CHECK: Applied the reasonable-person standard to every requirement. No requirement would cause two competent developers with domain knowledge to diverge. The subtract function's operand order (REQ-MON-2.6) is an API design choice, not an ambiguity — the requirement is about capability. The empty-list behavior of REQ-MON-2.9 (sumList) is mathematically obvious (sum of nothing is zero) per the reasonable-person standard.

THREE-STATE RULE: All 27 active requirements accounted for: 22 tested (verified by reading all 28 test methods in Tests.Isolated/Model/Money.fs and mapping unique REQ IDs), 4 waived (REQ-MON-2.1, 2.1.1, 2.7, 2.7.1 — "cannot test for the total absence of something"), 1 unenforceable (REQ-MON-1.1 — no currency tracking in the system). 22 + 4 + 1 = 27. Rule holds.

WAIVER SOUNDNESS: All four waived requirements describe prohibitions ("must only take Money types," "must never allow multiplication") that would require exhaustive code review to verify, not a unit test. The waiver reason ("cannot test for the total absence of something") is sound for all four.

UNENFORCEABLE SOUNDNESS: REQ-MON-1.1 (USD denomination) is correctly unenforceable — the Money type has no currency field, and money-type-enforcement.md confirms "This system carries no currency indicator in persistence or code."

WITHDRAWN TABLE: Empty. Nothing to evaluate.

RESOLVED FINDINGS: CV-2 (fromDecimal rounding mode), CV-4 (fromDecimal naming), AMB-13 (multiplication prohibition boundary), MON-2 (sum intermediate overflow), MON-3 (split count N type) — all previously overruled, none re-raised because the rulings match exactly.

STATEMENT DELTA: Dan's statement concerns the data-ingestion code-to-ID migration. Verified via git log that no Money domain files (spec, model, tests) were changed during that remediation. No delta to flag.
