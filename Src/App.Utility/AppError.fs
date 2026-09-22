module App.Utility.AppError

open System
open NodaTime

type AppError = // todo: turn AppError into an interface and remove upper level domain knowledge
    
    /// TestingError is NEVER to be used in the Src directory. It is only here to facilitate automated testing. Such as
    /// when I need to assert that somewthing was supposed to fail but it doesn't.
    | TestingError of string
    

    

    | CashflowAgreementMemoIsEmpty of string
    | CashflowAgreementMemoTooLong of string * int
    | CashflowAgreementNameDoesntMatchId of string
    | CashflowAgreementNameIsEmpty of string
    | CashflowAgreementNameTooLong of string * int
    | CashflowAgreementUpdateNoOp
    | CashflowBlockerNoteIsEmpty of string
    | CashflowBlockerNoteTooLong of string * int
    | CashflowCounterpartyIsEmpty of string
    | CashflowCounterpartyTooLong of string * int
    | CashflowDaysDueAfterInvoiceDateBelowMin of int * int
    | CashflowDaysDueAfterInvoiceDateExceededMax of int * int
    | CashflowExternalInvoiceIdIsEmpty of string
    | CashflowExternalInvoiceIdTooLong of string * int
    | CashflowInstanceDateNotAfterLatestInstance of Guid * LocalDate * LocalDate
    | CashflowInstanceCompositeDerivedFieldSet of string
    | CashflowInstanceCompositeUpdateNoOp
    | CashflowInstanceFulfilledWithNoInvoices of Guid
    | CashflowInstanceFulfilledWithUnpaidInvoice of Guid * Guid
    | CashflowInstanceIdListCannotBeEmpty
    | CashflowInstanceManyInvoicesForPaymentAgreement of Guid * Guid * int
    | CashflowInstanceNotUnderMasterAgreement of Guid * Guid
    | CashflowInstanceUpdateNoOp
    | CashflowInvalidBlocker of string
    | CashflowInvalidBlockerRow of string
    | CashflowInvalidFlowDirection of string
    | CashflowInvalidInvoiceState of string
    | CashflowInvalidPaymentAmountRow of string
    | CashflowInvalidPaymentState of string
    | CashflowInvalidPaymentTransactionPointerRow of string
    | CashflowInvalidPostedState of string
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
    | CashflowPaymentAgreementIdListCannotBeEmpty
    | CashflowPaymentAgreementLinkLineAlreadyLinked of Guid * Guid
    | CashflowPaymentAgreementLinkNoInvoiceToMatch of Guid * Guid
    | CashflowPaymentAgreementLinkUpdateNoOp
    | CashflowPaymentAgreementMemoIsEmpty of string
    | CashflowPaymentAgreementMemoTooLong of string * int
    | CashflowPaymentAgreementNameDoesntMatchId of string
    | CashflowPaymentAgreementNameIsEmpty of string
    | CashflowPaymentAgreementNameTooLong of string * int
    | CashflowPaymentAgreementNonPositiveExpectedAmount of Guid * decimal
    | CashflowPaymentAgreementNotUnderMasterAgreement of Guid * Guid
    | CashflowPaymentAgreementUpdateNoOp
    | CashflowPaymentAgreementsListCannotBeEmpty
    | CashflowPaymentIdDoesntExist of Guid
    | CashflowPaymentLineNotOnAgreementAccount of Guid * Guid option * Guid
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
    
    
    
    
    
    | IngestionBaseStageEntryGroupIdIsEmpty of string
    | IngestionBaseStageEntryGroupIdTooLong of string * int
    | IngestionBaseStageGroupIdDistinctDataViolation of string
    | IngestionClassificationRuleGroupsEmpty
    | IngestionClassificationRuleIdDoesntExist of Guid
    | IngestionClassificationRuleIdListCannotBeEmpty
    | IngestionClassificationRuleInvalidClaimant of Guid * Guid option * Guid option
    | IngestionClassificationRuleNameIsEmpty of string
    | IngestionClassificationRuleNameTooLong of string * int 
    | IngestionClassificationRuleUpdateNoOp
    | IngestionFieldMatchChainEmpty
    | IngestionInvalidClassificationClaimantType of string
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
    | IngestionStageEntryLineIdDoesntExist of Guid
    | IngestionStageEntryLineIdListCannotBeEmpty
    | IngestionStageEntryHeaderNoOp
    | IngestionStageEntryLineNoOp
    | IngestionStageEntryLineNoMatchingJournalEntryLine of Guid
    | IngestionStageHeaderIdListCannotBeEmpty
    | IngestionSourceFileIsEmpty of string
    | IngestionSourceFileTooLong of string * int
    | IngestionStatusTransitionList
    | IngestionUpdateStageEntryLinesMustMatchHeader of Guid * Guid
    | IngestionNoneAccount of Guid
    | IngestionUpdateStageEntryNoOp
    | IngestionSourceNameNotFound of string
    
    | InterfaceBridgeConversionFailure of string * string * string * string
    
    
    
    | ReportingUnknownReportName of string

module AppError =
    let toMessage =
        function

            | TestingError message -> message
            
            
            | CashflowAgreementMemoIsEmpty memo -> $"AgreementMemo cannot be empty. Provided Memo is {memo}."
            | CashflowAgreementMemoTooLong(memo, max) -> $"AgreementMemo cannot exceed {max} characters. Provided Memo is {memo}."
            | CashflowAgreementNameDoesntMatchId name -> $"AgreementName of {name} doesn't match a MasterAgreement ID in the database."
            | CashflowAgreementNameIsEmpty name -> $"AgreementName cannot be empty. Provided name is {name}."
            | CashflowAgreementNameTooLong(name, max) -> $"AgreementName cannot exceed {max} characters. Provided name is {name}."
            | CashflowAgreementUpdateNoOp -> "Updating the Agreement composite failed because at least one updatable parameter must be set."
            | CashflowBlockerNoteIsEmpty note -> $"AgreementMemo cannot be empty. Provided Memo is {note}."
            | CashflowBlockerNoteTooLong(note, max) -> $"BlockerNote cannot exceed {max} characters. Provided Memo is {note}."
            | CashflowCounterpartyIsEmpty name -> $"Counterparty cannot be empty. Provided name is {name}."
            | CashflowCounterpartyTooLong(name, max) -> $"Counterparty cannot exceed {max} characters. Provided name is {name}."
            | CashflowDaysDueAfterInvoiceDateBelowMin(raw, min) -> $"Failed to convert {raw} to a DaysDueAfterInvoiceDate as value falls below the minimum allowable value of {min}."
            | CashflowDaysDueAfterInvoiceDateExceededMax(raw, max) -> $"Failed to convert {raw} to a DaysDueAfterInvoiceDate as value exceeds the maximum allowable value of {max}."
            | CashflowExternalInvoiceIdIsEmpty eid -> $"ExternalInvoiceId cannot be empty. Provided ExternalInvoiceId is {eid}."
            | CashflowExternalInvoiceIdTooLong(eid, max) -> $"ExternalInvoiceId cannot exceed {max} characters. Provided ExternalInvoiceId is {eid}."
            | CashflowInstanceDateNotAfterLatestInstance(agreementId, attemptedDate, latestDate) -> $"An Instance for MasterAgreement {agreementId} cannot be created at {attemptedDate} because its latest existing Instance is dated {latestDate}. Instances are only ever created forward; correct the cadence instead."
            | CashflowInstanceCompositeDerivedFieldSet fieldName -> $"{fieldName} is derived from the Instance's Invoices and Payments and cannot be set directly."
            | CashflowInstanceCompositeUpdateNoOp -> "Updating the Instance composite failed because at least one updatable parameter must be set."
            | CashflowInstanceFulfilledWithNoInvoices instanceId -> $"Instance {instanceId} is marked fulfilled but has no Invoices."
            | CashflowInstanceFulfilledWithUnpaidInvoice(instanceId, invoiceId) -> $"Instance {instanceId} is marked fulfilled but Invoice {invoiceId} is not FullyPaid."
            | CashflowInstanceIdListCannotBeEmpty -> "The instanceIds list must contain at least 1 ID."
            | CashflowInstanceManyInvoicesForPaymentAgreement(instanceId, paymentAgreementId, count) -> $"Instance {instanceId} has {count} Invoices for PaymentAgreement {paymentAgreementId}. There may be at most one Invoice per leg per period."
            | CashflowInstanceNotUnderMasterAgreement(instanceId, agreementId) -> $"Instance {instanceId} does not belong to MasterAgreement {agreementId}."
            | CashflowInstanceUpdateNoOp -> "Updating the Instance record failed because at least one updatable parameter must be set."
            | CashflowInvalidBlocker str -> $"Invalid Blocker of \"{str}\"."
            | CashflowInvalidBlockerRow reason -> $"Invalid Blocker row: {reason}."
            | CashflowInvalidFlowDirection str -> $"Invalid FlowDirection of \"{str}\"."
            | CashflowInvalidInvoiceState str -> $"Invalid InvoiceState of \"{str}\"."
            | CashflowInvalidPaymentAmountRow reason -> $"Invalid Payment amount row: {reason}."
            | CashflowInvalidPaymentState str -> $"Invalid PaymentState of \"{str}\"."
            | CashflowInvalidPaymentTransactionPointerRow reason -> $"Invalid Payment transactionPointer row: {reason}."
            | CashflowInvalidPostedState str -> $"Invalid PostedState of \"{str}\"."
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
            | CashflowPaymentAgreementIdListCannotBeEmpty -> "The paymentAgreementIds list must contain at least 1 ID."
            | CashflowPaymentAgreementLinkLineAlreadyLinked(stageEntryLineId, paymentAgreementId) -> $"Stage entry line {stageEntryLineId} is already linked to PaymentAgreement {paymentAgreementId}. Repoint that linkage or remove it rather than adding a second one."
            | CashflowPaymentAgreementLinkNoInvoiceToMatch(stageEntryLineId, paymentAgreementId) -> $"Stage entry line {stageEntryLineId} is linked to PaymentAgreement {paymentAgreementId} but no open Invoice accepts it. The Instance or Invoice it belongs to is missing, or the cadence that would have created it is wrong."
            | CashflowPaymentAgreementLinkUpdateNoOp -> "Updating the PaymentAgreementLink record failed because at least one updatable parameter must be set."
            | CashflowPaymentAgreementMemoIsEmpty memo -> $"PaymentAgreementMemo cannot be empty. Provided Memo is {memo}."
            | CashflowPaymentAgreementMemoTooLong(memo, max) -> $"PaymentAgreementMemo cannot exceed {max} characters. Provided Memo is {memo}."
            | CashflowPaymentAgreementNameDoesntMatchId name -> $"PaymentAgreementName of {name} doesn't match a PaymentAgreement ID in the database."
            | CashflowPaymentAgreementNameIsEmpty name -> $"PaymentAgreementName cannot be empty. Provided name is {name}."
            | CashflowPaymentAgreementNameTooLong(name, max) -> $"PaymentAgreementName cannot exceed {max} characters. Provided name is {name}."
            | CashflowPaymentAgreementNonPositiveExpectedAmount(paymentAgreementId, amount) -> $"PaymentAgreement {paymentAgreementId} expected amount ({amount}) cannot be less than or equal to 0.00."
            | CashflowPaymentAgreementNotUnderMasterAgreement(paymentAgreementId, agreementId) -> $"PaymentAgreement {paymentAgreementId} does not belong to MasterAgreement {agreementId}."
            | CashflowPaymentAgreementUpdateNoOp -> "Updating the PaymentAgreement record failed because at least one updatable parameter must be set."
            | CashflowPaymentAgreementsListCannotBeEmpty -> "A MasterAgreement must have at least one PaymentAgreement."
            | CashflowPaymentIdDoesntExist uuid -> $"Could not locate a Payment with the id of {uuid}."
            | CashflowPaymentLineNotOnAgreementAccount(paymentUuid, actualAccountUuid, expectedAccountUuid) ->
                let actualStr =
                    match actualAccountUuid with
                    | Some uuid -> uuid.ToString()
                    | None -> "unassigned"
                $"Payment {paymentUuid} points at a line on account {actualStr}, but its PaymentAgreement is satisfied on account {expectedAccountUuid}."
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
            
            
            | IngestionBaseStageEntryGroupIdIsEmpty str -> $"BaseStageEntryGroupId cannot be empty. Provided value is {str}."
            | IngestionBaseStageEntryGroupIdTooLong (str, max) -> $"BaseStageEntryGroupId cannot exceed {max} characters. Provided value is {str}."
            | IngestionBaseStageGroupIdDistinctDataViolation str -> $"More than one combination of \"header\" data found for BaseStageEntryGroupId {str}"
            | IngestionClassificationRuleGroupsEmpty -> "A ClassificationRule's ClassificationRuleGroup list cannot be empty."
            | IngestionClassificationRuleIdDoesntExist uuid -> $"Could not locate a ClassificationRule with the id of {uuid}."
            | IngestionClassificationRuleIdListCannotBeEmpty -> "The classificationRuleIds list must contain at least 1 ID."
            | IngestionClassificationRuleInvalidClaimant(ruleUuid, accountUuid, paymentAgreementUuid) ->
                let accountStr = match accountUuid with
                                    | Some x -> x.ToString()
                                    | None -> "None"
                let paymentAgreementStr = match paymentAgreementUuid with
                                            | Some x -> x.ToString()
                                            | None -> "None"
                $"ClassificationRule ({ruleUuid}) must claim exactly one of an account or a payment agreement. Account at match is {accountStr}; payment agreement at match is {paymentAgreementStr}."
            | IngestionClassificationRuleNameIsEmpty str -> $"ClassificationRuleName cannot be empty. Provided value is {str}."
            | IngestionClassificationRuleNameTooLong (str, max) -> $"ClassificationRuleName cannot exceed {max} characters. Provided value is {str}."
            | IngestionClassificationRuleUpdateNoOp -> "Updating the ClassificationRule record failed because at least one updatable parameter must be set."
            | IngestionFieldMatchChainEmpty -> "A FieldMatchChain's chain cannot be empty."
            | IngestionInvalidClassificationClaimantType str -> $"Invalid ClassificationClaimantType of \"{str}\"."
            | IngestionInvalidClassificationGroupConnector str -> $"Invalid ClassificationConnector of \"{str}\"."
            | IngestionInvalidNumericSearchOperator str -> $"Invalid NumericSearchOperator of \"{str}\"."
            | IngestionInvalidStagedEntryStatus str -> $"Provided string of '{str}' is not a valid StagedEntryStatus."
            | IngestionInvalidStageStatusChangeMechanism str -> $"Provided string of '{str}' is not a valid StageStatusChangeMechanism."
            | IngestionInvalidStageStatusTransition (fromStr, toStr) -> $"Invalid stage status transition. Cannot move from {fromStr} to {toStr}."
            | IngestionSearchPatternIsEmpty str -> $"SearchPattern cannot be empty. Provided value is {str}."
            | IngestionSearchPatternTooLong (str, max) -> $"SearchPattern cannot exceed {max} characters. Provided value is {str}."
            | IngestionStageEntryHeaderIdDoesntExist uuid -> $"Could not locate a stage entry header with the id of {uuid}."
            | IngestionStageEntryLineIdDoesntExist uuid -> $"Could not locate a stage entry line with the id of {uuid}."
            | IngestionStageEntryLineIdListCannotBeEmpty -> "The stageEntryLineIds list must contain at least 1 ID."
            | IngestionStageEntryHeaderNoOp -> "Updating the StageEntryHeader record failed because at least one updatable parameter must be set."
            | IngestionStageEntryLineNoOp -> "Updating the StageEntryLine record failed because at least one updatable parameter must be set."
            | IngestionStageEntryLineNoMatchingJournalEntryLine uuid -> $"Could not locate a journal entry line matching stage entry line {uuid} on account, line type, and amount."
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
                                        
            | ReportingUnknownReportName name -> $"Unknown report: {name}."

