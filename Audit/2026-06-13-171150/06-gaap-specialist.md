# gaap-account-domain-auditor

**Findings: 7**


---

## MR-AC-PARENT-TYPE
- **Category:** missing-requirement
- **Severity:** high
- **Location:** /workspace/SonOfLeo/Specs/Behavioral/AccountCrud.md, Section 2 (Create behaviors)
- **Summary:** No requirement constrains a child account to share its parent's AccountType.

The spec validates that a parent exists (REQ-AC-2.6), is active (REQ-AC-2.7), and is not a descendant (REQ-AC-2.16), but nothing prevents creating an Expense account under an Asset parent. In a standard chart of accounts, the parent-child hierarchy is within a single account type: asset accounts nest under asset headers, expense accounts nest under expense headers, etc. Without this constraint, the hierarchy can produce a chart of accounts where subtree rollups cross type boundaries -- e.g., an Expense leaf rolling up under an Asset header -- which makes balance aggregation by walking the tree produce nonsensical numbers. The LeoBloom seed data (AccountSubTypes.feature, FT-AST-015 through FT-AST-017) confirms this convention is followed in practice: every child's account code shares its parent's leading digit (1xxx under 1000, 2xxx under 2000, etc.), meaning they share the same type. But the constraint is enforced only by convention, not by the spec or the code. Any future agent or CLI user could violate it.

**Suggested action:** Add a requirement in Section 2 (Create behaviors): 'When creating an Account record with a parent ID, the system must reject the creation if the child account's AccountType differs from the parent account's AccountType.' Consider whether subtype matching should also be constrained (less clear-cut -- a Cash asset under a generic Asset header is fine).

**Why:** Without this, tree-based balance aggregation (the standard way to compute subtotals for reporting) will silently mix account types, producing incorrect subtotals. This is not an edge case -- 'account list' is used weekly (Hobson's notes Section 1), and any reporting that walks the hierarchy will break.


---

## MR-AC-FETCH-ALL
- **Category:** missing-requirement
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Specs/Behavioral/AccountCrud.md, Section 3 (Read behaviors)
- **Summary:** No requirement exists for retrieving all accounts (the full chart of accounts).

Section 3 provides fetch-by-id (REQ-AC-3.3), fetch-by-code (REQ-AC-3.4), fetch-by-parent (REQ-AC-3.5), and fetch-by-type (REQ-AC-3.6). There is no fetch-all or list-all requirement. Hobson's notes (Section 1) explicitly list 'account list' as used every week for COA sync against the markdown reference. The withdrawn requirements REQ-AC-3.5.1 and REQ-AC-3.6.1 (active-only filtering) were removed because of the new active/inactive model, but the base 'list everything' capability was never added. The code's readRowsFromDb function supports it technically (predicate and limit are both optional), but no public function or requirement exposes it.

**Suggested action:** Add a requirement: 'The system must be able to retrieve all Account records.' This is the simplest of the read operations and the one most frequently used in practice.

**Why:** The weekly COA sync (the most common account read operation per Hobson) requires listing all accounts. Without this requirement, the CLI cannot implement 'account list', which is documented as a weekly necessity.


---

## CVV-AC-DB-DEFAULTS
- **Category:** convention-violation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/DbMigrations/2026-06-01-07-48-CreateAccountTable.sql, lines 45-48
- **Summary:** Database migration uses DEFAULT now() on temporal columns, violating the Temporal convention.

The Temporal convention (Conventions/Temporal.md lines 18-20) states: 'The persistence layer may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.)' and 'Required (non-nullable) temporal columns carry no defaults; a write that omits the value is rejected, never filled in by the database.' However, the account table migration applies DEFAULT now() to active_begin (line 45), created_at (line 47), and modified_at (line 48). The application code does supply these values correctly via AuditEnvelope, so this is a defense-in-depth violation rather than an active bug. But the whole point of the convention is that a write that accidentally omits a timestamp should fail loudly, not silently succeed with a database-generated value that might differ from what the application intended.

**Suggested action:** Remove the DEFAULT now() clauses from active_begin, created_at, and modified_at in the account table DDL. A new migration to ALTER COLUMN ... DROP DEFAULT would suffice.

**Why:** The convention exists specifically to catch application bugs where a temporal value is accidentally omitted. With defaults in place, such a bug would silently succeed, potentially creating audit timestamp discrepancies between what the AuditEnvelope recorded and what the database stored.


---

## AMB-AC-1.47
- **Category:** ambiguity
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Specs/Behavioral/AccountCrud.md, REQ-AC-1.47
- **Summary:** REQ-AC-1.47 (no descendant as parent) is declared as a data-state rule but its enforcement is structurally impossible and the code acknowledges this without the spec doing so.

REQ-AC-1.47 states: 'An Account record's parent ID can never reference one of its descendent accounts. (This will be difficult to enforce.)' The parenthetical editorial note acknowledges the difficulty but the requirement remains active and is not in the waived-from-testing table. Meanwhile, the code comment at line 321-329 of Account.fs explains that circular ancestry is structurally impossible because (a) child IDs are generated at insertion time (so a new child cannot already have descendants), and (b) REQ-AC-4.22 forbids reparenting. This is a sound argument, but the spec still lists REQ-AC-1.47 as an active data-state rule that, per REQ-SYS-2.1, must be enforced at every construct/persist/reconstitute operation. The code deliberately does NOT enforce it at those boundaries. The requirement and the implementation have diverged, and neither the spec nor the waived-from-testing table reflects this.

**Suggested action:** Either (1) move REQ-AC-1.47 to the waived-from-testing table with the structural impossibility argument as the reason, or (2) rewrite it as a derived invariant that holds by construction (citing REQ-AC-2.13 and REQ-AC-4.22 as the guarantors) rather than a rule that must be checked.

**Why:** As written, REQ-AC-1.47 is an active requirement that is neither tested nor waived. The traceability convention requires every active requirement to be in exactly one of those two states. An auditor following the spec literally would flag this as unenforced.


---

## AMB-AC-1.48-BEFORE-BEGIN
- **Category:** ambiguity
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Behavioral/AccountCrud.md, REQ-AC-1.48
- **Summary:** REQ-AC-1.48 does not define the active status of an account when the reference point is before active_begin.

REQ-AC-1.48 defines when an account is 'deactivated': active_end is non-null and <= reference point. But it never defines what happens when the reference point is BEFORE active_begin. Is the account active? Inactive? In a third state? The code treats this as inactive (the isActive function returns false when beginDate > referencePoint, line 48 falls through to the catch-all false). The confirmAccountIsValidAndActive function (lines 306, 308) also rejects this case explicitly. But the spec is silent on it. For accounting purposes, an account that hasn't started yet is not active, but the spec should say so explicitly since REQ-AC-1.48.1 goes to the trouble of saying the reference point is context-dependent.

**Suggested action:** Add a companion to REQ-AC-1.48: 'An Account record is considered inactive (not yet active) when the reference point in time is earlier than its active_begin date.'

**Why:** The spec defines deactivation but not pre-activation. While the code handles this correctly, the gap means a future implementer could interpret the silence differently, especially since REQ-AC-1.48 only covers the active_end case.


---

## MR-AC-EQUITY-SUBTYPES
- **Category:** insufficient-elaboration
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Behavioral/AccountCrud.md, REQ-AC-1.32
- **Summary:** Equity accounts accept no subtypes, but personal finance equity accounts have meaningfully different roles.

REQ-AC-1.32 says 'Account records of type Equity can only have null subtypes.' The LeoBloom seed data shows three equity leaf accounts (3010, 3020, 3099) all with null subtypes. In personal finance double-entry, equity accounts typically serve distinct roles: owner's capital/contributions, retained earnings (or net income carry-forward), and owner's draws/distributions. These are functionally different -- retained earnings accumulates automatically at period close (or is computed from the accounting identity), while owner's contributions and draws are manual transactions. Since the system explicitly rejects closing entries (Hobson's notes Section 4), the equity structure matters less now. However, the Hobson notes include the caveat 'Keep the schema able to support a period close someday.' If period close ever arrives, the system will need to distinguish retained earnings from other equity accounts, and the current flat equity structure with no subtypes provides no way to do that without a schema change. This is a 'someday' concern, not an immediate accounting problem.

**Suggested action:** Consider adding equity subtypes (e.g., OwnerContribution, RetainedEarnings, OwnerDistribution) to future-proof the schema. Alternatively, document as a decision that equity subtypes are intentionally deferred until period close is designed.

**Why:** If period close is ever implemented, the closing entry needs to target a specific equity account (retained earnings). Without a subtype or other mechanism to identify it, the system would need a hard-coded account code or a schema change at that point.


---

## SA-AC-1.25-1.26
- **Category:** stale-annotation
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/Account.fs, lines 100-101
- **Summary:** Code comments reference withdrawn requirement IDs REQ-AC-1.25 and REQ-AC-1.26.

Lines 100-101 of Account.fs annotate the createdAt and modifiedAt assignments with comments referencing REQ-AC-1.25 and REQ-AC-1.26. Both of these requirements were withdrawn (see AccountCrud.md Withdrawn table) and superseded by REQ-SYS-3.2. The traceability convention says code annotations reference the requirement being enforced. Pointing to withdrawn IDs could confuse a future auditor who searches for these IDs and finds them in the withdrawn table.

**Suggested action:** Update the annotations on lines 100-101 to reference REQ-SYS-3.2 instead of the withdrawn REQ-AC-1.25 and REQ-AC-1.26.

**Why:** Stale requirement annotations undermine the traceability system. An auditor searching for REQ-AC-1.25 enforcement points would find code referencing a withdrawn requirement, creating confusion about whether the code is implementing current or obsolete behavior.
