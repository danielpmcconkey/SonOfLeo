module Utilities.AppError

open System
open NodaTime

type AppError =
    
    /// TestingError is NEVER to be used in the Src directory. It is only here to facilitate automated testing. Such as
    /// when I need to assert that somewthing was supposed to fail but it doesn't.
    | TestingError of string
    
    | AccountActiveChildrenBeforeDeactivation of Guid
    | AccountAlreadyInactive of Guid * LocalDate
    | AccountBalanceFetchInvalidArguments
    | AccountCodeDoesntMatchAccountId of string
    | AccountCodeIsEmpty of string
    | AccountCodeTooLong of string * int
    | AccountDeactivationFailedJournalEntryValidation
    | AccountDeactivationProposedDateIsInvalid of Guid * LocalDate * LocalDate
    | AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate of Guid
    | AccountExternalReferenceIsEmpty of string
    | AccountExternalReferenceTooLong of string * int
    | AccountIdDoesntMatch of Guid
    | AccountInvalidTypeSubtypeCombo of string * string option
    | AccountNameIsEmpty of string
    | AccountNameTooLong of string * int
    | AccountNonZeroBalanceBeforeDeactivation of Guid * decimal * decimal
    | AccountParentAndChildAreSame of Guid option * Guid
    | AccountParentAndChildTypesDontMatch of string * string
    | AccountParentCodeInvalid of string
    | AccountParentCodeIsEmpty of string
    | AccountParentCodeTooLong of string * int
    | AccountParentIsInactive of Guid
    | AccountSubtypeInvalid of string
    | AccountTypeInvalid of string
    | AccountUpdateNoOp

    | ActiveEndBeforeBegin of LocalDate * LocalDate option

    | CashflowAgreementMemoIsEmpty of string
    | CashflowAgreementMemoTooLong of string * int
    | CashflowAgreementNameIsEmpty of string
    | CashflowAgreementNameTooLong of string * int
    | CashflowAgreementUpdateNoOp
    | CashflowBlockerNoteIsEmpty of string
    | CashflowBlockerNoteTooLong of string * int
    | CashflowCounterpartyIsEmpty of string
    | CashflowCounterpartyTooLong of string * int
    | CashflowExternalInvoiceIdIsEmpty of string
    | CashflowExternalInvoiceIdTooLong of string * int
    | CashflowInstanceFulfilledWithNoInvoices of Guid
    | CashflowInstanceFulfilledWithUnpaidInvoice of Guid * Guid
    | CashflowInstanceIdDoesntExist of Guid
    | CashflowInstanceIdListCannotBeEmpty
    | CashflowInstanceNotUnderMasterAgreement of Guid * Guid
    | CashflowInstanceUpdateNoOp
    | CashflowInvalidBlocker of string
    | CashflowInvalidBlockerRow of string
    | CashflowInvalidCadenceRow of string
    | CashflowInvalidDateInMonthNumber of int
    | CashflowInvalidFlowDirection of string
    | CashflowInvalidInvoiceState of string
    | CashflowInvalidMonth of string
    | CashflowInvalidPaymentAmountRow of string
    | CashflowInvalidPaymentState of string
    | CashflowInvalidPaymentTransactionPointerRow of string
    | CashflowInvalidPostedState of string
    | CashflowInvalidWeekDay of string
    | CashflowInvalidWeekInMonthNumber of int
    | CashflowInvoiceCompositeUpdateNoOp
    | CashflowInvoiceDiamondMismatch of Guid * Guid * Guid
    | CashflowInvoiceFullyPaidAmountMismatch of Guid * decimal * decimal
    | CashflowInvoiceFullyPaidWithBlocker of Guid
    | CashflowInvoiceIdDoesntExist of Guid
    | CashflowInvoiceIdListCannotBeEmpty
    | CashflowInvoiceMemoIsEmpty of string
    | CashflowInvoiceMemoTooLong of string * int
    | CashflowInvoiceNonPositiveAmount of Guid * decimal
    | CashflowInvoiceNotUnderInstance of Guid * Guid
    | CashflowInvoiceNotUnderMasterAgreement of Guid * Guid
    | CashflowInvoicePartiallyPaidWithNoPayments of Guid
    | CashflowInvoicePartiallyPostedWithNoPostedPayment of Guid
    | CashflowInvoicePostedToLedgerRequiresFullyPaid of Guid
    | CashflowInvoicePostedToLedgerWithUnpostedPayment of Guid
    | CashflowInvoiceUpdateNoOp
    | CashflowMasterAgreementIdDoesntExist of Guid
    | CashflowMasterAgreementIdListCannotBeEmpty
    | CashflowMasterAgreementUnavailable of Guid * LocalDate * LocalDate * LocalDate option
    | CashflowMasterAgreementUpdateNoOp
    | CashflowPaymentAgreementCreditAccountInvalid of Guid
    | CashflowPaymentAgreementDebitAccountInvalid of Guid
    | CashflowPaymentAgreementIdDoesntExist of Guid
    | CashflowPaymentAgreementMemoIsEmpty of string
    | CashflowPaymentAgreementMemoTooLong of string * int
    | CashflowPaymentAgreementNonPositiveExpectedAmount of Guid * decimal
    | CashflowPaymentAgreementNotUnderMasterAgreement of Guid * Guid
    | CashflowPaymentAgreementUpdateNoOp
    | CashflowPaymentAgreementsListCannotBeEmpty
    | CashflowPaymentMemoIsEmpty of string
    | CashflowPaymentMemoTooLong of string * int
    | CashflowPaymentNotUnderInvoice of Guid * Guid
    | CashflowPaymentNotUnderMasterAgreement of Guid * Guid
    | CashflowPaymentPostedToLedgerDateMismatch of Guid * LocalDate * LocalDate
    | CashflowPaymentPostedToLedgerDateWithoutJournalEntry of Guid
    | CashflowPaymentUpdateNoOp
    | CashflowProjectionHorizonInDaysBelowMin of int * int
    | CashflowProjectionHorizonInDaysExceededMax of int * int

    | CliUnknownCommand of string * string
    
    | ConfigReadError of string * exn
    | ConfigNotFound of string
    
    | DalCantCompleteTransactionOfNone
    | DalCantFetchTransactionOfNone
    | DalCantUseTransactionOfNoneInAutoCommit
    | DalConnectionStringEnvVarContainsConnectionString
    | DalConnectionStringEnvVarNotFound
    | DalConnectionStringIsEmpty
    | DalDecimalUnboxingReturnedNull
    | DalEnvVarNotSet of string
    | DalErrorDuringAutoCompleteTransactionRun of exn
    | DalErrorDuringDecimalOptionUnboxing of exn
    | DalErrorDuringDecimalUnboxing of exn
    | DalErrorDuringInstantOptionUnboxing of exn
    | DalErrorDuringInstantUnboxing of exn
    | DalErrorDuringIntOptionUnboxing of exn
    | DalErrorDuringIntUnboxing of exn
    | DalErrorDuringLocalDateOptionUnboxing of exn
    | DalErrorDuringLocalDateUnboxing of exn
    | DalErrorDuringLongOptionUnboxing of exn
    | DalErrorDuringLongUnboxing of exn
    | DalErrorDuringNonQueryExecution of exn
    | DalErrorDuringReaderQueryExecution of exn
    | DalErrorDuringScalarExecution of exn
    | DalErrorDuringStringOptionUnboxing of exn
    | DalErrorDuringStringUnboxing of exn
    | DalErrorDuringTransactionCommit of exn
    | DalErrorDuringTransactionCreation of exn
    | DalErrorDuringTransactionRollback of exn
    | DalErrorDuringUuidOptionUnboxing of exn
    | DalErrorDuringUuidUnboxing of exn
    | DalInstantUnboxingReturnedNull
    | DalIntUnboxingReturnedNull
    | DalLocalDateUnboxingReturnedNull
    | DalLongUnboxingReturnedNull
    | DalResultantRowsDidntMatchExpectation of string * int
    | DalStringUnboxingReturnedNull
    | DalUuidUnboxingReturnedNull
    
    | FileIoDirectoryDoesntExist of string
    | FileIoFileDoesntExist of string
    | FileIoError of exn
    
    | FiscalPeriodInvalidKeyString of string
    | FiscalPeriodNoPeriodMatchingId of Guid
    | FiscalPeriodNoPeriodMatchingKey of string
    | FiscalPeriodToggleOpenNoOp
    
    | IngestionBaseStageEntryGroupIdIsEmpty of string
    | IngestionBaseStageEntryGroupIdTooLong of string * int
    | IngestionBaseStageGroupIdDistinctDataViolation of string
    | IngestionClassificationRuleGroupsEmpty
    | IngestionClassificationRuleNameIsEmpty of string 
    | IngestionClassificationRuleNameTooLong of string * int 
    | IngestionClassificationRuleUpdateNoOp
    | IngestionFieldMatchChainEmpty
    | IngestionInvalidClassificationGroupConnector of string
    | IngestionInvalidNumericSearchOperator of string
    | IngestionInvalidStagedEntryStatus of string
    | IngestionInvalidStageStatusChangeMechanism of string
    | IngestionInvalidStageStatusTransition of string option * string
    | IngestionSearchPatternIsEmpty of string
    | IngestionSearchPatternTooLong of string * int
    | IngestionStageLineNonPositiveAmount of decimal
    | IngestionStageEntryDebitCreditMismatch of decimal * decimal
    | IngestionStageEntryInsufficientLines of int
    | IngestionStageEntryHeaderIdDoesntExist of Guid
    | IngestionStageEntryHeaderNoOp
    | IngestionStageEntryLineNoOp
    | IngestionStageHeaderIdListCannotBeEmpty
    | IngestionSourceFileIsEmpty of string
    | IngestionSourceFileTooLong of string * int
    | IngestionStatusTransitionList
    | IngestionUpdateStageEntryLinesMustMatchHeader of Guid * Guid
    | IngestionNoneAccount of Guid
    | IngestionUpdateStageEntryNoOp
    | IngestionSourceNameNotFound of string
    
    | InterfaceBridgeConversionFailure of string * string * string * string
    | InterfaceBridgeFailedJsonDeserialization of string * string * string
    | InterfaceBridgeFailedJsonSerialization of string * string * string
    
    | JournalEntryCommentIsEmpty of string
    | JournalEntryCommentPrimaryAndSecondaryIdsAreSame of Guid * Guid
    | JournalEntryCommentPrimaryJeHeaderIdNotFound of Guid
    | JournalEntryCommentSecondaryJeHeaderIdNotFound of Guid
    | JournalEntryCommentTooLong of string * int
    | JournalEntryCommentUpdateNoOp
    | JournalEntryDateNotInFiscalPeriod of LocalDate
    | JournalEntryDebitCreditMismatch of decimal * decimal
    | JournalEntryDescriptionIsEmpty of string
    | JournalEntryDescriptionTooLong of string * int
    | JournalEntryExternalReferenceIsEmpty of string
    | JournalEntryExternalReferenceTooLong of string * int
    | JournalEntryFetchByDateRangeBeginAfterEnd of LocalDate * LocalDate
    | JournalEntryFetchByReferenceBothArgumentsNull
    | JournalEntryHeaderEntryDateInvalid of LocalDate
    | JournalEntryHeaderIdDoesntExist of Guid
    | JournalEntryHeaderIdListCannotBeEmpty
    | JournalEntryInsufficientLines of int
    | JournalEntryLineAccountDoesntExist of Guid
    | JournalEntryLineAccountInactive of Guid * LocalDate * LocalDate * LocalDate Option
    | JournalEntryLineMemoIsEmpty of string
    | JournalEntryLineMemoTooLong of string * int
    | JournalEntryLineNonPositiveAmount of decimal
    | JournalEntryLineTypeInvalid of string
    | JournalEntryReferenceTextIsEmpty of string
    | JournalEntryReferenceTextTooLong of string * int
    | JournalEntryReferenceUpdateNoOp
    | JournalEntrySourceIsEmpty of string
    | JournalEntrySourceTooLong of string * int
    | JournalEntryVoidingCannotFetchFiscalPeriod of LocalDate * Guid
    | JournalEntryVoidingFiscalPeriodIsClosed of LocalDate * Guid
    | JournalEntryVoidingNoOp of Guid
    | JournalRefFinancialInstitutionIsEmpty of string
    | JournalRefFinancialInstitutionTooLong of string * int
    
    | MoneyFailedToConvertBelowMin of decimal * decimal
    | MoneyFailedToConvertExceededMax of decimal * decimal
    | MoneyFailedToConvertImproperPrecision of decimal
    | MoneyImproperSplit of int
    | MoneySplitFailedReconciliation of decimal * decimal
    
    | ReportingUnknownReportName of string

module AppError =
    let toMessage =
        function

        | TestingError message -> message
        
        | AccountActiveChildrenBeforeDeactivation uuid -> $"Account {uuid} deactivation failed because one or more child account records is active."
        | AccountAlreadyInactive(uuid, endDate) -> $"Account {uuid} deactivation failed because active end is already set to {endDate}."
        | AccountBalanceFetchInvalidArguments -> "fetchByAccountIdList requires at least one account ID"
        | AccountCodeDoesntMatchAccountId code -> $"Account code of {code} doesn't match an Account ID in the database."
        | AccountCodeIsEmpty code -> $"Account code cannot be empty. Provided code is {code}."
        | AccountCodeTooLong(code, max) -> $"Account code cannot exceed {max} characters. Provided code is {code}."
        | AccountDeactivationFailedJournalEntryValidation -> "Failed to validate the Account's Journal Entries prior to deactivation"
        | AccountDeactivationProposedDateIsInvalid(uuid, proposedDate, beginDate) -> $"Deactivating account {uuid} failed because the active end ({proposedDate}) would be before the active begin ({beginDate})"
        | AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate uuid -> $"Account {uuid} cannot be deactivated as it has one or more Journal Entries dated after the deactivation date."
        | AccountExternalReferenceIsEmpty externalReference -> $"Account external reference cannot be empty. Provided external reference is {externalReference}."
        | AccountExternalReferenceTooLong(externalReference, max) -> $"Account external reference cannot exceed {max} characters. Provided external reference is {externalReference}."
        | AccountIdDoesntMatch uuid -> $"There is no account in the database with ID {uuid}."
        | AccountInvalidTypeSubtypeCombo(accountType, subtype) -> $"Invalid AccountType / AccountSubType combo: {accountType} / {subtype}"
        | AccountNameIsEmpty name -> $"Account name cannot be empty. Provided name is {name}."
        | AccountNameTooLong(name, max) -> $"Account name cannot exceed {max} characters. Provided name is {name}."
        | AccountNonZeroBalanceBeforeDeactivation(uuid, debits, credits) -> $"The Account {uuid} cannot be deactivated as it has a non-zero balance. Total debits: {debits}. Total credits: {credits}."
        | AccountParentAndChildAreSame(parent, child) -> $"A child account ({child}) cannot be its own parent ({parent})."
        | AccountParentAndChildTypesDontMatch(parent, child) -> $"Parent ({parent}) and child ({child}) account types do not match."
        | AccountParentCodeInvalid code -> $"Provided parent code ({code}) doesn't match an ID in the database."
        | AccountParentCodeIsEmpty code -> $"Account parent code cannot be empty. Provided code is {code}."
        | AccountParentCodeTooLong(code, max) -> $"Account parent code cannot exceed {max} characters. Provided code is {code}."
        | AccountParentIsInactive uuid -> $"Parent account {uuid} failed \"is active\" check."
        | AccountSubtypeInvalid subtype -> $"Provided string of '{subtype}' is not a valid account subtype."
        | AccountTypeInvalid typeString -> $"Provided string of '{typeString}' is not a valid account type."
        | AccountUpdateNoOp -> "Updating the account record failed because at least one updatable parameter must be set."

        | ActiveEndBeforeBegin(activeBegin, activeEnd) -> $"An activity period's active end ({activeEnd}) cannot be before its active begin ({activeBegin})."

        | CashflowAgreementMemoIsEmpty memo -> $"AgreementMemo cannot be empty. Provided Memo is {memo}."
        | CashflowAgreementMemoTooLong(memo, max) -> $"AgreementMemo cannot exceed {max} characters. Provided Memo is {memo}."
        | CashflowAgreementNameIsEmpty name -> $"AgreementName cannot be empty. Provided name is {name}."
        | CashflowAgreementNameTooLong(name, max) -> $"AgreementName cannot exceed {max} characters. Provided name is {name}."
        | CashflowAgreementUpdateNoOp -> "Updating the Agreement composite failed because at least one updatable parameter must be set."
        | CashflowBlockerNoteIsEmpty note -> $"AgreementMemo cannot be empty. Provided Memo is {note}."
        | CashflowBlockerNoteTooLong(note, max) -> $"BlockerNote cannot exceed {max} characters. Provided Memo is {note}."
        | CashflowCounterpartyIsEmpty name -> $"Counterparty cannot be empty. Provided name is {name}."
        | CashflowCounterpartyTooLong(name, max) -> $"Counterparty cannot exceed {max} characters. Provided name is {name}."
        | CashflowExternalInvoiceIdIsEmpty eid -> $"ExternalInvoiceId cannot be empty. Provided ExternalInvoiceId is {eid}."
        | CashflowExternalInvoiceIdTooLong(eid, max) -> $"ExternalInvoiceId cannot exceed {max} characters. Provided ExternalInvoiceId is {eid}."
        | CashflowInstanceFulfilledWithNoInvoices instanceId -> $"Instance {instanceId} is marked fulfilled but has no Invoices."
        | CashflowInstanceFulfilledWithUnpaidInvoice(instanceId, invoiceId) -> $"Instance {instanceId} is marked fulfilled but Invoice {invoiceId} is not FullyPaid."
        | CashflowInstanceIdDoesntExist uuid -> $"Could not locate an Instance with the id of {uuid}."
        | CashflowInstanceIdListCannotBeEmpty -> "The instanceIds list must contain at least 1 ID."
        | CashflowInstanceNotUnderMasterAgreement(instanceId, agreementId) -> $"Instance {instanceId} does not belong to MasterAgreement {agreementId}."
        | CashflowInstanceUpdateNoOp -> "Updating the Instance record failed because at least one updatable parameter must be set."
        | CashflowInvalidBlocker str -> $"Invalid Blocker of \"{str}\"."
        | CashflowInvalidBlockerRow reason -> $"Invalid Blocker row: {reason}."
        | CashflowInvalidCadenceRow reason -> $"Invalid Cadence row: {reason}."
        | CashflowInvalidDateInMonthNumber i -> $"Invalid DateInMonthNumber of \"{i}\"."
        | CashflowInvalidFlowDirection str -> $"Invalid FlowDirection of \"{str}\"."
        | CashflowInvalidInvoiceState str -> $"Invalid InvoiceState of \"{str}\"."
        | CashflowInvalidMonth str -> $"Invalid Month of \"{str}\"."
        | CashflowInvalidPaymentAmountRow reason -> $"Invalid Payment amount row: {reason}."
        | CashflowInvalidPaymentState str -> $"Invalid PaymentState of \"{str}\"."
        | CashflowInvalidPaymentTransactionPointerRow reason -> $"Invalid Payment transactionPointer row: {reason}."
        | CashflowInvalidPostedState str -> $"Invalid PostedState of \"{str}\"."
        | CashflowInvalidWeekDay str -> $"Invalid WeekDay of \"{str}\"."
        | CashflowInvalidWeekInMonthNumber i -> $"Invalid WeekInMonthNumber of \"{i}\"."
        | CashflowInvoiceCompositeUpdateNoOp -> "Updating the Invoice composite failed because at least one updatable parameter must be set."
        | CashflowInvoiceDiamondMismatch(invoiceId, instanceAgreementId, paymentAgreementAgreementId) -> $"Invoice {invoiceId}'s Instance traces to MasterAgreement {instanceAgreementId} but its PaymentAgreement traces to MasterAgreement {paymentAgreementAgreementId}; both must trace to the same MasterAgreement."
        | CashflowInvoiceFullyPaidAmountMismatch(invoiceId, paidTotal, invoiceAmount) -> $"Invoice {invoiceId} is FullyPaid but its Payments sum to {paidTotal}, not its amount of {invoiceAmount}."
        | CashflowInvoiceFullyPaidWithBlocker invoiceId -> $"Invoice {invoiceId} cannot be FullyPaid while a Blocker is set."
        | CashflowInvoiceIdDoesntExist uuid -> $"Could not locate an Invoice with the id of {uuid}."
        | CashflowInvoiceIdListCannotBeEmpty -> "The invoiceIds list must contain at least 1 ID."
        | CashflowInvoiceMemoIsEmpty memo -> $"InvoiceMemo cannot be empty. Provided Memo is {memo}."
        | CashflowInvoiceMemoTooLong(memo, max) -> $"InvoiceMemo cannot exceed {max} characters. Provided Memo is {memo}."
        | CashflowInvoiceNonPositiveAmount(invoiceId, amount) -> $"Invoice {invoiceId} amount ({amount}) must be greater than 0."
        | CashflowInvoiceNotUnderInstance(invoiceId, instanceId) -> $"Invoice {invoiceId} does not belong to Instance {instanceId}."
        | CashflowInvoiceNotUnderMasterAgreement(invoiceId, agreementId) -> $"Invoice {invoiceId} does not belong to MasterAgreement {agreementId}."
        | CashflowInvoicePartiallyPaidWithNoPayments invoiceId -> $"Invoice {invoiceId} is PartiallyPaid but has no Payments."
        | CashflowInvoicePartiallyPostedWithNoPostedPayment invoiceId -> $"Invoice {invoiceId} is PartiallyPosted but none of its Payments are posted to a journal entry."
        | CashflowInvoicePostedToLedgerRequiresFullyPaid invoiceId -> $"Invoice {invoiceId} cannot be PostedToLedger unless its PaymentState is FullyPaid."
        | CashflowInvoicePostedToLedgerWithUnpostedPayment invoiceId -> $"Invoice {invoiceId} is PostedToLedger but at least one of its Payments is still staged, not posted to a journal entry."
        | CashflowInvoiceUpdateNoOp -> "Updating the Invoice record failed because at least one updatable parameter must be set."
        | CashflowMasterAgreementIdDoesntExist uuid -> $"Could not locate a MasterAgreement with the id of {uuid}."
        | CashflowMasterAgreementIdListCannotBeEmpty -> "The masterAgreementIds list must contain at least 1 ID."
        | CashflowMasterAgreementUnavailable(uuid, referenceDate, beginDate, endDate) ->
            let endDateStr = match endDate with
                                | Some x -> x.ToString()
                                | None -> "None"
            $"Master Agreement ({uuid}) is not available (begin {beginDate}; end {endDateStr}) as of {referenceDate}."
        | CashflowMasterAgreementUpdateNoOp -> "Updating the MasterAgreement record failed because at least one updatable parameter must be set."
        | CashflowPaymentAgreementCreditAccountInvalid uuid -> $"PaymentAgreement's credit account ({uuid}) does not match an Account in the database."
        | CashflowPaymentAgreementDebitAccountInvalid uuid -> $"PaymentAgreement's debit account ({uuid}) does not match an Account in the database."
        | CashflowPaymentAgreementIdDoesntExist uuid -> $"Could not locate a PaymentAgreement with the id of {uuid}."
        | CashflowPaymentAgreementMemoIsEmpty memo -> $"PaymentAgreementMemo cannot be empty. Provided Memo is {memo}."
        | CashflowPaymentAgreementMemoTooLong(memo, max) -> $"PaymentAgreementMemo cannot exceed {max} characters. Provided Memo is {memo}."
        | CashflowPaymentAgreementNonPositiveExpectedAmount(paymentAgreementId, amount) -> $"PaymentAgreement {paymentAgreementId} expected amount ({amount}) cannot be less than or equal to 0.00."
        | CashflowPaymentAgreementNotUnderMasterAgreement(paymentAgreementId, agreementId) -> $"PaymentAgreement {paymentAgreementId} does not belong to MasterAgreement {agreementId}."
        | CashflowPaymentAgreementUpdateNoOp -> "Updating the PaymentAgreement record failed because at least one updatable parameter must be set."
        | CashflowPaymentAgreementsListCannotBeEmpty -> "A MasterAgreement must have at least one PaymentAgreement."
        | CashflowPaymentMemoIsEmpty memo -> $"PaymentMemo cannot be empty. Provided Memo is {memo}."
        | CashflowPaymentMemoTooLong(memo, max) -> $"PaymentMemo cannot exceed {max} characters. Provided Memo is {memo}."
        | CashflowPaymentNotUnderInvoice(paymentId, invoiceId) -> $"Payment {paymentId} does not belong to Invoice {invoiceId}."
        | CashflowPaymentNotUnderMasterAgreement(paymentId, agreementId) -> $"Payment {paymentId} does not belong to MasterAgreement {agreementId}."
        | CashflowPaymentPostedToLedgerDateMismatch(paymentId, provided, actual) -> $"Payment {paymentId}'s postedToLedgerDate ({provided}) does not match its journal entry's entry date ({actual})."
        | CashflowPaymentPostedToLedgerDateWithoutJournalEntry paymentId -> $"Payment {paymentId} has a postedToLedgerDate set but its transactionPointer is not Posted to a journal entry."
        | CashflowPaymentUpdateNoOp -> "Updating the Payment record failed because at least one updatable parameter must be set."
        | CashflowProjectionHorizonInDaysBelowMin(raw, min) -> $"Failed to convert {raw} to a ProjectionHorizonInDays as value falls below the minimum allowable value of {min}."
        | CashflowProjectionHorizonInDaysExceededMax(raw, max) -> $"Failed to convert {raw} to a ProjectionHorizonInDays as value exceeds the maximum allowable value of {max}."
        
        | CliUnknownCommand(domain, verb) -> $"Unknown command: {domain} {verb}"
        
        | ConfigReadError (keyString, ex) -> $"Cannot resolve config with key {keyString}. It likely cannot be parsed as the requested type. Full error: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | ConfigNotFound keyString -> $"Cannot find config with key {keyString}."
        
        | DalCantCompleteTransactionOfNone -> "Error. You cannot commit or rollback with a raw transaction of None."
        | DalCantFetchTransactionOfNone -> "Error. You cannot fetch a connection with a raw transaction of None."
        | DalCantUseTransactionOfNoneInAutoCommit -> "Error. You cannot send a transaction of None into the auto-commit pipeline."
        | DalConnectionStringEnvVarContainsConnectionString -> "ConnectionStringEnvVar contains a connection string, not an env var name."
        | DalConnectionStringEnvVarNotFound -> "ConnectionStringEnvVar not found in appsettings.json."
        | DalConnectionStringIsEmpty -> "Connection string is empty."
        | DalDecimalUnboxingReturnedNull -> "Decimal unboxing returned DB null"
        | DalEnvVarNotSet envVarName -> $"Environment variable {envVarName} not set or empty."
        | DalErrorDuringAutoCompleteTransactionRun ex -> $"Database error during runWithAutoCompleteTransaction. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringDecimalOptionUnboxing ex -> $"Database error decimal option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringDecimalUnboxing ex -> $"Database error during decimal unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringInstantOptionUnboxing ex -> $"Database error during instant option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringInstantUnboxing ex -> $"Database error during instant unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringIntOptionUnboxing ex -> $"Database error during int option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringIntUnboxing ex -> $"Database error during int unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLocalDateOptionUnboxing ex -> $"Database error during LocalDate option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLocalDateUnboxing ex -> $"Database error during LocalDate unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLongOptionUnboxing ex -> $"Database error during long option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLongUnboxing ex -> $"Database error during long unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringNonQueryExecution ex -> $"Database error during non query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringReaderQueryExecution ex -> $"Database error during reader query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringScalarExecution ex -> $"Database error during scalar execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringStringOptionUnboxing ex -> $"Database error during string option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringStringUnboxing ex -> $"Database error during string unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringTransactionCommit ex -> $"Database error during transaction commit. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringTransactionCreation ex -> $"Database error during transaction creation: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringTransactionRollback ex -> $"Database error during transaction rollback. You probably have corrupted data that you should address immediately. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringUuidOptionUnboxing ex -> $"Database error during UUID option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringUuidUnboxing ex -> $"Database error during UUID unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalInstantUnboxingReturnedNull -> "Instant unboxing returned DB null"
        | DalIntUnboxingReturnedNull -> "Int unboxing returned DB null"
        | DalLocalDateUnboxingReturnedNull -> "LocalDate unboxing returned DB null"
        | DalLongUnboxingReturnedNull -> "Long unboxing returned DB null"
        | DalResultantRowsDidntMatchExpectation(expected, actual) -> $"Resultant rows didn't match expectation. Expected {expected}. Actual {actual}."
        | DalStringUnboxingReturnedNull -> "String unboxing returned DB null"
        | DalUuidUnboxingReturnedNull -> "UUID unboxing returned DB null"
    
        | FileIoDirectoryDoesntExist str -> $"Directory {str} doesn't exist."
        | FileIoFileDoesntExist str -> $"No file exists at path \"{str}\"."
        | FileIoError ex -> $"Error in File I/O operation. Error message: {ex.Message}{Environment.NewLine} {ex.StackTrace}"
        
        | FiscalPeriodInvalidKeyString key -> $"Passed string \"{key}\" is invalid as a Period Key."
        | FiscalPeriodNoPeriodMatchingId uuid -> $"No Fiscal Period matching the id {uuid} could be found in the database."
        | FiscalPeriodNoPeriodMatchingKey key -> $"No Fiscal Period matching the key {key} could be found in the database."
        | FiscalPeriodToggleOpenNoOp -> "Opening or closing this fiscal period would've had no result. Likely because it was already in the desired state."
        
        | IngestionBaseStageEntryGroupIdIsEmpty str -> $"BaseStageEntryGroupId cannot be empty. Provided value is {str}."
        | IngestionBaseStageEntryGroupIdTooLong (str, max) -> $"BaseStageEntryGroupId cannot exceed {max} characters. Provided value is {str}."
        | IngestionBaseStageGroupIdDistinctDataViolation str -> $"More than one combination of \"header\" data found for BaseStageEntryGroupId {str}"
        | IngestionClassificationRuleGroupsEmpty -> "A ClassificationRule's ClassificationRuleGroup list cannot be empty."
        | IngestionClassificationRuleNameIsEmpty str -> $"ClassificationRuleName cannot be empty. Provided value is {str}."
        | IngestionClassificationRuleNameTooLong (str, max) -> $"ClassificationRuleName cannot exceed {max} characters. Provided value is {str}."
        | IngestionClassificationRuleUpdateNoOp -> "Updating the ClassificationRule record failed because at least one updatable parameter must be set."
        | IngestionFieldMatchChainEmpty -> "A FieldMatchChain's chain cannot be empty."
        | IngestionInvalidClassificationGroupConnector str -> $"Invalid ClassificationConnector of \"{str}\"."
        | IngestionInvalidNumericSearchOperator str -> $"Invalid NumericSearchOperator of \"{str}\"."
        | IngestionInvalidStagedEntryStatus str -> $"Provided string of '{str}' is not a valid StagedEntryStatus."
        | IngestionInvalidStageStatusChangeMechanism str -> $"Provided string of '{str}' is not a valid StageStatusChangeMechanism."
        | IngestionInvalidStageStatusTransition (fromStr, toStr) -> $"Invalid stage status transition. Cannot move from {fromStr} to {toStr}."
        | IngestionSearchPatternIsEmpty str -> $"SearchPattern cannot be empty. Provided value is {str}."
        | IngestionSearchPatternTooLong (str, max) -> $"SearchPattern cannot exceed {max} characters. Provided value is {str}."
        | IngestionStageEntryHeaderIdDoesntExist uuid -> $"Could not locate a stage entry header with the id of {uuid}."
        | IngestionStageEntryHeaderNoOp -> "Updating the StageEntryHeader record failed because at least one updatable parameter must be set."
        | IngestionStageEntryLineNoOp -> "Updating the StageEntryLine record failed because at least one updatable parameter must be set."
        | IngestionStageHeaderIdListCannotBeEmpty -> "The stageEntryHeaderIds list must contain at least 1 Header ID."
        | IngestionStageLineNonPositiveAmount amount -> $"StageEntry Amount field ({amount}) cannot be less than or equal to 0.00."
        | IngestionStageEntryDebitCreditMismatch(debits, credits) -> $"Error in Base Stage Entry Group. The sum of all debit amounts ({debits}) must exactly equal the sum of all credit amounts ({credits})."
        | IngestionStageEntryInsufficientLines lineCount -> $"Insufficient number of lines ({lineCount}) for a stage entry. At least two are required."
        | IngestionSourceFileIsEmpty str -> $"Ingestion source file cannot be empty. Provided value is {str}."
        | IngestionSourceFileTooLong (str, max) -> $"Ingestion source file cannot exceed {max} characters. Provided value is {str}."
        | IngestionStatusTransitionList -> "StageEntryStatusTransition list cannot be empty."
        | IngestionUpdateStageEntryLinesMustMatchHeader (headerId, lineId) -> $"Error updating StageEntry {headerId}. Line {lineId} is for a different header."
        | IngestionNoneAccount uuid -> $"Stage Entry Line with an account of None is not allowed at this phase of the ingestion pipeline. Line ID: {uuid}"
        | IngestionUpdateStageEntryNoOp -> "updateStageEntry failed because at least one updatable parameter must be set."
        | IngestionSourceNameNotFound str -> $"No ingestion source of {str} could be found."
        
        | InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError) -> $"Failed conversion in InterfaceBridge. Original type: {originalType}. Desired type: {desiredType}. Original value: {originalValue}. Additional details: {childError}"
        | InterfaceBridgeFailedJsonDeserialization(typeName, error, stackTrace) -> $"Failed to deserialize JSON string into type {typeName}. {error}{Environment.NewLine}{stackTrace}"
        | InterfaceBridgeFailedJsonSerialization(typeName, error, stackTrace) -> $"Failed to serialize JSON string into type {typeName}. {error}{Environment.NewLine}{stackTrace}"
        
        | JournalEntryCommentIsEmpty comment -> $"Journal Entry Comment cannot be empty. Provided string is {comment}."
        | JournalEntryCommentPrimaryAndSecondaryIdsAreSame(primary, secondary) -> $"Primary ({primary}) and secondary ({secondary}) journal entries cannot be the same."
        | JournalEntryCommentPrimaryJeHeaderIdNotFound uuid -> $"Error looking up primary header ID. Could not locate a journal entry header with the id of {uuid}."
        | JournalEntryCommentSecondaryJeHeaderIdNotFound uuid -> $"Error looking up secondary header ID. Could not locate a journal entry header with the id of {uuid}."
        | JournalEntryCommentTooLong(comment, max) -> $"Journal Entry Comment cannot exceed {max} characters. Provided string is {comment}."
        | JournalEntryCommentUpdateNoOp -> "Updating the Journal Entry Comment record failed because at least one updatable parameter must be set."
        | JournalEntryDateNotInFiscalPeriod entryDate -> $"Entry date {entryDate} is not associated to any recorded Fiscal Periods in the database."
        | JournalEntryDebitCreditMismatch(debits, credits) -> $"The sum of all debit line amounts ({debits}) must exactly equal the sum of all credit line amounts ({credits})."
        | JournalEntryDescriptionIsEmpty description -> $"Journal Entry Description cannot be empty. Provided string is {description}."
        | JournalEntryDescriptionTooLong (description, max) -> $"Journal Entry Description cannot exceed {max} characters. Provided string is {description}."
        | JournalEntryExternalReferenceIsEmpty externalReference -> $"Journal Entry ExternalReference cannot be empty. Provided string is {externalReference}."
        | JournalEntryExternalReferenceTooLong(externalReference, max) -> $"Journal Entry ExternalReference cannot exceed {max} characters. Provided string is {externalReference}."
        | JournalEntryFetchByDateRangeBeginAfterEnd (beginDate, endDate) -> $"Journal Entry Fetch By Date Range failed because begin date ({beginDate}) cannot be after end date ({endDate})."
        | JournalEntryFetchByReferenceBothArgumentsNull -> "FI and reference cannot both be null when fetching by reference"
        | JournalEntryHeaderEntryDateInvalid entryDate -> $"Entry date of {entryDate} is not associated to an open Fiscal Period."
        | JournalEntryHeaderIdDoesntExist uuid -> $"Could not locate a journal entry header with the id of {uuid}."
        | JournalEntryHeaderIdListCannotBeEmpty -> "The journalEntryHeaderIds list must contain at least 1 Header ID."
        | JournalEntryInsufficientLines lineCount -> $"Insufficient number of lines ({lineCount}) for a journal entry. At least two are required."
        | JournalEntryLineAccountDoesntExist uuid -> $"Account fetch on {uuid} returned zero rows while creating Journal Entry Line."
        | JournalEntryLineAccountInactive(uuid, entryDate, beginDate, endDate) ->
            let endDateStr = match endDate with
                                | Some x -> x.ToString()
                                | None -> "None"
            $"Account ({uuid}) is not active (begin {beginDate}; end {endDateStr}) relative to the Journal Entry's entry date ({entryDate})." 
        | JournalEntryLineMemoIsEmpty lineMemo -> $"Journal Entry Line Memo cannot be empty. Provided string is {lineMemo}."
        | JournalEntryLineMemoTooLong(lineMemo, max) -> $"Journal Entry LineMemo cannot exceed {max} characters. Provided string is {lineMemo}."
        | JournalEntryLineNonPositiveAmount amount -> $"Journal Entry Line Amount field ({amount}) cannot be less than or equal to 0.00."
        | JournalEntryLineTypeInvalid s -> $"Invalid JournalEntryLineType of {s}"
        | JournalEntryReferenceTextIsEmpty referenceText -> $"Journal Entry ReferenceText cannot be empty. Provided string is {referenceText}."
        | JournalEntryReferenceTextTooLong(referenceText, max) -> $"Journal Entry ReferenceText cannot exceed {max} characters. Provided string is {referenceText}."
        | JournalEntryReferenceUpdateNoOp -> "Updating the Journal Entry Reference record failed because at least one updatable parameter must be set."
        | JournalEntrySourceIsEmpty source -> $"Journal Entry Source cannot be empty. Provided string is {source}."
        | JournalEntrySourceTooLong(source, max) -> $"Journal Entry Source cannot exceed {max} characters. Provided string is {source}."
        | JournalEntryVoidingCannotFetchFiscalPeriod(entryDate, fiscalPeriodId) -> $"Could not fetch a FiscalPeriod row from the database for the fiscal period ID of {fiscalPeriodId}, which was fetched using the entry date of {entryDate}."
        | JournalEntryVoidingFiscalPeriodIsClosed(entryDate, fiscalPeriodId) -> $"Can not void a Journal Entry whose FiscalPeriod is already closed. FiscalPeriodId of {fiscalPeriodId}, which was fetched using the entry date of {entryDate}."
        | JournalEntryVoidingNoOp uuid -> $"Attempting to void Journal Entry ({uuid}) resulted in zero rows updated. Either the UUID is wrong or the entry is already voided."
        | JournalRefFinancialInstitutionIsEmpty fi -> $"Journal Entry External Reference's Financial Institution cannot be empty. Provided string is {fi}."
        | JournalRefFinancialInstitutionTooLong (fi, max) -> $"Journal Entry External Reference's Financial Institution cannot exceed {max} characters. Provided string is {fi}."
        
        | MoneyFailedToConvertBelowMin(raw, min) -> $"Failed to convert {raw} to Money record as value falls below the minimum allowable value of {min}."
        | MoneyFailedToConvertExceededMax(raw, max) -> $"Failed to convert {raw} to Money record as value exceeds the maximum allowable value of {max}."
        | MoneyFailedToConvertImproperPrecision raw -> $"Failed to convert {raw} to Money record due to improper decimal precision."
        | MoneyImproperSplit n -> $"Improper Money split of {n}. Money can only be split by a positive integer, greater than 1."
        | MoneySplitFailedReconciliation(originalAmount, sumTotal) -> $"Sum of all shares {sumTotal} does not match original amount {originalAmount}."
        
        | ReportingUnknownReportName name -> $"Unknown report: {name}."

