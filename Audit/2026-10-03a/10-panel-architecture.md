# architecture-auditor

## ARCH-1 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:12-45; Src/Business.FinancialServices.Classification/{ClassificationComponent.fs:8, ClassificationRule.fs:12, Classifier.fs:6}; Src/Business.CrossDomainOrchestration/ClassificationOrchestration.fs:58,73,353
- **Summary:** The Data Ingestion error vocabulary holds Classification's and Cash Flow's errors and labels, and the Classification container has no error vocabulary of its own.
- **Resolution:** dan-decides

Rules: 'This application is structured in domain tiers and we are strictly forbidden from altering or circumventing the tier hierarchy' (no lower tier understands higher-tier 'modules, types, or even labels'), 'Each domain defines its own errors', and 'Foundations Are the Base of Their Container' (every container's foundations include 'Its error vocabulary'). DataIngestion sits below CashFlow and Classification ('Domains Build Upward': Ledger < DataIngestion < CashFlow < Classification). Its error module still holds: (a) StageLineProtection = LinkedToPaymentAgreement | ReferencedByPayment | RecordedInClassificationRun (lines 12-21), which are cash-flow and classification labels; (b) about 13 classification-domain cases, namely IngestionClassificationRuleGroupsEmpty, ...RuleIdDoesntExist, ...RuleIdListCannotBeEmpty, ...RuleInvalidClaimant, ...RuleNameIsEmpty/TooLong, ...RuleUpdateNoOp, IngestionFieldMatchChainEmpty, IngestionInvalidClassificationClaimantType, IngestionInvalidClassificationGroupConnector, IngestionInvalidNumericSearchOperator, IngestionSearchPattern*, IngestionClassificationRuleStoredPatternInvalid/PatternTimedOut (lines 27-45); (c) IngestionPaidStageEntryCannotBeExcluded ('a Payment references one of its lines'). The Classification project has no error file. Its fsproj compiles ClassificationComponent.fs first, and ClassificationComponent, ClassificationRule and Classifier all open Business.FinancialServices.DataIngestion.DataIngestionError to raise their own errors. ClassificationOrchestration raises IngestionFieldMatchChainEmpty (58), IngestionClassificationRuleGroupsEmpty (73) and IngestionClassificationRuleUpdateNoOp (353). The model matches the gap: under Error management functions it lists 'Define cash flow errors', 'Define data ingestion errors' and so on, but no 'Define classification errors'. Related lower-tier label leaks of the same kind: StageEntryComponent.fs:48 StageStatusChangeMechanism.Classifier ('the process that runs vendor classification rules'), and Business.General/Cadence.fs:403 parameter 'agreementStart' (a cash-flow concept in the business foundation). I excluded the staged-entry statuses Classified/NoMatch/Conflict because Definitions.md (Postable) uses them.

**Action:** Create Src/Business.FinancialServices.Classification/ClassificationError.fs (first in that fsproj's compile list) and move the classification cases there. Move StageLineProtection's cash-flow and classification reasons up to the tier that can see them, for example as an error owned by CashFlow/Classification or orchestration, and translate it per 'Lower level errors are "backstops"'. Rename agreementStart to a concept-neutral name. Add 'Define classification errors' to the model.

**Why:** Dan's statement for this slice says the monolithic errors were split per domain specifically so that 'no lower tier should have any insight into an upper tier's domain'. The DI error type is now the one place where DataIngestion understands Payment, PaymentAgreement, ClassificationRule and classification runs.

---

## ARCH-2 — architecture
- **Location:** Src/App.DataAccessLayer/LookupCache.fs:10,52-63,83-91; Src/Ui.InterfaceBridge/BoundaryConverters/{AccountFieldConverters.fs:24-51,143, FiscalPeriodFieldConverters.fs:16, SharedContractConverters.fs:26, CashFlowLookupConverters.fs:18-73}
- **Summary:** The App-tier LookupCache hard-codes Business tables and concept names, opens its own transaction, and lets the UI read ledger and cashflow tables without going through the owning domains.
- **Resolution:** dan-decides

Rules: 'Tiers Build Upward' ('App references nothing in Business or Ui') together with 'This application is structured in domain tiers and we are strictly forbidden from altering or circumventing the tier hierarchy' (no understanding of higher-tier 'labels'). LookupCache.fs:83-91 defines accountCodeToId/accountIdToCode/accountIdToName over "ledger.account", fiscalPeriodKeyToId/IdToKey over "ledger.fiscal_period", masterAgreementNameToId/IdToName over "cashflow.master_agreement", and paymentAgreementNameToId/IdToName over "cashflow.payment_agreement". Those are Business table names, column names and concept names inside App.DataAccessLayer. Second rule: 'The decision on whether and how to use database transactions belongs in the interface bridge, at the route level'. LookupCache.fetchAll (52-63) calls createDbTransaction() and rolls it back itself, independent of the route's Context. Third, the 'Dependencies Build From the Base Up' doc warns that a downward reference 'can still break another principle: ... breaks encapsulation and domain ownership'. The UI converters (AccountFieldConverters, FiscalPeriodFieldConverters, SharedContractConverters, CashFlowLookupConverters) resolve codes and names by reading these tables through the DAL instead of through Account/FiscalPeriod/MasterAgreement/PaymentAgreement. The file's own line 10 todo ('make this an interface so that lower tier doesn't need to have higher tier awareness') acknowledges the breach. Per precedent SS-3 a todo is not a directive, and it does not make the code conform.

**Action:** Keep the generic Cache in App.DataAccessLayer, but declare the concrete caches (table, column, name) in the owning Business modules (Ledger Account/FiscalPeriod, CashFlow MasterAgreement/PaymentAgreement) or in the bridge converters that use them. Have loadAll use the caller's transaction instead of creating one.

**Why:** This is the only App-tier module that names Business tables. It also creates a transaction outside the route-level decision point the model assigns.

---

## ARCH-3 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:389; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:581,595
- **Summary:** Data-ingestion-level and cash-flow-level orchestrators call the classification-level orchestrator, a higher sibling inside Business.CrossDomainOrchestration.
- **Resolution:** dan-decides

Rule: 'Domains Build Upward', which says inside Business.CrossDomainOrchestration 'the full order again, account-level orchestration basest, classification-level orchestration most specialised ... a piece may use its own concept and lower ones, never a higher sibling'. Also 'Out-of-Order Is a Design Smell' ('the alarm reports it to Dan'). StageEntryOrchestration.classifyAccounts (data ingestion, which the model realizes as the DI capability 'Classify staged entry accounts') calls ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches at line 389. CashFlowOps.classifyPaymentAgreements (cash flow, which the model realizes as the Cash flow capability 'Classify staged lines to payment agreements') calls ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches (581) and ClassificationOrchestration.fetchRulesFiltered (595). Both are sibling orchestrators reaching up the concept ladder. The model's serving edges record these references (model_drift.py reports no drift), so the model documents the breach but no Principle or approved exception sanctions it. The precedent ledger has no matching ruling.

**Action:** Either move classifyAccounts and classifyPaymentAgreements into classification-level orchestration, since the 'For cross domain use cases' principle's Example 1 already places account assignment in Classification, or record an explicit Dan-approved exception for these two calls.

**Why:** These are the two cases inside orchestration where a lower concept depends on a higher one, which is exactly what the out-of-order alarm exists to surface.

---

## ARCH-4 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/Business.CrossDomainOrchestration.fsproj:35-44
- **Summary:** The orchestration compile list is not in concept order: ClassificationOrchestration compiles before the data-ingestion, ledger-report and cash-flow orchestrators, and PrePostingReview compiles after CashFlowOps.
- **Resolution:** dan-decides

Rules: 'Build Order Follows the Hierarchy' ('each .fsproj's Compile Include list follow[s] the rungs: a file compiles after everything below it and before everything above it') and 'Domains Build Upward' (inside orchestration the concept order is account < fiscal period < journal entry < data ingestion < cash flow < classification). Actual order after the JE orchestrators: TrialBalance, ClassificationOrchestration (line 35, classification), StageEntryOrchestration (36, DI), Reconciliation (37), BalanceSheetIntegrity (38) and PeriodActivity (39), which are ledger reports built only on AccountBalance/AccountActivity, then CashFlowCompositeFetcher, InstanceOrchestration, AgreementOrchestration, CashFlowOps (40-43, cash flow), and finally PrePostingReview (44). The model realizes PrePostingReview as the DI capability 'Compute the pre-posting review', and it references only StageEntryOrchestration among its siblings. The out-of-order position of ClassificationOrchestration is what lets ARCH-3's upward calls compile.

**Action:** Reorder to FetchFilterAndSort, then the account-level files, FiscalPeriodCreation, the JE-level files and ledger reports (TrialBalance, BalanceSheetIntegrity, PeriodActivity), then the DI files (StageEntryOrchestration, Reconciliation, PrePostingReview), then the cash-flow files, then ClassificationOrchestration last. Do this after ARCH-3 is resolved.

**Why:** Compile order is the mechanical guardrail Dan chose so that agent developers cannot quietly place a component on the wrong rung. Here it currently permits the upward sibling calls.

---

## ARCH-5 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/Business.FinancialServices.Ledger.fsproj:20; Src/Business.FinancialServices.DataIngestion/...fsproj:17; Src/Business.FinancialServices.CashFlow/...fsproj:18; Src/Business.FinancialServices.Classification/...fsproj:17; Src/App.Operation/App.Operation.fsproj:10-12
- **Summary:** Every domain-specific auditable-action module compiles last in its container instead of at the foundation, and in App.Operation CoreAuditableAction does not sit directly above IAuditableAction.
- **Resolution:** dan-decides

Rule: 'Foundations Are the Base of Their Container', which says 'Its error vocabulary and its auditable actions (e.g. LedgerError and LedgerAuditableAction in Ledger ...). Nothing else in the container is below them.' and 'Foundations compile first in their container'. It also says the interface definition is 'the very bottom and the container's own implementation sits just above it: ... IAuditableAction below CoreAuditableAction in App.Operation'. Actual positions: LedgerAuditableAction.fs is the last Compile Include in Ledger (line 20), DataIngestionAuditableAction last in DataIngestion (17), CashFlowAuditableAction last in CashFlow (18), ClassificationAuditableAction last in Classification (17). In App.Operation the order is IAuditableAction (10), AuditEnvelope (11), CoreAuditableAction (12). None of these modules references anything else in its container (each opens only App.Operation.IAuditableAction), so the placement is not forced by a dependency.

**Action:** In each Business fsproj, move the *AuditableAction.fs entry next to the error file at the top. In App.Operation, order IAuditableAction, CoreAuditableAction, AuditEnvelope.

**Why:** The model names auditable actions as container foundations. Placing them last inverts the rung the model assigns.

---

## ARCH-6 — architecture
- **Location:** Src/Ui.InterfaceBridge/Ui.InterfaceBridge.fsproj:11-13
- **Summary:** CommandRoute (the routes-layer base) and Startup (the interface-level runner) compile before the contracts and converters layers they sit above.
- **Resolution:** dan-decides

Rules: 'UI Layers Build Upward' (contracts < converters < routes < interface) and 'Foundations Are the Base of Their Container' ('SharedContracts is the base of the UI contracts layer; CommandRoute is the base of the UI routes layer'), checked through 'Build Order Follows the Hierarchy'. Ui.InterfaceBridge.fsproj compiles BridgeError (10), CommandRoute (11), Startup (12), and then InterfaceContracts\SharedContracts (13). CommandRoute defines the CommandRoute/ReportRoute records and the route transaction runners, which makes it routes-layer. Startup.run is documented as 'the interfaces' outermost frame' and is called only from Ui.OperatorCli/Program.fs and Ui.ReportCli/Program.fs. Both therefore sit on the bottom rung of the bridge, below SharedContracts. A side effect: the Opens check treats 'open Ui.InterfaceBridge.CommandRoute' after the converters (for example ClassificationRoutes.fs:20, OperatorCli Program.fs:9) as out of order, even though it is correct by layer.

**Action:** Move CommandRoute.fs to just before Routes\AccountRoutes.fs. Either place Startup.fs after the routes, or move it into the interface projects, or record it as a bridge foundation in the model's Foundations principle.

**Why:** The layer order inside the bridge is how the model keeps routes from becoming a dependency of contracts and converters. Compile order is what enforces it.

---

## ARCH-7 — architecture
- **Location:** Src/App.Session/App.Session.fsproj:14-16; Src/Business.FinancialServices.Ledger/...fsproj:24-28; Src/Business.FinancialServices.DataIngestion/...fsproj; Src/Business.FinancialServices.CashFlow/...fsproj; Src/Business.FinancialServices.Classification/...fsproj:21-27; Src/Business.CrossDomainOrchestration/...fsproj:10-18; Src/Ui.InterfaceBridge/Ui.InterfaceBridge.fsproj:50-53
- **Summary:** ProjectReference lists in seven of the fourteen Src projects are not ordered base-first.
- **Resolution:** dan-decides

Rule: 'Build Order Follows the Hierarchy' ('project references ... follow the rungs ... foundations first'), restated in 'Dependencies Build From the Base Up' ('project references, the Compile Include order of each .fsproj, and the open statements of each file all run base first'). Actual orders: App.Session lists DAL, Operation, Utility (Utility last). Ledger lists Session, DAL, Utility, FinServ, General. DataIngestion lists Ledger, Utility, FinServ. CashFlow lists DataIngestion, Ledger, Utility, FinServ, General. Classification lists Session, DAL, CashFlow, General, DataIngestion, Ledger, Utility. CrossDomainOrchestration lists Session, DAL, CashFlow, Classification, DataIngestion, Ledger, FinServ, General, Utility (Classification before DataIngestion/Ledger, Utility last). Ui.InterfaceBridge lists Session, DAL, CDO, Utility. Only DAL, Operation, General, FinancialServices and the two CLIs (single reference, or Utility only) conform.

**Action:** Reorder each ProjectReference ItemGroup base-first: App.Utility, then App.DataAccessLayer/App.Operation, App.Session, Business.General/Business.FinancialServices, Ledger, DataIngestion, CashFlow, Classification, CrossDomainOrchestration.

**Why:** This is the listing-order half of the principle the model says the constraint realizes. Nothing checks it mechanically today: check-compile-order.sh verifies only file membership.

---

## ARCH-8 — architecture
- **Location:** 113 open pairs in 56 Src files, e.g. App.DataAccessLayer/ExecuteScalar.fs:8; Business.FinancialServices.Ledger/JournalEntryComment.fs:5; Business.FinancialServices.DataIngestion/StageEntryHeader.fs:12; Business.FinancialServices.CashFlow/Payment.fs:7; Business.CrossDomainOrchestration/{FetchFilterAndSort.fs:11, JournalEntryVoiding.fs:11, InstanceOrchestration.fs:16, PrePostingReview.fs:15}; Ui.InterfaceBridge/InterfaceContracts/ClassificationContracts.fs:5-7; Ui.InterfaceBridge/BoundaryConverters/ReportConverters.fs:15,18; Ui.InterfaceBridge/Routes/ReportRoutes.fs:10,14
- **Summary:** Open statements are out of build order throughout Src: 113 adjacent pairs in 56 files, 29 of which cross tiers or projects.
- **Resolution:** dan-decides

Rule: 'Opens Follow Build Order' ('open statements for Src modules list them in build order: baser modules first ... An open that has to break that order is reported to Dan, not a formatting choice'). I compared each file's opens against tier/project rank and compile index, treating DAL/Operation and General/FinancialServices as unordered peers and excluding pairs caused by CommandRoute's misplacement (ARCH-6). Result: 113 out-of-order pairs in 56 files (DAL 7, Ledger 5, DataIngestion 11, CashFlow 16, Classification 6, CDO 28, Ui.InterfaceBridge 40). Cross-tier and cross-project examples: ExecuteScalar.fs:8 opens App.Utility.Result after App.DataAccessLayer.DalError. JournalEntryComment.fs:5 opens App.Utility.IAppError after DAL modules. StageEntryHeader.fs:12 opens Ledger.JournalEntryComponent after DataIngestion.IngestionSource. Payment.fs:7 and PaymentAgreementLink.fs:5 open App.Utility after DataIngestion. FetchFilterAndSort.fs:11 opens Ledger.AccountComponent after CashFlow.CashFlowComponent. InstanceOrchestration.fs:16 opens DataIngestion after CashFlow. PrePostingReview.fs:15, ReportConverters.fs:18 and PrePostingReviewWriter.fs:13 open CashFlow after Classification. JournalEntryVoiding.fs:11 opens Business.FinancialServices.Ledger after a CDO module. ClassificationContracts.fs:5,7 open CDO FetchFilters after AccountContracts and App.Utility.FieldUpdate after CDO. ReportConverters.fs:15 opens a Ledger component after ReportsContracts. ReportRoutes.fs:10,14 open App.Utility and CDO.BalanceSheetIntegrity after converters. Intra-project examples: ExecuteReader.fs:12 (DbConnection after QueryParameter), Account.fs:7,10, StageEntryOrchestration.fs:18. None of these opens is forced by a name collision that I could see, and the precedent ledger has no approved exception.

**Action:** Reorder each file's Src opens base-first (App.Utility, then DAL/Operation, Session, General/FinServ, Ledger, DataIngestion, CashFlow, Classification, CDO, bridge layers by layer and concept). Where an out-of-order open is needed for shadowing, record the approved exception. Consider adding a Checks/ script, since none currently covers opens.

**Why:** The model says an out-of-order open is an alarm for Dan, not a style nit. With 113 instances the alarm cannot currently signal anything.

---

## ARCH-9 — architecture
- **Location:** Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:26-97,153-172; Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:554-571; Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:458-467
- **Summary:** Ingestion rules, post-and-snapshot orchestration and a fetch-and-merge of current agreement state are implemented in the UI tier, and the Business orchestrator the model credits with ingestion is bypassed by the real route.
- **Resolution:** dan-decides

Rule: 'Business logic is not allowed in the UI tier'. (1) IngestionRoutes.ingestRawEntries (26-97) implements the file-acceptance algorithm itself: line numbering that counts blank lines, per-record conversion, excluding any group that lost a record from group checks, mapping group failures back to line numbers, sorting failures into file order, and building DataIngestionError.IngestionFileRejected before calling StageEntryOrchestration.constructFromRaw/persistConstructed. The model says StageEntryOrchestration realizes 'Ingest raw rows to stage', but its ingestRawToStage (458-467) is called only from Tests (StageEntryIngestion.fs:115, StageEntryFetching.fs:690). Its own comment says 'a caller handing over rows rather than a file gets the first failing group; the route reports every one'. The operator-facing behavior therefore lives in Ui. (2) postWithExternallyManagedTransaction (153-172) orchestrates a business use case in the route: trial balance before, StageEntryOrchestration.post, trial balance after. (3) The converter 'convert [UpdateAgreementInput] to [MasterAgreementFieldUpdates]' (CashFlowFieldConverters.fs:554-571) fetches the current MasterAgreement from the database, merges unchanged dates from current state, and builds a new ActivityPeriod. That is state-dependent domain logic in a converter.

**Action:** Move file-level ingestion validation and aggregation into a StageEntryOrchestration function the route calls, and retire or repurpose ingestRawToStage. Move the post-with-trial-balance snapshot into orchestration. Have AgreementOrchestration.updateAgreement accept begin/end FieldUpdates and do the merge itself.

**Why:** Tests exercise the Business orchestrator while operators run a different implementation in Ui. The model's capability-to-component mapping for 'Ingest raw rows to stage' does not describe the code path production uses.

---

## ARCH-10 — architecture
- **Location:** Architecture/SonOfLeo.archimate (Data ingestion functions / Cash flow functions); Src/Ui.InterfaceBridge/Routes/ClassificationRoutes.fs:117-170,174+; Src/Business.FinancialServices.Classification/ClassificationAuditableAction.fs:6-10; Src/Business.CrossDomainOrchestration/{StageEntryOrchestration.fs:364, CashFlowOps.fs:541,622}
- **Summary:** Domain ownership of the classification use cases disagrees across the model's capability folders, the route domain, the auditable-action vocabulary and the orchestrators.
- **Resolution:** dan-decides

Rules: 'The Classification domain owns classification capabilities', 'The Cash Flow domain owns cash flow capabilities', 'The Data Ingestion domain owns data ingestion capabilities', and 'For cross domain use cases, the use case should be considered to be a component of the domain tier that best aligns to the use case' (Example 1: assigning an account to a staged entry 'is part of the Classification domain'). The model files 'Classify staged entry accounts' under Data ingestion functions, realized by StageEntryOrchestration. That contradicts the principle's own Example 1. The model files 'Classify staged lines to payment agreements', 'Create a payment agreement link', 'Update payment agreement link' and 'Delete a payment agreement link' under Cash flow functions, realized by CashFlowOps/PaymentAgreementLink. In code, all five are exposed as domain "Classification" routes (ClassificationRoutes.fs: classifyAccounts 117, createPaymentAgreementLink 126, updatePaymentAgreementLink 140, deletePaymentAgreementLink 151, classifyPaymentAgreements 162), audited as ClassificationAuditableAction cases (ClassifyAccounts, ClassifyPaymentAgreements, Create/Delete/UpdatePaymentAgreementLink), and orchestrated by a DI orchestrator (StageEntryOrchestration.classifyAccounts) and a cash-flow orchestrator (CashFlowOps.classifyPaymentAgreements, constructNewPaymentAgreementLinkAndPersist). So each of these use cases has three different owners depending on where you look.

**Action:** Decide one owning domain per use case. Then move the model ApplicationFunction to that domain's folder, put the route under that domain, put the auditable-action case in that domain's vocabulary, and place the orchestrator at that domain's rung (see ARCH-3).

**Why:** Domain ownership is what the tier rules are evaluated against. With three answers per use case, the out-of-order alarm cannot tell whether a call is legitimate.

---

## ARCH-11 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/{AccountDeactivation.fs:20-40, JournalEntryVoiding.fs:49, JournalEntryCommentOrchestration.fs:66, JournalEntryExternalReferenceOrchestration.fs:45, ClassificationOrchestration.fs:291}; Src/Business.FinancialServices.Ledger/{Account.fs:226, FiscalPeriod.fs:137}
- **Summary:** Several entities have no 'update' backbone in their module. Their UPDATE SQL lives in orchestration or under other names, and 'update'-prefixed functions do not call a module update function.
- **Resolution:** dan-decides

Rules: 'Name functions that provide the basic database update "backbone" for an entity type "update"' and 'Prefix function names with "update" when they call their module's update function to update DB rows in specific manners'. The CashFlow entities, StageEntryHeader, StageEntryLine and Account carry an `update` backbone. By contrast: AccountDeactivation.updateActiveEnd (20-40) writes its own 'UPDATE ledger.account' instead of calling Account.update, which is private (Account.fs:226) and cannot set active_end. JournalEntryHeader, JournalEntryComment and JournalEntryExternalReference have no update function; their UPDATEs live in orchestration as JournalEntryVoiding.voidById (49), JournalEntryCommentOrchestration.updateComment (66) and JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText (45). ClassificationRule has no update; ClassificationOrchestration.updateClassificationRule (291) holds the SQL. FiscalPeriod's backbone is named toggleOpenFlagById (137). updateActiveEnd, updateComment, updateFiAndReferenceText and updateClassificationRule are 'update'-prefixed but call no module update function.

**Action:** Give Account (active_end), JournalEntryHeader (voided_at), JournalEntryComment, JournalEntryExternalReference and ClassificationRule an `update` backbone in their entity modules, rename FiscalPeriod.toggleOpenFlagById to update, and have the orchestrators call those.

**Why:** Naming the backbone 'update' is how the model locates each entity's write path. Today, for five entities, the write path sits outside the entity module.

---

## ARCH-12 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/{ClassificationOrchestration.fs:80, StageEntryOrchestration.fs:305, InstanceOrchestration.fs:779, CashFlowOps.fs:622}
- **Summary:** Orchestration functions that validate and save a new entity are not named constructNewAndPersist.
- **Resolution:** dan-decides

Rule: 'Name orchestration functions that both validate an entity and save it to the DB "constructNewAndPersist"'. Several functions validate then persist under other names. ClassificationOrchestration.createNewClassificationRule (80) runs confirmRuleGroups, confirmClassificationClaimant and ClassificationRule.persist. StageEntryOrchestration.createNewSource (305) runs a uniqueness check and IngestionSource.persist. InstanceOrchestration.createInstanceCompositeAndSaveToDb (779) constructs, confirms and persists. CashFlowOps.constructNewPaymentAgreementLinkAndPersist (622) is a variant spelling. The other orchestrators follow the rule: AccountCreation, FiscalPeriodCreation, JournalEntry*Orchestration and AgreementOrchestration all expose constructNewAndPersist.

**Action:** Rename them to constructNewAndPersist, moving functions into dedicated modules where a module would otherwise hold two such functions, or record the variant spelling as an accepted pattern.

**Why:** Agents find the create path by this name. The cash-flow and classification slice is where the convention lapsed.

---

## ARCH-13 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/StageEntryHeader.fs:183-193; Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:434-456
- **Summary:** StageEntryHeader.persist writes two rows, and StageEntryOrchestration.persistConstructed returns its input entities instead of unit.
- **Resolution:** dan-decides

Rules: 'Name functions that write a single new row to the DB "persist"' and 'Persist functions should only return unit' ('The caller should already have the record they are persisting'). StageEntryHeader.persist (183-193) calls persistRow, which inserts into ingestion.staged_entry, and then updateHeaderStatus, which inserts into ingestion.staged_entry_audit. That is two rows under the name 'persist', while a single-row persistRow exists alongside it. StageEntryOrchestration.persistConstructed (434-456) has the signature Result<StageEntry list, IAppError> and ends with 'return entries', handing back the list it was given.

**Action:** Rename StageEntryHeader.persist to describe the header-plus-initial-transition write, or make callers do the two steps, so that 'persist' means one row. Make persistConstructed return Result<unit, IAppError> and have the route keep its own list.

**Why:** The persist contract (one row, unit result) is what lets callers reason about writes without reading the function body.

---

## ARCH-14 — architecture
- **Location:** Src/Business.FinancialServices.DataIngestion/StageEntryHeader.fs:325-328; Src/Business.CrossDomainOrchestration/AccountBalance.fs:64,66
- **Summary:** SQL values are interpolated into query text in two places instead of being passed as parameters.
- **Resolution:** dan-decides

Rule: 'SQL values are always passed as parameters, never interpolated into the SQL'. StageEntryHeader.fetchBySourceFile builds `and latest_statuses.to_status in ('Ingested','Classified',...)` with $"'{x |> StagedEntryStatus.toString}'" (line 327), with the comment 'direct interpolation is okay since this is directly pulled from the DU'. AccountBalance.fetchByAccountIdList puts `select '{Credit |> JournalEntryLineType.toString}' as line_type` and the Debit equivalent (64, 66) into the CTE. Both are data values (status strings and line-type strings compared to column data), not structural SQL. Every other variable list in Src (the `in ({names})` lists) is built from @-parameter names.

**Action:** Pass the status strings and line-type strings as @parameters, using the same names-plus-parameters pattern used elsewhere.

**Why:** The rule makes no exception for code-sourced values. The inline comment is an agent exempting itself from a constraint, which the model reserves for Dan.

---

## ARCH-15 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/{StageEntryOrchestration.fs:55-56, AccountBalance.fs:64,66,110,115, AccountDeactivation.fs:72-73, PeriodActivity.fs:58-59, PrePostingReview.fs:158,192, JournalEntryOrchestration.fs:39-40}
- **Summary:** Common DU cases (Debit, Credit, Posted) are used unqualified in files that open several modules defining them.
- **Resolution:** dan-decides

Rule: 'Fully qualify common DU case names when several modules are opened to avoid name collisions' (doc: 'ex: Posted, Active, Debit, Credit'). These files open both Ledger.AccountComponent (AccountTypeNormalBalance = Debit | Credit) and Ledger.JournalEntryComponent (JournalEntryLineType = Debit | Credit), yet use bare Debit/Credit: StageEntryOrchestration.fs:55-56, AccountBalance.fs:64,66,110,115, AccountDeactivation.fs:72-73, PeriodActivity.fs:58-59, PrePostingReview.fs:192, JournalEntryOrchestration.fs:39-40. PrePostingReview.fs:158 matches bare `Posted _` while opening both StageEntryComponent (StagedEntryStatus.Posted) and CashFlowComponent (TransactionPointer.Posted). Each currently resolves only because of which module is opened last, and ARCH-8's reorder would keep or change that silently.

**Action:** Qualify these as JournalEntryLineType.Debit/Credit and TransactionPointer.Posted.

**Why:** Resolution that depends on open order is fragile in exactly the files ARCH-8 asks to reorder.

---

## ARCH-16 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:49-50,161,308-313; Src/Business.CrossDomainOrchestration/AccountBalance.fs:16; Src/Business.CrossDomainOrchestration/CashFlowOps.fs:714
- **Summary:** Several component and composite types are public records or public wrapper cases, and their fields are read directly instead of through accessors.
- **Resolution:** dan-decides

Rules: 'Entity, component, and composite type definitions are private' and 'Access to Entity, component, or composite type fields is strictly through accessor functions'. In CashFlowComponent, DebitAccount/CreditAccount (49-50) are public single-case wrappers, unlike the `private` ids and strings around them. InvoiceLifeCycleState (161) is a public record. InvoiceDate, DueDate, PostedToFiDate, PostedToLedgerDate, InvoiceAmount and PaymentAmount (308-313) are public records such as `{ localDate: LocalDate }` and `{ money: Money.Money }`. The composite AccountBalance.AccountBalance (AccountBalance.fs:16) is public, and CashFlowOps.fs:714 reads balance.accountId and balance.netBalance directly. Compare ProjectionHorizonInDays and DaysDueAfterInvoiceDate (284, 296), which are private records in the same file, and Ledger/DataIngestion components, which are private.

**Action:** Make these types private, expose create/value or accessor functions, and update the call sites.

**Why:** The privacy constraint is how the model keeps instantiation and validation inside each module. These are the cash-flow slice's exceptions to it.

---

## ARCH-17 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountBalance.fs:19-23; Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:36-38
- **Summary:** Component types are defined in the orchestration tier.
- **Resolution:** dan-decides

Rules: 'Any component type in the cross domain orchestration or higher is a signal that we're violating the tiering or domain principles somewhere' and 'Component types are defined in a components module in the lowest tier of their business domain'. AccountBalance.fs:19 defines `type AccountBalanceComponent = private { accountId; lineType; accountType; sumAtType }`, a component by its own name, in Business.CrossDomainOrchestration. StageEntryOrchestration.fs:36 defines `type AccountValidationType = AllowNone | DisallowNone`, a domain flag DU, in orchestration rather than StageEntryComponent.

**Action:** Move AccountValidationType to StageEntryComponent. Either move AccountBalanceComponent into Ledger (for example JournalEntryComponent or AccountComponent) or replace it with a tuple, per the model's guidance on avoiding convenience types.

**Why:** The model names this exact situation as a signal to raise with Dan.

---

## ARCH-18 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/StageEntryOrchestration.fs:522-527
- **Summary:** StageEntryLineAddition re-declares StageEntryLine's business fields minus the not-yet-known ones, which the model explicitly forbids.
- **Resolution:** dan-decides

Rule: 'Avoid creating types strictly for code convenience'. Its doc says not to create 'types that would comprise all of the "business" fields of those types, but without the not-yet-known fields ... Instead, create tuples of the primitives ... and pass them into their respective constructNewAndPersist functions'. StageEntryLineAddition = { amount: Money; lineType: JournalEntryLineType; accountId: AccountId option; memo: JournalEntryLineMemo option } is StageEntryLine (StageEntryLine.fs:19-26) without stageEntryLineId, stageEntryHeaderId and journalEntryLineId. The cash-flow slice follows the rule: InstanceCompositeUpdate.newInvoices and InvoiceCompositeUpdate.newPayments use tuples.

**Action:** Replace StageEntryLineAddition with a (Money * JournalEntryLineType * AccountId option * JournalEntryLineMemo option) tuple, matching the cash-flow pattern.

**Why:** It is the exact anti-pattern the model's example describes.

---

## ARCH-19 — architecture
- **Location:** Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs:52,102; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:197,273
- **Summary:** The input contract ReconciliationInput is reused by two use cases: the Reconciliation report and the operator ShadowReconcile command.
- **Resolution:** dan-decides

Rule: 'Input contract types should not be re-used. they are use-case specific'. ReportRoutes.reconciliation and IngestionRoutes.shadowReconcile both deserialize Json.fromJson<ReconciliationInput> and declare inputContract = typeof<ReconciliationInput>. Reporting.md REQ-RPT-4.4/4.6 define these as separate operations (one a read-only report, one a rolled-back command) and do not require a shared input shape. Every other route has its own input contract, apart from the NoInput marker, which I did not flag because it marks the absence of input.

**Action:** Introduce a ShadowReconcileInput contract, even if it is structurally identical today, and a matching converter.

**Why:** The model wants input contracts to evolve with their use case. A shared one couples the report side to the operator side.

---

## ARCH-20 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/Account.fs:52-54; Src/Business.FinancialServices.CashFlow/MasterAgreement.fs:64-66; Src/Business.General/ActivityPeriod.fs:67-75
- **Summary:** Account.create and MasterAgreement.create contain a code path that can throw, through ActivityPeriod.insistBeginValidationBehavior's failwith.
- **Resolution:** dan-decides

Rule: 'Infallible Create, Orchestrator Validates' ('Create (construct) functions in Entity-level type modules never fail ... No exceptions in constructors.'). Account.create (line 54) and MasterAgreement.create (66) pipe their ActivityPeriod through ActivityPeriod.insistBeginValidationBehavior, which re-runs ActivityPeriod.create and does `Result.defaultWith(fun e -> failwith(e.ToMessage()))` (ActivityPeriod.fs:75). The comment there argues the path is unreachable because the input was already valid. The constructor still contains an exception path, and nothing in the type guarantees the argument.

**Action:** Give ActivityPeriod a total function that changes only the behavior flag on an existing value (for example a private record copy-with), so that the entity constructors contain no exception path.

**Why:** The rule bans exceptions in constructors outright. An 'unreachable' throw is a construction-time exception path all the same.

---

## STMT-1 — statement-delta
- **Location:** Dan's statement, sections 'cash flow' and 'refactor'
- **Summary:** Dan says Classification 'now stands as its own domain' and that errors were split so 'no lower tier should have any insight into an upper tier's domain'. The repo only partly bears this out.
- **Resolution:** dan-decides

What the statement says and what the repo shows: (1) Classification's errors still live in DataIngestionError, and the Classification project has no error type (ARCH-1). (2) Account classification is orchestrated by the data-ingestion orchestrator, StageEntryOrchestration.classifyAccounts, and the model files 'Classify staged entry accounts' under Data ingestion functions (ARCH-10). (3) Both DI-level and cash-flow-level orchestrators call up into ClassificationOrchestration (ARCH-3). (4) Classification's isolated tests still live under Tests/Tests.Isolated/Model/DataIngestion/ (ClassificationRuleComponent.fs, Classifier.fs, FieldMatch*.fs, ClassificationRule*.fs). (5) The App tier's LookupCache knows ledger and cashflow tables (ARCH-2), and DataIngestion knows Payment/PaymentAgreement labels (ARCH-1). These parts of the statement are confirmed: ActivityPeriod is now in Business.General and serves Account and MasterAgreement; the per-tier fsproj split exists; per-domain auditable actions implement IAuditableAction; migrations grant runtime roles only USAGE and CRUD; staging, dedup and classification are separate routes.

**Action:** Dan either updates his mental model (the classification extraction is in progress) or schedules the remaining extraction (ARCH-1, ARCH-2, ARCH-3, ARCH-10).

**Why:** Dan relies on this audit as his main safeguard. Where his model of the system is ahead of the code, he should know it.

---

