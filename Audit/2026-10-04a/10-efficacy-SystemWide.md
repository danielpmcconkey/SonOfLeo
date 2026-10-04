# test-efficacy-SystemWide

## EFF-SYS-3.2-POS-1 — test-gap
- **Location:** REQ-SYS-3.2, REQ-SYS-3.3, REQ-SYS-5.1. Untested sites: Src/Business.General/Person.fs:152-157; Src/Business.FinancialServices.Positions/{DimensionValue.fs:128-133, Security.fs:166, InvestmentAccount.fs:267, Holding.fs:154, Property.fs:276, Valuation.fs:78-85, AccountSnapshotHeader.fs:76-82}; Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:161-185. Tests that should cover them: Tests/Tests.Integrated/CrossDomainOrchestration/{PersonMaintenance,SecurityMaintenance,InvestmentAccountMaintenance,HoldingMaintenance,AccountSnapshotRecording,PropertyMaintenance}.fs, Tests/Tests.Integrated/InterfaceBridge/{PersonRoutes,PositionsRoutes}.fs, and the REQ-SYS-3.3 theory in Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:100-104
- **Summary:** No test anywhere checks created-at or modified-at for Person or for any of the seven Positions entities, so REQ-SYS-3.2, 3.3 and 5.1 are unverified for the whole new slice.
- **Resolution:** fix-test

Migrations 202610041000 and 202610041020 give general.person and seven positions tables (dimension_value, security, investment_account, holding, account_snapshot, property, valuation) NOT NULL created_at/modified_at columns. Each domain type exposes createdAt/modifiedAt accessors. Each update writes `modified_at = @modified` from Context.getInitiationInstant, for example Person.fs:152-157 and Holding.fs:154.

The new slice also adds a replace path that is new in kind. On re-record, AccountSnapshotOrchestration.recordOne (lines 168-177) builds the header with `stored |> createdAt` and `instant`, and Valuation.fs:78-85 updates value, basis and modified_at in place. Both should keep created-at and move modified-at.

A grep of Tests/ for createdAt, modifiedAt, created_at or modified_at returns nothing in any Person or Positions test file. The only REQ-SYS-3.2 citations are JournalEntryCreation.fs:129, AccountCreation.fs:29, FiscalPeriod.fs:229 and LinkageAndMatching.fs:358/376. The REQ-SYS-3.3 citations cover Account, journal entry comment and external reference, journal entry void, payment agreement link (OperationInstantAndAtomicity.fs:104) and classification rule. All are Ledger, CashFlow or Classification records.

The Person/Positions same-value update tests cite REQ-SYS-6.1: PositionsRoutes.fs:346 (ticker), :492 (institution) and :910 (use). They assert only `Assert.Equal(created |> securitySummary, returned |> securitySummary)` and a stored summary equal to the created one, and the summary has no timestamp. The ledger counterparts in SameValueUpdates.fs, whose header says 'One case per updatable entity family', call `assertAdvanced (before ...modifiedAt) (after ...modifiedAt)` to prove the update was actually written. That file has no Person or Positions case.

Smell test: suppose a Positions update left modified_at alone, or an insert wrote modified_at = created_at from a stale instant, or a snapshot replace reset created_at. Every Person/Positions test would still pass.

Precedent: audit 2026-10-03a accepted EFF-SYS-3.3-1 (#128) and EFF-SYS-5.1-1 (#138) for exactly this gap in CashFlow, where it was hiding a real bug: five update functions never wrote modified_at.

**Action:** Add rows for Person, Dimension Value, Security, Investment Account, Holding, Property, Account Snapshot (replace) and Valuation (replace) to the REQ-SYS-3.3 theory in OperationInstantAndAtomicity.fs. Each row creates under one context, updates under `TestContext.updateInitiationInstant`, and reads back from persistence asserting modified-at = updating instant and created-at = creating instant. Add REQ-SYS-3.2 / REQ-SYS-5.1 assertions to one create test per entity: created-at and modified-at equal the context's initiation instant, and the same values come back on read.

**Why:** REQ-SYS-3.2/3.3 are system-wide, so every new entity family needs its own evidence. A test on journal entries says nothing about a separate UPDATE statement in another project, and the CashFlow defect found last audit shows this exact blind spot hiding real data corruption. The same-value REQ-SYS-6.1 tests are also weaker for it: without a modified-at check they cannot tell an update that ran from one silently skipped.

---

## EFF-SYS-8.1-3 — test-gap
- **Location:** REQ-SYS-8.1. Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:324 (assertions at ~349-351 and ~369-370)
- **Summary:** The REQ-SYS-8.1 'all of the operation's writes are in the database' test asserts only hard-wired counts of lines, references and comments, and never looks at what was written.
- **Resolution:** fix-test

`REQ-SYS-8.1 for each of posting a journal entry with several comments and a batch post of several staged entries, when every step is valid, all of the operation's writes are in the database` asserts:
```fsharp
Assert.Equal(2, read |> JE.jeLines |> List.length)
Assert.Equal(1, read |> JE.externalReferences |> List.length)
Assert.Equal(2, read |> JE.comments |> List.length)
```
and, in the batch-post branch, `Assert.Equal(2, journalEntry |> JE.jeLines |> List.length)` and `Assert.Equal(1, journalEntry |> JE.externalReferences |> List.length)`.

The literals 2/1/2 are typed in by hand (Specimen 1) rather than taken from `input.lines`, `input.externalReferences` and `input.comments`, or from the staged entries' lines. No value is ever inspected (Specimen 3). The comments 'first' and 'second' are never checked for their text, and the external reference 'Ref {tag}' is never checked for its value.

Smell test: suppose the operation committed the header and two copies of the first comment, or the lines of a different entry. A count-only check passes either way. The claim the test makes is that each of the operation's writes is present, and that claim is about identity, not cardinality.

**Action:** Take the expected sets from the input the test built. Assert that the read-back comment texts equal `input.comments |> List.map _.commentText`. Assert that the external references equal the input's (FI, reference text) pairs. Assert that the line (account, amount, type) triples equal the input lines. For the batch branch, assert each journal entry's line triples against the staged entry's lines.

**Why:** Counts are allowed alongside value assertions, never in place of them (Tests/README.md, Assertion shape). An atomicity test that only counts cannot tell 'all writes landed' from 'the right number of rows landed', and those are different properties.

---

