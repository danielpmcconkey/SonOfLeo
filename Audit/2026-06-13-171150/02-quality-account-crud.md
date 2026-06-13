# AccountCrud Requirements Quality Auditor

**Findings: 7**


---

## AMB-AC-1.48
- **Category:** insufficient-elaboration
- **Severity:** high
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-1.48
- **Summary:** REQ-AC-1.48 defines "deactivated" but never defines "active," leaving the not-yet-started state unspecified.

REQ-AC-1.48 says: An Account is 'deactivated' when its active_end is non-null and <= the reference point. This defines one specific state, but the spec never positively defines what 'active' means. There are three runtime states: (1) activeBegin <= now AND (activeEnd is None OR activeEnd > now) -- intuitively active; (2) activeEnd is not null and <= now -- deactivated per REQ-AC-1.48; (3) activeBegin > now -- not yet started. State 3 is NOT 'deactivated' per the spec (active_end could be null), yet it's also not intuitively 'active.' The code's isActive function (Account.fs lines 39-48) treats state 3 as not-active by requiring activeBegin <= referencePoint, but this behavior has no spec backing. Multiple requirements reference 'active' status (REQ-AC-2.7, REQ-AC-4.3, REQ-AC-4.19) and all depend on a definition that doesn't exist.

**Suggested action:** Add a companion definition to REQ-AC-1.48 that positively defines 'active': an account is active when activeBegin <= referencePoint AND (activeEnd is None OR activeEnd > referencePoint). This pins down that not-yet-started accounts are not active.

**Why:** Without a positive definition of 'active,' two developers could reasonably disagree on whether a not-yet-started account is 'active.' One could treat it as active (since it's not deactivated), another as not-active (since it hasn't started). Every requirement that references 'active' inherits this ambiguity.


---

## AMB-AC-2.7
- **Category:** ambiguity
- **Severity:** medium
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-2.7
- **Summary:** REQ-AC-2.7 requires the parent account to be 'active' but does not specify the reference point, violating REQ-AC-1.48.1.

REQ-AC-1.48.1 explicitly states: 'Each requirement that references deactivation status must specify which reference point applies.' REQ-AC-2.7 says 'the system must confirm that the parent account is active' but never says whether the reference point is the system clock, the new child's active_begin, or the audit envelope timestamp. The code uses AuditEnvelope.instant (the system clock at operation time), but this choice is not spec-backed. Compare to REQ-AC-4.3, which correctly specifies 'reference as-of system run-time.'

**Suggested action:** Amend REQ-AC-2.7 to specify the reference point, e.g., 'the system must confirm that the parent account is active as-of system run-time' (matching the pattern in REQ-AC-4.3).

**Why:** REQ-AC-1.48.1 exists precisely because the reference point changes the outcome. A parent whose activeBegin is tomorrow would pass an 'active at child's activeBegin' check but fail an 'active at system clock' check. This is a self-imposed rule the spec is violating.


---

## AMB-AC-1.42-43
- **Category:** ambiguity
- **Severity:** medium
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-1.42 and REQ-AC-1.43
- **Summary:** REQ-AC-1.42 and REQ-AC-1.43 use 'date/time' instead of the defined term 'Instant' from Definitions.md.

Definitions.md carefully distinguishes 'Instant' (a singular globally agreed-upon point in time) from 'Date' (a calendar coordinate with no time component). REQ-AC-1.42 says accounts must 'represent a date/time signifying when that account began' and REQ-AC-1.43 says 'a date/time signifying when that account ceased being active.' The term 'date/time' is not defined in Definitions.md. The implementation uses NodaTime Instant, which is the correct choice, but the spec text is ambiguous about whether this should be an Instant or something else. Other requirements in the same file compound this: REQ-AC-1.46 says 'earlier or equal in time' (implying instant semantics), REQ-AC-1.48 says 'active end date' (using the defined term 'Date' which means something different), and REQ-AC-4.1 says 'active end date.'

**Suggested action:** Replace 'date/time' in REQ-AC-1.42 and REQ-AC-1.43 with 'Instant' to align with Definitions.md. Also replace 'active end date' with 'active end instant' in REQ-AC-1.48, REQ-AC-4.1, REQ-AC-4.2, and REQ-AC-4.6 for consistency.

**Why:** Definitions.md exists specifically because 'a term that does scope arithmetic must be pinned once' (Decisions.md, 2026-06-11). Using undefined or wrong terms in requirements undermines that discipline and could mislead a developer into using a Date type where an Instant is needed.


---

## STALE-AC-1.38
- **Category:** stale-annotation
- **Severity:** low
- **Location:** Specs/Behavioral/AccountCrud.md, Withdrawn table, REQ-AC-1.38
- **Summary:** The withdrawal reason for REQ-AC-1.38 says 'deferred to database create and update events' but the deferral has already been fulfilled.

REQ-AC-1.38's withdrawal reason says: 'Deemed too computationally expensive at every Account construction event. Deferred to database create and update events.' This implies a replacement requirement would be written for the create/update layer. In practice, the constraint is already covered by the combination of REQ-AC-2.7 (parent must be active on create) and REQ-AC-4.3 (parent can't deactivate while children are active). The word 'deferred' suggests outstanding work, but the work is done. The withdrawal reason should reflect that it was superseded, not deferred.

**Suggested action:** Update the withdrawal reason to: 'Superseded by REQ-AC-2.7 (parent must be active on create) and REQ-AC-4.3 (parent deactivation blocked by active children).'

**Why:** A future auditor or developer reading 'deferred' will look for the replacement requirement and waste time searching for something that already exists under different IDs.


---

## GAP-AC-1.47
- **Category:** enforcement-gap
- **Severity:** low
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-1.47
- **Summary:** REQ-AC-1.47 is active and testable but structurally unenforceable, with no spec-level acknowledgment of why.

REQ-AC-1.47 says an account's parent ID can never reference one of its descendant accounts, with the parenthetical '(This will be difficult to enforce.)' The code (Account.fs lines 321-329) consciously skips enforcement with a comment explaining why circular ancestry is structurally impossible given REQ-AC-2.13 (IDs are system-generated at insert) and REQ-AC-4.22 (parent ID is immutable). This reasoning is sound -- if a child's ID doesn't exist until insert, the parent can't already reference it, and since parent ID can't change afterward, no circular chain can form. But this structural argument lives only in a code comment, not in the spec. The requirement is not in the waived-from-testing table despite being, by the code's own analysis, vacuously true and unenforced.

**Suggested action:** Either (a) add REQ-AC-1.47 to the waived-from-testing table with the structural argument as the reason, or (b) add a spec-level note explaining that REQ-AC-1.47 is satisfied by the conjunction of REQ-AC-2.13 and REQ-AC-4.22, making explicit enforcement unnecessary.

**Why:** The two-state rule in the waived table header says 'every active requirement is either tested or in this table.' REQ-AC-1.47 is in neither category. A future auditor will flag it as missing a test, and the only defense is a code comment in a different file.


---

## AMB-AC-4.19
- **Category:** ambiguity
- **Severity:** low
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-4.19
- **Summary:** REQ-AC-4.19 references 'system run-time' for deactivation status but doesn't clarify whether the reference point is the AuditEnvelope instant or the literal wall clock.

REQ-AC-4.19 says: 'Updates to a deactivated Account record (with respect to system run-time) are permitted.' The parenthetical 'with respect to system run-time' specifies the reference point (good, per REQ-AC-1.48.1), but the term 'system run-time' could mean either the literal wall clock or the AuditEnvelope instant passed into the operation. In the current codebase, the AuditEnvelope instant IS the system clock (via Clock.fs), so they're identical. But the AuditEnvelope was introduced specifically to make timestamps injectable for testing (Decisions.md mentions IClock was rejected in favor of AuditEnvelope). If they diverge in a test scenario, which one governs?

**Suggested action:** Clarify whether 'system run-time' means 'the AuditEnvelope instant provided to the operation' or 'the wall clock at invocation.' The same ambiguity applies to REQ-AC-4.3 which also says 'system run-time.'

**Why:** This is low severity because in production the values are identical, but in test scenarios the distinction matters for verifying edge cases. Pinning the term now prevents confusion when tests are written.


---

## AMB-AC-4.6
- **Category:** ambiguity
- **Severity:** low
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-4.6
- **Summary:** REQ-AC-4.6 references 'entry date or post date' -- terms not yet defined in Definitions.md or any behavioral spec.

REQ-AC-4.6 says: 'the system must reject any request where the Account has any journal entry items dated (either entry date or post date) after the provided active end date.' The terms 'entry date' and 'post date' are journal-domain concepts that don't yet have definitions. While this requirement is explicitly deferred (todo comment in code), the spec text itself uses undefined terms. If journal entries end up defining these as Dates rather than Instants (which is plausible for accounting dates), the comparison against active_end (an Instant) would require type conversion rules not yet specified.

**Suggested action:** Add a note to REQ-AC-4.6 acknowledging that 'entry date' and 'post date' are forward references to the journal entry spec, and that the comparison semantics will be finalized when that spec is written.

**Why:** Definitions.md distinguishes Instant from Date for exactly this reason. If journal dates turn out to be calendar Dates, comparing them against an Instant field requires a timezone assumption (per Definitions.md's Date definition), and that assumption needs to be specified.
