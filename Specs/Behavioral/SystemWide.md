# System-Wide Requirements

Cross-cutting policies that apply to every entity and every operation in the system. Entity
specs (e.g., AccountCrud.md) define each entity's legal data states and entity-specific
behaviors; this document defines the policies common to all of them and where those rules are
enforced. A rule belongs here only when the specific, testable detail behind it lives in an
entity spec (usually its legal-data-states section) or the rule itself is directly testable
per entity. Generic requirements state policy and scope, not vague aspiration.

## 1. String handling

- **REQ-SYS-1.1** All raw string inputs must be trimmed of leading and trailing white space at the system boundary, before validation, before persistence, and before being returned to the caller.
- **REQ-SYS-1.2** A required (non-nullable) text field may never hold a value that is empty or whitespace-only post-trim.
- **REQ-SYS-1.3** An optional (nullable) text field, when provided, may never hold a value that is empty or whitespace-only post-trim. Absence must be represented as null, never as an empty string.

## 2. Legal data-state enforcement

- **REQ-SYS-2.1** Every operation that constructs, persists, or reconstitutes an entity — create, update, and read-from-persistence alike — must enforce that entity's legal data-state rules (the "valid and invalid data states" section of that entity's spec). No operation may produce, persist, or return an entity in an illegal data state.
- **REQ-SYS-2.1.1 Rejections determinable from the entity's own properties must occur before any database write. 
- **REQ-SYS-2.1.2 Rejections requiring database state may fall through to database constraints.
- **REQ-SYS-2.2** stricken

## 3. Audit

- **REQ-SYS-3.1** Every persisted entity must carry a "created at" and a "modified at" timestamp.
- **REQ-SYS-3.2** When a record is created, both "created at" and "modified at" Instant properties must be set to the AuditEnvelope's system instant property at time of creation.
- **REQ-SYS-3.3** Every successful update to a record must set its "modified at" timestamp to the system clock at time of the update.
todo: add a requirement for logging audit activities to an external log

## 4. Deletion

No system-wide deletion policy. Whether an entity's records may be hard-deleted is a
domain-level decision, made in each entity's spec (for Accounts, see REQ-AC-5.1).

- **REQ-SYS-4.1** stricken

## 5. Persistence fidelity

- **REQ-SYS-5.1** The persistence layer must persist all entity properties in such a way that the entity type can be perfectly reconstituted upon subsequent read.

## 6. State transitions

- **REQ-SYS-6.1** No state-transition operation may silently succeed as a no-op. When a requested operation would change nothing — because the target entity is already in the requested state, or because the record the operation would create already exists — the operation must produce an error rather than update or insert nothing. A silent no-op masks a caller that believes the system is in a different state than it is, hiding an upstream problem the system should surface. Per-entity instances cite this rule (e.g., REQ-FP-4.1.1 close-already-closed, REQ-FP-4.2.1 reopen-already-open, REQ-AC-2.9 / REQ-FP-2.2 duplicate creation, and journal-entry void-already-voided).
- **REQ-SYS-6.1.1** Any exception to REQ-SYS-6.1 (an operation deliberately permitted to be idempotent) must be stated explicitly in the relevant entity spec; absent such a statement, the no-op rejection applies.

## Waived from testing

Active requirements that are deliberately not verified by tests. Two-state rule: every
active requirement is either tested or in this table.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-SYS-6.1 | This is a general requirement. Testing should be enforced by every individual write operation with a no-op possibility | Dan, 2026-07-06 |

## Withdrawn

| ID          | Original Requirement | Reason |
|-------------|----------------------|--------|
| REQ-SYS-2.2 | Rejections under REQ-SYS-2.1 must occur before any database write, and must produce a meaningful error message. | replaced with 2.1.1 and 2.1.2 for better clarity |
| REQ-SYS-4.1 | The system must not provide a user interface for hard-deleting any entity record. | Deletion policy is per-entity, not system-wide (see Decisions, 2026-06-11). Account's prohibition restored to REQ-AC-5.1. |


## Promotion candidates

Rules that look general but stay entity-specific until a second entity confirms them:

- **REQ-AC-2.13** (IDs are system-generated UUIDs; new UUIDs may not be passed in) — revisit when journal entry creation is specced.
