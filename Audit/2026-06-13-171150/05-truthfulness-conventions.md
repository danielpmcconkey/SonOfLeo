# convention-enforcement-auditor

**Findings: 10**


---

## TEMP-MIG-1
- **Category:** convention-violation
- **Severity:** high
- **Location:** /workspace/SonOfLeo/DbMigrations/2026-06-01-07-48-CreateAccountTable.sql, lines 45, 47, 48
- **Summary:** Three required temporal columns carry DEFAULT now(), violating two separate Temporal convention rules.

The Temporal convention states: (1) "The persistence layer may never be the originator of temporal values (no use of now() in any defaults, triggers, stored procedures, etc.)" and (2) "Required (non-nullable) temporal columns carry no defaults; a write that omits the value is rejected, never filled in by the database." The migration defines three columns with DEFAULT now(): active_begin (line 45), created_at (line 47), and modified_at (line 48). While the application layer does supply these values via parameterized inserts (so the defaults are unlikely to fire in practice), the defaults still exist as a fallback and violate the convention on two counts: the persistence layer would originate a temporal value, and a write that omits these values would silently succeed instead of being rejected.

**Suggested action:** Remove DEFAULT now() from the active_begin, created_at, and modified_at column definitions so they are NOT NULL with no default. This ensures a write that omits a temporal value fails at the database level.

**Why:** If any write path bypasses the application layer (manual SQL, future migration, ad-hoc fix), the database would silently fill in a timestamp that the application never audited. The convention exists precisely to prevent the persistence layer from being a silent source of truth for temporal data.


---

## MONEY-ROUND-1
- **Category:** convention-violation
- **Severity:** high
- **Location:** /workspace/SonOfLeo/Src/Model/Money.fs, line 20
- **Summary:** Money.fromDecimal calls Math.Round without specifying MidpointRounding.AwayFromZero, using .NET's default banker's rounding.

The Money convention states: "When rounding is required the system must employ a 'half-up' rules (e.g.: MidpointRounding.AwayFromZero). Note that .NET's Math.Round default is banker's rounding (half-to-even), so the system should always pass the rounding mode explicitly." On line 20, fromDecimal calls Math.Round(raw, 2) without the third argument. This uses the .NET default of MidpointRounding.ToEven (banker's rounding). By contrast, splitByN on line 48 correctly passes MidpointRounding.AwayFromZero. The fromDecimal function uses the rounded value only to check whether the input was already at penny precision (if rounded != raw, reject), so the practical impact is limited to edge cases where a value like 1.125M would round to 1.12 (banker's) rather than 1.13 (half-up) -- but the comparison would still reject it either way. However, the convention says "always pass the rounding mode explicitly" and this call does not.

**Suggested action:** Change line 20 from Math.Round(raw, 2) to Math.Round(raw, 2, MidpointRounding.AwayFromZero) to match the convention and the pattern already used in splitByN.

**Why:** Consistency in rounding mode prevents subtle bugs if the function's logic ever changes to use the rounded value for anything beyond a precision check. The convention exists because .NET's default is a known source of accounting bugs.


---

## NAMING-MONEY-1
- **Category:** convention-violation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Src/Model/Money.fs, line 19
- **Summary:** Money's public constructor is named fromDecimal instead of create, violating the Naming convention for wrapping constructors.

The Naming convention (Naming.md) states: "Does the type *wrap* the input? Use create (e.g., AccountName.create, AccountActivityPeriod.create)." Money is a single-case record that wraps a decimal, which is exactly the pattern the convention says should use 'create'. The public constructor is named 'fromDecimal' (line 19), while the private internal function is named 'create' (line 16). The convention's examples (AccountName.create, AccountActivityPeriod.create) all follow this pattern, and the codebase is otherwise consistent -- AccountCode.create, AccountName.create, AccountExternalReference.create all wrap their inputs and use 'create'. Money.fromDecimal is the only wrapping constructor that deviates.

**Suggested action:** Rename the public Money.fromDecimal to Money.create and rename the private create to something like 'wrap' or 'unsafeCreate' (or inline it since it's a one-liner).

**Why:** Naming conventions exist to reduce cognitive load. A developer who has internalized 'create means wrapping, fromString means DU parsing' will look for Money.create and not find it.


---

## TRACE-STALE-1
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/AccountComponent.fs, lines 33 and 46
- **Summary:** Code annotates withdrawn requirement REQ-AC-2.1 (superseded by REQ-SYS-1.1).

AccountComponent.fs lines 33 and 46 annotate string trimming with '// REQ-AC-2.1'. Per AccountCrud.md's Withdrawn table, REQ-AC-2.1 was withdrawn and superseded by REQ-SYS-1.1 (SystemWide.md). The annotations should reference the active requirement ID, REQ-SYS-1.1, not the withdrawn one. The Traceability convention says annotations let future developers know which requirements bear load -- stale IDs pointing to withdrawn requirements defeat that purpose.

**Suggested action:** Replace '// REQ-AC-2.1' with '// REQ-SYS-1.1' on lines 33 and 46 of AccountComponent.fs.

**Why:** A future audit looking for REQ-SYS-1.1 enforcement will miss these enforcement points. A developer looking up REQ-AC-2.1 will find it in the Withdrawn table and have to trace the supersession chain to understand what the code is doing.


---

## TRACE-STALE-2
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/Account.fs, lines 100-101
- **Summary:** Code annotates withdrawn requirements REQ-AC-1.25, REQ-AC-1.26, REQ-AC-2.11, and REQ-AC-2.12 (all superseded by REQ-SYS-3.2).

Account.fs line 100 annotates '// REQ-AC-1.25, REQ-AC-2.11' and line 101 annotates '// REQ-AC-1.26, REQ-AC-2.12'. All four of these requirement IDs appear in the AccountCrud.md Withdrawn table: REQ-AC-1.25 and REQ-AC-1.26 are 'Superseded by REQ-SYS-3.2', and REQ-AC-2.11 and REQ-AC-2.12 are 'Superseded by REQ-SYS-3.2'. The annotations should reference the active requirement REQ-SYS-3.2.

**Suggested action:** Replace annotations on lines 100-101 with '// REQ-SYS-3.2'.

**Why:** Same traceability concern as TRACE-STALE-1. Audits for REQ-SYS-3.2 compliance will not find these enforcement points.


---

## TRACE-STALE-3
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/Account.fs, lines 157-161
- **Summary:** Code annotates withdrawn requirement REQ-AC-3.1 (superseded by REQ-SYS-2.1).

The reconstitute function in Account.fs annotates five lines (157-161) with '// REQ-AC-3.1'. Per AccountCrud.md's Withdrawn table, REQ-AC-3.1 was withdrawn and superseded by REQ-SYS-2.1 ('Every operation that constructs, persists, or reconstitutes an entity ... must enforce that entity's legal data-state rules').

**Suggested action:** Replace '// REQ-AC-3.1' with '// REQ-SYS-2.1' on lines 157-161 of Account.fs.

**Why:** Audit for REQ-SYS-2.1 (legal data-state enforcement at reconstitution) will miss these enforcement points.


---

## TRACE-STALE-4
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/Account.fs, lines 227, 239
- **Summary:** Code annotates withdrawn requirement REQ-AC-2.15 (superseded by REQ-SYS-5.1).

The insertNewToDb function annotates the insert query with '// REQ-AC-2.15' on lines 227 and 239. Per AccountCrud.md's Withdrawn table, REQ-AC-2.15 was withdrawn and superseded by REQ-SYS-5.1 ('The persistence layer must persist all entity properties in such a way that the entity type can be perfectly reconstituted upon subsequent read').

**Suggested action:** Replace '// REQ-AC-2.15' with '// REQ-SYS-5.1' on lines 227 and 239 of Account.fs.

**Why:** Same traceability concern -- stale IDs break automated and manual audits.


---

## TRACE-STALE-5
- **Category:** stale-annotation
- **Severity:** medium
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/Account.fs, lines 371, 397
- **Summary:** Code annotates withdrawn requirement REQ-AC-4.7 (superseded by REQ-SYS-3.3).

The updateDb function annotates modified_at handling with '// REQ-AC-4.7' on lines 371 and 397. Per AccountCrud.md's Withdrawn table, REQ-AC-4.7 was withdrawn and superseded by REQ-SYS-3.3 ('Every successful update to a record must set its modified_at timestamp to the system clock at time of the update').

**Suggested action:** Replace '// REQ-AC-4.7' with '// REQ-SYS-3.3' on lines 371 and 397 of Account.fs.

**Why:** Audit for REQ-SYS-3.3 compliance at update time will not find this enforcement point.


---

## TRACE-STALE-6
- **Category:** stale-annotation
- **Severity:** low
- **Location:** /workspace/SonOfLeo/Src/Model/Ledger/Account.fs, lines 472, 483
- **Summary:** Code annotates withdrawn requirement REQ-AC-4.21 (duplicate of REQ-AC-4.18, both superseded by REQ-SYS-2.1).

The updateAccountName and updateExternalReference functions annotate validation with '// REQ-AC-4.21' on lines 472 and 483. Per AccountCrud.md's Withdrawn table, REQ-AC-4.21 is 'Duplicate of REQ-AC-4.18; both superseded by REQ-SYS-2.1'.

**Suggested action:** Replace '// REQ-AC-4.21' with '// REQ-SYS-2.1' on lines 472 and 483 of Account.fs.

**Why:** Same traceability concern. Low severity because the code behavior is correct regardless.


---

## TRACE-STALE-7
- **Category:** stale-annotation
- **Severity:** low
- **Location:** /workspace/SonOfLeo/DbMigrations/2026-06-01-07-48-CreateAccountTable.sql, lines 47-48
- **Summary:** Migration annotates withdrawn requirements REQ-AC-1.25 and REQ-AC-1.26 (superseded by REQ-SYS-3.2).

The migration's created_at column (line 47) annotates '-- REQ-AC-1.25' and modified_at (line 48) annotates '-- REQ-AC-1.26'. Both are withdrawn per AccountCrud.md and superseded by REQ-SYS-3.2. Note: this migration has already been executed in dev so the annotations are cosmetic at this point, but they would mislead anyone reading the migration history.

**Suggested action:** Update annotations to '-- REQ-SYS-3.2' in the migration file (cosmetic only since migration was already run).

**Why:** Low severity because migrations are run-once artifacts, but the stale annotations could mislead future schema reviewers.
