# agentic-readiness

## STALE-REF-1 — stale-reference
- **Location:** Specs/Behavioral/DataIngestion.md (REQ-STG-2.16, section 5 intro)
- **Summary:** DataIngestion.md says the classification rule entity "is specified separately" in two places, but no separate behavioral spec exists in Specs/Behavioral/.
- **Resolution:** fix-spec

REQ-STG-2.16 states: "The vendor classification rules entity is specified separately." Section 5 intro repeats: "The rules entity (pattern, priority, FI scoping, account mapping) is specified separately." No ClassificationRule.md or equivalent exists in Specs/Behavioral/. The entity is fully built: 6 code files under Src/Model/DataIngestion/Classification/ (ClassificationRuleComponent.fs, ClassificationRule.fs, ClassificationRuleGroup.fs, FieldMatch.fs, FieldMatchChain.fs, Classifier.fs), orchestration in ModelOrchestrator/ClassificationOrchestration.fs, 4 CLI routes in InterfaceBridge/Routes/IngestionRoutes.fs (NewClassificationRule, FetchClassificationRuleById, FetchClassificationRuleByName, FetchClassificationRuleFiltered), a DB table with 8 columns across 2 migrations, and activate/deactivate behavior. The entity has data states (rule name max 250 chars, code_at_match FK to ledger.account, priority integer, rule_groups JSONB, is_active boolean) and CRUD behaviors (create, fetch by ID/name/filter, update code/priority/groups, activate, deactivate, match evaluation) — none of which have REQ IDs. Existing tests cite only REQ-STG-5.x, which govern the staging pipeline's use of the classifier, not the entity's own validity constraints. The traceability audit (check-traceability.sh) reports clean because there are no REQs to be uncovered — the gap is invisible to the automated guardrail system. This is the highest-risk agentic readiness finding because it means BD cannot write tests for this entity (per TestWriter Phase 1: 'Do not write tests unless you have a behavioral REQ to cite'), and the entity's validation rules exist only in code with no spec-level documentation of what is correct.

**Action:** Create Specs/Behavioral/ClassificationRule.md (or equivalent) with REQ IDs covering the entity's data states (name constraints, code_at_match FK, priority semantics, rule_groups structure, active/inactive, audit timestamps) and CRUD behaviors (create, read, update, activate/deactivate). Remove or update the 'specified separately' references in DataIngestion.md to point at the new spec.

**Why:** The spec-to-test linkage system is the repo's primary guardrail. An entity with no spec is an entity with no guardrails. BD cannot write tests without REQ IDs to cite, the traceability audit cannot detect missing coverage, and no auditor can verify correctness against a spec that does not exist. Classification rules directly determine which account every imported transaction lands in — a rule with a wrong code_at_match, wrong priority, or broken match logic corrupts the ledger silently.

---

## TEST-GAP-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs, Src/InterfaceBridge/Routes/IngestionRoutes.fs
- **Summary:** Four of eight ingestion CLI routes (all classification rule CRUD routes) have no route-level tests.
- **Resolution:** fix-test

IngestionRoutes.fs registers 8 command routes. Route-level tests in Tests/Tests.Integrated/InterfaceBridge/IngestionRoutes.fs cover 4: IngestRawFileToStage (REQ-STG-3.1), UpdateStageEntry (REQ-STG-6.1/6.2), PostStageEntries shadow and real (REQ-STG-8.1/8.4/9.1/9.8), and CreateIngestionSource (REQ-STG-2.4). The 4 untested routes are NewClassificationRule, FetchClassificationRuleById, FetchClassificationRuleByName, and FetchClassificationRuleFiltered. These routes are live — any actor can invoke them through the CLI. Errors in boundary conversion (IngestionFieldConverters.fs), JSON contract deserialization (IngestionContracts.fs), or route-specific validation would not be caught. This finding is a downstream consequence of STALE-REF-1: without REQ IDs for the classification rule entity, BD cannot write tests per TestWriter Phase 1 ('Do not write tests unless you have a behavioral REQ to cite'). The Tests/README hierarchy (section on 'Hierarchy of testing layers') calls for happy-path coverage at every layer, and failure-vector coverage requires route-level sad-path tests for the 'caller gets back an error naming the field they got wrong' vector.

**Action:** After STALE-REF-1 is resolved (classification rule spec created with REQ IDs), write route-level tests for the 4 classification rule CLI routes: happy-path for each, plus sad-path tests for input validation errors surfaced at the boundary.

**Why:** Route-level tests are the only layer that exercises the full CLI contract: JSON deserialization, boundary conversion, domain logic, and JSON serialization. A classification rule created through the CLI with an invalid pattern or a non-existent account code should fail with a typed error. Without route tests, those failure paths exist only in code review, not in the test suite.

---
