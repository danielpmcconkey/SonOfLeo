# Synthesis: Recommended Actions



I need to analyze all 48 raw findings, deduplicate them, and produce a consolidated audit report. Let me first identify the duplicates.

Looking through the findings, I can identify several clusters of duplicates:

1. **AMB-AC-1.42-43** and **AMB-AC-1.42-43-TEMPORAL** - same issue about date/time vs Instant terminology
2. **AMB-AC-1.48** (from AMB-AC-1.48 finding) and **AMB-AC-1.48-BEFORE-BEGIN** and **MISSING-REQ-NOT-BEGUN** and **ISACTIVE-LOGIC-GAP** - all about the missing "active" definition
3. **GAP-AC-1.47** and **AMB-AC-1.47** - same issue about REQ-AC-1.47 waived-from-testing
4. **TEMPORAL-MIGRATION-NOW** and **TEMP-MIG-1** and **CVV-AC-DB-DEFAULTS** - all about DEFAULT now() in migration
5. **PHANTOM-AC-1.25/1.26** and **TRACE-STALE-2** and **SA-AC-1.25-1.26** - same stale annotations on lines 100-101
6. **PHANTOM-AC-2.1** and **TRACE-STALE-1** - same stale annotations on lines 33/46
7. **PHANTOM-AC-3.1** and **TRACE-STALE-3** - same stale annotations on lines 157-161
8. **PHANTOM-AC-2.15** and **TRACE-STALE-4** - same stale annotations on lines 227/239
9. **PHANTOM-AC-4.7** and **TRACE-STALE-5** - same stale annotations on lines 371/397
10. **PHANTOM-AC-4.21** and **TRACE-STALE-6** - same stale annotations on lines 472/483
11. **PHANTOM-AC-4.16** and **CIRCULAR-ANCESTRY-REASONING** - overlapping (dead REQ in comment block)
12. **MISSING-TRIM-SUBTYPE** and **MISSING-ANNO-TRIM-DAL** - AccountType.fromString trimming (line 82) covered in both
13. **CONTRA-TEMP-4** relates to the same Temporal.md line 12 issue

Now let me produce the consolidated report.

# SonOfLeo Requirements Audit -- Consolidated Findings

## Executive Summary

Six specialist agents audited the SonOfLeo specs, conventions, code annotations, and migration DDL. After deduplication, **35 unique findings** remain. The three most consequential issues are: (1) the database migration includes `DEFAULT now()` on three temporal columns, directly violating the Temporal convention's absolute prohibition; (2) the spec never positively defines "active," leaving every requirement that references active status dependent on an unstated definition; and (3) `Money.fromDecimal` uses .NET's default banker's rounding instead of the mandated half-up rounding. Roughly a third of the findings are stale code annotations pointing at withdrawn requirement IDs -- mechanical fixes that can be batched. Several findings require Dan's judgment on spec-level design questions (e.g., whether convention-file "must" statements need REQ- IDs).

---

## Findings by Category

### Contradictions / Convention Violations

#### CV-1: DEFAULT now() in Account Migration (HIGH)
- **Merged from:** TEMPORAL-MIGRATION-NOW, TEMP-MIG-1, CVV-AC-DB-DEFAULTS
- **Location:** `DbMigrations/2026-06-01-07-48-CreateAccountTable.sql`, lines 45, 47, 48
- **Action (fix the code):** Create a new migration to `ALTER COLUMN ... DROP DEFAULT` on `active_begin`, `created_at`, and `modified_at`. The application already supplies these values; removing the defaults ensures an omission fails loudly.
- **Why:** Temporal convention absolutely prohibits `now()` in defaults. The defaults create a silent fallback that masks application bugs and breaks audit trail guarantees.
- **Dan's Disposition** Agreed. I nuked the DB from orbit and rebuilt all new migration files


#### CV-2: Money.fromDecimal Uses Banker's Rounding (HIGH)
- **Merged from:** MONEY-ROUND-1
- **Location:** `Src/Model/Money.fs`, line 20
- **Action (fix the code):** Change `Math.Round(raw, 2)` to `Math.Round(raw, 2, MidpointRounding.AwayFromZero)`.
- **Why:** Money convention mandates half-up rounding and explicitly warns that .NET defaults to banker's rounding. `splitByN` on line 48 already does this correctly; `fromDecimal` is inconsistent.
- **Dan's Disposition** Incorrect, the rounding is used only to ensure the value isn't more precise that it should be. We use the raw value when constructing the Money record. However, because you guys will flag this until I die, I have explicitly stated the rounding  

#### CV-3: Temporal.md "instances" Typo Creates Self-Contradiction (HIGH)
- **Merged from:** CONTRA-TEMP-4
- **Location:** `Specs/Conventions/Temporal.md`, line 12
- **Action (fix the spec):** Change "instances" to "instants" on line 12, and scope the "No exceptions" clause to instant-type values only (since line 16 correctly says dates use `date` type).
- **Why:** As written, line 12 says "all instances as timestamptz. No exceptions" and line 16 says dates use the `date` type. A strict reader concludes they contradict each other.
- **Dan's Disposition** Good catch. updated to "The database will persist all `Instant` values as `timestamptz`. No exceptions."

#### CV-4: Money.fromDecimal Named Wrong per Naming Convention (MEDIUM)
- **Merged from:** NAMING-MONEY-1
- **Location:** `Src/Model/Money.fs`, line 19
- **Action (fix the code):** Rename public `fromDecimal` to `create`; rename private `create` to `wrap` or inline it.
- **Why:** Naming convention says wrapping constructors use `create`. Every other wrapping constructor in the codebase follows this pattern.
- **Dan's Disposition** Overruled. fromDecimal is the easiest name for the caller to understand. It stays.

#### CV-5: REQ-SYS-3.2 Should Specify Single Clock Read (MEDIUM)
- **Merged from:** CONTRA-SYS-3.2-TEMPORAL
- **Location:** `Specs/Behavioral/SystemWide.md`, REQ-SYS-3.2
- **Action (fix the spec):** Add "both timestamps must be derived from a single clock read (or from the AuditEnvelope's instant)" to REQ-SYS-3.2.
- **Why:** Two separate clock reads could produce different values, causing intermittent test failures on `created_at == modified_at` assertions and misaligning with the AuditEnvelope design.
- **Dan's Disposition** agreed. updated to "When a record is created, both "created at" and "modified at" Instant properties must be set to the AuditEnvelope's system instant property at time of creation."

---

### Ambiguities

#### AMB-1: No Positive Definition of "Active" (HIGH)
- **Merged from:** AMB-AC-1.48, ISACTIVE-LOGIC-GAP, AMB-AC-1.48-BEFORE-BEGIN, MISSING-REQ-NOT-BEGUN
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-1.48 and Section 1
- **Action (fix the spec):** Add a companion requirement (e.g., REQ-AC-1.50): "An Account is active when activeBegin <= referencePoint AND (activeEnd is None OR activeEnd > referencePoint). An Account whose referencePoint precedes its activeBegin is not active."
- **Why:** The spec defines "deactivated" but never "active." The code's `isActive` function treats not-yet-started accounts as inactive, but this behavior has no spec backing. Every requirement referencing "active" (REQ-AC-2.7, REQ-AC-4.3, REQ-AC-4.19) inherits the gap.
- **Dan's Disposition** agreed. added REQ-AC-1.50

#### AMB-2: "date/time" Used Instead of Defined Terms (MEDIUM)
- **Merged from:** AMB-AC-1.42-43, AMB-AC-1.42-43-TEMPORAL
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-1.42, 1.43, 1.46, 1.48, 4.1, 4.2
- **Action (fix the spec):** Replace "date/time" with "Instant" in REQ-AC-1.42, 1.43. Replace "active end date" with "active end Instant" in REQ-AC-1.48, 4.1, 4.2, 4.6.
- **Why:** Definitions.md distinguishes Instant from Date with different semantics. Using undefined compound terms defeats the purpose of having Definitions.md.
- **Dan's Disposition** Agreed on 1.42, 1.43 and changed "date/time" to "valid Instant". On 1.46, I removed "in time", though it's an awfully pedantic reading by the agent. Did both on 1.48. Changed 4.1, 4.2, 4.6 in ways that make sense for each.

#### AMB-3: REQ-AC-2.7 Missing Reference Point (MEDIUM)
- **Merged from:** AMB-AC-2.7
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-2.7
- **Action (fix the spec):** Append "as-of system run-time" (matching REQ-AC-4.3's pattern) to the parent-active check.
- **Why:** REQ-AC-1.48.1 requires every deactivation-status reference to specify its reference point. REQ-AC-2.7 violates this self-imposed rule.
- **Dan's Disposition** changed both it and 4.3 to say "(reference as-of the AuditEnvelope's instant property)"

#### AMB-4: REQ-DAL-2.1 vs REQ-DAL-2.3 Overlapping Scope (MEDIUM)
- **Merged from:** AMB-DAL-2.1
- **Location:** `Specs/Behavioral/DataAccessLayer.md`, REQ-DAL-2.1 and 2.3
- **Action (fix the spec):** Consolidate into a single requirement: "All values passed to SQL statements must be parameterized to prevent SQL injection." Remove the separate user-input-only version.
- **Why:** Two requirements with different scopes on the same topic creates ambiguity about whether system-generated values in updates/deletes need parameterization.
- **Dan's Disposition** Overruled. Those are 2 separate concepts and they are specifically broken out this way to prevent me from being required to parameterize the limit in Account.fs line 200, which would break my "flexible multipurpose" intent without forcing me to have the entire query post from clause to be provided.

#### AMB-5: REQ-DAL-2.2 Missing Failure Behavior (MEDIUM)
- **Merged from:** AMB-DAL-2.2
- **Location:** `Specs/Behavioral/DataAccessLayer.md`, REQ-DAL-2.2
- **Action (fix the spec):** Specify the consequence of a row-count mismatch (e.g., "must return an Error result") and remove set-based reads from scope.
- **Why:** "Verify" without specifying the failure mode leaves each module to invent its own error handling.
- **Dan's Disposition** overruled. this is just dumb. What do they think validation means? We probably have 2 dozen requirements in this repo that say "must validate". What's different about this one? I'm not setting a precedent that forces me to write a useless sentence every time I want a requriement.

#### AMB-6: REQ-SYS-5.1 "Perfectly Reconstituted" Overpromises (MEDIUM)
- **Merged from:** AMB-SYS-5.1
- **Location:** `Specs/Behavioral/SystemWide.md`, REQ-SYS-5.1
- **Action (fix the spec):** Change "perfectly reconstituted" to "reconstituted with full fidelity to the system's declared type constraints."
- **Why:** Temporal convention loses sub-second precision and offsets; Money convention truncates beyond 2dp. A pedantic audit would flag these as violations of "perfectly."
- **Dan's Disposition** Overruled. Any application of the system that writes a value to the database must do so in such a way that any subsequent application of the system that reads that record from the database would return an Entity that is byte perfect from what was written--assuming no outside actor directly wrote to the DB. The "edge" cases this reviewer cites aren't edge. They're quite specifically and deliberately planned for and coded against  

#### AMB-7: REQ-SYS-2.2 "Where Possible" Unbounded (MEDIUM)
- **Merged from:** INSUF-SYS-2.2
- **Location:** `Specs/Behavioral/SystemWide.md`, REQ-SYS-2.2
- **Action (fix the spec):** Replace "where possible" with a concrete boundary: "Rejections determinable from the entity's own properties must occur before any database write. Rejections requiring database state may fall through to database constraints."
- **Why:** Without a boundary, each entity module makes its own judgment about what's "possible," producing inconsistent validation architectures.
- **Dan's Disposition** Agreed. created 2.1.1 and 2.1.2. moved 2.2 to withdrawn section.

#### AMB-8: REQ-AC-1.47 Neither Tested Nor Waived (MEDIUM)
- **Merged from:** GAP-AC-1.47, AMB-AC-1.47
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-1.47
- **Action (fix the spec):** Add REQ-AC-1.47 to the waived-from-testing table with reason: "Satisfied by construction: REQ-AC-2.13 (IDs generated at insert) and REQ-AC-4.22 (parent ID immutable) make circular ancestry structurally impossible."
- **Why:** The two-state rule says every active requirement is either tested or waived. REQ-AC-1.47 is in neither state.
- **Dan's Disposition** Added it to the "waved from testing" section

#### AMB-9: "System Run-Time" Ambiguous Between Wall Clock and AuditEnvelope (LOW)
- **Merged from:** AMB-AC-4.19
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-4.19 and REQ-AC-4.3
- **Action (fix the spec):** Clarify that "system run-time" means "the AuditEnvelope instant provided to the operation."
- **Why:** In production they're identical; in tests they could diverge. Pinning the term now prevents confusion when tests are written.
- **Dan's Disposition** 4.19 seems to have had a copy + paste error. I removed the reference to system run time.

#### AMB-10: REQ-AC-4.6 Uses Undefined Forward References (LOW)
- **Merged from:** AMB-AC-4.6
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-4.6
- **Action (fix the spec):** Add a note: "entry date and post date are forward references to the journal entry spec; comparison semantics will be finalized when that spec is written."
- **Why:** If journal dates are calendar Dates (not Instants), comparison against an Instant field requires timezone assumptions not yet specified.
- **Dan's Disposition** I'd already changed the word "dates" to "instants". entry and posted dates are otherwise unambiguous in the world of accounting

#### AMB-11: REQ-DAL-3.2.1 Escape Valve Unbounded (LOW)
- **Merged from:** AMB-DAL-3.2.1
- **Location:** `Specs/Behavioral/DataAccessLayer.md`, REQ-DAL-3.2.1
- **Action (fix the spec):** Add guidance on when non-ANSI SQL is acceptable, or explicitly acknowledge REQ-DAL-3.2 is aspirational.
- **Why:** "If needed" makes the abstraction requirement unenforceable. Acknowledging this saves future audit arguments.
- **Dan's Disposition** ours is a system architecture that requires the client modules to pass query strings and parameters into the DAL functions. If that's the case, they *must* have the discretion to determine when it's appropriate and when not to use Postgres-specific SQL. I don't know how to word this in any way. I think it's a bullshit finding.

#### AMB-12: REQ-DAL-1.8 "Print" Ambiguous (LOW)
- **Merged from:** CONTRA-DAL-1.8-CONNSTR
- **Location:** `Specs/Behavioral/DataAccessLayer.md`, REQ-DAL-1.8
- **Action (fix the spec):** Reword to "The external configuration file must not contain the database password."
- **Why:** "Print" is ambiguous in a software context (write-to-file vs. output-to-console).
- **Dan's Disposition** I re-wrote 1.8 to "Any connection string in this system must use a parameter to represent the database password that will only be resolved at runtime when the system will read the password from a configured secret vault or environment variable"

#### AMB-13: Money Multiplication Prohibition Boundary Unclear (LOW)
- **Merged from:** AMB-MONEY-3
- **Location:** `Specs/Conventions/Money.md`, lines 25-27
- **Action (fix the spec):** Clarify whether the prohibition means (a) Money type must not define `*` and `/` operators, (b) code must unpack before multiplying, or (c) both.
- **Why:** F# makes operator overloading explicit; the answer changes the type's API surface.
- **Dan's Disposition** Overruled. The code already doesn't have a definition for * and / operators. The requirement says "can't do it". What more could we possibly do here? 

---

### Missing Requirements

#### MR-1: No Parent-Child Type Constraint (HIGH)
- **Merged from:** MR-AC-PARENT-TYPE
- **Location:** `Specs/Behavioral/AccountCrud.md`, Section 2
- **Action (fix the spec -- DAN DECIDES):** Add a requirement: "When creating an Account with a parent ID, the child's AccountType must match the parent's AccountType."
- **Why:** Without this, an Expense account can nest under an Asset header, making tree-based balance aggregation produce nonsensical subtotals.
- **Dan's Disposition** good call out. Added AC-2.19

#### MR-2: No Fetch-All Requirement (MEDIUM)
- **Merged from:** MR-AC-FETCH-ALL
- **Location:** `Specs/Behavioral/AccountCrud.md`, Section 3
- **Action (fix the spec):** Add a requirement: "The system must be able to retrieve all Account records."
- **Why:** The weekly COA sync (most common account read operation) requires listing all accounts. The CLI cannot implement `account list` without this.
- **Dan's Disposition** I added REQ-AC-3.7 and REQ-AC-3.8

#### MR-3: Money Rounding and Allocation Rules Have No REQ- IDs (MEDIUM)
- **Merged from:** GAP-MONEY-1
- **Location:** `Specs/Conventions/Money.md`, lines 39-42
- **Action (DAN DECIDES -- see also DEC-1 below):** If convention "must" statements are requirements, mint REQ- IDs for (1) half-up rounding and (2) exact-allocation-on-split.
- **Why:** Without REQ- IDs, these rules cannot be traced through code or test annotations per the Traceability convention.
- **Dan's Disposition** I want to punt on this. The question is valid. But we need to decide how these "specs" get refactored. That's a bigger convo. and I recognize the traceability gap

#### MR-4: Debug-Mode Production-DB Prohibition Has No REQ- ID (MEDIUM)
- **Merged from:** GAP-BUILD-1
- **Location:** `Specs/Conventions/BuildAndEnvironment.md`, lines 15-17
- **Action (DAN DECIDES -- see also DEC-1 below):** If this is a requirement, mint a REQ- ID (likely in DAL or SystemWide) and add a test.
- **Why:** Highest-stakes convention in the file. Without a REQ- ID and test, the only enforcement is human review.
- **Dan's Disposition** BD, recommend me a requirement for this. 

#### MR-5: Temporal No-Database-Origination Has No REQ- ID (LOW)
- **Merged from:** GAP-TEMP-3
- **Location:** `Specs/Conventions/Temporal.md`, lines 18-20
- **Action (DAN DECIDES -- see also DEC-1 below):** If this is a requirement, mint a REQ- ID. Testable: check DDL for DEFAULT clauses on temporal columns.
- **Why:** If the database has a `now()` default and the application omits a value, the record is created with a database-generated timestamp that silently violates REQ-SYS-3.2.
- **Dan's Disposition** See my DEC-1 comment, though this probably needs to be a requirement.

#### MR-6: Money Type/Primitive Constraints Have No REQ- IDs (LOW)
- **Merged from:** GAP-MONEY-2
- **Location:** `Specs/Conventions/Money.md`, lines 5-6, 17-22
- **Action (DAN DECIDES -- see also DEC-1 below):** Same meta-question as MR-3.
- **Why:** Imperative "must" language in a convention file that the README says is "not behavioral requirements."
- **Dan's Disposition** see my DEC-1 comment

#### MR-7: Seconds-Precision Rule Has No REQ- ID (LOW)
- **Merged from:** GAP-TEMP-5
- **Location:** `Specs/Conventions/Temporal.md`, lines 22-24
- **Action (DAN DECIDES -- see also DEC-1 below):** Same meta-question.
- **Why:** Testable behavioral constraint using "must" without traceability.
- **Dan's Disposition** see my DEC-1 comment

---

### Insufficient Elaboration

#### IE-1: Temporal.md Missing Application-Layer Date Type (MEDIUM)
- **Merged from:** GAP-TEMP-2
- **Location:** `Specs/Conventions/Temporal.md`, Dates section (lines 35-37)
- **Action (fix the spec):** Add: "Date values in the application layer must use NodaTime.LocalDate. System.DateTime is prohibited for date values."
- **Why:** Instants have a clear convention for both layers; dates only have a persistence convention. An implementer cannot determine the F# type for dates from the convention alone.
- **Dan's Disposition** This will need to be defined when we get to that piece of the project. For now, there are no calendar date needs so I'm not going to waste time deliberating the correct storage and app-layer representation for a value I neither store nor represent at this Instant.

#### IE-2: REQ-DAL-3.6 Mixes Requirement and DBA Advisory (LOW)
- **Merged from:** INSUF-DAL-3.6
- **Location:** `Specs/Behavioral/DataAccessLayer.md`, REQ-DAL-3.6
- **Action (fix the spec):** Split into (1) a requirement listing allowed DB enforcement (FK, UK, NOT NULL, data types) vs. prohibited (triggers, business-logic CHECK constraints), and (2) a separate DBA advisory note. Remove "generally."
- **Why:** "Generally not enforce" is untestable. A developer adding a CHECK constraint can argue either way.
- **Dan's Disposition** I'm gonna punt on this. There's a meta convo needed here and I don't have the energy for it today.

#### IE-3: Temporal.md Missing US Eastern Anchoring Rule (LOW)
- **Merged from:** GAP-TEMP-6
- **Location:** `Specs/Conventions/Temporal.md`, lines 27-29
- **Action (fix the spec):** Add the US Eastern anchoring rule from Decisions.md to Temporal.md's middleware section, or add a cross-reference.
- **Why:** An implementer building import middleware will not know the anchoring timezone from Temporal.md alone.
- **Dan's Disposition** Overruled. I deleted the "decision". While it was something I said, it was said in the context of the assumption rules that would be imposed on the importers, by their creators, and at the time of their creation.

#### IE-4: Equity Subtypes Not Future-Proofed (LOW)
- **Merged from:** MR-AC-EQUITY-SUBTYPES
- **Location:** `Specs/Behavioral/AccountCrud.md`, REQ-AC-1.32
- **Action (DAN DECIDES):** Either add equity subtypes now or document as a deliberate deferral.
- **Why:** If period close is ever implemented, the system needs to identify the retained-earnings equity account. Current flat structure provides no mechanism.
- **Dan's Disposition** The auditor's right that we'll need to distinguish retained earnings from other equity accounts eventually. But the subtype   isn't the only way to do it. And it's not even the obvious way to do it. We could just as easily identify it by code (3099), by name, or by a flag. The requirement they're proposing solves a problem we don't have yet, in a shape we haven't chosen yet. It's a deliberate deferral. When we build period closure, we'll know exactly what the closing procedure needs and can add the right mechanism then. Speculating now just cements a guess.

---

### Stale Annotations (Code)

All of these are **fix the code** actions -- update the comment to reference the surviving requirement ID.

| ID | Severity | Location | Old REQ | New REQ |
|---|---|---|---|---|
| SA-1 | MEDIUM | `Account.fs:100` | REQ-AC-1.25, REQ-AC-2.11 | REQ-SYS-3.2 |
| SA-2 | MEDIUM | `Account.fs:101` | REQ-AC-1.26, REQ-AC-2.12 | REQ-SYS-3.2 |
| SA-3 | MEDIUM | `AccountComponent.fs:33, 46` | REQ-AC-2.1 | REQ-SYS-1.1 |
| SA-4 | MEDIUM | `Account.fs:157-161` | REQ-AC-3.1 | REQ-SYS-2.1 |
| SA-5 | MEDIUM | `Account.fs:227, 239` | REQ-AC-2.15 | REQ-SYS-5.1 |
| SA-6 | MEDIUM | `Account.fs:371, 397` | REQ-AC-4.7 | REQ-SYS-3.3 |
| SA-7 | MEDIUM | `Account.fs:472, 483` | REQ-AC-4.21 | REQ-SYS-2.1 |
| SA-8 | MEDIUM | `Account.fs:320-332` | REQ-AC-4.16 | REQ-AC-4.22 (also add note re: structural guarantee depending on updateDb not accepting parentId changes) | Dan comment, I disagree with adding the note. We have a requirement and we have an enforcement mechanism. We don't need to yap about it in prose multiple times.
| SA-9 | MEDIUM | `Account.fs:148` | REQ-AC-2.10 | REQ-SYS-2.1 (reconstitute path, not create path) |
| SA-10 | MEDIUM | `Tests.Ledger/AccountCrud.fs:6` | REQ-AC-001 | Delete placeholder or rename to real REQ- IDs | Dan's comment: Testing hasn't even been started yet
| SA-11 | LOW | `Migration SQL:47-48` | REQ-AC-1.25, REQ-AC-1.26 | REQ-SYS-3.2 (cosmetic; migration already run) | Dan's comment: The account table no longer enforces this at all (since I deleted the now defaults). That's entirely done in the app layer.

**Merged from:** PHANTOM-AC-1.25, PHANTOM-AC-1.26, PHANTOM-AC-2.1, PHANTOM-AC-2.11, PHANTOM-AC-2.12, PHANTOM-AC-2.15, PHANTOM-AC-3.1, PHANTOM-AC-4.16, PHANTOM-AC-4.21, PHANTOM-AC-4.7, PHANTOM-AC-001, CIRCULAR-ANCESTRY-REASONING, RECONSTITUTE-AC-2.10, TRACE-STALE-1 through TRACE-STALE-7, SA-AC-1.25-1.26

Dan's disposition: I followed all stale annotations recommendations other than those I added a comment to.

### Stale Annotations (Spec)

| ID | Severity | Location | Action |
|---|---|---|---|
| SS-1 | MEDIUM | `Conventions/README.md:15` | Remove AuditEnvelope claim from Temporal.md description, OR add AuditEnvelope section to Temporal.md |
| SS-2 | LOW | `AccountCrud.md`, Withdrawn table, REQ-AC-1.38 | Change "deferred" to "Superseded by REQ-AC-2.7 and REQ-AC-4.3" |
| SS-3 | LOW | `SystemWide.md:26` | Promote the bare `todo` to a real REQ- ID or move to backlog and delete comment |

Dan's disposition: BD owns the readme. I made the recommended change on SS-2. SS-3 is overruled. My code base and I use Rider's "todo" function.

### Missing Annotations (Code)

All **fix the code** -- add the missing REQ- annotation.

| ID | Severity | Location | Missing REQ |
|---|---|---|---|
| MA-1 | MEDIUM | `Account.fs:98` | REQ-AC-1.39 -- add explicit self-parent check OR change annotation to clarify probabilistic guarantee |
| MA-2 | LOW | `AccountComponent.fs:82` | REQ-SYS-1.1 (AccountType.fromString Trim) |
| MA-3 | LOW | `AccountComponent.fs:118` | REQ-SYS-1.1 (AccountSubtype.fromString Trim) |
| MA-4 | LOW | `AccountComponent.fs:155` | REQ-SYS-1.1 (AccountExternalReference.create Trim) |
| MA-5 | LOW | `AccountComponent.fs:157` | REQ-SYS-1.3 (alongside REQ-AC-1.49) |
| MA-6 | LOW | `AccountComponent.fs:35` | REQ-SYS-1.2 (alongside REQ-AC-1.1, REQ-AC-1.2) |
| MA-7 | LOW | `AccountComponent.fs:48` | REQ-SYS-1.2 (alongside REQ-AC-1.6, REQ-AC-1.7) |

Dan's disposition: MA-1 is hoseshit, but I changed code anyway as a middle finger. BD, find the agent who marked it as medium and go full "of mice and men" on it. I made the rest of the recommended changes

### Other

| ID | Severity | Location | Action |
|---|---|---|---|
| TYPO-1 | LOW | `Specs/Conventions/Money.md:27` | Change "substraction" to "subtraction" |

Dan's disposition: done
---

## Counts

### By Severity
| Severity | Count |
|---|---|
| High | 5 |
| Medium | 19 |
| Low | 18 |
| **Total** | **42 actions across 35 unique findings** |

Note: some findings (especially stale annotations) produce multiple atomic actions from a single finding.

### By Category
| Category | Count |
|---|---|
| Contradiction / Convention Violation | 5 |
| Ambiguity | 13 |
| Missing Requirement | 7 |
| Insufficient Elaboration | 4 |
| Stale Annotation (Code) | 11 |
| Stale Annotation (Spec) | 3 |
| Missing Annotation (Code) | 7 |
| Other (typo) | 1 |

---

## Items Requiring Dan's Decision

### DEC-1: Convention "Must" vs. Requirement "Must" (Meta-Decision)
- **Affects:** MR-3, MR-4, MR-5, MR-6, MR-7 (five findings)
- **Question:** When a convention file uses "must" language for a testable behavioral constraint, does it need a REQ- ID? The Conventions README says conventions are "not behavioral requirements verified by tests," but Traceability.md says "all requirements must be identified by an REQ label." Multiple convention files use imperative language that reads as requirements.
- **Options:** (A) Add a litmus test to Traceability.md or the README: "if it is testable and violation would be a defect, it needs a REQ- ID regardless of file location." Then mint IDs for all qualifying convention statements. (B) Clarify that convention "must" is enforced by review only, and qualify Traceability.md's "all requirements" language. Either way, five findings resolve downstream.
- **Dan's Disposition** I want to punt on this. The question is valid. But we need to decide how these "specs" get refactored. That's a bigger convo. and I recognize the traceability gap

### DEC-2: Parent-Child AccountType Matching (MR-1)
- **Question:** Should a child account be required to share its parent's AccountType? The seed data follows this convention, but the spec and code don't enforce it. Cross-type nesting would break tree-based balance aggregation.
- **Options:** (A) Add the requirement now. (B) Defer with a documented decision. If A, also decide whether AccountSubtype must match.
- **Dan's Disposition** I added the requirement. The code will come later.

### DEC-3: REQ-AC-1.39 Self-Parent Enforcement (MA-1)
- **Question:** Is probabilistic impossibility (UUID collision) an acceptable enforcement strategy for REQ-AC-1.39, or should the code add an explicit `if Some id = parentId then Error` check?
- **Options:** (A) Add the explicit check (one line, zero risk). (B) Accept probabilistic guarantee and update the annotation to say so. Option A is cheap insurance.
- **Dan's Disposition** go fuck yourself auditors

### DEC-4: Equity Subtypes (IE-4)
- **Question:** Should equity subtypes (OwnerContribution, RetainedEarnings, OwnerDistribution) be added now to future-proof for period close, or is this explicitly deferred?
- **Options:** (A) Add subtypes now. (B) Record as a deliberate deferral in Decisions.md.
- **Dan's Disposition** Nope

### DEC-5: AuditEnvelope Convention Location (SS-1)
- **Question:** The Conventions README claims Temporal.md covers AuditEnvelope, but it doesn't. Should AuditEnvelope be added to Temporal.md, or should the README be corrected?
- **Options:** (A) Write an AuditEnvelope section in Temporal.md (preferred -- the convention is real and undocumented). (B) Remove the claim from the README (quicker but leaves the convention undocumented).
- **Dan's Disposition** BD owns the readme
