# JournalEntryCrud Spec Auditor

## AMB-JE-3.6.2 — ambiguity
- **Location:** Specs/Behavioral/JournalEntryCrud.md, REQ-JE-3.6.2
- **Summary:** REQ-JE-3.6.2 uses "should" where every other capability requirement in the spec uses "must," leaving the mandatory/optional status of the as-of date filter ambiguous.
- **Resolution:** fix-spec

REQ-JE-3.6.2 reads: "The caller should be able to pass an optional 'as-of' date such that the result represents the balance as it would've been at the end of the as-of date." Every other capability requirement in section 3 (3.1 through 3.9.3) and throughout the spec uses "must" (e.g., REQ-JE-3.7: "The system must be able to retrieve..."; REQ-JE-3.6: "The system must be able to compute and return..."). In formal requirements engineering, "should" signals recommended-but-not-required behavior, while "must" signals mandatory. The feature IS implemented and tested (two [<Fact>] tests in Tests.Integrated/ModelOrchestrator/AccountBalance.fs cite REQ-JE-3.6.2), so the implementation treats it as mandatory. The spec text is inconsistent with the actual enforcement status and with the modal verb used by every peer requirement.

**Action:** Replace "should" with "must" in REQ-JE-3.6.2 if the as-of date capability is mandatory (which the tests confirm it is).

**Why:** Modal verb inconsistency in a formal spec creates genuine uncertainty about a requirement's binding force. During prioritization, triage, or deferred-work decisions, a developer or PM following standard requirements conventions could legitimately treat a "should" as deferrable while treating a "must" as non-negotiable. The single outlier word obscures the requirement's actual status.

---
