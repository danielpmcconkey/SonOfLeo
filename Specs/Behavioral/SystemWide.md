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
- **REQ-SYS-2.2** Where possible, rejections under REQ-SYS-2.1 must occur before any database write, and must produce a meaningful error message.

## 3. Audit timestamps

- **REQ-SYS-3.1** Every persisted entity must carry a "created at" and a "modified at" timestamp.
- **REQ-SYS-3.2** When a record is created, both timestamps must be set to the system clock at time of creation.
- **REQ-SYS-3.3** Every successful update to a record must set its "modified at" timestamp to the system clock at time of the update.

## 4. Deletion

- **REQ-SYS-4.1** The system must not provide a user interface for hard-deleting any entity record. (untestable)

## 5. Persistence fidelity

- **REQ-SYS-5.1** The persistence layer must persist all entity properties in such a way that the entity type can be perfectly reconstituted upon subsequent read.

## Promotion candidates

Rules that look general but stay entity-specific until a second entity confirms them:

- **REQ-AC-2.13** (IDs are system-generated UUIDs; new UUIDs may not be passed in) — revisit when journal entry creation is specced.
