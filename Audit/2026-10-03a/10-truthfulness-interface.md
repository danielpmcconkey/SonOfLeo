# code-truthfulness-ui-interface-bridge

## CT-SYS81-FP-ENSURE — contradiction
- **Location:** Src/Ui.InterfaceBridge/Routes/FiscalPeriodRoutes.fs:26-35 (ensure); Src/Business.CrossDomainOrchestration/FiscalPeriodCreation.fs ensureFiscalPeriods; REQ-SYS-8.1, REQ-FP-2.7
- **Summary:** FiscalPeriod Ensure can insert several fiscal periods, but its route runs without a transaction, so a failure partway leaves the earlier periods committed.
- **Resolution:** fix-code

The ensure handler builds its context with `Context.create NoTransaction FiscalPeriodEnsure`. It does not use `runCommandRouteAndAutoCompleteTransaction`. ensureFiscalPeriods then runs `missingKeys |> List.map (constructNewAndPersist context) |> convertListOfResultsToResultsList`, which makes one insert per missing month. With NoTransaction, ExecuteNonQuery opens its own connection for each statement (ExecuteNonQuery.fs, the `isNone -> true` branch), so each insert autocommits on its own. Because List.map is eager, every insert is still attempted after one fails. REQ-SYS-8.1 says: "An operation that makes more than one write performs them in a single database transaction that commits only when the whole operation succeeds and rolls back otherwise... Read-only operations and single-write operations may run without a transaction." Ensure is neither read-only nor single-write. Every other multi-write route (JE PostNew, Void, Ingestion, all CashFlow mutations, ClassifyAccounts) uses the auto-complete runner. The existing REQ-SYS-8.1 tests (OperationInstantAndAtomicity.fs:248, 316) cover JE posting and batch post only. Failure scenario: Ensure {startPeriodKey:"2026-01", endPeriodKey:"2026-12"}, where creating 2026-06 fails (for example a unique-key race with a concurrent Create). Then 2026-01..05 and 2026-07..12 are committed, the call returns an error, and the database is left half-applied.

**Action:** Run the FiscalPeriod Ensure handler inside runCommandRouteAndAutoCompleteTransaction FiscalPeriodEnsure, and add a REQ-SYS-8.1 case for Ensure.

**Why:** Dan calls the interface the unit of work. A Saturday-run step that half-applies and then reports failure is exactly the illegal intermediate state REQ-SYS-8.1 exists to prevent.

---

## CT-SYS11-LOOKUP-UNTRIMMED — contradiction
- **Location:** Src/Ui.InterfaceBridge/BoundaryConverters/CashFlowLookupConverters.fs:11-19, 38-47; SharedContractConverters.fs:22-27; FiscalPeriodFieldConverters.fs:12-17; REQ-SYS-1.1
- **Summary:** Lookups by agreement name, payment agreement name and fiscal period key use the raw untrimmed string, so input with surrounding spaces fails as not found.
- **Resolution:** fix-code

REQ-SYS-1.1: "All raw string inputs must be trimmed of leading and trailing white space at the system boundary, before validation, before persistence...". AccountFieldConverters handles this correctly: it creates the AccountCode (trimmed) and looks up `AccountCode.value`, with the comment "the code is looked up trimmed, as it is stored". The other lookups do not. (1) CashFlowLookupConverters runs `let! _ = nameString |> AgreementName.create`, throws away the trimmed value, then calls `nameString |> LookupCache.masterAgreementNameToId.fetch`. The payment agreement name lookup does the same. (2) SharedContractConverters, PeriodKey branch: `let! _ = periodKey |> FiscalPeriodKey.fromString`, then looks up the raw `periodKey`. (3) FiscalPeriodFieldConverters.`convert FiscalPeriodKeyString to FiscalPeriodId` passes the key straight to the cache with no trim or validation. LookupCache.fetchOne runs `where {col} = @key` on that raw value. Failure scenarios: CashFlow FetchAgreementSummary {"agreementName":" Rent "} returns CashflowAgreementNameDoesntMatchId even though agreement "Rent" exists. The same input stored by CreateAgreement is trimmed (tested by REQ-CF-2.4 REQ-SYS-1.1). FiscalPeriod Fetch/Close/Reopen, JournalEntry FetchByPeriod and any TemporalFilter PeriodKey given " 2026-05" return FiscalPeriodNoPeriodMatchingKey. Classification CreatePaymentAgreementLink, claimant input, CreateInstance and NewInvoiceFieldsInput.paymentAgreementName are affected the same way. No test covers lookup-by-name with padded input.

**Action:** Look up the trimmed domain value (AgreementName.value / PaymentAgreementName.value / FiscalPeriodKey.value) in all four converters, as AccountFieldConverters does, and route FiscalPeriodFieldConverters through FiscalPeriodKey.fromString first.

**Why:** The same string is accepted and stored trimmed on create but rejected as not found on lookup. That makes trimming at the boundary depend on which route the operator happens to call.

---

## CT-SYS11-SOURCELIKE — contradiction
- **Location:** Src/Ui.InterfaceBridge/BoundaryConverters/ClassificationFieldConverters.fs:271; REQ-SYS-1.1, REQ-SYS-1.3, REQ-CR-5.3
- **Summary:** The classification rule filter's sourceLike text reaches SQL untrimmed and unvalidated, so an empty or whitespace value matches every rule that has a Source match.
- **Resolution:** fix-code

In `convert [ClassificationRuleFilterInput] to [ClassificationRuleFilter]`, nameLike goes through ClassificationRuleName.create, which trims and rejects empty. sourceLike is copied verbatim: `sourceLike = filterInput.sourceLike`. ClassificationOrchestration.fetchRulesFiltered then binds `containsPattern x` into `LIKE @source_like`. REQ-SYS-1.1 requires trimming at the boundary, and REQ-SYS-1.3 says an optional text field "when provided, may never hold a value that is empty or whitespace-only post-trim". Every other partial-match filter in the bridge (description, memo, fiReference, nameLike) goes through a smart constructor. Failure scenarios: FetchClassificationRuleFiltered {filter:{sourceLike:""...}} returns every rule with any Source field match, not an error. {sourceLike:" Chase"} returns nothing for rules that match Source "Chase".

**Action:** Convert sourceLike through the same smart constructor the Source field match uses (or JournalRefFinancialInstitution.create), so it is trimmed and empty input is rejected.

**Why:** This is the only partial-match filter in the interface layer that skips boundary normalization. That inconsistency is the kind an agent-written slice reproduces without anyone noticing.

---

## CT-NGUI16-PAYMENT-AGREEMENT — contradiction
- **Location:** Src/Ui.InterfaceBridge/InterfaceContracts/CashFlowContracts.fs:111-112 (PaymentAgreementReturn); BoundaryConverters/CashFlowFieldConverters.fs `convert [PaymentAgreement] to [PaymentAgreementReturn]`; REQ-NGUI-1.6
- **Summary:** PaymentAgreementReturn identifies its debit and credit accounts by code only, with no account names.
- **Resolution:** fix-code

REQ-NGUI-1.6: "All interface return payloads that identify an account must include the account name alongside the account code." Its waiver reads "enforced by code review and periodic audit", so this audit is the enforcement. PaymentAgreementReturn has `debitAccountCode: string; creditAccountCode: string` and no name fields. The converter resolves only `convert AccountId to AccountCodeString` for each. It is returned by CashFlow CreateAgreement, UpdateAgreement and FetchAgreementSummary (AgreementReturn.paymentAgreements). Other return types in the same slice comply: ProjectedAccountReturn has accountName, and so does ClassificationClaimantReturn.Account. Precedent: Audit 2026-08-21a NGUI-1.6-INGESTION (action item 27), which Dan accepted and fixed for the ingestion and classification return types.

**Action:** Add debitAccountName and creditAccountName to PaymentAgreementReturn and populate them with `convert AccountId to AccountNameString`.

**Why:** The CashFlow slice's agreement summary is an operator review surface. Dan already ruled that account codes without names violate REQ-NGUI-1.6 when he fixed the same gap in ingestion.

---

## CT-NGUI16-PARENT-CODE — contradiction
- **Location:** Src/Ui.InterfaceBridge/InterfaceContracts/AccountContracts.fs:25 (AccountReturn.parentCode), :48 (AccountActivityReturn.accountParentCode); AccountFieldConverters.fs:92; JournalEntryFieldConverters.fs:243-244; REQ-NGUI-1.6
- **Summary:** AccountReturn and AccountActivityReturn identify the parent account by code only, with no parent account name.
- **Resolution:** fix-code

REQ-NGUI-1.6 requires the account name wherever a return payload identifies an account. AccountReturn.parentCode and AccountActivityReturn.accountParentCode each identify a second account (the parent) by code alone. The converters resolve only `convert AccountId Option to AccountCodeString Option` / `convert AccountId Option to AccountCode Option`. These types are returned by every Account route (Create, FetchByCode, FetchByParentCode, FetchByAccountType, FetchAll, Deactivate, UpdateName, UpdateExternalReference) and by FetchActivity. resolved-findings.md has no ruling on parent identification, and the 2026-08-21a NGUI-1.6 fix did not cover these types.

**Action:** Add parentName (AccountReturn) and accountParentName (AccountActivityReturn) as string option, resolved through `convert [AccountId option] to [AccountName string option]`.

**Why:** REQ-NGUI-1.6 is waived from testing on the grounds that audit enforces it, so a gap that audit does not report is a gap no one reports.

---

## CT-RPT23-NOT-FULLY-QUALIFIED — contradiction
- **Location:** Src/Ui.InterfaceBridge/ReportWriters/{TrialBalance:273, BalanceSheetIntegrity:126, PeriodActivity:164, PrePostingReview:176}Writer.fs via Src/App.Utility/File.fs:7-14 createFullPath; ReportsContracts.OutputPathReturn.fullyQualifiedPath; REQ-RPT-2.3
- **Summary:** Report writers return `Path.Combine(baseDir, fileName)` as `fullyQualifiedPath`. When the caller gives a relative baseDir, that path is relative, not fully qualified.
- **Resolution:** fix-code

REQ-RPT-2.3: "renders the trial balance to an HTML file and returns the fully qualified file path to the written file". REQ-RPT-6.4 and 7.7 extend these output modes to the other reports. All four writers do `let! path = createFullPath pathInfo.baseDir fileName` and return it as `{ fullyQualifiedPath = path }`. createFullPath is only `Path.Combine(baseDir, fileName)`, with no Path.GetFullPath, so a relative baseDir produces a relative result. Failure scenario: Reports TrialBalance with reportOutput Report {baseDir:"reports", fileName:"tb", interpolateAsOf:false} writes ./reports/tb.html relative to the process CWD and returns {"fullyQualifiedPath":"reports/tb.html"}. A caller in a different working directory (the Saturday state machine, an agent) cannot open it. Tests pass because testOutputDir is absolute (Tests.Integrated/InterfaceBridge/ReportRoutes.fs:85).

**Action:** Normalize with Path.GetFullPath in createFullPath (or in the writers) so the returned path is absolute whatever baseDir is.

**Why:** Both the contract field name and the requirement promise an absolute path. A relative one silently depends on the caller's working directory.

---

## CT-RPT-TB-HTML-ENCODE — other
- **Location:** Src/Ui.InterfaceBridge/ReportWriters/TrialBalanceWriter.fs:206; REQ-RPT-1.3, REQ-RPT-3.6
- **Summary:** The trial balance HTML writer inserts account code and name into the markup without HTML-encoding them. The other three writers encode.
- **Resolution:** fix-code

TrialBalanceWriter.createAccountRowDomElement builds `Span $"{code} &middot; {accountName}"` from the raw values. DomElement.toString puts Span content into the tag verbatim. BalanceSheetIntegrityWriter, PeriodActivityWriter and PrePostingReviewWriter all define `encode = WebUtility.HtmlEncode` and apply it to every data string. Rendering checks for REQ-RPT-3.x are waived as "verified by code review and visual inspection", so code review is the only safeguard. Failure scenario: an account named "Bed <Bath> & Beyond" renders as "Bed  & Beyond", because the browser treats <Bath> as an unknown tag. A name that contains "</span>" breaks the row structure.

**Action:** HtmlEncode the account code and name in TrialBalanceWriter, keeping the literal &middot; separator unencoded.

**Why:** The report's purpose is to show each account's name (REQ-RPT-1.3). The one writer that skips encoding is also the oldest, and its waiver depends on review catching exactly this.

---

## CT-SYS34-SECOND-CLOCK-READ — contradiction
- **Location:** Src/Ui.InterfaceBridge/Routes/ReportRoutes.fs:45 (Calendar.today()); Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:89 (Clock.now()); REQ-SYS-3.4, REQ-RPT-7.7, REQ-STG-3.12
- **Summary:** Two routes read the clock a second time and do not use the operation's initiation instant: the pre-posting review run date, and the processed-file timestamp prefix.
- **Resolution:** dan-decides
- **Prior ruling:** IE-AC-1 (2026-07-06) ruled that reads may use Calendar.today() because AuditEnvelope is for mutations. Re-raised because REQ-SYS-3.4 was reworded on 2026-09-26, after that ruling. It now says "Every operation" and names "the reference date for account activity", the exact read case IE-AC-1 covered, as a derived date that must use the initiation instant. The processed-file timestamp is a write path, so IE-AC-1 does not cover it at all.

REQ-SYS-3.4 (amended 2026-09-26): "Every operation carries ... a single initiation instant read from the system clock when the operation begins. Every timestamp the operation writes, and every 'current date' it derives ... uses that instant." (1) The prePostingReview route creates `context` (with its AuditEnvelope instant) but passes `Calendar.today()` to PrePostingReviewWriter.write. That date goes into the file name (REQ-RPT-7.7 interpolation) and the header. (2) ingestRawEntries names the processed file with `Clock.now() |> instantToString` after commit, while the staged entries' status transitions carry the initiation instant. So the file prefix the spec calls "the ingestion timestamp" (REQ-STG-3.12) differs from the transition instants in ingestion.staged_entry_audit by the ingest's duration. Failure scenario: a review that starts at 23:59:59.9 and renders after midnight is labelled with the next day's date. An ingested file's prefix cannot be joined back to its transitions' instant.

**Action:** Dan decides whether REQ-SYS-3.4 binds read-only report routes and post-commit file naming. If it does, derive the run date from `context |> Context.getInitiationInstant |> Calendar.dateFromInstant` (as the post route already does) and capture the ingest instant before the transaction for the file prefix.

**Why:** Dan amended REQ-SYS-3.4 so that one operation has one moment. These are the only two places in the interface layer that read the clock again.

---

## CT-REQ-TAGS-IN-WRITERS — idiom
- **Location:** Src/Ui.InterfaceBridge/ReportWriters/PeriodActivityWriter.fs:145; Src/Ui.InterfaceBridge/ReportWriters/PrePostingReviewWriter.fs:158
- **Summary:** Two report writers carry `// REQ-RPT-x:` traceability tags, which the no-REQ-annotations decision retired.
- **Resolution:** fix-code

PeriodActivityWriter.fs:145 has `// REQ-RPT-6.4: the header shows the range` and PrePostingReviewWriter.fs:158 has `// REQ-RPT-7.7: the header shows the run date and the number of entries and lines`. CompoundedLearnings/articles/architecture/no-req-annotations-in-source.md (settled 2026-07-31) allows a REQ ID only inside rationale that explains why code looks wrong but isn't. A tag that asserts "this site enforces REQ-X" is "the thing being retired": "Delete the first, keep the second." Both comments are site-enforcement tags, not rationale. Both writers were added in the cash-flow/pre-posting slice, after the retirement, so they show agents reintroducing the pattern. (The parenthetical cites in IngestionContracts.fs:90/98, CashFlowContracts.fs:175 and IngestionRoutes.fs:194 explain why a contract is shaped as it is, so they read as permitted rationale.)

**Action:** Delete the two `// REQ-RPT-...:` comments, or rewrite them as plain descriptions with no REQ ID.

**Why:** Dan settled this because nothing executes such tags: an incomplete set reads as complete. With fully agentic development, every reintroduced tag teaches the next agent the retired convention.

---

## CT-DEAD-BRIDGEERROR — maintainability
- **Location:** Src/Ui.InterfaceBridge/BridgeError.fs
- **Summary:** BridgeError (case InterfaceBridgeConversionFailure) is compiled into the bridge but never constructed, opened or referenced anywhere in Src or Tests.
- **Resolution:** fix-code

grep for `BridgeError` and `InterfaceBridgeConversionFailure` across Src and Tests finds only the defining file. The bridge's converters report every failure through the lower-tier domain errors (LedgerError, CashFlowError and others), and the routes raise no bridge-specific error. Dan's refactor broke the monolithic error into per-tier domain errors so each tier owns its errors. A tier error type that no path can produce is dead surface, and an agent may assume it is the right channel for conversion failures. Separately, check-apperror-coverage still reads the retired Src/Utilities/AppError.fs path, so no mechanical check would ever flag this.

**Action:** Delete BridgeError.fs and its compile entry, or route a real bridge-level conversion failure through it.

**Why:** Unreachable error cases mislead agent developers about which error channel the bridge uses. The coverage check that would normally catch them currently passes on an empty count (0/0).

---


