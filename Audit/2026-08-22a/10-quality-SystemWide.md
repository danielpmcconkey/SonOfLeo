# hobson-systemwide-auditor

_No findings._

## Reasoning

Audited SystemWide.md (12 active REQs, 6 waived, 0 unenforceable, 2 withdrawn) against all seven checklist items. Read all 12 audit-conduct articles and the full resolved-findings ledger (24 entries) before evaluating.

**1. Terms vs Definitions.md** -- "entity" is the load-bearing term; it appears in REQ-SYS-2.1, 3.1, 3.2, 5.1, and 6.1. Definitions.md explicitly excludes staged entries and staged lines from entity status. SystemWide.md's usage is consistent: all entity-level policies (timestamps, legal-data-state, persistence fidelity, no-op rejection) correctly scope to entities, not pipeline artifacts. "Instant" in REQ-SYS-3.2 matches the Definitions.md temporal definition. No term misuse found.

**2. Internal contradictions** -- REQ-SYS-3.2 uses "AuditEnvelope's system instant property" while REQ-SYS-3.3 uses "system clock." These describe the same mechanism from different angles (precedent SYS-CLK-1, overruled). REQ-SYS-6.1 (no silent no-ops) and REQ-SYS-6.1.1 (escape valve for declared idempotent operations) are complementary. REQ-SYS-2.1.1 (pre-write rejection for property-determinable issues) and 2.1.2 (DB-state rejections may fall through to constraints) partition 2.1 cleanly. No contradictions.

**3. Cross-references to other specs** -- REQ-SYS-6.1 cites five entity-level REQ IDs as examples: REQ-FP-4.1.1, REQ-FP-4.2.1, REQ-AC-2.9, REQ-FP-2.2, and REQ-AC-5.1. All five verified present in FiscalPeriodCrud.md and AccountCrud.md respectively. The illustrative list also mentions "journal-entry void-already-voided" without a REQ ID; REQ-JE-4.6 explicitly cites REQ-SYS-6.1 from the JE side, so the back-link exists. The omission of the forward ID in an "(e.g., ...)" list is cosmetic, not normative. Newer entity specs (ClassificationRuleCrud REQ-CR-6.2, DataIngestion) implement no-op rejection without citing REQ-SYS-6.1 by ID, which is fine -- the system-wide policy applies regardless of citation.

**4. Ambiguity (reasonable-person standard)** -- Every requirement states a clear, testable behavior. REQ-SYS-1.1's "system boundary" is unambiguous to any developer familiar with layered architecture. REQ-SYS-5.1's "perfectly reconstituted" was litigated and overruled (AMB-6). REQ-SYS-2.1.1's "entity's own properties" was clarified through the JE-COMPOSITE-ORDER precedent. No requirement would cause two competent developers to diverge.

**5. Insufficient elaboration** -- The system-wide requirements are intentionally general policies. Specific data-state rules, validation details, and entity-specific behaviors correctly live in entity specs. The level of elaboration is appropriate for cross-cutting policies.

**6. Withdrawn table** -- Two entries. REQ-SYS-2.2 was split into 2.1.1 and 2.1.2 for clarity. The "meaningful error message" clause from 2.2 was dropped during the split; this is consistent with AMB-5's ruling that "verify" and validation failure modes are self-explanatory and need not be spelled out per-requirement. REQ-SYS-4.1 (system-wide hard-delete prohibition) correctly moved to per-entity decisions (REQ-AC-5.1 for Accounts). No uncovered gaps from either withdrawal.

**7. Three-state rule and waiver soundness** -- Six REQs waived: SYS-2.1, 2.1.1, 2.1.2, 3.1, 6.1, 6.1.1. All share the same structural reason: system-wide policies too general for dedicated tests, enforced per-entity. This is sound -- you cannot write a single test proving "every entity carries timestamps" or "no operation anywhere is a silent no-op." The remaining 6 active REQs (SYS-1.1, 1.2, 1.3, 3.2, 3.3, 5.1) were verified by grep in Tests/ -- all have named test methods carrying their REQ IDs across multiple entity domains (AccountComponent, JournalEntryComponent, JournalEntryExternalReference, Account, FiscalPeriod, JournalEntryComment, JournalEntryCreation). Zero unenforceable entries. Rule holds.

**Also considered and dismissed**: The todo on line 28 (external audit log) is an intentional Dan-placed marker per precedent SS-3. The missing closing bold markers on REQ-SYS-2.1.1 and 2.1.2 are markdown formatting, not a requirements-quality issue. The Postable definition in Definitions.md references "account_code" which may be stale after the code-to-ID migration, but SystemWide.md does not reference the Postable term, so this is outside scope.
