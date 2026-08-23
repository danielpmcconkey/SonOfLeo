# hobson-ac-spec-auditor

## STALE-AC-1 — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md, line 3 (intro paragraph)
- **Summary:** Intro paragraph references "structural specs" that do not exist anywhere in the repository.
- **Resolution:** fix-spec

The opening paragraph states: "Structural constraints (FK, unique index) are covered separately in structural specs." No Specs/Structural/ directory exists. No file matching *structural* exists at any depth. Specs/README.md's "Where things live" table defines no structural-spec category. The repo's documentation system (Behavioral/, Definitions.md, Archive/, CompoundedLearnings/) has no slot for structural specs. The FK and unique-index constraints ARE defined — in DbMigrations/ SQL files — but the word "specs" in a repo with a formal spec system (Specs/Behavioral/, Specs/README.md) implies formal spec documents, not migration scripts. This is the only behavioral spec file that uses the phrase; no other spec references "structural specs."

**Action:** Replace "covered separately in structural specs" with language that accurately describes where structural constraints live (e.g., "defined in the database schema via DbMigrations/"), or remove the clause entirely since the behavioral requirements in section 1 already cover the behavioral implications of those constraints (REQ-AC-1.4 for unique code, REQ-AC-1.40 for parent FK, etc.).

**Why:** A developer or auditor reading this intro forms a mental model of the documentation system that includes a structural-spec category. When they go looking for it — to understand FK behavior, to audit structural coverage, or to add a new constraint — they find nothing. The reference is a vestige that no longer matches the repo's documentation architecture.

---

