# sys-efficacy

## SYS-EFF-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/AccountCreation.fs, line 17
- **Summary:** Test REQ-AC-2.13 REQ-SYS-3.2 constructNew generates UUID cites REQ-SYS-3.2 but asserts only UUID generation, not timestamp behavior.
- **Resolution:** fix-test

The test at line 17 names REQ-SYS-3.2 in its title, but the body's sole assertion is Assert.NotEqual(Guid.Empty, id) -- a UUID check. REQ-SYS-3.2 requires that both created_at and modified_at are set to the AuditEnvelope's system instant at creation time. No timestamp assertion appears anywhere in the test body. The sibling test at line 37 (constructNew sets timestamps from AuditEnvelope) does properly verify the behavior with Assert.Equal(expected, Account.createdAt account) and Assert.Equal(expected, Account.modifiedAt account). REQ-SYS-3.2 behavioral coverage is therefore present via three other tests (the sibling, plus JournalEntryCreation.fs line 114 and FiscalPeriod.fs line 193). The miscitation inflates traceability counts for REQ-SYS-3.2 and, per Tests/README.md naming rules (every test name starts with the requirement IDs it verifies), claims a verification it does not perform.

**Action:** Remove REQ-SYS-3.2 from the first test's name, leaving it as REQ-AC-2.13 constructNew generates UUID. The sibling test already carries the REQ-SYS-3.2 citation with proper assertions.

**Why:** A test name is the claim a test makes. When the traceability audit script greps for REQ-SYS-3.2, it counts this test as coverage. If someone later removed the sibling test -- believing two tests already covered the REQ -- timestamp verification would silently disappear. The naming convention exists precisely to prevent this: IDs in the name mean the body verifies them.

---
