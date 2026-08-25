# Resolved Audit Findings

Prior rulings from requirements audits. Agents must read this before flagging findings.
If a finding matches a resolved entry's scope, skip it — Dan already ruled on it.

## How to read this file

- **overruled**: Dan reviewed and explicitly rejected the finding. Do not re-flag.
- **deferred**: Dan acknowledged the gap but chose not to act yet. Do not re-flag unless
  the "revisit when" trigger has been met.

---

## CV-2: Money.fromDecimal Rounding Mode
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Money.fromDecimal using Math.Round for precision validation
- **Ruling:** The rounding in fromDecimal is a precision gate (reject values with more than 2dp), not an arithmetic rounding operation. The raw value is used when constructing the Money record. The explicit MidpointRounding.AwayFromZero was added to silence future auditors, but the behavioral concern was never real.

## CV-4: Money.fromDecimal Naming
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Money.fromDecimal naming vs create convention
- **Ruling:** fromDecimal is a deliberate exception — it's validating the boundard conversion, not a plain wrap, so the create-vs-from rule doesn't cleanly map.

## AMB-4: REQ-DAL-2.1 vs REQ-DAL-2.3 Overlapping Scope
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether DAL-2.1 and DAL-2.3 should be consolidated
- **Ruling:** These are two separate concepts. DAL-2.1 covers data inserted into the database (parameterized values). DAL-2.3 covers user-originated input specifically. The distinction exists to avoid requiring parameterization of structural query elements like LIMIT clauses in the flexible multipurpose read pattern (Account.fs readRowsFromDb).

## AMB-5: REQ-DAL-2.2 Missing Failure Behavior
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether REQ-DAL-2.2 needs explicit failure mode specification
- **Ruling:** "Verify" is self-explanatory in this codebase. Dozens of requirements say "must validate" without spelling out the failure mode. Not setting a precedent that forces a useless sentence on every validation requirement.

## AMB-6: REQ-SYS-5.1 "Perfectly Reconstituted" Overpromises
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether "perfectly reconstituted" is too strong given precision constraints
- **Ruling:** Any application of the system that writes a value to the database must do so such that any subsequent read returns a byte-perfect entity. The temporal precision and money 2dp constraints are deliberately planned for and coded against — they aren't edge cases, they're the design. "Perfectly" is accurate.

## AMB-11: REQ-DAL-3.2.1 Escape Valve Unbounded
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether the non-ANSI SQL exception needs tighter guidance
- **Ruling:** The system architecture requires client modules to pass query strings and parameters into DAL functions. They must have the discretion to determine when Postgres-specific SQL is appropriate. No useful wording exists to constrain this further.

## AMB-13: Money Multiplication Prohibition Boundary
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether Money.md needs to clarify operator vs behavioral prohibition
- **Ruling:** The code already doesn't define * and / operators on Money. The convention says "can't do it." There's nothing more to do here.


## IE-4: Equity Subtypes Not Future-Proofed
- **Status:** deferred
- **Date:** 2026-06-13
- **Revisit when:** GAAP closing entries (retained-earnings sweep) are designed
- **Ruling:** The subtype isn't the only or obvious way to identify retained earnings. Could use code, name, or a flag. Speculating on the mechanism before knowing what period closure needs just cements a guess.

## DEC-1: Convention "Must" vs Requirement "Must"
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether convention "must" and requirement "must" need formal disambiguation
- **Ruling:** De facto resolved. Convention docs hold prose guidance and design rationale. Behavioral specs hold REQ-labeled testable requirements. When a convention encodes a testable rule, it gets extracted to a REQ ID in the behavioral spec (Money.md established this pattern). The two "musts" serve different purposes and do not conflict.

## IE-2: REQ-DAL-3.6 Mixes Requirement and DBA Advisory
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQs can contain non-assertable language
- **Ruling:** As long as the language doesn't create ambiguity or encourage test writers to write bullshit tests, additional elaboration is fine. 

## SS-3: SystemWide.md todo Comment
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether `todo` comments must be in reference to an existing REQ
- **Ruling:** Dan uses Rider's todo function as either 1. a "note to self" to remind him what to implement next; or 2. a note to the LLMs that I have intentionally not yet implemented something that would otherwise belong in that section. To-do remarks are always intentionally placed and should not be evaluated in an audit as any sort of stand-alone directive. 

## DEC-3: REQ-AC-1.39 Self-Parent Enforcement
- **Status:** overruled
- **Date:** 2026-06-13
- **Scope:** Whether an explicit self-parent check is needed beyond UUID generation
- **Ruling:** Dan added the explicit check anyway (as a middle finger to the auditors), but the probabilistic argument was never a real concern. The finding was horseshit at medium severity.

## IE-AC-1: REQ-AC-3.9 Instant Source for Reads
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether read-time "current date" must source from AuditEnvelope
- **Ruling:** AuditEnvelope is for mutations with audit timestamps. Reads use Calendar.today() (Clock.now() through US Eastern Time). The mechanism differs from mutation-path checks by design, not by accident. Different operations using different instant sources is not a contradiction.

## AMB-AC-2: REQ-AC-4.4 Balance Reference Date
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether "non-zero balance" in REQ-AC-4.4 is ambiguous about the date range
- **Ruling:** "Balance" is standard GAAP terminology meaning the cumulative net of all posted (non-voided) entries since inception. It does not require a date qualifier. "At the time of the request" modifies when the check occurs, not what is summed. Standard accounting terms should not be flagged as ambiguous.

## SYS-CLK-1: REQ-SYS-3.3 "System Clock" vs AuditEnvelope Wording
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-SYS-3.3's "system clock" contradicts the AuditEnvelope decision
- **Ruling:** The AuditEnvelope's system instant IS the system clock captured at request time. "System clock at time of the update" and "AuditEnvelope system instant property" describe the same value from different angles. The distinction is pedantic — all reasonable interpretations land within a second of each other, which is fine for an audit timestamp.

## AMB-DAL-01: REQ-DAL-1.16 Connection String Detection Criteria
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-DAL-1.16 must define the heuristic for detecting a pasted connection string
- **Ruling:** The requirement defines the WHAT: reject a value that contains an actual connection string. The HOW — the detection heuristic — is an implementation choice, not a spec obligation. Requirements are not implementation guides. The implementer chose a reasonable heuristic; the spec is not deficient for not prescribing it.

## CON-DAL-02: REQ-DAL-2.2 Rows Affected vs Flexible Read
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-DAL-2.2 conflicts with the flexible multipurpose read pattern
- **Ruling:** The implementation satisfies 2.2 for reads via AcceptableExpectedRows.AnyQuantityIsAcceptable — the caller declares its expectation, and the system validates against that declaration. "Verify against expected rows" does not mean "assert a specific count"; it means the caller must state what it expects. "Any quantity" is a valid expectation. No conflict exists.

## AMB-JE-1: REQ-JE-1.11 Vacuous Guard
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-JE-1.11 is redundant with REQ-JE-2.5 and whether "assigned to" implies a missing FK
- **Ruling:** The period IS persisted — fiscal_period_id is a real FK on journal_entry. "Assigned to" is accurate. The requirement states the invariant the derivation must uphold: the entry date falls within the derived period's date range. That the derivation guarantees it by construction today does not make the invariant vacuous — it is the contract the derivation is built to satisfy. The auditor's premises were wrong on both counts.

## GAP-JE-2: External References Missing Audit Timestamps
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether external references need domain-specific timestamp REQs beyond REQ-SYS-3.1/3.3
- **Ruling:** REQ-SYS-3.1 and 3.3 are system-wide requirements that apply to all persisted entities. They do not need to be restated in every domain spec. The schema has created_at and modified_at. The code implements them. The spec coverage is REQ-SYS-3.1.

## AMB-JE-3a: REQ-JE-4.9 Target Reference Identification
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-JE-4.9 must specify how the target reference is identified
- **Ruling:** The external reference has a UUID primary key (REQ-JE-1.40). The value field is an intentionally unstructured string (REQ-JE-1.44, 1.45). The identifying key is obvious — the entity's own UUID. The spec does not need to spell out that you identify a record by its primary key.

## MON-2: Sum Intermediate Overflow
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-MON-2.9/2.9.1 is ambiguous about intermediate overflow during summation
- **Ruling:** The auditor assumed summation was implemented as a fold over the add function (which validates intermediates). It isn't — the implementation uses List.sumBy on the decimal projection and validates once via fromDecimal. The ambiguity exists only in the invented implementation. Auditors must verify implementation details against the code or confine findings to the spec text as written.

## MON-3: Split Count N Type and Integrality
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether REQ-MON-2.4's split count N needs its type and integrality specified
- **Ruling:** "Split N ways" means N is a positive integer. This is not ambiguous. The sub-requirements (reject 0, reject 1, reject negative) make it obvious. Do not flag domain-obvious semantics as under-elaborated.

## GAAP-CLOSE: Period Close vs GAAP Closing Entries
- **Status:** deferred
- **Date:** 2026-08-02
- **Revisit when:** Dan schedules the closing-entries slice
- **Ruling:** FP closing (`is_open` toggle) is a posting lock only. GAAP closing entries (annual retained-earnings sweep) are a planned, unscheduled enhancement — not abandoned, not incomplete. The design session happens when Dan schedules that slice. Do not flag closing as incomplete.

## CLAUDE-MD: No Repo-Level Agent Entry Point
- **Status:** overruled
- **Date:** 2026-08-02
- **Scope:** Whether a CLAUDE.md (or equivalent) should exist at the SonOfLeo repo root
- **Ruling:** Vetoed repeatedly. Agents enter via wakeups and prompts, not a repo-root file. The harness launches from its own root (`~/penthouse-pete/` for Hobson, `~/` for BD); a CLAUDE.md at the SonOfLeo repo root would never load. The imagined problem (agents lacking context) doesn't exist — the wakeup protocol provides it. Do not re-flag.

## JE-COMPOSITE-ORDER: Composite Validation Runs After Component Writes
- **Status:** overruled
- **Date:** 2026-08-03
- **Scope:** Whether JE composite checks (min 2 lines, debit=credit balance) must run before any DB write per REQ-SYS-2.1.1
- **Ruling:** It is impossible to validate that we have 2+ *valid* lines until each line has been constructed and persisted — a line's validity depends on DB state (account existence, period state). Pre-write validation would mean either (a) validating twice, or (b) validating against unvalidated input, both of which are worse than the current design. The transaction bracket ensures atomicity; a failed composite check rolls back everything. REQ-SYS-2.1.1 ("determinable from the entity's own properties") does not apply because line validity is not determinable from properties alone. Vetoed repeatedly. Do not re-flag.

## DAL-EFFICACY: DAL Test Efficacy Not Applicable
- **Status:** overruled
- **Date:** 2026-08-20 (verbiage corrected 2026-08-22)
- **Scope:** Whether the DAL needs a test-efficacy audit pass
- **Ruling:** The DAL has a behavioral spec (DataAccessLayer.md, 19 active REQ-DAL requirements), but all 19 are either waived from testing or classified as unenforceable. Its correctness is validated transitively through the domain tests that exercise it (every account/JE/staging operation hits the DAL). A test-efficacy auditor scoped to the DAL will always return "no findings" because there are no tested REQ IDs to audit against. This is by design, not a gap. Do not flag the absence of DAL-specific efficacy findings. This ruling covers test-efficacy only — it does not suppress spec-quality, ambiguity, or contradiction audits against DataAccessLayer.md.

## IDIOM-JE-1: REQ-JE-3.6.1 Net Balance Sign Test
- **Status:** overruled
- **Date:** 2026-08-20
- **Scope:** Whether the REQ-JE-3.6.1 net-balance test's `> zero` assertion is a cowardly inequality
- **Ruling:** The rest of the 3.6 suite asserts correct amounts; this specific test is about sign direction (net balance is positive when debits exceed credits). The `> zero` assertion is appropriate for a sign-direction test. Do not re-flag.

## NGUI-AQ-1: CLI stderr Assert.Contains vs Assert.Equal
- **Status:** overruled
- **Date:** 2026-08-20
- **Scope:** Whether SonOfLeoCli's stderr test should use Assert.Equal instead of Assert.Contains
- **Ruling:** The stderr output includes a trailing line break. The Reports CLI handles this by appending a newline to the expected message and using Assert.Equal; the main CLI uses Assert.Contains to check the error message is present within the response. Both approaches are equally safe — the Contains check verifies the entire expected error message is present. Changing either to match the other adds no safety. Do not re-flag.

## DB-STAGE-1: Staging Entities Missing created_at/modified_at
- **Status:** overruled
- **Date:** 2026-08-20
- **Scope:** Whether ingestion.staged_entry and ingestion.staged_entry_line need REQ-SYS-3.1 timestamp columns
- **Ruling:** Staged entries and staged lines are not entities per Definitions.md. They are transient pipeline artifacts with a full audit trail (ingestion.staged_entry_audit) that records every status transition with timestamps. Entity-level policies like REQ-SYS-3.1 do not apply. Do not re-flag.

## WAIVE-1: REQ-NGUI-3.1-3.5 Waiver Reason Soundness
- **Status:** overruled
- **Date:** 2026-07-06
- **Scope:** Whether the "too broadly scoped" waiver reason is sound for REQ-NGUI-3.1-3.5
- **Ruling:** "Too broadly scoped" means the requirement describes something so general that no single test can specifically satisfy it. These requirements describe structural input-format truths (first arg is domain, second is verb, payload via stdin) that are exercised implicitly by every CLI test but cannot be meaningfully isolated into a dedicated test. The waiver reason is appropriate. Do not re-flag waiver reasons without understanding Dan's usage of the phrase.

## MAINT-TZ-1: Clock.fs / Calendar.fs Duplicate Time Zone Binding
- **Status:** overruled
- **Date:** 2026-08-25
- **Scope:** Whether the localized time zone binding duplicated between Clock.fs and Calendar.fs is a maintainability defect
- **Ruling:** Deliberate design. Clock and Calendar are independent modules with distinct responsibilities — Clock provides instants, Calendar provides local dates. Each reads the configured time zone independently. This is not accidental duplication; it is intentional module independence. Coupling them to share a single binding would create a dependency between modules that currently have none. Overruled repeatedly across multiple audit rounds (2026-07-06a, 2026-08-22a, 2026-08-24a). Do not re-flag.
