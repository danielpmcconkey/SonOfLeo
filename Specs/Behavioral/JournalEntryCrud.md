# Journal Entry CRUD

Service-level behavioral specs for posting, reading, and voiding journal entries, together with their external references and comments. Cross-cutting policies (string trimming, data-state enforcement, audit timestamps, no-op rejection, deletion) live in SystemWide.md and apply to everything below.

**Design note — temporal model.** A journal entry's `entry_date` is a Calendar Date (LocalDate), not an Instant: GAAP recognizes a transaction by calendar date and the time of day is meaningless (cash-basis: the day the money moved). Posting and audit events (`created_at`, `modified_at`, and the void marker `voided_at`) are Instants. Period assignment is derived from the entry date: parse year and month, construct the PeriodKey, fetch the period, check `is_open`.

**Design note — references.** What LeoBloom stored as one overloaded `reference` string is split into two distinct concepts:
- **External references** — an external transaction identifier (`reference`) and the source financial institution (`source_fi`) it belongs to. Audit traceability only.
- **Comments** — free text, which may additionally name a second entry to record a directional relationship (e.g. a correcting entry pointing at the one it corrects).

Deduplication of imported source rows is the **importer's** concern, handled in the stage layer — it is not a ledger concern, so the synthetic composite dedup keys LeoBloom kept on the ledger do not exist here.

**Design note — voiding and reversal.** Voiding is a soft-delete marker: a voided entry remains in the ledger (it is never edited or hard-deleted) but is excluded from all balance computations. Reversal is **not** a separate mechanism — a reversal is an ordinary offsetting entry plus a comment linking it to the original. A posted entry is not immutable (see REQ-JE-4.2).


## 1. Valid and invalid data states

### Header

- **REQ-JE-1.1** Journal entry ID cannot be null
- **REQ-JE-1.2** Journal entry ID must be unique
- **REQ-JE-1.3** Journal entry description cannot be null
- **REQ-JE-1.4** Journal entry description cannot be whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.5** Journal entry description length cannot exceed 1000 characters
- **REQ-JE-1.6** Journal entry source is optional (nullable)
- **REQ-JE-1.7** When provided, journal entry source cannot be whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.8** When provided, journal entry source length cannot exceed 50 characters
- **REQ-JE-1.9** Journal entry date cannot be null
- **REQ-JE-1.10** Journal entry date is a Calendar Date (LocalDate) with no time component
- **REQ-JE-1.11** Journal entry date must fall within the start and end dates (inclusive) of the fiscal period it is assigned to
- **REQ-JE-1.12** A journal entry must have at least 2 lines
- **REQ-JE-1.13** The sum of all debit line amounts must exactly equal the sum of all credit line amounts (balanced entry). Under the positive-amount + entry-type model (REQ-JE-1.24/1.25) this equality is the realization of the Decisions-log invariant "a journal entry's lines sum to zero"; the two are not in conflict.
- **REQ-JE-1.14** The void marker (`voided_at`) is a nullable Instant: null means the entry is active; a non-null value means the entry was voided at that instant. It is not one of the immutable posted fields (REQ-JE-4.1) — it changes only via the void operation (REQ-JE-4.3).

### Lines

- **REQ-JE-1.20** Journal entry line ID cannot be null
- **REQ-JE-1.21** Journal entry line ID must be unique
- **REQ-JE-1.22** Journal entry line must reference a valid account by UUID (the persisted foreign key). Codes are a boundary concern only (REQ-JE-2.3).
- **REQ-JE-1.23** Journal entry line amount must be a Money value (per Money.md)
- **REQ-JE-1.24** Journal entry line amount must be positive (greater than zero)
- **REQ-JE-1.25** Journal entry line entry type must be one of 'Debit' or 'Credit'
- **REQ-JE-1.26** Journal entry line memo is optional (nullable)
- **REQ-JE-1.27** When provided, journal entry line memo cannot be whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.28** When provided, journal entry line memo length cannot exceed 1000 characters
- **REQ-JE-1.29** Journal entry ID must belong to exactly one journal entry (`journal_entry_id` foreign key, not null)

### External references

- **REQ-JE-1.40** External reference ID cannot be null and must be unique (UUID)
- **REQ-JE-1.41** An external reference must belong to exactly one journal entry (`journal_entry_id` foreign key, not null)
- **REQ-JE-1.42** External reference source FI cannot be null or whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.43** Stricken
- **REQ-JE-1.44** External reference value cannot be null or whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.45** External reference value length cannot exceed 100 characters
- **REQ-JE-1.46** A journal entry may carry zero or more external references
- **REQ-JE-1.47** stricken
- **REQ-JE-1.48** Duplicate `(source_fi, reference)` pairs across different journal entries are permitted (uniqueness is not enforced across entries)
- **REQ-JE-1.49** External reference source FI length cannot exceed 100 characters

### Comments

- **REQ-JE-1.50** Comment ID cannot be null and must be unique (UUID)
- **REQ-JE-1.51** Comment primary journal entry ID cannot be null (`primary_journal_entry_id` foreign key)
- **REQ-JE-1.52** Comment secondary journal entry ID is nullable (`secondary_journal_entry_id` foreign key). When set, it records a directional relationship: the primary entry relates to (comments on, corrects, supersedes, voids) the secondary entry.
- **REQ-JE-1.53** When the secondary journal entry ID is set, it cannot equal the primary journal entry ID (an entry cannot link to itself)
- **REQ-JE-1.54** Comment text cannot be null or whitespace only (post-trim per REQ-SYS-1.1) and cannot exceed 2000 characters
- **REQ-JE-1.55** A journal entry may carry zero or more comments


## 2. Create (post) behaviors

- **REQ-JE-2.1** When posting a journal entry, the system must generate a unique UUID for the header ID (new UUIDs may not be passed in).
- **REQ-JE-2.2** When posting a journal entry, the system must generate a unique UUID for each line ID (new UUIDs may not be passed in).
- **REQ-JE-2.3** At the interface boundary, journal entry lines reference accounts by **code**. 
- **REQ-JE-2.4** When posting a journal entry, the system must reject any line whose account code does not resolve to an existing account (before any database write, per REQ-SYS-2.1.1).
- **REQ-JE-2.5** When posting a journal entry, the system must derive the fiscal period from the entry date: parse year and month, construct the PeriodKey, and look up the corresponding fiscal period record.
- **REQ-JE-2.6** When posting a journal entry, the system must reject any entry whose derived fiscal period does not exist in the database.
- **REQ-JE-2.7** When posting a journal entry, the system must reject any entry whose derived fiscal period is not open (`is_open = false`).
- **REQ-JE-2.8** When posting a journal entry, the system must reject any entry that references an account not active as of the entry date. The reference point is the entry date (a Calendar Date, per REQ-AC-1.48.1); an account is active when `active_begin <= entry_date AND (active_end IS NULL OR entry_date <= active_end)` (inclusive, per REQ-AC-1.50). This is a pure Calendar Date comparison — no instant conversion is involved.
- **REQ-JE-2.9** When posting a journal entry, the system must generate a unique UUID for each external reference and persist it with the entry.
- **REQ-JE-2.10** stricken
- **REQ-JE-2.11** When posting a journal entry, if all validations pass, the system must persist the header, all lines, and all external references atomically in a single database transaction, and return the fully constructed journal entry with all generated IDs and timestamps.
- **REQ-JE-2.12** When posting a journal entry, if any validation fails, no rows may be persisted (atomicity).
- **REQ-JE-2.13** The system must provide a means to post a new journal entry.


## 3. Read behaviors

- **REQ-JE-3.1** When retrieving a journal entry from the persistence layer, the system must return a JournalEntry type with all header properties, all associated lines, all external references, and all comments.
- **REQ-JE-3.2** The system must be able to retrieve a journal entry by the caller providing that entry's ID.
- **REQ-JE-3.3** The system must be able to retrieve all journal entries for a given fiscal period by the caller providing a PeriodKey.
- **REQ-JE-3.4** stricken
- **REQ-JE-3.5** The system must be able to retrieve the journal entries carrying a given external reference, by the caller providing a source FI and reference value. The result is a set (external references are not unique across entries, per REQ-JE-1.48).
- **REQ-JE-3.6** The system must be able to compute and return the total debit amount, total credit amount, and net balance (credits minus debits) for a given account's non-voided journal entry lines (per REQ-JE-4.7).
- **REQ-JE-3.7** The system must be able to retrieve all journal entries whose entry date falls within a caller-provided date range (start date and end date, both inclusive Calendar Dates). The result is a set of complete journal entries (per REQ-JE-3.1).
- **REQ-JE-3.8** The system must be able to retrieve all journal entries carrying at least one external reference whose source FI matches a caller-provided value. Unlike REQ-JE-3.5, this requires only the FI — no reference value. The result is a set of complete journal entries (per REQ-JE-3.1).
- **REQ-JE-3.9** The system must be able to retrieve all journal entry lines for a given account, enriched with their parent entry's `entry_date`, `description`, `source`, and `voided_at`. At the boundary the account is identified by code; the internal capability is by UUID. The caller may filter to non-voided entries only (per REQ-JE-4.7). The result is ordered by entry date. The enriched fields are a boundary-only return type — the domain model is unchanged.


## 4. Update and void behaviors

- **REQ-JE-4.1** The system must not provide a user interface for updating any of the following posted fields of a journal entry or its lines: entry date, description, source, and — per line — referenced account, amount, entry type, and memo. These fields are set when the entry is posted and have no update path.
- **REQ-JE-4.2** A posted journal entry is not immutable. The only changes permitted after posting are: (a) voiding the entry, which sets its void marker and excludes its lines from all balance computations; and (b) attaching or amending explanatory comments via the comment record. Neither path edits the posted fields enumerated in REQ-JE-4.1. Voiding deliberately changes an entry's effective contribution to ledger balances; for that reason, no spec, requirement, or tooling may characterize journal entries as immutable or append-only.
- **REQ-JE-4.3** The system must provide a means to void a posted journal entry, which sets the void marker (`voided_at`).
- **REQ-JE-4.4** Voiding a journal entry must record a reason as a comment on the voided entry (primary journal entry = the voided entry). A void with no reason — or a whitespace-only reason — is rejected (the comment fails REQ-JE-1.54).
- **REQ-JE-4.5** Voiding is rejected when the entry's derived fiscal period is not open. A voided period cannot be re-opened by voiding within it; closed-period corrections go through an offsetting entry (REQ-JE-4.8).
- **REQ-JE-4.6** Voiding an already-voided entry must produce an error rather than update nothing, per REQ-SYS-6.1. *Why:* this diverges deliberately from LeoBloom, which made re-void idempotent; a silent no-op masks a caller working from a stale view of the entry's state.
- **REQ-JE-4.7** Voided journal entries must be excluded from every balance, trial-balance, and account-sum computation. The exclusion must be applied such that a voided entry's lines contribute nothing (see the leobloom_prod skill's note on the `LEFT JOIN ... AND voided_at IS NULL` overstatement trap — the void check belongs in the `WHERE`, not the join).
- **REQ-JE-4.8** Corrections to an entry in a closed period are made by posting an ordinary offsetting journal entry into the current open period and linking it to the original with a comment (secondary journal entry = the original). There is no separate reversal operation.
- **REQ-JE-4.9** The system must provide a means for an actor to update a journal entry reference's FI and value
- **REQ-JE-4.10** The system must provide a means to attach a new external reference to an existing journal entry, by the caller providing a journal entry ID, a source FI, and a reference value. The system must generate a unique UUID for the new reference and persist it (per REQ-JE-2.9 semantics). A reference may be appended regardless of whether the entry is voided or its fiscal period is closed (mirrors REQ-JE-5.5 for comments).

## 5. Comment behaviors

- **REQ-JE-5.1** The system must provide a means to attach a comment to a journal entry, optionally naming a secondary journal entry to record a directional relationship.
- **REQ-JE-5.2** When a comment is created, the system must generate a unique UUID and set its created/modified timestamps (per REQ-SYS-3.2).
- **REQ-JE-5.3** The system must provide a means to amend a comment's text. Amending updates the modified-at timestamp (per REQ-SYS-3.3).
- **REQ-JE-5.4** stricken
- **REQ-JE-5.5** A comment may be appended to an existing journal entry, even if the JE is voided or if the JE's fiscal period is closed.
- **REQ-JE-5.6** A comment's primary journal entry link is fixed once created. The primary relationship a comment records is a historical fact and must not be re-pointed.

## 6. Deletion behaviors

- **REQ-JE-6.1** The system must not provide a user interface for hard-deleting a journal entry or its lines.
- **REQ-JE-6.2** The system must not provide a user interface for hard-deleting a journal entry's external references or comments; both are audit data.


## Waived from testing

Active requirements that are deliberately not verified by tests. Two-state rule: every
active requirement is either tested or in this table.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-JE-1.1 | Guid is a value type — the solution won't build if you try to pass a null ID. | Dan, 2026-07-03 |
| REQ-JE-1.2 | The ID is system-generated (generation tested under REQ-JE-2.1) and the primary key constraint enforces uniqueness; a collision cannot be meaningfully provoked in a test. | Dan, 2026-07-03 |
| REQ-JE-1.9 | LocalDate is a value type — can't be null. | Dan, 2026-07-03 |
| REQ-JE-1.10 | Quite obviously enforced in the type definition — LocalDate carries no time component. | Dan, 2026-07-03 |
| REQ-JE-1.20 | Same as REQ-JE-1.1 — Guid value type. | Dan, 2026-07-03 |
| REQ-JE-1.23 | Enforced in the type definition — the line amount is a MoneyRecord constructed via MoneyModule.fromDecimal; Money validation has its own isolated tests. | Dan, 2026-07-03 |
| REQ-JE-1.29 | journalEntryId is a non-nullable Guid on the line type and a not-null FK in the schema; a line cannot be constructed without exactly one parent entry. | Dan, 2026-07-03 |
| REQ-JE-1.41 | Same shape as REQ-JE-1.29 — non-nullable Guid plus not-null FK. | Dan, 2026-07-03 |
| REQ-JE-1.50 | Same as REQ-JE-1.2 — system-generated UUID plus primary key constraint. | Dan, 2026-07-03 |
| REQ-JE-1.51 | Non-nullable Guid plus not-null FK; existence of the primary entry is validated at construction (validateJournalEntryHeader, exercised by every comment test). | Dan, 2026-07-03 |
| REQ-JE-4.1 | A negative existence claim over the entire API surface ("no function exposes an update path for these fields") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Dan, 2026-06-22 |
| REQ-JE-4.2 | The prohibition "no spec, requirement, or tooling may characterize journal entries as immutable" is a negative existence claim over documentation and the API surface; the positive behaviors it depends on (void, comments) are tested under REQ-JE-4.3/4.7/5.x. Enforced by review. | Dan, 2026-06-22 |
| REQ-JE-4.8 | We can test against voiding in a closed period, but we can't actually test that someone would, instead, create an offset | Dan, 2026-06-22 | 
| REQ-JE-6.1 | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit. | Dan, 2026-06-22 |
| REQ-JE-6.2 | Same negative-existence rationale as REQ-JE-6.1, extended to external references and comments. | Dan, 2026-06-22 |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
| — | "References and voiding are deferred" (design note) | Reversed: prod usage showed references on 97% of entries and voids on 10%. References are modeled as external references + comments; voiding is specced in §4. |
| REQ-JE-1.43 | External reference source FI must be one of the recognized source financial institutions (a controlled vocabulary, not free text). An unrecognized value is rejected. | I don't want to constrain this field for a personal application |
| REQ-JE-1.47 | External references are write-once: they carry a `created_at` Instant, are set when the entry is posted (or appended thereafter), and are never edited | Bullshit. We'll fat finger this someday. And then what? |
| REQ-JE-2.10 | When posting a journal entry, the source FI of each external reference must be a recognized value (REQ-JE-1.43); an unrecognized value rejects the post. | same reason as 1.43 |
| REQ-JE-3.4 | The system must be able to retrieve all journal entry lines for a given account. At the boundary the account is identified by code; the internal capability is by UUID (mirrors REQ-AC-3.4 / 3.3.1). | Replaced by REQ-JE-3.9 — bare lines without parent entry context (entry date, description) are not useful for account activity review |
| REQ-JE-5.4 | A comment's primary and secondary journal entry links are fixed once created; only the comment text may be amended. The relationship a comment records is a historical fact and must not be re-pointed. | Too restrictive and no value add |
