# code-outward-coverage-business-tier

## COV-POS-HOLD-1 — test-gap
- **Location:** Src/Business.FinancialServices.Positions/PositionsError.fs:21 (PositionsHoldingDoesntExist); Src/Business.FinancialServices.Positions/Holding.fs:125-160 (fetchByInvestmentAccountAndSecurity / updateBasisMethod); raised at Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:524-531; REQ-POS-11.5, REQ-POS-11.9
- **Summary:** Changing a Holding's basis method when the named account and Security both exist but the account has no Holding of that Security fails with PositionsHoldingDoesntExist. No REQ describes this failure and no test covers it.
- **Resolution:** fix-spec

The route reaches this path directly: PositionsRoutes.fs:158 calls InvestmentOrchestration.changeHoldingBasisMethod, which calls Holding.fetchByInvestmentAccountAndSecurity. When that returns None, it returns PositionsHoldingDoesntExist(accountName, securityName) (InvestmentOrchestration.fs:528-530). REQ-POS-11.5 says only that the system must provide 'a means ... to change a Holding's basis method subject to REQ-POS-5.2'. REQ-POS-11.9 gives a typed not-found error for a Dimension Value, Security, Investment Account or Property name, but does not list Holding, and a Holding has no name of its own. No requirement says what happens when the account/Security pair has no Holding. PositionsHoldingDoesntExist is one of the six PositionsError cases that appear nowhere in Tests/ (grep -rw over Tests finds 0 files). The basis-method tests (HoldingMaintenance.fs:108, 119; PositionsRoutes.fs:542) all change an existing fixture Holding. This is the only operator-reachable error branch in the Holding maintenance surface with neither a REQ nor a test.

**Action:** Amend REQ-POS-11.5 to say a basis-method change naming an Investment Account and Security with no Holding between them fails with a typed error naming both. Then add a HoldingMaintenance test that matches PositionsHoldingDoesntExist with both names, and asserts that no Holding was created.

**Why:** This is how the operator learns they named the wrong account or Security on a basis-method change. Nothing in the spec says what the error must name, or that the system must not create the Holding as a side effect (compare REQ-POS-6.5), and no test would notice if the branch changed.

---

## CONTRA-POS-DV-1 — contradiction
- **Location:** Src/Business.FinancialServices.Positions/DimensionValue.fs:119-139 (rename), reached via Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:61-71 (renameDimensionValue -> confirmDimensionValueNameFree, lines 33-45); REQ-POS-11.1, REQ-POS-2.3, REQ-SYS-6.1
- **Summary:** Renaming a Dimension Value to the name it already has is rejected with PositionsDimensionValueAlreadyExists. REQ-SYS-6.1 says a field set to the value it already holds counts as named and the update succeeds.
- **Resolution:** fix-code

renameDimensionValue fetches the value by its current name, then calls confirmDimensionValueNameFree context dimension newName. That function (lines 33-45) fails whenever fetchByDimensionAndName finds any row, and unlike the other name checks it takes no 'self' exclusion. So when newName = currentName, the value finds itself and the rename fails with PositionsDimensionValueAlreadyExists. REQ-SYS-6.1, second bullet (Amended 2026-10-03): 'A field set to the value it already holds counts as named, and the update succeeds. This is the system default for every update operation; an entity spec need not restate it.' REQ-POS-2.3 forbids two Dimension Values sharing a name in one dimension, and a rename to the current name creates no second value. Every other rename in the slice excludes the record being changed: PersonOrchestration.confirmNameFree (Some personId), confirmSecurityNameFree (self), confirmInvestmentAccountNameFree (Some self), and confirmPropertyNameFree (Some self). The Dimension Value rename is the only one that doesn't. No test renames a Dimension Value to its own name: SecurityMaintenance.fs:98 and 111 cover a new name and another value's name.

**Action:** Give confirmDimensionValueNameFree an exclusion for the Dimension Value being renamed, as the Person/Security/Investment Account/Property checks have. Add a REQ-POS-11.1 REQ-SYS-6.1 test showing that renaming a Dimension Value to its current name succeeds and leaves it unchanged.

**Why:** The code breaks a system-wide rule that a higher authority states explicitly. A caller re-sending a value the record already holds is not mistaken about the system's state, and the spec says such an update must succeed. Here it is told a duplicate exists.

---

## UNSPEC-POS-MORT-1 — missing-requirement
- **Location:** Src/Business.FinancialServices.Positions/Property.fs:74 (create: mortgageAccountIds |> List.distinct), Property.fs update (persistMortgageAccounts ... (newMortgageAccounts |> List.distinct)); REQ-POS-9.3, REQ-POS-9.8, REQ-POS-11.6
- **Summary:** When the same ledger account is given twice in one Property's mortgage-account set, it is silently collapsed to one. The same repeat among owners is rejected. No REQ describes this, and no test covers it.
- **Resolution:** dan-decides

Property.create stores mortgageAccountIds |> List.distinct, and Property.update re-inserts newMortgageAccounts |> List.distinct. RealEstateOrchestration.confirmMortgageLinks (lines 118-122) only checks each account, with confirmMortgageLink: Liability type, and not another Property's mortgage. It never checks for repeats within the request. So a CreateProperty or UpdateProperty payload with mortgageAccountCodes ['F-2210','F-2210'] succeeds and stores one link. For owners the spec is explicit: REQ-POS-9.3 says 'The same Person may not appear twice among one Property's owners', and that is enforced and tested (PropertyMaintenance.fs:163). REQ-POS-9.8 ('zero or more ledger mortgage Accounts ... A ledger Account may be a mortgage Account of at most one Property') says nothing about a repeat inside one set. No test sends a repeated mortgage code: PropertyMaintenance.fs:283 uses two distinct accounts.

**Action:** Dan decides: either add a REQ-POS-9.8 clause that rejects a repeated mortgage account within one Property, with a typed error naming the code (mirroring REQ-POS-9.3), or state that repeats are collapsed. Then add a test for the chosen behaviour.

**Why:** Two sets of the same shape in one operation handle a repeat in opposite ways, and only one way is specced. A silent collapse hides a malformed payload, for example a parser emitting a mortgage line twice, which the owner rule exists to surface.

---

## COV-CF-EXTID-1 — missing-requirement
- **Location:** Src/Business.FinancialServices.CashFlow/CashFlowComponent.fs:211-223 (ExternalInvoiceId.create); CashFlowError cases CashflowExternalInvoiceIdIsEmpty / CashflowExternalInvoiceIdTooLong; REQ-CF-5.x, REQ-CF-14.5, REQ-SYS-1.3
- **Summary:** An Invoice's external invoice ID must be at most 100 characters, a limit no REQ states. Neither of its rejection branches (whitespace-only, over 100 characters) has a test.
- **Resolution:** fix-spec

ExternalInvoiceId.create trims the input, rejects an empty/whitespace-only value with CashflowExternalInvoiceIdIsEmpty, and rejects anything over maxLength = 100 with CashflowExternalInvoiceIdTooLong. Operators reach it through CreateInvoice/UpdateInvoice (CashFlowFieldConverters.fs:397 converts externalInvoiceId; REQ-CF-14.5 lists 'update an Invoice's external invoice ID'). The whitespace rejection is covered by REQ-SYS-1.3, which applies to optional text fields. The 100-character limit appears in no requirement: CashFlow.md §5 (REQ-CF-5.1 to 5.19) gives the memo, blocker note and cancellation note lengths but not the external invoice ID's. On tests: TrimmedText.fs:33 checks trimming and that a value of exactly 100 characters is accepted, citing only REQ-SYS-1.1. Neither error case appears anywhere in Tests/ (grep -rw finds 0 files), so neither the whitespace-only rejection nor the 101-character rejection is exercised. Every sibling text type (InvoiceMemo REQ-CF-5.15, BlockerNote REQ-CF-5.14, AgreementMemo REQ-CF-2.22) has its limit in a CF REQ.

**Action:** Add a CF §5 data-state REQ: the external invoice ID may be null, and when non-null cannot be whitespace-only (post-trim) and cannot exceed 100 characters. Then add isolated tests: whitespace-only rejected with CashflowExternalInvoiceIdIsEmpty, and 101 characters rejected with CashflowExternalInvoiceIdTooLong.

**Why:** This rule changes what an operator can save on an Invoice, yet no spec states it and no test pins it. The limit could change, or the check could be removed, without any test failing.

---

## COV-STG-SRC-1 — test-gap
- **Location:** Src/Business.FinancialServices.DataIngestion/StageEntryComponent.fs:71-81 (SourceFile.create whitespace branch -> IngestionSourceFileIsEmpty); REQ-STG-2.6, REQ-SYS-1.2
- **Summary:** No test exercises REQ-STG-2.6's clause that source_file cannot be whitespace only. The SourceFile.create branch returning IngestionSourceFileIsEmpty has no test, although an operator can reach it through the manual update route.
- **Resolution:** fix-test

REQ-STG-2.6: 'Staged entry source_file cannot be null or whitespace only, and cannot exceed 150 characters.' The length clause is tested (RevisedRequirementsStaging.fs:250, a 150/151 theory on ingest), and TrimmedText.fs:51 tests trimming. IngestionSourceFileIsEmpty appears nowhere in Tests/ (grep -rw finds 0 files). Ingest builds the source file from the file path, so ingest cannot supply a blank value. The manual update path can: IngestionRoutes.fs:84 maps input.sourceFileUpdate through SourceFile.create, and REQ-STG-6.2 lets the operator set any field of the staged entry. So an UpdateStageEntry payload with sourceFileUpdate SetTo "   " reaches the untested branch.

**Action:** Add a REQ-STG-2.6 REQ-SYS-1.2 test. In an isolated SourceFile.create theory, or an UpdateStageEntry route test, show that an empty and a whitespace-only source file are each rejected with IngestionSourceFileIsEmpty and that the stored source file is unchanged.

**Why:** Spec-outward, REQ-STG-2.6 looks tested, but one of its two clauses has no test, and an operator-reachable rejection branch could regress silently.

---

## DEAD-POS-1 — maintainability
- **Location:** Src/Business.FinancialServices.Positions/Security.fs:37 (dimensionValueIn)
- **Summary:** Security.dimensionValueIn is a public function with no caller in Src and no reference in Tests.
- **Resolution:** fix-code
- **Prior ruling:** Not a re-raise. It applies the accepted 2026-10-03a #017 (COV-DEAD-1) dead-code disposition to a function added after that audit.

`let dimensionValueIn (dimension: Dimension) s = s.dimensionValues |> Map.tryFind dimension` is referenced only on its own definition line (grep -rnw dimensionValueIn over Src, Tests and DevDataStage finds only Security.fs:37). Security views are built through Security.dimensionValues in InvestmentOrchestration.viewWith. The function is new in the Positions slice. Precedent: audit 2026-10-03a row #017 (COV-DEAD-1) accepted deleting public business-tier functions with no Src caller and no test, such as RuleMatch.fetchById and PaymentAgreement.fetchByName.

**Action:** Delete Security.dimensionValueIn.

**Why:** A public function with no caller and no test is surface that nothing checks. Following the dead-code ruling at #017 keeps the business tier's public API limited to behaviour something actually uses.

---

