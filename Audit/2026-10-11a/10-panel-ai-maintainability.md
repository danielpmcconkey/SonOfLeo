# ai-maintainability

## AIM-PLB-1 — enforcement-gap
- **Location:** Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs:16-46; Src/Business.FinancialServices.Ledger/FiscalPeriod.fs:55 (public persist); Src/Business.CrossDomainOrchestration/PositionsLedgerLinks.fs:33; REQ-POS-15.7, REQ-POS-15.8
- **Summary:** The guards that stop pre-ledger balances being stranded (REQ-POS-15.7, 15.8) work only if every caller goes through one orchestrator function. FiscalPeriod.persist and the link writers are public, and no check or DB constraint stops another path from calling them directly.
- **Resolution:** fix-code

REQ-POS-15.7 is enforced only by FiscalPeriodCreation.confirmNoPreLedgerBalanceStranded, called from constructNewAndPersist. REQ-POS-15.8 is enforced only by PositionsLedgerLinks.confirmCarriesNoPreLedgerBalance, which InvestmentAccountOrchestration.confirmInvestmentAccountLink (line 50) and RealEstateOrchestration.confirmAssetLink (line 122) call. Because of the tiering, the Ledger tier cannot know about Positions, so FiscalPeriod.persist (Ledger/FiscalPeriod.fs:55) has to be public. It inserts a period with no pre-ledger check. Today the only call site is FiscalPeriodCreation.fs:45, which I confirmed by grep across Src, DevDataStage and LeoBloomMigrator. pre_ledger_balance has no DB-level rule tying balance_date to ledger.fiscal_period, and the migration comment says "Which accounts and dates may carry one is the application's rule". The 15.7 and 15.8 tests drive the existing orchestrators, so they would stay green if another path appeared. A plausible future example: an ingestion or batch-post slice that auto-creates the missing month, or a Property or InvestmentAccount update helper that writes the link without the confirm step. Either would compile, pass every test, and leave the balances counting for nothing in net worth on pre-ledger dates (REQ-RPT-8.7 reads the ledger for any date in a fiscal period). Checks/ already uses scripts for structural rules like this (check-npgsql, check-clock, check-peer-guard).

**Action:** Add a Checks/ script that fails when FiscalPeriod.persist is called anywhere except FiscalPeriodCreation.fs, and when the InvestmentAccount or Property ledger-link persist/update functions are called anywhere except InvestmentAccountOrchestration and RealEstateOrchestration.

**Why:** The spec's own rationale for 15.7 and 15.8 is that the failure is silent: the balance 'would silently stop counting'. A guard that one future agent can bypass without any build, test or check failing is a convention, not a guard. Net worth history is the planner's input, so the error would spread into it unnoticed.

---

## AIM-TS-1 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:490-530; REQ-SYS-3.2, REQ-SYS-3.3; positions.pre_ledger_balance, positions.investment_activity
- **Summary:** Slice 2 added two timestamped entities, Pre-ledger Balance and Investment Activity. Neither was added to the REQ-SYS-3.2/3.3 theories, and no test anywhere reads their created_at or modified_at.
- **Resolution:** fix-test

Migrations 202610101010 and 202610101020 give investment_activity and pre_ledger_balance created_at and modified_at, so both are entities under Definitions. PreLedgerBalance.replaceBalance (PreLedgerBalance.fs:189-205) is an update path that must stamp modified_at with the initiation instant and leave created_at alone (REQ-SYS-3.3). The REQ-SYS-3.2 and 3.3 theories list exactly these InlineData kinds: Person, Dimension Value, Security, Investment Account, Holding, Account Snapshot, Property, Valuation. A grep for createdAt, modifiedAt, created_at and modified_at across every test file that mentions PreLedger or InvestmentActivity returns nothing. Disposition row #085 of audit 2026-10-04a (HobsonsNotes/brief-test-agent-audit-remediation-2026-10-05.md item 21) set the rule: every Positions entity goes into the 3.3 theory, with one create test per entity. Slice 2 did not carry that forward, and traceability cannot notice because REQ-SYS-3.2/3.3 are already cited.

**Action:** Add 'Pre-ledger Balance' (create, and re-record as the update) and 'Investment Activity' (create) to the REQ-SYS-3.2 and REQ-SYS-3.3 theories in OperationInstantAndAtomicity.fs, and see each fail before it passes.

**Why:** An agent that edits replaceBalance and drops modified_at, or stamps a fresh Clock instant, would pass every test. Cross-cutting requirements cited once per theory are exactly where per-entity coverage decays without anyone noticing, because traceability counts IDs, not entities.

---

## AIM-PEER-1 — enforcement-gap
- **Location:** Checks/check-peer-guard.sh:12-14
- **Summary:** check-peer-guard.sh lists investment modules by hand, and the list was not extended for slice 2. AccountSnapshotLot, InvestmentActivity, InvestmentActivityOrchestration and UnitRollForward are therefore outside the peer rule.
- **Resolution:** fix-code

The investment variable lists DimensionValue, Security, InvestmentAccount, Holding, AccountSnapshotHeader, AccountSnapshotLine and their orchestrators, plus HoldingsAsOf and InvestmentWealthHistory. Slice 2's investment modules are not in it. The result: a real-estate file (Property, Valuation, RealEstateOrchestration) can open InvestmentActivity or AccountSnapshotLot unnoticed, and the new investment files are never scanned for real-estate references. The script does not fail on a Positions module that appears in neither list, so it went stale silently and reports PASS (the plan-positions-2b final report shows 'PASS check-peer-guard'). The Positions spec states the rule: 'They are peers: neither depends on the other'.

**Action:** Add AccountSnapshotLot, InvestmentActivity, InvestmentActivityOrchestration and UnitRollForward to the investment list. Make the script fail when a file in Src/Business.FinancialServices.Positions, or a Positions orchestrator, appears in neither list and is not on an explicit 'shared/combining' allowlist.

**Why:** A hand-kept allowlist that defaults to 'not checked' gets weaker with every slice, and the agents adding modules are the ones least likely to remember the script exists. A check that passes while covering only half the domain is worse than none, because it reports a guarantee it does not give.

---

## AIM-DEF-1 — contradiction
- **Location:** Specs/Definitions.md:54-55 (Component rows); DbMigration/Scripts/202610101000-CreateAccountSnapshotLotTable.sql
- **Summary:** Definitions' Component rows entry names its tables one by one and does not include positions.account_snapshot_lot. By Definitions' own Entity test, a lot is therefore an entity owed REQ-SYS-3.1 timestamps, but the migration creates it without any.
- **Resolution:** fix-spec

Definitions' Entity litmus test: 'does any user action ever insert or update a row? Yes → entity'. The only carve-out is the Component rows entry, which enumerates account_snapshot_line, investment_account_owner, property_owner, property_asset_account and property_mortgage_account. Slice 2 changed Definitions only to add 'Pre-ledger date' (commit 95df0e4). The lot migration's comment says 'A lot is a component of its line, like the line is of its snapshot: no timestamps', and the table has no created_at or modified_at. Definitions ranks above code, so as the repo reads today the lot table violates REQ-SYS-3.1. A fresh agent following the 2026-10-05 remediation brief ('every Positions entity (not component rows — Definitions)') would have to either add timestamps or treat the lot as an exception the spec does not record.

**Action:** Add positions.account_snapshot_lot (part of an account snapshot line) to the Component rows entry in Specs/Definitions.md.

**Why:** Definitions exists so that whether a record is an entity is decided by the spec, not by a migration comment. An enumerated list that slices forget to extend leaves agents to choose between the spec and the schema, and the next one may 'fix' whichever side it reads first.

---

## AIM-LOT-1 — customer-gap
- **Location:** Specs/Behavioral/Positions.md §8 preamble (lines 186-188), REQ-POS-8.3, REQ-POS-6.10; Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs
- **Summary:** Holdings-as-of is the spec's named extract for 'anything that plans from the data'. It gives lots only from the latest snapshot, but lots arrive less often than weekly snapshots. On most dates the planner extract therefore says 'lots not supplied', even when lots were recorded the week before.
- **Resolution:** dan-decides

§8 says holdings-as-of 'is the extract anything that plans from the data consumes'. REQ-POS-8.3 returns the lots of the latest snapshot's lines. REQ-POS-6.10's rationale says institutions report lots 'not every week' and 'the weekly figures must not wait on the lot report'. A test pins the behaviour: 'REQ-POS-8.3 holdings as of a date after a snapshot recorded without lots give that snapshot's lines no lots, not the lots of an earlier snapshot'. No read returns the most recent lots reported on or before a date, or the date of the snapshot they came from. A consumer has to list snapshot dates (REQ-POS-7.5) and fetch them one by one until it finds lots. Dan's statement is that slice 2 'readies the data for my Monte Carlo simulation' so the simulator 'can work out real gains and long- vs short-term treatment', and that deriving lots from activity is the simulator's job. To do that, the simulator needs the last lot-bearing snapshot as its starting point, and the designated extract does not provide or point to it.

**Action:** Dan decides whether holdings-as-of (or a sibling read) should also return, for each line, the most recent lots reported on or before the as-of date together with that snapshot's date, or whether consumers are expected to walk snapshot history themselves.

**Why:** The data is recorded faithfully, but in its current shape the stated consumer, working from the extract the spec designates, gets no lots on most dates. That shortfall would surface only when the simulator is built.

---

## AIM-LOUD-1 — statement-delta
- **Location:** HobsonsNotes/plan-positions-2b-2026-10-10.md:109-112; Checks/check-apperror-coverage.sh:2-5; README.md 'Where the rules live'
- **Summary:** Dan says loud-only rejections are untested by design. That ruling exists only in a HobsonsNotes plan, while the repo's own check states the opposite goal: every error case tested.
- **Resolution:** fix-spec

Dan's statement: 'error cases that only reject loudly are untested by design.' The only written trace of this is plan-positions-2b's Out of scope: 'A loud rejection earns no spec and no test (Dan's ruling)'. HobsonsNotes is not in the authority hierarchy and not in README's 'Where the rules live' table. Checks/check-apperror-coverage.sh's header says 'Tracks the coverage goal: every error case of every IAppError implementation in Src/ is referenced by at least one test', and gives instructions for flipping it to gating. It currently lists 79 untested cases, including slice 2's PositionsActivityRangeListIsEmpty, PositionsPreLedgerBalanceListIsEmpty and PositionsAccountSnapshotLineIdListCannotBeEmpty. Neither CompoundedLearnings nor resolved-findings.md records the ruling (grep for 'loud' finds only unrelated text). A fresh agent therefore cannot tell which of the 79 are deliberately untested and which are real gaps. It may write tests Dan does not want, or flip the check to gating and block merges, or decide the whole list is ignorable and miss a real gap.

**Action:** Record the ruling in CompoundedLearnings (or resolved-findings.md as an overruled precedent), and edit check-apperror-coverage.sh's header so its stated goal matches the ruling (for example, an allowlist of deliberately untested loud-rejection cases, with the rest reported as gaps).

**Why:** Rules that live only in plan notes cannot be found by the agents who need them. When a mechanical check states a goal that Dan has overruled, agents will follow the check.

---

## AIM-FLAKY-1 — test-gap
- **Location:** Tests/Tests.Integrated/DataAccessLayer/DalTests.fs:71-83 (REQ-DAL-2.2 an update requiring exactly one row that touches two ...)
- **Summary:** A known order-dependent test is still at HEAD. It fails on a fresh database whenever it runs before any fixture has populated ledger.account, and the slice reports wave it through as 'pre-existing'.
- **Resolution:** fix-test

The test updates 'ledger.account ... limit 2' and asserts the affected count is 2. The module is not part of the shared fixture collection, so on a brand-new database ledger.account may be empty when it runs, giving 'Expected ExactlyOne. Actual 0'. plan-positions-2-2026-10-10.md:596 reports the failure and says it 'reproduces on origin/main' and 'passes on any second run'. plan-positions-2b-2026-10-10.md:113 puts it out of scope as 'its own fix later', and line 286 notes it 'did not occur on this run ... It depends on test order'. The test was added to resolve audit 2026-10-04a disposition #040, so a fix for one audit finding introduced the instability. It is not in resolved-findings.md.

**Action:** Make the test create its own two accounts inside its rolled-back transaction (or join the fixture collection), so its outcome does not depend on run order.

**Why:** Merge gate step 12 is 'All tests pass'. A failure that agents are told to dismiss as pre-existing teaches every later agent that a red integrated run can be explained away. Then a real regression that fails only on a fresh database can be dismissed the same way.

---

