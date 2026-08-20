# resolved-findings-staleness-auditor

_No findings._

## Reasoning

Examined all 25 resolved findings (23 overruled, 2 deferred) against the current specs, definitions, archived decisions, and source code. Read all 12 audit conduct articles before beginning.

FOR EACH RULING, verified:

(A) Whether referenced REQ IDs still exist, are active, and carry materially the same text:

- CV-2, CV-4: Money.fromDecimal still exists at Src/Model/Money.fs:22. REQ-MON-2.2 (decimal-to-Money conversion) unchanged.
- AMB-4: REQ-DAL-2.1 and REQ-DAL-2.3 both active and unchanged in DataAccessLayer.md. The distinction (parameterized DB values vs user-originated input) is preserved.
- AMB-5: REQ-DAL-2.2 active and unchanged.
- AMB-6: REQ-SYS-5.1 active, still says "perfectly reconstituted."
- AMB-11: REQ-DAL-3.2.1 active and unchanged.
- AMB-13: REQ-MON-2.7 active and unchanged.
- IE-4 (deferred): REQ-AC-1.32 still constrains Equity to null subtypes. Trigger ("GAAP closing entries designed") unmet — FiscalPeriodCrud.md design note still says "defers closing tooling until wanted."
- DEC-1: Convention vs requirement "must" — the pattern described (conventions hold prose, behavioral specs hold REQ-labeled testable requirements) still holds across all current spec files.
- IE-2: REQ-DAL-3.6 active and unchanged. Now also in the "Unenforceable" table (consistent with the ruling's tolerance of non-assertable language).
- SS-3: The todo comment is still at SystemWide.md line 28. Ruling still applies.
- DEC-3: REQ-AC-1.39 active. The explicit self-parent check (confirmParentAndChildAreDistinct) still exists at AccountCreation.fs:38.
- IE-AC-1: REQ-AC-3.9 active and unchanged. Calendar.today() still exists at Utilities/Calendar.fs:10, still routes through Clock.now() and US Eastern Time, exactly as described in the ruling.
- AMB-AC-2: REQ-AC-4.4 active and unchanged.
- SYS-CLK-1: REQ-SYS-3.3 active, still says "system clock at time of the update." AuditEnvelope still exists (Src/Logger/Audit.fs:30, Src/Context/Context.fs).
- AMB-DAL-01: REQ-DAL-1.16 active and unchanged.
- CON-DAL-02: REQ-DAL-2.2 active and unchanged.
- AMB-JE-1: REQ-JE-1.11 active and unchanged. fiscal_period_id FK confirmed in migration (202606221206-CreateJeTables.sql:14,18) and model (JournalEntryHeader.fs). The ruling's factual premise ("the period IS persisted") is still correct.
- GAP-JE-2: REQ-SYS-3.1 and 3.3 both active and unchanged.
- AMB-JE-3a: REQ-JE-4.9 active. REQ-JE-1.40 (UUID PK), REQ-JE-1.44, REQ-JE-1.45 all active and unchanged.
- MON-2: REQ-MON-2.9 and 2.9.1 active and unchanged.
- MON-3: REQ-MON-2.4 and sub-requirements active and unchanged.
- GAAP-CLOSE (deferred): Trigger ("Dan schedules the closing-entries slice") unmet. FP spec design note still says closing tooling is deferred.
- CLAUDE-MD: Not a spec-level ruling; repo-root CLAUDE.md still does not exist and the wakeup protocol is still the entry point.
- JE-COMPOSITE-ORDER: REQ-SYS-2.1.1 active and unchanged ("Rejections determinable from the entity's own properties must occur before any database write"). The ruling's reasoning (composite checks require DB state, therefore 2.1.1 does not apply) is still architecturally accurate.
- WAIVE-1: REQ-NGUI-3.1 through 3.5 still waived with "too broadly scoped" reason. The new REQ-NGUI-3.10 uses a different waiver reason and is outside this ruling's scope.

(B) Whether any ruling was scoped to a phase that has passed: No ruling carries phase-based scoping. The two deferred findings (IE-4, GAAP-CLOSE) have trigger conditions, not phase gates, and neither trigger has been met.

(C) Whether any ruling is so broadly worded it could suppress unintended findings: Each ruling has a specific Scope field. The broadest candidates (IE-2 "Whether REQs can contain non-assertable language" and DEC-1 "convention must vs requirement must") both include guardrails in their ruling text that prevent over-application — IE-2 conditions tolerance on "doesn't create ambiguity"; DEC-1 describes a specific pattern rather than blanket-suppressing all must-vs-must questions.

Also checked the new specs (DataIngestion.md, Reporting.md) that post-date many rulings: no ruling's reasoning is undermined by the introduction of these domains. REQ-RPT-1.9 (as-of date filtering for trial balance) and REQ-JE-3.6.2 (optional as-of date for balance computation) are consistent with, not contradictory to, the AMB-AC-2 ruling about "balance" meaning cumulative net since inception.

Confirmed all Decisions.md archive entries: the IClock-to-AuditEnvelope overturn (2026-07-30) is already reflected in the SYS-CLK-1 and IE-AC-1 rulings. The instants-to-dates overturn (2026-06-22) is already reflected in the current AccountCrud.md spec (REQ-AC-1.42 through 1.50 use Calendar Dates).
