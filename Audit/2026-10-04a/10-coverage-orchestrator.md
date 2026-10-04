# code-outward-coverage-CrossDomainOrchestration

## CDO-COV-POS-1 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:511-534 (changeHoldingBasisMethod); REQ-POS-11.5, REQ-POS-11.9
- **Summary:** Changing the basis method for an account and Security that both exist but have no Holding together fails with PositionsHoldingDoesntExist. No REQ describes this error and no test triggers it.
- **Resolution:** fix-spec

changeHoldingBasisMethod looks up the account and the Security by name, which REQ-POS-11.9 covers. It then fetches the Holding for that pair, and when there is none it returns PositionsHoldingDoesntExist(accountName, securityName) (lines 525-531). REQ-POS-11.9 lists only 'A Dimension Value, Security, Investment Account or Property name' that does not match a record. It does not mention Holdings, which have no name and are addressed by the (account, Security) pair. REQ-POS-11.5 describes the change operation but not what happens when the pair has no Holding. A grep of Tests/ finds no reference to PositionsHoldingDoesntExist; Checks/check-apperror-coverage also lists it as untested. The HoldingMaintenance tests at :108 and :119 and the PositionsRoutes test at :542 all change an existing Holding.

**Action:** Add the unmatched-Holding case to the spec, either by extending REQ-POS-11.9 or with a clause in REQ-POS-11.5 that says a basis-method change for an account and Security with no Holding fails with a typed error naming both. Then add a test that matches PositionsHoldingDoesntExist and checks both names.

**Why:** This is a reachable, operator-facing failure: the operator gives two valid names that do not form a Holding. Nothing in the spec says it must fail with a typed error naming both, and no test would catch the branch being removed or turned into a generic DAL row-count error.

---

## CDO-COV-POS-2 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:34-46, 61-71 (renameDimensionValue / confirmDimensionValueNameFree); REQ-SYS-6.1
- **Summary:** Renaming a Dimension Value to the name it already has is rejected with PositionsDimensionValueAlreadyExists, which names the value itself. REQ-SYS-6.1 says a field set to its current value counts as named and the update succeeds.
- **Resolution:** fix-code

renameDimensionValue calls confirmDimensionValueNameFree for the new name. Unlike every other name-uniqueness guard in this slice, that check takes no 'self' argument. confirmPersonNameFree, confirmSecurityNameFree, confirmTickerFree, confirmInvestmentAccountNameFree and confirmPropertyNameFree each skip the record being updated. So renaming 'Large Cap' to 'Large Cap' finds itself and fails with PositionsDimensionValueAlreadyExists(dimension, name). REQ-SYS-6.1, amended 2026-10-03, says: 'For an update, "would change nothing" means the request names no field to change. A field set to the value it already holds counts as named, and the update succeeds. This is the system default for every update operation; an entity spec need not restate it.' REQ-POS-11.1 provides the rename and states no exception. No test covers a rename to the current name; SecurityMaintenance.fs:98/111/130 and PositionsRoutes.fs:252 all rename to a different name.

**Action:** Give confirmDimensionValueNameFree a self-exclusion, as the sibling guards have, so a rename to the current name succeeds. Add a REQ-POS-11.1 REQ-SYS-6.1 test showing that a rename to the stored name succeeds and leaves the value as stored.

**Why:** The code (lowest authority) contradicts a system-wide REQ that was amended to settle exactly this case. The rename guard is the only one of its kind in the slice that does not skip the record being updated.

---

## CDO-COV-POS-3 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/PositionsLedgerLinks.fs:120-121, 125, 177 (self-exclusion arms of confirmNotLinkedElsewhere / confirmMortgageLink); REQ-POS-4.9, REQ-POS-9.8, REQ-SYS-6.1
- **Summary:** No test re-saves a link the record already holds. The three self-exclusion arms that let an update keep its own ledger link (Investment Account link, Property asset link, a retained mortgage account) are never run.
- **Resolution:** fix-test

confirmNotLinkedElsewhere accepts an account already linked to the record being updated (line 120: Some linked, _, LinkingInvestmentAccount self when ... = self; line 125: the Property equivalent). confirmMortgageLink accepts a mortgage account already linked to the same Property (line 177: rejects only when the id <> self). These arms run only on update, when the request re-sends a link the record already has. Every update test sets a different link. InvestmentAccountMaintenance.fs:202 moves jordanCustodial to F-1250, and :191 targets another account's link to get the rejection. PropertyMaintenance.fs:296 sets a new T-1591 asset account, and :323 replaces the residence's F-2310 mortgage with F-2210, keeping no member. PositionsRoutes.fs:873 goes from [F-2210] to [F-2230; F-2220], also keeping none. On create, self is a freshly generated id, so these arms cannot match. If any self-exclusion were removed, 'keep my link' updates would fail with PositionsLedgerAccountAlreadyLinked or PositionsMortgageAccountAlreadyLinked naming the record itself, and every current test would still pass. REQ-SYS-6.1's amendment requires such an update to succeed, and REQ-POS-11.6's 'mortgage Accounts (given as the complete new set)' means any mortgage-set edit that keeps one account goes through this arm.

**Action:** Add tests that (a) update an Investment Account, re-sending its current ledger link along with another change, and the update succeeds; (b) do the same for a Property's asset account; (c) update a Property's mortgage set to its existing account plus a new one, and both are linked afterwards.

**Why:** The self-exclusion is what makes an ordinary edit possible, such as adding a second mortgage while keeping the first. With no test on it, removing it during a refactor would break routine maintenance and nothing would go red.

---

## CDO-COV-POS-4 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:177, 379; Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs:180; REQ-POS-3.3, REQ-POS-4.1, REQ-POS-9.1
- **Summary:** Three uniqueness checks on the update path have no test: renaming an Investment Account to another's name, renaming a Property to another's name, and changing a Security's ticker to another's ticker.
- **Resolution:** fix-test

Each update calls the name-free or ticker-free guard with Some self: updateInvestmentAccount (InvestmentOrchestration.fs:379, confirmInvestmentAccountNameFree), updateProperty (RealEstateOrchestration.fs:180, confirmPropertyNameFree) and updateSecurity (InvestmentOrchestration.fs:177, confirmTickerFree). The only uniqueness tests are create tests: InvestmentAccountMaintenance.fs:101 (REQ-POS-4.1 create), PropertyMaintenance.fs:121 (REQ-POS-9.1 create) and SecurityMaintenance.fs:179 (REQ-POS-3.3 create). Their siblings do have update-collision tests: Security rename (SecurityMaintenance.fs:246) and Person rename (PersonMaintenance.fs:111). These three update calls are separate call sites with a different self argument. If one were deleted or passed the wrong self, the DB unique constraint would reject the write with a generic DAL error, not the typed error REQ-POS-4.1/9.1/3.3 require, and no test would notice.

**Action:** Add three tests: renaming an Investment Account to another's name, renaming a Property to another's name, and setting a Security's ticker to another Security's ticker. Each is rejected with the typed already-exists error naming the value, and neither record changes. Model them on SecurityMaintenance.fs:246.

**Why:** The question is whether this code path has any test, not whether its REQ does. Each REQ is covered on create only, and the update guards are separate code.

---

## CDO-COV-POS-5 — test-gap
- **Location:** Src/Business.CrossDomainOrchestration/NetWorth.fs:99-120; REQ-RPT-8.2, REQ-RPT-8.5 (amended 2026-10-04 in da96f73)
- **Summary:** Net worth's handling of a Property that is no longer owned but still has ledger links has no fixture data. HEAD amended REQ-RPT-8.5 to list such a Property's mortgage with the other liabilities, and no test covers that.
- **Resolution:** fix-test

computeNetWorth leaves out of liabilities only the mortgage accounts of Properties owned on the date (ownedMortgageIds, line 107, applied at 118). A disposed Property's mortgage therefore falls into liabilityAccounts. HEAD commit da96f73 changed only Reporting.md, amending REQ-RPT-8.5 to require this listing. Separately, linkedAssetIds (99-102) takes every Property's asset account whether or not it is owned, so a disposed Property's at-cost asset account also drops out of ledger assets. The fixture's only disposed Property is '7 Former Example Road' (PositionsFixture.fs:318-320), created with assetAccountId = None and mortgageIds = []. So no net worth test reaches either branch with data. The liability rows asserted at NetWorth.fs:159-178 contain only accounts with no Property link, and :131 asserts that the owned Properties' F-2310 and F-2320 are absent. If line 107 were changed to collect every Property's mortgages, the amended clause would be broken and the suite would stay green.

**Action:** Give the disposed fixture Property a mortgage account with a non-zero balance, and an asset account if wanted. Then add a REQ-RPT-8.5 REQ-RPT-8.2 test: as of a date after the disposal, that mortgage appears among liabilityAccounts with its balance and is subtracted from net worth once, and the Property is absent from properties. Optionally also assert that the disposed Property's linked asset account is not among the counted assets.

**Why:** HEAD's last commit changed behaviour that money totals depend on and added no test that can tell the amended rule from the old one.

---

## CDO-COV-POS-6 — missing-requirement
- **Location:** Src/Business.FinancialServices.Positions/Property.fs:74, 303 (List.distinct on mortgageAccountIds), reached from Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs:124-150, 211-214; REQ-POS-9.3, REQ-POS-9.8
- **Summary:** A Property create or update that lists the same mortgage account twice succeeds, and the duplicate is silently collapsed. Repeated owners in the same request are rejected under REQ-POS-9.3. No REQ covers repeated mortgage accounts, and no test does either.
- **Resolution:** dan-decides

RealEstateOrchestration.resolveOwners rejects a repeated owner with PositionsPropertyOwnerRepeated (line 79), which REQ-POS-9.3 requires ('The same Person may not appear twice among one Property's owners'). The mortgage list gets no equivalent check. confirmMortgageLinks validates each id separately, and both copies pass because neither is linked to another Property. Property.create (Property.fs:74) and the mortgage-set replace (Property.fs:303) then apply List.distinct, so [F-2310; F-2310] is stored as [F-2310] and the call returns Ok. REQ-POS-9.8 says only 'zero or more ledger mortgage Accounts' and says nothing about a repeat within one request. That is observable behaviour no spec describes, and it treats a repeat differently from the parallel owner rule. No test passes a repeated mortgage account.

**Action:** Dan decides whether a repeated mortgage account in one request is rejected, as owners are, or deliberately collapsed. Then add the matching clause to REQ-POS-9.8 and a test.

**Why:** Code-inward, this is unspecced behaviour: the system quietly changes caller input in one list while rejecting the same mistake in the list beside it. Either answer is defensible, but the spec should say which one applies.

---

## CDO-COV-POS-7 — contradiction
- **Location:** Src/Business.CrossDomainOrchestration/AccountSnapshotOrchestration.fs:208-212 (recordSnapshots); REQ-POS-7.1, REQ-SYS-6.1
- **Summary:** Recording an empty list of snapshots succeeds, writes nothing and returns []. REQ-POS-7.1 describes recording 'one or more' snapshots, and REQ-SYS-6.1 forbids an operation that silently succeeds while changing nothing.
- **Resolution:** dan-decides

recordSnapshots runs confirmNoRepeatedSnapshot and maps recordOne over the inputs. With inputs = [] both succeed, and the function returns Ok []. The route (PositionsRoutes.fs:184-199) passes input.snapshots through unchanged, so the AccountSnapshot Record payload {"snapshots": []} commits an empty transaction and returns an empty array. REQ-POS-7.1: 'The system must provide a means to record one or more Account Snapshots in a single atomic operation.' REQ-SYS-6.1: 'No state-transition operation may silently succeed as a no-op ... the operation must produce an error rather than update or insert nothing.' REQ-POS-7.2's idempotency exception covers re-recording an existing date, not an empty request. Other list-taking operations in the codebase reject empty input with typed errors (for example IngestionSourceFileIsEmpty and the *IdListCannotBeEmpty cases). No test sends an empty snapshot list.

**Action:** Dan confirms whether an empty Record request is a no-op under REQ-SYS-6.1. If so, recordSnapshots rejects an empty input list with a typed error, and a REQ-POS-7.1 REQ-SYS-6.1 test asserts that error.

**Why:** An operator script that builds the snapshot list from a parsed institution download and gets nothing (a parser bug, an empty file) would see success. That is the hidden upstream problem REQ-SYS-6.1 exists to surface.

---

## CDO-COV-CF-1 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/AgreementOrchestration.fs:53-80, 429 (confirmPaymentAgreementBelongsToAgreement via updateAgreement); REQ-CF-14.7, REQ-CF-14.8
- **Summary:** An UpdateAgreement payload can name a Payment Agreement of a different Master Agreement, and the code rejects it with CashflowPaymentAgreementNotUnderMasterAgreement. No REQ describes this rejection and no test triggers it.
- **Resolution:** fix-spec
- **Prior ruling:** Audit 2026-10-03a disposition #020 (CDO-COV-2), recorded in Audit/2026-10-03a/99-disposition.md, not in resolved-findings.md: it raised updateAgreement's Payment Agreement updates, including 'its cohesion check that a leg belongs to the agreement being updated has no test'. The ruling allowed the updates and led to REQ-CF-14.8, but said nothing about the cohesion check, which is still unspecced and untested. This raises the part of #020 the ruling left open; it is not a re-flag of a ruled point.

After disposition #020, REQ-CF-14.8 lets a Payment Agreement be updated, and the route addresses it by name. CashFlowFieldConverters.fs:554-560 resolves paymentAgreementName to an id across all agreements, because names are unique system-wide under REQ-CF-3.9. updateAgreement then runs confirmAuthorityAndCohesion (line 429), which rejects a leg that does not belong to the Master Agreement being updated (lines 60-68). So an operator who updates agreement A and names one of agreement B's legs reaches that branch. REQ-CF-14.7 covers names that match nothing, and REQ-CF-14.8 covers the update fields. Neither says that a leg of another Master Agreement is rejected. A grep of Tests/ finds no reference to CashflowPaymentAgreementNotUnderMasterAgreement; check-apperror-coverage also lists it as untested.

**Action:** Add a clause to REQ-CF-14.8: a Payment Agreement named in a Master Agreement's update must belong to that Master Agreement, otherwise the update fails with a typed error naming both. Add a route test that updates agreement A while naming a leg of agreement B, matches CashflowPaymentAgreementNotUnderMasterAgreement, and shows that neither agreement changed.

**Why:** The check guards against editing one agreement's leg while believing it belongs to another, and it is now reachable from the route. It is neither specced nor tested.

---

## CDO-COV-CF-2 — statement-delta
- **Location:** Src/Business.CrossDomainOrchestration/InstanceOrchestration.fs:537-551 (preConstructInvoiceComposite, 'belongs to another Invoice' branch); Audit/2026-10-03a/99-disposition.md #021
- **Summary:** Dan says everything dispositioned last audit has been fixed. Disposition #021 told test-agent to add a test for the 'Payment belongs to another Invoice' branch; no such test exists, and the branch cannot be reached from any production caller.
- **Resolution:** fix-test
- **Prior ruling:** Audit 2026-10-03a disposition #021 (accepted) told test-agent to add this test. This finding reports that the accepted action was not carried out; it does not dispute the ruling.

Disposition #021 (accepted, 2026-10-03): 'Test-agent separately adds the untested "Payment belongs to another Invoice" branch test (InstanceOrchestration.fs:604-605).' After later edits that branch is at InstanceOrchestration.fs:537-551. It returns CashflowPaymentNotUnderInvoice when a Payment id in paymentUpdates or paymentIdsToDelete exists but belongs to a different Invoice. A grep of Tests/ finds no reference to CashflowPaymentNotUnderInvoice, and check-apperror-coverage lists it as untested. The branch also cannot be reached in production. CashFlowOps.deletePaymentAndItsLinkage (CashFlowOps.fs:690-705) takes the Invoice from the Payment itself, so the Payment always belongs to it. transitionPaymentsToPosted (CashFlowOps.fs:873-883) builds paymentUpdates from each Invoice's own Payments. The route converters (CashFlowFieldConverters.fs:492, 510-511) always pass empty paymentUpdates and paymentIdsToDelete. So no route can trigger the branch, and the test #021 called for was never written.

**Action:** Either add the #021 test, calling updateInstanceComposite directly with a Payment id from another Invoice and matching CashflowPaymentNotUnderInvoice, or, since no production caller can reach the branch, ask Dan to replace #021's action with deleting the branch. Record whichever he chooses in the disposition.

**Why:** Dan's mental model is that last audit's dispositions are done. This one is not: the dispositioned branch is still untested, and it also turns out to be unreachable.

---


