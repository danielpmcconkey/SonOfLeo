# spec-quality-AccountCrud

## AC-TZ-1 — contradiction
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-1.48.1 (l.55), REQ-AC-2.7 (l.69), REQ-AC-3.9 (l.104), REQ-AC-4.1 (l.119), REQ-AC-4.3 (l.121) vs Specs/Behavioral/SystemWide.md REQ-SYS-7.1 (l.55), REQ-SYS-3.4 (l.30)
- **Summary:** AccountCrud hard-codes 'Eastern' / 'US Eastern Time' as the zone for the current date, but REQ-SYS-7.1 (revised 2026-09-28) says Instant-to-Date conversion uses one configured local time zone.
- **Resolution:** fix-spec
- **Prior ruling:** IE-AC-1 (2026-07-06) ruled on the instant source for REQ-AC-3.9 reads (AuditEnvelope vs Calendar.today). This finding is about the hard-coded Eastern zone against the 2026-09-28 revision of REQ-SYS-7.1, which is a different point.

Five AC requirements fix the reference date to 'the Eastern calendar date of the AuditEnvelope's system instant' (1.48.1, 2.7, 4.1, 4.3), or to 'a US Eastern Time interpretation of the calendar date associated to the system run time' (3.9). REQ-SYS-7.1 says 'Converting an Instant to a calendar Date uses one configured local time zone, system-wide.' The code follows SYS-7.1: Src/App.Utility/Clock.fs reads `LocalizedTimeZone` from config, and Calendar.dateFromInstant uses it. Every appsettings file sets it to America/New_York today, so the observed behavior is the same. But the two specs now name different authorities: AC says Eastern is required, SYS says the zone is whatever is configured. A test writer reading AC could fairly pin a test to America/New_York, and a configuration change that SYS-7.1 permits would then violate AC. The same requirements also use 'AuditEnvelope's system instant', which is a code-structure term (Specs/README.md: requirements are 'written in operator concepts ... never how the code is structured'). SYS-3.4 names the same value 'the initiation instant'. REQ-AC-3.9 says 'system run time', a third wording for that value. The code (Account.fetchAll) uses the initiation instant. Prior ruling IE-AC-1 is about which instant reads use. It does not cover the time-zone conflict raised here.

**Action:** Reword REQ-AC-1.48.1, 2.7, 3.9, 4.1 and 4.3 to 'the calendar date (per REQ-SYS-7.1) of the operation's initiation instant (REQ-SYS-3.4)', so they no longer name Eastern or AuditEnvelope.

**Why:** When two behavioral specs name different authorities for the same value, a test writer can lock in the stricter one, and a configuration change that SYS-7.1 allows would then break AC.

---

## AC-NOOP-1 — contradiction
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-4.8, REQ-AC-4.9 vs SystemWide.md REQ-SYS-6.1, REQ-SYS-6.1.1; compare CashFlow.md REQ-CF-14.2
- **Summary:** AccountCrud names no exception to REQ-SYS-6.1, so by REQ-SYS-6.1.1 renaming an Account to its current name (or clearing an already-null external reference) must fail, but the code lets it succeed and the only written rationale for allowing it is in CashFlow.md.
- **Resolution:** dan-decides

REQ-SYS-6.1.1 says: 'Any exception to REQ-SYS-6.1 ... must be stated explicitly in the relevant entity spec; absent such a statement, the no-op rejection applies.' REQ-AC-4.8 and 4.9 say nothing about re-sending a value the record already holds. In Src/Business.FinancialServices.Ledger/Account.fs, updateAccountNameById and updateExternalReferenceById always pass `SetTo` to `update`, which rejects only when every field is NoChange (AccountUpdateNoOp). So updating the name to its current value, or setting an already-null external reference to null, writes the row, bumps modified_at and succeeds. On 2026-10-03 Dan stated the opposite policy for CashFlow only (REQ-CF-14.2: 'a field set to the value it already holds counts as named', with a *Why* explaining that an unchanged value is not a no-op). Read literally, SYS-6.1.1 makes the Account code non-conformant. Under the CF rationale, AccountCrud is missing the explicit exception that SYS-6.1.1 requires. Two developers would build this differently: one rejects the same-value update, the other accepts it.

**Action:** Dan decides whether the CF-14.2 'unchanged value counts as named' policy applies to Account updates. If it does, add that exception explicitly to REQ-AC-4.8/4.9, or lift it into SystemWide §6. If it does not, the code must reject same-value updates.

**Why:** SYS-6.1.1 is written to fail closed: when an entity spec says nothing, the no-op rule applies. The Account update path currently relies on an exception that exists only in another domain's spec.

---

## AC-WD-1.38 — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md Withdrawn row REQ-AC-1.38 (l.203); REQ-AC-2.7, REQ-AC-2.23, REQ-AC-4.3
- **Summary:** REQ-AC-1.38 (an active account may not have an inactive parent) was withdrawn as 'Superseded by REQ-AC-2.7 and REQ-AC-4.3', but those two requirements, together with REQ-AC-2.23, still allow an active child under an inactive parent.
- **Resolution:** dan-decides

Both REQ-AC-2.7 and REQ-AC-4.3 judge activity only as of the operation's current date. That leaves concrete paths to the state 1.38 forbade. (a) REQ-AC-2.23 (added 2026-09-26) allows an active end at creation. Create parent P with active end 2026-12-31: P is active today. Then create child C under P with no active end: 2.7 passes because P is active today. From 2027-01-01, C is active and P is not. (b) REQ-AC-4.3 counts only children active today. A child whose active begin (unvalidated per REQ-AC-2.17) is a future date is 'not active' per REQ-AC-1.50 (confirmed in ActivityPeriod.isActive and the test 'REQ-AC-1.50 isActive returns false when ref precedes begin'). So the parent can be deactivated, and the child later becomes active under it. (c) REQ-AC-4.1 accepts a caller-supplied active end, yet REQ-AC-4.3 checks children as of today, not as of that end date. Deactivating a parent with a past end date fails only because the child is active today, and the reverse cases are not covered. The code matches the spec text in AccountCreation.confirmParentAccountIsActive and AccountDeactivation.confirmNoActiveChildrenBeforeDeactivation, so the gap comes from the withdrawal itself.

**Action:** Dan decides whether the invariant 1.38 protected is still wanted. If it is, restate it in 2.7/4.3 terms that compare activity periods (the child's window within the parent's) rather than activity as of today. If it is not, change the withdrawal reason to say the invariant was dropped, not superseded.

**Why:** A withdrawal reason that says 'superseded' tells future auditors and implementers that the protection still exists elsewhere. Here it only partly does.

---

## AC-4.5-2.23 — contradiction
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-4.5 (l.123) vs REQ-AC-2.23 (l.87), REQ-AC-4.1 (l.119), REQ-AC-1.50
- **Summary:** REQ-AC-4.5 assumes that a non-null active end means the account is already deactivated. Since REQ-AC-2.23 allows an active end at creation, an account that is active today with a scheduled end can never be deactivated.
- **Resolution:** dan-decides

REQ-AC-4.5 rejects deactivation 'where the Account already has a non-null "active end" date'. When 4.5 was written, the only way to set active end was deactivation, so a non-null end meant 'already deactivated'. REQ-AC-2.23 (2026-09-26) now lets the caller supply an active end at creation. An account created with active end 2027-06-30 is 'active' today under REQ-AC-1.50, yet REQ-AC-4.1's 'means to deactivate an Account' cannot be used on it. 4.5 rejects it, and REQ-AC-4.22 together with section 4 gives no other path to change active end. The code (AccountDeactivation.deactivateAccount) returns `AccountAlreadyInactive` for an account that is active by the spec's own definition. The spec does not say whether ending such an account early is meant to be impossible.

**Action:** Dan decides whether a scheduled active end may be moved earlier by deactivation. Then amend REQ-AC-4.5 to say so explicitly, for example 'rejects when active end is non-null and earlier than or equal to the current date' or 'rejects whenever active end is non-null, including scheduled ends'.

**Why:** Requirements written before a new capability existed can carry assumptions that the new capability breaks. Here 'already deactivated' quietly became 'has any end date'.

---

## AC-3.12.3-VOID — ambiguity
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-3.12.3 (l.110), REQ-AC-3.12.2
- **Summary:** REQ-AC-3.12.3 does not say what happens to an Account whose only lines are on voided entries when the unvoided-only flag is set, and it leaves the flag out of its list of journal-entry filters.
- **Resolution:** fix-spec

3.12.3 returns a line-less Account once 'provided no filter on journal entry properties (temporal, source, journal entry ID, amount, description) is applied'. The unvoided-only flag is not in that list. Read literally, an Account with no lines is still returned when the flag is set, and that is what the code does. Src/Business.CrossDomainOrchestration/AccountActivity.fs adds `and je.voided_at is null` to the WHERE clause of a LEFT JOIN, so rows with no line pass. The spec does not cover an Account that has lines, all of them on voided entries. In the code those rows fail the predicate, so the Account vanishes from the result entirely. An Account with zero lines appears; an Account whose lines were all voided does not. A reasonable developer could equally return that Account once without line detail, treating it as having no qualifying lines. The 3.12.3 tests cover only the listed filters.

**Action:** Amend REQ-AC-3.12.3 to state whether the unvoided-only flag counts as a journal-entry filter, and whether an Account whose lines are all excluded by a filter is returned once without line detail or omitted.

**Why:** The result set's membership changes depending on that reading. Report consumers such as period activity read this fetch, so they inherit whichever choice the implementer happened to make.

---

## AC-4.6-VOID — ambiguity
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-4.6 (l.124)
- **Summary:** REQ-AC-4.6 rejects deactivation when the Account 'is referenced by a journal entry line' dated after the active end, but does not say whether lines of voided entries count; the code ignores them.
- **Resolution:** fix-spec

In AccountDeactivation.confirmNoJournalEntriesAfterDeactivationDate, the query includes `and je.voided_at is null`, so a voided entry dated after the proposed end does not block deactivation. The spec's wording, 'referenced by a journal entry line', covers any line that exists. Voided lines still exist and still reference the account. REQ-AC-4.4 is computed on balance (GAAP: voided entries are excluded), but 4.6 is a reference test, not a calculation. Two developers would differ here: one counts every referencing line, the other only unvoided ones.

**Action:** Add 'unvoided' to REQ-AC-4.6 ('referenced by a line of an unvoided journal entry whose entry date is later ...'), or state that voided entries also block.

**Why:** Whether a voided entry blocks deactivation is behavior the operator can see. The spec should pin it down rather than leave it to whichever implementation exists.

---

## AC-2.14-STALE — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-2.14 (l.77)
- **Summary:** REQ-AC-2.14 is conditioned on 'if the calling system specifies that the record should be saved to the DB', but account creation has no such option.
- **Resolution:** fix-spec

The only create path, AccountCreation.constructNewAndPersist (reached from AccountRoutes create), always persists. Account.create is a pure constructor and is not a 'creation function' that a caller can tell to save or not. The condition implies a non-persisting create mode. A test writer could look for that mode, or write a test of the 'do not save' branch that nothing implements. The rest of the requirement (persist the validated record and return it with ID and created/modified timestamps) is accurate.

**Action:** Remove the 'if the calling system specifies that the record should be saved to the DB' clause from REQ-AC-2.14.

**Why:** Conditions that describe interfaces which no longer exist invite tests of phantom branches and make readers doubt whether the clause is enforced.

---

## AC-WD-STALE — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md Withdrawn rows REQ-AC-1.19.1 (l.193) and REQ-AC-2.3 (l.206)
- **Summary:** Two Withdrawn-table reasons are stale. The REQ-AC-1.19.1 reason still carries a finished to-do ('Five tests cite 1.19.1 — rename to cite 1.19 (BD)'), and the REQ-AC-2.3 reason says 'Moved to DAL-level requirement' though no DAL requirement covers case fidelity.
- **Resolution:** fix-spec

No file in Tests/ cites REQ-AC-1.19.1 any more (grep finds none, and Invariant 1 of check-traceability is clean), so the rename is done and the action item is history in an authoritative spec. 1.19.1 is also the only withdrawn AC ID with no body stub (every other withdrawn ID keeps a 'stricken' bullet). REQ-AC-2.3 (case-perfect string fidelity) gives no target ID, and DataAccessLayer.md has no case-fidelity requirement: §3 covers PostgreSQL, abstraction, UTF-8 (3.4), collation (3.5), business logic (3.6) and now() (3.7). The obligation is in fact carried by REQ-SYS-5.1 (perfect reconstitution), which is not cited. The protection still exists, but the cited source does not.

**Action:** Change the 1.19.1 reason to 'Verbatim duplicate of the second clause of REQ-AC-1.19' (drop the to-do) and add the body stub. Change the 2.3 reason to 'Superseded by REQ-SYS-5.1'.

**Why:** Withdrawal reasons are how later readers check that a removed protection survived. A reason that points to nothing makes that check fail.

---


