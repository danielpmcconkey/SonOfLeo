# interface-bridge-auditor

## NGUI-1.6-INGESTION — enforcement-gap
- **Location:** Src/InterfaceBridge/InterfaceContracts/IngestionContracts.fs (StageEntryLineReturn line 50, ClassificationRuleReturn line 108, PrioritizedMatchReturn line 85); REQ-NGUI-1.6
- **Summary:** Three ingestion-domain return types carry account codes without account names, violating REQ-NGUI-1.6.
- **Resolution:** fix-code

REQ-NGUI-1.6 states: "All interface return payloads that identify an account must include the account name alongside the account code." The waiver says this is "enforced by code review and periodic audit." Three return types in IngestionContracts.fs violate this:

1. StageEntryLineReturn (line 50-58): has `accountCode: string option` with no accountName field.
2. ClassificationRuleReturn (line 108-117): has `codeAtMatch: string` with no account name field.
3. PrioritizedMatchReturn (line 85-89): has `code: string` (which is AccountCode.value per the converter at IngestionFieldConverters.fs line 262) with no account name field.

All pre-ingestion return types comply: AccountReturn has code+name, JournalEntryLineReturn has accountCode+accountName, AccountActivityReturn has accountCode+accountName, AccountBalanceReturn has accountCode+accountName, TrialBalanceReturnRow has accountCode+accountName. The ingestion domain types were built after the NGUI spec and did not adopt this universal requirement.

Confirmed by grep: no `accountName` or `account_name` field exists anywhere in IngestionContracts.fs. The corresponding converters (IngestionFieldConverters.fs) do not look up or include account names when constructing these return types.

**Action:** Add account name fields alongside each account code field in the three return types (accountName: string option on StageEntryLineReturn, accountName: string on ClassificationRuleReturn and PrioritizedMatchReturn) and populate them via LookupCache.accountIdToName or the existing AccountFieldConverters in the boundary conversion functions.

**Why:** REQ-NGUI-1.6 exists so that an actor consuming CLI output can understand which account a code refers to without a separate lookup. Account codes like '5110' or '2300' are meaningless without their names. The waiver explicitly designates periodic audit as the enforcement mechanism for this requirement, making this audit the intended catch point.

---
