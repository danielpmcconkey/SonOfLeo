module ModelOrchestrator.InstanceOrchestration

open DataAccessLayer.ExecuteReader
open Model
open Model.CashFlow
open Model.DataIngestion
open Model.DataIngestion.StageEntryComponent
open Model.Ledger
open Model.Ledger.JournalEntryComponent
open ModelOrchestrator.CashFlowCompositeFetcher
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper

type InvoiceComposite = private {
    invoice: Invoice.Invoice
    payments: Payment.Payment list
}

type InstanceComposite = private {
    instance: Instance.Instance
    invoiceComposites: InvoiceComposite list
}

let private isPostedPayment (payment: Payment.Payment) : bool =
    match payment |> Payment.transactionPointer with
    | CashFlowComponent.Posted _ -> true
    | CashFlowComponent.Staged _ -> false

let private confirmPaymentIsUnderInvoice
    (invoiceId: CashFlowComponent.InvoiceId)
    (payment: Payment.Payment)
    : Result<unit, AppError> =
    if payment |> Payment.invoiceId = invoiceId then Ok ()
    else
        let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
        let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid))

let private confirmPayment
    (context: Context.Context)
    (payment: Payment.Payment)
    : Result<unit, AppError> =
    result {
        // JE and SE existence is checked below via whichever half of the transactionPointer is actually populated;
        // the other half isn't reachable off a reconstituted Payment (see transactionPointerFromColumns).
        let! journalEntryHeader =
            match payment |> Payment.transactionPointer with
            | CashFlowComponent.Posted journalEntryHeaderId ->
                match journalEntryHeaderId |> JournalEntryHeader.fetchById context with
                | Ok header -> Ok(Some header)
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    let journalEntryHeaderUuid = journalEntryHeaderId |> JournalEntryHeaderId.value
                    Error(JournalEntryHeaderIdDoesntExist journalEntryHeaderUuid)
                | Error e -> Error e
            | CashFlowComponent.Staged stageEntryHeaderId ->
                match stageEntryHeaderId |> StageEntryHeader.fetchById context with
                | Ok _ -> Ok None
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    let stageEntryHeaderUuid = stageEntryHeaderId |> StageEntryHeaderId.value
                    Error(IngestionStageEntryHeaderIdDoesntExist stageEntryHeaderUuid)
                | Error e -> Error e
        return!
            match payment |> Payment.postedToLedgerDate, journalEntryHeader with
            | None, _ -> Ok ()
            | Some _, None ->
                let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                Error(CashflowPaymentPostedToLedgerDateWithoutJournalEntry paymentUuid)
            | Some providedDate, Some header ->
                let actualDate = header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                if providedDate.localDate = actualDate then Ok ()
                else
                    let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                    Error(CashflowPaymentPostedToLedgerDateMismatch(paymentUuid, providedDate.localDate, actualDate))
    }

let private confirmInvoiceAmountIsPositive
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    let invoiceAmount = invoice |> Invoice.amount
    let invoiceAmountDecimal = invoiceAmount.money |> Money.amount
    if invoiceAmountDecimal > 0M then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowInvoiceNonPositiveAmount(invoiceUuid, invoiceAmountDecimal))

let private confirmFullyPaidAmountMatches
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, AppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.paymentState <> CashFlowComponent.FullyPaid then Ok () else
    result {
        let! paidTotal = payments |> List.map Payment.amount |> List.map _.money |> Money.sumList
        let invoiceAmount = invoice |> Invoice.amount
        return!
            if paidTotal = invoiceAmount.money then Ok ()
            else
                let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
                let paidDec = paidTotal |> Money.amount
                let invoiceDec = invoiceAmount.money |> Money.amount
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

let private confirmInvoiceComposite
    (context: Context.Context)
    (invoiceComposite: InvoiceComposite)
    : Result<unit, AppError> =
    let invoice = invoiceComposite.invoice
    let payments = invoiceComposite.payments
    let invoiceId = invoice |> Invoice.invoiceId
    result {
        do!
            payments
            |> List.map (confirmPaymentIsUnderInvoice invoiceId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do! invoice |> confirmInvoiceAmountIsPositive
        do! confirmFullyPaidAmountMatches invoice payments
        do! confirmPostedToLedgerRequiresFullyPaid invoice
        do! confirmFullyPaidHasNoBlocker invoice
        do! confirmPartiallyPaidHasPayments invoice payments
        do! confirmPostedToLedgerRequiresAllPaymentsPosted invoice payments
        do! confirmPartiallyPostedHasAPostedPayment invoice payments
        do!
            payments
            |> List.map (confirmPayment context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private confirmInvoiceIsUnderInstance
    (instanceId: CashFlowComponent.InstanceId)
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    if invoice |> Invoice.instanceId = instanceId then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        let instanceUuid = instanceId |> CashFlowComponent.InstanceId.value
        Error(CashflowInvoiceNotUnderInstance(invoiceUuid, instanceUuid))

let private confirmInvoicePaymentAgreementIsUnderInstanceAgreement
    (context: Context.Context)
    (instanceAgreementId: CashFlowComponent.MasterAgreementId)
    (agreementPaymentAgreementIds: CashFlowComponent.PaymentAgreementId list)
    (invoice: Invoice.Invoice)
    : Result<unit, AppError> =
    let paymentAgreementId = invoice |> Invoice.paymentAgreementId
    if agreementPaymentAgreementIds |> List.contains paymentAgreementId then Ok () else
    result {
        // this fetch only runs once the diamond is already known to be broken; it exists to name the other
        // MasterAgreement in the error, not to decide the check
        let! paymentAgreement =
            match paymentAgreementId |> PaymentAgreement.fetchById context with
            | Ok pa -> Ok pa
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                Error(CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
            | Error e -> Error e
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        let instanceAgreementUuid = instanceAgreementId |> CashFlowComponent.MasterAgreementId.value
        let paymentAgreementAgreementUuid =
            paymentAgreement |> PaymentAgreement.masterAgreementID |> CashFlowComponent.MasterAgreementId.value
        return!
            Error(CashflowInvoiceDiamondMismatch(invoiceUuid, instanceAgreementUuid, paymentAgreementAgreementUuid))
    }

let private confirmDiamond
    (context: Context.Context)
    (instance: Instance.Instance)
    (invoices: Invoice.Invoice list)
    : Result<unit, AppError> =
    if invoices |> List.isEmpty then Ok () else
    result {
        let instanceAgreementId = instance |> Instance.masterAgreementID
        let! agreementPaymentAgreements =
            [ instanceAgreementId ] |> PaymentAgreement.fetchByMasterAgreementIdList context
        let agreementPaymentAgreementIds =
            agreementPaymentAgreements |> List.map PaymentAgreement.paymentAgreementId
        return!
            invoices
            |> List.map (
                confirmInvoicePaymentAgreementIsUnderInstanceAgreement
                    context instanceAgreementId agreementPaymentAgreementIds)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private confirmFulfilledInstanceHasInvoices
    (instance: Instance.Instance)
    (invoices: Invoice.Invoice list)
    : Result<unit, AppError> =
    if instance |> Instance.isFulfilled |> not || (invoices |> List.isEmpty |> not) then Ok ()
    else
        let instanceUuid = instance |> Instance.instanceId |> CashFlowComponent.InstanceId.value
        Error(CashflowInstanceFulfilledWithNoInvoices instanceUuid)

let private confirmFulfilledInstanceInvoicesAreFullyPaid
    (instance: Instance.Instance)
    (invoices: Invoice.Invoice list)
    : Result<unit, AppError> =
    if instance |> Instance.isFulfilled |> not then Ok () else
    let instanceUuid = instance |> Instance.instanceId |> CashFlowComponent.InstanceId.value
    invoices
    |> List.map (fun invoice ->
        let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
        if lifeCycleState.paymentState = CashFlowComponent.FullyPaid then Ok ()
        else
            let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
            Error(CashflowInstanceFulfilledWithUnpaidInvoice(instanceUuid, invoiceUuid)))
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let confirmInstanceComposite
    (context: Context.Context)
    (instanceComposite: InstanceComposite)
    : Result<unit, AppError> =
    let instance = instanceComposite.instance
    let invoiceComposites = instanceComposite.invoiceComposites
    let invoices = invoiceComposites |> List.map (fun invoiceComposite -> invoiceComposite.invoice)
    let instanceId = instance |> Instance.instanceId
    result {
        // cohesion runs first on purpose: every check after it reads the in-hand Instance as the parent of these
        // invoices, which is only sound once they've been proven to belong to it
        do!
            invoices
            |> List.map (confirmInvoiceIsUnderInstance instanceId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do! confirmDiamond context instance invoices
        do! confirmFulfilledInstanceHasInvoices instance invoices
        do! confirmFulfilledInstanceInvoicesAreFullyPaid instance invoices
        do!
            invoiceComposites
            |> List.map (confirmInvoiceComposite context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
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

let fetchCompositeByInstanceId
    (context: Context.Context)
    (instanceId: CashFlowComponent.InstanceId)
    : Result<InstanceComposite, AppError> =
    result {
        let! instance = instanceId |> Instance.fetchById context
        let! invoices = [ instanceId ] |> Invoice.fetchByInstanceIdList context
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments =
            if invoiceIds |> List.isEmpty then Ok [] else invoiceIds |> Payment.fetchByInvoiceIdList context
        return { instance = instance; invoiceComposites = compileFromSubLists invoices payments }
    }

let private confirmPaymentBelongsToInvoice
    (context: Context.Context)
    (invoiceId: CashFlowComponent.InvoiceId)
    (fieldUpdates: Payment.PaymentFieldUpdates)
    : Result<unit, AppError> =
    result {
        let! payment = fieldUpdates.paymentIdToUpdate |> Payment.fetchById context
        return! payment |> confirmPaymentIsUnderInvoice invoiceId
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
/// in a transaction you can roll back. The returned composite is the whole Instance the updated Invoice hangs off,
/// not just that Invoice
let updateInvoiceComposite
    (context: Context.Context)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates)
    : Result<InstanceComposite, AppError> =
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
        let! updatedInvoice = invoiceUpdates.invoiceIdToUpdate |> Invoice.fetchById context
        let! fetched = updatedInvoice |> Invoice.instanceId |> fetchCompositeByInstanceId context
        do! fetched |> confirmInstanceComposite context
        return fetched
    }
    
let createInstanceCompositeAndSaveToDb
    (context: Context.Context)
    (masterAgreementID: CashFlowComponent.MasterAgreementId)
    (instanceDate: LocalDate)
    (isFulfilled: bool)
    (invoiceCompositeFieldsList: (
        // invoice fields
        CashFlowComponent.PaymentAgreementId *
        CashFlowComponent.ExternalInvoiceId option *
        CashFlowComponent.InvoiceDate *
        CashFlowComponent.DueDate *
        CashFlowComponent.InvoiceAmount *
        CashFlowComponent.InvoiceLifeCycleState *
        CashFlowComponent.InvoiceMemo option *
        ( // payments
            CashFlowComponent.TransactionPointer *
            CashFlowComponent.PaymentAmount *
            CashFlowComponent.PostedToFiDate option *
            CashFlowComponent.PostedToLedgerDate option *
            CashFlowComponent.PaymentMemo option) list
        ) list)
    : Result<InstanceComposite, AppError> =
    result {
        let instanceId = CashFlowComponent.InstanceId.create()
        let now = context |> Context.getInitiationInstant
        let newInstance = Instance.create instanceId masterAgreementID instanceDate isFulfilled now now
        let invoiceComposites =
            invoiceCompositeFieldsList |> List.map(fun invFields ->
                let paId, externalInvoiceId, invoiceDate, dueDate,
                    invAmount, lifecycle, invMemo, paymentsFieldsList = invFields
                let invoiceId = CashFlowComponent.InvoiceId.create()
                let invoice = Invoice.create invoiceId instanceId paId externalInvoiceId
                                  invoiceDate dueDate invAmount lifecycle invMemo now now
                let payments = paymentsFieldsList |> List.map(fun pmtFields ->
                    let paymentId = CashFlowComponent.PaymentId.create()
                    let transactionPointer, pmtAmount, postedToFi, postedToLedger, pmtMemo = pmtFields
                    Payment.create paymentId invoiceId transactionPointer pmtAmount
                        postedToFi postedToLedger pmtMemo now now
                    )
                { invoice = invoice; payments = payments }
                )
        let instanceComposite = { instance = newInstance; invoiceComposites = invoiceComposites }
        do! instanceComposite |> confirmInstanceComposite context // todo: this probably does reads on the database and none of this is in the db yet. rethink
        do! newInstance |> Instance.insertNewToDb context
        do! invoiceComposites
            |> List.map(fun invoiceComposite -> result {
                do! invoiceComposite.invoice |> Invoice.insertNewToDb context
                do! invoiceComposite.payments
                    |> List.map(fun payment -> payment |> Payment.insertNewToDb context)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
                return () }
                )
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return instanceComposite
    }
    


