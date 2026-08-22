# efficacy-cr

## EFF-CR-1 — idiom
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/ClassificationRuleCrud.fs, lines 282-295, REQ-CR-5.3
- **Summary:** The source-pattern filter test hard-wires its expected rule names instead of deriving them from fixture data, matching Specimen 1.
- **Resolution:** fix-test

The test `REQ-CR-5.3 fetchRulesFiltered by source pattern fragment returns the rules whose rule group bodies carry that pattern and no others` asserts against a hard-coded list:

```fsharp
let expected =
    [ "Source = TestSplitBank && Credit then 5650"
      "Source = TestSplitBank && Debit then 5350" ]
Assert.Equal<string list>(expected, found |> namesOf)
```

Every other filter test in the same file (by id, by name fragment, by codeAtMatch, by activeOnly, combined filters, all-omitted) derives its expected values from `fixtureRules()` using list operations. This one is the sole outlier. The expected list encodes what the fixture contained at write time. If a fixture rule with a TestSplitBank source pattern is added (or one of these two is renamed), the test breaks on stale expected data rather than on the function's behavior.

The expected value IS derivable from fixture data without touching the function under test's call chain: filter `fixtureRules()` by walking each rule's groups, chains, and field matches using record accessors and DU pattern matching (checking for `Source` cases whose `StringSearchPattern.value` contains "TestSplitBank"). Those accessors are structural readers, not functions in `fetchRulesFiltered`'s call chain.

**Action:** Derive the expected list from `fixtureRules()` by filtering rules whose rule groups contain a Source field match with a pattern containing the search term, using the same DU pattern matching and list operations the other filter tests use. Keep the NotEmpty and NotEqual guards the sibling tests use to prove the filter is selective.

**Why:** Specimen 1 exists because hard-wired expected values separate the test's truth from the fixture's truth. When fixture data changes, a derived expected value adjusts automatically and the test catches function-level regressions. A hard-wired value forces a manual update, and during that manual update the author might encode the wrong expectation, masking a real bug. The convention is consistent across every other filter test in this file -- this one breaks the pattern.

---
