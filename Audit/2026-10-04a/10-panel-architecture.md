# SonOfLeo architecture auditor (ArchiMate model vs Src)

## ARCH-POS-1 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/PositionsLedgerLinks.fs:15-130; InvestmentOrchestration.fs:14,305,418; RealEstateOrchestration.fs:15,133,135,209
- **Summary:** Investment and real-estate orchestration both depend on PositionsLedgerLinks, a module that reads both peers, so each sub-domain reaches into the other.
- **Resolution:** dan-decides

Rule: Principle 'Domains Build Upward' says: 'Inside Business.FinancialServices.Positions: investments and real estate are unordered peers. Neither may use the other; anything that combines them (net worth, investable wealth) is orchestration.' It also says the order applies 'at every zoom level', and that inside orchestration a sibling may not use a higher or peer sibling. PositionsLedgerLinks.confirmNotLinkedElsewhere (lines 30-50) calls both InvestmentAccount.fetchByLedgerAccountId and Property.fetchByLedgerAssetAccountId, and its public type LinkingRecord names both InvestmentAccountId and PropertyId. It compiles below both peer orchestrators, and both use it. InvestmentOrchestration.createInvestmentAccount (line 305) and updateInvestmentAccount (line 418) call confirmInvestmentAccountLink, which reads Property, so an investment use case reads real-estate records. RealEstateOrchestration.createProperty (line 133) and updateProperty (line 209) call confirmPropertyAssetLink, which reads InvestmentAccount. The only module the rule lets combine the peers is one above both, as NetWorth.fs is. This also contradicts Dan's statement that the two peers can't reference each other, since each peer's use cases read the other's records through the shared guard.

**Action:** Have Dan rule. Either move the shared 'one ledger account stands for at most one Investment Account or Property' guard into a combining orchestrator above both peers, the way NetWorth combines them, or record an approved exception for the cross-peer link check under 'Out-of-Order Is a Design Smell'.

**Why:** The peer rule exists so that neither sub-domain gains knowledge of the other. A shared helper below both peers lets each one silently depend on the other, and that is the kind of out-of-order reach the model says must go to Dan rather than be quietly accommodated.

---

## ARCH-POS-2 — architecture
- **Location:** PersonOrchestration.fs:30,48-55; InvestmentOrchestration.fs:61-71,161-169,212-233,266-292,482-534,546-553; AccountSnapshotOrchestration.fs:15-29,165,232-262; RealEstateOrchestration.fs:17-38,69-84,174-176,257-317
- **Summary:** Person and Positions orchestration identify records by human-readable names, not by type-wrapped IDs.
- **Resolution:** dan-decides

Rule: Principle 'IDs are used in the Business tier while human-readable strings are used at the UI boundary'. Its documentation says the Business tier 'should always use these ID fields for identification (e.g., the Business tier function for updating an account should take an account ID as its identifier)' and 'The UI tier is expected to translate that code and pass the ID down to the Business tier'. The older domains follow this: ledger account codes are turned into AccountId in the UI by AccountFieldConverters.fallibleConverterAccountCodeToAccountId, and ClassificationOrchestration.updateClassificationRule takes a ClassificationRuleId. The new orchestrators instead take names and resolve them in the Business tier. Examples: PersonOrchestration.updatePerson takes currentName: PersonName. InvestmentOrchestration.renameDimensionValue takes dimension plus currentName; updateSecurity takes currentName: SecurityName; createHolding and changeHoldingBasisMethod take InvestmentAccountName and SecurityName; NewInvestmentAccount.owners and InvestmentAccountUpdate.ownersUpdate are PersonName lists; InvestmentAccountUpdate.currentName is a name. AccountSnapshotOrchestration's SnapshotInput.investmentAccountName and SnapshotLineInput.securityName are names, and deleteSnapshot, fetchSnapshot and listSnapshotDates take InvestmentAccountName. RealEstateOrchestration's NewProperty.owners, PropertyUpdate.currentName, recordValuation, deleteValuation and listValuations take names. The name-to-record lookups (fetchPersonByName, fetchSecurityByName, fetchInvestmentAccountByName, fetchPropertyByName, dimensionValueByName) all run in the Business tier.

**Action:** Move the name-to-ID resolution into the Positions and Person boundary converters, the way fallibleConverterAccountCodeToAccountId works, and change the orchestration signatures to take PersonId, SecurityId, InvestmentAccountId, PropertyId and DimensionValueId. Or Dan approves name-addressing as an exception for these domains.

**Why:** The principle keeps a firm split: the UI is the only layer that knows how the operator names things, and the Business tier works only with identity. Resolving names inside orchestration mixes the two, and it makes these domains inconsistent with Ledger, CashFlow and Classification.

---

## ARCH-POS-3 — architecture
- **Location:** InvestmentOrchestration.fs:212 (NewInvestmentAccount), 223 (InvestmentAccountUpdate); RealEstateOrchestration.fs:17 (NewProperty), 28 (PropertyUpdate); AccountSnapshotOrchestration.fs:15 (SnapshotLineInput), 23 (SnapshotInput)
- **Summary:** Orchestration defines 'new entity' and update records that carry an entity's business fields, which the convenience-type rule forbids.
- **Resolution:** dan-decides

Rule: Principle 'Avoid creating types strictly for code convenience'. Its documentation describes the temptation to 'create types that would comprise all of the "business" fields of those types, but without the not-yet-known fields' and says 'Do not do that. I do not want to maintain yet another type that looks strikingly similar to our domain types... Instead, create tuples of the primitives that the domain types would need to create and pass them into their respective constructNewAndPersist functions'. NewInvestmentAccount repeats InvestmentAccount's fields (name, institution, accountGroup, taxTreatment, owners, activityPeriod, ledgerAccountId) minus id and timestamps. NewProperty does the same for Property. SnapshotInput and SnapshotLineInput do the same for AccountSnapshotHeader and AccountSnapshotLine, with a security name in place of a holding id. InvestmentAccountUpdate and PropertyUpdate are second FieldUpdate records alongside the entity modules' own InvestmentAccountFieldUpdates and PropertyFieldUpdates. PositionsFieldConverters.fs:101,126,185,254,281 exist mainly to fill these types.

**Action:** Remove the six records. Pass tuples of domain-typed values into a constructNewAndPersist (or update) function, as the documentation prescribes, or have Dan approve them as an exception.

**Why:** Every near-copy of an entity type is a second place to keep in step whenever a field is added. The principle was written to stop that kind of maintenance cost from spreading.

---

## ARCH-POS-4 — idiom
- **Location:** PersonOrchestration.fs:38 createPerson; InvestmentOrchestration.fs:48 createDimensionValue, 143 createSecurity, 294 createInvestmentAccount, 482 createHolding; RealEstateOrchestration.fs:124 createProperty
- **Summary:** The new orchestration functions that validate an entity and save it are named create<X>, not constructNewAndPersist.
- **Resolution:** dan-decides

Rule: Principle 'Name orchestration functions that both validate an entity and save it to the DB "constructNewAndPersist"', part of 'Reuse common function names'. Each listed function runs confirm checks, calls the entity's create, then its persist; createPerson at lines 39-45 is an example. Every earlier domain uses the prescribed name: AccountCreation, FiscalPeriodCreation, the JournalEntry*Orchestration modules, AgreementOrchestration, InstanceOrchestration, CashFlowOps and ClassificationOrchestration all expose constructNewAndPersist. Separately, the name create<X> is the one the sibling principle 'Name functions that instantiate entities from a complete set of domain types "create"' reserves for the infallible entity constructor.

**Action:** Rename these to constructNewAndPersist, qualified by module, for example in a PersonOrchestration, SecurityOrchestration or HoldingOrchestration module. Or Dan approves the create<X> naming.

**Why:** Shared names exist so that a reader knows what a function does from its name across every domain. Reusing 'create' for a validating, persisting orchestrator blurs the line between the infallible constructor and the orchestrator.

---

## ARCH-POS-5 — idiom
- **Location:** Src/Business.FinancialServices.Positions/AccountSnapshotLine.fs:54 (checkFigures)
- **Summary:** AccountSnapshotLine.checkFigures validates and returns Result<unit, IAppError>, but its name has no 'confirm' prefix.
- **Resolution:** dan-decides

Rule: Principle 'Prefix function names with "confirm" that validate a type and return only a unit result'. checkFigures checks the quantity sign, market-value sign, cost-basis sign and the 0.05 tolerance, and returns Result<unit, IAppError>. AccountSnapshotOrchestration.fs:119 calls it with do!. Every other unit-returning validator in Business follows the convention (Invoice.confirmAmountIsPositive, Account.confirmTypeAndSubtypeAreValid, Cadence.confirmWeekDay, PositionsLedgerLinks.confirmMortgageLink and others). The model's capability for this function is also worded 'Check a snapshot line's reported figures'.

**Action:** Rename it to confirmFigures (or confirmReportedFigures) and update the caller. Also find out why Checks/check-confirm-naming.sh did not catch it.

**Why:** The confirm prefix tells a caller that the function is a pure gate with no payload. An outlier name breaks that signal and also slips past the automated check.

---

## ARCH-POS-6 — idiom
- **Location:** Src/Business.FinancialServices.Positions/DimensionValue.fs:119 (rename); Valuation.fs:75 (replace); AccountSnapshotHeader.fs:72 (replace)
- **Summary:** Three new functions run SQL UPDATE statements, but none is named update or update<Something>.
- **Resolution:** dan-decides

Rules: Principle 'Name functions that provide the basic database update "backbone" for an entity type "update"' and Principle 'Prefix function names with "update" when they call their module's update function to update DB rows in specific manners'. DimensionValue.rename is the module's only UPDATE of positions.dimension_value. Valuation.replace and AccountSnapshotHeader.replace are each their module's only UPDATE, of positions.valuation and positions.account_snapshot. None of the three modules has an update backbone, and none of the functions carries the update prefix. By contrast, Holding.updateBasisMethod in the same slice follows the prefix convention. Earlier code has verb exceptions such as Invoice.cancel and Instance.cancel, but they describe state transitions, not field overwrites.

**Action:** Rename them update (or updateName, updateValueAndBasis, updateProvenanceAndContributionBasis), or Dan approves rename and replace as domain verbs.

**Why:** Shared names let a reader find every write path in a module by name. 'replace' and 'rename' hide that a row is overwritten.

---

## ARCH-POS-7 — architecture
- **Location:** Src/Business.General/Person.fs:15 (PersonId), 21 (PersonName)
- **Summary:** Person's component types are defined inside the Person entity module, not in a components module.
- **Resolution:** dan-decides

Rule: Constraint 'Component types are defined in a components module in the lowest tier of their business domain', which realizes Principle 'A type lives in the lowest compile tier that can see everything it references'. PersonId and PersonName are component types: a private single-case ID wrapper and a validated bounded name with create and value. They are declared at the top of Person.fs, the entity module. Every other domain keeps such types in a components module: AccountComponent, FiscalPeriodComponent, JournalEntryComponent, StageEntryComponent, CashFlowComponent, ClassificationComponent, and PositionsComponent in this slice, which holds SecurityId, SecurityName and the rest. Business.General has no components module. As a result, InvestmentAccount.fs:14, Property.fs:13, PositionsFieldConverters (through PersonFieldConverters) and RealEstateOrchestration.fs:9 must open the whole Person entity module just to name PersonId or PersonName.

**Action:** Move PersonId and PersonName into a Business.General components module (for example PersonComponent.fs, compiled before Person.fs). Or Dan approves keeping them inline.

**Why:** Keeping components apart from the entity lets higher modules refer to an identity or value type without depending on the entity's persistence code. The other domains already show the intended shape.

---

## ARCH-POS-8 — architecture
- **Location:** InvestmentWealthHistory.fs:12 (WealthGrouping with toString/fromString), 35 (WealthGroup); NetWorth.fs:34 (PropertyValueSource); PositionsLedgerLinks.fs:15 (LinkingRecord)
- **Summary:** The slice defines component-style discriminated unions in Business.CrossDomainOrchestration.
- **Resolution:** dan-decides

Rules: Principle 'Any component type in the cross domain orchestration or higher is a signal that we're violating the tiering or domain principles somewhere' and Constraint 'Component types are defined in a components module in the lowest tier of their business domain'. WealthGrouping is a closed vocabulary with all, toString, and a fallible fromString that returns PositionsError.PositionsInvalidWealthGrouping. That is the same shape as Dimension, TaxTreatment and Provenance in PositionsComponent, and it references only Dimension, so it could live there. WealthGroup, PropertyValueSource and LinkingRecord are DUs of Positions values and IDs. None of them is a composite of entities; all of them refer only to Positions-level types.

**Action:** Move WealthGrouping (and WealthGroup and PropertyValueSource if they are judged components) into PositionsComponent. Or Dan confirms they are orchestration-only types and records the exception.

**Why:** The principle treats a component type above the domain tier as a signal that domain vocabulary has leaked into the wrong layer. Here WealthGrouping's parse error is already a PositionsError, which shows the type belongs to the domain.

---

## ARCH-POS-9 — architecture
- **Location:** InvestmentOrchestration.fs:80,236,459; AccountSnapshotOrchestration.fs:31,37,43; RealEstateOrchestration.fs:41; HoldingsAsOf.fs:13,24; NetWorth.fs:15,21,38,48; InvestmentWealthHistory.fs:40; readers: PositionsFieldConverters.fs:55-59,88-97,155-181,219-231,238-247; ReportConverters.fs (netWorth.*, point.*); NetWorthWriter.fs:90-173; PositionsRoutes.fs:197-198
- **Summary:** The slice's composite types are public records, and the UI tier reads their fields directly instead of through accessor functions.
- **Resolution:** dan-decides

Rules: Constraint 'Entity, component, and composite type definitions are private' ('to encapsulate instantiation within the module') and Constraint 'Access to Entity, component, or composite type fields is strictly through accessor functions'. Every composite this slice adds to orchestration is a plain public record: SecurityView, InvestmentAccountView, HoldingView, SnapshotLineView, SnapshotView, RecordedSnapshot, PropertyView, HoldingsAsOfLine, HoldingsAsOfAccount, LedgerAccountBalance, NetWorthInvestmentAccount, NetWorthProperty, NetWorth and WealthPoint. Converters, writers and routes read them by field, for example view.security, view.ownerNames, account.lines, netWorth.totalInvestments, r.snapshot and r.replacedExisting. The entity and component types in the same slice follow the rule (private records with accessors), and older composites such as AgreementOrchestration.Agreement, InstanceComposite, AccountBalance and JournalEntry are private. Some older report composites (BalanceSheetIntegrity, PeriodActivity, PrePostingReview, AccountActivity) share the public-record pattern, and no ruling on them appears in the precedent ledger.

**Action:** Make the new composites private records with accessor functions named after their fields, and switch the converters and writers to the accessors. Or Dan approves public read-model records for report and view composites, and the precedent ledger records it.

**Why:** Private definitions keep construction inside the owning module, so a composite can't be built in an inconsistent state elsewhere. Accessors keep field renames local to that module.

---

## ARCH-POS-10 — architecture
- **Location:** Src/Business.CrossDomainOrchestration/HoldingsAsOf.fs:137-184; Src/Business.FinancialServices.Positions/PositionsComponent.fs:93 (Dimension.securityColumn)
- **Summary:** HoldingsAsOf runs its own SQL in orchestration, joining positions tables with general.person and ledger.account and bypassing every domain module's query and reconstitute.
- **Resolution:** dan-decides

Rules: Assessment 'A read-only join into another domain's tables is not orchestration' (which influences Principle 'Encapsulation conventions are enforced'), Constraint 'Query functions should only be called by functions within the same business domain', and Principle 'Business domains are real boundaries'. fetchHoldingsAsOf builds a 25-line SELECT that joins positions.account_snapshot, investment_account, investment_account_owner, account_snapshot_line, holding, security and seven aliased dimension_value tables. It also reads general.person (owner names) and ledger.account (code and account_name), and runs it with executeReaderQuery straight from Business.CrossDomainOrchestration. It hand-maps the result into a private RawRow and rebuilds Quantity, Price and Money outside their owning modules. It repeats the Investment Account active-period rule as SQL (sia.active_begin <= @as_of and (sia.active_end is null or sia.active_end >= @as_of)) instead of using ActivityPeriod.isActive. To make the SQL possible, the component module exposes the security table's column names (Dimension.securityColumn). In this orchestration project, only the older AccountActivity, AccountBalance and AccountDeactivation run SQL directly, and AccountDeactivation is the documented SQL example in Constraint 'A validation is answered by a SQL query only when it is a pure data question that domain types cannot practically answer'. HoldingsAsOf is not a validation.

**Action:** Build holdings-as-of from the domain modules (AccountSnapshotHeader, AccountSnapshotLine, Holding, Security, DimensionValue, InvestmentAccount, plus Person and Account fetches for names), or move the join into a Positions-owned query function. Or Dan approves the raw join for performance and records it.

**Why:** The assessment exists because a cross-schema join written in orchestration couples orchestration to three domains' table layouts and repeats their rules in SQL. A later column or rule change in any of those domains would silently miss this query.

---

## ARCH-POS-11 — architecture
- **Location:** Src/Ui.OperatorCli/Program.fs:9; Src/Ui.InterfaceBridge/BoundaryConverters/PositionsFieldConverters.fs:32-33; Src/Ui.InterfaceBridge/Routes/PersonRoutes.fs:9-10; pre-existing: Business.FinancialServices.Ledger/JournalEntryHeader.fs:5-6, JournalEntryExternalReference.fs:5-6, Business.FinancialServices.Classification/ClassificationRule.fs:5-7, Business.CrossDomainOrchestration/JournalEntryVoiding.fs:4-5, Ui.InterfaceBridge/Routes/ReportRoutes.fs:16-18
- **Summary:** Several files open Src modules out of build order: three new ones in this slice and five older ones.
- **Resolution:** dan-decides

Rule: Constraint 'Opens Follow Build Order': 'Within each Src file, open statements for Src modules list them in build order: baser modules first... An open that has to break that order is reported to Dan.' New in this slice: (1) Program.fs opens Routes.PersonRoutes on line 9, after CashFlowRoutes, but PersonRoutes compiles first among the routes in Ui.InterfaceBridge.fsproj, before AccountRoutes. (2) PositionsFieldConverters.fs opens AccountFieldConverters (line 32) before PersonFieldConverters (line 33), but PersonFieldConverters compiles before AccountFieldConverters. (3) PersonRoutes.fs opens Business.General.Person (line 9) before Business.General.BizGeneralAuditableAction (line 10), but BizGeneralAuditableAction compiles before Person in Business.General.fsproj. Older code, with no matching precedent-ledger entry: JournalEntryHeader, JournalEntryExternalReference, ClassificationRule and JournalEntryVoiding open App.Utility.FieldUpdate (compile position 9) before App.Utility.Result (position 3), and ClassificationRule also opens App.Utility.Json.Json after FieldUpdate. ReportRoutes opens Ui.InterfaceBridge.ReportWriters (line 16) before InterfaceContracts.ReportsContracts and BoundaryConverters.ReportConverters (lines 17-18), which compile earlier. Not reported: every route file opens App.Operation before App.DataAccessLayer, but those are unordered App siblings.

**Action:** Reorder the opens to compile order: in Program.fs, put PersonRoutes before AccountRoutes; in PositionsFieldConverters, put PersonFieldConverters before AccountFieldConverters; in PersonRoutes, put BizGeneralAuditableAction before Person; in the older files, put Result and Json before FieldUpdate, and contracts and converters before ReportWriters.

**Why:** The constraint makes each file's opens show the dependency ladder at a glance. An out-of-order open is either a formatting slip or a sign of a real dependency inversion, and the model routes both to Dan instead of leaving them to drift.

---

## ARCH-POS-12 — architecture
- **Location:** Src/Ui.InterfaceBridge/InterfaceContracts/PositionsContracts.fs:53 (SecurityCreateInput.dimensionValues), 150-174 (AccountSnapshotLineContract)
- **Summary:** Two Positions input contracts reuse a shared or return type instead of a type specific to their use case.
- **Resolution:** dan-decides

Rule: Principle 'Input contract types should not be re-used. they are use-case specific'. SecurityCreateInput.dimensionValues is typed DimensionValueReturn list, which is the return contract of the DimensionValue routes, used here as input. AccountSnapshotLineContract is used both in AccountSnapshotInput.lines (input, line 163) and in AccountSnapshotReturn.lines (return, line 174), so the record-snapshot input shape and the fetch/delete return shape are tied together. Every other Positions use case has its own input type, including separate AccountSnapshotDeleteInput and AccountSnapshotFetchInput with identical fields, which shows the convention was otherwise followed.

**Action:** Add use-case-specific input types, for example SecurityDimensionValueInput for create, and an AccountSnapshotLineInput separate from the return line type.

**Why:** Input contracts are use-case specific so that changing a return payload never silently changes what an operator must send.

---

## ARCH-POS-13 — architecture
- **Location:** Architecture/SonOfLeo.archimate: ApplicationComponent 'Business.FinancialServices.Positions' (path Src/Business.FinancialServices.Positions/Business.FinancialServices.Positions.fsproj)
- **Summary:** In the model, the Positions project realizes no rule, while every sibling FinancialServices project realizes 'Foundations Are the Base of Their Container'.
- **Resolution:** dan-decides

Model drift. Principle 'Foundations Are the Base of Their Container' is realized by App.Utility, App.DataAccessLayer, App.Operation, Business.General, Business.FinancialServices, Business.FinancialServices.Ledger, DataIngestion, CashFlow, Classification, Business.CrossDomainOrchestration and the three Ui projects. It is not realized by Business.FinancialServices.Positions, which has no outgoing realization at all. The code does conform: PositionsError and PositionsAuditableAction compile first and open nothing above them. Principle 'Domains Build Upward' has its own bullet for this project ('Inside Business.FinancialServices.Positions: investments and real estate are unordered peers'), yet only Business.FinancialServices.Ledger realizes it among the domain projects. So the model does not point an auditor at the place where the peer rule applies most directly.

**Action:** Add to the model: Realization edges from ApplicationComponent 'Business.FinancialServices.Positions' to 'Foundations Are the Base of Their Container' and to 'Domains Build Upward'.

**Why:** The model is a constraint model for auditors. A component that realizes nothing tells an auditor its rules apply only 'wherever the wording says', which weakens the check exactly where the new peer rule matters most.

---

## ARCH-POS-14 — architecture
- **Location:** Architecture/SonOfLeo.archimate: ApplicationFunctions 'Compute net worth as of a date' and 'Compute investment wealth history' (folder Ledger functions > Ledger reporting functions)
- **Summary:** The model files the two Positions-driven reports under Ledger reporting functions, not Positions functions.
- **Resolution:** dan-decides

Rule: Principle 'The Positions domain owns positions capabilities', realized by ApplicationFunction 'Positions functions', and Principle 'For cross domain use cases, the use case should be considered to be a component of the domain tier that best aligns to the use case'. Both reports are composed under 'Ledger reporting functions', alongside trial balance, period activity and balance-sheet integrity. InvestmentWealthHistory.fs touches no Ledger module: it reads only HoldingsAsOf, Positions components and Money. NetWorth.fs is the orchestration that combines investments and real estate, which Principle 'Domains Build Upward' names as positions-level ('net worth, investable wealth'). Dan's statement also places net worth in the positions domain. So the model assigns ownership of a positions use case to the Ledger.

**Action:** Add to the model (re-home): compose both functions under 'Positions functions' (or a Positions reporting sub-group) instead of 'Ledger reporting functions'.

**Why:** Capability placement is how the model expresses domain ownership. Filing a positions use case under Ledger hides it from an auditor checking what the Positions domain owns.

---

## ARCH-POS-15 — architecture
- **Location:** Architecture/SonOfLeo.archimate: ApplicationFunction 'Maintain persons'; Src/Ui.InterfaceBridge/Routes/PersonRoutes.fs:26-34,50-55; PersonOrchestration.fs:48-70
- **Summary:** The 'Maintain persons' capability says 'create, rename and list', but the Person Update route also changes a Person's birthdate.
- **Resolution:** dan-decides

Model drift (route vs capability). The capability's documentation reads 'Create, rename and list the people who own accounts and property'. The operator route Person/Update (PersonRoutes.fs:50-55, 'Update a Person's name and birthdate') and PersonOrchestration.updatePerson accept birthdateUpdate: FieldUpdate<LocalDate> and check it with confirmBirthdateNotInFuture. Updating a birthdate is a use case the code offers that no capability describes.

**Action:** Add to the model: reword 'Maintain persons' to 'Create, update (name and birthdate) and list...'.

**Why:** Capabilities are the model's list of what the system offers. An undescribed use case is invisible to anyone auditing from the model.

---

## STMT-POS-1 — statement-delta
- **Location:** Src/App.Utility/Json.fs:18-38 (missingFieldMessage); e.g. Ui.InterfaceBridge/InterfaceContracts/PositionsContracts.fs:158-167 (AccountSnapshotRecordInput wrapping AccountSnapshotInput)
- **Summary:** Dan says fromJson now names the field a payload actually left out, but the fix covers only fields of the top-level record being read.
- **Resolution:** dan-decides

Dan's statement: 'App.Utility.Json.fromJson, which every route shares, now names the field a payload actually left out.' missingFieldMessage<'T> corrects the library message only when it begins 'Missing field for record type {typeof<'T>.FullName}: ', that is, only when the missing field belongs to the outermost record. For any nested record it returns the library message unchanged, and the code's own comment says that message can name a null-sent option field instead of the field actually left out. Nested payloads are common in this slice. AccountSnapshot/Record reads AccountSnapshotRecordInput { snapshots: AccountSnapshotInput list }, and AccountSnapshotInput contains an option field (contributionBasis) declared before a required one (lines). A snapshot that sends contributionBasis: null and omits lines still gets a message naming contributionBasis. The same applies to every report input's nested ReportAsOf and OutputSpecifier, and to the field-update records.

**Action:** Either narrow the statement ('names the missing field when it is a field of the top-level payload record'), or extend missingFieldMessage to walk into the nested record the library names.

**Why:** Dan's mental model says the fix is complete. An operator sending a nested snapshot payload will still be pointed at the wrong field, which is the exact failure the change was meant to remove.

---

