module ModelOrchestrator.InvoiceOrchestration

open DataAccessLayer.ExecuteReader
open Model
open Model.CashFlow
open ModelOrchestrator.CashFlowCompositeFetcher
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper

type InvoiceComposite = private {
    invoice: Invoice.Invoice
    payments: Payment.Payment list
}

let private confirmPaymentBelongsToInvoice
    (context: Context.Context)
    (invoiceId: CashFlowComponent.InvoiceId)
    (fieldUpdates: Payment.PaymentFieldUpdates)
    : Result<unit, AppError> =
    result {
        let! payment = fieldUpdates.paymentIdToUpdate |> Payment.fetchById context
        return!
            if payment |> Payment.invoiceId = invoiceId then Ok ()
            else
                let paymentUuid = fieldUpdates.paymentIdToUpdate |> CashFlowComponent.PaymentId.value
                let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
                Error(CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid))
    }

let private confirmAuthorityAndCohesion
    (context: Context.Context)
    (invoiceUpdate: Invoice.InvoiceFieldUpdates)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    : Result<unit, AppError> =
    let invoiceId = invoiceUpdate.invoiceIdToUpdate
    paymentUpdates
    |> List.map (confirmPaymentBelongsToInvoice context invoiceId)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmDiamond
    (context: Context.Context)
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    result {
        let! instance = invoice |> Invoice.instanceId |> Instance.fetchById context
        let! paymentAgreement = invoice |> Invoice.paymentAgreementId |> PaymentAgreement.fetchById context
        let instanceAgreementId = instance |> Instance.masterAgreementID
        let paymentAgreementAgreementId = paymentAgreement |> PaymentAgreement.masterAgreementID
        return!
            if instanceAgreementId = paymentAgreementAgreementId then Ok ()
            else
                let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
                let instanceAgreementUuid = instanceAgreementId |> CashFlowComponent.MasterAgreementId.value
                let paymentAgreementAgreementUuid = paymentAgreementAgreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowInvoiceDiamondMismatch(invoiceUuid, instanceAgreementUuid, paymentAgreementAgreementUuid))
    }

let private confirmFullyPaidAmountMatches
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.paymentState <> CashFlowComponent.FullyPaid then Ok () else
    result {
        let! paidTotal = payments |> List.map Payment.amount |> Money.sumList
        let invoiceAmount = invoice |> Invoice.amount
        return!
            if paidTotal = invoiceAmount then Ok ()
            else
                let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
                let paidDec = paidTotal |> Money.amount
                let invoiceDec = invoiceAmount |> Money.amount
                Error(CashflowInvoiceFullyPaidAmountMismatch(invoiceUuid, paidDec, invoiceDec))
    }

let private confirmPostedToLedgerRequiresFullyPaid
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.postedState <> CashFlowComponent.PostedToLedger
       || lifeCycleState.paymentState = CashFlowComponent.FullyPaid then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowInvoicePostedToLedgerRequiresFullyPaid invoiceUuid)

let private confirmFullyPaidHasNoBlocker
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.paymentState <> CashFlowComponent.FullyPaid || lifeCycleState.blocker |> Option.isNone then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowInvoiceFullyPaidWithBlocker invoiceUuid)

let private confirmPartiallyPaidHasPayments
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.paymentState <> CashFlowComponent.PartiallyPaid || (payments |> List.isEmpty |> not) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowInvoicePartiallyPaidWithNoPayments invoiceUuid)

let private isPostedPayment (payment: Payment.Payment) : bool =
    match payment |> Payment.transactionPointer with
    | CashFlowComponent.Posted _ -> true
    | CashFlowComponent.Staged _ -> false

let private confirmPostedToLedgerRequiresAllPaymentsPosted
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.postedState <> CashFlowComponent.PostedToLedger
       || (payments |> List.forall isPostedPayment) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowInvoicePostedToLedgerWithUnpostedPayment invoiceUuid)

let private confirmPartiallyPostedHasAPostedPayment
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.postedState <> CashFlowComponent.PartiallyPosted
       || (payments |> List.exists isPostedPayment) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowInvoicePartiallyPostedWithNoPostedPayment invoiceUuid)

let confirmInvoiceComposite
    (context: Context.Context)
    (invoiceComposite: InvoiceComposite)
    : Result<unit, AppError> =
    let invoice = invoiceComposite.invoice
    let payments = invoiceComposite.payments
    result {
        do! invoice |> confirmDiamond context
        do! confirmFullyPaidAmountMatches invoice payments
        do! confirmPostedToLedgerRequiresFullyPaid invoice
        do! confirmFullyPaidHasNoBlocker invoice
        do! confirmPartiallyPaidHasPayments invoice payments
        do! confirmPostedToLedgerRequiresAllPaymentsPosted invoice payments
        do! confirmPartiallyPostedHasAPostedPayment invoice payments
    }

let private compileFromSubLists
    (invoices: Invoice.Invoice list)
    (payments: Payment.Payment list)
    : InvoiceComposite list =
    invoices
    |> List.map (fun inv ->
        let invId = inv |> Invoice.invoiceId
        let paymentsAtInv = payments |> List.filter (fun p -> p |> Payment.invoiceId = invId)
        { invoice = inv; payments = paymentsAtInv })

let fetchFiltered
    (context: Context.Context)
    (expectedRows: AcceptableExpectedRows)
    (filter: AgreementFilter)
    : Result<InvoiceComposite list, AppError> =
    result {
        let! invoices =
            filter |> fetchCompositeFiltered context expectedRows Invoice.readRowsFromDb TargetComposite.Invoice
        if invoices |> List.isEmpty then return [] else
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments = invoiceIds |> Payment.fetchByInvoiceIdList context
        return compileFromSubLists invoices payments
    }

let fetchCompositeByInvoiceId
    (context: Context.Context)
    (invoiceId: CashFlowComponent.InvoiceId)
    : Result<InvoiceComposite, AppError> =
    result {
        let! invoice = invoiceId |> Invoice.fetchById context
        let! payments = [ invoiceId ] |> Payment.fetchByInvoiceIdList context
        return { invoice = invoice; payments = payments }
    }

let private isThereAnInvoiceUpdate
    (invoiceUpdates: Invoice.InvoiceFieldUpdates)
    : bool =
    invoiceUpdates.externalInvoiceIdUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.invoiceDateUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.dueDateUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.amountUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.invoiceStateUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.paymentStateUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.postedStateUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.blockerUpdate <> FieldUpdate.NoChange
    || invoiceUpdates.memoUpdate <> FieldUpdate.NoChange

let private isThereAPaymentUpdate
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    : bool =
    paymentUpdates
    |> List.map (fun u ->
        u.journalEntryHeaderIdUpdate <> FieldUpdate.NoChange
        || u.stageEntryHeaderIdUpdate <> FieldUpdate.NoChange
        || u.postedToFiDateUpdate <> FieldUpdate.NoChange
        || u.memoUpdate <> FieldUpdate.NoChange)
    |> List.exists id

/// Note to caller, many of the updates are sent to the DB *before* true aggregate validation. Make sure you wrap this
/// in a transaction you can roll back
let updateInvoiceComposite
    (context: Context.Context)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates)
    : Result<InvoiceComposite, AppError> =
    result {
        let shouldUpdateInvoice = invoiceUpdates |> isThereAnInvoiceUpdate
        let shouldUpdatePayments = paymentUpdates |> isThereAPaymentUpdate
        do!
            if shouldUpdateInvoice = false && shouldUpdatePayments = false
            then Error CashflowInvoiceCompositeUpdateNoOp
            else Ok ()
        do! confirmAuthorityAndCohesion context invoiceUpdates paymentUpdates
        do!
            if shouldUpdateInvoice then invoiceUpdates |> Invoice.updateDb context |> Result.map ignore
            else Ok ()
        do!
            if shouldUpdatePayments then
                paymentUpdates
                |> List.map (Payment.updateDb context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        let! fetched = invoiceUpdates.invoiceIdToUpdate |> fetchCompositeByInvoiceId context
        do! fetched |> confirmInvoiceComposite context
        return fetched
    }
