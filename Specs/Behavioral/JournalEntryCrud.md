# Journal Entry CRUD

Service-level behavioral specs for posting, reading, and voiding journal entries, together with their external references and comments. Cross-cutting policies (string trimming, data-state enforcement, audit timestamps, no-op rejection, deletion) live in SystemWide.md and apply to everything below.

**Design note — temporal model.** A journal entry's `entry_date` is a Calendar Date (LocalDate), not an Instant: GAAP recognizes a transaction by calendar date and the time of day is meaningless (cash-basis: the day the money moved). Posting and audit events (`created_at`, `modified_at`, and the void marker `voided_at`) are Instants. Period assignment is derived from the entry date: parse year and month, construct the PeriodKey, fetch the period, check `is_open`.

**Design note — references.** What LeoBloom stored as one overloaded `reference` string is split into two distinct concepts:
- **External references** — an external transaction identifier (`reference`) and the source financial institution (`source_fi`) it belongs to. Audit traceability only.
- **Comments** — free text, which may additionally name a second, related entry. The link records no direction and no relation; any specific relation is stated in the comment's text. The one exception is a void-reason comment, whose secondary entry is the voided entry's replacement (REQ-JE-4.4).

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
- **REQ-JE-1.13** The sum of all debit line amounts must exactly equal the sum of all credit line amounts (balanced entry). (Amended 2026-10-05)
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
- **REQ-JE-1.29** A journal entry line must belong to exactly one journal entry (`journal_entry_id` foreign key, not null)

### External references

- **REQ-JE-1.40** External reference ID cannot be null and must be unique (UUID)
- **REQ-JE-1.41** An external reference must belong to exactly one journal entry (`journal_entry_id` foreign key, not null)
- **REQ-JE-1.42** External reference source FI cannot be null or whitespace only (post-trim per REQ-SYS-1.1). The null clause is waived (see REQ-JE-1.3).
- **REQ-JE-1.43** Stricken
- **REQ-JE-1.44** External reference value cannot be null or whitespace only (post-trim per REQ-SYS-1.1). The null clause is waived (see REQ-JE-1.3).
- **REQ-JE-1.45** External reference value length cannot exceed 100 characters
- **REQ-JE-1.46** A journal entry may carry zero or more external references
- **REQ-JE-1.47** stricken
- **REQ-JE-1.48** Duplicate `(source_fi, reference)` pairs across different journal entries are permitted (uniqueness is not enforced across entries)
- **REQ-JE-1.49** External reference source FI length cannot exceed 100 characters

### Comments

- **REQ-JE-1.50** Comment ID cannot be null and must be unique (UUID)
- **REQ-JE-1.51** Comment primary journal entry ID cannot be null (`primary_journal_entry_id` foreign key)
- **REQ-JE-1.52** Comment secondary journal entry ID is nullable (`secondary_journal_entry_id` foreign key). When set, it names a related entry. The link carries no direction and no stored relation: it records only that the two entries are related. REQ-JE-4.4 gives the secondary entry of a void-reason comment its meaning (the voided entry's replacement); any other relation is stated in the comment's text. (Amended 2026-10-03, 2026-10-05)
- **REQ-JE-1.53** When the secondary journal entry ID is set, it cannot equal the primary journal entry ID (an entry cannot link to itself)
- **REQ-JE-1.54** Comment text cannot be null or whitespace only (post-trim per REQ-SYS-1.1) and cannot exceed 2000 characters. The null clause is waived (see REQ-JE-1.3).
- **REQ-JE-1.55** A journal entry may carry zero or more comments
- **REQ-JE-1.56** Comment secondary journal entry ID may be updated to be pointed at a different JE or to no JE


## 2. Create (post) behaviors

- **REQ-JE-2.1** When posting a journal entry, the system must generate a unique UUID for the header ID (new UUIDs may not be passed in).
- **REQ-JE-2.2** When posting a journal entry, the system must generate a unique UUID for each line ID (new UUIDs may not be passed in).
- **REQ-JE-2.3** At the interface boundary, journal entry lines reference accounts by **code**. 
- **REQ-JE-2.4** When posting a journal entry, the system must reject any line whose account code does not resolve to an existing account (before any database write).
- **REQ-JE-2.5** When posting a journal entry, the system must derive the fiscal period from the entry date: parse year and month, construct the PeriodKey, and look up the corresponding fiscal period record.
- **REQ-JE-2.6** When posting a journal entry, the system must reject any entry whose derived fiscal period does not exist in the database.
- **REQ-JE-2.7** When posting a journal entry, the system must reject any entry whose derived fiscal period is not open (`is_open = false`).
- **REQ-JE-2.8** When posting a journal entry, the system must reject any entry that references an account not active as of the entry date. The reference point is the entry date (a Calendar Date, per REQ-AC-1.48.1); an account is active when `active_begin <= entry_date AND (active_end IS NULL OR entry_date <= active_end)` (inclusive, per REQ-AC-1.50). This is a pure Calendar Date comparison — no instant conversion is involved.
- **REQ-JE-2.9** When posting a journal entry, the system must generate a unique UUID for each external reference and persist it with the entry.
- **REQ-JE-2.10** stricken
- **REQ-JE-2.11** When posting a journal entry, if all validations pass, the system must persist the header, all lines, all external references, and all comments atomically in a single database transaction, and return the fully constructed journal entry with all generated IDs and timestamps. (Comments added 2026-09-26)
- **REQ-JE-2.12** When posting a journal entry, if any validation fails, no rows may be persisted (atomicity).
- **REQ-JE-2.13** The system must provide a means to post a new journal entry.
- **REQ-JE-2.14** The system must not allow the creation of a new journal entry that is already voided.
- **REQ-JE-2.15** When posting a journal entry, the caller may supply zero or more comments. Each is created with the new entry as its primary journal entry and is validated as any other new comment (REQ-JE-1.52–1.54, REQ-JE-5.8). A secondary entry named here records only that the entries are related (REQ-JE-1.52); any specific relation is stated in the comment's text. (Amended 2026-10-05)


## 3. Read behaviors

- **REQ-JE-3.1** When retrieving a journal entry from the persistence layer, the system must return a JournalEntry type with all header properties, all associated lines, all external references, and all comments whose primary journal entry is this entry. (Comment scope clarified 2026-09-26)
- **REQ-JE-3.1.1** Journal entry reads return voided entries alongside active ones; the void marker distinguishes them. Only line-level reads offer a non-voided filter (REQ-JE-3.4, and account activity's unvoided-only flag, REQ-AC-3.12.1). (Amended 2026-10-03)
- **REQ-JE-3.2** The system must be able to retrieve a journal entry by the caller providing that entry's ID.
- **REQ-JE-3.3** The system must be able to retrieve all journal entries for a given fiscal period by the caller providing a PeriodKey.
- **REQ-JE-3.4** The system must be able to retrieve all journal entry lines for a given account, optionally restricted to lines of non-voided entries. Note: this requirement is retained alongside account activity (REQ-AC-3.12) because the capability exists and may serve a future need. (Amended 2026-10-03)
- **REQ-JE-3.5** The system must be able to retrieve the journal entries carrying a matching external reference, by the caller providing a source FI, a reference value, or both. When both are provided, a single external reference must match both. The result is a set (external references are not unique across entries, per REQ-JE-1.48). (Either-or lookup added 2026-09-26)
- **REQ-JE-3.5.1** When neither a source FI nor a reference value is provided, the lookup must fail with a typed error.
- **REQ-JE-3.6** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
- **REQ-JE-3.6.1** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
- **REQ-JE-3.6.2** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
- **REQ-JE-3.7** The system must be able to retrieve all journal entries whose entry date falls within a caller-provided date range (start date and end date, both inclusive Calendar Dates). The result is a set of complete journal entries (per REQ-JE-3.1).
- **REQ-JE-3.7.1** When the start date is after the end date, the retrieval must fail with a typed error.
- **REQ-JE-3.8** *(Withdrawn 2026-10-05 — see the Withdrawn table.)*
- **REQ-JE-3.9** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
- **REQ-JE-3.9.1** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*
- **REQ-JE-3.9.3** *(Withdrawn 2026-10-03 — see the Withdrawn table.)*

## 4. Update and void behaviors

- **REQ-JE-4.1** The system must not provide a user interface for updating any of the following posted fields of a journal entry or its lines: entry date, description, source, and — per line — referenced account, amount, entry type, and memo. These fields are set when the entry is posted and have no update path.
- **REQ-JE-4.2** A posted journal entry is not immutable. The only changes permitted after posting are: (a) voiding the entry, which sets its void marker and excludes its lines from all balance computations; (b) attaching or amending explanatory comments via the comment record; and (c) attaching new external references or updating existing ones (REQ-JE-4.9, REQ-JE-4.10). None of these paths edit the posted fields enumerated in REQ-JE-4.1. Voiding deliberately changes an entry's effective contribution to ledger balances; for that reason, no spec, requirement, or tooling may characterize journal entries as immutable or append-only.
- **REQ-JE-4.3** The system must provide a means to void a posted journal entry, which sets the void marker (`voided_at`).
- **REQ-JE-4.4** Voiding a journal entry must record a reason as a comment on the voided entry (primary journal entry = the voided entry). The reason comment may name a secondary journal entry, which records the voided entry's replacement, validated per REQ-JE-1.53 and REQ-JE-5.8. A void with no reason — or a whitespace-only reason — is rejected (the comment fails REQ-JE-1.54). If the void fails for any reason, the reason comment is not persisted. (Secondary entry and atomicity added 2026-09-26)
- **REQ-JE-4.5** Voiding is rejected when the entry's derived fiscal period is not open. (Amended 2026-10-03)
- **REQ-JE-4.6** Voiding an already-voided entry must produce an error rather than update nothing, per REQ-SYS-6.1. *Why:* this diverges deliberately from LeoBloom, which made re-void idempotent; a silent no-op masks a caller working from a stale view of the entry's state.
- **REQ-JE-4.7** Voided journal entries must be excluded from every balance, trial-balance, and account-sum computation. A voided entry's lines contribute nothing. (Amended 2026-10-03)
- **REQ-JE-4.8** Corrections to an entry in a closed period are made by posting an ordinary offsetting journal entry into the current open period and linking it to the original with a comment (secondary journal entry = the original). There is no separate reversal operation.
- **REQ-JE-4.9** The system must provide a means for an actor to update a journal entry reference's FI and value. The FI and value may be updated regardless of whether the entry is voided or its fiscal period is closed (mirrors REQ-JE-4.10 and REQ-JE-5.5).
- **REQ-JE-4.10** The system must provide a means to attach a new external reference to an existing journal entry, by the caller providing a journal entry ID, a source FI, and a reference value. The system must generate a unique UUID for the new reference and persist it (per REQ-JE-2.9 semantics). A reference may be appended regardless of whether the entry is voided or its fiscal period is closed (mirrors REQ-JE-5.5 for comments).
- **REQ-JE-4.11** *(Withdrawn 2026-09-27 — see the Withdrawn table.)*
- **REQ-JE-4.12** *(Withdrawn 2026-09-27 — see the Withdrawn table.)*
- **REQ-JE-4.13** Voiding a journal entry never changes staging. A staged entry that produced the voided entry stays `'Posted'`, and it and its lines keep their records of the journal entry and journal entry lines they produced (REQ-STG-9.10).
  - *Why:* Posting is the end of the line for ingestion (REQ-STG-4.2). The staged entry's link to the voided entry is history, and it is the provenance a replacement entry is traced back through. (2026-09-27)
- **REQ-JE-4.14** A void is rejected with a typed error naming the Payment when any Payment's transaction pointer references a line of the entry being voided.
  - *Why:* Voiding it would leave the Invoice reading 'FullyPaid' and 'PostedToLedger' on cash the ledger no longer records. The operator re-points or removes the Payment deliberately first, or corrects the entry with an adjusting entry instead of a void. (2026-09-27)

## 5. Comment behaviors

- **REQ-JE-5.1** The system must provide a means to attach a comment to a journal entry, optionally naming a secondary, related journal entry (REQ-JE-1.52). The secondary link records only that the entries are related; any specific relation is stated in the comment's text. (Amended 2026-10-05)
- **REQ-JE-5.2** When a comment is created, the system must generate a unique UUID and set its created/modified timestamps (per REQ-SYS-3.2).
- **REQ-JE-5.3** The system must provide a means to amend a comment's text. Amending updates the modified-at timestamp (per REQ-SYS-3.3).
- **REQ-JE-5.4** stricken
- **REQ-JE-5.5** A comment may be appended to an existing journal entry, even if the JE is voided or if the JE's fiscal period is closed.
- **REQ-JE-5.6** A comment's primary journal entry link is fixed once created. The primary relationship a comment records is a historical fact and must not be re-pointed.
- **REQ-JE-5.7** Updating a comment where all mutable fields are unchanged (text and secondary journal entry both NoChange) must be rejected as a no-op per REQ-SYS-6.1.
- **REQ-JE-5.8** When a comment is created, its primary journal entry and, if named, its secondary journal entry must exist. A primary or secondary ID that matches no journal entry fails with a typed error distinguishing which of the two was not found.

## 6. Deletion behaviors

- **REQ-JE-6.1** The system must not provide a user interface for hard-deleting a journal entry or its lines.
- **REQ-JE-6.2** The system must not provide a user interface for hard-deleting a journal entry's external references or comments; both are audit data.


## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-JE-1.1 | Guid is a value type — the solution won't build if you try to pass a null ID. | Dan, 2026-07-03 |
| REQ-JE-1.2 | The ID is system-generated (generation tested under REQ-JE-2.1) and the primary key constraint enforces uniqueness; a collision cannot be meaningfully provoked in a test. | Dan, 2026-07-03 |
| REQ-JE-1.9 | LocalDate is a value type — can't be null. | Dan, 2026-07-03 |
| REQ-JE-1.10 | Quite obviously enforced in the type definition — LocalDate carries no time component. | Dan, 2026-07-03 |
| REQ-JE-1.20 | Same as REQ-JE-1.1 — Guid value type. | Dan, 2026-07-03 |
| REQ-JE-1.23 | Enforced in the type definition — the line amount's type can only be built through the validating Money conversion (Money.md), which has its own tests. (Reason restated 2026-10-03) | Dan, 2026-07-03 |
| REQ-JE-1.29 | journalEntryId is a non-nullable Guid on the line type and a not-null FK in the schema; a line cannot be constructed without exactly one parent entry. | Dan, 2026-07-03 |
| REQ-JE-1.41 | Same shape as REQ-JE-1.29 — non-nullable Guid plus not-null FK. | Dan, 2026-07-03 |
| REQ-JE-1.50 | Same as REQ-JE-1.2 — system-generated UUID plus primary key constraint. | Dan, 2026-07-03 |
| REQ-JE-1.51 | The primary entry ID is a non-nullable identifier with a not-null foreign key, and the primary entry's existence is checked before the comment is written (tested under REQ-JE-5.8). (Reason restated 2026-10-03) | Dan, 2026-07-03 |
| REQ-JE-1.3 | A null is loud: the interface contract's description field is non-optional, so the deserializer rejects a null or missing value, and a null reaching the constructor throws. Same treatment as REQ-JE-1.1/1.9. The same waiver covers the null clauses of REQ-JE-1.42, 1.44 and 1.54, whose remaining clauses are tested. | Dan, 2026-10-03 |
| REQ-JE-1.14 | Enforced by the type definition — `voidedAt` is `Instant option` on the header type. Void behavior tested under REQ-JE-4.3/4.7 | Dan, 2026-08-02 |
| REQ-JE-2.14 | Enforced structurally — no creation input carries a void marker; it appears only on the entry the system returns. Reconstituting a stored entry legitimately carries the void marker; reconstitution is not creation. (Reason restated 2026-10-05) | Dan, 2026-08-02 |
| REQ-JE-5.6 | Enforced structurally — no comment update input accepts a primary journal entry, so the link has no path to change and a test of the update cannot fail. Same shape as REQ-JE-2.14 and REQ-JE-4.1. | Hobson, 2026-10-05 (delegated by Dan) |
| REQ-JE-4.1 | A negative existence claim over the entire API surface ("no function exposes an update path for these fields") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Dan, 2026-06-22 |
| REQ-JE-4.2 | The prohibition "no spec, requirement, or tooling may characterize journal entries as immutable" is a negative existence claim over documentation and the API surface; the positive behaviors it depends on (void, comments) are tested under REQ-JE-4.3/4.7/5.x. Enforced by review. | Dan, 2026-06-22 |
| REQ-JE-6.1 | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit. | Dan, 2026-06-22 |
| REQ-JE-6.2 | Same negative-existence rationale as REQ-JE-6.1, extended to external references and comments. | Dan, 2026-06-22 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| REQ-JE-4.8 | The system blocks voiding in a closed period (REQ-JE-4.5, tested), but nothing forces the operator to post an offsetting entry as the remedy. The correction procedure is guidance for the human, not a system-enforced constraint | Dan, 2026-06-22 |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
| — | "References and voiding are deferred" (design note) | Reversed: prod usage showed references on 97% of entries and voids on 10%. References are modeled as external references + comments; voiding is specced in §4. |
| REQ-JE-4.11 | When the voided journal entry was posted from a staged entry, the void must, in the same transaction: transition that staged entry from `'Posted'` to `'Reviewed'` (REQ-STG-4.7), and clear the staged entry's and its lines' records of the journal entry and journal entry lines they produced (REQ-STG-9.10). | Replaced by REQ-JE-4.13. A posted staged entry never goes back to review (REQ-STG-4.7 withdrawn). (2026-09-27) |
| REQ-JE-4.12 | When a Payment's transaction pointer references a line of the voided journal entry, the void must, in the same transaction: clear the Payment's journal entry line ID when the Payment also references a staged line, so the pointer resolves to Staged again; and re-derive the affected Invoice's and Instance's states (REQ-CF-9.10). When such a Payment references no staged line, the void is rejected with a typed error naming the Payment. | Replaced by REQ-JE-4.14. With no path back to staging, a Payment on a voided line has nothing to fall back to; the void is refused instead. (2026-09-27) |
| REQ-JE-1.43 | External reference source FI must be one of the recognized source financial institutions (a controlled vocabulary, not free text). An unrecognized value is rejected. | I don't want to constrain this field for a personal application |
| REQ-JE-1.47 | External references are write-once: they carry a `created_at` Instant, are set when the entry is posted (or appended thereafter), and are never edited | Bullshit. We'll fat finger this someday. And then what? |
| REQ-JE-2.10 | When posting a journal entry, the source FI of each external reference must be a recognized value (REQ-JE-1.43); an unrecognized value rejects the post. | same reason as 1.43 |
| REQ-JE-5.4 | A comment's primary and secondary journal entry links are fixed once created; only the comment text may be amended. The relationship a comment records is a historical fact and must not be re-pointed. | Too restrictive and no value add |
| REQ-JE-3.6 | The system must be able to compute and return the total debit amount, total credit amount, and net balance for a given account's non-voided journal entry lines (per REQ-JE-4.7). | Superseded by REQ-AC-3.13/3.13.1, which specify the same balance capability in full. (2026-10-03) |
| REQ-JE-3.6.1 | Net balance is expressed in the account's normal balance orientation such that a positive net balance always means "more of what this account holds". | Superseded by REQ-AC-3.13. (2026-10-03) |
| REQ-JE-3.6.2 | The caller must be able to pass an optional "as-of" date such that the result represents the balance as it would've been at the end of the as-of date. | Superseded by REQ-AC-3.13/3.13.1. (2026-10-03) |
| REQ-JE-3.9 | The system must be able to retrieve all journal entry lines for a given account, enriched with their parent entry's `entry_date`, `description`, `source`, and `voided_at`. | Superseded by REQ-AC-3.12–3.12.2 (account activity), which also specify the filters, their conjunction and the line-less Account row. (2026-10-03) |
| REQ-JE-3.9.1 | The caller may filter to non-voided entries only (per REQ-JE-4.7). The enriched fields are a boundary-only return type. | Superseded by REQ-AC-3.12.1/3.12.2. (2026-10-03) |
| REQ-JE-3.9.3 | The result can be ordered by entry date, account code, or amount (either ascending or descending) at the caller's choosing. | Superseded by REQ-AC-3.12.4. (2026-10-03) |
| REQ-JE-3.8 | The system must be able to retrieve all journal entries carrying at least one external reference whose source FI matches a caller-provided value. Unlike REQ-JE-3.5, this requires only the FI — no reference value. The result is a set of complete journal entries (per REQ-JE-3.1). | Superseded by REQ-JE-3.5's FI-only mode (added 2026-09-26). (2026-10-05) |
