module ModelOrchestrator.InvoiceOrchestration

open System
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.CashFlow.Invoice
open ModelOrchestrator.CashFlowCompositeFetcher
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.ResultHelper

type InvoiceComposite = private {
    invoice: Invoice.Invoice
    payments: Payment.Payment list
}

let private confirmAuthorityAndCohesion
    (context: Context.Context)
    (invoiceUpdate: InvoiceFieldUpdates)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    : Result<unit, AppError> =
    result {
        let invoiceId = invoiceUpdate.invoiceIdToUpdate
        // confirm that every paymentUpdates.paymentIdToUpdate belongs to a Payment whose invoiceId matches invoiceId.
        
        raise(NotImplementedException())
    }

let confirmInvoiceComposite
    (context: Context.Context)
    (invoiceComposite: InvoiceComposite)
    : Result<unit, AppError> =
    (*
    diamond check. Insure InstanceId and PaymentAgreementId both relate to the same MasterAgreementId
    PaymentState → FullyPaid requires sum(payments) = invoice amount.
    PostedState → PostedToLedger requires PaymentState = FullyPaid.
    PaymentState → FullyPaid requires Blocker = None. 
    PaymentState → PartiallyPaid requires at least one Payment exists. 
    PostedState → PostedToLedger requires all Payments have a JE pointer (not staged). 
    PostedState → PartiallyPosted requires at least one Payment with a JE pointer
    Blocker can't be set while PaymentState = FullyPaid. The reverse of #4 — if it's fully paid, what are you blocking?
    *)
    raise(NotImplementedException())

let fetchFiltered
    (context: Context.Context)
    (filter: AgreementFilter)
    : Result<InvoiceComposite list, AppError> =
    result {
        let fetchFunc = Invoice.readRowsFromDb
        let! masterAgreements =
            filter
            |> fetchCompositeFiltered context fetchFunc TargetComposite.Agreement
        // fetch the rest of the composite parts
        // assemble. see compileFromSubLists in StageEntryOrchestration for the pattern
        // trust the DB and return
        raise(NotImplementedException())
        return []
    }

let fetchCompositeByInvoiceId
    (context: Context.Context)
    (invoiceId: InvoiceId)
    : Result<InvoiceComposite, AppError> =
    result {
        // form a filter with the invoiceId
        // call fetchFiltered with expected rows of exactly one
        // take the head and return
        raise(NotImplementedException())
    }
    
/// Note to caller, many of the updates are sent to the DB *before* true aggregate validation. Make sure you wrap this
/// in a transaction you can roll back
let updateInvoiceComposite
    (context: Context.Context)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (invoiceUpdates: InvoiceFieldUpdates)
    : Result<InvoiceComposite, AppError> =
    result {
        // check which elements need to be updated
        // confirm against no op
        // call confirmAuthorityAndCohesion
        // if invoice needs updating
            // call Invoice.updateDb; discard the returned agreement
        // if any payments need updating
            // send each invoiceUpdate to Invoice.updateDb; discard
        // call fetchCompositeByInvoiceId
        // call confirmInvoiceComposite to make sure you didn't fuck up my database
        // return fetched
        raise(NotImplementedException())
    }
