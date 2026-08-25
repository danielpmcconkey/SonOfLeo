# DataIngestion Spec Quality Auditor

## AMB-STG-1 — ambiguity
- **Location:** Specs/Behavioral/DataIngestion.md, REQ-STG-10.2
- **Summary:** Text filter criteria in REQ-STG-10.2 do not specify match semantics (exact vs partial, case sensitivity), unlike the sibling spec's REQ-CR-5.3 which annotates every text filter explicitly.
- **Resolution:** fix-spec

REQ-STG-10.2 lists filter criteria including 'description', 'ingestion source', 'FI reference', 'source file', and 'memo' without specifying whether these are exact match, partial match (LIKE/ILIKE), or case-sensitive. Within the same requirement, the date range filter IS annotated with '(begin and end inclusive),' showing the spec is willing to specify matching behavior inline. The sibling spec ClassificationRuleCrud.md REQ-CR-5.3 sets a clear precedent: every text filter carries an explicit annotation — 'name (partial match)', 'account-at-match (exact)', 'source pattern (partial match against rule group JSONB)'. The code (StageEntryOrchestration.fetchFiltered) implements exact equality for all text fields via '=' SQL operators. The classification rule filter type even encodes the distinction in its field names: 'nameLike' and 'sourceLike' (partial) vs 'accountAtMatch' (exact). The stage entry filter type uses plain names ('description', 'memo') without any 'Like' suffix, matching the exact-equality implementation. The most operationally impactful case is 'description'. Staged entry descriptions are raw FI strings (e.g., 'AMAZON MARKETPLACE AMZ ORDER 123-456-789'). With exact match, an operator querying for 'AMAZON' returns nothing. With partial match, it returns all Amazon transactions. Two developers implementing from the spec alone would diverge here, producing different observable behavior.

**Action:** Annotate each text filter criterion in REQ-STG-10.2 with its match type, following the REQ-CR-5.3 convention: e.g., 'description (exact match)', 'memo (exact match)'. If partial matching is desired for any field, the spec should say so and the implementation should be updated accordingly.

**Why:** Filter match type is observable behavior. Two developers implementing the same requirement could produce systems that return different result sets for the same query input. The sibling spec already solved this problem — DataIngestion.md should follow the same convention.

---

## IE-STG-1 — insufficient-elaboration
- **Location:** Specs/Behavioral/DataIngestion.md, REQ-STG-2.6
- **Summary:** REQ-STG-2.6 (source_file) omits a maximum length constraint, breaking the pattern every other string field in sections 1 and 2 follows.
- **Resolution:** fix-spec

REQ-STG-2.6 states: 'Staged entry source_file cannot be null. Records the full file path of the base staging format file that produced this entry.' Every other string field in sections 1 and 2 of this spec includes an explicit maximum length: REQ-STG-2.3 description 1000 chars, REQ-STG-2.5 fi_reference 100 chars, REQ-STG-2.15 memo 1000 chars, REQ-STG-1.9 description 1000, REQ-STG-1.10 fi_source 100, REQ-STG-1.11 fi_reference 100, REQ-STG-1.12 memo 1000. source_file is the sole exception. The code enforces a max length of 150 (StageEntryComponent.fs, SourceFile.maxLength = 150). The DB schema enforces character varying(150) on the column (migration 202608081415). A developer implementing from the spec alone would not know the limit exists.

**Action:** Add 'Maximum 150 characters.' to REQ-STG-2.6, matching the pattern used by every other string field in this spec.

**Why:** The spec is the authoritative source for data-state constraints. A constraint that exists only in the code and schema but not in the spec is invisible to spec-driven test writing, code review against the spec, and future audits. The consistency of the pattern (every other string field has a max length) makes the omission look accidental rather than intentional.

---

