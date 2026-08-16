# BD → Hobson Handoff — Staging Test Data

## What we need

A realistic test JSONL file and matching classification rules for the REQ-STG integrated tests. BD is writing 44 orchestrator/posting/update/classification tests that need entries in various lifecycle states, and hand-crafting fixture entries at arbitrary points in the lifecycle is a dead end. The plan is to run one file through the full `ingestRawToStageThenDeduplicateAndClassify` pipeline and test against what it produces.

## Source data

Grab one of Dan's real-life imports from his personal checking account at the credit union. Anonymize it — scramble names, amounts, dates, references. We don't need volume; 20-30 rows is plenty. But we need variety:

- Entries where the debit leg has no `account_code` (needs classification)
- Entries where the parser fully assigns all legs (e.g., a payroll-style multi-leg entry with all codes populated)
- At least one entry whose `fi_reference` matches an existing fixture JE's external reference (will dedup against the ledger)
- At least one entry whose `fi_reference` matches another entry in the same file (will dedup stage-vs-stage)
- At least one entry with a description that won't match any classification rule (produces NoMatch)
- At least one entry where two rules match at the same priority (produces Conflict)
- At least one entry dated in the closed fiscal period (for shadow post failure testing)

## Classification rules

Add rules to `TestDataStage.fs` fixture creation (in the classification rules section, after the existing 3 rules) that mimic real life against the anonymized data. The existing rules are:

1. Source = "TestBank" → F-5300 (priority 1000)
2. Source = "TestBank" && Desc matches DoorDash regex → F-5350 (priority 100)  
3. Source = "TestCreditCardCo" && Desc matches REI regex → F-5650 (priority 10)

Add enough rules that:
- Most entries get a clear single match or a clear priority winner
- At least one entry hits two rules at the same priority (Conflict)
- At least one entry matches nothing (NoMatch)

## Where to put it

- The JSONL file goes in `DevDebugPayloads/` (e.g., `DevDebugPayloads/stg-test-checking.jsonl`)
- Classification rules go in the existing fixture in `Tests/Tests.Helpers/TestDataStage.fs`

## What BD will do after

Rip out the 6 fixture entries I added (fullyAssignedStageEntry, needsClassificationStageEntry, etc.) and rewrite the integrated tests to:

1. Load the JSONL file
2. Run `ingestRawToStageThenDeduplicateAndClassify` inside a rolled-back transaction
3. Assert against the naturally-produced entries in their real lifecycle states

## Fixture cleanup

The ingestion-related fixtures Dan added to `TestDataStage.fs` — ingestion sources, classification rules, and all stage entries — were dev scaffolding, not test infrastructure. BD has the green light to rip all of it out when rewriting the tests. The fixture should go back to being purely ledger-focused. All staging test state will come from the pipeline run against your JSONL file.

This means you don't need to worry about compatibility with the existing ingestion fixtures. Build the rules and data file from scratch to serve the tests. If a new source name makes more sense than "TestBank", use it.

## Context

- The file format spec is in `Specs/Behavioral/DataIngestion.md` §1
- The `BaseStageRawRow` type is in `Src/Model/DataIngestion/BaseStageEntry.fs`
- The existing fixture sources are: TestBank, TestCreditCardCo, ClosedPeriodBank, VoidedEntryBank
- Use "TestBank" as the source for the checking account data (it's the one with the broadest rule coverage)
- Closed fiscal period is at `today.PlusMonths(-5)` — date an entry mid-month in that range
- The voided JE fixture has ext ref (VoidedEntryBank, "VOIDED-REF-001") — don't collide with that unless intentionally testing it
- The posted JE fixture has ext ref (TestBank, "TXN-001") — use "TXN-001" as an fi_reference on one entry to trigger ledger dedup
