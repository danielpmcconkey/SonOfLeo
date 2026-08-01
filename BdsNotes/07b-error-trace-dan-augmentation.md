| domain	| verb	| handler	| contract	| element	| error	| comment	| gap in code	| gap in testing	 |
| ---	| ---	| ---	| ---	| ---	| ---	| ---	| ---	| ---	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| code	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| code	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| name	| AccountNameIsEmpty	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| name	| AccountNameTooLong	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| accountTypeSt	| AccountTypeInvalid	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| combined	| AccountActiveEndBeforeBegin	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| subType	| AccountSubtypeInvalid	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentCodeInvalid	| 	| 	| 	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| reference	| AccountExternalReferenceIsEmpty	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| reference	| AccountExternalReferenceTooLong	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentIsInactive	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentAndChildTypesDontMatch	| 	| 	| lower	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| parentCode	| AccountParentAndChildAreSame	| 	| 	| yes	 |
| Account	| Create	| accountCreate	| AccountCreateInput	| combined	| AccountInvalidTypeSubtypeCombo	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| activeEnd	| AccountAlreadyInactive	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| activeEnd	| AccountDeactivationProposedDateIsInvalid	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountActiveChildrenBeforeDeactivation	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountNonZeroBalanceBeforeDeactivation	| 	| 	| lower	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate	| 	| 	| yes	 |
| Account	| Deactivate	| accountDeactivate	| AccountDeactivationInput	| code	| AccountDeactivationFailedJournalEntryValidation	| 	| 	| yes	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| code	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| code	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| newName	| AccountNameIsEmpty	| 	| 	| lower	 |
| Account	| UpdateName	| accountUpdateName	| AccountUpdateNameInput	| newName	| AccountNameTooLong	| 	| 	| lower	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| code	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| code	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| 	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| newReference	| AccountExternalReferenceIsEmpty	| 	| 	| lower	 |
| Account	| UpdateExternalReference	| accountUpdateExternalReference	| AccountUpdateExternalReferenceInput	| newReference	| AccountExternalReferenceTooLong	| 	| 	| lower	 |
| Account	| FetchByCode	| accountFetchByCode	| AccountFetchByCodeInput	| code	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| FetchByCode	| accountFetchByCode	| AccountFetchByCodeInput	| code	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| FetchByCode	| accountFetchByCode	| AccountFetchByCodeInput	| code	| AccountCodeDoesntMatchAccountId	| 	| 	| lower	 |
| Account	| FetchByParentCode	| accountFetchByParentCode	| AccountFetchByParentCodeInput	| parentCode	| AccountCodeIsEmpty	| Account code is unambiguous here	| 	| lower	 |
| Account	| FetchByParentCode	| accountFetchByParentCode	| AccountFetchByParentCodeInput	| parentCode	| AccountCodeTooLong	| Account code is unambiguous here	| 	| lower	 |
| Account	| FetchByParentCode	| accountFetchByParentCode	| AccountFetchByParentCodeInput	| parentCode	| AccountCodeDoesntMatchAccountId	| Account code is unambiguous here	| 	| 	 |
| Account	| FetchByAccountType	| accountFetchByAccountType	| AccountFetchByAccountTypeInput	| accountTypeSt	| AccountTypeInvalid	| 	| 	| lower	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountCode	| AccountCodeIsEmpty	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountCode	| AccountCodeTooLong	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountCode	| AccountCodeDoesntMatchAccountId	| 	| 	| lower	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountParentCode	| AccountParentCodeIsEmpty	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountParentCode	| AccountParentCodeTooLong	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountParentCode	| AccountParentCodeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountType	| AccountTypeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.accountSubtype	| AccountSubtypeInvalid	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.amount	| MoneyFailedToConvertImproperPrecision	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.amount	| MoneyFailedToConvertExceededMax	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.amount	| MoneyFailedToConvertBelowMin	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.description	| JournalEntryDescriptionIsEmpty	| 	| 	| lower	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.description	| JournalEntryDescriptionTooLong	| 	| 	| lower	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.source	| JournalEntrySourceIsEmpty	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.source	| JournalEntrySourceTooLong	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.temporalFilter	| FiscalPeriodInvalidKeyString	| 	| 	| 	 |
| Account	| FetchActivity	| accountActivityFetch	| AccountActivityFetchInput	| filter.temporalFilter	| FiscalPeriodNoPeriodMatchingKey	| 	| 	| 	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountCodeIsEmpty	| 	| 	| lower	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountCodeTooLong	| 	| 	| lower	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountCodeDoesntMatchAccountId	| 	| 	| lower	 |
| Account	| FetchBalances	| accountBalancesFetch	| AccountBalanceFetchByAccountListInput	| codes	| AccountBalanceFetchInvalidArguments	| 	| 	| lower	 |
| FiscalPeriod	| Create	| create	| FiscalPeriodInput	| periodKey	| FiscalPeriodInvalidKeyString	| 	| 	| lower	 |
| FiscalPeriod	| Create	| create	| FiscalPeriodInput	| combined	| DalResultantRowsDidntMatchExpectation	| nearly impossible to reach this error	| 	| yes	 |
| FiscalPeriod	| FetchByKey	| fetch	| FiscalPeriodInput	| periodKey	| FiscalPeriodNoPeriodMatchingKey	| 	| 	| yes	 |
| FiscalPeriod	| FetchByKey	| fetch	| FiscalPeriodInput	| periodKey	| FiscalPeriodNoPeriodMatchingId	| 	| 	| yes	 |
| FiscalPeriod	| Close	| close	| FiscalPeriodInput	| periodKey	| FiscalPeriodNoPeriodMatchingKey	| 	| 	| yes	 |
| FiscalPeriod	| Close	| close	| FiscalPeriodInput	| periodKey	| DalResultantRowsDidntMatchExpectation	| nearly impossible to reach this error	| 	| yes	 |
| FiscalPeriod	| Reopen	| reopen	| FiscalPeriodInput	| periodKey	| FiscalPeriodNoPeriodMatchingKey	| 	| 	| yes	 |
| FiscalPeriod	| Reopen	| reopen	| FiscalPeriodInput	| periodKey	| FiscalPeriodToggleOpenNoOp	| 	| 	| lower	 |
| FiscalPeriod	| Reopen	| reopen	| FiscalPeriodInput	| periodKey	| DalResultantRowsDidntMatchExpectation	| nearly impossible to reach this error	| 	| yes	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| description	| JournalEntryDescriptionIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| description	| JournalEntryDescriptionTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| source	| JournalEntrySourceIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| source	| JournalEntrySourceTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| entryDate	| JournalEntryDateNotInFiscalPeriod	| 	| 	| lower	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| AccountCodeIsEmpty	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| AccountCodeTooLong	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| AccountCodeDoesntMatchAccountId	| 	| 	| lower	 |
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
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| entryDate	| JournalEntryHeaderEntryDateInvalid	| 	| 	| lower	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| JournalEntryLineAccountDoesntExist	| 	| 	| lower	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| accountCode	| JournalEntryLineAccountInactive	| 	| 	| lower	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| amount	| JournalEntryLineNonPositiveAmount	| 	| 	| lower	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| combined	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame	| 	| 	| lower	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| combined	| JournalEntryInsufficientLines	| 	| 	| 	 |
| JournalEntry	| PostNew	| postNew	| JournalEntryInput	| combined	| JournalEntryDebitCreditMismatch	| 	| 	| lower	 |
| JournalEntry	| FetchById	| fetchById	| JournalEntryFetchByIdInput	| id	| JournalEntryHeaderIdDoesntExist	| 	| 	| 	 |
| JournalEntry	| FetchByPeriod	| fetchByPeriod	| JournalEntryFetchByPeriodInput	| periodKey	| FiscalPeriodNoPeriodMatchingKey	| 	| 	| lower	 |
| JournalEntry	| FetchLinesByAccount	| fetchLinesByAccount	| JournalEntryFetchLinesByAccountInput	| accountCode	| AccountCodeIsEmpty	| 	| 	| lower	 |
| JournalEntry	| FetchLinesByAccount	| fetchLinesByAccount	| JournalEntryFetchLinesByAccountInput	| accountCode	| AccountCodeTooLong	| 	| 	| lower	 |
| JournalEntry	| FetchLinesByAccount	| fetchLinesByAccount	| JournalEntryFetchLinesByAccountInput	| accountCode	| AccountCodeDoesntMatchAccountId	| 	| 	| lower	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| fi	| JournalRefFinancialInstitutionIsEmpty	| 	| 	| lower	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| fi	| JournalRefFinancialInstitutionTooLong	| 	| 	| lower	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| reference	| JournalEntryReferenceTextIsEmpty	| 	| 	| lower	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| reference	| JournalEntryReferenceTextTooLong	| 	| 	| lower	 |
| JournalEntry	| FetchByExternalReference	| fetchByExternalReference	| JournalEntryFetchByExternalReferenceInput	| combined	| JournalEntryFetchByReferenceBothArgumentsNull	| 	| 	| lower	 |
| JournalEntry	| FetchByDateRange	| fetchByDateRange	| JournalEntryFetchByDateRangeInput	| (none)	| JournalEntryFetchByDateRangeBeginAfterEnd	| 	| 	| yes	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| combined	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryVoidingCannotFetchFiscalPeriod	| 	| 	| yes	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryVoidingFiscalPeriodIsClosed	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryVoidingNoOp	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| id	| JournalEntryHeaderIdDoesntExist	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| JournalEntryCommentInput	| JournalEntryCommentPrimaryJeHeaderIdNotFound	| 	| 	| lower	 |
| JournalEntry	| Void	| voidJe	| JournalEntryVoidInput	| JournalEntryCommentInput	| JournalEntryCommentSecondaryJeHeaderIdNotFound	| 	| 	| yes	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| fi	| JournalRefFinancialInstitutionIsEmpty	| 	| 	| lower	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| fi	| JournalRefFinancialInstitutionTooLong	| 	| 	| lower	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| reference	| JournalEntryReferenceTextIsEmpty	| 	| 	| lower	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| reference	| JournalEntryReferenceTextTooLong	| 	| 	| lower	 |
| JournalEntry	| UpdateExternalReference	| updateExternalReference	| JournalEntryUpdateExternalReferenceInput	| combined	| JournalEntryReferenceUpdateNoOp	| 	| 	| yes	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| financialInstitution	| JournalRefFinancialInstitutionIsEmpty	| 	| 	| lower	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| financialInstitution	| JournalRefFinancialInstitutionTooLong	| 	| 	| lower	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| referenceText	| JournalEntryReferenceTextIsEmpty	| 	| 	| lower	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| referenceText	| JournalEntryReferenceTextTooLong	| 	| 	| lower	 |
| JournalEntry	| AddExternalReference	| addExternalReference	| JournalEntryAddExternalReferenceInput	| journalEntryId	| JournalEntryHeaderIdDoesntExist	| 	| 	| lower	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| lower	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| lower	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| journalEntryId	| JournalEntryCommentPrimaryJeHeaderIdNotFound	| 	| 	| lower	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| secondaryJournalEntryId	| JournalEntryCommentSecondaryJeHeaderIdNotFound	| 	| 	| yes	 |
| JournalEntry	| AddComment	| addComment	| JournalEntryAddCommentInput	| combined	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame	| 	| 	| lower	 |
| JournalEntry	| UpdateComment	| updateComment	| JournalEntryUpdateCommentInput	| commentText	| JournalEntryCommentIsEmpty	| 	| 	| 	 |
| JournalEntry	| UpdateComment	| updateComment	| JournalEntryUpdateCommentInput	| commentText	| JournalEntryCommentTooLong	| 	| 	| lower	 |
| JournalEntry	| UpdateComment	| updateComment	| JournalEntryUpdateCommentInput	| secondaryJournalEntryId	| JournalEntryCommentPrimaryAndSecondaryIdsAreSame 	| 	| 	| lower	 |
