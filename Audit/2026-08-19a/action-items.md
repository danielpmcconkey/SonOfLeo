# Action Items — 2026-08-19a Audit

| # | Finding | What | Owner | Status |
|---|---------|------|-------|--------|
| 1 | SD-1 | Dedup and classify have no standalone CLI routes — both coupled to ingestion pipeline. Decide whether to add them or update position statement. | Dan | overruled — not a factual delta; auditor inferred a gap from a stretch reading of the position statement |
| 2 | CON-STG-1 | Normalize all status value references in DataIngestion.md to PascalCase throughout | Hobson | done |
| 3 | CON-STG-2 | Align Definitions.md "Postable" definition with REQ-STG-4.4 — remove the account_code criterion or update REQ-STG-4.4 to check it. | Dan | done — same ruling as #13 (STG-CONTRA-1) |
| 4 | CON-JE-1 | Fix the spec to include all legal post-posting alterations | Hobson | done |
| 5 | CON-NGUI-1 | Discuss with Dan — needs deeper reasoning about changing the specs around the UI layer. Not a quick fix. | Hobson | accepted |
| 6 | TEST-GAP-RPT-1 | Fix REQ-RPT-1.6 test to verify depth-first tree order, not flat alphabetical sort (todo on line 110 confirms known issue). | Dan/BD | accepted |
| 7 | EG-SYS-3.1a | staged_entry and staged_entry_line lack created_at/modified_at columns required by REQ-SYS-3.1. Add columns or document exemption. | Dan | overruled — same as #30; staging entities are not first-class entities, have full audit logs |
| 8 | SCHEMA-STG-1 | Same as EG-SYS-3.1a — code-truthfulness auditor independently found the missing timestamps on staging entities. | Dan | overruled — same as #7/#30 |
| 9 | SPEC-STG-1 | Same as CON-STG-1 — code-truthfulness auditor independently found the lowercase status casing in four REQs. | Hobson | done — same ruling as #2 |
| 10 | AQ-AC-1 | Add fetchByCode happy-path assertions in Tests/Tests.Integrated/Model/Ledger/Account.fs, just under the REQ-AC-3.3 fetchById test | Dan/BD | accepted |
| 11 | STG-ASSERT-1 | Add to bullshit-test hall of shame AND rewrite. The test is worthless: (a) REQ-STG-4.2 is about terminal status, not counting transitions, (b) hard-coded constants, (c) counting DU elements has zero value. Replace with a Theory of all 72 status pairs (9 from × 8 to) with pass/fail expectations, or a manual gauntlet like AccountComponent.fs:160-507 | Dan/BD | accepted |
| 12 | STG-MISLABEL-1 | Test cites REQ-STG-5.3 but exercises inactive-rule filtering. Rename to reflect actual behavior and cite correct REQ. (See #33–#35 for follow-on actions.) | Dan/BD | accepted |
| 13 | STG-CONTRA-1 | Rewrite spec: posting process must fail loudly for any record whose status is Classified or Reviewed AND whose account_code is None or doesn't match an account code in the ledger. The test is correct; the spec wording is wrong. | Hobson | done — Definitions.md Postable rewritten; REQ-STG-4.4 and 9.4 already correct |
| 14 | FP-AQ-1 | Rewrite test to use F# to count periods in fixture data and compare with exact equality. No >= allowed. | Dan/BD | accepted |
| 15 | FP-AQ-2 | Date-derivation tests for REQ-FP-1.4/1.5 assert month and day but never assert the year component. Add year assertions. | Dan/BD | accepted |
| 16 | IDIOM-JE-1 | REQ-JE-3.6.1 net-balance test uses `> zero` (cowardly inequality) instead of asserting exact $200 expected amount. | Dan/BD | overruled — added to resolved-findings.md |
| 17 | IDIOM-JE-2 | Write happy-path test in model orchestrator tests (not just sad-path validation) | Dan/BD | accepted |
| 18 | IDIOM-JE-3 | Change test citation from JE-4.9 to SYS-6.1. (See #36 for follow-on investigation.) | Dan/BD | accepted |
| 19 | IDIOM-JE-4 | Implement auditor's recommendation — add at minimum a null-input-returns-null assertion (or equivalent) to each of the six tests | Dan/BD | accepted |
| 20 | MON-SPEC4-1 | All 13 Money sad-path tests use Assert.True(result.IsError) instead of typed DU matching. Convert to typed match expressions. | Dan/BD | accepted |
| 21 | NGUI-AQ-1 | SonOfLeoCli stderr test uses Assert.Contains (Specimen 4 pattern). Replace with Assert.Equal to match Reports CLI pattern. | Dan/BD | overruled — Contains check is equivalent to Reports CLI's newline-append pattern; both handle stderr's trailing line break differently but are equally safe |
| 22 | NGUI-COV-1 | Split REQ-NGUI-1.3.1 into two REQs — one for the testable behavior and one for the stack-trace suppression which will be waived as untestable (can't force a .NET system exception through command routes) | Hobson | done — REQ-NGUI-1.3.1 split, REQ-NGUI-1.3.2 created and waived |
| 23 | RPT-SORT-1 | Fix the test to verify depth-first tree order | Dan/BD | accepted |
| 24 | SYS-5.1-PARTIAL | Have BD add equality assertions on the other fields | Dan/BD | accepted |
| 25 | CUST-FETCH-1 | Add a fetchFiltered route on StageEntry | Dan | accepted |
| 26 | CON-POSTABLE-1 | Same as CON-STG-2/STG-CONTRA-1 — Definitions.md "Postable" contradicts REQ-STG-4.4. Update Definition. | Hobson | done — same ruling as #13 |
| 27 | IDIOM-FC-1 | Replace map/filter/count with List.forall in FieldMatchChain.doesMatch and ClassificationRule.doesMatch | Dan | accepted |
| 28 | IDIOM-CL-1 | Replace match on List.length with pattern match on list structure in Classifier.classifyCandidate | Dan | accepted |
| 29 | MAINTAINABILITY-TZ-1 | Move timezone constant into appconfig | Dan | done |
| 30 | DB-STAGE-1 | Add created_at/modified_at columns to ingestion.staged_entry and staged_entry_line, extend F# types and persistence (see also #7, #8) | Dan | overruled — StageEntry has a full audit log satisfying the intent; neither StageEntry nor StageEntryLine are first-class entities per Definitions.md. See #38 for follow-on. |
| 31 | STALE-REF-1 | Create Specs/Behavioral/ClassificationRule.md with REQ IDs for the classification rule entity — currently has code but no spec, blocking BD from writing tests (see also #33) | Hobson | accepted |
| 32 | TEST-GAP-1 | Add all missing tests cited in the finding — 4 classification rule CLI routes (NewClassificationRule, FetchById, FetchByName, FetchFiltered) — blocked on #31 | Dan/BD | accepted |
| 33 | STG-MISLABEL-1a | Write ClassificationRule spec to reflect what the code actually does — rule structure, match types, fuzzy logic, active flag (follow-on from #12, overlaps #31) | Hobson | pending |
| 34 | STG-MISLABEL-1b | Change STG-5.3 test annotation to cite the new ClassificationRule spec (follow-on from #12, blocked on #33) | Dan/BD | pending |
| 35 | STG-MISLABEL-1c | Audit classification test coverage against the new spec — find what's untested (follow-on from #12, blocked on #33) | Dan/BD | pending |
| 36 | IDIOM-JE-3a | Move REQ-JE-4.9 UpdateExternalReference no-op test from interface bridge to model orchestrator tests. Rewrite to flow like the comment no-op test (`REQ-JE-4.9 updateComment rejects no-op when both fields are NoChange`). | Dan/BD | accepted |
| 37 | DAL-CANON-1 | Add DAL-efficacy no-findings reasoning to resolved-findings.md as canon — DAL has no behavioral spec, is infrastructure, and its correctness is validated transitively through domain tests | Hobson | done |
| 38 | DB-STAGE-1a | Update Definitions.md to explicitly denote Staged entry and Staged line as non-entities (they have audit logs but are not first-class entities) — follow-on from #30 overruling | Hobson | done |
| 39 | GAAP-SCOPE-1 | Expand GAAP panel auditor scope in the workflow script to include DataIngestion.md and Src/Model/DataIngestion/ — anything that can touch the ledger is within the GAAP mandate | Hobson | done |
| 40 | SD-PROMPT-1 | Tighten statement-delta auditor prompt: flag only factual contradictions ("you said X, repo says not-X"), not inferred gaps from things the statement didn't mention | Hobson | done |

**Deduplication note:** Findings independently raised by multiple auditors:
- **Postable definition vs REQ-STG-4.4:** CON-STG-2 (#3), STG-CONTRA-1 (#13), CON-POSTABLE-1 (#26)
- **Status casing in DataIngestion.md:** CON-STG-1 (#2), SPEC-STG-1 (#9)
- **Staging entity timestamps:** EG-SYS-3.1a (#7), SCHEMA-STG-1 (#8), DB-STAGE-1 (#30)
- **REQ-RPT-1.6 sort order:** TEST-GAP-RPT-1 (#6), RPT-SORT-1 (#23)
- **Classification rule spec gap:** STALE-REF-1 (#31), STG-MISLABEL-1a (#33)

**Audit complete.** 29 auditors across 6 batches. Disposition template: `99-disposition.md`.

## Notes

- **GAAP panel scope** was limited to ledger specs (JournalEntryCrud, AccountCrud, FiscalPeriodCrud, SystemWide, Money). Data ingestion was outside its scope by design — the workflow prompt directed it to `Src/Model/Ledger/` and `Src/ModelOrchestrator/`.
- **#7, #8, #30:** All overruled — staging entities are not first-class entities and have full audit logs. #38 added to document this in Definitions.md.
