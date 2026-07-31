module Utilities.AppError

open System
open NodaTime

type AppError =

    | TestingError of string // this is NEVER to be used in the Src directory. It is only here to facilitate automated testing. Such as when I need to assert that somewthing was supposed to fail but it doesn't.

    | DalConnectionStringEnvVarNotFound
    | DalErrorRetrievingAppSettings of exn
    | DalConnectionStringEnvVarContainsConnectionString
    | DalEnvVarNotSet of string
    | DalConnectionStringIsEmpty
    | DalErrorDuringTransactionCreation of exn
    | DalCantUseTransactionOfNoneInAutoCommit
    | DalCantCompleteTransactionOfNone
    | DalCantFetchTransactionOfNone
    | DalErrorDuringAutoCompleteTransactionRun of exn
    | DalErrorDuringTransactionCommit of exn
    | DalErrorDuringTransactionRollback of exn
    | DalResultantRowsDidntMatchExpectation of string * int
    | DalErrorDuringNonQueryExecution of exn
    | DalErrorDuringReaderQueryExecution of exn
    | DalErrorDuringScalarExecution of exn
    | DalStringUnboxingReturnedNull
    | DalIntUnboxingReturnedNull
    | DalLongUnboxingReturnedNull
    | DalDecimalUnboxingReturnedNull
    | DalLocalDateUnboxingReturnedNull
    | DalInstantUnboxingReturnedNull
    | DalUuidUnboxingReturnedNull
    | DalErrorDuringStringUnboxing of exn
    | DalErrorDuringStringOptionUnboxing of exn
    | DalErrorDuringIntUnboxing of exn
    | DalErrorDuringIntOptionUnboxing of exn
    | DalErrorDuringLongUnboxing of exn
    | DalErrorDuringLongOptionUnboxing of exn
    | DalErrorDuringDecimalUnboxing of exn
    | DalErrorDuringDecimalOptionUnboxing of exn
    | DalErrorDuringLocalDateUnboxing of exn
    | DalErrorDuringLocalDateOptionUnboxing of exn
    | DalErrorDuringInstantUnboxing of exn
    | DalErrorDuringInstantOptionUnboxing of exn
    | DalErrorDuringUuidUnboxing of exn
    | DalErrorDuringUuidOptionUnboxing of exn

    | AccountActiveEndBeforeBegin of LocalDate * LocalDate option
    | AccountInvalidTypeSubtypeCombo of string * string option
    | AccountParentAndChildAreSame of Guid option * Guid
    | AccountCodeIsEmpty of string
    | AccountCodeTooLong of string * int
    | AccountCodeDoesntMatchAccountId of string
    | AccountNameIsEmpty of string
    | AccountNameTooLong of string * int
    | AccountTypeInvalid of string
    | AccountSubtypeInvalid of string
    | AccountExternalReferenceIsEmpty of string
    | AccountExternalReferenceTooLong of string * int
    | AccountParentIsInactive of Guid
    | AccountParentAndChildTypesDontMatch of string * string
    | AccountUpdateNoOp
    | AccountBalanceFetchInvalidArguments
    | AccountDeactivationProposedDateIsInvalid of Guid * LocalDate * LocalDate
    | AccountActiveChildrenBeforeDeactivation of Guid
    | AccountNonZeroBalanceBeforeDeactivation of Guid * decimal * decimal
    | AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate of Guid
    | AccountDeactivationFailedJournalEntryValidation
    | AccountAlreadyInactive of Guid * LocalDate
    | AccountParentCodeInvalid of string

    | FiscalPeriodInvalidKeyString of string
    | FiscalPeriodNoPeriodMatchingKey of string

    | JournalEntryCommentTooLong of string * int
    | JournalEntryCommentIsEmpty of string
    | JournalEntryCommentPrimaryAndSecondaryIdsAreSame of Guid * Guid
    | JournalEntryExternalReferenceTooLong of string * int
    | JournalEntryExternalReferenceIsEmpty of string
    | JournalEntryReferenceTextTooLong of string * int
    | JournalEntryReferenceTextIsEmpty of string
    | JournalEntryDescriptionTooLong of string * int
    | JournalEntryDescriptionIsEmpty of string
    | JournalEntrySourceTooLong of string * int
    | JournalEntrySourceIsEmpty of string
    | JournalEntryDateNotInFiscalPeriod of LocalDate
    | JournalEntryLineTypeInvalid of string
    | JournalEntryLineMemoTooLong of string * int
    | JournalEntryLineMemoIsEmpty of string
    | JournalEntryReferenceUpdateNoOp
    | JournalEntryLineNonPositiveAmount of decimal
    | JournalEntryLineAccountDoesntExist of Guid
    | JournalEntryHeaderEntryDateInvalid of LocalDate
    | JournalEntryDebitCreditMismatch of decimal * decimal
    | JournalEntryInsufficientLines of int
    | JournalEntryLineAccountInactive of Guid * LocalDate * LocalDate * LocalDate Option
    | JournalEntryFetchByReferenceBothArgumentsNull
    | JournalEntryVoidingCannotFetchFiscalPeriod of LocalDate * Guid
    | JournalEntryVoidingFiscalPeriodIsClosed of LocalDate * Guid
    | JournalEntryVoidingNoOp of Guid

    | MoneyFailedToConvertImproperPrecision of decimal
    | MoneyFailedToConvertExceededMax of decimal * decimal
    | MoneyFailedToConvertBelowMin of decimal * decimal
    | MoneyImproperSplit of int
    | MoneySplitFailedReconciliation of decimal * decimal

    | InterfaceBridgeFailedJsonDeserialization of string * string * string
    | InterfaceBridgeFailedJsonSerialization of string * string * string
    | InterfaceBridgeConversionFailure of string * string * string * string

    | CliUnknownCommand of string * string

module AppError =
    let toMessage =
        function

        | TestingError message -> message

        | DalConnectionStringEnvVarNotFound -> "ConnectionStringEnvVar not found in appsettings.json."
        | DalErrorRetrievingAppSettings ex ->
            $"Error retrieving appsettings.json. Error message: {ex.Message}{Environment.NewLine} {ex.StackTrace}"
        | DalConnectionStringEnvVarContainsConnectionString ->
            "ConnectionStringEnvVar contains a connection string, not an env var name."
        | DalEnvVarNotSet envVarName -> $"Environment variable {envVarName} not set or empty."
        | DalConnectionStringIsEmpty -> "Connection string is empty."
        | DalErrorDuringTransactionCreation ex ->
            $"Database error during transaction creation: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalCantUseTransactionOfNoneInAutoCommit ->
            "Error. You cannot send a transaction of None into the auto-commit pipeline."
        | DalCantCompleteTransactionOfNone -> "Error. You cannot commit or rollback with a raw transaction of None."
        | DalCantFetchTransactionOfNone -> "Error. You cannot fetch a connection with a raw transaction of None."
        | DalErrorDuringAutoCompleteTransactionRun ex -> $"Database error during runWithAutoCompleteTransaction. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringTransactionCommit ex ->
            $"Database error during transaction commit. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringTransactionRollback ex ->
            $"Database error during transaction rollback. You probably have corrupted data that you should address immediately. {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalResultantRowsDidntMatchExpectation(expected, actual) ->
            $"Resultant rows didn't match expectation. Expected {expected}. Actual {actual}."
        | DalErrorDuringNonQueryExecution ex ->
            $"Database error during non query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringReaderQueryExecution ex ->
            $"Database error during reader query execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringScalarExecution ex ->
            $"Database error during scalar execution: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalStringUnboxingReturnedNull -> "String unboxing returned DB null"
        | DalIntUnboxingReturnedNull -> "Int unboxing returned DB null"
        | DalLongUnboxingReturnedNull -> "Long unboxing returned DB null"
        | DalDecimalUnboxingReturnedNull -> "Decimal unboxing returned DB null"
        | DalLocalDateUnboxingReturnedNull -> "LocalDate unboxing returned DB null"
        | DalInstantUnboxingReturnedNull -> "Instant unboxing returned DB null"
        | DalUuidUnboxingReturnedNull -> "UUID unboxing returned DB null"
        | DalErrorDuringStringUnboxing ex ->
            $"Database error during string unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringStringOptionUnboxing ex ->
            $"Database error during string option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringIntUnboxing ex ->
            $"Database error during int unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringIntOptionUnboxing ex ->
            $"Database error during int option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLongUnboxing ex ->
            $"Database error during long unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLongOptionUnboxing ex ->
            $"Database error during long option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringDecimalUnboxing ex ->
            $"Database error during decimal unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringDecimalOptionUnboxing ex ->
            $"Database error decimal option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLocalDateUnboxing ex ->
            $"Database error during LocalDate unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringLocalDateOptionUnboxing ex ->
            $"Database error during LocalDate option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringInstantUnboxing ex ->
            $"Database error during instant unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringInstantOptionUnboxing ex ->
            $"Database error during instant option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringUuidUnboxing ex ->
            $"Database error during UUID unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"
        | DalErrorDuringUuidOptionUnboxing ex ->
            $"Database error during UUID option unboxing: {ex.Message}{Environment.NewLine}{ex.StackTrace}"

        | AccountActiveEndBeforeBegin(activeBegin, activeEnd) ->
            $"Active end ({activeEnd}) cannot be before active begin ({activeBegin})"
        | AccountInvalidTypeSubtypeCombo(accountType, subtype) ->
            $"Invalid AccountType / AccountSubType combo: {accountType} / {subtype}"
        | AccountParentAndChildAreSame(parent, child) ->
            $"A child account ({child}) cannot be its own parent ({parent})."
        | AccountCodeIsEmpty code -> $"Account code cannot be empty. Provided code is {code}."
        | AccountCodeTooLong(code, max) -> $"Account code cannot exceed {max} characters. Provided code is {code}."
        | AccountCodeDoesntMatchAccountId code -> $"Account code of {code} doesn't match an Account ID in the database."
        | AccountNameIsEmpty name -> $"Account name cannot be empty. Provided name is {name}."
        | AccountNameTooLong(name, max) -> $"Account name cannot exceed {max} characters. Provided name is {name}."
        | AccountTypeInvalid typeString -> $"Provided string of '{typeString}' is not a valid account type."
        | AccountSubtypeInvalid subtype -> $"Provided string of '{subtype}' is not a valid account subtype."
        | AccountExternalReferenceIsEmpty externalReference ->
            $"Account external reference cannot be empty. Provided external reference is {externalReference}."
        | AccountExternalReferenceTooLong(externalReference, max) ->
            $"Account external reference cannot exceed {max} characters. Provided external reference is {externalReference}."
        | AccountParentIsInactive uuid -> $"Parent account {uuid} failed \"is active\" check."
        | AccountParentAndChildTypesDontMatch(parent, child) ->
            $"Parent ({parent}) and child ({child}) account types do not match."
        | AccountUpdateNoOp ->
            "Updating the account record failed because at least one updatable parameter must be set."
        | AccountBalanceFetchInvalidArguments -> "fetchByAccountIdList requires at least one account ID"
        | AccountDeactivationProposedDateIsInvalid(uuid, proposedDate, beginDate) ->
            $"Deactivating account {uuid} failed because the active end ({proposedDate}) would be before the active begin ({beginDate})"
        | AccountActiveChildrenBeforeDeactivation uuid ->
            $"Account {uuid} deactivation failed because one or more child account records is active."
        | AccountNonZeroBalanceBeforeDeactivation(uuid, debits, credits) ->
            $"The Account {uuid} cannot be deactivated as it has a non-zero balance. Total debits: {debits}. Total credits: {credits}."
        | AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate uuid ->
            $"Account {uuid} cannot be deactivated as it has one or more Journal Entries dated after the deactivation date."
        | AccountDeactivationFailedJournalEntryValidation ->
            "Failed to validate the Account's Journal Entries prior to deactivation"
        | AccountAlreadyInactive(uuid, endDate) ->
            $"Account {uuid} deactivation failed because active end is already set to {endDate}."
        | AccountParentCodeInvalid code -> $"Provided parent code ({code}) doesn't match an ID in the database."

        | FiscalPeriodInvalidKeyString key -> $"Passed string \"{key}\" is invalid as a Period Key."
        | FiscalPeriodNoPeriodMatchingKey key -> $"No Fiscal Period matching {key} could be found in the database."

        | JournalEntryCommentTooLong(comment, max) ->
            $"Journal Entry Comment cannot exceed {max} characters. Provided string is {comment}."
        | JournalEntryCommentIsEmpty comment -> $"Journal Entry Comment cannot be empty. Provided string is {comment}."
        | JournalEntryCommentPrimaryAndSecondaryIdsAreSame(primary, secondary) ->
            $"Primary ({primary}) and secondary ({secondary}) journal entries cannot be the same."
        | JournalEntryExternalReferenceTooLong(externalReference, max) ->
            $"Journal Entry ExternalReference cannot exceed {max} characters. Provided string is {externalReference}."
        | JournalEntryExternalReferenceIsEmpty externalReference ->
            $"Journal Entry ExternalReference cannot be empty. Provided string is {externalReference}."
        | JournalEntryReferenceTextTooLong(referenceText, max) ->
            $"Journal Entry ReferenceText cannot exceed {max} characters. Provided string is {referenceText}."
        | JournalEntryReferenceTextIsEmpty referenceText ->
            $"Journal Entry ReferenceText cannot be empty. Provided string is {referenceText}."
        | JournalEntryDescriptionTooLong(description, max) ->
            $"Journal Entry Description cannot exceed {max} characters. Provided string is {description}."
        | JournalEntryDescriptionIsEmpty description ->
            $"Journal Entry Description cannot be empty. Provided string is {description}."
        | JournalEntrySourceTooLong(source, max) ->
            $"Journal Entry Source cannot exceed {max} characters. Provided string is {source}."
        | JournalEntrySourceIsEmpty source -> $"Journal Entry Source cannot be empty. Provided string is {source}."
        | JournalEntryDateNotInFiscalPeriod entryDate ->
            $"Entry date {entryDate} is not associated to any recorded Fiscal Periods in the database."
        | JournalEntryLineTypeInvalid s -> $"Invalid JournalEntryLineType of {s}"
        | JournalEntryLineMemoTooLong(lineMemo, max) ->
            $"Journal Entry LineMemo cannot exceed {max} characters. Provided string is {lineMemo}."
        | JournalEntryLineMemoIsEmpty lineMemo ->
            $"Journal Entry Line Memo cannot be empty. Provided string is {lineMemo}."
        | JournalEntryReferenceUpdateNoOp ->
            "Updating the Journal Entry Reference record failed because at least one updatable parameter must be set."
        | JournalEntryLineNonPositiveAmount amount ->
            $"Journal Entry Line Amount field ({amount}) cannot be less than or equal to 0.00."
        | JournalEntryLineAccountDoesntExist uuid ->
            $"Account fetch on {uuid} returned zero rows while creating Journal Entry Line."
        | JournalEntryHeaderEntryDateInvalid entryDate ->
            $"Entry date of {entryDate} is not associated to an open Fiscal Period."
        | JournalEntryDebitCreditMismatch(debits, credits) ->
            $"The sum of all debit line amounts ({debits}) must exactly equal the sum of all credit line amounts ({credits})."
        | JournalEntryInsufficientLines lineCount ->
            $"Insufficient number of lines ({lineCount}) for a journal entry. At least two are required."
        | JournalEntryLineAccountInactive(uuid, entryDate, beginDate, endDate) ->
            let endDateStr =
                match endDate with
                | Some x -> x.ToString()
                | None -> "None"
            $"Account ({uuid}) is not active (begin {beginDate}; end {endDateStr}) relative to the Journal Entry's entry date ({entryDate})."
        | JournalEntryFetchByReferenceBothArgumentsNull ->
            "FI and reference cannot both be null when fetching by reference"
        | JournalEntryVoidingCannotFetchFiscalPeriod(entryDate, fiscalPeriodId) ->
            $"Could not fetch a FiscalPeriod row from the database for the fiscal period ID of {fiscalPeriodId}, which was fetched using the entry date of {entryDate}."
        | JournalEntryVoidingFiscalPeriodIsClosed(entryDate, fiscalPeriodId) ->
            $"Can not void a Journal Entry whose FiscalPeriod is already closed. FiscalPeriodId of {fiscalPeriodId}, which was fetched using the entry date of {entryDate}."
        | JournalEntryVoidingNoOp uuid ->
            $"Attempting to void Journal Entry ({uuid}) resulted in zero rows updated. Either the UUID is wrong or the entry is already voided."

        | MoneyFailedToConvertImproperPrecision raw ->
            $"Failed to convert {raw} to Money record due to improper decimal precision."
        | MoneyFailedToConvertExceededMax(raw, max) ->
            $"Failed to convert {raw} to Money record as value exceeds the maximum allowable value of {max}."
        | MoneyFailedToConvertBelowMin(raw, min) ->
            $"Failed to convert {raw} to Money record as value falls below the minimum allowable value of {min}."
        | MoneyImproperSplit n ->
            $"Improper Money split of {n}. Money can only be split by a positive integer, greater than 1."
        | MoneySplitFailedReconciliation(originalAmount, sumTotal) ->
            $"Sum of all shares {sumTotal} does not match original amount {originalAmount}."

        | InterfaceBridgeFailedJsonDeserialization(typeName, error, stackTrace) ->
            $"Failed to deserialize JSON string into type {typeName}. {error}{Environment.NewLine}{stackTrace}"
        | InterfaceBridgeFailedJsonSerialization(typeName, error, stackTrace) ->
            $"Failed to serialize JSON string into type {typeName}. {error}{Environment.NewLine}{stackTrace}"
        | InterfaceBridgeConversionFailure(originalType, originalValue, desiredType, childError) ->
            $"Failed conversion in InterfaceBridge. Original type: {originalType}. Desired type: {desiredType}. Original value: {originalValue}. Additional details: {childError}"

        | CliUnknownCommand(domain, verb) -> $"Unknown command: {domain} {verb}"
