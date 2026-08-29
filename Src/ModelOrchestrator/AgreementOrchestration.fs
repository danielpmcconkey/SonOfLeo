module ModelOrchestrator.AgreementOrchestration

open System
open Model
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.CashFlowCompositeFetcher
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper

type Agreement = private {
    masterAgreement: MasterAgreement.MasterAgreement
    paymentAgreements: PaymentAgreement.PaymentAgreement list
    instances: Instance.Instance list
    invoices: Invoice.Invoice list
    payments: Payment.Payment list
}

let private confirmValidAccountId
    (context: Context.Context)
    (accountId: AccountId)
    : Result<unit, AppError> =
    let accountUuid = accountId |> AccountId.value
    let lookupResult = // we don't need the code; we just check that the ID is in the DB this way
        accountUuid |> LookupCache.accountIdToCode.fetch context 
    match lookupResult with
    | Ok _ -> Ok ()
    | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
        Error (AccountIdDoesntMatch accountUuid)
    | Error e -> Error e

let private confirmAuthorityAndCohesion
    (context: Context.Context)
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    (instanceUpdates: Instance.InstanceFieldUpdates list)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates list)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<unit, AppError> =
    result {
        // note: this runs super slow. It's not a common activity so that's likely okay. Start with this flow. If we
        // notice that it takes forever, we can implement some memoization down the line
        let agreementId = masterAgreementUpdates.agreementIdToUpdate
        // confirm that every paymentAgreementUpdates.paymentAgreementIdToUpdate belongs to a PaymentAgreement whose masterAgreementID matches agreementId.
        // confirm that every instanceUpdates.instanceIdToUpdate belongs to an Instance whose masterAgreementID matches agreementId.
        // confirm that every invoiceUpdates.invoiceIdToUpdate belongs to an Invoice whose instanceId belongs to an Instance whose masterAgreementID matches agreementId.
        // confirm that every paymentUpdates.paymentIdToUpdate belongs to Payment whose invoiceId belongs to an Invoice whose instanceId belongs to an Instance whose masterAgreementID matches agreementId.
        
        raise(NotImplementedException())
    }

let private confirmPayment
    (context: Context.Context)
    (payment: Payment.Payment)
    : Result<unit, AppError> =
    result {
        // confirm invoiceId is real
        // confirm JE and SE links in transactionPointer are real
        // confirm if postedToLedgerDate is some then so is transactionPointer's JE header ID
        // confirm postedToLedgerDate (if some) matches the JE entry date 
        raise(NotImplementedException())
    }

let private confirmPayments
    (context: Context.Context)
    (payments: Payment.Payment list)
    : Result<unit, AppError> =
    payments
    |> List.map (confirmPayment context)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmInvoice
    (context: Context.Context)
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    result {
        // confirm instanceId is real
        // confirm paymentAgreementId is real
        // confirm amount is >= 0
        raise(NotImplementedException())
    }

let private confirmInvoices
    (context: Context.Context)
    (invoices: Invoice.Invoice list)
    : Result<unit, AppError> =
    invoices
    |> List.map (confirmInvoice context)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmInstance
    (context: Context.Context)
    (agreementId: MasterAgreementId)
    (instance: Instance.Instance)
    : Result<unit, AppError> =
    result {
        // confirm pa masterAgreementID matches agreementId
        raise(NotImplementedException())
    }

let private confirmInstances
    (context: Context.Context)
    (agreementId: MasterAgreementId)
    (instances: Instance.Instance list)
    : Result<unit, AppError> =
    instances
    |> List.map (confirmInstance context agreementId)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmPaymentAgreement
    (context: Context.Context)
    (agreementId: MasterAgreementId)
    (paymentAgreement: PaymentAgreement.PaymentAgreement)
    : Result<unit, AppError> =
    result {
        // confirm pa masterAgreementID matches agreementId
        // confirm debit and credit account map to real accounts (use confirmValidAccountId but map errors to new errors that indicate whether it's the debit or credit account)
        // confirm expected amount (if some) is > 0
        raise(NotImplementedException())
    }

let private confirmPaymentAgreements
    (context: Context.Context)
    (agreementID: MasterAgreementId)
    (paymentAgreements: PaymentAgreement.PaymentAgreement list)
    : Result<unit, AppError> =
    result {
        // confirm list isn't empty
        // confirm each pa is valid
        raise(NotImplementedException())
    }

let private confirmAgreementDates
    (context: Context.Context)
    (startDate: LocalDate)
    (endDate: LocalDate option)
    : Result<unit, AppError> =
    result {
        if endDate |> Option.isNone then Ok () else
        // confirm end is >= start (can be a one day event)
        // confirm end isn't in the past
        raise(NotImplementedException())
    }

let private confirmMasterAgreement
    (context: Context.Context)
    (masterAgreement: MasterAgreement.MasterAgreement)
    : Result<unit, AppError> =
    confirmAgreementDates context (masterAgreement |> MasterAgreement.startDate) (masterAgreement |> MasterAgreement.endDate)

let private confirmComposite
    (context: Context.Context)
    (agreement: Agreement)
    : Result<unit, AppError> =
    result {
        // call confirmMasterAgreement
        // call confirmPaymentAgreements
        // call confirmInstances
        // call confirmInvoices
        // call confirmPayments
        raise(NotImplementedException())
    }
    
/// Note to caller, master agreement and payment agreements are persisted into the DB *before* composite validation.
/// Make sure you wrap this in a transaction you can roll back
let constructNewAndSaveToDb
    (context: Context.Context)
    (paymentAgreements: PaymentAgreement.PaymentAgreement list)
    (masterAgreement: MasterAgreement.MasterAgreement)
    : Result<Agreement, AppError> =
    result {
        do! masterAgreement |> confirmMasterAgreement context
        do! paymentAgreements |> confirmPaymentAgreements context (masterAgreement |> MasterAgreement.agreementID)
        // persist the master agreement
        // construct and persist payment agreements
        // construct the composite Agreement
        // call confirmComposite
        // return the composite
        raise(NotImplementedException())
    }

let fetchFiltered
    (context: Context.Context)
    (filter: AgreementFilter)
    : Result<Agreement list, AppError> =
    result {
        let fetchFunc = MasterAgreement.readRowsFromDb
        let! masterAgreements =
            filter
            |> fetchCompositeFiltered context fetchFunc TargetComposite.Agreement
        // fetch the rest of the composite parts
        // assemble. see compileFromSubLists in StageEntryOrchestration for the pattern
        // trust the DB and return
        raise(NotImplementedException())
    }

let fetchByMasterAgreementId
    (context: Context.Context)
    (agreementId: MasterAgreementId)
    : Result<Agreement, AppError> =
    result {
        // form a filter with the agreementId
        // call fetchFiltered with expected rows of exactly one
        // take the head and return
        raise(NotImplementedException())
    }
    
/// Note to caller, many of the updates are sent to the DB *before* true aggregate validation. Make sure you wrap this
/// in a transaction you can roll back
let updateAgreement
    (context: Context.Context)
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    (instanceUpdates: Instance.InstanceFieldUpdates list)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates list)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<Agreement, AppError> =
    result {
        // check which elements need to be updated
        // confirm against no op
        // call confirmAuthorityAndCohesion
        // if master needs updating
            // call MasterAgreement.updateDb; discard the returned agreement
        // if any payment agreements need updating
            // call PaymentAgreement.updateDb on each; discard all
        // if any instances need updating
            // send each instanceUpdate to Instance.updateDb; discard
        // if any invoices need updating
            // send each invoiceUpdate to Invoice.updateDb; discard
        // if any payments need updating
            // send each paymentUpdate to Payment.updateDb; discard
        // call fetchByMasterAgreementId
        // call confirmComposite to make sure you didn't fuck up my database
        // return fetched
        raise(NotImplementedException())
    }
