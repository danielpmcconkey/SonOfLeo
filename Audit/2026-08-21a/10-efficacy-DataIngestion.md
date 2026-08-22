# test-efficacy-stg

## BC-STG-1 — test-gap
- **Location:** Tests/Tests.Integrated/ModelOrchestrator/StageEntryClassification.fs, line 62 — REQ-STG-5.4
- **Summary:** The REQ-STG-5.4 test exercises the ManyMatchesClearWinner code path, not OneMatch, because the entry it picks (DoorDash) matches two classification rules.
- **Resolution:** fix-test

REQ-STG-5.4: "When exactly one rule matches and the line's account_code is null, the classifier assigns the rule's account code to the line and records the classification_rule_id on the staged line."

The test picks the DoorDash entry (grp-001, description "DD DoorDash Order 8431927"). The fixture creates two rules that match this entry: (1) the generic "Source = TestBank then 5300" at priority 1000, and (2) the specific "Source = TestBank && Desc = DoorDash then 5350" at priority 100. Both fire. The adjacent REQ-STG-5.5 test (same file, line 81) uses the same entry and explicitly asserts the outcome is ManyMatchesClearWinner, confirming two rules matched.

The ClassifierOutcome DU (ClassificationRuleComponent.fs:114-118) has a distinct OneMatch case. The orchestrator handles them separately (ClassificationOrchestration.fs:186-187), though both currently call the same updateLineWithMatch. The 5.4 test's assertions (Assert.Equal on account code F-5350, Assert.True on classificationRuleId Option.isSome) pass because ManyMatchesClearWinner also assigns the winner's code and rule ID — the test cannot distinguish the two code paths.

Other test entries in the pipeline DO hit OneMatch: MARATHON PETRO (grp-002), HARRIS TEETER (grp-003), and SPECTRUM (grp-004) each match only the generic TestBank rule. But no test asserts on their classification outcomes while citing REQ-STG-5.4. The isolated test for REQ-CR-3.4 (Tests.Isolated/Model/DataIngestion/Classifier.fs:75) covers OneMatch at the model level, but the integrated orchestrator test that should cover it uses the wrong test data.

**Action:** Change the 5.4 test to use an entry that matches exactly one rule — MARATHON PETRO (grp-002) is the simplest candidate. Assert (1) the debit line's account_code is Some "F-5300" (the generic rule's code), (2) the classificationRuleId equals the generic rule's ID (derivable from fixture.Data.classificationRules), and optionally (3) the classification result's outcome is OneMatch.

**Why:** The OneMatch branch in the orchestrator is a distinct code path from ManyMatchesClearWinner. If a regression broke OneMatch specifically — for example, failing to record classificationRuleId on the staged line only when one rule matched — no STG-citing test would catch it. The 5.4 test claims single-match coverage in its name and REQ citation while silently exercising multi-match, which is a false coverage signal to the traceability audit.

---
