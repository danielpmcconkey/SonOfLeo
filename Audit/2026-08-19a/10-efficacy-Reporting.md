# reporting-test-efficacy-auditor

## RPT-SORT-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/TrialBalance.fs, line 109 — REQ-RPT-1.6
- **Summary:** The REQ-RPT-1.6 test verifies flat alphabetical code sort instead of depth-first tree ordering, and the todo comment on line 110 confirms the test is known to be wrong.
- **Resolution:** fix-test

REQ-RPT-1.6 requires depth-first tree order: parents before children, children sorted by code within parent. The test at line 109 extracts all account codes, calls List.sort (flat alphabetical), and asserts the result matches the actual order. The todo comment on line 110 says explicitly: 'this rules is wrong and this test needs to be revisited. The parent child hierarchy is primary. Account code is secondary.' The REQ's own Why clause states that 'a flat code sort interleaves unrelated subtrees.' The production code correctly implements depth-first order (TrialBalance.fs line 95: parent emitted before children via flattenNestedTrialBalance, children sorted by code in crawlAndCompile at line 42), and even has a commented-out flat sort at line 114 that was removed. But the test still checks the old behavior. The fixture's F-prefixed account codes happen to be structured so that flat alphabetical sort and depth-first tree order produce identical output, so the test passes without exercising the distinguishing property. A fixture account whose code sorts between a parent and its descendants (e.g., a top-level account F-5100 sitting alphabetically between parent F-5000 and its child F-5300) would expose a flat-sort implementation bug that this test cannot detect. The test name ('result list is sorted by account code') is also hollow per the specimens doc: it describes flat sort, not depth-first tree ordering, and is satisfiable by an implementation that merely sorts codes.

**Action:** Rewrite the test to derive the expected ordering from the fixture's parent-child hierarchy using a depth-first walk (parent appears before its children, children sorted by code within parent), then assert that the actual row order matches. Update the test name to describe the depth-first tree order property (e.g., 'parent row appears immediately before its children sorted by code within each parent').

**Why:** The test cites a requirement it does not exercise. If the implementation were changed to a flat code sort (which the commented-out line 114 shows was once considered), this test would still pass, leaving a broken invariant undetected. The REQ's own rationale calls flat code sort incorrect behavior.

---
