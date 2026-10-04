# NGUI spec-quality auditor

## NGUI-1.4-TRACE — test-gap
- **Location:** Specs/Behavioral/NonGraphicalInterface.md REQ-NGUI-1.4 (waiver row removed in da96f73); Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs lines 441, 845, 859
- **Summary:** Commit da96f73 removed REQ-NGUI-1.4's waiver because tests now cite it, but all three citing tests check REQ-NGUI-1.5's behaviour (an unknown code fails), not REQ-NGUI-1.4's.
- **Resolution:** dan-decides

REQ-NGUI-1.4 makes two promises. (a) Every interface capability lets the actor refer to accounts by code, so the actor never has to handle Account UUIDs. (b) Every return payload that identifies an account includes its account code. Dan approved a waiver on 2026-07-06: "You can't test a negative and it's also quite clear by the interface contracts that codes are present". Commit da96f73 deleted that waiver, with the commit message "REQ-NGUI-1.4's waiver goes now that tests cite it". Only three tests cite REQ-NGUI-1.4: `REQ-POS-4.8 REQ-NGUI-1.4 an InvestmentAccount Create payload whose ledger account code matches no account fails with a typed error naming the code...` (line 441), `REQ-POS-9.7 REQ-NGUI-1.4 a Property Create payload whose asset account code matches no account fails...` (line 845) and `REQ-POS-9.8 REQ-NGUI-1.4 a Property Create payload whose mortgage account code matches no account fails...` (line 859). Each one sends an account code that does not exist and asserts a code-not-found error. That is exactly REQ-NGUI-1.5: "When a UI-facing operation references an Account entity by code and that code does not correspond to an existing Account entity, the operation must fail with an error." None of the three cites REQ-NGUI-1.5. None of them asserts that a return payload includes an account code, and none says anything about UUIDs or about other domains. The traceability script reports REQ-NGUI-1.4 as tested because the ID appears in a test name. In substance the requirement is now neither waived nor tested, so the three-state rule holds only on paper. Dan's original reason (it is a negative, and the contracts show the codes are present) still applies unchanged.

**Action:** Dan decides one of two options. (1) Re-cite the three tests to REQ-NGUI-1.5 (keeping their REQ-POS IDs) and restore the REQ-NGUI-1.4 waiver row with its original reason and approval. (2) Keep REQ-NGUI-1.4 as tested and add tests that assert what it actually says, for example a Positions return payload carrying the linked ledger account's code.

**Why:** A test citation is a claim that the cited requirement is verified. When the citing test checks a sibling requirement, the traceability gate passes but proves nothing about the requirement it names. Removing a waiver Dan approved on the strength of such citations replaces an honest 'deliberately untested' with a false 'tested'.

---

## NGUI-JSON-DELTA — statement-delta
- **Location:** Src/App.Utility/Json.fs (missingFieldMessage, fromJson); Src/Ui.InterfaceBridge/InterfaceContracts/PositionsContracts.fs PropertyCreateInput (lines 184-193); REQ-NGUI-1.3.1
- **Summary:** Dan says fromJson "now names the field a payload actually left out", but the fix names the first declared record field missing from the payload, and that can be an optional field the payload left out legitimately.
- **Resolution:** fix-code

`missingFieldMessage` steps in only when the library has reported a missing field on the record being read. It then returns `FSharpType.GetRecordFields recordType |> Array.tryFind (fun f -> not (sent.Contains f.Name))`, which is the first declared field whose name is absent from the payload JSON. It does not skip option-typed fields. The code's own comment says the library treats an option field sent as null as "not set" and still accepts the payload, so an unset option field is allowed. That suggests an option field left out of the payload altogether is also allowed (I could not confirm this here: there is no dotnet in the container). If it is allowed, here is how the wrong field gets named. In `PropertyCreateInput`, the option field `disposalDate` is declared before the required `purchaseBasis`. A Property Create payload that leaves out both gets the message `Missing field for record type ...: disposalDate`, which names a field the caller may omit, not the field the caller actually has to supply. `InvestmentAccountCreateInput` has no option field before a required one, so it is unaffected, but any other contract with that ordering is exposed. The route tests (PositionsRoutes.fs lines 426-438, 814-842) remove one required field from a fully populated payload in which the option fields are present (set to null), so they never exercise this case. The fix therefore covers the null-option case the comment describes, but not every case in Dan's statement that the message names the field the payload actually left out.

**Action:** Change missingFieldMessage to skip option-typed (and other library-skippable) fields when it looks for the absent field. Add a route test that leaves out an option field and a later required field together, and asserts that the message names the required field.

**Why:** Under REQ-NGUI-1.3.1 the error message is the actor's whole payload on failure. A message that names a field the actor may legally omit sends the operator to fix the wrong thing, and that is the exact error the change was meant to remove.

---

