# spec-quality-AccountCrud

## AC-ACT-1 — contradiction
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-1.48 vs REQ-AC-1.50, REQ-AC-4.5, REQ-AC-4.19; Specs/Behavioral/Reporting.md REQ-RPT-5.4; Specs/README.md (ID-grammar example)
- **Summary:** REQ-AC-1.48 defines "not active" as active end non-null and earlier than the reference date, which is not the complement of REQ-AC-1.50's "active", and other specs use 1.48 for a different word, "deactivated", with more than one meaning.
- **Resolution:** fix-spec

REQ-AC-1.50: an account is active on date D when begin <= D AND (end is null OR end >= D). REQ-AC-1.48: an account is "considered not active" on D when end is non-null AND end < D. Take an account whose active begin is after D and whose end is null. Under 1.50 it is not active. Under 1.48 it is not "not active" either. The two definitions therefore contradict each other for any account whose begin is in the future. The code follows 1.50 (Src/Business.General/ActivityPeriod.fs isActive returns false when begin > reference). The test `REQ-AC-1.48 isActive returns false when end < ref (deactivated)` treats 1.48 as the "deactivated" case. REQ-RPT-5.4 cites "deactivated (REQ-AC-1.48)", and the Specs/README.md grammar example paraphrases REQ-AC-1.48 as "An Account is 'deactivated' when its active-end date is non-null and earlier than a given reference date". AccountCrud.md never uses the word "deactivated" in 1.48. It does use it, undefined, in REQ-AC-4.19 ("Updates to a deactivated Account record are permitted"). REQ-AC-4.5 treats any non-null active end as already ended, including a future one, and the code's error for that case is AccountAlreadyInactive. So "inactive" or "deactivated" has three readings: (a) the complement of 1.50, (b) end < reference date (1.48, RPT-5.4), and (c) end is non-null (4.5). A spec writer who cites 1.48 for "not active", as REQ-AC-1.48.1 asks every activity reference to do, would treat a future-begin account as active. Every check that cites REQ-AC-1.50 would reject it.

**Action:** Reword REQ-AC-1.48 to define "deactivated" (active end non-null and earlier than the reference date), the term RPT-5.4, the README and the tests already use, and state that "not active" means not active under REQ-AC-1.50. Alternatively, rename 1.48's term and update RPT-5.4 and the README to match.

**Why:** Two requirements in the same section give opposite answers for a future-begin account. Downstream specs cite 1.48 under a different name than the one it defines, so the next spec to use "inactive" has to guess which of the three meanings applies.

---

## AC-ACT-2 — contradiction
- **Location:** Specs/Behavioral/AccountCrud.md REQ-AC-1.48.1 vs Specs/Behavioral/CashFlow.md REQ-CF-8.6
- **Summary:** REQ-CF-8.6 relies on account activity status ("a managed cash account is an active account whose subtype is 'Cash'") but names no reference date, which REQ-AC-1.48.1 requires.
- **Resolution:** fix-spec

REQ-AC-1.48.1: "Each requirement that references activity status must specify which reference point applies." REQ-CF-8.6 reads "A managed cash account is an active account whose subtype is 'Cash'." It names no reference point. The projection runs over a horizon of 1 to 365 days (REQ-CF-8.1), so two reasonable readings lead to different output. Under the first, the account is active on the current date. Under the second, it is active on any day of the horizon. A Cash account whose begin date falls next week would be excluded under the first reading and included under the second, and an account that ends mid-horizon could be treated differently too. The code picked the current date (Src/Business.CrossDomainOrchestration/CashFlowOps.fs:723 `Account.fetchAll context true`, which per Account.fs:237 uses the initiation instant's calendar date). The spec does not say so. Every other activity reference checked does name its reference point: REQ-JE-2.8 (entry date), REQ-AC-2.7, 3.9 and 4.3 (current date), and REQ-RPT-5.4 (current date). REQ-RPT-1.2 needs none because it says "regardless".

**Action:** Amend REQ-CF-8.6 to name the reference point, e.g. "active (REQ-AC-1.50) as of the current date (the calendar date of the operation's initiation instant, REQ-SYS-3.4)".

**Why:** REQ-AC-1.48.1 is the only control on this policy because it is unenforceable and binds spec writers. A spec that breaks it is the exact failure the rule exists to catch, and here the readings change which accounts appear in the projection.

---

## AC-STALE-1 — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md preamble (line 3) vs Specs/Behavioral/SystemWide.md section 4
- **Summary:** The AccountCrud preamble says the deletion policy lives in SystemWide.md, but SystemWide.md says there is no system-wide deletion policy and points back to REQ-AC-5.1.
- **Resolution:** fix-spec

AccountCrud.md line 3: "Cross-cutting policies (string trimming, data-state enforcement at every operation, audit timestamps, deletion) live in SystemWide.md and apply to everything below." SystemWide.md section 4: "No system-wide deletion policy. Whether an entity's records may be hard-deleted is a domain-level decision, made in each entity's spec (for Accounts, see REQ-AC-5.1)." REQ-SYS-4.1 is withdrawn: "Deletion policy is per-entity, not system-wide... Account's prohibition restored to REQ-AC-5.1." The preamble predates that withdrawal. The same sentence also omits cross-cutting policies that SystemWide now carries and that do bind Account operations: no-op rejection (REQ-SYS-6.1, cited by REQ-AC-2.9), not-found errors (REQ-SYS-6.2/6.3), time zone (REQ-SYS-7.1, cited by REQ-AC-1.48.1, 2.7, 3.9, 4.1 and 4.3) and atomicity (REQ-SYS-8.1).

**Action:** Remove "deletion" from the preamble's list of SystemWide policies, or replace the list with a plain pointer to SystemWide.md so it cannot fall out of date again.

**Why:** The spec's own preamble sends a reader to SystemWide.md for a deletion policy that SystemWide.md disclaims, which is the circular pointer the REQ-SYS-4.1 withdrawal was meant to resolve.

---

## AC-WD-1 — stale-reference
- **Location:** Specs/Behavioral/AccountCrud.md Withdrawn table, REQ-AC-1.24 and REQ-AC-2.5 reasons
- **Summary:** The withdrawal reasons for REQ-AC-1.24 and REQ-AC-2.5 say they were "Replaced by `active_begin` and `active_end` timestamps", but REQ-AC-1.42/1.43 say those fields are Calendar Dates and explicitly not Instants.
- **Resolution:** fix-spec

REQ-AC-1.42: "Account records must be able to represent a valid Calendar Date (LocalDate)... *Why a date, not an Instant:* ... The instant a (de)activation action occurred is captured by `modified_at`, not here." Definitions.md separates Instant from Date (calendar). The withdrawal reasons for 1.24 and 2.5 still call the replacements "timestamps", a term this corpus uses for Instants ("audit timestamps", REQ-SYS-3.1 to 3.3). Dan has kept withdrawal reasons current before: REQ-AC-1.38's reason was corrected on 2026-10-03.

**Action:** Change both reasons to "Replaced by the `active_begin` / `active_end` Calendar Dates (REQ-AC-1.42 to 1.50)".

**Why:** Definitions.md treats Instant and Date as distinct terms because they change which requirements apply, such as time-zone conversion (REQ-SYS-7.1). A withdrawal reason that names a timestamp contradicts the active requirement that replaced it.

---

