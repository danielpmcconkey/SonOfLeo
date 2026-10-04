# test-efficacy-NGUI

## EFF-NGUI-1.4 — test-gap
- **Location:** REQ-NGUI-1.4; Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs:441-452, 845-856, 859-870; Specs/Behavioral/NonGraphicalInterface.md Waived table (waiver row removed in da96f73)
- **Summary:** The three tests that cite REQ-NGUI-1.4 test REQ-NGUI-1.5's behaviour (an unmatched code fails), not 1.4's. 1.4 is now active and not waived, but nothing tests what it actually requires.
- **Resolution:** dan-decides

REQ-NGUI-1.4 makes two promises: (a) every interface capability lets the actor refer to accounts by code, so no UUID is ever forced on them; (b) every return payload that identifies an account includes its code. HEAD commit da96f73 removed 1.4's waiver ('You can't test a negative and it's also quite clear by the interface contracts that codes are present', Dan 2026-07-06). Its message says the waiver goes 'now that tests cite it'. Three tests cite it:
- PositionsRoutes.fs:441 `REQ-POS-4.8 REQ-NGUI-1.4 an InvestmentAccount Create payload whose ledger account code matches no account fails...`. Assertion: `createAccount { ... ledgerAccountCode = Some "F-9919" } |> expectError codeNotFound (fun code -> Assert.Equal("F-9919", code))` then `Assert.Empty(stored)`.
- PositionsRoutes.fs:845 (REQ-POS-9.7, asset code "F-1599") and :859 (REQ-POS-9.8, mortgage code "F-2399") assert the same thing: `expectError codeNotFound`, where `codeNotFound = function AsError (AccountCodeDoesntMatchAccountId code) -> Some code`.
All three check that an unknown code is rejected with a typed error. That is word for word REQ-NGUI-1.5 ('references an Account entity by code and that code does not correspond to an existing Account entity, the operation must fail with an error'), together with the POS existence clauses. None of them checks clause (a): only an InvestmentAccount Create and a Property Create are touched, and a route somewhere else that takes an account UUID would leave all three green. None checks clause (b): no return payload is inspected, because each test is a sad path. Smell test: if some other route's input contract demanded an account Guid, or some return contract identified an account by Guid alone, none of these tests could fail. A grep of Src/Ui.InterfaceBridge/InterfaceContracts finds no account-id field today. So the system is compliant by construction, which is exactly the old waiver's reasoning. The traceability audit now reads 1.4 as tested when it is not. The same three tests are the only Positions route tests for unknown codes, and they do not cite REQ-NGUI-1.5, the requirement they actually prove.

**Action:** Dan picks one: (1) restore REQ-NGUI-1.4's waiver row and re-cite the three PositionsRoutes tests from REQ-NGUI-1.4 to REQ-NGUI-1.5; or (2) keep 1.4 tested, re-cite those three tests to REQ-NGUI-1.5, and add a real 1.4 test. For example, a reflection test over Ui.InterfaceBridge.InterfaceContracts like the one in AccountNamesInPayloads.fs, asserting that no Input contract has an account-identifying Guid field and that every return contract which references an account has a code field.

**Why:** A citation is a claim of coverage. Here the claim is what removed a waiver. A test proves the requirement whose behaviour its assertions would catch breaking, not the one in its name. These assertions would catch a broken unknown-code rejection (1.5) and nothing that 1.4 forbids, so 1.4 now counts as tested while no test checks it.

---

## EFF-NGUI-3.8-4.2 — test-gap
- **Location:** REQ-NGUI-3.8, REQ-NGUI-4.2; Tests/Tests.Integrated/SonOfLeoCli/Program.fs:84-91 (assertion line 91), 94-101 (assertion line 101); Tests/Tests.Integrated/Reports/Program.fs:102-109 (assertion line 109)
- **Summary:** The case-sensitivity tests for the domain, the verb and the report name have one assertion each: `(exitCode = 1) |> Assert.True`. They never check that the failure was an unknown-route rejection (Specimen 5).
- **Resolution:** fix-test

All three tests run the real CLI process with a lowercased argument (`[ "account"; "FetchAll" ]`, `[ "Account"; "fetchAll" ]`, `[ "trialbalance" ]`) and a valid payload. Each discards stdout and stderr (`let exitCode, _, _ = runCli ...`) and asserts only `(exitCode = 1) |> Assert.True`. Exit code 1 is what Startup.run returns for any typed failure, so these tests also pass when the lowercased route was matched case-insensitively and its handler then failed for another reason (database unavailable, a payload/contract change, a handler regression). They do not show that the argument was rejected as an unknown command or report name, which is what case sensitivity means here. The neighbouring process tests already show the stronger form costs one line: the REQ-NGUI-3.9 test (SonOfLeoCli/Program.fs:111-113) asserts `Assert.Equal("Unknown command: Ropa Interior", e.Trim())`, and the REQ-NGUI-4.5 test (Reports/Program.fs:119-121) asserts `Assert.Equal("Unknown report: RopaInterior.", e.Trim())`. The success-path siblings (Program.fs:32, Reports/Program.fs:42) send the same payloads with correct casing, which narrows the false-pass window but does not close it, and they cite different REQs. They also use `Assert.True(x = 1)` where Tests/README.md prefers equality ('Asserting equality is preferred to asserting truth').

**Action:** In each of the three tests, keep stderr and assert it equals the unknown-route message for the lowercased input, as the 3.9 and 4.5 tests do: `Assert.Equal("Unknown command: account FetchAll", e.Trim())`, `Assert.Equal("Unknown command: Account fetchAll", e.Trim())`, `Assert.Equal("Unknown report: trialbalance.", e.Trim())`. Replace `(exitCode = 1) |> Assert.True` with `Assert.Equal(1, exitCode)`.

**Why:** An exit code says only 'something failed'. A case-sensitivity test exists to prove that one particular rejection happened, the router refusing a differently cased name. Without the message check, any failure that happens to exit 1 counts as proof, and Specimen 5 names exactly that.

---

## EFF-NGUI-4.4-STDOUT — test-gap
- **Location:** REQ-NGUI-4.4; Tests/Tests.Integrated/Reports/Program.fs:87-99 (assertion line 97)
- **Summary:** The Reports CLI's stdout success test asserts only that stdout deserialises to the DataOnly case (`Assert.True(fetched.IsDataOnly)`). It never looks at the payload's contents (Specimen 3).
- **Resolution:** fix-test

REQ-NGUI-4.4: 'Upon successful execution, the Reports CLI returns the payload via stdout and exits with code 0.' The citing test sends a DataOnly TrialBalance request, asserts `Assert.Equal(0, exitCode)`, deserialises stdout as `TrialBalanceReportReturn`, and then asserts only `Assert.True(fetched.IsDataOnly)`. The case it checks is the one the request asked for, so a Reports CLI that printed `DataOnly []`, or rows for the wrong as-of date or a stale or partial trial balance, would pass. Smell test: garbage of the right shape passes. The main CLI's matching test for REQ-NGUI-3.6 (SonOfLeoCli/Program.fs:55-81) sets the standard: it derives the expected name from fixture.Data.accounts and asserts `Assert.Equal(expectedName, fetched.name)` and `Assert.Equal(targetCode, fetched.code)`. The only other Reports-CLI stdout check, FileArgumentPosition.fs:134, asserts `Assert.NotEmpty(rows)`, which is a count-style guard. So no test confirms that the payload the Reports process writes to stdout is the report the route produced.

**Action:** Inside the DataOnly arm, compare the rows to an independently derived expectation. Either compare them, as (accountCode, accountName, debit, credit) tuples, to the rows returned for the same input by `routeReportingCommandForTesting "TrialBalance"` or by fetchTrialBalanceData (both independently tested, so allowed under ROUTE-ORACLE), or assert at least one fixture account's code, name and totals derived from fixture.Data journal lines. Replace `Assert.True(fetched.IsDataOnly)` with a match whose Report arm fails.

**Why:** REQ-NGUI-4.4 is about delivering the payload, and a test of payload delivery has to look at the payload. Checking only the DU tag proves the process printed something of the right type, not that it printed the report, so serialisation or stdout bugs that drop or garble rows go undetected.

---

