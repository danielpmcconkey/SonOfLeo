# AccountCrud Auditor

_No findings._

## Reasoning

Audited AccountCrud.md (78 active requirements across sections 1-5, plus 35 withdrawn) against Definitions.md, SystemWide.md, the resolved-findings ledger (17 entries reviewed), and all 12 audit-conduct articles.

TERMINOLOGY CONSISTENCY WITH DEFINITIONS.MD: All key terms -- Calendar Date, Instant, Entity, Interface, Actors -- are used consistently. "Calendar Date (LocalDate)" in REQ-AC-1.42/1.43 aligns with the Definitions.md "Date (calendar)" entry. "Active begin"/"active end" are used as calendar dates throughout, consistent with the date-not-instant rationale in 1.42 and confirmed by the DB schema (migration 202606220851 altered columns to `date` type).

INTERNAL CONTRADICTIONS: Examined REQ-AC-1.48 (not-active definition) vs REQ-AC-1.50 (active definition). REQ-AC-1.48 defines "not active" only via active_end, which does not cover the "not yet started" case (active_begin > reference). However, REQ-AC-1.50 provides the complete positive "active" definition, and all behavioral requirements (3.9, 4.3, 2.7) reference the "active" concept from 1.50, not the "not active" concept from 1.48. Tests confirm: REQ-AC-1.50 tests include the "not yet started" path explicitly. The definitional asymmetry in 1.48 is academic -- no behavioral requirement depends on 1.48's narrower definition alone. The type-subtype constraint pairs (1.28-1.36) are bidirectionally exhaustive and internally consistent across all five account types and nine subtypes.

CONTRADICTIONS WITH SYSTEMWIDE.MD: REQ-AC-4.22's immutable field list correctly omits "modified at" because modified_at is mutable (auto-updated per REQ-SYS-3.3) while including "created at" which is truly immutable (set once per REQ-SYS-3.2). Withdrawn requirements correctly cite their system-wide replacements (2.1->SYS-1.1, 2.11/2.12->SYS-3.2, 4.7->SYS-3.3, 2.19/4.18->SYS-2.1, 2.15->SYS-5.1). The AuditEnvelope vs system-run-time distinction between mutation paths (REQ-AC-2.7, 4.3) and read paths (REQ-AC-3.9) is consistent with the resolved finding IE-AC-1.

AMBIGUITY CHECK (reasonable-person standard applied): No requirement would cause two competent developers to diverge. REQ-AC-2.14's "if the calling system specifies that the record should be saved to the DB" clearly describes a save/no-save mode. REQ-AC-4.8/4.9 (update name/external-reference) are capability-existence statements elaborated by section 1 data-state rules via REQ-SYS-2.1. REQ-AC-4.6's deactivation-vs-JE-lines check is precisely worded with explicit Calendar Date comparison semantics and inclusive boundary note.

WITHDRAWN TABLE: All 35 withdrawals have sound reasons. Supersession chains are valid (checked: 1.11-1.15 confirmed removed by migration 202606231237-RemoveAccountType.sql; 1.24-1.27 replaced by active_begin/active_end model; 4.10-4.16 consolidated into 4.22). The REQ-AC-1.19.1 withdrawal's action item ("rename tests to cite 1.19") has been completed (grep found zero test citations of 1.19.1). No withdrawn requirement leaves an uncovered behavioral gap.

WAIVED TABLE: All 20 waivers are sound. Verified against audit-conduct article "check-schema-before-questioning-waivers": type-system waivers (1.1, 1.6, 1.9, 1.16, 1.17, 1.21-1.23, 1.37, 1.41, 1.44) are justified by F# value types and discriminated unions. Structural-impossibility waivers (1.39, 1.47, 2.8, 2.16) are justified by system-generated UUIDs per REQ-AC-2.13. Coverage-by-proxy waivers (2.10->1.18, 2.18->1.46) are justified because the create-time constraint is identical to the data-state constraint already tested. Negative-existence waivers (4.22, 5.1) correctly note that API-surface absence cannot be proven by unit test.

UNENFORCEABLE TABLE: All 6 entries are sound. REQ-AC-1.48.1 is policy for spec authors. REQ-AC-2.17 explicitly disclaims validation. The four contextual annotations (2.6.1, 2.7.1, 3.3.1, 3.5.2) correctly identify non-behavioral notes.

THREE-STATE RULE: Every active requirement is accounted for as tested, waived, or unenforceable. No orphans found.

CONSIDERED AND REJECTED: (1) REQ-AC-1.42's rationale claim that "the only thing that ever reads these boundaries is the posting gate" is factually inaccurate (3.9, 4.3, 2.7 also read them), but the rationale's conclusion (day granularity suffices) remains correct, the requirement text itself is unambiguous, and no developer would implement differently based on the rationale prose -- cosmetic, not a finding. (2) Stricken/stricken capitalization inconsistency across entries -- style, not quality.
