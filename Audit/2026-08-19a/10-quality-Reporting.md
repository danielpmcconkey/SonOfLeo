# reporting-auditor

## TEST-GAP-RPT-1 — test-gap
- **Location:** Specs/Behavioral/Reporting.md, REQ-RPT-1.6
- **Summary:** REQ-RPT-1.6 is classified as tested but the test verifies flat alphabetical sort, not the depth-first tree order the requirement specifies.
- **Resolution:** fix-test

REQ-RPT-1.6 states: 'The result list must be sorted in depth-first tree order: top-level accounts are sorted by account code, and within each parent, children are sorted by account code. A parent account's row appears immediately before its children's rows.' The spec's own Why note warns: 'A flat code sort interleaves unrelated subtrees (e.g., child 5311 sorts after sibling parent 5300, breaking the hierarchy).'

The test at Tests/Tests.Integrated/ModelOrchestrator/TrialBalance.fs:109 is named 'REQ-RPT-1.6 result list is sorted by account code' and asserts: codes |> List.sort = codes. This checks flat alphabetical ordering of account codes, which is exactly the behavior the requirement's Why note says is wrong. The test name misstates the requirement ('sorted by account code' vs 'depth-first tree order'). A todo comment on line 110 confirms this is a known issue: 'todo: this rules is wrong and this test needs to be revisited. The parent child hierarchy is primary. Account code is secondary.'

The test currently passes only because the test fixture's chart of accounts happens to produce identical ordering under both flat sort and depth-first traversal. A chart of accounts where a sibling parent's code sorts between another parent's children would break depth-first order under flat sort, and this test would not catch it.

REQ-RPT-1.6 does not appear in the waived or unenforceable tables, so it is implicitly classified as tested. The three-state rule requires that every active requirement be tested, waived, or unenforceable. With the test acknowledged as wrong, REQ-RPT-1.6 should either have its test corrected to verify depth-first tree order, or be moved to the waived table with an appropriate reason.

**Action:** Either fix the test to verify depth-first tree order (parent rows immediately before their children, code-sorted within each parent) or move REQ-RPT-1.6 to the waived table with a reason until the test is corrected.

**Why:** The three-state rule exists to ensure every active requirement has a verified enforcement mechanism. A test that checks the wrong property provides false confidence -- a flat-sort regression in the implementation would pass this test while violating the requirement. The spec invested a Why note explaining exactly this failure mode, making the gap between the test and the requirement especially clear.

---
