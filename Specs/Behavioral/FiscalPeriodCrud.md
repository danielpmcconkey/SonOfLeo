# Fiscal Period CRUD

Service-level behavioral specs for creating, reading, and updating fiscal periods. Cross-cutting policies (string trimming, data-state enforcement, audit timestamps, deletion) live in SystemWide.md and apply to everything below.

**Design note (from cli-requirements-from-leobloom-usage.md §4):** LeoBloom's period-closing machinery (seven spec files) was never exercised. SonOfLeo keeps the open/closed state for posting gating but defers closing tooling until wanted.


## 1. Valid and invalid data states for the FiscalPeriod type

- **REQ-FP-1.1** Fiscal period key cannot be null
- **REQ-FP-1.2** Fiscal period key must match the format `YYYY-MM` where YYYY is a four-digit year and MM is a two-digit month (01–12). 
- **REQ-FP-1.3** No 2 (or more) fiscal period records may share the same period key. (Period key must be unique)
- **REQ-FP-1.4** Fiscal period start date is derived from the key as the first day of the indicated month (e.g., key "2026-07" → start date 2026-07-01). It is not a caller-provided value.
- **REQ-FP-1.5** Fiscal period end date is derived from the key as the last day of the indicated month (e.g., key "2026-07" → end date 2026-07-31; key "2026-02" → end date 2026-02-28 or 2026-02-29 in a leap year). It is not a caller-provided value.
- **REQ-FP-1.6** Fiscal period ID cannot be null
- **REQ-FP-1.7** Fiscal period ID must be unique
- **REQ-FP-1.8** Fiscal period "is open" flag must be a boolean value and cannot be null


## 2. Create behaviors

- **REQ-FP-2.1** When creating a fiscal period, the system must generate a unique UUID for the ID (new UUIDs may not be passed in).
- **REQ-FP-2.2** When creating a fiscal period, the system must reject any duplicated period key.
- **REQ-FP-2.3** When creating a fiscal period, the system must compute the start and end dates from the key. The caller provides only the key.
- **REQ-FP-2.3.1** The system will not allow the creating actor to specify start and end dates
- **REQ-FP-2.4** When creating a fiscal period, if all validations pass, the creation function must persist the fully validated record in the database and return a fiscal period record with the created ID, computed dates, and created/modified timestamps.
- **REQ-FP-2.5** The system must provide a means to create a new fiscal period.
- **REQ-FP-2.6** When creating a fiscal period the system will set the "is open" flag to true
- **REQ-FP-2.6.1** The system will not allow the creating actor to create a fiscal period as "closed"


## 3. Read behaviors

- **REQ-FP-3.1** When retrieving a fiscal period record from the persistence layer, the system must return a FiscalPeriod type with all fiscal period properties.
- **REQ-FP-3.2** The system must be able to retrieve a fiscal period by the caller providing that record's period key string.
- **REQ-FP-3.3** stricken
- **REQ-FP-3.4** The system must be able to retrieve all fiscal period records without filter.
- **REQ-FP-3.5** The system must be able to retrieve all open fiscal period records.


## 4. Update behaviors

- **REQ-FP-4.1** The system must provide a means to close a fiscal period (set is_open to false).
- **REQ-FP-4.1.1** The system must produce an error when the caller tries to close an already closed period (updating nothing)
- **REQ-FP-4.2** The system must provide a means to reopen a fiscal period (set is_open to true).
- **REQ-FP-4.2.1** The system must produce an error when the caller tries to reopen an already opened period (updating nothing)
- **REQ-FP-4.3** The system must not provide a user interface for updating any of the following immutable FiscalPeriod fields: ID, period key, start date, end date.


## 5. Deletion behaviors

- **REQ-FP-5.1** The system must not provide a user interface for hard-deleting a fiscal period record.


## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-FP-1.1 | It's an impossible state to represent in this model | Dan, 2026-06-21 |
| REQ-FP-1.6 | It's an impossible state to represent in this model | Dan, 2026-06-21 |
| REQ-FP-1.7 | It's an impossible state to test, given the constructNew function always creates the UUID at runtime | Dan, 2026-06-21 |
| REQ-FP-1.8 | It's an impossible state to represent in this model | Dan, 2026-06-21 |
| REQ-FP-2.3.1 | You cannot test for the absence of something | Dan, 2026-06-21 |
| REQ-FP-2.6.1 | You cannot test for the absence of something | Dan, 2026-06-21 |
| REQ-FP-4.3 | You cannot test for the absence of something | Dan, 2026-06-21 |
| REQ-FP-5.1 | You cannot test for the absence of something | Dan, 2026-06-21 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID          | Original Requirement | Reason |
|-------------|----------------------|--------|
| REQ-FP-3.3  | The system must be able to retrieve the fiscal period that contains a given date (the period whose start_date <= date <= end_date). | Not needed |
