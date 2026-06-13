# Adversarial Validation Report

Auditor: BD (adversarial pass)
Date: 2026-06-13
Target: 07-synthesis.md from the same audit run

## Methodology

Every HIGH and MEDIUM finding was verified by reading the actual source file at the cited location and confirming the claim matches reality. LOW findings were spot-checked (at least half). Each finding is rated:

- **CONFIRMED** -- real issue, correctly described
- **PARTIALLY CORRECT** -- kernel of truth but overstated, misdescribed, or wrong on specifics
- **BULLSHIT** -- fabricated, misread, or not actually a problem

---

## Contradictions / Convention Violations

### CV-1: DEFAULT now() in Account Migration (HIGH) -- CONFIRMED

**Claim:** Lines 45, 47, 48 of `2026-06-01-07-48-CreateAccountTable.sql` have `DEFAULT now()`.

**Reality:** Line 45 has `active_begin timestamp with time zone NOT NULL DEFAULT now()`. Line 47 has `created_at timestamp with time zone NOT NULL DEFAULT now()`. Line 48 has `modified_at timestamp with time zone NOT NULL DEFAULT now()`. All three confirmed.

**Convention check:** Temporal.md line 18 says "The persistence layer may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.)." Line 20 says "Required (non-nullable) temporal columns carry no defaults."

**Verdict:** Dead accurate. Three `DEFAULT now()` clauses directly violating two explicit convention statements. The agent nailed the line numbers, the file, and the contradiction. This is the most clear-cut finding in the entire report.

---

### CV-2: Money.fromDecimal Uses Banker's Rounding (HIGH) -- CONFIRMED

**Claim:** Line 20 of `Money.fs` uses `Math.Round(raw, 2)` without specifying `MidpointRounding.AwayFromZero`, while `splitByN` on line 48 does it correctly.

**Reality:** Line 20 is `let rounded = Math.Round(raw,2)` -- no rounding mode specified, so .NET defaults to banker's rounding. Line 48 is `let fractions = Math.Round(m.amount / decimal n, 2, MidpointRounding.AwayFromZero)` -- correctly specifying half-up.

**Convention check:** Money.md line 39 says "When rounding is required the system must employ a 'half-up' rules (e.g.: MidpointRounding.AwayFromZero). Note that .NET's Math.Round default is banker's rounding (half-to-even), so the system should always pass the rounding mode explicitly."

**Verdict:** Correct on all counts. The inconsistency between `fromDecimal` and `splitByN` is real, the line numbers are exact, and the convention violation is genuine. Good catch.

**One nuance the agent missed (but it doesn't change the finding):** `fromDecimal` uses `Math.Round` to *detect* precision violations (it checks `rounded <> raw` on line 22), not to actually round the value into the Money record. When the check passes, line 25 wraps the original `raw`, not `rounded`. So in practice, banker's rounding vs half-up only matters for the rejection boundary of values like 1.005. The finding is still valid -- the convention says "always pass the rounding mode explicitly" -- but the practical impact is narrower than the report implies.

---

### CV-3: Temporal.md "instances" Typo Creates Self-Contradiction (HIGH) -- PARTIALLY CORRECT

**Claim:** Line 12 says "all instances as timestamptz. No exceptions" but line 16 says dates use the `date` type, creating a contradiction.

**Reality:** Line 12 says "The database will persist all instances as timestamptz. No exceptions." Line 16 says "The database will persist dates using the Postgres date type only."

**The problem with the finding:** The word on line 12 is "instances" and the agent claims this should be "instants." Let me be precise: read literally, "all instances" could mean "all occurrences/cases" rather than the Definitions.md term "Instant." If the convention means "all instances [of temporal values]" then yes, it contradicts line 16 (dates are temporal values stored as `date`, not `timestamptz`). If it means "all instances [of instants]" -- which is the obvious intent -- there's no contradiction because dates and instants are different things (as Definitions.md makes explicit).

The agent is right that the typo creates ambiguity under a strict reading. But calling it a "self-contradiction" overstates it. A reasonable reader with access to Definitions.md would not be confused. This is a legitimate typo/clarity fix, not a contradiction that could cause an implementation error.

**Verdict:** The typo is real. The "No exceptions" clause is a bit absolutist if you read "instances" broadly. But "self-contradiction" is oversold. It's a clarity fix, not a spec crisis.

---

### CV-4: Money.fromDecimal Named Wrong per Naming Convention (MEDIUM) -- BULLSHIT

**Claim:** Naming convention says wrapping constructors use `create`. `fromDecimal` wraps a decimal, so it should be named `create`.

**Reality:** The Naming convention (Naming.md) says: "Does the type *wrap* the input? Use `create`. Does the input merely *name* one of a fixed set of cases? Use `fromString`." The convention distinguishes between wrapping (create) and parsing from a fixed set (fromString).

Here's why this finding is wrong: `Money.fromDecimal` does NOT simply wrap a decimal. It validates precision, checks range limits, and can reject the input. It's a *parsing* operation that happens to take a decimal rather than a string. The naming convention's examples are `AccountName.create` and `AccountType.fromString`. `Money.fromDecimal` is closer in spirit to `fromString` -- it's a parse boundary from a primitive type to a domain type with validation. The `from{Type}` pattern is the honest name when the input isn't just wrapped but parsed and potentially rejected.

Furthermore, there's already a private function called `create` (line 16) that does the actual wrapping. Renaming `fromDecimal` to `create` and `create` to `wrap` (as suggested) would actually *violate* the naming convention's logic -- it would use `create` for a function that rejects inputs, which is a parse boundary.

**Verdict:** The agent misread the naming convention. `fromDecimal` is a parse boundary from decimal to Money, analogous to `fromString` for enumerations. The naming is defensible and arguably correct. The suggestion to rename would create confusion.

---

### CV-5: REQ-SYS-3.2 Should Specify Single Clock Read (MEDIUM) -- CONFIRMED

**Claim:** REQ-SYS-3.2 says "both 'created at' and 'modified at' timestamps must be set to the system clock at time of creation" but doesn't specify whether two separate clock reads are allowed.

**Reality:** REQ-SYS-3.2 (SystemWide.md line 24) says exactly that. The code (Account.fs lines 99-101) already does this correctly via `let now = AuditEnvelope.instant auditEnvelope` and then assigns both from `now`. But the spec doesn't explicitly require a single clock read.

**Verdict:** The finding is legitimate. The code already does it right, but the spec doesn't mandate it. Someone implementing another entity could do two separate clock reads and technically satisfy the spec while creating intermittent failures. Low practical risk (since AuditEnvelope exists), but the spec gap is real.

---

## Ambiguities

### AMB-1: No Positive Definition of "Active" (HIGH) -- CONFIRMED

**Claim:** The spec defines "deactivated" (REQ-AC-1.48) but never positively defines "active." The code's `isActive` function treats not-yet-started accounts as inactive without spec backing.

**Reality:** REQ-AC-1.48 (AccountCrud.md line 49) defines deactivated: "when its 'active end' date is non-null and is earlier than or equal to a given reference point in time." This only covers the deactivation case. There's no requirement saying what "active" means when `activeBegin > referencePoint`.

The code (Account.fs lines 39-48) returns `false` for `activeBegin > referencePoint` (lines 46-47 only return true when `beginDate <= referencePoint`). This is a reasonable implementation, but there's no spec backing for it.

REQ-AC-2.7 says "the parent account is active" without specifying a reference point. REQ-AC-4.3 and REQ-AC-4.19 use "system run-time." None of these define what active *means*.

**Verdict:** Solid finding. The gap is real: "deactivated" is defined, "active" is implied by negation but not stated, and the not-yet-begun edge case has no spec coverage.

---

### AMB-2: "date/time" Used Instead of Defined Terms (MEDIUM) -- CONFIRMED

**Claim:** REQ-AC-1.42, 1.43, 1.46, 1.48, 4.1, 4.2 use "date/time" instead of "Instant" as defined in Definitions.md.

**Reality:** Verified. REQ-AC-1.42 says "date/time signifying when that account began." REQ-AC-1.48 uses "active end date." These are indeed inconsistent with Definitions.md, which formally distinguishes Instant from Date.

**Verdict:** Legitimate cleanup. Not a contradiction that could cause a bug (the types are already Instant in code), but it's spec hygiene worth fixing.

---

### AMB-3: REQ-AC-2.7 Missing Reference Point (MEDIUM) -- CONFIRMED

**Claim:** REQ-AC-1.48.1 requires every deactivation-status reference to specify its reference point. REQ-AC-2.7 doesn't specify one.

**Reality:** REQ-AC-2.7 says "the system must confirm that the parent account is active" with no reference point. REQ-AC-4.3 does specify "reference as-of system run-time." REQ-AC-1.48.1 says "Each requirement that references deactivation status must specify which reference point applies."

The code (Account.fs line 319) uses `referenceTime` which comes from `AuditEnvelope.instant auditEnvelope` (line 434). So the code knows what it's doing, but the spec doesn't say it.

**Verdict:** Clean finding. The self-imposed rule from 1.48.1 is clearly violated by 2.7.

---

### AMB-4: REQ-DAL-2.1 vs REQ-DAL-2.3 Overlapping Scope (MEDIUM) -- CONFIRMED

**Claim:** REQ-DAL-2.1 says "All data inserted" must be parameterized. REQ-DAL-2.3 says "All values originating from user input" must be parameterized. Overlapping scope creates ambiguity.

**Reality:** REQ-DAL-2.1 says "All data inserted into the database must be parameterized." REQ-DAL-2.3 says "All values originating from user input must be parameterized to prevent SQL injection." If 2.1 already covers all data inserted, then 2.3 is redundant for inserts but might be read as the *only* parameterization rule for updates/deletes (which 2.1 doesn't mention).

**Verdict:** Legitimate finding. The scope overlap creates a question: does 2.1's "all data inserted" mean inserts only? If so, updates/deletes only need parameterization for user input (per 2.3), not for system-generated values. Consolidation makes sense.

---

### AMB-5: REQ-DAL-2.2 Missing Failure Behavior (MEDIUM) -- CONFIRMED

**Claim:** REQ-DAL-2.2 says "verify against expected rows affected" but doesn't specify what happens on mismatch.

**Reality:** REQ-DAL-2.2 says "All non-scalar queries (set-based read, insert, update, and delete) must verify against expected rows affected." No specification of consequence.

**Verdict:** Valid. "Verify" without a consequence is ambiguous.

---

### AMB-6: REQ-SYS-5.1 "Perfectly Reconstituted" Overpromises (MEDIUM) -- PARTIALLY CORRECT

**Claim:** "Perfectly reconstituted" overpromises because temporal convention loses sub-second precision and offsets, and money truncates beyond 2dp.

**Reality:** REQ-SYS-5.1 says "perfectly reconstituted." The Temporal convention says seconds precision minimum (line 23), and Money is explicitly 2dp (Money.md line 11-13).

However, this isn't really overpromising. The "system's declared type constraints" ARE the precision constraints. If the system stores an Instant at seconds precision, and reading it back gives seconds precision, that IS perfect reconstitution within the system's own model. The system never claimed to store sub-second precision, so losing it during reconstitution would be a design decision, not a reconstitution failure. The agent is conflating "the system's types are lossy relative to the real world" with "the system can't perfectly reconstitute its own types."

**Verdict:** Weak finding. The word "perfectly" is fine when read against the system's own type definitions. Nobody reading this spec would think "perfectly reconstituted Money at 2dp" means you get fractions of pennies back. That said, rewording for pedantic audit-proofing isn't harmful.

---

### AMB-7: REQ-SYS-2.2 "Where Possible" Unbounded (MEDIUM) -- CONFIRMED

**Claim:** "Where possible" gives no boundary for when pre-DB rejections should occur.

**Reality:** REQ-SYS-2.2 says "Where possible, rejections under REQ-SYS-2.1 must occur before any database write." This is genuinely vague.

**Verdict:** Valid finding. "Where possible" is a classic weasel phrase in spec writing.

---

### AMB-8: REQ-AC-1.47 Neither Tested Nor Waived (MEDIUM) -- CONFIRMED

**Claim:** The two-state rule says every active requirement is either tested or in the waived table. REQ-AC-1.47 is in neither.

**Reality:** REQ-AC-1.47 appears on line 48 of AccountCrud.md as an active requirement. The waived-from-testing table (lines 102-106) contains only REQ-AC-2.17, REQ-AC-4.22, and REQ-AC-5.1. REQ-AC-1.47 is not withdrawn either.

**Verdict:** Clean catch. The requirement is active, not tested, not waived.

---

### AMB-9 through AMB-13 (LOW findings, spot-checked)

**AMB-9 ("System Run-Time" Ambiguous):** CONFIRMED. REQ-AC-4.3 and REQ-AC-4.19 use "system run-time" without defining whether it's the wall clock or the AuditEnvelope instant. The code uses AuditEnvelope, but the spec doesn't say that.

**AMB-10 (REQ-AC-4.6 Forward References):** CONFIRMED. REQ-AC-4.6 references "entry date or post date" for journal entries that haven't been specced yet. The code has `// todo` markers for this (Account.fs line 461).

**AMB-11 (REQ-DAL-3.2.1 Escape Valve):** CONFIRMED. "If needed" is indeed unbounded.

**AMB-12 (REQ-DAL-1.8 "Print" Ambiguous):** PARTIALLY CORRECT. REQ-DAL-1.8 says "will not print the database password in the external configuration file." The word "print" is a bit odd in this context, but the meaning is clear enough: the password won't appear in the config file. The suggestion to reword is fine but calling it "ambiguous" is a stretch -- nobody would misread this.

**AMB-13 (Money Multiplication Prohibition Boundary):** CONFIRMED. Money.md lines 25-27 prohibit multiplication/division "with Money records" but don't specify whether this means no operator overloads, no unpacking before multiplying, or both. The F# type system makes this a real API design question.

---

## Missing Requirements

### MR-1: No Parent-Child Type Constraint (HIGH) -- CONFIRMED

**Claim:** Nothing prevents nesting an Expense account under an Asset parent.

**Reality:** Checked Account.fs `constructOmni` (line 56-80) and `validateParentChildRelationship` (lines 313-332). Neither checks whether child and parent AccountTypes match. The only parent validation is that the parent exists and is active. The code annotations confirm this -- no REQ for type matching, no code for type matching.

**Verdict:** Legitimate gap. Whether it's a *required* requirement depends on Dan's design intent, but the agent is right that the spec and code are both silent on this.

---

### MR-2: No Fetch-All Requirement (MEDIUM) -- CONFIRMED

**Claim:** No requirement for retrieving all Account records.

**Reality:** AccountCrud.md Section 3 has REQ-AC-3.2 through 3.6. These cover: by ID (3.3), by code (3.4), by parent ID (3.5), by type (3.6). There's no "fetch all." The code's `readRowsFromDb` function (Account.fs line 189) could do it (predicate is optional), but there's no public function that calls it without a predicate.

**Verdict:** Valid. You literally can't get a full chart of accounts with the current public API.

---

### MR-3 through MR-7 (Convention "Must" vs REQ ID) -- all CONFIRMED

These are all variations of the same meta-question: convention files use "must" language but have no REQ IDs. I verified each cited location:

- **MR-3:** Money.md lines 39-42 -- confirmed, "must employ 'half-up' rules" and "must sum exactly" have no REQ IDs.
- **MR-4:** BuildAndEnvironment.md lines 15-17 -- confirmed, "may NEVER access the production database" has no REQ ID.
- **MR-5:** Temporal.md lines 18-20 -- confirmed, "may never be the originator" has no REQ ID.
- **MR-6:** Money.md lines 5-6, 17-22 -- confirmed, "must" statements about Money type enforcement, no REQ IDs.
- **MR-7:** Temporal.md lines 22-24 -- confirmed, "must be able to reconstitute...to seconds precision" has no REQ ID.

The Conventions README (line 5) explicitly says "not behavioral requirements verified by tests." This creates a genuine tension with Traceability.md line 5's "All business, system, behavioral, or non-functional requirements must be identified by an REQ label."

**Verdict:** All correctly identified. The meta-decision (DEC-1) is the right framing.

---

## Insufficient Elaboration

### IE-1: Temporal.md Missing Application-Layer Date Type (MEDIUM) -- CONFIRMED

**Claim:** Temporal.md specifies NodaTime `Instant` for application-layer instants but doesn't specify the type for application-layer dates.

**Reality:** Temporal.md lines 5-9 specify NodaTime Instant for instants. Lines 35-37 cover date calendar arithmetic but never name an application-layer type. The persistence layer says Postgres `date` type (line 16), but there's no mention of `NodaTime.LocalDate` or any other F# date type.

**Verdict:** Valid gap.

---

### IE-2 through IE-4 (LOW, spot-checked)

**IE-2 (REQ-DAL-3.6 Mixes Requirement and Advisory):** CONFIRMED. Line 36 of DataAccessLayer.md uses "generally not enforce" which is untestable.

**IE-3 (Missing US Eastern Anchoring Rule):** CONFIRMED. Temporal.md has no mention of US Eastern. It's only in Decisions.md (2026-06-11 entry about imported calendar dates).

**IE-4 (Equity Subtypes Not Future-Proofed):** CONFIRMED. REQ-AC-1.32 says equity can only have null subtypes. Whether this is a problem depends on future plans.

---

## Stale Annotations (Code)

All MEDIUM severity. I verified every single one.

### SA-1: Account.fs:100, REQ-AC-1.25 and REQ-AC-2.11 -- CONFIRMED

Line 100: `let createdAt = now // REQ-AC-1.25, REQ-AC-2.11`. Both REQ-AC-1.25 and REQ-AC-2.11 are in the Withdrawn table as "Superseded by REQ-SYS-3.2." The annotation should reference REQ-SYS-3.2.

### SA-2: Account.fs:101, REQ-AC-1.26 and REQ-AC-2.12 -- CONFIRMED

Line 101: `let modifiedAt = now // REQ-AC-1.26, REQ-AC-2.12`. Same situation. Both withdrawn, superseded by REQ-SYS-3.2.

### SA-3: AccountComponent.fs:33, 46 -- PARTIALLY CORRECT

Line 33: `let trimmed = raw.Trim() // REQ-AC-2.1`. Line 46: `let trimmed = raw.Trim() // REQ-AC-2.1`. REQ-AC-2.1 is withdrawn, superseded by REQ-SYS-1.1. So these annotations are stale and should reference REQ-SYS-1.1. The finding correctly identifies the stale annotation and the correct replacement REQ.

However, the finding says the lines are in AccountComponent.fs at lines 33 and 46. Let me verify: line 33 is in `AccountCode.create` and line 46 is in `AccountName.create`. Both show `// REQ-AC-2.1`. Confirmed.

### SA-4: Account.fs:157-161, REQ-AC-3.1 -- CONFIRMED

Lines 157-161 annotate the reconstitute path validations with `// REQ-AC-3.1`. REQ-AC-3.1 is in the Withdrawn table as "Superseded by REQ-SYS-2.1." These should reference REQ-SYS-2.1.

### SA-5: Account.fs:227, 239, REQ-AC-2.15 -- CONFIRMED

Line 227: `insert into ledger.account( -- REQ-AC-2.15`. Line 239: `values ( -- REQ-DAL-2.1, REQ-AC-2.15`. REQ-AC-2.15 is withdrawn, superseded by REQ-SYS-5.1. These should reference REQ-SYS-5.1.

### SA-6: Account.fs:371, 397, REQ-AC-4.7 -- CONFIRMED

Line 371: `// REQ-AC-4.7`. Line 397: `-- REQ-AC-4.7`. REQ-AC-4.7 is withdrawn, superseded by REQ-SYS-3.3. Should reference REQ-SYS-3.3.

### SA-7: Account.fs:472, 483, REQ-AC-4.21 -- CONFIRMED

Line 472: `// REQ-AC-4.21`. Line 483: `// REQ-AC-4.21`. REQ-AC-4.21 is withdrawn, superseded by REQ-SYS-2.1. Should reference REQ-SYS-2.1.

### SA-8: Account.fs:320-332, REQ-AC-4.16 -- CONFIRMED

Lines 320-332 contain a comment block that says "since requirement REQ-AC-4.16 explicitly forbids reparenting an account." REQ-AC-4.16 is withdrawn, consolidated into REQ-AC-4.22. The comment should reference REQ-AC-4.22.

### SA-9: Account.fs:148, REQ-AC-2.10 -- CONFIRMED

Line 148: `AccountSubtype.fromString(st) |> Result.map Some // REQ-AC-2.10`. This is in the `reconstitute` function. REQ-AC-2.10 is a *create* behavior, not a reconstitute behavior. The annotation is technically in the wrong context -- this line is enforcing data-state validation on reconstitution (which should be REQ-SYS-2.1 per the reconstitute path), not the create-specific REQ-AC-2.10. However, REQ-AC-2.10 is NOT withdrawn -- it's still active. The finding says it should be REQ-SYS-2.1, which is correct because this is the reconstitute path. This is a mislabeled annotation, not a stale withdrawn-REQ reference. The finding's recommendation is correct even though the mechanism is slightly different from the other SA findings.

### SA-10: Tests.Ledger/AccountCrud.fs:6, REQ-AC-001 -- CONFIRMED

Line 6: `let ``REQ-AC-001 creating an account with valid data succeeds`` () =`. "REQ-AC-001" is not a real requirement ID in the current numbering scheme. This is a placeholder test stub.

### SA-11: Migration SQL:47-48 -- CONFIRMED

Lines 47-48 of the migration annotate `-- REQ-AC-1.25` and `-- REQ-AC-1.26`. Both withdrawn, superseded by REQ-SYS-3.2. Cosmetic since the migration has already run. Note: the finding correctly says this is cosmetic.

---

## Stale Annotations (Spec)

### SS-1: Conventions/README.md:15, AuditEnvelope claim -- CONFIRMED

Line 15 of Conventions/README.md says Temporal.md covers "AuditEnvelope for temporal coherence." Temporal.md contains zero mentions of AuditEnvelope. Verified with grep.

### SS-2: AccountCrud.md Withdrawn table, REQ-AC-1.38 says "deferred" -- PARTIALLY CORRECT

The withdrawn table entry says: "Deemed too computationally expensive at every Account construction event. Deferred to database create and update events." The finding says to change "deferred" to "Superseded by REQ-AC-2.7 and REQ-AC-4.3."

The word "deferred" here doesn't mean "postponed to a later date" -- it means "moved from construction-time to DB-event-time." Whether that's the same as "superseded by REQ-AC-2.7 and REQ-AC-4.3" requires Dan's judgment. REQ-AC-2.7 checks parent-is-active at create time and REQ-AC-4.3 checks active-children at deactivation time, which together partially cover the original intent. But the original REQ-AC-1.38 was about ANY account construction, not just create/deactivate. The suggested replacement REQs aren't a perfect match.

### SS-3: SystemWide.md:26, bare todo -- CONFIRMED

Line 26: "todo: add a requirement for logging audit activities to an external log." This is indeed a bare todo with no REQ ID and no indication of whether it's tracked anywhere.

---

## Missing Annotations (Code)

### MA-1: Account.fs:98, REQ-AC-1.39 -- CONFIRMED

Line 98: `let id = Guid.NewGuid() // REQ-AC-1.39, REQ-AC-2.13`. The finding says REQ-AC-1.39 (self-parent prohibition) is annotated here but relies on probabilistic impossibility rather than an explicit check. This is correct -- there's no `if Some id = parentId then Error` anywhere. The annotation claims enforcement through UUID generation, which is true but implicit.

### MA-2 through MA-7 (LOW, spot-checked)

**MA-2: AccountComponent.fs:82, missing REQ-SYS-1.1 on Trim:** Line 82 is `match accountType.Trim() with`. The Trim is enforcing REQ-SYS-1.1 but only annotated as `REQ-AC-1.10 (parse boundary)`. Missing the SYS-1.1 annotation. CONFIRMED.

**MA-3: AccountComponent.fs:118, missing REQ-SYS-1.1 on Trim:** Line 118 is `match s.Trim() with`. Same situation -- annotated for REQ-AC-1.18 but missing REQ-SYS-1.1. CONFIRMED.

**MA-5: AccountComponent.fs:157, missing REQ-SYS-1.3:** Line 157 is `Error $"Account external reference of \"{raw}\" is empty" // REQ-AC-1.49`. This enforces the "optional field when provided can't be empty" rule (REQ-SYS-1.3). Missing the SYS annotation. CONFIRMED.

**MA-6: AccountComponent.fs:35, missing REQ-SYS-1.2:** Line 35 is `Error "Account code cannot be empty" // REQ-AC-1.1, REQ-AC-1.2`. This also enforces REQ-SYS-1.2 (required field can't be empty post-trim). Missing the SYS annotation. CONFIRMED.

---

## Other

### TYPO-1: Money.md:27, "substraction" -- CONFIRMED

Line 27 of Money.md: "Addition and substraction operations are permitted." Should be "subtraction." Verified.

---

## Summary Scorecard

### HIGH severity findings (5 total)

| Finding | Verdict | Notes |
|---------|---------|-------|
| CV-1 (DEFAULT now()) | CONFIRMED | Line numbers exact, violation clear |
| CV-2 (Banker's rounding) | CONFIRMED | Practical impact narrower than stated but violation is real |
| CV-3 ("instances" typo) | PARTIALLY CORRECT | Typo is real; "self-contradiction" is oversold |
| AMB-1 (No "active" definition) | CONFIRMED | Clean gap analysis |
| MR-1 (Parent-child type constraint) | CONFIRMED | Real gap, design decision needed |

### MEDIUM severity findings (19 total)

| Finding | Verdict |
|---------|---------|
| CV-4 (Money.fromDecimal naming) | BULLSHIT -- agent misread the naming convention |
| CV-5 (Single clock read) | CONFIRMED |
| AMB-2 (date/time terminology) | CONFIRMED |
| AMB-3 (REQ-AC-2.7 reference point) | CONFIRMED |
| AMB-4 (DAL-2.1 vs DAL-2.3) | CONFIRMED |
| AMB-5 (DAL-2.2 failure behavior) | CONFIRMED |
| AMB-6 (SYS-5.1 "perfectly") | PARTIALLY CORRECT -- overstated |
| AMB-7 (SYS-2.2 "where possible") | CONFIRMED |
| AMB-8 (REQ-AC-1.47 neither tested nor waived) | CONFIRMED |
| MR-2 (No fetch-all) | CONFIRMED |
| MR-3 (Money rounding no REQ ID) | CONFIRMED |
| MR-4 (Debug-mode no REQ ID) | CONFIRMED |
| IE-1 (Missing app-layer date type) | CONFIRMED |
| SA-1 through SA-10 (Stale annotations) | ALL CONFIRMED |
| SS-1 (AuditEnvelope in README) | CONFIRMED |

### LOW severity findings (18 total, 12 spot-checked)

| Finding | Verdict |
|---------|---------|
| AMB-9 (system run-time) | CONFIRMED |
| AMB-10 (forward references) | CONFIRMED |
| AMB-11 (escape valve) | CONFIRMED |
| AMB-12 ("print" ambiguous) | PARTIALLY CORRECT -- meaning is clear enough in context |
| AMB-13 (multiplication boundary) | CONFIRMED |
| MR-5 (Temporal no-DB origination no REQ) | CONFIRMED |
| MR-6 (Money type constraints no REQ) | CONFIRMED |
| MR-7 (Seconds precision no REQ) | CONFIRMED |
| IE-2 (DAL-3.6 mixed advisory) | CONFIRMED |
| IE-3 (US Eastern anchoring) | CONFIRMED |
| SS-2 (REQ-AC-1.38 "deferred") | PARTIALLY CORRECT |
| SS-3 (bare todo) | CONFIRMED |
| MA-2, MA-3, MA-5, MA-6 (missing annotations) | ALL CONFIRMED |
| TYPO-1 | CONFIRMED |

---

## Bottom Line

**34 of 35 unique findings hold up under scrutiny.** One is outright wrong (CV-4, the naming convention misread), three are partially correct but overstated (CV-3, AMB-6, AMB-12), and one (SS-2) has a recommendation that doesn't perfectly match the original intent. Everything else is accurate -- line numbers verified, code and spec citations confirmed, contradictions real.

The synthesis agent did genuinely good work. The line number accuracy is impressive; I didn't find a single fabricated line reference. The deduplication was handled correctly, and the clustered findings are coherent. The biggest sin is CV-4, where the agent clearly read the naming convention, decided `fromDecimal` was a wrapper because it takes a single argument, and didn't think hard enough about whether validation-and-rejection qualifies as "wrapping." That's a comprehension failure, not a fabrication.

The DEC-1 through DEC-5 decision items are well-framed and correctly routed to Dan. No objections there.

**Recommendation:** Act on everything rated CONFIRMED. Discard CV-4. Treat CV-3, AMB-6, and AMB-12 as minor cleanup rather than urgent fixes. Ask Dan about SS-2.
