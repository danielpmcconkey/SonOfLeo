# requirements-quality-auditor-dal-systemwide

**Findings: 10**


---

## AMB-DAL-2.1
- **Category:** ambiguity
- **Severity:** medium
- **Location:** Specs/Behavioral/DataAccessLayer.md, REQ-DAL-2.1
- **Summary:** REQ-DAL-2.1 requires parameterization for 'all data inserted' while REQ-DAL-2.3 only requires it for 'user input', creating ambiguity about which rule governs.

REQ-DAL-2.1 says 'All data inserted into the database must be parameterized in accordance with industry standard best practice to prevent SQL injection.' REQ-DAL-2.3 says 'All values originating from user input must be parameterized to prevent SQL injection.' These overlap but have different scopes. REQ-DAL-2.1 covers ALL inserts (including system-generated values like UUIDs and timestamps), while REQ-DAL-2.3 narrows to user input. A developer reading both could reasonably ask: does a system-generated UUID need to be parameterized per 2.1, or is it exempt because it's not user input per 2.3? Additionally, REQ-DAL-2.1 says 'inserted' but omits updates and deletes, while REQ-DAL-2.3 says 'values' without specifying operation type. Two reasonable developers could implement different parameterization scopes.

**Suggested action:** Either consolidate 2.1 and 2.3 into a single requirement ('all values passed to SQL statements must be parameterized') or clarify that 2.1 covers the insert path specifically and 2.3 covers all operations for user-originating values. The current pair is trying to say two related things and stepping on each other.

**Why:** Parameterization scope is a security-critical decision. Ambiguity here could lead a developer to skip parameterization for system-generated values in update/delete operations, or conversely waste effort debating whether to parameterize constants.


---

## AMB-DAL-2.2
- **Category:** ambiguity
- **Severity:** medium
- **Location:** Specs/Behavioral/DataAccessLayer.md, REQ-DAL-2.2
- **Summary:** REQ-DAL-2.2's 'verify against expected rows affected' does not specify what happens when the count mismatches.

REQ-DAL-2.2 states: 'All non-scalar queries (set-based read, insert, update, and delete) must verify against expected rows affected.' The requirement says to verify, but does not say what compliance looks like when verification fails. Must the system throw an exception? Roll back? Log a warning? Return an error result? Two developers could reasonably implement this differently -- one might throw, another might return a Result.Error, a third might log and continue. Additionally, for set-based reads, the concept of 'expected rows affected' is unusual -- reads don't 'affect' rows. It is unclear whether the intent is to verify that a read returned the expected number of rows (which would require the caller to know the expected count ahead of time, which is often impossible for set-based reads).

**Suggested action:** Clarify two things: (1) what the system must do when the actual row count differs from expected (throw? return error?), and (2) whether set-based reads are really in scope here or if this was intended for write operations only. Consider splitting into a write-side requirement (insert/update/delete must verify rows affected and fail if mismatched) and removing reads from scope.

**Why:** Row-count verification is an important correctness guard. Without specifying the failure behavior, different modules could handle mismatches inconsistently, making debugging harder and potentially letting data corruption go unnoticed.


---

## AMB-SYS-5.1
- **Category:** ambiguity
- **Severity:** medium
- **Location:** Specs/Behavioral/SystemWide.md, REQ-SYS-5.1
- **Summary:** 'Perfectly reconstituted' is insufficiently precise given the temporal precision constraints in Conventions/Temporal.md.

REQ-SYS-5.1 says: 'The persistence layer must persist all entity properties in such a way that the entity type can be perfectly reconstituted upon subsequent read.' The word 'perfectly' implies bit-for-bit fidelity, but Conventions/Temporal.md establishes that instants need only be accurate to seconds precision, and the Decisions log (2026-06-11) says 'persistence stores the instant and deliberately discards the original local offset.' So reconstitution is not perfect in the literal sense -- sub-second precision may be lost, and the original offset IS lost. Similarly, Money convention specifies numeric(12,2), which means any decimal value beyond 2dp would be truncated. A pedantic reading of 'perfectly reconstituted' would call these violations. The intent is clearly 'reconstituted within the system's declared precision constraints,' but the requirement doesn't say that.

**Suggested action:** Amend REQ-SYS-5.1 to something like: 'The persistence layer must persist all entity properties in such a way that the entity type can be reconstituted with full fidelity to the system's declared type constraints upon subsequent read.' This makes it clear that 'perfect' means 'as defined by the type,' not 'bit-for-bit identical to whatever was passed in before type construction.'

**Why:** This is the kind of requirement that will bite you during an audit. An auditor comparing REQ-SYS-5.1 against Conventions/Temporal.md's seconds-precision rule could flag every instant that lost sub-second precision as a violation. The intent is clear to humans today but the text doesn't match the intent.


---

## STALE-SYS-3-TODO
- **Category:** stale-annotation
- **Severity:** low
- **Location:** Specs/Behavioral/SystemWide.md, line 26 (after REQ-SYS-3.3)
- **Summary:** The 'todo: add a requirement for logging audit activities to an external log' comment is a known gap that should be tracked outside the spec.

Line 26 of SystemWide.md contains a bare 'todo' comment sitting between REQ-SYS-3.3 and Section 4. This is flagged per the task instructions as a known gap. The comment has no REQ ID, no owner, and no target date. It sits in a behavioral spec where every other statement is either a requirement with an ID or structural prose. A todo comment in a requirements document is inherently a different species of artifact -- it's a reminder to the spec author, not a statement of system behavior.

**Suggested action:** Either (a) promote this to a real requirement with an ID (e.g., REQ-SYS-3.4) and a clear behavioral statement, even if it's immediately waived from testing, or (b) move it to a backlog/tracking system and remove the comment from the spec. The spec should contain requirements and structural prose, not personal reminders.

**Why:** Bare todos in spec files create ambiguity about whether the behavior is required but unwritten, aspirational, or just a thought someone had. Any agent auditing this spec will have to ask 'is this a requirement or not?' every time.


---

## AMB-DAL-3.2.1
- **Category:** ambiguity
- **Severity:** low
- **Location:** Specs/Behavioral/DataAccessLayer.md, REQ-DAL-3.2.1
- **Summary:** The exception allowing 'non-Ansi-generic SQL strings' undermines the abstraction guarantee of REQ-DAL-3.2 without any constraint on scope.

REQ-DAL-3.2 says the DAL must build abstraction layers so callers don't need PostgreSQL references, preserving the ability to shift RDBMS. REQ-DAL-3.2.1 immediately carves out an exception: 'client modules can pass non-Ansi-generic SQL strings to the DAL if needed.' The phrase 'if needed' is unbounded -- any developer can claim their use case 'needs' PostgreSQL-specific SQL. This effectively makes REQ-DAL-3.2 unenforceable in practice because any violation can be justified as falling under 3.2.1. Two developers would disagree on where the line is. This is not necessarily a problem (it may be a deliberate pragmatism valve), but it should be acknowledged as making 3.2 aspirational rather than enforceable.

**Suggested action:** Consider adding guidance on when non-ANSI SQL is acceptable (e.g., 'for performance-critical queries where the ANSI equivalent would be materially slower' or 'for PostgreSQL-specific features with no ANSI equivalent'). Alternatively, acknowledge explicitly that REQ-DAL-3.2 is aspirational and 3.2.1 is the escape valve, so auditors know not to flag every PostgreSQL-specific query as a violation.

**Why:** Without a boundary on the exception, the abstraction goal in 3.2 has no teeth. This matters less now (you're not switching databases tomorrow) but could matter during audits when someone asks 'are we compliant with our own specs?'


---

## CONTRA-SYS-3.2-TEMPORAL
- **Category:** contradiction
- **Severity:** medium
- **Location:** Specs/Behavioral/SystemWide.md, REQ-SYS-3.2 vs Specs/Conventions/Temporal.md
- **Summary:** REQ-SYS-3.2 says timestamps must be set to 'the system clock at time of creation' but does not specify precision, while Conventions/Temporal.md mandates seconds precision and the Temporal convention says 'the persistence layer may never be the originator of temporal values.'

REQ-SYS-3.2 says 'both created at and modified at timestamps must be set to the system clock at time of creation.' This is a behavioral requirement (authority level 4). Conventions/Temporal.md (authority level 3, higher) says instants must be reconstitutable to seconds precision at minimum. The requirement doesn't specify that both timestamps must be identical instants (i.e., captured from a single clock read), which matters because if the implementation reads the clock twice, the two timestamps could differ by microseconds or even milliseconds. This would violate REQ-SYS-5.1's reconstitution fidelity if the system expects them to be identical on creation. The Decisions log mentions 'AuditEnvelope type' as the replacement for IClock, suggesting temporal coherence is handled there, but REQ-SYS-3.2 doesn't reference or defer to that mechanism.

**Suggested action:** Clarify in REQ-SYS-3.2 that both timestamps must be captured from a single clock read (or from the AuditEnvelope's timestamp) to ensure they are identical on creation. This aligns the requirement with the AuditEnvelope approach mentioned in Decisions.md.

**Why:** If two clock reads produce different values, a test asserting created_at == modified_at on a fresh record could intermittently fail. More importantly, the requirement should be precise enough that the AuditEnvelope approach (single timestamp for all audit fields) is clearly the correct implementation rather than an arbitrary design choice.


---

## INSUF-DAL-3.6
- **Category:** insufficient-elaboration
- **Severity:** low
- **Location:** Specs/Behavioral/DataAccessLayer.md, REQ-DAL-3.6
- **Summary:** REQ-DAL-3.6 mixes a testable requirement with an advisory DBA note, blurring what compliance looks like.

REQ-DAL-3.6 says: 'The system will generally not enforce business logic in the database layer outside of foreign key and unique key constraints. The application layer is responsible for all enforcement of legal data states. Therefore, it should be noted for all database administrators that granting write access to any table within this database should be kept to a minimum. Caveat emptor.' The first sentence is a testable requirement (no check constraints, no triggers for business logic, FK and UK only). The second sentence is an advisory to DBAs about access control. The third is a disclaimer. Only the first sentence is a behavioral requirement. The word 'generally' makes even that soft -- does a NOT NULL constraint count as 'business logic'? What about a CHECK constraint that enforces a positive value on a numeric column?

**Suggested action:** Split into two parts: (1) a requirement stating what database-layer enforcement IS allowed (FK, UK, NOT NULL, data type constraints) vs what is NOT (triggers, check constraints encoding business rules, stored procedures), and (2) a separate advisory note for DBAs. Remove 'generally' or define its boundary.

**Why:** The ambiguity around 'generally not enforce business logic' means a developer adding a CHECK constraint could argue either way. For a system that explicitly puts business logic in the application layer (a deliberate architectural decision), the boundary should be crisp.


---

## AMB-AC-1.42-43-TEMPORAL
- **Category:** ambiguity
- **Severity:** medium
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-1.42 and REQ-AC-1.43 vs Specs/Definitions.md
- **Summary:** REQ-AC-1.42 and REQ-AC-1.43 say 'date/time' but the Definitions distinguish 'Instant' from 'Date' -- which one are these fields?

REQ-AC-1.42 says accounts 'must be able to represent a date/time signifying when that account began as an active account.' REQ-AC-1.43 uses the same 'date/time' phrasing for active end. Definitions.md carefully distinguishes between Instant ('a singular and globally agreed-upon point in time') and Date ('a calendar coordinate: the name of a single day'). The phrase 'date/time' is neither of these defined terms. Decisions.md originally said 'all temporal values are instants; no date-only values' but then adds 'Note: the prohibition against date-only has been overturned. See Definitions.md.' Definitions.md now includes both Instant and Date as valid temporal concepts. So: are active_begin and active_end Instants or Dates? REQ-AC-1.46 says 'earlier or equal in time,' which suggests Instant (time-based comparison). REQ-AC-1.48 says 'active end date,' using the word 'date.' The requirements use both terms interchangeably, but the Definitions say they are fundamentally different things. Conventions/Temporal.md has separate sections for instants and dates with different rules for each.

**Suggested action:** Replace all occurrences of 'date/time' in REQ-AC-1.42, 1.43, 1.46, 1.48, 4.1, 4.2, and 2.17/2.18 with the correct Definitions.md term -- either 'Instant' or 'Date'. Given that the existing code likely uses Instant (based on the temporal convention mandating timestamptz for instants), this should be 'Instant' throughout. If the intent is that these are calendar Dates, that changes the comparison semantics significantly and several requirements need rework.

**Why:** Definitions.md exists specifically because 'a term that does scope arithmetic must be pinned once.' The active_begin/active_end fields are subject to arithmetic (REQ-AC-1.46 compares them, REQ-AC-4.2 compares them). Using an undefined compound term 'date/time' instead of the defined terms 'Instant' or 'Date' defeats the purpose of having Definitions.md.


---

## CONTRA-DAL-1.8-CONNSTR
- **Category:** ambiguity
- **Severity:** low
- **Location:** Specs/Behavioral/DataAccessLayer.md, REQ-DAL-1.8 vs REQ-DAL-1.9
- **Summary:** REQ-DAL-1.8 says the connection string 'will not print' the password, but 'print' is an unusual verb for a configuration file.

REQ-DAL-1.8 says: 'The SonOfLeo connection string will not print the database password in the external configuration file.' The word 'print' is ambiguous -- does it mean the password will not be stored/written in the config file (which is the clear intent when read alongside REQ-DAL-1.9's runtime injection), or does it mean the system will not log/display the connection string with the password? The word 'print' in a software context could mean either 'write to file' or 'output to console/log.' REQ-DAL-1.9 clarifies the mechanism (inject at runtime), but 1.8's phrasing could be read as a logging/display restriction rather than a storage restriction.

**Suggested action:** Reword REQ-DAL-1.8 to: 'The external configuration file must not contain the database password; the connection string must include a placeholder that is resolved at runtime per REQ-DAL-1.9.'

**Why:** Minor clarity issue. A developer reading 1.8 and 1.9 together will understand the intent, but 'print' is a loaded word in programming and could confuse someone reading 1.8 in isolation.


---

## INSUF-SYS-2.2
- **Category:** insufficient-elaboration
- **Severity:** medium
- **Location:** Specs/Behavioral/SystemWide.md, REQ-SYS-2.2
- **Summary:** REQ-SYS-2.2's 'where possible' qualifier makes it unclear when pre-write rejection is required vs optional.

REQ-SYS-2.2 says: 'Where possible, rejections under REQ-SYS-2.1 must occur before any database write, and must produce a meaningful error message.' The phrase 'where possible' is doing heavy lifting without definition. Every validation is technically 'possible' before a database write -- even uniqueness checks can be done with a pre-read (though they're subject to race conditions). The intent seems to be that validations requiring database state (like uniqueness) may defer to the DB constraint, while pure data-state validations (like string length, null checks) must happen in application code. But the requirement doesn't draw that line. Two developers could disagree on whether a parent-ID-exists check (REQ-AC-2.6) must happen before the write or can rely on the FK constraint.

**Suggested action:** Replace 'where possible' with a concrete boundary, e.g., 'Rejections that can be determined from the entity's own properties (without querying the database) must occur before any database write. Rejections that require database state (e.g., uniqueness, referential integrity) should occur before the write where practical, but may fall through to database constraints.'

**Why:** This requirement governs the validation architecture for every entity in the system. Without a clear boundary, each entity module will make its own judgment about what's 'possible,' leading to inconsistent validation strategies across the codebase.
