# test-efficacy-ClassificationRuleCrud

## EFF-CR-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/ClassificationRuleRoutes.fs:351-353 (REQ-CR-1.26)
- **Summary:** The route test for creating a rule with an invalid pattern proves 'no rule is written' with Result.isError, and the bug it guards against would also produce an error there.
- **Resolution:** fix-test

The test `REQ-CR-1.26 creating a rule whose Source, Description or Memo pattern is not a valid regular expression is rejected with a typed error naming the pattern, and no rule is written` checks its second claim like this:

    let written = name |> ClassificationRule.fetchByName (Context.create ... NoTransaction FetchOnly)
    Assert.True(written |> Result.isError, "no rule should have been written under that name")

That is Specimen 4: it accepts any error at all. It also has a worse problem here. Suppose a rule with the invalid pattern did get written. Under the same REQ-CR-1.26, reading it fails with `IngestionClassificationRuleStoredPatternInvalid` (ClassificationRule.fs confirmStoredPatternsAreValid; asserted by ClassificationRuleCrud.fs:866-895). So fetchByName returns Error, isError is true, and the 'no rule is written' assertion passes in exactly the case it exists to catch. A dropped connection or any other read failure passes it too. The REQ-CR-1.5/4.3 sibling in RevisedRequirementsClassification.fs:184-185 does this correctly: it fetches with a filter and asserts Assert.Empty.

**Action:** Replace the isError check with a typed not-found match on the fetchByName result: the not-found case passes, while Ok and any other Error (including IngestionClassificationRuleStoredPatternInvalid) fail. Alternatively, fetch with a name filter and use Assert.Empty, as RevisedRequirementsClassification.fs:184 does.

**Why:** A typed match proves why the read failed. Here, 'the row is absent' and 'the row is present but unreadable' both come back as Error, and only the first means the requirement held. Result.isError treats them as the same thing, so this assertion cannot tell the requirement holding from the requirement being broken.

---

## EFF-CR-2 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationClaimantsAndRuns.fs:581-586 (REQ-CR-8.5)
- **Summary:** The REQ-CR-8.5 read-back test gets its expected match IDs from RuleMatch.fetchByRunId, the same function the route under test calls to produce its answer.
- **Resolution:** fix-test

The test `REQ-CR-8.5 fetching a run's match rows returns every row of that run and none of another run's, each with the rule's name, claimant and priority` builds its expected value like this:

    let! stored = first.runId |> RuleMatch.fetchByRunId (fresh ())
    let! returned = fetchRun (first.runId |> ClassificationRunId.value)
    Assert.Equal(2, stored.Length)
    Assert.Equal<Set<Guid>>(stored |> List.map (RuleMatch.classificationMatchId >> ClassificationMatchId.value) |> Set.ofList, returned.matches |> List.map _.ruleMatchId |> Set.ofList)

The route's handler (ClassificationRoutes.fs:98-115) calls ClassificationOrchestration.fetchRunMatchesWithRules, and that calls `runId |> RuleMatch.fetchByRunId context` (ClassificationOrchestration.fs:230). Expected and actual both come from the same query (Specimen 6). The test's own name claims 'none of another run's', but nothing checks run membership independently of fetchByRunId. If that query selected the wrong run's rows, both sides would agree and the test would pass. The only independent check is the hard-coded count of 2, which a wrong run holding the same two rows also satisfies, and this test makes such a run (`second`, built from the same candidates).

**Action:** Build the expected set from data that does not go through fetchByRunId. One option is the (stageEntryLineId, ruleId) pairs from `first.results` returned by classifyMatchCandidatesAndRecordMatches, compared with the returned matches' (stageEntryLineId, rule). Another is to also assert that no returned ruleMatchId appears among `second`'s rows.

**Why:** Under Tests/README and Specimen 6, no function in the call chain of the function under test may help derive the expected value. If it does, a defect in that function corrupts the expected and actual values in the same way, and the test ends up checking only that the code agrees with itself.

---

## EFF-CR-3 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationClaimantsAndRuns.fs:371-396 (REQ-CR-5.6)
- **Summary:** Each row of the REQ-CR-5.6 Theory accepts any of three typed errors, so a row is never tied to the error its own field should produce.
- **Resolution:** fix-test

Every InlineData row ('account code', 'payment agreement name', 'claimant type') is checked against the same predicate:

    match attempt with
    | Error (AsError (LedgerError.AccountCodeDoesntMatchAccountId n))
    | Error (AsError (CashFlowError.CashflowPaymentAgreementNameDoesntMatchId n))
    | Error (AsError (IngestionInvalidClassificationClaimantType n)) -> n = missing
    | _ -> false
    Assert.True(namesIt)

So the 'account code' row passes if the filter's account code is wrongly resolved through the payment-agreement-name lookup and fails with CashflowPaymentAgreementNameDoesntMatchId(missing). The 'claimant type' row passes on AccountCodeDoesntMatchAccountId(missing). Specimen 4 calls for a typed case match, but a three-way disjunction checks only that some lookup refused the value, not that the right one did. On failure, Assert.True also reports nothing about which error actually came back. Compare REQ-CR-5.6 in RevisedRequirementsClassification.fs:299-303, which pins the single case it expects.

**Action:** Add the expected case per row (for example, an InlineData discriminator that selects one pattern) and match only that case, with a 'Wrong error' arm that reports e.DomainName.e.CaseName.

**Why:** A typed-error assertion is only as strong as the set of cases it accepts. Each case added to the accepted set is a way for a wrong implementation to pass.

---

## EFF-CR-4 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:774-777 (REQ-CR-6.5); also :107-110 (REQ-CR-4.5)
- **Summary:** The modified_at check for REQ-CR-6.5 is a cowardly inequality, and the REQ-CR-4.5 timestamp check only compares the two timestamps to each other and to MinValue, although the test knows the exact expected instant in both cases.
- **Resolution:** fix-test
- **Prior ruling:** SYS-CLK-1 and IDIOM-JE-1 are related but do not match. SYS-CLK-1 settles REQ-SYS-3.3 wording, not test assertions. IDIOM-JE-1 is limited to a sign-direction test for REQ-JE-3.6.1.

REQ-CR-6.5 (line 774):

    Assert.True((updated |> ClassificationRule.modifiedAt) > (created |> ClassificationRule.modifiedAt), ...)

This is Specimen 2. The test builds `laterContext = context |> Context.updateInitiationInstant`, and updateClassificationRule writes `@modified = context |> Context.getInitiationInstant` (ClassificationOrchestration.fs:302). The exact expected value is therefore `laterContext |> Context.getInitiationInstant`. The `>` check passes on any later instant, such as a wall-clock stamp, a doubled offset or Instant.MaxValue.

REQ-CR-4.5 (lines 107-110) asserts `createdAt = modifiedAt` and `createdAt <> Instant.MinValue`. createNewClassificationRule stamps both from `context |> Context.getInitiationInstant` (ClassificationOrchestration.fs:88), which the test can read. Any two equal non-MinValue instants pass.

The prior ruling SYS-CLK-1 is about the wording of REQ-SYS-3.3 (system clock vs AuditEnvelope), not about test assertion shape. IDIOM-JE-1 accepted `> zero` only because that test was about sign direction. Neither ruling covers this.

**Action:** Assert Assert.Equal(laterContext |> Context.getInitiationInstant, updated |> ClassificationRule.modifiedAt) at line 774. At lines 107-110, assert both timestamps equal context |> Context.getInitiationInstant.

**Why:** Tests/README says 'Asserting equality is preferred to asserting truth. You should know what you expect.' When the exact expected value is in hand, an inequality throws away most of the test's power to detect a fault.

---

## EFF-CR-5 — test-gap
- **Location:** REQ-CR-4.3, REQ-CR-6.3; Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:160-171, 711-719; Tests/Tests.Integrated/InterfaceBridge/ClassificationRuleRoutes.fs
- **Summary:** REQ-CR-4.3 and 6.3 say the account claimant is supplied by account code, but no test sends an unresolvable account code to create or update and checks the typed refusal. The only account-side negative tests pass a random AccountId.
- **Resolution:** fix-test

REQ-CR-4.3: 'validate that the claimant resolves to an existing account (supplied by account code) or an existing payment agreement (supplied by name). If it does not, the creation must fail.' REQ-CR-6.3 says the same for update.

Tests that exist:
- The payment-agreement-name half is tested at the route for create (RevisedRequirementsClassification.fs:173) and update (:376).
- The account half is tested only at the orchestrator, with `ClassificationClaimant.Account(AccountId.create())` (ClassificationRuleCrud.fs:167, :716), asserting AccountIdDoesntMatch. A caller cannot produce that state: the contracts accept `ClassificationClaimantInput.Account code`, and the boundary resolves the code.
- grep for AccountCodeDoesntMatchAccountId in classification tests finds only the REQ-CR-5.6 filter test (ClassificationClaimantsAndRuns.fs:392), not create or update.

Tests/README ('A failure vector is a failed user interaction') treats 'a caller names an account code that does not exist when creating or updating a rule' as its own vector, and its lowest possible layer is the NewClassificationRule / UpdateClassificationRule route. No test covers that vector.

**Action:** Add route-level tests that send ClassificationClaimantInput.Account with a well-formed but nonexistent code to NewClassificationRule and to UpdateClassificationRule. Match the typed AccountCodeDoesntMatchAccountId case carrying that code, and confirm no rule was written or changed.

**Why:** A REQ that names the input form ('supplied by account code') is tested by sending that form. The orchestrator test proves the model rejects an unknown ID. It does not prove that the caller's code resolves to a typed refusal rather than a raw DAL error, which is the leak Specimen 4 documents for ingestion.

---

## EFF-CR-6 — test-gap
- **Location:** REQ-CR-5.4; Src/Business.CrossDomainOrchestration/ClassificationOrchestration.fs:144-151; Tests/Tests.Integrated/CrossDomainOrchestration/ClassificationRuleCrud.fs:503-532; Tests/Tests.Helpers/EntityFunctions.fs:197-215
- **Summary:** The account-code sort is only ever tested on a table of account-claimant rules. Nothing specifies or tests where payment-agreement-claimant rules (which have no account code) land in that sort.
- **Resolution:** dan-decides

REQ-CR-5.4 ('optional sort ordering by account code ... resolved via the account table') was not revised on 2026-09-26, when REQ-CR-1.5 added payment-agreement claimants. The implementation sorts with `order by a.code asc|desc` over `left join ledger.account a on cr.account_at_match = a.unique_id` (ClassificationOrchestration.fs:144-151). Every payment-agreement rule therefore has a NULL sort key, and PostgreSQL puts those last when ascending and first when descending. No REQ states that behavior.

The sort test can't reach this case. Every fixture rule comes from createClassificationRuleForTest, which always builds `ClassificationClaimant.Account` (EntityFunctions.fs:206-207). The test's helper `codeStrOf` (ClassificationRuleCrud.fs:58-65) returns a TestingError for a payment-agreement rule, so the 'code' row would error out rather than check anything if one existed. The route-level filtered-fetch tests always pass `sort = None`.

**Action:** Dan decides where payment-agreement-claimant rules belong under an account-code sort (excluded, first, last, or ordered by payment agreement name) and revises REQ-CR-5.4 to say so. Then add a sort test whose table includes at least one payment-agreement rule.

**Why:** The spec covers two claimant types, but this test only covers one. Behavior the spec leaves unstated and no test reaches becomes whatever the SQL happens to do.

---

## EFF-CR-7 — missing-requirement
- **Location:** Src/Business.FinancialServices.Classification/FieldMatch.fs:15-24; Src/Business.FinancialServices.Classification/Classifier.fs:10-16, 53-65; Tests/Tests.Integrated/CrossDomainOrchestration/StageEntryClassification.fs:289-325 (cites REQ-CR-1.26)
- **Summary:** No REQ specifies that a valid pattern slower than a 1-second match limit fails the whole classification run with IngestionClassificationRulePatternTimedOut. A test asserts this behavior while citing REQ-CR-1.26, which says something narrower and gives a rationale that argues against it.
- **Resolution:** dan-decides

The code sets `matchTimeout = TimeSpan.FromSeconds 1.0` (FieldMatch.fs:17). Classifier.ruleMatches turns RegexMatchTimeoutException into `Error(IngestionClassificationRulePatternTimedOut(ruleUuid, pattern))` (Classifier.fs:14-16). classify combines the per-candidate Results with convertListOfResultsToResultsList (Classifier.fs:63-65), so one slow pattern on one candidate fails the run for every candidate, and classifyMatchCandidatesAndRecordMatches records nothing.

The test `REQ-CR-1.26 a classification run whose valid pattern exceeds the match time limit on an entry fails with a typed error naming the rule, and raises no exception` asserts that whole-run failure and the typed error.

REQ-CR-1.26 says only 'Evaluating a valid pattern never raises an exception.' It says nothing about a time limit or its value, nothing about whether a timeout means 'no match' or 'run fails', and nothing about the error's payload. Its Why reads: 'An invalid pattern accepted at save time fails mid-run and aborts the whole classification for every entry.' That names abort-for-every-entry as the harm to avoid, and the timeout path brings it back. A grep of Specs/Behavioral for 'time limit', 'timeout' or 'timed out' returns nothing.

**Action:** Dan decides the timeout semantics: the limit, whether a timeout aborts the run or only that rule/candidate evaluation (for example, treated as no match), and the error raised. Record the decision as a new REQ-CR so the StageEntryClassification.fs:290 test cites a requirement that describes what it asserts.

**Why:** Tests/README: 'If the code you are testing does something uncited by the REQs, stop and point that out. Likely an REQ needs to be added.' Right now the test pins down a design choice nobody has approved, and that choice goes against the stated reason for the requirement it cites.

---

## EFF-CR-8 — statement-delta
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:43 (and the other IngestionClassification*/IngestionSearchPattern*/IngestionFieldMatchChainEmpty cases); Src/Business.FinancialServices.Classification/Classifier.fs:6; Src/Business.CrossDomainOrchestration/ClassificationOrchestration.fs:16, 246
- **Summary:** Dan says classification is now its own domain and errors were split into domain-specific implementations, but every classification error is still a case of DataIngestionError, and the Classification project has no error type of its own.
- **Resolution:** dan-decides

Dan's statement: 'classification ... now stands as its own domain, servicing both data ingestions and cash flow', and 'I broke out the monolithic error ... into domain-specific implementations of an interface.'

The repo:
- Business.FinancialServices.Classification has no ClassificationError file (scout's project listing; confirmed by the `let` inventory of that project).
- Classification failures are DataIngestionError cases: IngestionSearchPatternInvalidRegex (DataIngestionError.fs:43), IngestionClassificationRulePatternTimedOut (opened in Classifier.fs:6), IngestionClassificationRuleIdDoesntExist (ClassificationOrchestration.fs:246), IngestionClassificationRuleGroupsEmpty, IngestionFieldMatchChainEmpty, IngestionClassificationRuleUpdateNoOp, IngestionClassificationRuleInvalidClaimant, IngestionInvalidClassificationClaimantType, and others.
- Every REQ-CR sad-path test therefore asserts an Ingestion* case (for example ClassificationRuleComponent.fs:30, ClassificationRuleCrud.fs:271/298/700, ClassificationRuleRoutes.fs:345).
- The isolated classification tests are still filed under Tests.Isolated/Model/DataIngestion, with module names `Tests.Isolated.Business.FinancialServices.DataIngestion.*`.

Error ownership was not split out for this domain, so Classification depends on DataIngestion for its failure vocabulary.

**Action:** Dan confirms whether classification errors should move to a Classification-owned IAppError implementation, consistent with the per-domain error split, or whether keeping them in DataIngestionError is intended. If they move, the REQ-CR tests' typed matches move with them.

**Why:** Dan relies on this audit as his back-stop for an agent-written codebase, so his picture of the domain boundaries needs to match the code. The per-domain error split is one of the structural guards he named, and it is missing for this domain.

---

