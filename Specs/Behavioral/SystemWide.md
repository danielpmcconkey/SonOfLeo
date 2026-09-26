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
- **REQ-SYS-1.4** A partial-match ("contains") filter treats the caller's text literally. Characters that are special to the underlying matching mechanism (such as `%` and `_` in SQL `LIKE`) match only themselves.
  - *Why:* A search for "50%" must not match every description containing "50". (2026-09-26)

## 2. Legal data-state enforcement

- **REQ-SYS-2.1** Every operation that constructs, persists, or reconstitutes an entity — create, update, and read-from-persistence alike — must enforce that entity's legal data-state rules (the "valid and invalid data states" section of that entity's spec). No operation may produce, persist, or return an entity in an illegal data state.
- **REQ-SYS-2.1.1 Rejections determinable from the entity's own properties must occur before any database write. 
- **REQ-SYS-2.1.2 Rejections requiring database state may fall through to database constraints.
- **REQ-SYS-2.2** stricken

## 3. Audit

- **REQ-SYS-3.1** Every persisted entity must carry a "created at" and a "modified at" timestamp.
- **REQ-SYS-3.2** When a record is created, both "created at" and "modified at" Instant properties must be set to the initiation instant of the operation creating it (REQ-SYS-3.4). (Reworded 2026-09-26)
- **REQ-SYS-3.3** Every successful update to a record must set its "modified at" timestamp to the initiation instant of the operation performing the update (REQ-SYS-3.4). (Amended 2026-09-26 — was "the system clock at time of the update")
- **REQ-SYS-3.4** Every operation carries an auditable action identifying what the operation is, and a single initiation instant read from the system clock when the operation begins. Every timestamp the operation writes, and every "current date" it derives (e.g. the reference date for account activity, the start of a projection horizon), uses that instant.
  - *Why:* One operation, one moment. A batch that writes hundreds of rows records them as happening together, and date-dependent logic cannot straddle midnight partway through a run. (2026-09-26)
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
- **REQ-SYS-6.2** An operation that updates or deletes a record identified by ID, where no record has that ID, fails with a typed not-found error naming the kind of record and the ID. It must not surface as a generic database or row-count error.
- **REQ-SYS-6.3** When an operation sets a reference to another record (for example, a comment's secondary journal entry), the referenced record must exist. A missing referent fails with a typed not-found error before any write, on update as well as on create.
  - *Why:* Relying on the database's foreign key check produces an error the caller cannot act on. (2026-09-26)

## 7. Time zone

- **REQ-SYS-7.1** Converting an Instant to a calendar Date uses one configured local time zone, system-wide. When the time zone is not configured or is not a recognised time zone identifier, the system refuses to run rather than falling back to a default.
  - *Why:* Per the Date definition, mapping an Instant to a Date always requires a declared time zone. A silent fallback (UTC, or the host's zone) would shift late-evening activity into the next day. (2026-09-26)

## 8. Operation atomicity

- **REQ-SYS-8.1** Every operation triggered through the interface is atomic: either all of its database writes persist or none do. An operation that makes more than one write performs them in a single database transaction that commits only when the whole operation succeeds and rolls back otherwise, including when the operation raises an exception. Read-only operations and single-write operations may run without a transaction.
  - *Why:* The interface is the unit of work. A half-applied operation (a journal entry header without its lines, a staged batch half-posted) is an illegal state no later operation can be trusted to notice. (2026-09-26)

## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-SYS-3.1 | it's too general for a test and you can't test that there isn't a violation | Dan, 2026-07-06 |
| REQ-SYS-2.1.1 | it's too general for a test and you can't test that there isn't a violation | Dan, 2026-07-06 |
| REQ-SYS-2.1.2 | it's too general for a test and you can't test that there isn't a violation | Dan, 2026-07-06 |
| REQ-SYS-6.1 | This is a general requirement. Testing should be enforced by every individual write operation with a no-op possibility | Dan, 2026-07-06 |
| REQ-SYS-6.1.1 | simply untestable | Dan, 2026-07-06 |
| REQ-SYS-2.1 | Too general for a dedicated test — enforced per-entity by each entity's data-state tests and REQ-SYS-2.1.1/2.1.2 | Dan, 2026-08-02 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID          | Original Requirement | Reason |
|-------------|----------------------|--------|
| REQ-SYS-2.2 | Rejections under REQ-SYS-2.1 must occur before any database write, and must produce a meaningful error message. | replaced with 2.1.1 and 2.1.2 for better clarity |
| REQ-SYS-4.1 | The system must not provide a user interface for hard-deleting any entity record. | Deletion policy is per-entity, not system-wide (see Decisions, 2026-06-11). Account's prohibition restored to REQ-AC-5.1. |

