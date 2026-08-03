# quality:AccountCrud

## AC-DUP-1 — other
- **Location:** Specs/Behavioral/AccountCrud.md, Unenforceable table (lines 173 and 175)
- **Summary:** REQ-AC-3.3.1 appears twice in the Unenforceable table with identical text.
- **Resolution:** fix-spec

The Unenforceable table contains two rows for REQ-AC-3.3.1, both reading: 'A contextual annotation ("this is an internal-only capability"), not a testable behavior. Nothing in the system enforces internal-vs-external access boundaries | Dan, 2026-08-02'. This inflates the unenforceable count (7 entries for 6 unique requirements). The scout already noted this discrepancy. It is a copy-paste artifact from when the table was populated.

**Action:** Delete one of the two REQ-AC-3.3.1 rows from the Unenforceable table.

**Why:** The unenforceable table is a governance artifact that auditors and the commit gate rely on. A duplicate row creates ambiguity about whether it was intentional (two distinct unenforceability reasons) or accidental, and it inflates counts used for three-state-rule verification.


## AC-AMB-1 — ambiguity
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-1.48 (line 54) vs REQ-AC-1.50 (line 57)
- **Summary:** REQ-AC-1.48's parenthetical '(or "inactive")' equates 'inactive' with 'deactivated,' but deactivated is not the complement of 'active' as defined in REQ-AC-1.50, creating a false synonym.
- **Resolution:** fix-spec
- **Prior ruling:** This exact finding was raised as AMB-AC-3 in the 2026-07-06a audit (02-B-Account-CRUD-Spec-Quality-Reviewer.md). It does not appear in resolved-findings.md, meaning it was either filtered by the orchestrator before reaching Dan or was seen but never formally ruled on. Re-raising because the '(or "inactive")' text is unchanged and no ruling exists.

REQ-AC-1.48 defines 'deactivated (or "inactive")' as: active-end is non-null AND earlier than the reference date. REQ-AC-1.50 defines 'active' as: active-begin <= reference AND (active-end null OR active-end >= reference). An account whose active-begin is in the future relative to the reference date fails the 'active' test (1.50) but also fails the 'deactivated' test (1.48, because active-end may be null). This means there is a legitimate third state: not-yet-begun. The '(or "inactive")' synonym invites a future spec or test author to read 'inactive' as the natural boolean complement of 'active,' which would wrongly include not-yet-begun accounts in an 'inactive' filter. Currently 'inactive' appears nowhere else in any behavioral spec (verified by grep across all Specs/Behavioral/*.md), so the blast radius is zero today, but the synonym is a latent trap for downstream specs.

**Action:** Drop the '(or "inactive")' parenthetical from REQ-AC-1.48, or add a one-line note clarifying that 'deactivated' is not the complement of 'active' and that a pre-begin account is neither.

**Why:** Tri-state activity (not-yet-begun / active / deactivated) is a common source of off-by-one selection bugs. A synonym that reads as a boolean complement is a latent trap; when a future spec writer reaches for 'inactive accounts' meaning 'all accounts that are not active,' they will find the synonym in 1.48 and implement the narrower 'deactivated' definition, silently excluding not-yet-begun accounts.



