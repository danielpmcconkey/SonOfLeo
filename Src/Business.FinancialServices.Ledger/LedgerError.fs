module Business.FinancialServices.Ledger.LedgerError

open System
open NodaTime
open App.Utility.IAppError

type LedgerError =
    | AccountActiveChildrenBeforeDeactivation of Guid
    | AccountActiveEndBeforeBegin of LocalDate * LocalDate option
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
    | FiscalPeriodInvalidKeyString of string
    | FiscalPeriodNoPeriodMatchingId of Guid
    | FiscalPeriodNoPeriodMatchingKey of string
    | FiscalPeriodToggleOpenNoOp
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
    | JournalEntryLineIdDoesntExist of Guid
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
    
    interface IAppError with
        member this.DomainName = nameof LedgerError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | AccountActiveChildrenBeforeDeactivation uuid -> $"Account {uuid} deactivation failed because one or more child account records is active."
            | AccountActiveEndBeforeBegin(activeBegin, activeEnd) -> $"An account's active end ({activeEnd}) cannot be before its active begin ({activeBegin})."
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
            | FiscalPeriodInvalidKeyString key -> $"Passed string \"{key}\" is invalid as a Period Key."
            | FiscalPeriodNoPeriodMatchingId uuid -> $"No Fiscal Period matching the id {uuid} could be found in the database."
            | FiscalPeriodNoPeriodMatchingKey key -> $"No Fiscal Period matching the key {key} could be found in the database."
            | FiscalPeriodToggleOpenNoOp -> "Opening or closing this fiscal period would've had no result. Likely because it was already in the desired state."
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
            | JournalEntryLineIdDoesntExist uuid -> $"Could not locate a journal entry line with the id of {uuid}."
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

let toMessage (e: LedgerError) = (e :> IAppError).ToMessage()
let toAppError (e: LedgerError) : IAppError = e :> IAppError
let error (e: LedgerError) : Result<'T, IAppError> = Error (e :> IAppError)



