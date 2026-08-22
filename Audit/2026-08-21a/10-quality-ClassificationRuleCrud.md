# cr-spec-auditor

_No findings._

## Reasoning

Audited ClassificationRuleCrud.md (54 active requirements: 45 tested, 9 waived, 0 unenforceable) against Definitions.md, SystemWide.md, DataIngestion.md cross-references, the resolved-findings ledger, and all 12 audit-conduct articles. Verified code in Src/Model/DataIngestion/Classification/*.fs, ModelOrchestrator/ClassificationOrchestration.fs, and DB migrations 202608081415/202608110820.

Checks performed and what was considered:

1. TERM CONSISTENCY WITH DEFINITIONS.MD: "Money" used correctly in REQ-CR-1.16/1.21 (refers to Definitions.md Money variety-of-number). Classification rule fits the Entity definition (created/mutated at runtime on behalf of user); entity-level policies (REQ-SYS-3.1 timestamps, REQ-SYS-2.1 data-state enforcement) are properly addressed in REQ-CR-4.5 and 6.5. "Staged entry" and "staged line" are not referenced directly but the "candidate" concept maps correctly to the staged-line evaluation context established in DataIngestion.md.

2. INTERNAL CONTRADICTIONS: Examined the defense-in-depth pattern where REQ-CR-1.12 (chain is non-empty list) and REQ-CR-1.7 (must contain at least one group) define valid states, REQ-CR-4.6/4.7/6.4 enforce at construction, and REQ-CR-2.8/2.9 provide evaluation backstops. No contradiction — the Why annotations on 2.8/2.9 explicitly acknowledge this is intentional layered defense against vacuous truth. Examined REQ-CR-4.4 (always created active) vs REQ-CR-4.8 (no mechanism to create inactive) — complementary positive/negative statements, not redundant. Examined REQ-CR-5.2 (retrieve by name, exact match) vs REQ-CR-5.3 (name partial match in filter) — two different retrieval mechanisms for different purposes.

3. CROSS-SPEC CONTRADICTIONS: Verified all cross-references: REQ-STG-5.1 (classification runs on Ingested entries), REQ-STG-5.3 (classifier cannot override parser assignments), REQ-STG-9.4 (account_code resolution at posting time) — all accurately cited. REQ-SYS-1.1 string trimming is properly addressed with the explicit carve-out in REQ-CR-1.18 for regex patterns (whitespace meaningful). REQ-SYS-6.1 no-silent-no-ops is addressed by REQ-CR-6.2 (all-NoChange rejection). The authority hierarchy design note ("parser > classifier > operator") is consistent with DataIngestion.md's design note and REQ-STG-6.1 (operator can override regardless).

4. AMBIGUITY (reasonable-person standard): Considered whether "evaluated as a regex" (REQ-CR-1.14) needs regex flavor specification — this is HOW, not WHAT (per specs-define-what-not-how audit conduct). Considered whether priority (REQ-CR-1.6) needs range constraints — "integer" is clear, negative values are valid (just higher priority), and duplicate priorities are explicitly handled by REQ-CR-3.5/3.6 tie-breaking. Considered whether "candidate" needs formal definition — standard pattern-matching terminology, consistently used, fields map naturally from REQ-CR-1.13 targets to staged-line fields. Considered whether REQ-CR-3.5's "full list of matches" is ambiguous about including the winner — it is not, verified against code (Classifier.fs line 25 passes all matches including winner).

5. INSUFFICIENT ELABORATION: Considered whether REQ-CR-5.2 (fetch by name) needs a uniqueness constraint on names, since no Section 1 requirement establishes name uniqueness. Two points: (a) the DB has no UNIQUE constraint on rule_name, confirming duplicate names are allowed by design; (b) the fetch-by-name with ExactlyOne expectation would fail on duplicates, surfacing the problem. This is a design choice (no uniqueness) not a spec gap. Considered whether the MatchCandidate structure needs formal definition in Section 1 — it is not a component of the ClassificationRule type, it is an input to evaluation, and the field mapping is domain-obvious from REQ-CR-1.13.

6. WITHDRAWN TABLE: No withdrawn requirements exist (0 stricken, 0 withdrawn). Nothing to audit.

7. WAIVED TABLE: All 9 waivers verified sound. REQ-CR-1.1/4.2 (UUID PK uniqueness) — enforced by PK constraint. REQ-CR-1.2/1.17 (null rejection) — F# type system plus DB NOT NULL/JSONB structure. REQ-CR-1.10 (chainOne required) — non-optional record field. REQ-CR-1.13 (single target) — five-case DU structural exclusivity. REQ-CR-1.21 (Money validation) — write-path validates through Money.fromDecimal, read-path follows Journaling precedent. REQ-CR-4.8/7.1 (negative existence claims) — correctly identified as unprovable by test, enforced by code review. Verified DB schema (202608081415, 202608110820) confirms NOT NULL constraints align with waiver claims per check-schema-before-questioning-waivers audit conduct.

8. THREE-STATE RULE: 54 active = 45 tested + 9 waived + 0 unenforceable. All 45 tested REQ IDs confirmed present in Tests/ via grep. No gaps.

9. STATEMENT-DELTA: Dan's statement describes classification rule CRUD as part of the data-ingestion slice. The spec fully covers create (Section 4), read (Section 5), update (Section 6), and deletion prohibition (Section 7), plus evaluation behaviors (Section 2) and classifier behaviors (Section 3). No divergence between the statement and the repo.

10. PRECEDENT LEDGER: No resolved findings match this spec's scope. The WAIVE-1 precedent (waiver reason soundness) informed my waiver review approach but applies to NGUI, not CR.
