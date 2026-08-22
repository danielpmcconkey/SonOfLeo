# stale-ruling-auditor

## STALE-DAL-EFF — stale-ruling
- **Location:** Skills/SonOfLeoRequirementsAudit/resolved-findings.md, DAL-EFFICACY (line 166)
- **Summary:** DAL-EFFICACY ruling's premise -- "The DAL has no behavioral spec of its own" -- is factually wrong and broad enough to suppress non-efficacy findings about DataAccessLayer.md.
- **Resolution:** fix-spec

The DAL-EFFICACY ruling (overruled 2026-08-20) states: "The DAL has no behavioral spec of its own -- it is infrastructure. Its correctness is validated transitively through the domain tests that exercise it... A test-efficacy auditor scoped to the DAL will always return 'no findings' because there are no REQ IDs to audit against."

Specs/Behavioral/DataAccessLayer.md exists and contains 19 active REQ-DAL requirements (1.3, 1.14-1.20, 2.1-2.3, 3.1-3.7). All 19 are either waived from testing or classified as unenforceable, so the ruling's CONCLUSION -- that a test-efficacy audit would yield nothing -- is correct. But the PREMISE is wrong on two counts: (1) the DAL demonstrably has a behavioral spec, and (2) there are REQ IDs, they are just all waived/unenforceable.

The ruling's scope is narrow ("Whether the DAL needs a test-efficacy audit pass"), but the premise is stated as blanket fact ("The DAL has no behavioral spec"), not scoped to efficacy. An auditor reading the resolved-findings file as instructed encounters this factual claim and may skip DataAccessLayer.md for spec-quality, ambiguity, or contradiction audits -- findings Dan never intended this ruling to suppress.

**Action:** Rewrite the premise to state the accurate ground truth: "All DAL requirements (REQ-DAL-*) are either waived from testing or classified as unenforceable. A test-efficacy auditor scoped to the DAL will always return 'no findings' because there are no non-waived, enforceable REQ IDs to verify tests against. This is by design, not a gap. Do not flag the absence of DAL-specific efficacy findings." This preserves the correct conclusion while removing the false claim that no behavioral spec exists.

**Why:** Resolved findings are read as mandatory precedent by every auditor. A false factual premise in a precedent ruling creates a risk of suppressing legitimate findings in an adjacent audit type. The ruling intended to suppress only test-efficacy findings about the DAL, but the wording could suppress any finding about DataAccessLayer.md.

---

