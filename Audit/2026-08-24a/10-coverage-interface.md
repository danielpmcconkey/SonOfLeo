# code-inward-coverage-auditor

## TG-ROUTE-CR-1 — test-gap
- **Location:** Src/InterfaceBridge/Routes/IngestionRoutes.fs, lines 52-123
- **Summary:** Five classification rule routes registered in ingestionDomainCommandRoutes have no route-level tests, despite every other route in the system having dedicated route-level tests.
- **Resolution:** fix-test

The following routes are registered in ingestionDomainCommandRoutes (IngestionRoutes.fs lines 227-299) and reachable via the CLI, but no test in the entire test suite exercises them through routeUiCommandForTesting or any equivalent route-level entry point:

1. Ingestion NewClassificationRule (line 52) -- REQ-CR-4.1
2. Ingestion UpdateClassificationRule (line 72) -- REQ-CR-6.1
3. Ingestion FetchClassificationRuleById (line 95) -- REQ-CR-5.1
4. Ingestion FetchClassificationRuleByName (line 105) -- REQ-CR-5.2
5. Ingestion FetchClassificationRuleFiltered (line 115) -- REQ-CR-5.3

Verification: grep for routeUiCommandForTesting.*Ingestion across Tests/ returns hits only for IngestRawFileToStage, PostStageEntries, FetchStageEntryFiltered, UpdateStageEntry, and CreateIngestionSource. Zero hits for any classification rule verb.

Tests DO exist at the orchestrator level in Tests.Integrated/ModelOrchestrator/ClassificationRuleCrud.fs (26F+3T), but those tests call ClassificationOrchestration.createNewClassificationRule, ClassificationOrchestration.updateClassificationRule, and ClassificationOrchestration.fetchRulesFiltered directly, bypassing the route layer entirely.

The route layer performs real boundary conversion work that is not exercised: JSON deserialization of the route-specific input contracts (NewClassificationRuleInput, UpdateClassificationRuleInput, FetchClassificationRuleFilteredInput, etc.), account code to AccountId resolution for the codeAtMatch field, ClassificationRuleGroupContract to ClassificationRuleGroup bidirectional conversion, ClassificationRuleFilterInput to ClassificationRuleFilter conversion (including accountCodeAtMatch to accountAtMatch resolution), and ClassificationRule to ClassificationRuleReturn output conversion.

The codebase itself documents the risk of this gap: the IngestionRoutes test file contains the comment (line 143-144) about FetchStageEntryFiltered -- 'the route that carried a dead column reference through two audits because nothing ever called it.' The five classification rule routes are in the identical position.

**Action:** Add route-level tests for the five classification rule routes. At minimum: a happy-path Fact per route exercising the full round-trip (JSON in, route handler, JSON out), and a validation Theory per route that exercises the boundary conversion error paths (invalid account code for codeAtMatch, invalid classification rule name, etc.).

**Why:** Routes are the observable surface of the CLI. The boundary conversion code they contain (account code resolution, contract type marshalling) is the kind of code that has historically harbored bugs in this codebase when untested at the route level. Orchestrator tests verify domain logic but cannot catch defects in JSON deserialization, boundary type conversion, or the composition of these steps within the route function.

---
