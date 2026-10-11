# fsharp-ddd-panel

## FSDDD-ERR-1 — architecture
- **Location:** Src/Business.FinancialServices.Positions/PositionsError.fs:9-14; Src/Business.FinancialServices.Classification/ClassificationError.fs:8-11; Src/Business.FinancialServices.CashFlow/CashFlowError.fs:8-10,76; Src/Business.FinancialServices.DataIngestion/DataIngestionError.fs:8
- **Summary:** Four error modules declare domain types and carry them in error cases, which the new 'Error cases carry primitives' constraint forbids.
- **Resolution:** fix-code

Architecture/SonOfLeo.archimate (905c0b9), constraint 'Error cases carry primitives': 'An error module declares no other type... A type declared in an error module because the error module compiles first breaks this rule. Move the type to its domain's components module, give it a toString, and change the error case to carry a string.' Four modules break it. (1) PositionsError.fs:9 `ActivityShapeProblem` (SecurityMissing | QuantityMissing | ...), carried by `PositionsActivityShapeInvalid of string * LocalDate * string * ActivityShapeProblem` and produced by InvestmentActivity.shapeProblem (InvestmentActivity.fs:77). The todo at lines 7-8 describes the breach, but per SS-3's note a todo is not a waiver. (2) ClassificationError.fs:8 `StageLineProtection`, carried by ClassificationStageEntryLineCannotBeRemoved/Changed. It is also used as plain domain data: StageEntryOrchestration.protectionsOf (StageEntryOrchestration.fs:567-584) returns `(StageEntryLineId * Classification.ClassificationError.StageLineProtection) list`, so an ingestion orchestrator reaches into the Classification error module for a domain concept. (3) CashFlowError.fs:8 `OrphanedLineReason`, carried inside `CashflowPaymentAgreementLinksOrphaned of (Guid * Guid * OrphanedLineReason) list` and built in CashFlowOps.fs:542-543. (4) DataIngestionError.fs:8 `IngestionRejectedRecord = { lineNumbers; groupId; error: IAppError }`, carried by IngestionFileRejected and built as data in StageEntryOrchestration.ingestFile (line 450) and IngestionRoutes.fs:37.

**Action:** Move ActivityShapeProblem to PositionsComponent (beside ActivityKind), StageLineProtection to StageEntryComponent, OrphanedLineReason to CashFlowComponent, and IngestionRejectedRecord to StageEntryComponent or BaseStageEntry. Give each a toString, and change each error case to carry the string (for IngestionFileRejected, a list of already-rendered record descriptions).

**Why:** An error type is the codomain of every fallible function in its domain, so it has to compile first, and anything it references gets pulled ahead of the domain model. Once an error DU starts holding domain DUs, the error module turns into a second, hidden components module, and domain concepts end up living in it only because of compile order. StageLineProtection is already a domain value that an orchestrator computes and passes around. Errors that carry only primitives keep the dependency arrow one-way: the domain depends on its errors, and errors depend on nothing.

---

## FSDDD-RAW-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:43-85, 115-142, 196-207
- **Summary:** HoldingsAsOf's report read reads names, institution, account group, ticker, owner names and ledger code/name into bare strings that never pass through their domain constructors, and groups accounts by name.
- **Resolution:** fix-code

The new constraint 'A report read may reconstitute its own rows, but only into domain types' says: 'its reconstitute turns every column into its domain type ... No column is read into a field as a bare string, decimal or Guid.' HoldingsAsOf.mapRawForDbRead reads account_name, institution, account_group, owner_names, ledger_code, ledger_name, security_name, ticker and dimension value names with RowReader.getString/getStringOption (lines 66-80). reconstituteLine and buildAccount copy them straight into HoldingsAsOfLine/HoldingsAsOfAccount (securityName, ticker, dimensionValueNames, investmentAccountName, institution, accountGroup, ownerNames, ledgerAccountCodeAndName). SecurityName.create, Ticker.create, InvestmentAccountName.create, Institution.create, AccountGroup.create, DimensionValueName.create, PersonName.create, AccountCode.create and AccountName.create are never called. Only the numeric and DU columns are converted. Owner names come back as one string joined by a \u001f separator and are split by hand (line 125). Accounts are then grouped by `raw.accountName` (line 201), not by investment account ID, so a name stands in for identity. raw.accountName also goes into AccountSnapshotLine.confirmFigures as its label. Of the report reads in orchestration (AccountActivity, AccountBalance, AccountDeactivation, HoldingsAsOf), only this one leaves text columns unconverted.

**Action:** Select ia.unique_id, the security, person and ledger-account IDs, and dimension value IDs, not names. Read them as their typed IDs, convert every remaining column through its smart constructor in reconstitute, group by InvestmentAccountId, and let the UI bridge resolve names.

**Why:** Parse, don't validate: a smart constructor is the one gate that turns 'some string from somewhere' into 'a value the domain vouches for'. A read path that skips the gate lets unvalidated data into a typed report and makes the compiler's guarantees conditional on where a value came from. Grouping by a display name confuses identity with labelling. Entity identity is the ID, and names are attributes that can change.

---

## FSDDD-ID-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:14-37; NetWorth.fs:17-43,83; UnitRollForward.fs:14-29,44-47; Src/Business.FinancialServices.Positions/PositionsComponent.fs:433-436 (WealthGroup)
- **Summary:** Positions report records carry names and codes as bare strings in place of IDs, and in several places unwrap domain types (Quantity, ContributionBasis, TaxTreatment) to primitives.
- **Resolution:** fix-code

Two archimate rules from 905c0b9 apply: 'IDs are used in the Business tier...: a report or view row carries the IDs of the things it reports on, not their codes or names. The UserInterfaceBridge turns IDs into codes and names through App.DataAccessLayer.LookupCache', and 'Below the UserInterfaceBridge, a persisted value travels as its domain type... entity, component, composite, and report or view rows alike.' Offending fields: HoldingsAsOfLine.securityName: string, ticker: string option, dimensionValueNames: Map<Dimension,string>; HoldingsAsOfAccount.investmentAccountName/institution/accountGroup: string, ownerNames: string list, ledgerAccountCodeAndName: (string*string) option, contributionBasis: Money option (the domain type is ContributionBasis). NetWorth.LedgerAccountBalance.code/name: string (built by unwrapping AccountCode.value/AccountName.value at NetWorth.fs:143-144). NetWorthInvestmentAccount.investmentAccountName/accountGroup: string, ownerNames: string list. NetWorthProperty.propertyName: string (PropertyName.value at line 199), ownerNames. NetWorth.investmentsByAccountGroup: (string * Money) list. PositionsComponent.WealthGroup = GroupName of string | GroupOwners of string list | Unassigned: one string case holds account names, account groups, dimension value names, and TaxTreatment flattened with TaxTreatment.toString (InvestmentWealthHistory.fs:24), so the TaxTreatment DU is lost. UnitRollForward.RollForward.investmentAccountName and RollForwardRow.securityName are strings. startQuantity and endQuantity are Quantities unwrapped to decimal (quantitiesByHolding, line 45). The type comment justifies decimal only for expected and difference, which carry a sign.

**Action:** Give each Positions report record the IDs of what it reports on (InvestmentAccountId, SecurityId, PersonId set, AccountId, PropertyId, DimensionValueId) and typed values (AccountGroup, Institution, Ticker, ContributionBasis, Quantity). Make WealthGroup a DU over typed keys (e.g. ByAccount of InvestmentAccountId, ByTaxTreatment of TaxTreatment, ByOwners of Set<PersonId>, ByDimension of DimensionValueId). Bind LookupCache entries for Person, InvestmentAccount, Security, Property and DimensionValue, and resolve names in ReportConverters/ReportWriters.

**Why:** Primitive obsession throws away what the type system knows. A `string` field can hold an account name, a group label or a stringified tax treatment, and the compiler can't tell them apart. WealthGroup.GroupName shows the cost: four semantically different keys collapse into one case. Keeping IDs in the business tier and rendering names only at the boundary keeps identity stable and makes presentation a pure, swappable projection. It also gives the planned retirement engine the IDs it needs to join on, not display strings.

---

## FSDDD-ID-2 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingOrchestration.fs:12-16; SecurityOrchestration.fs:12-15,126-135; InvestmentAccountOrchestration.fs:18-22,253-263; InvestmentActivityOrchestration.fs:24-37; AccountSnapshotOrchestration.fs:26-37; PreLedgerBalanceOrchestration.fs:19-33; RealEstateOrchestration.fs:20-25,255-270; PersonOrchestration.fs (personNamesOf); PositionsLedgerLinks.fs:16,24
- **Summary:** Positions operation views, and the Business-tier helpers that build them, turn IDs into names and codes as strings below the UI bridge, which is the LookupCache's job in the UI tier.
- **Resolution:** fix-code

Every Positions view type pairs an entity with names resolved in orchestration as raw strings: HoldingView {investmentAccountName: string; securityName: string}; SecurityView.dimensionValueNames: Map<Dimension,string>, built from a Map<DimensionValueId,string> (SecurityOrchestration.fs:126-135) even though Security already holds Map<Dimension, DimensionValueId>; InvestmentAccountView {ownerNames: string list; ledgerAccountCodeAndName: (string*string) option}; ActivityView {investmentAccountName: string; securityName: string option}; RecordedRange.investmentAccountName: string; SnapshotLineView.securityName/SnapshotView.investmentAccountName: string; PreLedgerBalanceView {accountCode: string; accountName: string}; PropertyView {ownerNames; assetAccountCodesAndNames; mortgageAccountCodesAndNames: (string*string) list}. Three Business-tier helpers exist only to make this conversion: PersonOrchestration.personNamesOf : Set<PersonId> -> Result<string list>, PositionsLedgerLinks.ledgerAccountCodeAndName : AccountId -> Result<string*string>, and PositionsLedgerLinks.codeOf. The archimate IDs principle (905c0b9) says the UserInterfaceBridge turns IDs into names through LookupCache, and that 'a Business-tier type that carries another entity's code or name in place of its ID breaks this principle'. The Ledger side already follows it: Account.idToCode/idToName are used from AccountFieldConverters.fs:35-51. No Positions or Person LookupCache binding exists (grep for stringToIdCache/idToStringCache finds only Account, FiscalPeriod, MasterAgreement and PaymentAgreement). The domain confirm functions also take `accountLabel: string`/`securityLabel: string`/`accountCode: string` (AccountSnapshotLine.confirmFigures, AccountSnapshotLot.confirmLots, InvestmentActivity.confirmShape/confirmRange/confirmDatedInRange, PreLedgerBalance.confirmBeforeLedger), so callers unwrap names early to feed them in.

**Action:** Drop the name and code fields from the view records. Where an entity already carries the IDs, return the entity, or a record of entity plus related IDs. Remove personNamesOf/ledgerAccountCodeAndName/codeOf from orchestration. Add LookupCache bindings in Person, InvestmentAccount, Security, Property and DimensionValue, and resolve names in PositionsFieldConverters. Have confirm* functions take the typed name (e.g. InvestmentAccountName) and unwrap only when building the error.

**Why:** In DDD, a read model in the domain tier speaks in identities, and presentation is a projection at the edge. Resolving display names in orchestration couples business functions to presentation and adds N+1 fetches (personNamesOf does one fetchById per owner). It also means two tiers each own half of the ID-to-name mapping. With one boundary owning that translation, the business functions stay pure over IDs and their signatures say what they mean.

---

## FSDDD-ID-3 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/TrialBalance.fs:11-26; PeriodActivity.fs:23-28; Reconciliation.fs:13-19; BalanceSheetIntegrity.fs:13-18; PrePostingReview.fs:30-52
- **Summary:** Ledger and ingestion report rows identify accounts, rules, agreements and sources by code and name rather than by ID.
- **Resolution:** fix-code

These fields are typed as domain types (AccountCode, AccountName), so the primitive rule is satisfied, but the IDs principle as amended on 2026-10-11 is not: 'A report or view row carries the IDs of the things it reports on, not their codes or names... A Business-tier type that carries another entity's code or name in place of its ID breaks this principle.' TrialBalanceRowNested/Flattened carry accountCode + accountName and no AccountId; crawlAndCompile has the Account in hand and drops its ID (TrialBalance.fs:56-58, 87-88). PeriodActivityAccount carries accountCode + accountName with no AccountId. ReconciliationRow does the same. BalanceSheetIntegrity.DeactivatedAccountWithBalance has code + accountName. PrePostingLine has accountCode + accountName where the AccountId is in scope (PrePostingReview.fs:184-191), and ruleName: ClassificationRuleName option where it holds the ClassificationRuleId. PrePostingAgreement is {paymentAgreementName; masterAgreementName} where PaymentAgreementId/MasterAgreementId are in scope. PrePostingEntry.sourceName where the IngestionSourceId is available. Account.idToCode/idToName and MasterAgreement/PaymentAgreement.idToName LookupCache bindings already exist for the UI to use. Precedent check: REPORT-RECORDS covers only public versus private visibility, and its 2026-10-11 'Not covered' note excludes field typing, so it does not apply here.

**Action:** Replace each code/name pair with the entity's ID (AccountId, ClassificationRuleId, PaymentAgreementId, MasterAgreementId, IngestionSourceId), and resolve codes and names in ReportConverters/ReportWriters through the existing LookupCache bindings. Ordering by code (REQ-RPT-1.6) can stay in the computation, which has the Account in hand when it sorts.

**Why:** A code is a mutable, human-facing attribute, and an ID is identity. A report row that keeps only the code can't be joined back to its entity without a second lookup by an attribute, and it ties the report's meaning to whatever the code was when the report ran. Carrying the ID keeps the read model referentially sound and leaves rendering to the boundary.

---

## FSDDD-PRIM-1 — idiom
- **Location:** Src/Business.CrossDomainOrchestration/PrePostingReview.fs:19-27,170-173
- **Summary:** PrePostingPayment unwraps CashFlow domain types (PaymentAmount, InvoiceDate, DueDate, InvoiceAmount) to Money and LocalDate in the business tier.
- **Resolution:** fix-code

PrePostingPayment declares paymentAmount: Money, invoiceDate: LocalDate, dueDate: LocalDate, invoiceAmount: Money and is built with `PaymentAmount.value`, `InvoiceDate.value`, `DueDate.value` and `InvoiceAmount.value` (lines 170-173). CashFlowComponent defines InvoiceDate, DueDate, InvoiceAmount and PaymentAmount as private wrappers (CashFlowComponent.fs:349-377). The archimate constraint 'Below the UserInterfaceBridge, a persisted value travels as its domain type, never as the primitive it unwraps to... Domain types are unwrapped to primitives in one place only: the UserInterfaceBridge's boundary converters' applies. The same unwrap pattern appears in AccountSnapshotOrchestration.fs:182 and NetWorth.fs:143-144,199 (covered under FSDDD-ID-1 and FSDDD-ID-2).

**Action:** Type the four fields as PaymentAmount, InvoiceDate, DueDate and InvoiceAmount, drop the .value calls, and unwrap in ReportConverters/PrePostingReviewWriter.

**Why:** A wrapper type like DueDate exists so that a due date and an invoice date, both LocalDate underneath, can't be swapped silently. Unwrapping them in a business record puts two LocalDate fields side by side with only their labels to tell them apart, and the compiler can no longer catch a swap. The unwrap belongs at the edge, where the value leaves the domain.

---

## FSDDD-PRIM-2 — idiom
- **Location:** Src/Business.FinancialServices.Ledger/Account.fs:295-312; Src/Business.FinancialServices.Ledger/FiscalPeriod.fs:128; Src/Business.FinancialServices.Ledger/JournalEntryComponent.fs:100-110; Src/Ui.InterfaceBridge/Routes/AccountRoutes.fs:71,81
- **Summary:** Ledger entity-module functions take raw strings where a domain type exists: Account.updateAccountNameById (string), updateExternalReferenceById (string option), and FiscalPeriod.fetchIdByKey (string, though FiscalPeriodKey exists).
- **Resolution:** fix-code

Account.updateAccountNameById takes `newName: string` and calls AccountName.create inside the entity module. updateExternalReferenceById takes `newReference: string option` and calls AccountExternalReference.create inside. AccountRoutes.fs:71 and :81 pass the raw contract strings straight in, skipping any boundary converter, so validation that belongs at the boundary runs inside the domain module. Account.update right above them (line 250) already takes FieldUpdate<AccountName> and FieldUpdate<AccountExternalReference option>, so the string variants only skip the boundary. FiscalPeriod.fetchIdByKey takes `key: string`, and its one Src caller (EntryDate.create, JournalEntryComponent.fs:104-106) builds a FiscalPeriodKey with FiscalPeriodKey.ofDate and then unwraps it with FiscalPeriodKey.value just to call it.

**Action:** Remove updateAccountNameById and updateExternalReferenceById, and have the routes convert to AccountName and AccountExternalReference in AccountFieldConverters and call Account.update (or an orchestrator) with FieldUpdate values. Change fetchIdByKey to take FiscalPeriodKey.

**Why:** A function signature is a contract. `string -> Result<Account,_>` tells the caller nothing about what a valid name is and makes the domain module double as an input parser. Taking AccountName pushes parsing to the boundary and lets the type prove validity: the function can only receive a name that has already passed AccountName.create. Wrapping a key only to unwrap it on the next line is the clearest sign of primitive obsession: the callee asks for less than the caller already has.

---

## FSDDD-COMP-1 — contradiction
- **Location:** Architecture/SonOfLeo.archimate ('Composite types are defined in the cross domain orchestration tier', documentation added 905c0b9) vs CompoundedLearnings/articles/architecture/type-taxonomy.md (Composite types) and type-placement-by-compile-tier.md; Src/Business.CrossDomainOrchestration/JournalEntryOrchestration.fs:20-25, StageEntryOrchestration.fs:23-28, AgreementOrchestration.fs:19-25, InstanceOrchestration.fs:22-30
- **Summary:** The new composite definition (it must span more than one domain, and a one-domain record belongs in that domain's components module) contradicts the type-taxonomy article, which names the single-domain JournalEntry as the canonical composite. Compile order also makes the prescribed placement impossible for records that hold entities.
- **Resolution:** dan-decides

The archimate now says: 'A composite is built from the types of more than one business domain. A record whose fields all come from one domain is that domain's component type and is defined in that domain's components module.' type-taxonomy.md says: 'Composite types: Multi-part entities composed and validated at the orchestrator. JournalEntry (header + lines + references + comments)... The composite and its components live in the same namespace slice.' orchestration-layer.md reason 2 makes the same point. Read literally, the new text turns every current single-domain composite into a misplaced component: JournalEntry (all Ledger), StageEntry (all DataIngestion), Agreement, InstanceComposite and InvoiceComposite (all CashFlow). It then directs them into JournalEntryComponent.fs, StageEntryComponent.fs and CashFlowComponent.fs. Those modules compile before the entity modules the records hold (e.g. JournalEntryComponent precedes JournalEntryHeader.fs in Business.FinancialServices.Ledger.fsproj), so they cannot see JournalEntryHeader.JournalEntryHeader and the move cannot compile. The same applies to one-domain report records that hold entities, such as BalanceSheetIntegrity.DeactivatedAccountWithBalance (holds JournalEntryHeader list), HoldingView, and HoldingsAsOfLine (holds AccountSnapshotLot list). 'A type lives in the lowest compile tier that can see everything it references' would put these in the last file of their domain project, not in the components module.

**Action:** Dan to rule whether the 'more than one domain' test is meant for report and view records only, with aggregate composites validated in orchestration kept as composites, and where a one-domain record that holds entity types should live (components module, a new trailing file in the domain project, or orchestration). Then update type-taxonomy.md or the archimate documentation to match.

**Why:** In DDD the line between an aggregate and a component is about invariants (who enforces the consistency boundary), not about how many bounded contexts its fields come from. JournalEntry is a single-context aggregate whose invariants (two or more lines, debits equal credits) need orchestration because IDs don't exist before insert. A placement rule that ignores that, and also asks for an order F# won't compile, will be applied inconsistently by agents. Settling the definition once avoids churn.

---

## FSDDD-PLACE-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/TrialBalance.fs:11-26; PeriodActivity.fs:15-28; Reconciliation.fs:13-19; AccountActivity.fs:15-36; AccountBalance.fs:16-21; FetchFilterAndSort.fs:17-47,90-93; PrePostingReview.fs:19-32; InvestmentWealthHistory.fs:13-17; UnitRollForward.fs:14-29; NetWorth.fs:24-33
- **Summary:** Many records in orchestration draw every field from one domain (plus the shared Money, Quantity and Price), so under the 905c0b9 constraint they are that domain's component types, defined in the wrong tier.
- **Resolution:** fix-code

These records reference only component-level types of one domain, so nothing forces them up to orchestration: TrialBalanceRowNested/Flattened (AccountCode, AccountName, Money); PeriodActivityLine/PeriodActivityAccount (JournalEntryHeaderId, JournalEntryDescription, JournalEntryLineType, JournalEntryLineMemo, AccountCode, AccountName, Money); ReconciliationRow (AccountCode, AccountName, Money); AccountActivityDetail/AccountActivity and AccountBalance (Ledger ID and component types only); AccountActivityFilter and JournalEntryFetchFilter (Ledger only); AgreementFilter (MasterAgreementId only); PrePostingPayment (PaymentId, InvoiceId, PaymentState, Money) and PrePostingAgreement (PaymentAgreementName, AgreementName), CashFlow only; WealthPoint (WealthGroup, Money) and NetWorthInvestmentAccount (TaxTreatment, Provenance, Money), Positions only. Each could compile in its domain's components module (AccountComponent/JournalEntryComponent, CashFlowComponent, PositionsComponent). The archimate principle 'Any component type in the cross domain orchestration or higher is a signal that we're violating the tiering or domain principles somewhere' and the 905c0b9 composite documentation both apply. Precedent check: REPORT-RECORDS's 2026-10-11 'Not covered' note says it does not govern where report records are defined. Records that hold entity types are left out here; see FSDDD-COMP-1.

**Action:** After Dan rules on FSDDD-COMP-1, move each listed record to its domain's components module, keeping the computing functions in orchestration.

**Why:** Putting a type in the lowest tier that can see its dependencies makes the dependency graph the documentation. A Ledger-only row declared in orchestration suggests it needs cross-domain knowledge when it doesn't. It also leaves the domain tier unable to name its own read models, so any future domain-level function over trial-balance rows would have to move up a tier too.

---

## FSDDD-CTOR-1 — architecture
- **Location:** Src/Business.FinancialServices.Ledger/JournalEntryComponent.fs:92-121 (EntryDate.create, createWithFiscalPeriodId)
- **Summary:** EntryDate.create is a component smart constructor that takes a Context and reads the database, which the validation-layers article forbids, and it needs an `internal` escape hatch to avoid that I/O on reads.
- **Resolution:** dan-decides

CompoundedLearnings/articles/coding/validation-layers.md, 'What doesn't': 'Putting state-dependent checks inside construction — construction has no access to external state.' Layer 2 defines component smart constructors as 'per-value validation'. EntryDate.create (context: Context.Context) (entryDate: LocalDate) calls FiscalPeriod.fetchIdByKey, a DB read, to pair the date with its fiscal period ID, and translates FiscalPeriodNoPeriodMatchingKey into JournalEntryDateNotInFiscalPeriod. Because of that I/O, a second constructor, `createWithFiscalPeriodId` (marked internal with a WARNING comment), exists to skip the lookup during reconstitute. The value type therefore has two construction paths with different guarantees, and the safety of one depends on caller discipline. No resolved-findings entry covers EntryDate.

**Action:** Dan to decide whether EntryDate stays an I/O-bound value. The alternative is a pure EntryDate.create (entryDate, fiscalPeriodId) whose consistency the orchestrator establishes (fetch the period, then construct), which removes the internal bypass and keeps component constructors pure.

**Why:** A smart constructor should be a total, pure function from raw input to Result. That is what makes a value of the type proof of validity, whatever the context. Once construction needs a database, the type's guarantee depends on the environment, the constructor can't be used in pure code or tests without a DB, and the read path needs a trust-me back door. Effects belong in the orchestrator, which composes I/O with pure construction.

---

## FSDDD-CACHE-1 — idiom
- **Location:** Src/Business.FinancialServices.Ledger/Account.fs:317-319; FiscalPeriod.fs:192-193; Src/Business.FinancialServices.CashFlow/MasterAgreement.fs:328-329; PaymentAgreement.fs:342-343; Src/Ui.InterfaceBridge/BoundaryConverters/AccountFieldConverters.fs:145
- **Summary:** The LookupCache bindings exported by Business entity modules are typed Cache<string, Guid> and Cache<Guid, string>, so codes and IDs leave the business tier as bare primitives, and the UI re-wraps and re-validates them.
- **Resolution:** fix-code

Account.codeToId/idToCode/idToName, FiscalPeriod.keyToId/idToKey, MasterAgreement.nameToId/idToName and PaymentAgreement.nameToId/idToName are public Business-tier bindings whose types are App.DataAccessLayer.LookupCache.Cache<string,Guid> and Cache<Guid,string>. Callers have to unwrap typed IDs to call them (AccountFieldConverters.fs:35 `id |> AccountId.value |> Account.idToCode.fetch`), wrap the Guid back into AccountId, and re-run the smart constructor on a code the database already holds (line 145 `idToCode.fetch ... |> Result.bind AccountCode.create`). The 905c0b9 constraint says a Business-tier type holding a persisted value as a bare string or Guid breaks the rule. The generic Cache in App.DataAccessLayer rightly works on primitives, since that tier has no domain types; the leak is the entity modules re-exporting it untyped. Src/README.md describes these bindings, so Dan should confirm the typed form fits the intended design.

**Action:** In each entity module, wrap the binding in typed accessors (e.g. Account.idToCode : Context -> AccountId -> Result<AccountCode, IAppError>, Account.codeToId : Context -> AccountCode -> Result<AccountId, IAppError>) that apply fromGuid and the smart constructor once, and keep the raw Cache private.

**Why:** A module's public surface should speak its domain's language. An infrastructure generic is fine inside, but when Cache<Guid,string> leaks out, every caller has to know the column semantics and redo the wrapping. Wrapping the cache once in a typed adapter is the standard F# way to put a primitive-level mechanism behind a domain-level signature.

---

