# DataIngestion Test-Efficacy Auditor

## STG-AQ-1 — idiom
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs, line 146 (REQ-STG-2.9)
- **Summary:** Cowardly inequality on line count uses >= 2 instead of exact count (Specimen 2).
- **Resolution:** fix-test

The test citing REQ-STG-2.2 through 2.7 and 2.9 asserts `Assert.True((entry |> lines |> List.length) >= 2)` on the DoorDash entry (grp-001), which is constructed from exactly 2 raw rows in `buildTestRows` at lines 48-49 of the same file. The `>= 2` assertion is Specimen 2 from the bullshit-test specimens doc: it tolerates line duplication and provides no more protection than `> 0`. A system that created 4 lines from 2 raw rows would pass this test. The expected count should be `Assert.Equal(2, entry |> lines |> List.length)` because the test knows the exact input and should assert the exact output.

**Action:** Replace `Assert.True((entry |> lines |> List.length) >= 2)` with `Assert.Equal(2, entry |> lines |> List.length)`. The DoorDash entry has exactly 2 raw rows and must produce exactly 2 staged lines.

**Why:** An assertion that accepts any value above a floor is not testing the relationship between input and output. It states a property of the domain invariant (at least 2 lines) rather than the behavior under test (2 raw rows produce 2 staged lines). Specimen 2 exists because this pattern has survived review before -- a `>= n` looks like it is checking something when it is checking almost nothing.

---

## STG-UC-1 — missing-requirement
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryIngestion.fs, lines 435-445 (REQ-STG-7.3)
- **Summary:** The voided-JE dedup exclusion test exercises behavior that REQ-STG-7.3 does not describe.
- **Resolution:** fix-spec

The test `REQ-STG-7.3 dedup does not flag entry matching voided JE external reference` (line 435) verifies that a staged entry whose source+fi_reference matches a voided journal entry's external reference is NOT flagged as duplicate. This is correct behavior -- re-importing data after voiding the bad JE that consumed it should not be blocked. However, REQ-STG-7.3 says: 'A staged entry is flagged as duplicate when a posted journal entry in the ledger carries an external reference whose financial_institution and reference values match the staged entry's source and fi_reference.' A voided journal entry was posted and does carry an external reference; the requirement does not qualify 'posted' as 'non-voided.' REQ-JE-4.7 excludes voided entries from 'every balance, trial-balance, and account-sum computation,' but a dedup reference lookup is not a balance computation. The test is exercising a domain-correct edge case that no requirement currently covers.

**Action:** Add an explicit qualification to REQ-STG-7.3, e.g.: 'A staged entry is flagged as duplicate when a non-voided posted journal entry in the ledger carries an external reference...' Alternatively, add a new sub-requirement (REQ-STG-7.3.1) stating that voided journal entries are excluded from the ledger-side dedup match.

**Why:** The test verifies real, important behavior -- without this exclusion, voiding a bad JE and re-importing the corrected data would permanently block the re-import. But a test that goes beyond what the spec describes creates a traceability gap: the behavior is enforced by a test no requirement owns. If the code changed to include voided JEs in dedup, the test would catch it, but an auditor reading the spec alone would never know the exclusion was intended.

---

