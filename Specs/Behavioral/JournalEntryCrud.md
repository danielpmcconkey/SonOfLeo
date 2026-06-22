# Journal Entry CRUD

Service-level behavioral specs for posting, reading, and voiding journal entries. Cross-cutting policies (string trimming, data-state enforcement, audit timestamps, deletion) live in SystemWide.md and apply to everything below.

**Design note (from wakeup-2026-06-20a §7):** JE entry dates are Dates (LocalDate), not Instants. GAAP doesn't care about time-of-day for transaction recognition. Period assignment is: parse year+month from entry_date, construct the PeriodKey, fetch the period, check is_open.

**Design note (from LeoBloom):** References and voiding are deferred. LeoBloom specced both (PostJournalEntry §references, VoidJournalEntry) but references were rarely used and voiding was never exercised. SonOfLeo will add them when wanted, not speculatively.


## 1. Valid and invalid data states for the JournalEntry type

### Header

- **REQ-JE-1.1** Journal entry ID cannot be null
- **REQ-JE-1.2** Journal entry ID must be unique
- **REQ-JE-1.3** Journal entry description cannot be null
- **REQ-JE-1.4** Journal entry description cannot be whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.5** Journal entry description length cannot exceed 200 characters
- **REQ-JE-1.6** Journal entry source is optional (nullable)
- **REQ-JE-1.7** When provided, journal entry source cannot be whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.8** When provided, journal entry source length cannot exceed 50 characters
- **REQ-JE-1.9** Journal entry date cannot be null
- **REQ-JE-1.10** Journal entry date is a LocalDate (calendar date with no time component)
- **REQ-JE-1.11** Journal entry date must fall within the start and end dates (inclusive) of the fiscal period it is assigned to
- **REQ-JE-1.12** A journal entry must have at least 2 lines
- **REQ-JE-1.13** The sum of all debit line amounts must exactly equal the sum of all credit line amounts (balanced entry)

### Lines

- **REQ-JE-1.20** Journal entry line ID cannot be null
- **REQ-JE-1.21** Journal entry line ID must be unique
- **REQ-JE-1.22** Journal entry line must reference a valid account ID
- **REQ-JE-1.23** Journal entry line amount must be a Money value (per Money.md)
- **REQ-JE-1.24** Journal entry line amount must be positive (greater than zero)
- **REQ-JE-1.25** Journal entry line entry type must be one of 'Debit' or 'Credit'
- **REQ-JE-1.26** Journal entry line memo is optional (nullable)
- **REQ-JE-1.27** When provided, journal entry line memo cannot be whitespace only (post-trim per REQ-SYS-1.1)
- **REQ-JE-1.28** When provided, journal entry line memo length cannot exceed 200 characters


## 2. Create (post) behaviors

- **REQ-JE-2.1** When posting a journal entry, the system must generate a unique UUID for the header ID (new UUIDs may not be passed in).
- **REQ-JE-2.2** When posting a journal entry, the system must generate a unique UUID for each line ID (new UUIDs may not be passed in).
- **REQ-JE-2.3** When posting a journal entry, the system must derive the fiscal period from the entry date: parse year and month, construct the PeriodKey, and look up the corresponding fiscal period record.
- **REQ-JE-2.4** When posting a journal entry, the system must reject any entry whose derived fiscal period does not exist in the database.
- **REQ-JE-2.5** When posting a journal entry, the system must reject any entry whose derived fiscal period is not open (is_open = false).
- **REQ-JE-2.6** When posting a journal entry, the system must reject any entry that references an inactive account on any line. The reference point for activity is the journal entry's entry date (as an Instant at start-of-day UTC, or as a date comparison against the account's activity period).
- **REQ-JE-2.7** When posting a journal entry, if all validations pass, the system must persist the header and all lines atomically in a single database transaction and return the fully constructed journal entry with all generated IDs and timestamps.
- **REQ-JE-2.8** When posting a journal entry, if any validation fails, no rows may be persisted (atomicity).
- **REQ-JE-2.9** The system must provide a means to post a new journal entry.


## 3. Read behaviors

- **REQ-JE-3.1** When retrieving a journal entry from the persistence layer, the system must return a JournalEntry type with all header properties and all associated lines.
- **REQ-JE-3.2** The system must be able to retrieve a journal entry by the caller providing that entry's ID.
- **REQ-JE-3.3** The system must be able to retrieve all journal entries for a given fiscal period by the caller providing a PeriodKey.
- **REQ-JE-3.4** The system must be able to retrieve all journal entry lines for a given account by the caller providing that account's ID.


## 4. Update behaviors

- **REQ-JE-4.1** The system must not provide a user interface for updating any field of a posted journal entry or its lines. Journal entries are append-only; corrections are made by posting new entries.


## 5. Deletion behaviors

- **REQ-JE-5.1** The system must not provide a user interface for hard-deleting a journal entry or its lines.


## Waived from testing

Active requirements that are deliberately not verified by tests. Two-state rule: every
active requirement is either tested or in this table.

| ID | Reason testing is waived | Approved |
|---|---|---|

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
