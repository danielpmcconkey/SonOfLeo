# annotation-auditor

**Findings: 24**


---

## PHANTOM-AC-1.25
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:100, DbMigrations/2026-06-01-07-48-CreateAccountTable.sql:47
- **Summary:** REQ-AC-1.25 is withdrawn but still annotated in code; should reference REQ-SYS-3.2.

Account.fs line 100 annotates `let createdAt = now // REQ-AC-1.25, REQ-AC-2.11`. The migration SQL line 47 annotates `created_at ... DEFAULT now(), -- REQ-AC-1.25`. REQ-AC-1.25 was withdrawn with reason "Superseded by REQ-SYS-3.2". The code annotation should be updated to REQ-SYS-3.2. Note: the migration DEFAULT now() has its own separate finding below.

**Suggested action:** Replace REQ-AC-1.25 with REQ-SYS-3.2 in Account.fs:100. Remove REQ-AC-1.25 from the migration line (and address the DEFAULT now() issue separately).

**Why:** Stale annotations to withdrawn requirements break traceability and make audits unreliable.


---

## PHANTOM-AC-1.26
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:101, DbMigrations/2026-06-01-07-48-CreateAccountTable.sql:48
- **Summary:** REQ-AC-1.26 is withdrawn but still annotated in code; should reference REQ-SYS-3.2.

Account.fs line 101 annotates `let modifiedAt = now // REQ-AC-1.26, REQ-AC-2.12`. The migration SQL line 48 annotates `modified_at ... DEFAULT now(), -- REQ-AC-1.26`. REQ-AC-1.26 was withdrawn with reason "Superseded by REQ-SYS-3.2". Both annotations should reference REQ-SYS-3.2 instead.

**Suggested action:** Replace REQ-AC-1.26 with REQ-SYS-3.2 in Account.fs:101. Remove REQ-AC-1.26 from the migration line (and address the DEFAULT now() issue separately).

**Why:** Stale annotations to withdrawn requirements break traceability and make audits unreliable.


---

## PHANTOM-AC-2.1
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/AccountComponent.fs:33, Src/Model/Ledger/AccountComponent.fs:46
- **Summary:** REQ-AC-2.1 is withdrawn but still annotated in code; should reference REQ-SYS-1.1.

AccountComponent.fs lines 33 and 46 annotate `raw.Trim() // REQ-AC-2.1` in AccountCode.create and AccountName.create respectively. REQ-AC-2.1 was withdrawn with reason "Superseded by REQ-SYS-1.1". The trimming behavior is correct but the annotation should cite the surviving requirement.

**Suggested action:** Replace REQ-AC-2.1 with REQ-SYS-1.1 in both AccountCode.create (line 33) and AccountName.create (line 46).

**Why:** Stale annotations to withdrawn requirements break traceability and make audits unreliable.


---

## PHANTOM-AC-2.11
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:100
- **Summary:** REQ-AC-2.11 is withdrawn but still annotated; should reference REQ-SYS-3.2.

Account.fs line 100 annotates `// REQ-AC-1.25, REQ-AC-2.11`. REQ-AC-2.11 was withdrawn with reason "Superseded by REQ-SYS-3.2". This is on the same line as the REQ-AC-1.25 phantom -- both should become REQ-SYS-3.2.

**Suggested action:** Replace both REQ-AC-1.25 and REQ-AC-2.11 on line 100 with a single REQ-SYS-3.2 annotation.

**Why:** Stale annotations to withdrawn requirements break traceability.


---

## PHANTOM-AC-2.12
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:101
- **Summary:** REQ-AC-2.12 is withdrawn but still annotated; should reference REQ-SYS-3.2.

Account.fs line 101 annotates `// REQ-AC-1.26, REQ-AC-2.12`. REQ-AC-2.12 was withdrawn with reason "Superseded by REQ-SYS-3.2". Same situation as the line above -- both should become REQ-SYS-3.2.

**Suggested action:** Replace both REQ-AC-1.26 and REQ-AC-2.12 on line 101 with a single REQ-SYS-3.2 annotation.

**Why:** Stale annotations to withdrawn requirements break traceability.


---

## PHANTOM-AC-2.15
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:227, Src/Model/Ledger/Account.fs:239
- **Summary:** REQ-AC-2.15 is withdrawn but still annotated; should reference REQ-SYS-5.1.

Account.fs lines 227 and 239 annotate the insert query with REQ-AC-2.15. REQ-AC-2.15 was withdrawn with reason "Superseded by REQ-SYS-5.1" (persistence fidelity). The insert query does persist all Account properties, so the annotation is substantively correct but should cite the surviving requirement.

**Suggested action:** Replace REQ-AC-2.15 with REQ-SYS-5.1 on both lines.

**Why:** Stale annotations to withdrawn requirements break traceability.


---

## PHANTOM-AC-3.1
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:157-161
- **Summary:** REQ-AC-3.1 is withdrawn but still annotated on 5 lines in reconstitute; should reference REQ-SYS-2.1.

Account.fs lines 157-161 annotate the reconstitute result CE bindings with `// REQ-AC-3.1`. REQ-AC-3.1 was withdrawn with reason "Superseded by REQ-SYS-2.1" (legal data-state enforcement on read-from-persistence). The code does correctly enforce validation on reconstitution, matching REQ-SYS-2.1.

**Suggested action:** Replace all REQ-AC-3.1 annotations on lines 157-161 with REQ-SYS-2.1.

**Why:** Stale annotations to withdrawn requirements break traceability.


---

## PHANTOM-AC-4.16
- **Category:** stale-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/Account.fs:324
- **Summary:** Comment block references withdrawn REQ-AC-4.16 to support a reasoning argument.

Account.fs line 324 contains a comment: "since requirement REQ-AC-4.16 explicitly forbids reparenting an account". REQ-AC-4.16 was withdrawn and consolidated into REQ-AC-4.22. The reasoning is still valid (REQ-AC-4.22 also forbids updating parent ID), but the comment cites a dead requirement ID.

**Suggested action:** Update the comment to reference REQ-AC-4.22 instead of REQ-AC-4.16.

**Why:** Comments referencing dead requirement IDs cause confusion during future audits and make it harder to trace the actual authority for a design decision.


---

## PHANTOM-AC-4.21
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:472, Src/Model/Ledger/Account.fs:483
- **Summary:** REQ-AC-4.21 is withdrawn but still annotated in updateAccountName and updateExternalReference; should reference REQ-SYS-2.1.

Account.fs lines 472 and 483 annotate validation calls with `// REQ-AC-4.21`. REQ-AC-4.21 was withdrawn as a "Duplicate of REQ-AC-4.18; both superseded by REQ-SYS-2.1". The code correctly enforces legal-data-state rules during update, which is what REQ-SYS-2.1 requires.

**Suggested action:** Replace REQ-AC-4.21 with REQ-SYS-2.1 on both lines.

**Why:** Stale annotations to withdrawn requirements break traceability.


---

## PHANTOM-AC-4.7
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:371, Src/Model/Ledger/Account.fs:397
- **Summary:** REQ-AC-4.7 is withdrawn but still annotated in updateDb; should reference REQ-SYS-3.3.

Account.fs lines 371 and 397 annotate the modified_at timestamp update with `// REQ-AC-4.7`. REQ-AC-4.7 was withdrawn with reason "Superseded by REQ-SYS-3.3" (update sets modified_at to system clock). The code behavior is correct.

**Suggested action:** Replace REQ-AC-4.7 with REQ-SYS-3.3 on both lines.

**Why:** Stale annotations to withdrawn requirements break traceability.


---

## PHANTOM-AC-001
- **Category:** incorrect-annotation
- **Severity:** medium
- **Location:** Tests/Tests.Ledger/AccountCrud.fs:6
- **Summary:** Test references nonexistent REQ-AC-001; appears to be a placeholder or typo.

The test file contains a single skipped test named `REQ-AC-001 creating an account with valid data succeeds`. No requirement with ID REQ-AC-001 exists anywhere in the spec. The naming convention uses dotted numbers (e.g., REQ-AC-1.1) not zero-padded integers. This looks like a placeholder test that was never updated with a real requirement ID.

**Suggested action:** Either delete this placeholder test or rename it to reference one or more real requirement IDs (e.g., REQ-AC-2.14 which covers successful account creation with persistence).

**Why:** A test claiming to verify a nonexistent requirement provides false traceability signal and wastes audit time.


---

## TEMPORAL-MIGRATION-NOW
- **Category:** contradiction
- **Severity:** high
- **Location:** DbMigrations/2026-06-01-07-48-CreateAccountTable.sql:45,47,48
- **Summary:** Migration uses DEFAULT now() on active_begin, created_at, and modified_at, violating the Temporal convention.

Specs/Conventions/Temporal.md states: "The persistence layer may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.)" and "Required (non-nullable) temporal columns carry no defaults; a write that omits the value is rejected, never filled in by the database." The migration defines three columns with DEFAULT now(): active_begin (line 45), created_at (line 47), and modified_at (line 48). The application code does always supply these values explicitly via parameterized inserts, so the DEFAULT is never actually exercised in the normal code path. However, the convention is absolute ("no use of now() in any defaults") and having the default present means a direct SQL insert omitting these columns would silently succeed rather than being rejected, which is exactly what the convention is designed to prevent.

**Suggested action:** Remove DEFAULT now() from all three columns (active_begin, created_at, modified_at) in the migration. Since these are NOT NULL without a default, any insert omitting them will correctly fail. The application code already supplies these values, so no runtime behavior changes.

**Why:** The Temporal convention (authority level 3) explicitly forbids database-originated temporal values. The DEFAULT now() clauses create a loophole where direct database inserts could bypass the application's temporal value origination, undermining the audit trail guarantee.


---

## CIRCULAR-ANCESTRY-REASONING
- **Category:** incorrect-annotation
- **Severity:** high
- **Location:** Src/Model/Ledger/Account.fs:320-332
- **Summary:** The comment block in validateParentChildRelationship claims circular ancestry checks are unnecessary, but the reasoning relies on withdrawn REQ-AC-4.16 and the actual surviving requirement (REQ-AC-4.22) may not be as absolute.

The comment on lines 320-332 argues: (1) child IDs are generated at DB insertion so a new child cannot already have descendants, and (2) REQ-AC-4.16 forbids reparenting. Therefore circular ancestry is impossible. The first point is correct for the CREATE path. The second point cites REQ-AC-4.16, which is withdrawn and consolidated into REQ-AC-4.22. REQ-AC-4.22 says "The system must not provide a user interface for updating any of the following immutable Account fields: ... parent ID." This is a UI-layer prohibition, not a domain-layer enforcement. The Account module's updateDb function currently has no SetTo case for parentId (it only accepts name, activeEnd, and reference updates), so the reasoning holds at the code level. However, the argument is fragile: it depends on no future developer adding a parentId update path. The function is annotated REQ-AC-2.16 but performs no actual circular ancestry check -- the annotation claims enforcement but the code is a no-op beyond what confirmAccountIsValidAndActive already checks. For the CREATE path, the reasoning is sound. The annotation REQ-AC-2.16 on this function is technically truthful since the requirement is satisfied by the impossibility argument, but the comment that explains this relies on a dead requirement ID.

**Suggested action:** Update the comment to cite REQ-AC-4.22 instead of REQ-AC-4.16. Consider adding a brief note that the structural guarantee also depends on updateDb not accepting a parentId parameter change.

**Why:** The reasoning is currently correct but cites a dead requirement. If a future developer reads the comment and cannot find REQ-AC-4.16, they may not understand the constraint that makes the circular ancestry check unnecessary. The structural guarantee should reference the living requirement.


---

## MISSING-TRIM-SUBTYPE
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:118
- **Summary:** AccountSubtype.fromString trims input but does not annotate REQ-SYS-1.1.

AccountComponent.fs line 118: `match s.Trim() with` in AccountSubtype.fromString performs trimming. AccountType.fromString (line 82) also trims via `accountType.Trim()` but likewise lacks the annotation. Both are enforcement points for REQ-SYS-1.1 (all raw string inputs trimmed at system boundary). AccountCode.create and AccountName.create annotate trimming (currently as REQ-AC-2.1, which should become REQ-SYS-1.1), but these two fromString functions do not.

**Suggested action:** Add `// REQ-SYS-1.1` annotation to the Trim() calls in AccountSubtype.fromString (line 118) and AccountType.fromString (line 82).

**Why:** The traceability convention requires annotation at every enforcement point. Missing annotations make it impossible for audits to confirm complete coverage of REQ-SYS-1.1.


---

## MISSING-TRIM-EXTREF
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:155
- **Summary:** AccountExternalReference.create trims input but does not annotate REQ-SYS-1.1.

AccountComponent.fs line 155: `let trimmed = raw.Trim()` in AccountExternalReference.create performs trimming but has no REQ-SYS-1.1 annotation. This is an enforcement point for the system-wide trimming requirement.

**Suggested action:** Add `// REQ-SYS-1.1` annotation to line 155.

**Why:** Consistent annotation at every enforcement point is required by the traceability convention.


---

## MISSING-ANNO-EXTREF-WHITESPACE
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:157
- **Summary:** AccountExternalReference.create rejects empty strings but does not annotate REQ-SYS-1.3.

AccountComponent.fs line 157 checks `if trimmed = String.Empty then Error ...` which enforces REQ-SYS-1.3 (optional text field, when provided, may never hold empty or whitespace-only value post-trim). The line annotates REQ-AC-1.49 (which covers the entity-specific rule) but not REQ-SYS-1.3 (the system-wide policy it implements).

**Suggested action:** Add REQ-SYS-1.3 to the annotation on line 157 alongside REQ-AC-1.49.

**Why:** REQ-SYS-1.3 applies system-wide and should be annotated at every enforcement point per the traceability convention.


---

## MISSING-ANNO-CODE-WHITESPACE
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:35
- **Summary:** AccountCode.create empty/whitespace check does not annotate REQ-SYS-1.2.

AccountComponent.fs line 35 checks `if String.IsNullOrWhiteSpace trimmed then Error "Account code cannot be empty"` and annotates REQ-AC-1.1 and REQ-AC-1.2. This also enforces REQ-SYS-1.2 (required text field may never hold empty or whitespace-only post-trim) but does not annotate it.

**Suggested action:** Add REQ-SYS-1.2 to the annotation on line 35.

**Why:** REQ-SYS-1.2 is the system-wide policy being enforced. Missing annotation means system-wide coverage audits cannot find this enforcement point.


---

## MISSING-ANNO-NAME-WHITESPACE
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:48
- **Summary:** AccountName.create empty/whitespace check does not annotate REQ-SYS-1.2.

AccountComponent.fs line 48 checks `if String.IsNullOrWhiteSpace trimmed then Error "Account name cannot be empty"` and annotates REQ-AC-1.6 and REQ-AC-1.7. This also enforces REQ-SYS-1.2 but does not annotate it.

**Suggested action:** Add REQ-SYS-1.2 to the annotation on line 48.

**Why:** Same as the AccountCode finding -- system-wide policy enforcement should be annotated.


---

## ISACTIVE-LOGIC-GAP
- **Category:** incorrect-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:42-48
- **Summary:** isActive function annotates REQ-AC-1.48 but its logic deviates from the spec's definition of 'deactivated'.

REQ-AC-1.48 states: An Account record is considered 'deactivated' (or 'inactive') when its 'active end' date is non-null and is earlier than or equal to a given reference point in time. The isActive function (lines 42-48) returns true when: (1) activeEnd is None AND beginDate <= referencePoint, or (2) activeEnd is Some x AND beginDate <= referencePoint AND x > referencePoint. The function also returns false when beginDate > referencePoint (the account hasn't started yet), which REQ-AC-1.48 does not address -- the requirement only defines 'deactivated' in terms of active_end relative to reference. This is arguably correct behavior (an account that hasn't begun shouldn't be active), but the annotation REQ-AC-1.48 doesn't cover the 'not yet begun' case. The logic is sound, but the annotation implies this function only enforces 1.48 when it actually also enforces an unstated rule about accounts not being active before their begin date.

**Suggested action:** Either add a requirement covering the 'not yet begun' case (account is not active when referencePoint is before activeBegin) or add a code comment explaining that this guard is implicit and not covered by REQ-AC-1.48.

**Why:** The isActive function makes a consequential determination (active vs. inactive) that gates deactivation checks (REQ-AC-4.3) and parent validation (REQ-AC-2.7). An unannotated behavior in this function is a hidden requirement that could be accidentally removed.


---

## MISSING-ANNO-SELF-PARENT
- **Category:** missing-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:98
- **Summary:** REQ-AC-1.39 is annotated on the Guid.NewGuid() line but the actual enforcement is structural, not explicit.

Account.fs line 98 annotates `let id = Guid.NewGuid() // REQ-AC-1.39, REQ-AC-2.13`. REQ-AC-1.39 says 'an account record's ID and parent ID cannot be the same (an account cannot be its own parent)'. The annotation claims that generating a new GUID satisfies this because the caller provides parentId and the new ID is freshly generated, making collision astronomically unlikely. This is a probabilistic guarantee, not an explicit check. While UUID4 collision is practically impossible, the annotation suggests explicit enforcement where none exists. The constructNew function never explicitly compares id to parentId.

**Suggested action:** Either add an explicit check `if Some id = parentId then Error ...` after generating the GUID (to truly enforce REQ-AC-1.39), or change the annotation comment to clarify it is satisfied by the impossibility of UUID collision rather than by explicit validation.

**Why:** REQ-AC-1.39 is a legal-data-state rule. Relying on probabilistic impossibility is defensible but the annotation implies active enforcement, which is misleading for future developers.


---

## MISSING-ANNO-ACTPERIOD
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:21-26
- **Summary:** AccountActivityPeriod.create does not annotate REQ-AC-1.44 (active begin may not be null) at its enforcement point.

The AccountActivityPeriod type's create function takes rawBegin as a non-optional Instant, which means null is structurally impossible (F# value types). This is an enforcement point for REQ-AC-1.44 but is only annotated on the type definition (line 14), not at the constructor. The type definition annotations (line 14) cover REQ-AC-1.42 and REQ-AC-1.44. This is adequate but could be more explicit at the function level.

**Suggested action:** No action strictly required -- the type-level annotation is sufficient since F#'s type system makes the enforcement structural. Optionally, add a comment to AccountActivityPeriod.create noting REQ-AC-1.44 is enforced by the non-optional parameter type.

**Why:** Minor -- type-level enforcement is arguably the strongest form of enforcement and the annotation is present on the type.


---

## RECONSTITUTE-AC-2.10
- **Category:** incorrect-annotation
- **Severity:** medium
- **Location:** Src/Model/Ledger/Account.fs:148
- **Summary:** reconstitute annotates REQ-AC-2.10 but that requirement is about 'creating an Account record via primitive types', not reconstitution.

Account.fs line 148 in the reconstitute function annotates `AccountSubtype.fromString(st) |> Result.map Some // REQ-AC-2.10`. REQ-AC-2.10 says: 'When creating an Account record via primitive types, the passed in string (post-trim, per REQ-SYS-1.1) for account sub-type must match one of the enumerated account sub-types exactly or the creation must fail.' The reconstitute function is a read-from-persistence path, not a creation path. The correct requirement for validating data on reconstitution is REQ-SYS-2.1 (every operation that reconstitutes an entity must enforce legal data-state rules). The same annotation on line 108 in constructNew is correctly placed.

**Suggested action:** Change the annotation on line 148 from REQ-AC-2.10 to REQ-SYS-2.1.

**Why:** REQ-AC-2.10 is scoped to the create path. Annotating it on the reconstitute path misrepresents which requirement governs read-from-persistence validation.


---

## MISSING-ANNO-TRIM-DAL
- **Category:** missing-annotation
- **Severity:** low
- **Location:** Src/Model/Ledger/AccountComponent.fs:82
- **Summary:** AccountType.fromString trims its input but does not annotate REQ-SYS-1.1.

AccountComponent.fs line 82: `match accountType.Trim() with` in AccountType.fromString performs trimming of the incoming string. This is an enforcement point for REQ-SYS-1.1 but lacks annotation. (Covered together with the AccountSubtype.fromString finding but listed separately for the distinct location.)

**Suggested action:** Add `// REQ-SYS-1.1` annotation to line 82.

**Why:** Consistent annotation at every enforcement point is required by the traceability convention.


---

## MISSING-REQ-NOT-BEGUN
- **Category:** missing-requirement
- **Severity:** medium
- **Location:** Specs/Behavioral/AccountCrud.md Section 1
- **Summary:** No requirement defines what 'active' means when the reference point is before the account's active_begin.

REQ-AC-1.48 defines 'deactivated' solely in terms of active_end being non-null and <= reference point. The isActive function in Account.fs also returns false when referencePoint < activeBegin (the account hasn't started yet). This 'not yet active' state has no corresponding requirement. The confirmAccountIsValidAndActive function (lines 298-311) does check for this case and produces an error message, but the behavior is not specified in any requirement.

**Suggested action:** Add a requirement (e.g., REQ-AC-1.50) stating that an Account record is not considered 'active' when the reference point precedes its active_begin date.

**Why:** The isActive function and confirmAccountIsValidAndActive both implement this logic, which gates parent validation during account creation (REQ-AC-2.7). An unannotated behavior at this level of consequence should be formally specified.
