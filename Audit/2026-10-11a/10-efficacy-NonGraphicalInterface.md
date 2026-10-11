# test-efficacy-NonGraphicalInterface

## EFF-NGUI-1.5-POS — test-gap
- **Location:** REQ-NGUI-1.5; Src/Ui.InterfaceBridge/Routes/PositionsRoutes.fs:323-343 (deletePreLedgerBalance, listPreLedgerBalances); Src/Ui.InterfaceBridge/BoundaryConverters/PositionsFieldConverters.fs:178-179 (ledgerAccountCodeUpdate), 387-391 and 422-430 (asset/mortgage AccountCodesUpdate); Tests/Tests.Integrated/InterfaceBridge/PositionsSliceTwoRoutes.fs:324-335, 352-369; Tests/Tests.Integrated/InterfaceBridge/PositionsRoutes.fs:452-461, 856-879, 884-903, 1058-1077
- **Summary:** Four Positions operations take an account code from the caller, and no test sends any of them a code that matches no account: PreLedgerBalance Delete, PreLedgerBalance List with an account filter, InvestmentAccount Update (ledgerAccountCodeUpdate) and Property Update (assetAccountCodesUpdate / mortgageAccountCodesUpdate). Two of the four are new in slice 2.
- **Resolution:** dan-decides

REQ-NGUI-1.5: 'When a UI-facing operation references an Account entity by code and that code does not correspond to an existing Account entity, the operation must fail with an error.' The rule covers every UI-facing operation. The suite tests it one operation at a time with a typed assertion: Account Create parent, FetchByParentCode, Deactivate, UpdateName and UpdateExternalReference (AccountRoutes.fs:85, 169, 238, 276, 314, via isCorrectError AccountCodeDoesntMatchAccountId); JE lines (JournalEntryRoutes.fs:159, 669); ingestion (IngestionRoutes.fs:337, 747); classification claimants (ClassificationRuleRoutes.fs:416, 455); reconciliation (ReportRoutes.fs:408); InvestmentAccount Create (PositionsRoutes.fs:452, `expectError codeNotFound (fun code -> Assert.Equal("F-9919", code))`); Property Create asset and mortgage (PositionsRoutes.fs:856, 870); PreLedgerBalance Record (PositionsSliceTwoRoutes.fs:324, 'F-1998').

No test of this kind exists for the following operations, which each resolve a caller-supplied code through fallibleConverterAccountCodeToAccountId:
- PreLedgerBalance Delete (PositionsRoutes.fs:327, `input.accountCode |> fallibleConverterAccountCodeToAccountId context`). Its only test is the happy path at PositionsSliceTwoRoutes.fs:352.
- PreLedgerBalance List with accountCode = Some _ (PositionsRoutes.fs:336-337). Its only test is the happy path at PositionsSliceTwoRoutes.fs:371.
- InvestmentAccount Update with ledgerAccountCodeUpdate = SetTo (Some code) (PositionsFieldConverters.fs:178-179). Every route test passes NoChange or a real code.
- Property Update with assetAccountCodesUpdate or mortgageAccountCodesUpdate = SetTo [unknown] (PositionsFieldConverters.fs:422-430). The tests at PositionsRoutes.fs:884 and 1058 send only codes that exist (F-2230/F-2220, T-1597/T-1598).

A grep of Tests/ for AccountCodeDoesntMatchAccountId and codeNotFound returns no hit for any of these four operations. Under Tests/README and failure-vector-is-a-user-interaction.md, each of these is its own failure vector: an operator who deletes or lists a pre-ledger balance, or re-links an account or property, with a mistyped code. The lowest layer at which that caller exists is the route. The converter has been tested on other routes, but that says nothing about whether these routes call it. They do today, so the system is compliant. But if one of them were changed to look the code up through a path that returns None or an empty list for an unknown code, so that List returns [] or Delete returns NotFound for some other reason, nothing would catch it. Dan's statement says error cases that only reject loudly are untested by design. These cases end in a typed rejection rather than a DB failure, and the Create and Record siblings of the same routes are tested, so it is unclear whether that carve-out was meant to cover them.

**Action:** Dan decides whether REQ-NGUI-1.5 needs a route-level unknown-code test for each operation that takes a code. If it does, add one Theory row per operation to PositionsSliceTwoRoutes.fs / PositionsRoutes.fs covering PreLedgerBalance Delete, PreLedgerBalance List (accountCode filter), InvestmentAccount Update (ledgerAccountCodeUpdate) and Property Update (asset and mortgage code updates). Each row should send a code that matches no account and assert `expectError codeNotFound (fun code -> Assert.Equal(<the code sent>, code))`. For the two update rows, it should also assert that the stored entity is unchanged.

**Why:** A test of a converter shows that the converter works. It does not show that a given route calls it. REQ-NGUI-1.5 is a rule about every operation that takes a code, so each such operation is its own way a user can be failed. The new slice-2 Delete and List routes, and the update paths, are the ones where nothing would catch a regression to silently returning an empty list or a not-found error that hides the mistyped code.

---

## UNCITED-NGUI-3.10-TRAILING — missing-requirement
- **Location:** REQ-NGUI-3.10; Src/Ui.OperatorCli/Program.fs:25-31; Src/Ui.ReportCli/Program.fs:13-17
- **Summary:** When `--file` comes straight after the verb (or report name) with no path after it, the CLI treats it as an ordinary additional argument and reads the payload from stdin. No requirement covers this, and no test exercises it.
- **Resolution:** dan-decides

Main CLI dispatch: `| domain :: verb :: "--file" :: filePath :: rest -> ... File.ReadAllText(filePath)` and then `| domain :: verb :: rest -> ... Console.In.ReadToEnd()`. The Reports CLI has the same shape (`| name :: "--file" :: filePath :: rest` and then `| name :: rest`). For `Account Create --file` with no path, the first pattern does not match, so the second binds rest = ["--file"] and the process blocks on stdin, or reads whatever is piped to it. REQ-NGUI-3.10 says the payload may be provided 'via a `--file` argument followed by a file path' and that `--file` 'is recognised only in the position immediately after the verb'. Here `--file` is in the recognised position but has no path after it. The spec does not say whether that should be an error, a usage message (as REQ-NGUI-3.11 gives for too few arguments), or a quiet fallback to stdin. Two reasonable implementers would plausibly differ: a forgotten path typed by an operator at a terminal currently leaves the CLI waiting for input with no message. The only --file test, FileArgumentPosition.fs:57-101, covers `--file <path>` straight after the verb and `extra --file <path>`. It does not cover a trailing `--file` with no path.

**Action:** Dan decides the behaviour of a `--file` with no path in the recognised position, for both CLIs. Either amend REQ-NGUI-3.10 to say it is rejected (for example with the usage message on stderr and a non-zero exit) and add a FileArgumentPosition row for it, or state that it falls back to stdin as an ordinary argument and test that.

**Why:** The CLI does something here that no requirement describes. If it is meant to fail, the current quiet fallback to stdin is a defect. If it is meant to fall back, that intent is not written down, and a later change could break it without anything noticing.

---


