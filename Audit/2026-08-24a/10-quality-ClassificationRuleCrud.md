# classification-rule-crud-spec-auditor

## AMB-CR-1 — ambiguity
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md, REQ-CR-5.2 vs section 1
- **Summary:** REQ-CR-5.2 uses singular wording ('a classification rule by its name') implying name uniqueness, but section 1 imposes no uniqueness constraint on rule names.
- **Resolution:** fix-spec

REQ-CR-5.2 says 'The system must be able to retrieve a classification rule by its name (exact match)' -- singular, implying at most one result for a given name. The code implements this with an ExactlyOne row expectation (ClassificationRule.fs line 163). However, section 1's data states (REQ-CR-1.1 through 1.21) impose no uniqueness constraint on rule_name. The database has no UNIQUE constraint on rule_name (migration 202608220920-RebuildClassificationRule.sql). The creation orchestrator (ClassificationOrchestration.fs createNewClassificationRule, lines 55-79) does not check for name collisions. If a user creates two rules with the same name (currently permitted by every layer), fetchByName fails with a raw DAL row-count error rather than a meaningful domain error. AccountCrud.md handles this pattern correctly: REQ-AC-1.4 requires account code uniqueness, and REQ-AC-2.9 rejects duplicate codes at creation time, backing up the singular 'retrieve by code' semantics.

**Action:** Either add a data-state requirement for unique rule names (paralleling REQ-AC-1.4) with creation-time and update-time duplicate checks, or rewrite REQ-CR-5.2 to specify the expected behavior when multiple rules share a name (e.g., error, return first, return list).

**Why:** Two reasonable developers reading this spec would diverge. One reads REQ-CR-5.2's singular wording and enforces name uniqueness at creation; the other reads section 1's data states as the complete validation surface and permits duplicates. The result is either silently permitted duplicates that produce a cryptic DAL error on a routine retrieval, or inconsistent enforcement depending on which spec section the developer treats as authoritative.

---
