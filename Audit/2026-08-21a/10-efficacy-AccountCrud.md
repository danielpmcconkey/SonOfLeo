# ac-efficacy-auditor

## AC-EFF-1 — test-gap
- **Location:** Tests/Tests.Integrated/Model/Ledger/Account.fs lines 138-146 and Tests/Tests.Integrated/InterfaceBridge/AccountRoutes.fs lines 153-163; REQ-AC-3.7
- **Summary:** REQ-AC-3.7 fetchAll tests assert only count at both the model and route layers, never inspecting any returned account value (Specimen 3).
- **Resolution:** fix-test

Both tests for REQ-AC-3.7 ("retrieve all Account records without filter") derive their expected count correctly from fixture data, but count is the sole assertion.

Model layer (Account.fs line 143):
  Assert.Equal(expectedCount, fetched |> List.length)

Route layer (AccountRoutes.fs line 159):
  Assert.Equal(expected, fetchedAccounts |> List.length)

No value is ever inspected at either layer. Smell test: if Account.fetchAll returned N accounts with correct structure but from the wrong query (e.g., duplicating some and dropping others, or returning records from a corrupted mapping), both tests pass.

For contrast, the sibling REQ-AC-3.9 test at the same model layer adds a value assertion that the closed account is NOT present in the active-only results:
  fixture.Data.closedBank1290Id
  |> fun closedId -> fetched |> List.exists(fun a -> Account.accountId a = closedId)
  |> Assert.False

REQ-AC-3.5 (fetchByParentId) similarly includes membership assertions. REQ-AC-3.7 is the outlier among the read tests — it is the only one that relies entirely on count without any membership or value check.

**Action:** Add at least one membership assertion to each REQ-AC-3.7 test. For the model-level test, spot-check that a known fixture account ID appears in the results (e.g., Assert.True that fixture.Data.assets1000Id is present). For the route-level test, verify at least one deserialized AccountReturn carries the expected code or name from the fixture. This matches the pattern already established by REQ-AC-3.9.

**Why:** Tests/README.md states: "Counts only in addition to values, never instead of them." Specimen 3 in the bullshit-test-specimens doc describes this exact pattern — a count assertion that never looks inside — and demonstrates the fix. A count-only fetchAll test proves the SQL returned the right cardinality but not that the right rows were returned. The risk is low in practice because other tests (e.g., REQ-AC-2.14 round-trip) verify individual account fidelity, but the principle is a stated standard in this codebase, not a nice-to-have.

---

