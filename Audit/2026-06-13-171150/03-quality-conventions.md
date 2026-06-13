# conventions-auditor

**Findings: 12**


---

## STALE-TEMP-1
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Specs/Conventions/README.md, line 15
- **Summary:** README.md advertises AuditEnvelope coverage that Temporal.md does not contain.

The Conventions README index (line 15) describes Temporal.md as covering "AuditEnvelope for temporal coherence." However, the actual Temporal.md file contains zero mentions of AuditEnvelope. The README promises content that does not exist in the referenced file. Decisions.md (line 38) notes IClock was rejected in favor of AuditEnvelope, so this is a real concept in the system, but the convention for how AuditEnvelope works is missing from Temporal.md despite the README claiming it is there.

**Suggested action:** Either add an AuditEnvelope section to Temporal.md explaining the convention (what it wraps, when it is required, how it replaces IClock for audit timestamp injection), or remove the AuditEnvelope claim from the README index entry.

**Why:** An agent told to read the conventions for AuditEnvelope will look in Temporal.md per the README, find nothing, and either guess or ask. The README is the routing document for conventions -- if it lies, agents load the wrong file and waste context.


---

## GAP-TEMP-2
- **Category:** insufficient-elaboration
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Temporal.md, Dates section (lines 35-37)
- **Summary:** Temporal.md specifies the Postgres date type for dates and NodaTime Instant for instants, but never specifies the NodaTime application-layer type for dates.

The Instants section (lines 7-8) explicitly mandates NodaTime's Instant type in the application layer. The persistence section (line 16) mandates Postgres date type for dates. But the Dates section (lines 35-37) only states an arithmetic constraint -- it never names the application-layer type (NodaTime LocalDate? Something else?). This is an asymmetry: instants have a clear convention for both layers, dates only have a persistence convention. An implementer cannot determine from the convention alone what F# / NodaTime type to use for date values.

**Suggested action:** Add a statement to the Dates section specifying the required NodaTime application-layer type for date values (presumably NodaTime.LocalDate) and whether the DateTime/DateTimeOffset prohibition from the Instants section applies symmetrically to dates (i.e., no System.DateTime for dates either).

**Why:** Without this, an implementer might use System.DateTime for date columns while using NodaTime.Instant for instant columns, creating an inconsistency. Or they might use NodaTime.LocalDate without explicit authorization. The convention should be unambiguous about which type represents dates in F#.


---

## GAP-TEMP-3
- **Category:** missing-requirement
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Temporal.md, lines 18-20; /workspace/SonOfLeo/Specs/Behavioral/SystemWide.md, section 3
- **Summary:** Temporal.md mandates the persistence layer must never originate temporal values, but no REQ- ID covers this behavioral constraint.

Temporal.md lines 18-20 state: "The persistence layer may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.)" and "Required (non-nullable) temporal columns carry no defaults; a write that omits the value is rejected, never filled in by the database." These are testable, enforceable constraints on system behavior -- they define what happens when a write omits a timestamp (rejection, not default fill). SystemWide.md section 3 covers audit timestamps (REQ-SYS-3.1 through 3.3) but from the application side ("set to the system clock at time of creation"). Neither spec has a REQ- ID for the database-side prohibition of now() defaults. This matters because REQ-SYS-3.2 says timestamps are set at creation time, but nothing in the REQ-space forbids the database from also having a default that masks a bug where the application fails to provide a value.

**Suggested action:** Either promote the no-database-origination rule to a REQ- ID in SystemWide.md or DataAccessLayer.md (it is testable: check DDL for DEFAULT clauses on temporal columns, check for now() in triggers), or document it explicitly as a convention-only rule that is enforced by DDL review rather than test.

**Why:** If the database has a now() default on created_at, and the application layer has a bug that omits the value, the record will be created successfully with a database-generated timestamp instead of the application-layer clock. This silently violates REQ-SYS-3.2's intent but passes all existing tests. The convention says this is prohibited but no test will catch the violation because there is no REQ- to test against.


---

## GAP-MONEY-1
- **Category:** missing-requirement
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Money.md, lines 39-41; no corresponding REQ- ID in any behavioral spec
- **Summary:** Money.md's half-up rounding rule and exact-allocation rule are convention-only with no corresponding REQ- IDs.

Money.md specifies two testable behavioral constraints: (1) Rounding must use half-up / MidpointRounding.AwayFromZero (line 39), and (2) When splitting transactions, the allocation must sum exactly to the original pre-split amount with residual forced into one part (lines 41-42). Both are directly testable (pass 0.005 and verify it rounds to 0.01; split 10.00 three ways and verify the parts sum to 10.00). Decisions.md (line 54) records the rounding decision as Dan-approved. Yet no behavioral spec has a REQ- ID for either constraint. The Traceability convention (lines 1-2) states: "All business, system, behavioral, or non-functional requirements must be identified by an REQ label." These are behavioral requirements hiding in a convention file without REQ- IDs, which violates the project's own traceability convention.

**Suggested action:** Create REQ- IDs for (1) the half-up rounding mandate and (2) the exact-allocation-on-split rule. These likely belong in a future Money or Journaling behavioral spec. If no such spec exists yet, either create a placeholder spec or add them to SystemWide.md as cross-cutting numeric rules.

**Why:** Without REQ- IDs, these rules cannot be traced through code annotations or test annotations per the Traceability convention. An implementer building the journaling module could use banker's rounding or drop residuals, and no automated audit would flag it because there is no REQ- to audit against.


---

## GAP-MONEY-2
- **Category:** missing-requirement
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Money.md, lines 5-6, 17-22; no corresponding REQ- ID
- **Summary:** Money.md mandates a specific Money type, specific primitives (F# decimal, Postgres numeric(12,2)), and prohibits other types -- all testable constraints without REQ- IDs.

Money.md states: "we insist that the system create and enforce a specific Money type" (line 5-6), "Money amounts will be represented as F# decimal types" (line 17), "Money amounts will be persisted using a Postgres numeric (12,2) column type" (line 19), and "Any other primitive type or column types are prohibited from representing Money amounts" (line 21). These are structural and behavioral constraints that are testable (check column DDL, check F# type definitions). They carry no REQ- IDs, which conflicts with Traceability.md's mandate that all requirements have REQ labels.

**Suggested action:** Determine whether these are conventions (enforced by review, no REQ- needed) or requirements (need REQ- IDs). If they are requirements, mint IDs and move the testable assertions to a behavioral spec. If they are intentionally convention-only, note that the Traceability convention's "all requirements" language may need qualification to distinguish conventions from requirements.

**Why:** The Money.md file uses imperative language ("must", "will", "prohibited") that reads as requirements, but lives in Conventions/ which the README says are "not behavioral requirements verified by tests." Either the rules need REQ- IDs or the imperative language is misleading about their enforcement level.


---

## AMB-MONEY-3
- **Category:** ambiguity
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Money.md, lines 25-27
- **Summary:** Money.md prohibits multiplication/division on Money records but the boundary between record-level and amount-level arithmetic is ambiguous.

Line 25: "Multiplication and division operations are strictly prohibited with Money records." Lines 29-35 then describe unpacking the amount to a primitive, doing math, and repacking. The intent is clear in spirit (don't multiply two Money values together), but the convention does not define what "with Money records" means precisely. Does it mean: (a) the Money type must not have * and / operators defined? (b) no function may accept two Money arguments and multiply them? (c) something else? An implementer could read this as "never call decimal multiply on the inner value without explicitly unpacking first" or as "the type must not expose operator*" -- these lead to different implementations.

**Suggested action:** Clarify whether this means (a) the Money type must not define multiplication/division operators, (b) no code may perform these operations on a value whose static type is Money (must unpack first), or (c) both.

**Why:** F# makes operator overloading explicit. Whether the Money type defines (*) and (/) affects the type's API surface and how compile-time enforcement works. The current wording leaves room for two valid but incompatible implementations.


---

## CONTRA-TEMP-4
- **Category:** stale-annotation
- **Severity:** high
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Temporal.md, line 12; /workspace/SonOfLeo/Specs/Decisions.md, lines 28-31
- **Summary:** Temporal.md says instants are persisted as timestamptz exclusively, but the decision log's date-only overturn and Definitions.md's Date definition require a non-timestamptz date type.

Temporal.md line 12 states: "The database will persist all instances as timestamptz. No exceptions." However, Decisions.md line 31 notes: "the prohibition against date-only has been overturned. See Definitions.md." Definitions.md defines Date as "a calendar coordinate: the name of a single day" with "no time component." Temporal.md line 16 itself then says: "The database will persist dates using the Postgres date type only." These two statements in Temporal.md directly contradict each other: line 12 says "all instances as timestamptz. No exceptions" and line 16 says dates use the Postgres date type. The word "instances" on line 12 likely means "instants" (typo), but as written, "all instances" could be read as "all instances of temporal values" which would include dates. Even reading it charitably as "instants," the proximity of an absolute "No exceptions" statement followed four lines later by a date exception is confusing.

**Suggested action:** Fix the typo on line 12: change "instances" to "instants" and scope the "No exceptions" to make clear it applies only to instant-type values, not to all temporal values. Consider restructuring so the instants-only-timestamptz rule and the dates-only-date-type rule are clearly parallel rather than one appearing to override the other.

**Why:** A developer reading line 12 in isolation concludes all temporal persistence is timestamptz with no exceptions. Four lines later they learn dates use the date type. The contradiction forces the reader to guess which statement wins, and a strict reader of line 12 might persist dates as timestamptz, which would be wrong.


---

## GAP-TEMP-5
- **Category:** missing-requirement
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Temporal.md, lines 22-24; no REQ- ID
- **Summary:** Temporal.md's seconds-precision reconstitution rule is a testable constraint without a REQ- ID.

Temporal.md states: "The system must be able to reconstitute any instant type to seconds precision at minimum, meaning an accurate calculation of time span in seconds between any two instants must be correct." This is a testable, behavioral constraint (store an instant, read it back, verify seconds-level fidelity). It uses the word "must" and describes observable system behavior. It has no REQ- ID, which conflicts with Traceability.md's mandate.

**Suggested action:** Decide whether this is a requirement (needs a REQ- ID, probably in SystemWide.md or a future Temporal behavioral spec) or a convention (enforced by review only). If it is a requirement, assign a REQ- ID and add a test.

**Why:** Same pattern as the Money findings: imperative behavioral language in a convention file without traceability. If precision is important enough to specify, it is important enough to trace.


---

## GAP-BUILD-1
- **Category:** missing-requirement
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Specs/Conventions/BuildAndEnvironment.md, lines 15-17; /workspace/SonOfLeo/Specs/Behavioral/DataAccessLayer.md, REQ-DAL-1.1 through 1.13
- **Summary:** BuildAndEnvironment.md mandates debug-mode executables may never access the production database, but no REQ- ID enforces this and the DAL spec does not reference build configuration.

BuildAndEnvironment.md states: "Any executable configured to run in 'debug' mode may NEVER access the production database. Read or write. Only executables configured to run in 'release' mode may access the production database." This is a security-critical constraint that could be enforced (e.g., the DAL rejects a production connection string when running in debug configuration). The DAL behavioral spec (REQ-DAL-1.1 through 1.13) covers environment variables and connection strings but never mentions debug/release mode as a gate. There is no REQ- ID for this prohibition anywhere.

**Suggested action:** Create a REQ- ID (likely in DataAccessLayer.md or SystemWide.md) that requires the system to reject production database connections when running in debug mode. This is testable: run in debug config with a production connection string and verify rejection.

**Why:** This is the highest-stakes convention in the file -- accidentally running debug code against production data. Without a REQ- ID, there is no test, no code annotation, and no audit trail. The only enforcement is human review of environment configuration, which is exactly the kind of thing humans get wrong.


---

## GAP-TRACE-1
- **Category:** ambiguity
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Traceability.md, lines 1-2; /workspace/SonOfLeo/Specs/Conventions/README.md, lines 3-5
- **Summary:** Traceability.md says ALL requirements need REQ- IDs, but the README says conventions are not behavioral requirements -- creating ambiguity about which "must" statements in convention files are requirements.

Traceability.md opens with: "All business, system, behavioral, or non-functional requirements must be identified by an REQ label." The Conventions README says: "These are developer-facing rules enforced by review, not behavioral requirements verified by tests." Multiple convention files (Money.md, Temporal.md, BuildAndEnvironment.md) use imperative "must" language that reads as requirements. The system gives no clear test for distinguishing a convention (no REQ- needed) from a requirement that happens to be written in a convention file (REQ- needed). This is a meta-ambiguity that underlies several of the other findings.

**Suggested action:** Add a brief note to either the README or Traceability.md that clarifies the distinction: when a convention file says "must," is that a requirement (needs REQ- ID) or a review-enforced guideline? Consider a simple litmus: if it is testable and violation would be a defect, it is a requirement and needs a REQ- ID regardless of which file it lives in.

**Why:** Without this clarification, every convention audit will re-litigate which "must" statements are requirements. A one-time clarification prevents recurring ambiguity.


---

## TYPO-MONEY-1
- **Category:** other
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Money.md, line 27
- **Summary:** "substraction" is a typo for "subtraction."

Line 27 reads: "Addition and substraction operations are permitted..." The word "substraction" is not an English word; the correct spelling is "subtraction."

**Suggested action:** Change "substraction" to "subtraction."

**Why:** Minor, but spec documents are authoritative artifacts. Typos erode trust in precision, especially in a document about arithmetic precision.


---

## GAP-TEMP-6
- **Category:** insufficient-elaboration
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Specs/Conventions/Temporal.md, lines 27-29; /workspace/SonOfLeo/Specs/Decisions.md, lines 32-35
- **Summary:** Temporal.md says invalid external instants must be rejected, but the anchoring decision (calendar dates anchor to US Eastern) is not reflected in any convention.

Decisions.md states: "Imported calendar dates anchor to US Eastern (America/New_York) to become instants." Temporal.md lines 27-29 discuss rejecting external data that does not meet the instant standard, with an exception for system-owned middleware that converts inbound data. However, Temporal.md never mentions the US Eastern anchoring convention. An implementer building an import middleware would not know from the Temporal convention alone which timezone to use for anchoring calendar dates to instants.

**Suggested action:** Add the US Eastern anchoring rule to Temporal.md's middleware exception section, or add a cross-reference to the relevant Decision entry.

**Why:** The anchoring timezone is the most decision-specific part of the temporal model. An implementer reading only Temporal.md (as the README instructs) will miss it entirely because it only exists in Decisions.md.
