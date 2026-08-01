# Error Trace by Route

Every input contract + route handler, traced through to AppError cases from business
logic. System/infrastructure errors (Dal*, InterfaceBridgeFailedJson*, CliUnknownCommand)
are excluded unless they leak through as a backstop.

**Backstop markers:** Rows tagged ⚠️BACKSTOP surface `DalResultantRowsDidntMatchExpectation`
or `InterfaceBridgeConversionFailure` directly to the caller — a code gap where a
domain-specific error should exist but doesn't.

## Account

| domain | verb | handler | contract | element | error |
|---|---|---|---|---|---|
| Account | Create | accountCreate | AccountCreateInput | code | AccountCodeIsEmpty |
| Account | Create | accountCreate | AccountCreateInput | code | AccountCodeTooLong |
| Account | Create | accountCreate | AccountCreateInput | name | AccountNameIsEmpty |
| Account | Create | accountCreate | AccountCreateInput | name | AccountNameTooLong |
| Account | Create | accountCreate | AccountCreateInput | accountTypeSt | AccountTypeInvalid |
| Account | Create | accountCreate | AccountCreateInput | combined | AccountActiveEndBeforeBegin |
| Account | Create | accountCreate | AccountCreateInput | subType | AccountSubtypeInvalid |
| Account | Create | accountCreate | AccountCreateInput | parentCode | AccountCodeIsEmpty |
| Account | Create | accountCreate | AccountCreateInput | parentCode | AccountCodeTooLong |
| Account | Create | accountCreate | AccountCreateInput | parentCode | AccountParentCodeInvalid |
| Account | Create | accountCreate | AccountCreateInput | reference | AccountExternalReferenceIsEmpty |
| Account | Create | accountCreate | AccountCreateInput | reference | AccountExternalReferenceTooLong |
| Account | Create | accountCreate | AccountCreateInput | parentCode | AccountParentIsInactive |
| Account | Create | accountCreate | AccountCreateInput | parentCode | AccountParentAndChildTypesDontMatch |
| Account | Create | accountCreate | AccountCreateInput | parentCode | AccountParentAndChildAreSame |
| Account | Create | accountCreate | AccountCreateInput | combined | AccountInvalidTypeSubtypeCombo |
| Account | Create | accountCreate | AccountCreateInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountCodeIsEmpty |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountCodeTooLong |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountCodeDoesntMatchAccountId |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | activeEnd | AccountAlreadyInactive |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | activeEnd | AccountDeactivationProposedDateIsInvalid |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountActiveChildrenBeforeDeactivation |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountNonZeroBalanceBeforeDeactivation |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | code | AccountDeactivationFailedJournalEntryValidation |
| Account | Deactivate | accountDeactivate | AccountDeactivationInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | UpdateName | accountUpdateName | AccountUpdateNameInput | code | AccountCodeIsEmpty |
| Account | UpdateName | accountUpdateName | AccountUpdateNameInput | code | AccountCodeTooLong |
| Account | UpdateName | accountUpdateName | AccountUpdateNameInput | code | AccountCodeDoesntMatchAccountId |
| Account | UpdateName | accountUpdateName | AccountUpdateNameInput | newName | AccountNameIsEmpty |
| Account | UpdateName | accountUpdateName | AccountUpdateNameInput | newName | AccountNameTooLong |
| Account | UpdateName | accountUpdateName | AccountUpdateNameInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | UpdateExternalReference | accountUpdateExternalReference | AccountUpdateExternalReferenceInput | code | AccountCodeIsEmpty |
| Account | UpdateExternalReference | accountUpdateExternalReference | AccountUpdateExternalReferenceInput | code | AccountCodeTooLong |
| Account | UpdateExternalReference | accountUpdateExternalReference | AccountUpdateExternalReferenceInput | code | AccountCodeDoesntMatchAccountId |
| Account | UpdateExternalReference | accountUpdateExternalReference | AccountUpdateExternalReferenceInput | newReference | AccountExternalReferenceIsEmpty |
| Account | UpdateExternalReference | accountUpdateExternalReference | AccountUpdateExternalReferenceInput | newReference | AccountExternalReferenceTooLong |
| Account | UpdateExternalReference | accountUpdateExternalReference | AccountUpdateExternalReferenceInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | FetchByCode | accountFetchByCode | AccountFetchByCodeInput | code | AccountCodeIsEmpty |
| Account | FetchByCode | accountFetchByCode | AccountFetchByCodeInput | code | AccountCodeTooLong |
| Account | FetchByCode | accountFetchByCode | AccountFetchByCodeInput | code | AccountCodeDoesntMatchAccountId |
| Account | FetchByCode | accountFetchByCode | AccountFetchByCodeInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | FetchByParentCode | accountFetchByParentCode | AccountFetchByParentCodeInput | parentCode | AccountCodeIsEmpty |
| Account | FetchByParentCode | accountFetchByParentCode | AccountFetchByParentCodeInput | parentCode | AccountCodeTooLong |
| Account | FetchByParentCode | accountFetchByParentCode | AccountFetchByParentCodeInput | parentCode | AccountCodeDoesntMatchAccountId |
| Account | FetchByParentCode | accountFetchByParentCode | AccountFetchByParentCodeInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | FetchByAccountType | accountFetchByAccountType | AccountFetchByAccountTypeInput | accountTypeSt | AccountTypeInvalid |
| Account | FetchByAccountType | accountFetchByAccountType | AccountFetchByAccountTypeInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | FetchAll | accountFetchAll | AccountFetchAllInput | combined | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountCode | AccountCodeIsEmpty |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountCode | AccountCodeTooLong |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountCode | AccountCodeDoesntMatchAccountId |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountParentCode | AccountCodeIsEmpty |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountParentCode | AccountCodeTooLong |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountParentCode | AccountParentCodeInvalid |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountType | AccountTypeInvalid |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.accountSubtype | AccountSubtypeInvalid |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.amount | MoneyFailedToConvertImproperPrecision |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.amount | MoneyFailedToConvertExceededMax |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.amount | MoneyFailedToConvertBelowMin |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.description | JournalEntryDescriptionIsEmpty |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.description | JournalEntryDescriptionTooLong |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.source | JournalEntrySourceIsEmpty |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.source | JournalEntrySourceTooLong |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.temporalFilter | FiscalPeriodInvalidKeyString |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | filter.temporalFilter | FiscalPeriodNoPeriodMatchingKey |
| Account | FetchActivity | accountActivityFetch | AccountActivityFetchInput | combined | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| Account | FetchBalances | accountBalancesFetch | AccountBalanceFetchByAccountListInput | codes | AccountCodeIsEmpty |
| Account | FetchBalances | accountBalancesFetch | AccountBalanceFetchByAccountListInput | codes | AccountCodeTooLong |
| Account | FetchBalances | accountBalancesFetch | AccountBalanceFetchByAccountListInput | codes | AccountCodeDoesntMatchAccountId |
| Account | FetchBalances | accountBalancesFetch | AccountBalanceFetchByAccountListInput | codes | AccountBalanceFetchInvalidArguments |
| Account | FetchBalances | accountBalancesFetch | AccountBalanceFetchByAccountListInput | combined | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |

## FiscalPeriod

| domain | verb | handler | contract | element | error |
|---|---|---|---|---|---|
| FiscalPeriod | Create | create | FiscalPeriodInput | periodKey | FiscalPeriodInvalidKeyString |
| FiscalPeriod | Create | create | FiscalPeriodInput | combined | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| FiscalPeriod | FetchByKey | fetch | FiscalPeriodInput | periodKey | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| FiscalPeriod | FetchByKey | fetch | FiscalPeriodInput | periodKey | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| FiscalPeriod | Close | close | FiscalPeriodInput | periodKey | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| FiscalPeriod | Close | close | FiscalPeriodInput | periodKey | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| FiscalPeriod | Reopen | reopen | FiscalPeriodInput | periodKey | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| FiscalPeriod | Reopen | reopen | FiscalPeriodInput | periodKey | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |

## JournalEntry

| domain | verb | handler | contract | element | error |
|---|---|---|---|---|---|
| JournalEntry | PostNew | postNew | JournalEntryInput | description | JournalEntryDescriptionIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | description | JournalEntryDescriptionTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | source | JournalEntrySourceIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | source | JournalEntrySourceTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | entryDate | JournalEntryDateNotInFiscalPeriod |
| JournalEntry | PostNew | postNew | JournalEntryInput | accountCode | AccountCodeIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | accountCode | AccountCodeTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | accountCode | AccountCodeDoesntMatchAccountId |
| JournalEntry | PostNew | postNew | JournalEntryInput | amount | MoneyFailedToConvertImproperPrecision |
| JournalEntry | PostNew | postNew | JournalEntryInput | amount | MoneyFailedToConvertExceededMax |
| JournalEntry | PostNew | postNew | JournalEntryInput | amount | MoneyFailedToConvertBelowMin |
| JournalEntry | PostNew | postNew | JournalEntryInput | lineType | JournalEntryLineTypeInvalid |
| JournalEntry | PostNew | postNew | JournalEntryInput | memo | JournalEntryLineMemoIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | memo | JournalEntryLineMemoTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | financialInstitution | JournalEntryExternalReferenceIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | financialInstitution | JournalEntryExternalReferenceTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | referenceText | JournalEntryReferenceTextIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | referenceText | JournalEntryReferenceTextTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | commentText | JournalEntryCommentIsEmpty |
| JournalEntry | PostNew | postNew | JournalEntryInput | commentText | JournalEntryCommentTooLong |
| JournalEntry | PostNew | postNew | JournalEntryInput | entryDate | JournalEntryHeaderEntryDateInvalid |
| JournalEntry | PostNew | postNew | JournalEntryInput | accountCode | JournalEntryLineAccountDoesntExist |
| JournalEntry | PostNew | postNew | JournalEntryInput | accountCode | JournalEntryLineAccountInactive |
| JournalEntry | PostNew | postNew | JournalEntryInput | amount | JournalEntryLineNonPositiveAmount |
| JournalEntry | PostNew | postNew | JournalEntryInput | combined | JournalEntryCommentPrimaryAndSecondaryIdsAreSame |
| JournalEntry | PostNew | postNew | JournalEntryInput | combined | JournalEntryInsufficientLines |
| JournalEntry | PostNew | postNew | JournalEntryInput | combined | JournalEntryDebitCreditMismatch |
| JournalEntry | FetchById | fetchById | JournalEntryFetchByIdInput | id | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| JournalEntry | FetchByPeriod | fetchByPeriod | JournalEntryFetchByPeriodInput | periodKey | InterfaceBridgeConversionFailure ⚠️BACKSTOP |
| JournalEntry | FetchLinesByAccount | fetchLinesByAccount | JournalEntryFetchLinesByAccountInput | accountCode | AccountCodeIsEmpty |
| JournalEntry | FetchLinesByAccount | fetchLinesByAccount | JournalEntryFetchLinesByAccountInput | accountCode | AccountCodeTooLong |
| JournalEntry | FetchLinesByAccount | fetchLinesByAccount | JournalEntryFetchLinesByAccountInput | accountCode | AccountCodeDoesntMatchAccountId |
| JournalEntry | FetchByExternalReference | fetchByExternalReference | JournalEntryFetchByExternalReferenceInput | fi | JournalEntryExternalReferenceIsEmpty |
| JournalEntry | FetchByExternalReference | fetchByExternalReference | JournalEntryFetchByExternalReferenceInput | fi | JournalEntryExternalReferenceTooLong |
| JournalEntry | FetchByExternalReference | fetchByExternalReference | JournalEntryFetchByExternalReferenceInput | reference | JournalEntryReferenceTextIsEmpty |
| JournalEntry | FetchByExternalReference | fetchByExternalReference | JournalEntryFetchByExternalReferenceInput | reference | JournalEntryReferenceTextTooLong |
| JournalEntry | FetchByExternalReference | fetchByExternalReference | JournalEntryFetchByExternalReferenceInput | combined | JournalEntryFetchByReferenceBothArgumentsNull |
| JournalEntry | FetchByDateRange | fetchByDateRange | JournalEntryFetchByDateRangeInput | (none) | (none — no domain errors) |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | commentText | JournalEntryCommentIsEmpty |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | commentText | JournalEntryCommentTooLong |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | combined | JournalEntryCommentPrimaryAndSecondaryIdsAreSame |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | id | JournalEntryVoidingCannotFetchFiscalPeriod |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | id | JournalEntryVoidingFiscalPeriodIsClosed |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | id | JournalEntryVoidingNoOp |
| JournalEntry | Void | voidJe | JournalEntryVoidInput | id | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| JournalEntry | UpdateExternalReference | updateExternalReference | JournalEntryUpdateExternalReferenceInput | fi | JournalEntryExternalReferenceIsEmpty |
| JournalEntry | UpdateExternalReference | updateExternalReference | JournalEntryUpdateExternalReferenceInput | fi | JournalEntryExternalReferenceTooLong |
| JournalEntry | UpdateExternalReference | updateExternalReference | JournalEntryUpdateExternalReferenceInput | reference | JournalEntryReferenceTextIsEmpty |
| JournalEntry | UpdateExternalReference | updateExternalReference | JournalEntryUpdateExternalReferenceInput | reference | JournalEntryReferenceTextTooLong |
| JournalEntry | UpdateExternalReference | updateExternalReference | JournalEntryUpdateExternalReferenceInput | combined | JournalEntryReferenceUpdateNoOp |
| JournalEntry | AddExternalReference | addExternalReference | JournalEntryAddExternalReferenceInput | financialInstitution | JournalEntryExternalReferenceIsEmpty |
| JournalEntry | AddExternalReference | addExternalReference | JournalEntryAddExternalReferenceInput | financialInstitution | JournalEntryExternalReferenceTooLong |
| JournalEntry | AddExternalReference | addExternalReference | JournalEntryAddExternalReferenceInput | referenceText | JournalEntryReferenceTextIsEmpty |
| JournalEntry | AddExternalReference | addExternalReference | JournalEntryAddExternalReferenceInput | referenceText | JournalEntryReferenceTextTooLong |
| JournalEntry | AddExternalReference | addExternalReference | JournalEntryAddExternalReferenceInput | journalEntryId | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| JournalEntry | AddComment | addComment | JournalEntryAddCommentInput | commentText | JournalEntryCommentIsEmpty |
| JournalEntry | AddComment | addComment | JournalEntryAddCommentInput | commentText | JournalEntryCommentTooLong |
| JournalEntry | AddComment | addComment | JournalEntryAddCommentInput | journalEntryId | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| JournalEntry | AddComment | addComment | JournalEntryAddCommentInput | secondaryJournalEntryId | DalResultantRowsDidntMatchExpectation ⚠️BACKSTOP |
| JournalEntry | AddComment | addComment | JournalEntryAddCommentInput | combined | JournalEntryCommentPrimaryAndSecondaryIdsAreSame |
| JournalEntry | UpdateComment | updateComment | JournalEntryUpdateCommentInput | commentText | JournalEntryCommentIsEmpty |
| JournalEntry | UpdateComment | updateComment | JournalEntryUpdateCommentInput | commentText | JournalEntryCommentTooLong |
| JournalEntry | UpdateComment | updateComment | JournalEntryUpdateCommentInput | secondaryJournalEntryId | JournalEntryCommentPrimaryAndSecondaryIdsAreSame |

## Routes with zero domain-specific errors

- **FiscalPeriod FetchAll** — `FiscalPeriodFetchAllInput` has only `openOnly: bool`. No validation, no conversion.
- **JournalEntry FetchByDateRange** — `JournalEntryFetchByDateRangeInput` has `beginDate` and `endDateInclusive` as `LocalDate` primitives passed straight to the DAL. No begin-after-end check exists anywhere in the path.

## Where backstops come from

**InterfaceBridgeConversionFailure** on Account routes: all from `convert Account to AccountReturn` →
`convert AccountId Option to AccountCodeString Option`, which wraps the parent account's
ID-to-code lookup failure. Only fires on data integrity issues (parent row deleted between
account load and return conversion).

**InterfaceBridgeConversionFailure** on FiscalPeriod routes: from `convert FiscalPeriodKeyString to FiscalPeriodId`,
which wraps the LookupCache miss. The domain error `FiscalPeriodNoPeriodMatchingKey` exists
in `FiscalPeriod.fetchIdByKey` but the routes go through the cache path, not `fetchIdByKey`.

**DalResultantRowsDidntMatchExpectation** on FiscalPeriod Close/Reopen: from `toggleOpenFlagById`'s
UPDATE with `ExactlyOne` — if the period is already in the target state, the WHERE clause
(`is_open = @enforcedCurrentValue`) won't match. No domain error for "already closed" or
"already open" exists.

**DalResultantRowsDidntMatchExpectation** on Account FetchActivity/FetchBalances: from return
conversion — `convert AccountId Option to AccountCode Option` and
`convert AccountId to AccountCodeString` call `LookupCache.accountIdToCode.fetch` without
the `InterfaceBridgeConversionFailure` wrapper that `convert Account to AccountReturn` uses.

**DalResultantRowsDidntMatchExpectation** on JournalEntry FetchById: takes a raw GUID from input
and calls `fetchById` with `ExactlyOne`. No "journal entry not found" domain error exists.

**DalResultantRowsDidntMatchExpectation** on JournalEntry AddExternalReference/AddComment: from
`validateJournalEntryHeader` which calls `JournalEntryHeader.fetchById` with `ExactlyOne`.
If the journal entry doesn't exist, the DAL error leaks. No "journal entry not found" error.
