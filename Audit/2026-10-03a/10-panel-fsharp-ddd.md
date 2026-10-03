# fsharp-ddd-reviewer

## FSDDD-1 — enforcement-gap
- **Location:** Src/Business.FinancialServices.CashFlow/Invoice.fs:318, Instance.fs:183, MasterAgreement.fs:233, Payment.fs:359, PaymentAgreement.fs:266; REQ-SYS-3.3
- **Summary:** Five CashFlow `update` functions never set `modified_at`, so an edited Invoice, Instance, MasterAgreement, Payment or PaymentAgreement keeps its creation timestamp. This violates REQ-SYS-3.3.
- **Resolution:** fix-code

REQ-SYS-3.3: "Every successful update to a record must set its 'modified at' timestamp to the initiation instant of the operation performing the update." The UPDATE statements in Invoice.update (Invoice.fs:376-380), Instance.update (Instance.fs:207), MasterAgreement.update (MasterAgreement.fs:291), Payment.update (Payment.fs:397) and PaymentAgreement.update (PaymentAgreement.fs:315) build only `set {setClauses}` from the FieldUpdates and never bind `@modified`. All six cashflow tables carry `modified_at timestamptz NOT NULL` (202609071125-CreateCashFlowTables, lines 19/48/85/122/156/191), and the database has no trigger. The other update functions do set it: PaymentAgreementLink.update (PaymentAgreementLink.fs:209-212), Account.update, the classification rule update (ClassificationOrchestration.fs:347-350), JE voiding and the JE external-reference update. The REQ-SYS-3.3 theory in Tests/Tests.Integrated/CrossDomainOrchestration/OperationInstantAndAtomicity.fs:103 covers comments, voiding and payment agreement links only, so no test catches this. Every invoice state change, payment re-point and agreement edit therefore leaves the row's modified-at frozen. That includes the derived payment/posted-state writes done by InstanceOrchestration.updateInstanceComposite and CashFlowOps.transitionPaymentsToPosted.

**Action:** Add `modified_at = @modified`, bound to `Context.getInitiationInstant`, to all five CashFlow UPDATE statements, and extend the REQ-SYS-3.3 theory to include them.

**Why:** Each module hand-builds its SET clause from a list of FieldUpdates, so the cross-cutting invariant (an update stamps the modified time) depends on every author remembering it. The FP remedy is to make the invariant structural rather than conventional: one small shared function that assembles an UPDATE from (column, parameter) pairs and always appends the modified stamp. Then the omission cannot be written.

---

## FSDDD-2 — other
- **Location:** Src/Business.CrossDomainOrchestration/FetchFilterAndSort.fs:184-238; CashFlowCompositeFetcher.fs:74-121
- **Summary:** The shared amount and date predicate builders for `AgreementFilter` contain two bugs. A range filter applies its ceiling as `>=`, and every date filter references a parameter that is never bound.
- **Resolution:** fix-code

(1) createAmountPredicateAndParameters, AmountRange arm (lines 198-201): the ceiling clause is `$"{columnReference} >= @{parameterPrefix}_max"`, which should be `<=`. A range therefore returns only amounts at or above the ceiling. (2) createTemporalPredicateAndParameters (lines 230-235): the predicate references `@{prefix}_end_inclusive` but binds a parameter named `@{prefix}_end`. Any non-None temporal filter sends an unbound placeholder to Npgsql and fails at runtime. Both builders are used only by CashFlowCompositeFetcher.createPredicateAndParameters, for the paymentAgreementExpectedAmount, instanceTemporalFilter, invoiceDate/DueTemporalFilter, invoiceAmount, paymentAmount and paymentPostedToLedgerTemporalFilter fields. Every caller sets those fields to None (AgreementOrchestration.fs:304-316, 334-346), and no test sets them (grep of Tests: only `= None`). The defects are latent today but ready to fire the first time an agent wires the filter to a route.

**Action:** Fix both builders (use `<=` for the ceiling and make the parameter names agree), then either add tests that exercise each AgreementFilter range and temporal field, or delete the unused fields until a route needs them.

**Why:** This is speculative generality in a stringly-typed query builder. When SQL fragments and parameter names are assembled from interpolated strings in two separate places, nothing ties the placeholder to its binding, and code that no path executes is never checked. Build the fragment and its parameter from a single value (for example `let p = $"@{prefix}_end"` used in both places), and do not ship filter dimensions that no caller or test exercises. Unexercised code paths are where agent-written bugs go unnoticed.

---

## FSDDD-3 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/Account.fs:40-64; FiscalPeriod.fs:31-46; JournalEntryLine.fs:36-53; CashFlow/PaymentAgreement.fs:50-71; Classification/FieldMatchChain.fs:273; ClassificationRuleGroup.fs:167-174; CompoundedLearnings/articles/coding/validation-layers.md
- **Summary:** Entity and component `create` functions are total, unvalidated constructors. Invariants that depend only on the entity's own fields are enforced in orchestrators, so illegal entities can be built through the public API.
- **Resolution:** dan-decides

validation-layers.md (layer 2) puts "cross-field constraints within a single record in the entity's construction path (e.g., type/subtype combinations via AccountSubtype.validFor, activeEnd >= activeBegin)" in the constructor. The code does not do this:
- Account.create returns `Account` and accepts any `AccountType`/`AccountSubtype option` pair. The type/subtype check exists only in AccountCreation.confirmTypeAndSubtypeAreValid (AccountCreation.fs:70-79), and AccountSubtype.validFor (AccountComponent.fs:113), the function the article names, is dead code with no callers.
- FiscalPeriod.create accepts arbitrary startDate/endDate unrelated to the key; see FSDDD-4.
- JournalEntryLine.create accepts zero or negative Money. Positivity is checked in JournalEntryLineOrchestration.confirmAmountIsPositive.
- PaymentAgreement.create allows debit account = credit account and a non-positive expectedAmount. Both are checked in AgreementOrchestration.confirmPaymentAgreement (lines 120-134).
- FieldMatchChain.create and ClassificationRuleGroup.create accept empty chains. FieldMatchChain.doesMatch says "we have validation at construction", but that check lives in ClassificationOrchestration.confirmFieldMatchChain.
These are all entity-own-property rules (REQ-SYS-2.1.1 territory), not state-dependent layer-4 checks.

**Action:** Dan to rule whether entity `create` should return `Result` and own its single-record invariants (type/subtype, debit≠credit, positive amount, non-empty chain), with orchestrators keeping only state-dependent checks. If so, delete or reuse AccountSubtype.validFor and update validation-layers.md to match.

**Why:** Smart-constructor discipline means the only way to get an `Account` is through a function that proves it is legal. Then every holder of an `Account` can trust it without re-checking, and the orchestrator cannot forget. When `create` is total and validation is a separate step the orchestrator must remember to call, correctness depends on call-site discipline, not on types. With agents writing the code, that call-site discipline is exactly the guarantee you no longer have.

---

## FSDDD-4 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/FiscalPeriodComponent.fs:15-28; CrossDomainOrchestration/FiscalPeriodCreation.fs:13-21,43; Ledger/JournalEntryComponent.fs:99-108
- **Summary:** `FiscalPeriodKey` is a regex-checked string. The calendar month it stands for is recovered three separate times by string slicing and `Int32.Parse`, and FiscalPeriod's start and end dates are not tied to its key.
- **Resolution:** fix-code

FiscalPeriodKey wraps a `yyyy-MM` string. FiscalPeriodCreation.constructNewAndPersist slices `keyString[0..3]` and `[5..6]` and calls Int32.Parse to build start/end dates (lines 13-19). ensureFiscalPeriods repeats this in a local `firstOfMonth` (line 43) and orders keys with `String.CompareOrdinal` (line 47). EntryDate.create goes the other way, formatting `$"{entryDate.Year}-{monthF}"` to look up a key (JournalEntryComponent.fs:100-102). FiscalPeriod.create then accepts the key, startDate and endDate as three independent inputs, so a period keyed 2026-03 with April dates is representable.

**Action:** Give FiscalPeriodKey a structured representation (for example NodaTime `YearMonth`) with derived `startDate`/`endDate`/`ofDate` functions, and have FiscalPeriod derive its dates from the key, not accept them as parameters.

**Why:** In domain terms a fiscal period key is a calendar month, not a string. Modelling it as a string pushes the parsing to every consumer and lets three facts (key, start, end) disagree. Making the domain type carry its real structure gives a single source of truth: start and end become functions of the key, so the mismatch cannot be represented and the duplicated parsing goes away.

---

## FSDDD-5 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:161-166; Invoice.fs:36-47; CrossDomainOrchestration/InstanceOrchestration.fs:131-260,520-560; Specs/Behavioral/CashFlow.md REQ-CF-9.8, 9.9; CompoundedLearnings/articles/coding/field-update-pattern.md
- **Summary:** The spec says an invoice's payment state and posted state are derived from its payments, but the code stores and updates them as fields and then runs eight checks to catch combinations that a derivation could never produce.
- **Resolution:** dan-decides

REQ-CF-9.8 says "Payment state is derived" and REQ-CF-9.9 says "Posted state is derived". The code instead models them as independent settable fields: InvoiceLifeCycleState is a public record of four independent fields, and InvoiceFieldUpdates exposes paymentStateUpdate and postedStateUpdate (Invoice.fs:43-44). field-update-pattern.md justifies the split because "payment state and posted state move independently", which contradicts the spec. InstanceOrchestration then derives them (derivePaymentState and derivePostedState, lines ~521-545), writes them back through withDerivedStates, and also runs confirmFullyPaidAmountMatches, confirmPostedToLedgerRequiresFullyPaid, confirmPartiallyPaidHasPayments, confirmPostedToLedgerRequiresAllPaymentsPosted and confirmPartiallyPostedHasAPostedPayment (lines 131-215). Those checks police combinations the derivation already makes impossible. Nothing at the type level stops a future caller from passing `paymentStateUpdate = SetTo FullyPaid`; only the validation re-run catches it.

**Action:** Dan to decide: (a) make payment and posted state computed functions of an InvoiceComposite (stored only as a denormalised cache written by one function, with no FieldUpdate exposed), or (b) keep them settable and amend REQ-CF-9.8/9.9 and field-update-pattern.md so the two authorities agree.

**Why:** In DDD a derived value is a function, not state. Storing it as settable data creates an invariant (stored value = f(payments)) that has to be policed at runtime forever. Exposing only the inputs makes the illegal states unrepresentable, and five validation functions plus their error cases disappear.

---

## FSDDD-6 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:60-73,486-510,609-613; CashFlowOps.fs:329-341; Ui.InterfaceBridge/BoundaryConverters/CashFlowFieldConverters.fs:340-354; Specs/Behavioral/CashFlow.md REQ-CF-6.5
- **Summary:** The orchestrator's new-payment inputs require the caller to supply the payment amount and posted-to-ledger date, although the spec says both are derived. The UI layer fetches the amount from the orchestrator only to pass it back in.
- **Resolution:** fix-code

REQ-CF-6.5 says payment amount is derived from the line the transaction pointer references, and Payment.fs:24-26 notes that amount and postedToLedgerDate are "not separately tracked in the database". Yet InvoiceCompositeUpdate.newPayments and the newInvoices tuple take `TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option`. The lineAmount doc comment says "no caller supplies one" (InstanceOrchestration.fs:60-61), but the UI converter calls `InstanceOrchestration.lineAmount` and feeds the result back in (CashFlowFieldConverters.fs:350). postedToLedgerDate comes straight from operator input (line 352-354), and CashFlowOps.createPaymentForInvoice passes a caller-chosen amount (line 341). derivePaymentState then works from these caller-supplied amounts before the post-write re-fetch and confirmInstanceComposite correct anything.

**Action:** Drop PaymentAmount and PostedToLedgerDate from the new-payment input shape. Have preConstructInvoiceComposite and preConstructNewInvoiceComposite compute them from the transaction pointer through lineAmount and the JE header.

**Why:** An input type should contain only what the caller is the authority for. Accepting derived data as input means trusting the caller and then re-checking it, so the signature misstates who owns the fact. Narrowing the input type to the real degrees of freedom removes the mismatch error paths and makes the doc comment true.

---

## FSDDD-7 — architecture
- **Location:** Src/Business.FinancialServices.Classification/ClassificationRule.fs:93,149-151; CrossDomainOrchestration/ClassificationOrchestration.fs:146-160
- **Summary:** Classification rule groups are persisted by serialising private F# types to JSONB. Reading them back skips the smart constructors, and the stored JSON (and the SQL that queries it) depends on private field names.
- **Resolution:** fix-code

persist does `classificationRule.ruleGroups |> toJson<ClassificationRuleGroup list>` and reconstitute does `fromJson<ClassificationRuleGroup list>` (FSharp.SystemTextJson over private records and private single-case unions). Consequences: (1) Deserialisation skips StringSearchPattern.create, Money.fromDecimal (the Amount pattern's Money is a private record) and the non-empty chain/group rules. The code patches only the regex case after the fact (confirmStoredPatternsAreValid, with the comment "stored patterns are deserialised straight into the pattern type and skip its create"); Money precision and empty chains are never re-checked on read, contrary to REQ-SYS-2.1 ("read-from-persistence alike"). (2) fetchRulesFiltered queries `rg.value -> 'chainOne' -> 'chain'`, `fm.value ->> 'Case' = 'Source'` and `'Fields' ->> 0` (ClassificationOrchestration.fs:146-160), so renaming a private record field or changing the serializer's union encoding silently breaks stored rules and the source filter.

**Action:** Introduce an explicit persistence DTO for rule groups (plain records and strings), map it to the domain through the existing smart constructors (the same path the InterfaceBridge converters already use), and point the JSONB SQL at the DTO's stable field names.

**Why:** Reflection-based serialisation of domain types is a back door around smart constructors: private constructors mean nothing to System.Text.Json. It also makes private implementation names part of the persisted schema. An anti-corruption DTO keeps the persisted contract stable and forces every value back through validation.

---

## FSDDD-8 — idiom
- **Location:** Src/Business.FinancialServices.Classification/ClassificationComponent.fs:126-131,180-182; Classifier.fs:221-228; CrossDomainOrchestration/StageEntryOrchestration.fs:391-395
- **Summary:** `PrioritizedMatch` stores the claimant as two option fields (`accountId` and `paymentAgreementId`). The `ClassificationClaimant` union already says it is exactly one of the two, so the record allows both-set and neither-set states.
- **Resolution:** fix-code

ClassificationRule holds `classificationClaimant: ClassificationClaimant = Account of AccountId | PaymentAgreement of PaymentAgreementId`. Classifier.classifyCandidate breaks it apart into `accountId = Some x; paymentAgreementId = None` (or the reverse) for every PrioritizedMatch, so both-Some and both-None become representable. Downstream code re-collapses it with `prioritizedMatch.accountId` or `List.choose _.paymentAgreementId`. For example, StageEntryOrchestration.classifyAccounts' winningAccountId silently yields None if a payment-agreement rule were ever in the run, instead of the compiler forcing a decision.

**Action:** Replace the two option fields with `claimant: ClassificationClaimant` and pattern-match where the account or agreement is needed.

**Why:** "Make illegal states unrepresentable" applies directly: a sum type encodes exactly one of these, and a pair of options encodes 0, 1 or 2 of them. Breaking a DU into options throws away the exhaustiveness checking that made the DU worth having.

---

## FSDDD-9 — statement-delta
- **Location:** Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:27-45; Classification/ClassificationComponent.fs:8, ClassificationRule.fs:12, Classifier.fs:6
- **Summary:** Dan's statement says classification is now its own domain and that errors were split per domain, but classification still has no error type and reports its errors through `DataIngestionError` cases prefixed `Ingestion...`.
- **Resolution:** fix-code

Statement: classification "now stands as its own domain, servicing both data ingestions and cash flow", and errors were broken "into domain-specific implementations of an interface". The Classification project has no error file (the scout confirms). About 16 classification-specific cases live in DataIngestionError: IngestionClassificationRuleGroupsEmpty, IngestionFieldMatchChainEmpty, IngestionSearchPatternInvalidRegex, IngestionInvalidNumericSearchOperator, IngestionClassificationRulePatternTimedOut, IngestionInvalidClassificationClaimantType and others. Three Classification modules open `Business.FinancialServices.DataIngestion.DataIngestionError`. Every error for a cash-flow classification run (PaymentAgreementClaimant) therefore reports `DomainName = DataIngestionError`. The isolated classification tests also sit under a DataIngestion-named folder (scout).

**Action:** Create Classification/ClassificationError.fs implementing IAppError, move the classification cases into it under classification names, and update tests that match on DomainName/CaseName.

**Why:** Bounded contexts own their language, and error cases are part of that language. While classification borrows ingestion's error vocabulary, it is not yet a separate domain: it depends on one of its clients for its own failure semantics, which is the coupling the per-domain error refactor was meant to remove.

---

## FSDDD-10 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/AccountDeactivation.fs:16-35; JournalEntryVoiding.fs:~50-75; JournalEntryExternalReferenceOrchestration.fs:~55-87; ClassificationOrchestration.fs:~300-358
- **Summary:** Several entities' UPDATE SQL is written inside orchestrator files, not in the entity's own module. Account deactivation's update also skips the `ActivityPeriod` constructor that guards the end date.
- **Resolution:** fix-code

Account.fs owns a private `update` for name and external ref, but AccountDeactivation.updateActiveEnd writes `UPDATE ledger.account set active_end = ...` directly. It sets the raw column instead of building a new ActivityPeriod through ActivityPeriod.create, so the end ≥ begin rule is re-implemented as confirmProposedDeactivationDateIsValid. JE voiding (`UPDATE ledger.journal_entry`), the JE external-reference update (`UPDATE ledger.journal_entry_ext_reference`) and the classification rule update (`UPDATE classification.classification_rule`) likewise live in Business.CrossDomainOrchestration. By contrast, all CashFlow, DataIngestion and Classification entities keep their UPDATEs in their own modules. orchestration-layer.md: "Domain modules own single-concern operations on their own types; orchestration composes."

**Action:** Move each entity's UPDATE into its domain module (for example Account.updateActivityPeriod taking an ActivityPeriod, JournalEntryHeader.markVoided, JournalEntryExternalReference.update, ClassificationRule.update), leaving the orchestrators to compose checks and calls.

**Why:** A module boundary only means something if the module is the sole writer of its table. When writes are spread across orchestrators, an entity's persistence rules (modified_at, column encoding, constructor invariants) have several authors. This is how FSDDD-1-style drift and the duplicated end-date check arise.

---

## FSDDD-11 — idiom
- **Location:** Src/Business.FinancialServices.Ledger/JournalEntryComponent.fs:103-106
- **Summary:** `EntryDate.create` converts every error from the fiscal-period lookup into 'date not in any fiscal period', so a connection or database failure is reported as a domain problem.
- **Resolution:** fix-code

`key |> FiscalPeriod.fetchIdByKey context |> Result.mapError(fun _ -> (JournalEntryDateNotInFiscalPeriod entryDate))`. fetchIdByKey already turns the zero-row case into FiscalPeriodNoPeriodMatchingKey through whenNoRows (FiscalPeriod.fs:126). The wildcard mapError then also swallows DalErrorDuringReaderQueryExecution, DalErrorDuringTransactionCreation and others. dal-errors-are-backstops.md states the house rule: translate only the not-found case and let everything else through "as loudly and unhelpfully as it deserves". EntryDate.create is on the JE posting path (JournalEntryRoutes.fs:28) and the staged-posting path (StageEntryOrchestration.fs:740).

**Action:** Translate only the not-found error, e.g. `|> Result.mapError (function AsError (FiscalPeriodNoPeriodMatchingKey _) -> JournalEntryDateNotInFiscalPeriod entryDate :> IAppError | other -> other)`, or `whenNoRows` directly on the DAL result.

**Why:** On the railway, mapError with a wildcard discards information: two different tracks (domain absence and infrastructure failure) get merged into one. Error translation should pattern-match the specific case it understands and pass the rest through unchanged, otherwise the operator is told to fix a fiscal period when the database is down.

---

## FSDDD-12 — idiom
- **Location:** Src/Business.CrossDomainOrchestration/CashFlowOps.fs:758-759; CompoundedLearnings/articles/coding/money-arithmetic-boundaries.md; Src/README.md
- **Summary:** The cash-flow projection works out outstanding invoice amounts with raw `decimal` arithmetic (`List.sumBy` over `Money.amount`, subtraction, then `max 0M`) instead of the `Money` functions.
- **Resolution:** fix-code

`let paid = payments |> List.sumBy (fun payment -> (payment |> Payment.amount).money |> Money.amount)` followed by `Money.fromDecimal (max 0M ((invoice amount |> Money.amount) - paid))`. Src/README.md lists "Arithmetic on raw decimal money values" as a Never, and money-arithmetic-boundaries.md allows unpacking only for multiplication or division. The same file does it correctly a few lines later (Money.sumList, Money.add, Money.subtractVal1FromVal2 at 782-785), and InstanceOrchestration.derivePaymentState uses Money.sumList. This is the only raw-decimal money arithmetic left in Src.

**Action:** Use Money.sumList for paid and Money.subtractVal1FromVal2 for the remainder, and add a Money-level floor-at-zero (or max) helper rather than unwrapping.

**Why:** Having a Money type only pays off if all money arithmetic goes through it. Each unwrap-compute-rewrap is a spot where precision and range validation run only at the end and where the next agent copies the pattern. An operation the domain needs (floor at zero) belongs in the Money module.

---

## FSDDD-13 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/JournalEntryOrchestration.fs:127-149; StageEntryOrchestration.fs:45-122; JournalEntryLineOrchestration.fs:14-20; InstanceOrchestration.fs:131-138; AgreementOrchestration.fs:125-134
- **Summary:** Two domain rules have no type of their own. The balanced double-entry check is written twice (for journal entries and for staged entries), and the 'amount must be positive' check is written five times.
- **Resolution:** dan-decides

JournalEntryOrchestration.confirmAmountEquality and confirmLineCount are re-implemented almost line for line as StageEntryOrchestration.sumLinesByType, confirmAmountEquality and confirmLineCount, differing only in error type. The positive-amount rule is coded separately in JournalEntryLineOrchestration.confirmAmountIsPositive, StageEntryOrchestration.confirmLinesAreAllPositive, InstanceOrchestration.confirmInvoiceAmountIsPositive and AgreementOrchestration.confirmPaymentAgreement (expectedAmount > 0M), each with its own error case. Meanwhile InvoiceAmount and PaymentAmount (CashFlowComponent.fs:312-313) are transparent public records with no constructor, so the wrapper that exists enforces nothing.

**Action:** Introduce a validated PositiveMoney (or LineAmount) primitive with a smart constructor and use it for JE line, staged line, invoice and expected amounts. Factor the 'balanced set of debit/credit lines' rule into one pure function (over `(JournalEntryLineType * Money) list`) that both composites call.

**Why:** A rule repeated in five places is a missing type, and a rule repeated across two composites is a missing domain concept. With a PositiveMoney type the compiler carries the invariant from the boundary inward and the checks disappear. A single balance function means the ledger and staging cannot disagree about what 'balanced' means.

---

## FSDDD-14 — idiom
- **Location:** Src/Business.FinancialServices.Ledger/AccountComponent.fs:41-43,70-76; JournalEntryComponent.fs:120-134; CrossDomainOrchestration/AccountBalance.fs:119, PeriodActivity.fs:61-63
- **Summary:** The Ledger defines two separate `Debit | Credit` unions, `AccountTypeNormalBalance` and `JournalEntryLineType`, for the same concept: the side of the ledger.
- **Resolution:** fix-code

AccountComponent declares `AccountTypeNormalBalance = Debit | Credit`, and JournalEntryComponent later declares `JournalEntryLineType = Debit | Credit`. Files that open both rely on open order: JournalEntryOrchestration.fs:129 and AccountDeactivation.fs:350 write a bare `Debit`, which resolves to JournalEntryLineType only because JournalEntryComponent is opened later. AccountBalance.fs:119 and PeriodActivity.fs:61-63 have to write `AccountTypeNormalBalance.Debit` explicitly. This is the hazard du-case-collision-across-opens.md describes. Comparing an account's normal side with a line's side, which is the core of balance sign logic, crosses two types for one idea.

**Action:** Define a single LedgerSide (Debit | Credit) in AccountComponent, have AccountType.normalBalance return it, and use it as the JE line type (keeping the `JournalEntryLineType` name as an alias if desired).

**Why:** In DDD's ubiquitous language one concept gets one type. Two structurally identical unions for the same idea give no added safety; they only add conversions and silent resolution that depends on the order of `open`s. One type makes 'is this line on the account's normal side?' a plain equality.

---

## FSDDD-15 — idiom
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:486-510,622-637,779-797; AgreementOrchestration.fs:205-211; JournalEntryOrchestration.fs:189,242; Business.General/Cadence.fs:291-302 vs 322-329
- **Summary:** Several orchestrator APIs take long positional tuples of same-typed fields. The largest is a 9-tuple whose last item is a list of 5-tuples, and it is repeated verbatim in four signatures.
- **Resolution:** fix-code

The newInvoices shape `PaymentAgreementId * ExternalInvoiceId option * InvoiceDate * DueDate * InvoiceAmount * InvoiceState * Blocker option * InvoiceMemo option * (TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option) list` is written out in InstanceCompositeUpdate, preConstructNewInvoiceComposite, createInstanceCompositeAndSaveToDb and the CashFlowOps/converter call sites. AgreementOrchestration.constructNewAndPersist takes a 6-tuple list, and JE creation takes `(AccountId * Money * JournalEntryLineType * JournalEntryLineMemo option) list`. Cadence.cadenceToColumns returns `(name, dateInMonth, weekInMonth, weekDay, month, next)`, while Cadence.reconstitute takes `(name, weekDay, dateInMonth, weekInMonth, month, next)`, a different order with two adjacent `int option`s. A swap there compiles and corrupts cadences.

**Action:** Replace the multi-field tuples with named records (for example NewInvoice, NewPayment, NewPaymentAgreement, NewJournalEntryLine, CadenceColumns) declared at the right compile tier.

**Why:** Tuples are for small, local, positionally obvious groupings. Once fields share a type, position is the only thing distinguishing them and the compiler cannot catch a transposition. Named record fields make construction self-documenting and turn transpositions into compile errors.

---

## FSDDD-16 — idiom
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:308-313; Invoice.fs:225-227; Payment.fs:87,192-193; InstanceOrchestration.fs:71,75
- **Summary:** Four date wrappers share the field name `localDate` and two amount wrappers share `money`. F# therefore infers the last-declared type for any unannotated record expression, and call sites already need annotations to avoid it.
- **Resolution:** fix-code

`type InvoiceDate = { localDate }`, `DueDate = { localDate }`, `PostedToFiDate = { localDate }`, `PostedToLedgerDate = { localDate }`, `InvoiceAmount = { money }`, `PaymentAmount = { money }`. The workarounds are visible: `{ PostedToFiDate.localDate = x }` (Payment.fs:87,192), `({ money = ... } : CashFlowComponent.PaymentAmount)` (InstanceOrchestration.fs:71,75), `{CashFlowComponent.InvoiceDate.localDate = neededDate}` (CashFlowOps.fs:58). record-field-inference-collision.md names exactly this failure family. In Invoice.reconstitute the bare `{ localDate = invoiceDate }` / `{ money = amount }` compile only because the expected parameter types are known. The wrappers are also public with no constructor, so they add a name and no invariant (see FSDDD-13).

**Action:** Make them private single-case unions (`type DueDate = private DueDate of LocalDate`) with create/value functions, or give each record a distinct field label.

**Why:** A wrapper type's value is distinctness plus invariants. Records that share labels give partial distinctness and silently retarget inference when another one is declared. Single-case unions with private constructors are the idiomatic F# domain primitive: they never collide and they can carry validation.

---

## FSDDD-17 — enforcement-gap
- **Location:** Src/App.Session/Context.fs:28-32; Src/Ui.InterfaceBridge/Routes/IngestionRoutes.fs:89; REQ-SYS-3.4; Src/README.md (Context/AuditEnvelope row)
- **Summary:** The 'one operation, one instant' rule has two escape hatches: a public `Context.updateInitiationInstant` that mints a new instant mid-operation (currently unused), and a direct `Clock.now()` in the staging route that names the processed file.
- **Resolution:** fix-code

REQ-SYS-3.4: "a single initiation instant ... Every timestamp the operation writes ... uses that instant." Context.updateInitiationInstant builds a fresh AuditEnvelope (therefore a fresh Clock.now()), with the doc comment "used for long orchestrated events where you need tasks to show the order of operations". Its existence invites the opposite of the requirement; grep finds no callers. IngestionRoutes.fs:89 stamps the processed file name with `Clock.now()` rather than the context instant, although Src/README.md lists "A fresh Clock.now() inside a mutating operation" as a Never. Checks/check-clock.sh only bans DateTime/SystemClock APIs, so neither is caught mechanically.

**Action:** Delete Context.updateInitiationInstant, use the operation's initiation instant for the processed-file timestamp, and consider extending check-clock.sh to flag Clock.now() outside App.* and report footers.

**Why:** An invariant that comes with an exported way to break it is only a convention. Removing unused escape hatches from a module's public surface is the cheapest form of making the illegal path unrepresentable, and agents will find and use any public function that looks appropriate.

---

## FSDDD-18 — stale-reference
- **Location:** CompoundedLearnings/articles/architecture/dal-errors-are-backstops.md:6,36,49-56; orchestration-layer.md; type-taxonomy.md; type-placement-by-compile-tier.md; coding/du-case-collision-across-opens.md:61; coding/validation-layers.md
- **Summary:** Learnings that agents are told to read before writing code still describe the old `Model/` and `ModelOrchestrator/` layout and a DAL error-handling pattern that no longer exists, so they now teach agents code that is dead or misplaced.
- **Resolution:** fix-spec

dal-errors-are-backstops.md shows the canonical not-found pattern as `| Error (DalResultantRowsDidntMatchExpectation(expected, actual)) -> if actual = 0 then ...`. ExecuteReader.confirmNumRows now returns DalNoOp for zero rows under ExactlyOne/OneOrMany, and every Src site uses DalError.whenNoRows (59 references). An agent following the article writes an `actual = 0` arm that can never fire. orchestration-layer.md, type-taxonomy.md, type-placement-by-compile-tier.md and du-case-collision-across-opens.md ("applied in `Src/Model/CashFlow/Payment.fs`") still place code in `Model/` and `ModelOrchestrator/`, which the fsproj-tier refactor replaced with Business.FinancialServices.* and Business.CrossDomainOrchestration. validation-layers.md cites AccountSubtype.validFor as the type/subtype mechanism, but it has no callers (FSDDD-3). Dan's statement says this audit and the agent-facing guidance are the back-stop for a fully agentic workflow, so stale guidance flows straight into new code.

**Action:** Update the listed articles to the current project names and to the DalNoOp/whenNoRows pattern, and replace the validFor reference with whatever FSDDD-3's ruling settles.

**Why:** In an agent-written codebase, learnings work like the type signatures of the development process: whatever they say is what gets generated. An example that no longer matches the implementation is worse than no example, because it produces plausible code that compiles and is wrong.

---

## FSDDD-19 — architecture
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:280-282; Payment.fs:71-104; Specs/Behavioral/CashFlow.md REQ-CF-6.4, REQ-CF-13.2; CrossDomainOrchestration/CashFlowOps.fs:407
- **Summary:** The `Payment` entity cannot hold the staged line it came from once it is posted, so the 'paid line' rule in REQ-CF-13.2 has to query the database directly.
- **Resolution:** dan-decides

REQ-CF-6.4: "Both may be present: ... after posting, the journal entry line ID is added while the staged line ID is retained as provenance." REQ-CF-13.2 depends on that provenance: a line is ineligible if any Payment references it, "whether that Payment's pointer is still Staged or has moved to Posted". `TransactionPointer = Posted of JournalEntryLineId | Staged of StageEntryLineId` drops the staged ID on read (Posted wins). Payment.applyFieldUpdates admits the in-memory record and the write diverge ("leaves the in-hand pointer Posted even though the write still sets the column"). As a result, matching (CashFlowOps.fs:407) and dedup (StageEntryOrchestration.headerIdsWithAPaidLine) need a separate SQL helper, Payment.fetchReferencedStageEntryLineIds, to see what the entity hides.

**Action:** Model the pointer as `Staged of StageEntryLineId | Posted of JournalEntryLineId * provenance: StageEntryLineId option` (keeping a `resolve` function for the 'Posted takes precedence' reading), so the entity carries everything REQ-CF-6.4 says a Payment has.

**Why:** An entity type should be able to represent every legal state the spec describes. When the type is less expressive than the domain, the missing facts leak out into ad-hoc queries and comments explaining why the record you hold differs from the row you wrote, which is the gap persistence fidelity (REQ-SYS-5.1) is meant to close.

---

