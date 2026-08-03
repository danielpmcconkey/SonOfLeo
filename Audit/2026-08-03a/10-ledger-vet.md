# stale-ruling-auditor

## STALE-IE4-TRIGGER — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, IE-4 entry
- **Summary:** IE-4's 'revisit when' trigger text is ambiguous after GAAP-CLOSE disambiguated the meaning of 'period closure'.
- **Resolution:** fix-spec

IE-4 (Equity Subtypes Not Future-Proofed, deferred 2026-06-13) has the trigger 'Revisit when: Period closure is designed.' At the time this was written, 'period closure' was a single undifferentiated concept. GAAP-CLOSE (deferred 2026-08-02) subsequently split it into two distinct concepts: (1) FP posting lock (is_open toggle) -- now designed, built, tested in FiscalPeriodCrud.md REQ-FP-4.1/4.2 and exercised in code; and (2) GAAP closing entries (annual retained-earnings sweep) -- planned but unscheduled. IE-4's actual dependency is on concept (2): the ruling says 'Speculating on the mechanism before knowing what period closure needs just cements a guess,' and the mechanism in question (how to identify retained earnings) is relevant only to the GAAP closing-entries sweep, not to the posting lock. Since the posting lock IS now designed and built, a literal reading of IE-4's trigger ('Period closure is designed') could lead a future auditor to conclude the trigger has fired and prematurely re-raise the finding. GAAP-CLOSE's own trigger ('Dan schedules the closing-entries slice') uses the correct, disambiguated terminology.

**Action:** Rewrite IE-4's trigger to: 'Revisit when: GAAP closing entries (retained-earnings sweep) are designed' -- aligning with GAAP-CLOSE's terminology. The ruling text and status (deferred) are otherwise still sound and should be kept.

**Why:** A deferred finding whose trigger text can be misread as having already fired risks two failure modes: either a future auditor wastes effort re-investigating a finding Dan already ruled on, or a future auditor suppresses a legitimate re-raise because the ruling exists but the trigger interpretation is contested. Aligning the trigger with the established GAAP-CLOSE terminology removes the ambiguity.



