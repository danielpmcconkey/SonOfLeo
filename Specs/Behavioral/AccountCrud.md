# Account CRUD

Service-level behavioral specs for creating, updating, and deactivating chart-of-accounts entries. All scenarios exercise the service layer. Structural constraints (FK, unique index) are covered separately in structural specs; these scenarios verify the service rejects invalid inputs with meaningful error messages before any DB round-trip where possible. Cross-cutting policies (string trimming, data-state enforcement at every operation, audit timestamps, deletion) live in SystemWide.md and apply to everything below.

## 1. Valid and invalid data states for the Account type and related types

- **REQ-AC-1.1** Account code cannot be null
- **REQ-AC-1.2** Account code cannot be whitespace only
- **REQ-AC-1.3** Account code length cannot exceed 10 chars
- **REQ-AC-1.4** No 2 (or more) account records may share the same account code. (Account code must be unique)
- **REQ-AC-1.5** Account code is case sensitive. "ACCT-100" and "acct-100" are distinct account codes.
- **REQ-AC-1.6** Account name cannot be null
- **REQ-AC-1.7** Account name cannot be whitespace only
- **REQ-AC-1.8** Account name length cannot exceed 100 chars
- **REQ-AC-1.9** Account type normal balance must be one of 'Debit' or 'Credit'
- **REQ-AC-1.10** Account type name must be constrained to ['Asset','Liability','Equity','Revenue','Expense']
- **REQ-AC-1.11** Stricken
- **REQ-AC-1.12** Stricken
- **REQ-AC-1.13** Stricken
- **REQ-AC-1.14** Stricken
- **REQ-AC-1.15** Stricken
- **REQ-AC-1.16** Account types with name of 'Asset','Expense' must have a normal balance of 'Debit'
- **REQ-AC-1.17** Account types with name of 'Liability','Equity','Revenue' must have a normal balance of 'Credit'
- **REQ-AC-1.18** Account subtype must be constrained to ['Cash','CurrentLiability','FixedAsset','Investment','LongTermLiability','OperatingExpense','OperatingRevenue','OtherRevenue','OtherExpense']
- **REQ-AC-1.19** Account subtype can be null and a null subtype can be paired with any type
- **REQ-AC-1.20** Account external reference length must not exceed 50 characters
- **REQ-AC-1.21** Account ID cannot be null
- **REQ-AC-1.22** Account ID must be unique
- **REQ-AC-1.23** Account type cannot be null
- **REQ-AC-1.24** stricken
- **REQ-AC-1.25** stricken
- **REQ-AC-1.26** stricken
- **REQ-AC-1.27** stricken
- **REQ-AC-1.28** Account sub type of 'Cash', 'FixedAsset', and 'Investment' can only be applied account records of type 'Asset'
- **REQ-AC-1.29** Account records of type 'Asset' can only have null, 'Cash', 'FixedAsset', and 'Investment' subtypes
- **REQ-AC-1.30** Account sub type of 'CurrentLiability', and 'LongTermLiability' can only be applied account records of type 'Liability'
- **REQ-AC-1.31** Account records of type 'Liability' can only have null, 'CurrentLiability', and 'LongTermLiability' subtypes
- **REQ-AC-1.32** Account records of type 'Equity' can only have null subtypes
- **REQ-AC-1.33** Account sub type of 'OperatingRevenue' and 'OtherRevenue' can only be applied account records of type 'Revenue'
- **REQ-AC-1.34** Account records of type 'Revenue' can only have null, 'OperatingRevenue' and 'OtherRevenue' subtypes
- **REQ-AC-1.35** Account sub type of 'OperatingExpense' and 'OtherExpense' can only be applied account records of type 'Expense'
- **REQ-AC-1.36** Account records of type 'Expense' can only have null, 'OperatingExpense' and 'OtherExpense' subtypes
- **REQ-AC-1.37** Account parent ID can be null
- **REQ-AC-1.38** stricken
- **REQ-AC-1.39** An account record's ID and parent ID cannot be the same (an account cannot be its own parent)
- **REQ-AC-1.40** When not null, account parent Id must be a UUID of a preexisting database account record
- **REQ-AC-1.41** Account external reference can be null
- **REQ-AC-1.42** Account records must be able to represent a valid Calendar Date (LocalDate) for the first date on which journal entries may be posted to the account ("active begin"). *Why a date, not an Instant:* the only thing that ever reads these boundaries is the posting gate, and journal entries are day-granular (entry dates are Calendar Dates); sub-day precision on account activity is precision no domain operation can observe. The instant a (de)activation action occurred is captured by `modified_at`, not here.
- **REQ-AC-1.43** Account records must be able to represent a valid Calendar Date (LocalDate) for the last date on which journal entries may be posted to the account ("active end"). The boundary is inclusive: an entry dated on the active-end date is postable.
- **REQ-AC-1.44** An account record's "active begin" may not be null
- **REQ-AC-1.45** An account record's "active end" may be null
- **REQ-AC-1.46** An account record's "active end" may not be earlier than its "active begin". Equality is permitted and represents an account active for exactly one day (the inclusive single-day window).
- **REQ-AC-1.47** An Account record's parent ID can never reference one of its descendent accounts.
- **REQ-AC-1.48** An Account record is considered "deactivated" (or "inactive") relative to a reference Calendar Date when its "active end" is non-null and is earlier than that reference date (the active-end date itself is still active — the boundary is inclusive).
  - **REQ-AC-1.48.1** The reference point is a Calendar Date and is context-dependent: it may be the current date (the Eastern calendar date of the AuditEnvelope's system instant) or a date specific to the operation (e.g., a transaction's entry date). Each requirement that references activity status must specify which reference point applies.
- **REQ-AC-1.49** Account external reference cannot be whitespace only (pre-trimmed) or empty
- **REQ-AC-1.50** An Account record is considered "active" relative to a reference Calendar Date when its "active begin" is earlier than or equal to that reference date AND (its "active end" is null OR its "active end" is later than or equal to that reference date). Both boundaries are inclusive.


## 2. Create behaviors

- **REQ-AC-2.1** stricken
- **REQ-AC-2.2** stricken
- **REQ-AC-2.3** stricken
- **REQ-AC-2.4** When creating an Account record via primitive types, the passed in string (post-trim, per REQ-SYS-1.1) for account type must match one of the enumerated account types exactly or the creation must fail.
- **REQ-AC-2.5** stricken
- **REQ-AC-2.6** When creating an Account record, if the caller of the function provided a parent ID, the system must confirm that the ID maps to an existing Account in the database.
- **REQ-AC-2.6.1** Note, since moving to code-based UI, the "caller of the function" in the above is an internal application layer function only. The requirement still applies.
- **REQ-AC-2.7** When creating an Account record, if the caller of the function provided a parent ID, the system must confirm that the parent account is active as of the current date (the Eastern calendar date of the AuditEnvelope's system instant). 
- **REQ-AC-2.7.1** Note, since moving to code-based UI, the "caller of the function" in the above is an internal application layer function only. The requirement still applies.
- **REQ-AC-2.8** When creating an Account record, the system must reject any duplicated ID
- **REQ-AC-2.9** When creating an Account record, the system must reject any duplicated account code
- **REQ-AC-2.10** When creating an Account record via primitive types, the passed in string (post-trim, per REQ-SYS-1.1) for account sub-type must match one of the enumerated account sub-types exactly or the creation must fail.
- **REQ-AC-2.11** stricken
- **REQ-AC-2.12** stricken
- **REQ-AC-2.13** When creating an Account record, the creation function must generate a unique UUID for the ID (new UUIDs may not be passed in).
- **REQ-AC-2.14** When creating an Account record, if the calling system specifies that the record should be saved to the DB, and if all validations pass and the passed in arguments represent a valid data state, the creation function must persist the fully validated account record in the database and return an account record with the created ID and created/modified timestamps
- **REQ-AC-2.15** stricken
- **REQ-AC-2.16** When creating an Account record, if the caller of the function provided a parent ID, the system must confirm that the parent account is not already a descendent (no circular relationships).
- **REQ-AC-2.17** When creating an Account record, it is the responsibility of the calling function to provide an accurate "active begin" Calendar Date. There is no validation to confirm that the caller provided a correct begin date.
- **REQ-AC-2.18** When creating an Account record, the system will validate that any non-null "active end" is not earlier than the provided "active begin" (equality permitted, per REQ-AC-1.46).
- **REQ-AC-2.19** stricken
- **REQ-AC-2.20** When creating an Account with a parent ID, the child's AccountType must match the parent's AccountType.
- **REQ-AC-2.21** The system must provide a means to create a new account


## 3. Read behaviors

- **REQ-AC-3.1** stricken
- **REQ-AC-3.2** When retrieving an Account record from the persistence layer, the system must return an Account type with all account properties.
- **REQ-AC-3.3** The system must be able to retrieve an Account record by the caller providing that record's ID value.
- **REQ-AC-3.3.1** Note: since moving to code-based UI functions, this is an internal-only capability.
- **REQ-AC-3.4** The system must be able to retrieve an Account record by the caller providing that record's account code string.
- **REQ-AC-3.5** The system must be able to retrieve all child records of an Account by the caller providing that parent record's ID.
- **REQ-AC-3.5.1** stricken
- **REQ-AC-3.5.2** Note, since moving to code-based UI, the "caller" in the above is an internal application layer function only. The requirement still applies.
- **REQ-AC-3.6** The system must be able to retrieve all Account records of a particular type by the caller providing that AccountType.
- **REQ-AC-3.6.1** stricken
- **REQ-AC-3.7** The system must be able to retrieve all Account records without filter
- **REQ-AC-3.8** stricken
- **REQ-AC-3.9** The system must be able to retrieve all active Account records relative to the current date (anchored to a US Eastern Time interpretation of the calendar date associated to the system run time)
- **REQ-AC-3.10** The system must be able to retrieve all child records of an Account by the caller providing that parent record's Account Code string.

## 4. Update behaviors

- **REQ-AC-4.1** The system must provide a means to deactivate an Account, using a provided "active end" Calendar Date.
- **REQ-AC-4.2** When an Account deactivation is requested, the system must reject any request where the "active end" date would be earlier than the "active begin" date (equality permitted, per REQ-AC-1.46).
- **REQ-AC-4.3** When an Account deactivation is requested, the system must reject any request where the Account to be deactivated has active children accounts (reference as-of the current date — the Eastern calendar date of the AuditEnvelope's system instant).
- **REQ-AC-4.4** When an Account deactivation is requested, the system must reject any request where the Account has a non-zero balance at the time of the request.
- **REQ-AC-4.5** When an Account deactivation is requested, the system must reject any request where the Account already has a non-null "active end" date.
- **REQ-AC-4.6** When an Account deactivation is requested, the system must reject any request where the Account is referenced by a journal entry line whose entry date is later than the provided "active end" date (a pure Calendar Date comparison; the inclusive boundary means an entry dated on the active-end date is permitted).
- **REQ-AC-4.7** stricken
- **REQ-AC-4.8** The system must provide a means to update an Account record's "name" field.
- **REQ-AC-4.9** The system must provide a means to update an Account record's "external reference" field.
- **REQ-AC-4.10** stricken
- **REQ-AC-4.11** stricken
- **REQ-AC-4.12** stricken
- **REQ-AC-4.13** stricken
- **REQ-AC-4.14** stricken
- **REQ-AC-4.15** stricken
- **REQ-AC-4.16** stricken
- **REQ-AC-4.17** stricken
- **REQ-AC-4.18** stricken
- **REQ-AC-4.19** Updates to a deactivated Account record are permitted, provided that those updates meet all other requirements herein.
- **REQ-AC-4.20** stricken
- **REQ-AC-4.21** stricken
- **REQ-AC-4.22** The system must not provide a user interface for updating any of the following immutable Account fields: ID, "code", account type, subtype, "active begin", "created at", or parent ID.


## 5. Deletion behaviors

- **REQ-AC-5.1** The system must not provide a user interface for hard-deleting an Account record.


## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID          | Reason testing is waived  | Approved |
|---|---|---|
| REQ-AC-1.1  | Solution won't build if you try to pass a null value to AccountCode.create                                                                                                                                                                | Dan, 2026-06-14 |
| REQ-AC-1.6  | Solution won't build if you try to pass a null value to AccountName.create                                                                                                                                                                | Dan, 2026-06-14 |
| REQ-AC-1.9  | It is impossible to test all strings other than "Debit" and "Credit" and it's quite obviously enforced in the type definition |
| REQ-AC-1.16 | It's quite obviously enforced in the type definition |
| REQ-AC-1.17 | It's quite obviously enforced in the type definition |
| REQ-AC-1.39 | It is impossible to try. You can't set the account ID manually so you can't set the parent to its own ID |
| REQ-AC-1.47 | This is an impossible state if done through the code. The ID of the account is generated at creation time and therefore there can be no children of it. Only possible through direct DB editing                                           | Dan, 2026-06-13 |
| REQ-AC-2.16 | This is an impossible state if done through the code. See rationale for REQ-AC-1.47 |
| REQ-AC-4.22 | A negative existence claim over the entire API surface ("no function exposes an update path for these fields") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Dan, 2026-06-11 |
| REQ-AC-5.1  | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface.                  | Dan, 2026-06-11 |
| REQ-AC-1.21 | Account ID is a Guid (value type) — cannot be null | Dan, 2026-07-06 |
| REQ-AC-1.22 | Account ID is a system-generated UUID — uniqueness is structurally guaranteed | Dan, 2026-07-06 |
| REQ-AC-1.23 | Account type is a discriminated union — cannot be null | Dan, 2026-07-06 |
| REQ-AC-1.37 | Parent ID is an Option type — nullability is inherent in the type | Dan, 2026-07-06 |
| REQ-AC-1.41 | External reference is an Option type — nullability is inherent in the type | Dan, 2026-07-06 |
| REQ-AC-1.44 | Active begin is a NodaTime LocalDate (value type) — cannot be null | Dan, 2026-07-06 |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
| REQ-AC-2.17 | The requirement assigns responsibility to the caller and explicitly states there is no validation. Nothing in the system checks that the caller provided a correct begin date | Dan, 2026-06-11 |
| REQ-AC-3.3.1 | A contextual annotation ("this is an internal-only capability"), not a testable behavior. Nothing in the system enforces internal-vs-external access boundaries | Dan, 2026-08-02 |

## Withdrawn

| ID          | Original Requirement | Reason |
|-------------|----------------------|--------|
| REQ-AC-1.19.1 | A null subtype can be paired with any type | Verbatim duplicate of the second clause of REQ-AC-1.19. Five tests cite 1.19.1 — rename to cite 1.19 (BD). |
| REQ-AC-1.11 | Account type name of 'Asset' must map to the database ID of 1 | Removed Account Type as a separate DB lookup |
| REQ-AC-1.12 | Account type name of 'Liability' must map to the database ID of 2 | Removed Account Type as a separate DB lookup |
| REQ-AC-1.13 | Account type name of 'Equity' must map to the database ID of 3 | Removed Account Type as a separate DB lookup |
| REQ-AC-1.14 | Account type name of 'Revenue' must map to the database ID of 4 | Removed Account Type as a separate DB lookup |
| REQ-AC-1.15 | Account type name of 'Expense' must map to the database ID of 5 | Removed Account Type as a separate DB lookup |
| REQ-AC-1.24  | Account is active should default to true if a null value is provided | Replaced by `active_begin` and `active_end` timestamps |
| REQ-AC-1.25  | Account created at should default to the current runtime timestamp at time of database creation of the record | Superseded by REQ-SYS-3.2 |
| REQ-AC-1.26  | Account modified at should default to the current runtime timestamp at time of database creation of the record | Superseded by REQ-SYS-3.2 |
| REQ-AC-1.27  | Account modified at should be updated to the current runtime timestamp at time of database update of the record | Superseded by REQ-SYS-3.3 |
| REQ-AC-1.38  | An account record with the is active flag set to true may not have a parent ID that references an account record with the is active flag set to false | Deemed too computationally expensive at every Account construction event. Superseded by REQ-AC-2.7 and REQ-AC-4.3. |
| REQ-AC-2.1   | When creating an Account record, either through primitive types or through defined types, all raw string values must be trimmed of any leading or trailing white space before being added to the persistence layer or being returned to the caller of the function. | Superseded by REQ-SYS-1.1 |
| REQ-AC-2.2   | When creating an Account record, the database must be able to persist strings with full UTF-8 support. | Moved to DAL-level requirement (REQ-DAL-3.4) |
| REQ-AC-2.3   | When creating an Account record, any string field must be stored in the database with case-perfect fidelity (post-trim). | Moved to DAL-level requirement |
| REQ-AC-2.5   | When creating an Account record, if the provided "is active" value is null, the newly created Account record will be active. | Replaced by `active_begin` and `active_end` timestamps |
| REQ-AC-2.11  | When creating an Account record, the system must generate a "created at" timestamp that represents the system clock at time of creation. | Superseded by REQ-SYS-3.2 |
| REQ-AC-2.12  | When creating an Account record, the system must generate a "modified at" timestamp that represents the system clock at time of creation. | Superseded by REQ-SYS-3.2 |
| REQ-AC-2.15  | The persistence layer must persist all Account properties in such a way as to be able to perfectly reconstitute the Account type upon subsequent read. | Superseded by REQ-SYS-5.1 |
| REQ-AC-2.19  | The system must reject any Account creation request that would result in an illegal data state as defined in section 1. | Superseded by REQ-SYS-2.1 |
| REQ-AC-3.1   | When retrieving an Account record from the persistence layer, the system must validate for all the same legal data states as it does when creating a new record. | Superseded by REQ-SYS-2.1 |
| REQ-AC-3.5.1 | The caller should be able to specify whether they want all records or only active records | No longer needed due to how active accounts are thought of now |
| REQ-AC-3.6.1 | The caller should be able to specify whether they want all records or only active records | No longer needed due to how active accounts are thought of now |
| REQ-AC-3.8 | The system must be able to retrieve all active Account records relative to a passed in reference time | Changed to system run time |
| REQ-AC-4.7   | Any successful update to an Account record must also update the "modified at" timestamp for that Account record with the current system run date/time. | Superseded by REQ-SYS-3.3 |
| REQ-AC-4.10  | The system must not provide a user interface for updating an Account record's ID. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.11  | The system must not provide a user interface for updating an Account record's "code" field. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.12  | The system must not provide a user interface for updating an Account record's account type. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.13  | The system must not provide a user interface for updating an Account record's "active begin" field. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.14  | The system must not provide a user interface for updating an Account record's "created at" field. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.15  | The system must not provide a user interface for updating an Account record's "subtype" field. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.16  | The system must not provide a user interface for updating an Account record's parent ID. (untestable) | Consolidated into REQ-AC-4.22 |
| REQ-AC-4.17  | When updating an Account record, all updatable raw string values must be trimmed of any leading or trailing white space before being added to the persistence layer or being returned to the caller of the function. | Superseded by REQ-SYS-1.1 |
| REQ-AC-4.18  | The system must reject any Account update request that would result in an illegal data state as defined in section 1. | Superseded by REQ-SYS-2.1 |
| REQ-AC-4.20  | When updating an Account record, no updatable raw string may be updated to purely white-space. | Superseded by REQ-SYS-1.2 / REQ-SYS-1.3 (via REQ-SYS-2.1 and section 1 rules) |
| REQ-AC-4.21  | When updating an Account record, all legal/illegal data state rules (section 1) must be enforced | Duplicate of REQ-AC-4.18; both superseded by REQ-SYS-2.1 |
