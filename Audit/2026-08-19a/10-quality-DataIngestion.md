# DataIngestion-spec-auditor

## CON-STG-1 — contradiction
- **Location:** Specs/Behavioral/DataIngestion.md — REQ-STG-3.9, REQ-STG-5.1, REQ-STG-5.6, REQ-STG-5.8, REQ-STG-6.3, REQ-STG-9.7 vs REQ-STG-4.1
- **Summary:** Six requirements cite status values in lowercase that do not match the PascalCase canonical set defined by REQ-STG-4.1.
- **Resolution:** fix-spec

REQ-STG-4.1 defines the authoritative status set as: 'Ingested', 'Classified', 'NoMatch', 'Conflict', 'Reviewed', 'Duplicate', 'Posted', 'Ignored' (all PascalCase). Six other requirements reference these same statuses in lowercase: REQ-STG-3.9 ('ingested'), REQ-STG-5.1 ('ingested'), REQ-STG-5.6 ('conflict'), REQ-STG-5.8 ('classified'), REQ-STG-6.3 ('duplicate', 'reviewed'), REQ-STG-9.7 ('posted'). Within section 5 alone, REQ-STG-5.6 uses lowercase ('conflict') while REQ-STG-5.7 uses PascalCase ('NoMatch'). These are string values stored in and matched against a database column. The code (StageEntryComponent.fs lines 24-43) and the transition table in section 4 both use PascalCase exclusively. Because 'ingested' is not a member of the set {'Ingested', ...} defined by REQ-STG-4.1, these requirements technically direct the system to set an invalid status value per the canonical definition.

**Action:** Normalize all status value references throughout the spec to PascalCase, matching the canonical set in REQ-STG-4.1 and the code's StagedEntryStatus DU.

**Why:** Status values are string-typed database values where casing is semantically significant. A test writer implementing REQ-STG-3.9 as written would assert status equals 'ingested' (lowercase), which would fail against the code's PascalCase serialization. Internal consistency within the spec prevents this class of false-negative test failures.

---

## CON-STG-2 — contradiction
- **Location:** Specs/Definitions.md (Postable definition) vs Specs/Behavioral/DataIngestion.md REQ-STG-4.4
- **Summary:** Definitions.md defines Postable as requiring both status AND all-lines-coded; REQ-STG-4.4 explicitly excludes the all-lines-coded criterion.
- **Resolution:** dan-decides

Definitions.md defines Postable as: 'A staged entry whose status is Classified or Reviewed and whose every staged line has a non-null account_code.' REQ-STG-4.4 says: 'A staged entry is postable when its status is Classified or Reviewed. No additional filtering (e.g. line-level account_code presence) is applied.' The behavioral spec explicitly rejects the second conjunct of the Definition. Per the authority hierarchy (Definitions.md at level 2 outranks Behavioral specs at level 3), the Definition is the higher authority for the meaning of the term. The code (StageEntryOrchestration.fs lines 246-248) follows REQ-STG-4.4, fetching by status alone with no account_code check. If a Classified entry somehow retained a null account_code (broken upstream invariant), the Definition says it is not Postable; REQ-STG-4.4 says it is Postable but will fail loudly at posting. These produce different observable behavior: the Definition would exclude the entry from the postable set; the behavioral spec would include it and let it fail downstream.

**Action:** Align the two authorities. Either update Definitions.md to remove the account_code criterion (matching REQ-STG-4.4's trust-the-upstream design), or update REQ-STG-4.4 to check account_code presence (matching the Definition). Both positions are defensible; the current state is internally contradictory.

**Why:** When a term is defined in Definitions.md and then used in behavioral specs, the Definition is the shared contract that all specs lean on. A developer reading the Definition would implement an account_code presence filter; a developer reading REQ-STG-4.4 would not. The divergence is small in practice (upstream invariants are expected to hold) but the spec documents should agree on what the term means.

---
