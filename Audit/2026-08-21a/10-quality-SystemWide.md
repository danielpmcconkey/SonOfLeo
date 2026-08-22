# spec-audit-systemwide

_No findings._

## Reasoning

Audited SystemWide.md (12 active requirements across 6 sections) against all seven check dimensions.

**Terms vs Definitions.md:** "Entity," "Instant," and "AuditEnvelope" are used consistently with their Definitions.md entries. The preamble's scope phrase ("every entity and every operation") correctly aligns with the Entity definition, and the resolved finding DB-STAGE-1 confirms that staged entries/lines (explicitly non-entities per Definitions.md) are excluded from entity-level policies like REQ-SYS-3.1.

**Internal contradictions:** Considered the REQ-SYS-3.2 ("AuditEnvelope's system instant") vs REQ-SYS-3.3 ("system clock") wording difference — already overruled in SYS-CLK-1, which established these describe the same value from different angles. No other contradictions found.

**Cross-spec contradictions:** All four explicit REQ ID cross-references in REQ-SYS-6.1 verified as present in their target specs: REQ-FP-4.1.1 (line 44, FiscalPeriodCrud.md), REQ-FP-4.2.1 (line 46), REQ-AC-2.9 (line 72, AccountCrud.md), REQ-FP-2.2 (line 23). The unnamed "journal-entry void-already-voided" maps to REQ-JE-4.6 (line 115, JournalEntryCrud.md), which cites REQ-SYS-6.1 back. Considered whether naming by description rather than REQ ID is inconsistent, but the illustrative list is preceded by "e.g." and no reasonable developer would fail to locate the corresponding requirement.

**Ambiguity (two-developer test):** Evaluated REQ-SYS-1.1's four trim points ("at the system boundary, before validation, before persistence, and before being returned") — these are belt-and-suspenders, not contradictory; a reasonable developer reads this as "trim early and always." Evaluated REQ-SYS-6.1's universal phrasing ("no state-transition operation") against the entity-scoped preamble — the preamble plus Definitions.md Entity definition plus DB-STAGE-1 precedent make the entity scope clear under the reasonable-person standard. No ambiguity that would cause implementation divergence.

**Insufficient elaboration:** The spec deliberately delegates implementation detail to entity specs per its own preamble ("the specific, testable detail behind it lives in an entity spec"). Each requirement is clear about WHAT it requires; HOW is properly left to entity specs and code, per the specs-define-what-not-how audit conduct article.

**Withdrawn table:** Both withdrawals are sound. REQ-SYS-2.2 was replaced by 2.1.1 and 2.1.2 with better separation of pre-write vs DB-constraint enforcement. REQ-SYS-4.1 was properly devolved to per-entity deletion policy (REQ-AC-5.1 cited as example). Neither withdrawal leaves an uncovered gap.

**Three-state rule:** 12 active = 6 tested (1.1, 1.2, 1.3, 3.2, 3.3, 5.1 — confirmed by grepping Tests/ for REQ-SYS references) + 6 waived (2.1, 2.1.1, 2.1.2, 3.1, 6.1, 6.1.1 — all with Dan-approved reasons). Unenforceable table is empty, which is correct since all requirements bind code, not humans. Waiver reasons are sound: the "too general for a test" class correctly identifies requirements whose enforcement is per-entity, and "simply untestable" for 6.1.1 is accurate — you cannot programmatically verify that future entity specs will document idempotency exceptions.

**Resolved findings checked:** SS-3 (todo comment — overruled, intentional), AMB-6 (perfectly reconstituted — overruled), SYS-CLK-1 (system clock vs AuditEnvelope — overruled), JE-COMPOSITE-ORDER (2.1.1 scope — overruled), DB-STAGE-1 (staged entries not entities — overruled). None require re-raising.
