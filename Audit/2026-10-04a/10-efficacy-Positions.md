# test-efficacy-positions

## EFF-POS-1 — test-gap
- **Location:** Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs:432, :821, :836 (REQ-POS-4.7, REQ-POS-9.4, REQ-POS-9.5)
- **Summary:** Three route tests check a missing required field by matching the generic JsonDeserializationFailed case and then running Assert.Contains on its free-text message. That is Specimen 4: the error is identified by string matching, not by a typed case.
- **Resolution:** fix-test

The tests are `REQ-POS-4.7 an InvestmentAccount Create payload with no active begin...` (line 426), `REQ-POS-9.4 a Property Create payload with no acquisition date...` (815) and `REQ-POS-9.5 a Property Create payload with no purchase basis...` (830). Each one asserts `|> expectError missingField (fun message -> Assert.Contains("activeBegin", message))`, with "acquisitionDate" or "purchaseBasis" in the other two. `missingField` (line 55) matches `JsonDeserializationFailed (_, message, _)`. That one case covers every deserialization failure: App.Utility/UtilityError.fs:12 declares it as `string * string * string`, and Json.fs:44 raises it for every exception. The only thing that tells 'field left out' apart from any other failure is a substring search of the message. A payload where the field is present but malformed (for example an unparseable date) also fails with JsonDeserializationFailed. The library's conversion message for that case typically includes the JSON path ($.activeBegin), so the test would pass even though the field was not missing. Each test name says the error is 'typed ... naming the missing active begin', but neither the type nor the 'missing' part is checked. Json.missingFieldMessage (Json.fs:21-38) produces an exact, predictable text: `Missing field for record type <FullName>: <field>`. An exact equality is therefore possible, and it would also pin down the fix Dan describes for fromJson naming the field the payload left out. The precedent NGUI-AQ-1 does not cover this: it concerns CLI stderr Contains, where the full expected message has to be present.

**Action:** In each of the three tests, replace Assert.Contains(field, message) with Assert.Equal($"Missing field for record type {typeof<C.InvestmentAccountCreateInput>.FullName}: activeBegin", message), using the matching input type and field for the Property tests.

**Why:** A substring check on a generic error's text cannot tell the behavior under test from a neighbouring failure. The test passes as long as the field name appears anywhere in the message, whether the field was missing, malformed or the wrong type. If you cannot name the error exactly, the test does not know what the code did.

---

## EFF-POS-2 — test-gap
- **Location:** REQ-POS-4.1, REQ-POS-9.1, REQ-POS-3.3; Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:379, :177; Src/Business.CrossDomainOrchestration/RealEstateOrchestration.fs:180
- **Summary:** The uniqueness rules for Investment Account name, Property name and Security ticker are tested only on create. No test renames into an existing name or sets a ticker another Security already has, although the update path runs its own self-exempting check.
- **Resolution:** fix-test

Tests for REQ-POS-4.1 (InvestmentAccountMaintenance.fs:101) and REQ-POS-9.1 (PropertyMaintenance.fs:121) only call createInvestmentAccount / createProperty with a duplicate name. The REQ-POS-3.3 ticker-collision test (SecurityMaintenance.fs:179) only calls createSecurity. On update the code takes a different branch: `confirmInvestmentAccountNameFree context newName (Some self)` (InvestmentOrchestration.fs:379), `confirmPropertyNameFree context newName (Some self)` (RealEstateOrchestration.fs:180) and `confirmTickerFree context newTicker self` (InvestmentOrchestration.fs:177). Each of these passes the record's own id so a no-op rename is not rejected, and that `<> self` comparison is exercised only on create, where self is None or a fresh id. Two sibling REQs in the same spec do have update-path negative tests: Security name (SecurityMaintenance.fs:246, `REQ-POS-11.2 REQ-POS-3.2 renaming a Security to another Security's name is rejected`) and Dimension Value name (SecurityMaintenance.fs:111). The three listed here do not. If the self-exemption were wrong (for example comparing ids the wrong way round, or skipping the check when self is Some), the rename would reach the UNIQUE constraints in 202610041020-CreatePositionsTables.sql (investment_account_account_name_key, security_ticker_key and the property name key). The operator would then get a raw data-access error instead of the typed AlreadyExists error, the same failure Specimen 4 documents for ingestion. No current test would go red.

**Action:** Add three update-path sad-path tests, each matching the typed case: rename an Investment Account to another fixture account's name (expect PositionsInvestmentAccountNameAlreadyExists), rename a Property to another fixture Property's name (expect PositionsPropertyNameAlreadyExists), and update a Security's ticker to another fixture Security's ticker (expect PositionsTickerAlreadyExists).

**Why:** A uniqueness rule is enforced at every write that can break it. When create and update run different checks, a test of one says nothing about the other. The suite already applies this to Security names and Dimension Value names, which shows the gap is an omission and not a policy.

---

## EFF-POS-3 — missing-requirement
- **Location:** Src/Business.CrossDomainOrchestration/InvestmentOrchestration.fs:511-534 (changeHoldingBasisMethod); REQ-POS-11.5
- **Summary:** Changing the basis method for an account and Security that both exist but have no Holding fails with PositionsHoldingDoesntExist. No requirement states this behavior and no test exercises it.
- **Resolution:** dan-decides

changeHoldingBasisMethod resolves the account and the Security by name, which REQ-POS-11.9 covers and the tests exercise. It then looks up the Holding and, when there is none, returns `PositionsHoldingDoesntExist(accountName, securityName)` (InvestmentOrchestration.fs:530). A grep of Tests/ finds no reference to PositionsHoldingDoesntExist. REQ-POS-11.5 says only 'a means ... to change a Holding's basis method subject to REQ-POS-5.2'. REQ-POS-11.9 covers names that match no record, but a Holding is not addressed by its own name, so a Holding that does not exist is not an 11.9 case. Other operations in the same spec spell out their not-found rule: REQ-POS-7.4 for deleting a snapshot that does not exist, and Valuation delete is tested under REQ-POS-11.8 / REQ-SYS-6.1 (PropertyMaintenance.fs:472). The operator can easily hit this case through the CLI route Holding UpdateBasisMethod (Ui.InterfaceBridge/Routes/PositionsRoutes.fs:154/373), for example by naming a fund the account does not hold.

**Action:** Add a clause to REQ-POS-11.5 saying that changing the basis method for an Investment Account and Security with no Holding fails with a typed not-found error naming the account and the Security. Then add a HoldingMaintenance test, such as Jordan Custodial plus the total market fund, that matches PositionsHoldingDoesntExist and its payload.

**Why:** The code makes a promise the spec does not record and no test protects. If the behavior changes, nothing fails; and an operator who mistypes which account holds a fund depends on this error being clear.

---

## EFF-POS-4 — test-gap
- **Location:** Tests/Tests.Integrated/CrossDomainOrchestration/InvestmentAccountMaintenance.fs:348-356 (REQ-POS-5.3)
- **Summary:** The test that a TaxDeferred-to-Roth change succeeds checks the Holdings with a hard-wired count, `Assert.Equal(2, holdings.Length)`. It never looks at the Holdings' securities or basis methods (Specimens 1 and 3).
- **Resolution:** fix-test

Test `REQ-POS-5.3 changing the tax treatment of a TaxDeferred account with Holdings to Roth succeeds, since its Holdings carry no basis method under either`. It asserts `Assert.Equal(TaxTreatment.Roth, updated |> InvestmentAccount.taxTreatment)`, which is the function's own return value and is not re-read, and then `Assert.Equal(2, holdings.Length)`. The 2 is copied from the fixture (Sam 401k holds the bond fund and the total market fund) and is not derived from it. The count also never checks what the test name claims: that the Holdings still carry no basis method after the change. If the update gave a basis method to the Holdings, or swapped one Holding for another, the count would still be 2 and the test would pass. In other tests in the same suite the Holdings assertion is a full value comparison (HoldingMaintenance.fs:160, `[ PF.sam401k, PF.bondFund, None; PF.sam401k, PF.totalMarket, None ]`).

**Action:** Replace `Assert.Equal(2, holdings.Length)` with an equality on (Security name, basis method) pairs, matching `[ PF.bondFund, None; PF.totalMarket, None ]`. Also re-read the account through listInvestmentAccounts so the stored tax treatment is asserted, not just the value the call returned.

**Why:** A count tells you something came back, not that the right thing came back. The property this test exists to protect is 'the Holdings are untouched and still carry no basis method', and nothing currently asserts it.

---


