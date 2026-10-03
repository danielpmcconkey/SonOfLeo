# spec-quality:ClassificationRuleCrud

## CR-NOOP-1 — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-6.2 (line 117); SystemWide.md REQ-SYS-6.1, REQ-SYS-6.1.1; CashFlow.md REQ-CF-14.2; DataIngestion.md REQ-STG-6.3.2
- **Summary:** REQ-CR-6.2 rejects only the all-NoChange update. It never says whether setting a field to the value it already holds counts as a change, and REQ-SYS-6.1.1 says that silence means no-op rejection applies.
- **Resolution:** fix-spec

REQ-SYS-6.1 says an operation must fail when 'the target entity is already in the requested state'. REQ-SYS-6.1.1 says any exception 'must be stated explicitly in the relevant entity spec; absent such a statement, the no-op rejection applies.' On 2026-10-03 Dan amended the matching update REQs in two sibling specs to state the exception. REQ-CF-14.2 says 'a field set to the value it already holds counts as named', with a Why. REQ-STG-6.3.2 says the same. REQ-CR-6.2 was not amended. It says only 'if all fields are NoChange, the update must fail'. The code follows the CF/STG reading: ClassificationOrchestration.updateClassificationRule rejects only `updates.IsEmpty`. So a request whose only change is isActive SetTo true on an already-active rule succeeds and bumps modified_at. Read literally, REQ-SYS-6.1/6.1.1 require that request to be rejected. The spec and the higher-level policy disagree, and the code sides with the policy Dan stated in the other specs, not with the text that governs this one.

**Action:** Amend REQ-CR-6.2 in the REQ-CF-14.2 / REQ-STG-6.3.2 form: 'An update that names no field to change is rejected (REQ-SYS-6.1); a field set to the value it already holds counts as named.' Cite REQ-CF-14.2's Why.

**Why:** REQ-SYS-6.1.1 makes the entity spec the only place an exception may live. Today the behavior the code has and the tests accept rests on a ruling written in two other files, so a developer who follows the CR spec as written would build different behavior from the existing implementation.

---

## CR-ENTITY-TS-1 — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-8.2, REQ-CR-8.4; SystemWide.md REQ-SYS-3.1; Definitions.md 'Entity'; DbMigration/Scripts/202609071135-CreateClassificationTables (classification.rule_match)
- **Summary:** A classification match row meets the Definitions.md Entity test, so REQ-SYS-3.1 requires a 'modified at' timestamp, but REQ-CR-8.2 lists only the run's Instant and the table has only created_at.
- **Resolution:** dan-decides
- **Prior ruling:** DB-STAGE-1 overruled the same complaint for ingestion.staged_entry/staged_entry_line, because Definitions.md explicitly declares those non-entities. That reasoning does not carry over: Definitions.md says nothing about classification match rows, so I am raising this one.

Definitions.md test (1) for an Entity is 'does any user action ever insert or update a row? Yes → entity.' Match rows are inserted when the operator runs classification (REQ-CR-8.1/8.2), so they are entities. REQ-SYS-3.1 says 'Every persisted entity must carry a "created at" and a "modified at" timestamp.' REQ-CR-8.2 lists the row's fields as id, run ID, staged line ID, rule ID and the run's Instant. The rule_match table has `created_at` and no `modified_at`. Definitions.md explicitly exempts staged entries and staged lines as non-entities, and DB-STAGE-1 in resolved-findings.md rests on that exemption. Nothing exempts match rows. Compare REQ-CF-12.1: the Payment Agreement Link, also produced by a classification run, carries 'created/modified Instants'. REQ-CR-8.4 makes match rows immutable, which may be why modified_at was dropped, but no spec text states that exemption.

**Action:** Dan decides: either (a) state in ClassificationRuleCrud.md, or as a Definitions.md note, that match rows are immutable historical records exempt from REQ-SYS-3.1 (as is done for staged entries), or (b) add modified_at to REQ-CR-8.2 and the table.

**Why:** REQ-SYS-3.1 applies to every entity, and Definitions.md is the authority for what counts as one. A silent carve-out invites the next auditor or agent to 'fix' the table, or to copy the omission onto another entity.

---

## CR-WAIVE-1.21 — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md Waived table, REQ-CR-1.21 (line 149); REQ-CR-1.26; SystemWide.md REQ-SYS-2.1; Src/Business.FinancialServices.Classification/ClassificationRule.fs reconstitute
- **Summary:** The REQ-CR-1.21 waiver says read-path Money validation of the rule_groups JSONB is 'not performed by design'. That contradicts REQ-SYS-2.1, and it treats Money differently from REQ-CR-1.26, which requires read-time validation of patterns in the same JSONB column.
- **Resolution:** dan-decides

REQ-SYS-2.1: every operation that '... reconstitutes an entity — create, update, and read-from-persistence alike — must enforce that entity's legal data-state rules.' The waiver goes beyond waiving the test: it declares that the read path does not enforce REQ-CR-1.21 at all. The code confirms this. `reconstitute` deserializes rule_groups with `fromJson<ClassificationRuleGroup list>`, which writes the private `Money` record directly and never calls `Money.fromDecimal`. Stored string patterns have the same bypass, and on 2026-09-26 Dan added REQ-CR-1.26 to close it: 'rejected ... when a stored rule is read'. The code comment reads 'stored patterns are deserialised straight into the pattern type and skip its create, so they are checked here'. The same argument applies to the Money amount in a MoneySearchPattern, yet that field is still unvalidated on read. The waiver also cites a 'Journaling slice precedent' that does not appear anywhere in Specs/Behavioral or resolved-findings.md, so the justification cannot be checked.

**Action:** Dan decides: either require read-time Money validation of stored MoneySearchPatterns, in line with REQ-CR-1.26 (and then test it or waive only the test), or add an explicit exception to REQ-SYS-2.1 for this field and replace the untraceable 'Journaling slice precedent' with a citation that can be checked.

**Why:** A waiver from testing presumes the requirement is enforced. This one records that it is not enforced on the read path, against a higher system-wide requirement, and the spec already handles the identical hazard differently for patterns.

---

## CR-1.18-WS — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-1.18 (line 50); SystemWide.md REQ-SYS-1.2; Tests.Isolated/Model/DataIngestion/ClassificationRuleComponent.fs:80
- **Summary:** REQ-CR-1.18 exempts search patterns from REQ-SYS-1.1 trimming but not from REQ-SYS-1.2, which forbids whitespace-only values in required text fields. The code and a test accept a whitespace-only pattern.
- **Resolution:** fix-spec

REQ-CR-1.18: 'String search pattern cannot be empty. Whitespace is not trimmed — REQ-SYS-1.1 does not apply because whitespace is meaningful in regex patterns.' REQ-SYS-1.2: 'A required (non-nullable) text field may never hold a value that is empty or whitespace-only post-trim.' Only SYS-1.1 is exempted. A developer reading both would reject a pattern of '   ', because SYS-1.2 still forbids whitespace-only values. StringSearchPattern.create rejects only `raw = String.Empty`, and the test `REQ-CR-1.18 StringSearchPattern.create accepts a pattern consisting only of whitespace` asserts acceptance. This is the opposite of the 'requirements may be stricter' pattern: the entity spec allows something the system-wide rule forbids, without stating an exception.

**Action:** Amend REQ-CR-1.18 to say explicitly that neither REQ-SYS-1.1 nor REQ-SYS-1.2 applies, and that a whitespace-only pattern is legal.

**Why:** The intent (whitespace is a meaningful regex) is clear, but the spec text supports both readings, and the test and code follow the reading the spec does not state.

---

## CR-5.4-SORT — ambiguity
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-5.4 (line 109); Src/Business.CrossDomainOrchestration/ClassificationOrchestration.fs fetchRulesFiltered
- **Summary:** REQ-CR-5.4's 'sort by account code' was not revised when payment-agreement claimants were added on 2026-09-26, so it does not say where rules without an account code go.
- **Resolution:** fix-spec

REQ-CR-5.3 and REQ-CR-5.5 were revised on 2026-09-26 for payment-agreement claimants. REQ-CR-5.4 still says 'sort ordering by account code (ascending or descending, resolved via the account table)'. A payment-agreement-claimant rule has no account code. The implementation left-joins ledger.account and orders by `a.code asc|desc`, so PostgreSQL's default null handling decides the result: payment-agreement rules land last on ascending and first on descending. Reasonable developers would differ here: nulls first or last, exclude them, or order them by payment agreement name. The REQ-CR-5.4 code-sort test reverses the ascending list to check descending, which holds only because of the database's null default.

**Action:** Amend REQ-CR-5.4 to state where rules without an account code (payment-agreement claimants) sort under an account-code ordering, for example 'after all account-claimant rules in both directions'.

**Why:** This sort order is visible to the operator, and the spec leaves it undefined for one of the two claimant types it supports.

---

## CR-8.5-SORT — ambiguity
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-8.5 (line 136); Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs fetchClassificationRun
- **Summary:** REQ-CR-8.5's 'sorted by staged line' does not say what key orders staged lines. The implementation sorts by the line's UUID, which carries no meaning for a reviewer.
- **Resolution:** fix-spec

REQ-CR-8.5: 'sorted by staged line, then priority, then rule name.' A staged line has no natural order key given in the spec. One developer would sort by staged line ID (the current code: `List.sortBy (fun m -> m.stageEntryLineId, m.priority, m.classificationRuleName)`, which orders by Guid). Another would sort by parent entry date or entry, then the line's position. The REQ-CR-8.5 ordering test asserts the Guid order. Section 8 exists so 'an operator can review a contested or surprising outcome', and the order shown to that operator differs depending on which reading a developer picks.

**Action:** Amend REQ-CR-8.5 to name the staged-line ordering key: either 'grouped by staged line ID (order between lines unspecified)' or a meaningful key such as entry date, entry ID, then line.

**Why:** Under the 'specs define the what' rule this is an observable output difference, so the spec should settle it.

---

## CR-1.26-TIMEOUT — insufficient-elaboration
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-1.26 (line 52); Src/Business.FinancialServices.Classification/FieldMatch.fs matchTimeout, Classifier.fs ruleMatches; Tests.Integrated/CrossDomainOrchestration/StageEntryClassification.fs:290
- **Summary:** REQ-CR-1.26 says evaluating a valid pattern 'never raises an exception', but it does not say that evaluation is time-limited or what happens when a valid pattern times out. The code and a test cited to REQ-CR-1.26 make a timeout fail the whole run.
- **Resolution:** dan-decides

FieldMatch.fs builds each Regex with a 1-second matchTimeout, so a valid but catastrophically backtracking pattern throws RegexMatchTimeoutException. Classifier.ruleMatches catches it and returns IngestionClassificationRulePatternTimedOut. `classify` collects results with convertListOfResultsToResultsList, so one timed-out rule fails the entire run. The test `REQ-CR-1.26 a classification run whose valid pattern exceeds the match time limit ... fails with a typed error naming the rule` asserts this behavior, which no REQ text states. REQ-CR-1.26's own Why says the goal is to stop a bad pattern that 'fails mid-run and aborts the whole classification for every entry'. A timed-out valid pattern still does exactly that. Developers could reasonably differ: no timeout at all (the run hangs), a timeout that fails the run, or a timeout that treats the rule as non-matching or skips it with a report.

**Action:** Dan decides the timeout policy (time-bounded evaluation, yes or no; on timeout, fail the run or skip the rule). Then add it to REQ-CR-1.26 or as a sibling REQ, so the existing test traces to stated behavior.

**Why:** Both the time limit and the whole-run failure can be seen by the operator on a Saturday run, and today they exist only in code and a test.

---

## CR-NOTE-3.8 — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md design note 'authority hierarchy' (line 7) vs REQ-CR-3.8 (line 88)
- **Summary:** The spec's own design note says 'the classifier only fills null account assignments', but REQ-CR-3.8 says the classifier does not consider whether a line already has an account and leaves that decision to the caller.
- **Resolution:** fix-spec

Line 7: 'The classifier only fills null account assignments; it never overrides parser assignments (REQ-STG-5.3).' REQ-CR-3.8: 'The classifier does not consider whether a candidate line already has an account or a link. It reports which rules matched; deciding what to do with the result belongs to the caller.' Classifier.fs follows REQ-CR-3.8 (comment: 'We do *not* check that the account code is already None'). The note was written when classification was account-only, before it became its own domain serving cash flow as well. A developer who reads the note would put the null-account filter inside the classifier, which would break payment-agreement classification: REQ-CF-12.3 says 'A line's account assignment does not exclude it.'

**Action:** Reword the design note so the null-account rule belongs to the account-classification caller (REQ-STG-5.2/5.3), not to the classifier.

**Why:** Design notes are what agents read to orient themselves, and this one contradicts a REQ in the same file in the direction that would break the cash-flow linkage path.

---

## CR-1.8-XREF — stale-reference
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-1.8 (line 28); DataIngestion.md REQ-STG-5.1, REQ-STG-5.2
- **Summary:** REQ-CR-1.8 cites REQ-STG-5.1 as the source of 'only active rules participate', but REQ-STG-5.1 is about which staged-entry statuses are classified. The active-rule clause is in REQ-STG-5.2.
- **Resolution:** fix-spec

REQ-STG-5.1: 'The system must provide a means to run automated classification against staged entries with status Ingested, NoMatch, or Conflict.' REQ-STG-5.2: 'Classification evaluates each staged line whose account is null against the active classification rules whose claimant is an account.' REQ-CF-12.3 is the payment-agreement counterpart ('against the active rules whose claimant is a Payment Agreement').

**Action:** Change the citation in REQ-CR-1.8 to REQ-STG-5.2 and REQ-CF-12.3 (and REQ-CR-3.2).

**Why:** Cross-references are how agents move between specs, and this one points to an unrelated rule.

---

## CR-1.25-CF-12.4 — contradiction
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md REQ-CR-1.13, REQ-CR-1.14, REQ-CR-1.25; CashFlow.md REQ-CF-12.4; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:576-578
- **Summary:** REQ-CR-1.25 exists to serve REQ-CF-12.4, whose premise is that a rule matches only entry-level fields 'none of which distinguishes one line of the entry from another'. But REQ-CR-1.13 lets rules match the line-level Memo, and the candidate's Amount is also the line's amount.
- **Resolution:** dan-decides

REQ-CF-12.4: 'A rule matches an entry's description, source and amount, none of which distinguishes one line of the entry from another.' REQ-CR-1.13/1.14 allow `Memo` field matches, and REQ-CR-2.2 confirms Memo is per line (it can be None per candidate). CashFlowOps builds each candidate from the staged line's amount, lineType and memo, so in a multi-leg entry Amount differs line to line as well. Under REQ-CR-1.25 only a `LineType` match counts as 'constrains line type'. A payment-agreement rule that singles out one line by memo or amount is therefore treated as unconstrained: REQ-CF-12.4 throws away the lines it actually matched and applies the direction default instead, which can pick zero lines or a different line. The two specs rest on incompatible models of which fields are line-level.

**Action:** Dan decides whether 'constrains line type' (REQ-CR-1.25) should widen to 'selects lines' (any LineType, Memo or Amount match), or whether REQ-CF-12.4's premise should be corrected to acknowledge line-level fields and state that only LineType is honored for leg selection.

**Why:** Leg selection decides which staged line becomes a Payment. A spec premise that contradicts the rule grammar it depends on is how mis-linked payments get through review.

---

## CR-WAIVE-NULL — other
- **Location:** Specs/Behavioral/ClassificationRuleCrud.md Waived table, REQ-CR-1.2 and REQ-CR-1.17 (lines 145-146); Src/Business.FinancialServices.Classification/ClassificationComponent.fs ClassificationRuleName.create, StringSearchPattern.create
- **Summary:** The REQ-CR-1.2 and REQ-CR-1.17 waivers say 'Solution won't build if you try to pass a null value to ... create', which is false. F# `string` accepts the null literal, and no project enables nullness checking.
- **Resolution:** fix-spec

`ClassificationRuleName.create (raw: string)` and `StringSearchPattern.create (raw: string)` take System.String. In F#, `create null` compiles because null is a proper value of System.String. No .fsproj or .props in the repo sets Nullable or --checknulls (grep returned nothing). The function would then throw NullReferenceException at `raw.Trim()` / `raw.Length`. The requirement is still plausibly enforced by other means: rule_name is NOT NULL in classification.classification_rule, and the interface's JSON deserializer rejects null for non-option string fields. So the waivers may well stand, but the reason recorded for Dan's approval is factually wrong.

**Action:** Restate both waiver reasons around the real guarantees (the NOT NULL column for the name, boundary deserialization for both), or make the create functions reject null explicitly.

**Why:** The waiver table records the evidence Dan approved. With fully agentic development, a false premise in that table will be copied as precedent for other waivers.

---

## CR-SD-ERR — statement-delta
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs lines 15-45, 81-100; Src/Business.FinancialServices.Classification/ (no error module)
- **Summary:** Dan says classification is now its own domain and that errors were split per domain so no lower tier has insight into an upper tier. In the repo, every classification error is still a case of the DataIngestion project's DataIngestionError, a lower tier than Classification.
- **Resolution:** dan-decides

Dan's statement: 'classification ... now stands as its own domain' and 'I broke out the monolithic error ... into domain-specific implementations ... no lower tier should have any insight into an upper tier's domain.' Business.FinancialServices.Classification references DataIngestion, not the other way round, and has no error module of its own. DataIngestionError defines IngestionClassificationRuleGroupsEmpty, IngestionClassificationRuleNameIsEmpty/TooLong, IngestionSearchPatternInvalidRegex, IngestionClassificationRuleStoredPatternInvalid, IngestionClassificationRulePatternTimedOut, IngestionInvalidClassificationClaimantType, IngestionClassificationRuleUpdateNoOp and the RecordedInClassificationRun reason, among others. So the lower-tier DataIngestion project names classification-rule, regex-pattern, claimant-type and classification-run concepts that belong to the upper-tier Classification domain. The isolated classification tests also still live under Tests.Isolated/Model/DataIngestion/.

**Action:** Dan decides whether to give Classification its own IAppError implementation (ClassificationError) and move these cases there, or accept the current placement as a documented exception to the tiering principle.

**Why:** Dan relies on this audit as the backstop for the agentic workflow's architectural boundaries. This is a concrete place where the repo does not match his model of it.

---


