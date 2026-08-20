# DataIngestion Test Efficacy Auditor

## STG-ASSERT-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/DataIngestion/StageEntryStatusTransition.fs, lines 89-104, citing REQ-STG-4.2
- **Summary:** The validTransitions Theory test asserts only the count of valid transitions per status, never inspecting which transitions are returned.
- **Resolution:** fix-test

The Theory test at line 98 (`REQ-STG-4.2 validTransitions returns correct count for each status`) uses hard-wired InlineData counts (5, 4, 3, 3, 2, 2, 0, 1) and asserts only `Assert.Equal(expectedCount, transitions |> List.length)`. It never inspects the actual DU cases in the returned list. If `validTransitions Ingested` returned `[Ingested; Ingested; Ingested; Ingested; Ingested]` -- five copies of the wrong transition -- this test would pass. This is Specimen 1 (hard-wired counts from the spec's transition table) combined with Specimen 3 (the count that never looks inside). The inconsistency is visible within the same file: the companion test at line 107 (`validTransitions from None returns only Ingested`) checks both count AND the actual value (`Assert.Equal(Ingested, transitions |> List.head)`), which is the standard the Theory test should meet. The `validTransitions` function (StageEntryStatusTransition.fs line 27) returns concrete `StagedEntryStatus list` values whose members are straightforward to compare -- a `List.sort` and `Assert.Equal<StagedEntryStatus list>` per case would close the gap.

**Action:** Replace the count-only Theory assertion with a comparison of the actual transition list against the expected members. The InlineData approach does not lend itself to list comparisons; individual [<Fact>] tests per status (matching the fromString pattern above it) or a single test iterating a local list of (status, expectedTransitions) pairs would work.

**Why:** A count-only assertion on a function that returns a finite list of DU cases cannot detect a misimplemented transition table where the cardinality is preserved but the members are wrong. The transition table governs which status changes the system permits; a wrong member is a silent gate that either blocks a legitimate workflow or permits an illegal one.

---

## STG-MISLABEL-1 — test-gap
- **Location:** Tests/Tests.Isolated/Model/DataIngestion/Classifier.fs, line 134, citing REQ-STG-5.3
- **Summary:** The test cites REQ-STG-5.3 but exercises inactive-rule filtering, not the non-null account_code protection behavior the requirement describes.
- **Resolution:** fix-test

REQ-STG-5.3 states: 'Classification must not modify a staged line whose account_code is already non-null.' The test at line 134 (`REQ-STG-5.3 classify filters out inactive rules before matching`) creates an active and an inactive rule, passes a candidate with NO existing account_code (the `MatchCandidate` type at ClassificationRuleComponent.fs line 95 has no accountCode field -- it cannot represent a line with an existing code), and asserts the inactive rule is excluded. This tests that `classify` calls `List.filter isActive` (Classifier.fs line 38) -- a real behavior, but not REQ-STG-5.3's behavior. The non-null protection enforced by REQ-STG-5.3 happens at the orchestrator level, where candidates are constructed only for lines with null account_code; the classifier never sees lines with existing codes. The real REQ-STG-5.3 IS correctly tested at the integrated layer (StageEntryClassification.fs line 43), so coverage is not missing -- the citation is misleading. The behavior being tested (inactive-rule filtering) has no REQ in DataIngestion.md or any other listed spec; the spec notes 'The rules entity (pattern, priority, FI scoping, account mapping) is specified separately' but no such spec exists among the 9 behavioral specs.

**Action:** Rename the test to reflect what it actually exercises (inactive-rule filtering). Either cite a REQ from the forthcoming classification-rules spec when it exists, or flag the behavior as a candidate for a new REQ. No change to the integrated REQ-STG-5.3 test is needed -- it correctly covers the requirement.

**Why:** A mislabeled citation causes the traceability audit to report REQ-STG-5.3 as tested at the isolated layer when it is not testable there (the classifier's input type cannot represent the condition 5.3 guards against). Anyone searching for 'where is 5.3 verified?' finds a test that cannot fail for 5.3's reason. Separately, the inactive-rule filtering behavior is a real system property with no requirement backing it -- a gap that should be acknowledged.

---

## STG-CONTRA-1 — contradiction
- **Location:** Specs/Definitions.md line 49 vs Specs/Behavioral/DataIngestion.md REQ-STG-4.4
- **Summary:** The 'Postable' definition in Definitions.md includes a line-level account_code condition that REQ-STG-4.4 explicitly excludes.
- **Resolution:** fix-spec

Definitions.md (authority level 2) defines Postable as: 'A staged entry whose status is Classified or Reviewed and whose every staged line has a non-null account_code.' REQ-STG-4.4 (authority level 3) states: 'A staged entry is postable when its status is Classified or Reviewed. No additional filtering (e.g. line-level account_code presence) is applied.' The parenthetical in 4.4 explicitly names the very condition the Definition includes -- the two documents directly contradict each other on whether account_code presence is part of postability. The code and the REQ-STG-4.4 test (StageEntryPosting.fs line 492, which deliberately strips an account code and asserts the entry remains in the postable set) implement the requirement's version, not the Definition's. The requirement was written with clear intent ('if the upstream invariants are sound, all lines are coded by the time an entry reaches these statuses. If they are not, posting fails loudly... rather than silently excluding the entry') -- but the Definition was not updated to match.

**Action:** Update the Postable definition in Definitions.md to match REQ-STG-4.4: remove the 'and whose every staged line has a non-null account_code' clause, aligning the definition with the implemented behavior and the deliberate design decision recorded in REQ-STG-4.4.

**Why:** Under the authority hierarchy, Definitions.md outranks behavioral REQs. A test writer or auditor reading the Definition first would conclude that fetchAllForPosting must filter on account_code, contradicting what REQ-STG-4.4 and the existing test assert. The stale definition is a source of confusion about what 'postable' means in this system.

---
