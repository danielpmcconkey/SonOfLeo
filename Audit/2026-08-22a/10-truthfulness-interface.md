# interface-bridge-auditor

## CORR-IB-1 — contradiction
- **Location:** /media/dan/fdrive/codeprojects/SonOfLeo/Src/InterfaceBridge/BoundaryConverters/IngestionFieldConverters.fs, lines 232-233; REQ-NGUI-1.4
- **Summary:** ClassificationRuleReturn.codeAtMatch is populated with the account name instead of the account code.
- **Resolution:** fix-code

In the converter `convert [ClassificationRule] to [ClassificationRuleReturn]` (line 232-233), the `codeAtMatch` field is populated by calling `convert AccountId to AccountNameString`, which resolves the stored AccountId to the account's name. It should call `convert AccountId to AccountCodeString` to resolve to the account's code. The `accountNameAtMatch` field on line 237 also calls `convert AccountId to AccountNameString`, so both fields currently contain the account name. Every other converter in the codebase correctly pairs CodeString for code fields and NameString for name fields: AccountBalanceReturn (AccountFieldConverters.fs:179-180), JournalEntryLineReturn (JournalEntryFieldConverters.fs:51-52), PrioritizedMatchReturn (IngestionFieldConverters.fs:285-286). This field was likely broken during the code-to-ID migration (commit 378ce5e), when accountIdAtMatch replaced a code-based field and the output resolution was wired to the wrong lookup.

**Action:** Change line 233 from `convert AccountId to AccountNameString` to `convert AccountId to AccountCodeString`.

**Why:** REQ-NGUI-1.4 requires all return payloads to include account codes when identifying an account. The ClassificationRuleReturn contract names the field `codeAtMatch` and the corresponding input contract (NewClassificationRuleInput.codeAtMatch) accepts a code string. Any downstream consumer (Saturday routine, COYS bots) that reads this field expecting an account code receives the account name instead, which would fail if passed back as input to create or update operations.

---

