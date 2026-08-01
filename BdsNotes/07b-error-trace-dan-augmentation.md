| domain	| verb	| handler	| contract	| element	| error	| comment	| gap in code	| gap in testing	 |
| ---	| ---	| ---	| ---	| ---	| ---	| ---	| ---	| ---	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| code	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| code	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| name	| AccountNameIsEmpty	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| name	| AccountNameTooLong	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| accountTypeSt	| AccountTypeInvalid	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| combined	| AccountActiveEndBeforeBegin	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| subType	| AccountSubtypeInvalid	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentCodeInvalid	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| reference	| AccountExternalReferenceIsEmpty	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| reference	| AccountExternalReferenceTooLong	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentIsInactive	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentAndChildTypesDontMatch	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentAndChildAreSame	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| combined	| AccountInvalidTypeSubtypeCombo	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| activeEnd	| AccountAlreadyInactive	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| activeEnd	| AccountDeactivationProposedDateIsInvalid	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountActiveChildrenBeforeDeactivation	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountNonZeroBalanceBeforeDeactivation	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountDeactivationFailedJournalEntryValidation	| 	| 	| 	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| code	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| code	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| newName	| AccountNameIsEmpty	| 	| 	| 	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| newName	| AccountNameTooLong	| 	| 	| 	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| code	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| code	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| newReference	| AccountExternalReferenceIsEmpty	| 	| 	| 	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| newReference	| AccountExternalReferenceTooLong	| 	| 	| 	 |
| Account	| FetchByCode	| accountFetchByCode	| AccountFetchByCodeInput	| code	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| FetchByCode	| accountFetchByCode	| AccountFetchByCodeInput	| code	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| FetchByCode	| accountFetchByCode	| AccountFetchByCodeInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| FetchByParentCode	| accountFetchByParentCode	| AccountFetchByParentCodeInput	| parentCode	| AccountCodeIsEmpty	| Account code is unambiguous here	| 	| 	 |
| Account	| FetchByParentCode	| accountFetchByParentCode	| AccountFetchByParentCodeInput	| parentCode	| AccountCodeTooLong	| Account code is unambiguous here	| 	| 	 |
| Account	| FetchByParentCode	| accountFetchByParentCode	| AccountFetchByParentCodeInput	| parentCode	| AccountCodeDoesntMatchAccountId	| Account code is unambiguous here	| 	| 	 |
| Account	| FetchByAccountType	| accountFetchByAccountType	| AccountFetchByAccountTypeInput	| accountTypeSt	| AccountTypeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountCode	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountCode	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountCode	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountParentCode	| AccountCodeIsEmpty	| 	| yes	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountParentCode	| AccountCodeTooLong	| 	| yes	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountParentCode	| AccountParentCodeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountType	| AccountTypeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountSubtype	| AccountSubtypeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.amount	| MoneyFailedToConvertImproperPrecision	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.amount	| MoneyFailedToConvertExceededMax	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.amount	| MoneyFailedToConvertBelowMin	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.description	| JournalEntryDescriptionIsEmpty	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.description	| JournalEntryDescriptionTooLong	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.source	| JournalEntrySourceIsEmpty	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.source	| JournalEntrySourceTooLong	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.temporalFilter	| FiscalPeriodInvalidKeyString	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.temporalFilter	| FiscalPeriodNoPeriodMatchingKey	| 	| 	| 	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountBalanceFetchInvalidArguments	| 	| 	| 	 |
| FiscalPeriod	| Create	| create	| FiscalPeriodInput	| periodKey	| FiscalPeriodInvalidKeyString	| 	| 	| 	 |
| FiscalPeriod	| Create	| create	| FiscalPeriodInput	| combined	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| FiscalPeriod	| FetchByKey	| fetch	| FiscalPeriodInput	| periodKey	| InterfaceBridgeConversionFailure	| BACKSTOP	| yes	| 	 |
| FiscalPeriod	| FetchByKey	| fetch	| FiscalPeriodInput	| periodKey	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| FiscalPeriod	| Close	| close	| FiscalPeriodInput	| periodKey	| InterfaceBridgeConversionFailure	| BACKSTOP	| yes	| 	 |
| FiscalPeriod	| Close	| close	| FiscalPeriodInput	| periodKey	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| FiscalPeriod	| Reopen	| reopen	| FiscalPeriodInput	| periodKey	| InterfaceBridgeConversionFailure	| BACKSTOP	| yes	| 	 |
| FiscalPeriod	| Reopen	| reopen	| FiscalPeriodInput	| periodKey	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| description	| JournalEntryDescriptionIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| description	| JournalEntryDescriptionTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| source	| JournalEntrySourceIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| source	| JournalEntrySourceTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| entryDate	| JournalEntryDateNotInFiscalPeriod	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| AccountCodeIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| AccountCodeTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| amount	| MoneyFailedToConvertImproperPrecision	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| amount	| MoneyFailedToConvertExceededMax	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| amount	| MoneyFailedToConvertBelowMin	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| lineType	| JournalEntryLineTypeInvalid	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| memo	| JournalEntryLineMemoIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| memo	| JournalEntryLineMemoTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| financialInstitution	| JournalEntryExternalReferenceIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| financialInstitution	| JournalEntryExternalReferenceTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| referenceText	| JournalEntryReferenceTextIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| referenceText	| JournalEntryReferenceTextTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| entryDate	| JournalEntryHeaderEntryDateInvalid	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| JournalEntryLineAccountDoesntExist	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| JournalEntryLineAccountInactive	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| amount	| JournalEntryLineNonPositiveAmount	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| combined	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| combined	| JournalEntryInsufficientLines	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| combined	| JournalEntryDebitCreditMismatch	| 	| 	| 	 |
| JournalEntry	| FetchById	| fetchById	| JournalEntryFetchByIdInput	| id	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| JournalEntry	| FetchByPeriod	| fetchByPeriod	| JournalEntryFetchByPeriodInput	| periodKey	| InterfaceBridgeConversionFailure	| BACKSTOP	| yes	| 	 |
| JournalEntry	| FetchLinesByAccount	| fetchLinesByAccount	| JournalEntryFetchLinesByAccountInput	| accountCode	| AccountCodeIsEmpty	| 	| 	| 	 |
| JournalEntry	| FetchLinesByAccount	| fetchLinesByAccount	| JournalEntryFetchLinesByAccountInput	| accountCode	| AccountCodeTooLong	| 	| 	| 	 |
| JournalEntry	| FetchLinesByAccount	| fetchLinesByAccount	| JournalEntryFetchLinesByAccountInput	| accountCode	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| fi	| JournalEntryExternalReferenceIsEmpty	| wrong error code	| yes	| 	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| fi	| JournalEntryExternalReferenceTooLong	| wrong error code	| yes	| 	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| reference	| JournalEntryReferenceTextIsEmpty	| 	| 	| 	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| reference	| JournalEntryReferenceTextTooLong	| 	| 	| 	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| combined	| JournalEntryFetchByReferenceBothArgumentsNull	| 	| 	| 	 |
| JournalEntry	| FetchByDateRange	| fetchByDateRange	| JournalEntryFetchByDateRangeInput	| (none)	| (none — no domain errors)	| we need to check that begin is not after end	| yes	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| combined	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame	| 	| 	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryVoidingCannotFetchFiscalPeriod	| 	| 	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryVoidingFiscalPeriodIsClosed	| 	| 	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryVoidingNoOp	| 	| 	| 	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| fi	| JournalEntryExternalReferenceIsEmpty	| wrong error code	| yes	| 	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| fi	| JournalEntryExternalReferenceTooLong	| wrong error code	| yes	| 	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| reference	| JournalEntryReferenceTextIsEmpty	| 	| 	| 	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| reference	| JournalEntryReferenceTextTooLong	| 	| 	| 	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| combined	| JournalEntryReferenceUpdateNoOp	| 	| 	| 	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| financialInstitution	| JournalEntryExternalReferenceIsEmpty	| wrong error code	| yes	| 	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| financialInstitution	| JournalEntryExternalReferenceTooLong	| wrong error code	| yes	| 	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| referenceText	| JournalEntryReferenceTextIsEmpty	| 	| 	| 	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| referenceText	| JournalEntryReferenceTextTooLong	| 	| 	| 	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| journalEntryId	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| 	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| 	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| journalEntryId	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| secondaryJournalEntryId	| DalResultantRowsDidntMatchExpectation	| BACKSTOP	| yes	| 	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| combined	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame	| 	| 	| 	 |
| JournalEntry	| UpdateComment	| updateComment	| JournalEntryUpdateCommentInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| 	 |
| JournalEntry	| UpdateComment	| updateComment	| JournalEntryUpdateCommentInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| 	 |
| JournalEntry	| UpdateComment	| updateComment	| JournalEntryUpdateCommentInput	| secondaryJournalEntryId	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame 	| 	| 	| 	 |
