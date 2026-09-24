module Business.FinancialServices.CashFlow.CashFlowError

open System
open NodaTime
open App.Utility.IAppError

type CashFlowError =
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
    
    interface IAppError with
        member this.DomainName = nameof CashFlowError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
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
            
let toMessage (e: CashFlowError) = (e :> IAppError).ToMessage()
let toAppError (e: CashFlowError) : IAppError = e :> IAppError
let error (e: CashFlowError) : Result<'T, IAppError> = Error (e :> IAppError)
