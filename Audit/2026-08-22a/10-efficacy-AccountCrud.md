# account-crud-efficacy-auditor

## AQ-AC-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs, line 103, REQ-AC-3.10
- **Summary:** REQ-AC-3.10 FetchByParentCode happy-path test asserts only count, never inspecting the returned children's values or membership (Specimen 3).
- **Resolution:** fix-test

The test at AccountRoutes.fs:103-119 (`REQ-AC-3.10 Account FetchByParentCode happy path`) derives the expected count from fixture data (properly -- no hardcoded magic number) but uses it as the sole assertion:

```fsharp
Assert.Equal(expected, fetchedChildren |> List.length)
```

No property of any returned child is examined. If FetchByParentCode returned `expected` number of accounts belonging to a different parent, or `expected` random accounts of the right shape, the test would pass. This is the only happy-path test for REQ-AC-3.10 -- there is no model-layer test for fetch-by-parent-code (the model layer tests REQ-AC-3.5, fetch-by-parent-ID, which is a different requirement with a different input vector). The route level is the lowest layer where the code-to-ID conversion lives, so this test is the sole coverage for the full fetch-by-parent-code path.

Compare with the REQ-AC-3.5 model-layer test (Account.fs:104-120), which covers the analogous by-ID path with both count AND membership assertions:
```fsharp
Assert.Equal(expectedCount, List.length fetched)
expectedChildren
|> List.forall(fun id -> fetched |> List.exists(fun a -> Account.accountId a = id))
|> Assert.True
```

The test standard (Tests/README.md) states: 'Assert on domain values -- names, amounts, dates round-tripped -- and on membership. Counts only in addition to values, never instead of them.'

**Action:** Add a membership or value assertion to the REQ-AC-3.10 happy-path test. Derive expected child IDs (or codes) from the fixture the same way the REQ-AC-3.5 model test does, and assert that every expected child appears in the deserialized AccountReturn list. Alternatively, assert that every returned child's parentCode or accountType matches the parent's.

**Why:** A count-only assertion on a filtered fetch cannot distinguish the correct subset from a wrong subset of the same cardinality. The smell test fails: if FetchByParentCode returned the right count of unrelated accounts, the test would stay green. This is the only happy-path verification of the code-to-children path, so no other test compensates.

---
