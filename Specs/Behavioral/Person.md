# Person

Behavioral specs for Persons: the humans who stand in a relationship to the financial accounts
and property the system tracks, most often as owners. A Person is a more general concept than
any one domain's notion of an owner, so it stands as its own domain rather than inside one.
(Reworded 2026-10-05)

**Design note — a Person is not a User.** A Person is a human with a relationship to a
financial account or property the system tracks (see Definitions). A User is an actor operating
this system. The two may be the same human, but the system never treats one as the other: being
recorded as an account's owner grants nothing in the system, and operating the system makes
nobody an owner. (2026-10-04; amended 2026-10-05)

**Design note — what a Person carries.** A name and a birthdate. Planning dates (retirement
account access, required distributions, benefit eligibility) all derive from the birthdate; the
rules that derive them belong to whatever consumes the data, not here. Nothing else about a
person is recorded until a use case needs it. (2026-10-04)

## 1. Valid and invalid data states for the Person type

- **REQ-PER-1.1** Person name cannot be null or whitespace only (post-trim, per REQ-SYS-1.1).
- **REQ-PER-1.2** Person name length cannot exceed 100 characters.
- **REQ-PER-1.3** No two Persons may share the same name. Names compare exactly: case-sensitive, after trimming.
  - *Why:* Persons are addressed by name at the boundary (an account's owners are given by name), so a name must identify exactly one Person. (2026-10-04)
- **REQ-PER-1.4** Person birthdate cannot be null. Birthdate is a Calendar Date.
- **REQ-PER-1.5** Person birthdate cannot be later than the current date (the calendar date (REQ-SYS-7.1) of the operation's initiation instant (REQ-SYS-3.4)).

## 2. Operations

- **REQ-PER-2.1** The system must provide a means to create a Person from a name and a birthdate.
- **REQ-PER-2.2** The system must provide a means to update a Person's name and birthdate, the Person addressed by its current name, each subject to §1.
- **REQ-PER-2.3** The system must provide a read-only means to list every Person, each with its name and birthdate, ordered by name.
- **REQ-PER-2.4** A Person name given to any operation that does not match an existing Person fails with a typed error naming it.

## 3. Deletion behaviors

- **REQ-PER-3.1** The system must not provide a user interface for hard-deleting a Person.
  - *Why:* Owners are referenced by investment accounts and property (Positions), and their history must keep resolving. (2026-10-04)

## Waived from testing

Active requirements that are enforced (by type system, code review, schema, or
construction pattern) but deliberately not verified by tests.

| ID | Reason testing is waived | Approved |
|---|---|---|
| REQ-PER-3.1 | A negative existence claim over the entire API surface ("no function exposes a hard delete") cannot be proven by a unit test; enforced by code review and periodic adversarial audit of the public orchestrator surface. | Hobson, 2026-10-05 (delegated by Dan) |

## Unenforceable

Active requirements that bind humans, not code. Nothing in the system enforces these.

| ID | Why it cannot be enforced | Approved |
|---|---|---|
|  |  |  |

## Withdrawn

| ID | Original Requirement | Reason |
|---|---|---|
|  |  |  |
