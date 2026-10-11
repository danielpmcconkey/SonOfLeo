# test-efficacy-ClassificationRuleCrud

## EFF-CR-1 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationClaimantsAndRuns.fs:224-259 (REQ-CR-1.24)
- **Summary:** The REQ-CR-1.24 test inserts its broken-claimant row with rule_groups JSON in the old serialised-DU shape, so the row is also invalid in its rule groups, and the test passes only because reconstitute happens to check the claimant before parsing the groups.
- **Resolution:** fix-test

Line 246 writes rule_groups as `[{"chainOne": {"chain": [{"Case": "Description", "Fields": ["x"]}]}, "chainTwo": null, "connector": {"Case": "And"}}]`. That is the shape used before migration 202610031600-StoredRuleGroupShape. The current stored shape (ClassificationRule.fs StoredRuleGroup/StoredFieldMatch, and the migration header) is `{"connector": "And", "chainOne": [{"field": "Description", "pattern": "x", ...}], "chainTwo": null}`. Parsing the test's JSON with `fromJson<StoredRuleGroup list>` in ruleGroupsFromJson would fail (connector is an object, not a string; chainOne is an object, not a list), which gives ClassificationRuleStoredGroupsInvalid. The test asserts `ClassificationRuleInvalidClaimant(id, _, _)` (line 256). It passes today only because ClassificationRule.reconstitute (ClassificationRule.fs ~lines 250-258) resolves the claimant before it calls ruleGroupsFromJson. The rows are meant to differ from a valid rule in exactly one way (both claimants set, or neither), but each has two defects. A legitimate reordering of reconstitute, with the claimant validation still fully correct, would turn this test red for a reason unrelated to REQ-CR-1.24. The test does not give a false pass today: a regressed claimant check still fails it, on the groups error. Its premise is stale, though, and its verdict depends on an evaluation order no requirement specifies.

**Action:** Replace the literal JSON on line 246 with the current stored shape, ideally produced by `ClassificationRule.ruleGroupsToJson` from a valid group, so the claimant columns are the row's only defect.

**Why:** A negative test should vary exactly one thing from a valid input. When the input is broken in two ways, the error the test sees depends on which check runs first, not on the behavior the test names. The test then pins an unspecified implementation order and goes stale as soon as the stored format changes, which it already has.

---

## EFF-CR-2 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:448-469 (REQ-CR-5.3, active-only flag)
- **Summary:** The activeOnly=true filter test takes its expected count from the function under test (Specimen 6) and never checks which active rules come back.
- **Resolution:** fix-test

The test calls `fetchRulesFiltered` with accountAtMatch=5300 and activeOnly=false (withInactive), then again with activeOnly=true. Its assertions are `Assert.DoesNotContain(inactiveName, activeOnly |> namesOf)` and `Assert.Equal((withInactive |> List.length) - 1, activeOnly |> List.length)`. The expected count comes from a second call to `ClassificationOrchestration.fetchRulesFiltered`, which is the function under test (Specimen 6). The activeOnly=true result is never compared against a fixture-derived expected set; only its cardinality and the absence of one name are checked (Specimen 3 for the positive half). The sibling activeOnly=false test (lines 471-488) does pin the unfiltered set against fixtureRules(), so this is mitigated, not hollow. However, the only test that names the activeOnly=true behavior does not prove that every active rule satisfying the other filters is returned. A same-sized result that swapped one active rule for another would pass.

**Action:** Derive `expected = fixtureRules () |> List.filter (fun r -> claimant = Account personalExpenses5300Id && isActive r) |> namesOf` and assert `Assert.Equal<string list>(expected, activeOnly |> namesOf)` in place of the length-minus-one comparison.

**Why:** Tests/README.md: "No function in the call chain of the function under test may appear in the derivation of the expected value." A filter test has to state both halves: every row that satisfies the filter comes back, and nothing else does. Comparing one call's count with another's says nothing about which rows were returned.

---

## EFF-CR-3 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:986, :1011; Tests/Tests.Integrated/InterfaceBridge/ClassificationRuleRoutes.fs:214, :308 (REQ-CR-5.4, REQ-CR-5.2, REQ-CR-5.5)
- **Summary:** Four tests use hard-wired fixture counts as preconditions (Specimen 1), so a fixture change unrelated to the behavior under test fails them.
- **Resolution:** fix-test

ClassificationRuleCrud.fs:986 `Assert.Equal(6, sharedCode.Length)`, where the comment says F-5350 "already claims three fixture rules". ClassificationRuleCrud.fs:1011 `Assert.Equal(5, keyed |> List.filter (fun (priority, _) -> priority = 500) |> List.length)`, where the comment says priority 500 "already holds" two named fixture rules. ClassificationRuleRoutes.fs:214 `Assert.Equal(2, candidates |> List.length)` for rules starting "Acme Insurance to ". ClassificationRuleRoutes.fs:308 `Assert.Equal(2, acmeInsuranceRules |> List.length)`. Each assertion pins how many rules the fixture holds on the day the test was written. They are non-vacuity guards, not the asserted behavior: the real expectations (sort order, exact-name fetch, per-rule account name) are derived from fixture data. Adding a fourth F-5350 rule or a third "Acme Insurance to" rule to the fixture would fail all of these for a reason unrelated to REQ-CR-5.4, 5.2 or 5.5. Tests/README.md forbids this: "Do not ever write tests that assume the count of anything in the database is a constant."

**Action:** Replace each exact-count guard with a non-vacuity guard derived from the fixture. Per the ROUTE-ORACLE precedent, an inequality such as `>= 2` is acceptable as a guard. For example, use `Assert.True(sharedCode.Length >= 2)` and `Assert.True(candidates.Length >= 2)`. Keep the derived assertions as they are.

**Why:** Specimen 1: a hard-wired count records a snapshot of the fixture, not a relationship. A guard only has to prove the test is not vacuous (at least two rules share the key). An exact count does more than that and fails whenever someone adds fixture data for another test.

---

