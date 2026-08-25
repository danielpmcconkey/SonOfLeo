# AccountCrud Spec Quality Auditor

## EG-AC-1 — enforcement-gap
- **Location:** Specs/Behavioral/AccountCrud.md, Waived from testing table (lines 144-149)
- **Summary:** Five waivers lack the Dan-approved date required by the documentation system's waiver table format.
- **Resolution:** fix-spec

Specs/README.md defines the waiver table structure as 'ID, reason, Dan's approval date.' Five entries in AccountCrud.md's waiver table have no Approved cell at all: REQ-AC-1.9, REQ-AC-1.16, REQ-AC-1.17, REQ-AC-1.39, and REQ-AC-2.16. The raw file confirms these rows end after two pipe-delimited cells rather than three. Other waivers in the same table (e.g., REQ-AC-1.1 with 'Dan, 2026-06-14', REQ-AC-1.47 with 'Dan, 2026-06-13') have explicit dates. These five appear to be the earliest waivers added, predating the governance discipline that later entries follow.

**Action:** Add Dan's approval date to the five waiver entries to bring them into compliance with the README's table format.

**Why:** The approval date is the governance record that Dan explicitly authorized each waiver. In a system where waivers exempt requirements from testing, undocumented authorization leaves the waiver's legitimacy unverifiable by future auditors. This is a documentation gap, not a question about the waivers' soundness (their reasons are all defensible).

---

## AMB-AC-3 — ambiguity
- **Location:** Specs/Behavioral/AccountCrud.md, REQ-AC-4.6
- **Summary:** REQ-AC-4.6 does not specify whether voided journal entries' lines are included in or excluded from the future-dated reference check for account deactivation.
- **Resolution:** dan-decides

REQ-AC-4.6 reads: 'the system must reject any request where the Account is referenced by a journal entry line whose entry date is later than the provided active end date.' The requirement does not qualify whether voided entries' lines count as references. The parallel deactivation guard REQ-AC-4.4 ('non-zero balance at the time of the request') inherently excludes voided entries because REQ-JE-4.7 mandates voided entries are 'excluded from every balance, trial-balance, and account-sum computation.' However, REQ-AC-4.6 is a referential check, not a balance computation, so REQ-JE-4.7's exclusion list does not clearly cover it. A GAAP-aware developer would likely exclude voided entries (void = soft-delete, dead to the ledger), but a developer reading 4.6 literally would include all lines regardless of void status -- potentially blocking deactivation when only voided future-dated entries exist. The two guards (4.4 and 4.6) apply inconsistent implicit treatments of voided entries: 4.4 excludes them by nature of its calculation, 4.6 is silent.

**Action:** Qualify REQ-AC-4.6 to state explicitly whether voided journal entries are included or excluded (e.g., 'referenced by a non-voided journal entry line whose entry date is later than...').

**Why:** If voided entries are included, deactivation can be blocked by dead entries that affect no accounting calculation -- a false positive that could only be resolved by direct DB intervention (since the voided entry cannot be un-voided). If excluded, the requirement matches the GAAP intent that voided entries are inert. Either answer is defensible, but the spec should state which to prevent implementation divergence between the two interpretations.

---
