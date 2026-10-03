module Business.CrossDomainOrchestration.InstanceOrchestration

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.DataAccessLayer
open App.DataAccessLayer.DalError
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.Classification
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.CashFlowCompositeFetcher

type InvoiceComposite = private {
    invoice: Invoice.Invoice
    payments: Payment.Payment list
}

type InstanceComposite = private {
    instance: Instance.Instance
    invoiceComposites: InvoiceComposite list
}

let invoice (invoiceComposite: InvoiceComposite) = invoiceComposite.invoice
let payments (invoiceComposite: InvoiceComposite) = invoiceComposite.payments
let instance (instanceComposite: InstanceComposite) = instanceComposite.instance
let invoiceComposites (instanceComposite: InstanceComposite) = instanceComposite.invoiceComposites

type PaymentAgreementClassificationResult = {
    runId: ClassificationComponent.ClassificationRunId
    classificationResults: ClassificationComponent.ClassificationResult list
    linksCreated: PaymentAgreementLink.PaymentAgreementLink list
    decisionLog: ClassificationComponent.PaymentAgreementDecision list
    invoiceDecisionLog: CashFlowComponent.InvoiceDecision list
    openInstances: InstanceComposite list
}

let private isPostedPayment (payment: Payment.Payment) : bool =
    match payment |> Payment.transactionPointer with
    | CashFlowComponent.Posted _ -> true
    | CashFlowComponent.Staged _ -> false

let private confirmPaymentIsUnderInvoice
    (invoiceId: CashFlowComponent.InvoiceId)
    (payment: Payment.Payment)
    : Result<unit, IAppError> =
    if payment |> Payment.invoiceId = invoiceId then Ok ()
    else
        let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
        let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid))

/// lineAmount is the amount of the line a transaction pointer names. A Payment's amount is its line's; no caller
/// supplies one (REQ-CF-6.5, REQ-CF-9.8).
let lineAmount
    (context: Context.Context)
    (transactionPointer: CashFlowComponent.TransactionPointer)
    : Result<CashFlowComponent.PaymentAmount, IAppError> =
    match transactionPointer with
    | CashFlowComponent.Posted journalEntryLineId ->
        journalEntryLineId |> JournalEntryLine.fetchById context
        |> whenNoRows (LedgerError.JournalEntryLineIdDoesntExist (journalEntryLineId |> JournalEntryLineId.value))
        |> Result.map (fun line -> (CashFlowComponent.PaymentAmount.create (line |> JournalEntryLine.amount)))
    | CashFlowComponent.Staged stageEntryLineId ->
        stageEntryLineId |> StageEntryLine.fetchById context
        |> whenNoRows (DataIngestionError.IngestionStageEntryLineIdDoesntExist (stageEntryLineId |> StageEntryLineId.value))
        |> Result.map (fun line -> (CashFlowComponent.PaymentAmount.create (line |> StageEntryLine.amount)))

/// confirmPayment checks that the line a Payment points at exists, and that a posted-to-ledger date agrees with the
/// journal entry. The line may sit on any account: a Payment Agreement's accounts are an expectation, not a constraint.
let confirmPayment
    (context: Context.Context)
    (payment: Payment.Payment)
    : Result<unit, IAppError> =
    result {
        // JE and SE existence is checked below via whichever half of the transactionPointer is actually populated;
        // the other half isn't reachable off a reconstituted Payment (see transactionPointerFromColumns).
        let! journalEntryHeader =
            match payment |> Payment.transactionPointer with
            | CashFlowComponent.Posted journalEntryLineId ->
                // the pointer names a line, but the date checked below lives on the header, so this branch resolves
                // one hop further than the staged branch needs to
                let journalEntryLineUuid = journalEntryLineId |> JournalEntryLineId.value
                journalEntryLineId |> JournalEntryLine.fetchById context
                |> whenNoRows (LedgerError.JournalEntryLineIdDoesntExist journalEntryLineUuid)
                |> Result.bind (fun line ->
                    let headerId = line |> JournalEntryLine.journalEntryHeaderId
                    let journalEntryHeaderUuid = headerId |> JournalEntryHeaderId.value
                    headerId |> JournalEntryHeader.fetchById context
                    |> whenNoRows (LedgerError.JournalEntryHeaderIdDoesntExist journalEntryHeaderUuid)
                    |> Result.map Some)
            | CashFlowComponent.Staged stageEntryLineId ->
                let stageEntryLineUuid = stageEntryLineId |> StageEntryLineId.value
                stageEntryLineId |> StageEntryLine.fetchById context
                |> whenNoRows (DataIngestionError.IngestionStageEntryLineIdDoesntExist stageEntryLineUuid)
                |> Result.map (fun _ -> None)
        return!
            match payment |> Payment.postedToLedgerDate, journalEntryHeader with
            | None, _ -> Ok ()
            | Some _, None ->
                let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                Error(CashFlowError.CashflowPaymentPostedToLedgerDateWithoutJournalEntry paymentUuid)
            | Some providedDate, Some header ->
                let actualDate = header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                if (providedDate |> CashFlowComponent.PostedToLedgerDate.value) = actualDate then Ok ()
                else
                    let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                    Error(CashFlowError.CashflowPaymentPostedToLedgerDateMismatch(
                        paymentUuid, (providedDate |> CashFlowComponent.PostedToLedgerDate.value), actualDate))
    }

let private confirmFullyPaidAmountMatches
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.paymentState) <> CashFlowComponent.FullyPaid then Ok () else
    result {
        let! paidTotal = payments |> List.map Payment.amount |> List.map CashFlowComponent.PaymentAmount.value |> Money.sumList
        let invoiceAmount = invoice |> Invoice.amount
        return!
            if Money.isEqual paidTotal (invoiceAmount |> CashFlowComponent.InvoiceAmount.value) then Ok ()
            else
                let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
                let paidDec = paidTotal |> Money.amount
                let invoiceDec = invoiceAmount |> CashFlowComponent.InvoiceAmount.value |> Money.amount
                Error(CashFlowError.CashflowInvoiceFullyPaidAmountMismatch(invoiceUuid, paidDec, invoiceDec))
    }

let private confirmPartiallyPaidHasPayments
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.paymentState) <> CashFlowComponent.PartiallyPaid || (payments |> List.isEmpty |> not) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePartiallyPaidWithNoPayments invoiceUuid)

let private confirmPostedToLedgerRequiresAllPaymentsPosted
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.postedState) <> CashFlowComponent.PostedToLedger
       || (payments |> List.forall isPostedPayment) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePostedToLedgerWithUnpostedPayment invoiceUuid)

let private confirmPartiallyPostedHasAPostedPayment
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.postedState) <> CashFlowComponent.PartiallyPosted
       || (payments |> List.exists isPostedPayment) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePartiallyPostedWithNoPostedPayment invoiceUuid)

let private confirmInvoiceStateSuitsDirection
    (direction: CashFlowComponent.FlowDirection)
    (invoice: Invoice.Invoice)
    : Result<unit, IAppError> =
    let invoiceState = ((invoice |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.invoiceState)
    if invoiceState |> CashFlowComponent.InvoiceState.isValidFlowDirectionInvoiceStateCombination direction then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        CashFlowError.error (
            CashFlowError.CashflowInvoiceStateInvalidForFlowDirection(
                invoiceUuid,
                invoiceState |> CashFlowComponent.InvoiceState.toString,
                direction |> CashFlowComponent.FlowDirection.toString))

let private confirmInvoiceComposite
    (context: Context.Context)
    (invoiceComposite: InvoiceComposite)
    : Result<unit, IAppError> =
    let invoice = invoiceComposite.invoice
    let payments = invoiceComposite.payments
    let invoiceId = invoice |> Invoice.invoiceId
    result {
        do!
            payments
            |> List.map (confirmPaymentIsUnderInvoice invoiceId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let paymentAgreementId = invoice |> Invoice.paymentAgreementId
        let! paymentAgreement =
            let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
            paymentAgreementId |> PaymentAgreement.fetchById context
            |> whenNoRows (CashFlowError.CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
        let! masterAgreement =
            paymentAgreement |> PaymentAgreement.masterAgreementID |> MasterAgreement.fetchById context
        let direction = masterAgreement |> MasterAgreement.direction
        // the state check comes before anything that reads the direction, so a direction change that strands an
        // invoice's state is reported as that, not as whatever the new direction breaks downstream
        do! invoice |> confirmInvoiceStateSuitsDirection direction
        do! invoice |> Invoice.confirmAmountIsPositive
        do! confirmFullyPaidAmountMatches invoice payments
        do! invoice |> Invoice.confirmPostedToLedgerRequiresFullyPaid
        do! invoice |> Invoice.confirmFullyPaidHasNoBlocker
        do! confirmPartiallyPaidHasPayments invoice payments
        do! confirmPostedToLedgerRequiresAllPaymentsPosted invoice payments
        do! confirmPartiallyPostedHasAPostedPayment invoice payments
        return!
            payments
            |> List.map (confirmPayment context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private confirmInvoiceIsUnderInstance
    (instanceId: CashFlowComponent.InstanceId)
    (invoice: Invoice.Invoice)
    : Result<unit, IAppError> =
    if invoice |> Invoice.instanceId = instanceId then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        let instanceUuid = instanceId |> CashFlowComponent.InstanceId.value
        Error(CashFlowError.CashflowInvoiceNotUnderInstance(invoiceUuid, instanceUuid))

let private confirmInvoicePaymentAgreementIsUnderInstanceAgreement
    (context: Context.Context)
    (instanceAgreementId: CashFlowComponent.MasterAgreementId)
    (agreementPaymentAgreementIds: CashFlowComponent.PaymentAgreementId list)
    (invoice: Invoice.Invoice)
    : Result<unit, IAppError> =
    let paymentAgreementId = invoice |> Invoice.paymentAgreementId
    if agreementPaymentAgreementIds |> List.contains paymentAgreementId then Ok () else
    result {
        // this fetch only runs once the diamond is already known to be broken; it exists to name the other
        // MasterAgreement in the error, not to decide the check
        let! paymentAgreement =
            let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
            paymentAgreementId |> PaymentAgreement.fetchById context
            |> whenNoRows (CashFlowError.CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        let instanceAgreementUuid = instanceAgreementId |> CashFlowComponent.MasterAgreementId.value
        let paymentAgreementAgreementUuid =
            paymentAgreement |> PaymentAgreement.masterAgreementID |> CashFlowComponent.MasterAgreementId.value
        return!
            Error(CashFlowError.CashflowInvoiceDiamondMismatch(invoiceUuid, instanceAgreementUuid, paymentAgreementAgreementUuid))
    }

let private confirmDiamond
    (context: Context.Context)
    (instance: Instance.Instance)
    (invoices: Invoice.Invoice list)
    : Result<unit, IAppError> =
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
    : Result<unit, IAppError> =
    if instance |> Instance.isFulfilled |> not || (invoices |> List.isEmpty |> not) then Ok ()
    else
        let instanceUuid = instance |> Instance.instanceId |> CashFlowComponent.InstanceId.value
        Error(CashFlowError.CashflowInstanceFulfilledWithNoInvoices instanceUuid)

let private confirmFulfilledInstanceInvoicesAreFullyPaid
    (instance: Instance.Instance)
    (invoices: Invoice.Invoice list)
    : Result<unit, IAppError> =
    if instance |> Instance.isFulfilled |> not then Ok () else
    let instanceUuid = instance |> Instance.instanceId |> CashFlowComponent.InstanceId.value
    invoices
    |> List.map (fun invoice ->
        let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
        if (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.paymentState) = CashFlowComponent.FullyPaid || invoice |> Invoice.isCancelled then Ok ()
        else
            let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
            CashFlowError.error(CashFlowError.CashflowInstanceFulfilledWithUnpaidInvoice(instanceUuid, invoiceUuid)))
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmOneInvoicePerPaymentAgreement
    (instance: Instance.Instance)
    (invoices: Invoice.Invoice list)
    : Result<unit, IAppError> =
    let duplicated =
        invoices
        |> List.countBy Invoice.paymentAgreementId
        |> List.filter (fun (_, count) -> count > 1)
    match duplicated with
    | [] -> Ok ()
    | (paymentAgreementId, count) :: _ ->
        let instanceUuid = instance |> Instance.instanceId |> CashFlowComponent.InstanceId.value
        let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
        Error(CashFlowError.CashflowInstanceManyInvoicesForPaymentAgreement(instanceUuid, paymentAgreementUuid, count))

let confirmInstanceComposite
    (context: Context.Context)
    (instanceComposite: InstanceComposite)
    : Result<unit, IAppError> =
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
        do! confirmOneInvoicePerPaymentAgreement instance invoices
        do! confirmFulfilledInstanceHasInvoices instance invoices
        do! confirmFulfilledInstanceInvoicesAreFullyPaid instance invoices
        do!
            invoiceComposites
            |> List.map (confirmInvoiceComposite context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private compileInvoiceCompositesFromSubLists
    (invoices: Invoice.Invoice list)
    (payments: Payment.Payment list)
    : InvoiceComposite list =
    invoices
    |> List.map (fun inv ->
        let invId = inv |> Invoice.invoiceId
        let paymentsAtInv = payments |> List.filter (fun p -> p |> Payment.invoiceId = invId)
        { invoice = inv; payments = paymentsAtInv })

let compileInstanceCompositesFromSubLists
    (instances: Instance.Instance list)
    (invoices: Invoice.Invoice list)
    (payments: Payment.Payment list)
    : InstanceComposite list =
    let invoiceComposites = compileInvoiceCompositesFromSubLists invoices payments
    instances
    |> List.map (fun instance ->
        let instanceId = instance |> Instance.instanceId
        let compositesAtInstance =
            invoiceComposites
            |> List.filter (fun composite -> composite.invoice |> Invoice.instanceId = instanceId)
        { instance = instance; invoiceComposites = compositesAtInstance })

let fetchCompositeByInvoiceId
    (context: Context.Context)
    (invoiceId: CashFlowComponent.InvoiceId)
    : Result<InvoiceComposite, IAppError> =
    result {
        let! invoice = invoiceId |> Invoice.fetchById context
        let! payments = [ invoiceId ] |> Payment.fetchByInvoiceIdList context
        return { invoice = invoice; payments = payments }
    }

let fetchCompositeByInstanceId
    (context: Context.Context)
    (instanceId: CashFlowComponent.InstanceId)
    : Result<InstanceComposite, IAppError> =
    result {
        let! instance = instanceId |> Instance.fetchById context
        let! invoices = [ instanceId ] |> Invoice.fetchByInstanceIdList context
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments =
            if invoiceIds |> List.isEmpty then Ok [] else invoiceIds |> Payment.fetchByInvoiceIdList context
        return { instance = instance; invoiceComposites = compileInvoiceCompositesFromSubLists invoices payments }
    }

/// fetchOpenComposites returns every open Instance (neither fulfilled nor cancelled) with its Invoices and Payments,
/// ordered by Instance date, then Master Agreement name.
let fetchOpenComposites
    (context: Context.Context)
    : Result<InstanceComposite list, IAppError> =
    result {
        let! instances = Instance.fetchOpen context
        if instances |> List.isEmpty then return [] else
        let instanceIds = instances |> List.map Instance.instanceId
        let! invoices = instanceIds |> Invoice.fetchByInstanceIdList context
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments =
            if invoiceIds |> List.isEmpty then Ok [] else invoiceIds |> Payment.fetchByInvoiceIdList context
        return
            compileInstanceCompositesFromSubLists instances invoices payments
            |> List.sortBy (fun composite ->
                composite.instance |> Instance.instanceDate,
                composite.instance |> Instance.masterAgreementName |> CashFlowComponent.AgreementName.value)
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
    (paymentUpdates: Payment.PaymentFieldUpdates)
    : bool =
    paymentUpdates.journalEntryLineIdUpdate <> FieldUpdate.NoChange

type InvoiceCompositeUpdate = {
    invoiceUpdates: Invoice.InvoiceFieldUpdates
    paymentUpdates: Payment.PaymentFieldUpdates list
    paymentIdsToDelete: CashFlowComponent.PaymentId list
    newPayments: (
        CashFlowComponent.TransactionPointer *
        CashFlowComponent.PostedToFiDate option *
        CashFlowComponent.PostedToLedgerDate option *
        CashFlowComponent.PaymentMemo option) list
}

type InstanceCompositeUpdate = {
    instanceUpdates: Instance.InstanceFieldUpdates
    invoiceCompositeUpdates: InvoiceCompositeUpdate list
    newInvoices: (
        CashFlowComponent.PaymentAgreementId *
        CashFlowComponent.ExternalInvoiceId option *
        CashFlowComponent.InvoiceDate *
        CashFlowComponent.DueDate *
        CashFlowComponent.InvoiceAmount *
        CashFlowComponent.InvoiceState *
        CashFlowComponent.Blocker option *
        CashFlowComponent.InvoiceMemo option *
        ( // payments
            CashFlowComponent.TransactionPointer *
            CashFlowComponent.PostedToFiDate option *
            CashFlowComponent.PostedToLedgerDate option *
            CashFlowComponent.PaymentMemo option) list) list
}

let private isThereACompositeUpdate (compositeUpdate: InstanceCompositeUpdate) : bool =
    compositeUpdate.invoiceCompositeUpdates
       |> List.exists (fun invoiceCompositeUpdate ->
           invoiceCompositeUpdate.invoiceUpdates |> isThereAnInvoiceUpdate
           || invoiceCompositeUpdate.paymentUpdates |> List.exists isThereAPaymentUpdate
           || invoiceCompositeUpdate.paymentIdsToDelete |> List.isEmpty |> not
           || invoiceCompositeUpdate.newPayments |> List.isEmpty |> not)
    || compositeUpdate.newInvoices |> List.isEmpty |> not

let private derivePaymentState
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<CashFlowComponent.PaymentState, IAppError> =
    if payments |> List.isEmpty then Ok CashFlowComponent.NotYetPaid else
    result {
        let! paidTotal = payments |> List.map Payment.amount |> List.map CashFlowComponent.PaymentAmount.value |> Money.sumList
        let invoiceAmount = invoice |> Invoice.amount
        return
            if Money.isEqual paidTotal (invoiceAmount |> CashFlowComponent.InvoiceAmount.value) then CashFlowComponent.FullyPaid
            else CashFlowComponent.PartiallyPaid
    }

let private derivePostedState
    (paymentState: CashFlowComponent.PaymentState)
    (payments: Payment.Payment list)
    : CashFlowComponent.PostedState =
    let postedCount = payments |> List.filter isPostedPayment |> List.length
    if postedCount = 0 then CashFlowComponent.NotHandled
    elif postedCount = (payments |> List.length) && paymentState = CashFlowComponent.FullyPaid then
        CashFlowComponent.PostedToLedger
    else CashFlowComponent.PartiallyPosted

/// deriveIsFulfilled: at least one Invoice FullyPaid, and every Invoice FullyPaid or cancelled.
let private deriveIsFulfilled (invoiceComposites: InvoiceComposite list) : bool =
    let isFullyPaid (invoiceComposite: InvoiceComposite) =
        ((invoiceComposite.invoice |> Invoice.invoiceLifeCycleState) |> CashFlowComponent.InvoiceLifeCycleState.paymentState) = CashFlowComponent.FullyPaid
    invoiceComposites |> List.exists isFullyPaid
    && invoiceComposites
       |> List.forall (fun invoiceComposite -> isFullyPaid invoiceComposite || invoiceComposite.invoice |> Invoice.isCancelled)

let private withDerivedStates
    (paymentState: CashFlowComponent.PaymentState)
    (postedState: CashFlowComponent.PostedState)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates)
    : Invoice.InvoiceFieldUpdates =
    { invoiceUpdates with
        paymentStateUpdate = FieldUpdate.SetTo paymentState
        postedStateUpdate = FieldUpdate.SetTo postedState }

let private preConstructInvoiceComposite
    (context: Context.Context)
    (invoiceComposites: InvoiceComposite list)
    (invoiceCompositeUpdate: InvoiceCompositeUpdate)
    : Result<InvoiceComposite * Payment.Payment list, IAppError> =
    let invoiceId = invoiceCompositeUpdate.invoiceUpdates.invoiceIdToUpdate
    result {
        let! current =
            match
                invoiceComposites
                |> List.tryFind (fun candidate -> candidate.invoice |> Invoice.invoiceId = invoiceId)
            with
            | Some found -> Ok found
            | None ->
                let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
                CashFlowError.error(CashFlowError.CashflowInvoiceIdDoesntExist invoiceUuid)
        let! updatedPayments =
            current.payments
            |> List.filter (fun payment ->
                invoiceCompositeUpdate.paymentIdsToDelete |> List.contains (payment |> Payment.paymentId) |> not)
            |> List.map (fun payment ->
                let paymentId = payment |> Payment.paymentId
                match
                    invoiceCompositeUpdate.paymentUpdates
                    |> List.tryFind (fun paymentUpdate -> paymentUpdate.paymentIdToUpdate = paymentId)
                with
                | Some paymentUpdate -> payment |> Payment.applyFieldUpdates paymentUpdate
                | None -> Ok payment)
            |> convertListOfResultsToResultsList
        do!
            (invoiceCompositeUpdate.paymentUpdates |> List.map _.paymentIdToUpdate)
            @ invoiceCompositeUpdate.paymentIdsToDelete
            |> List.map (fun paymentId ->
                if current.payments |> List.exists (fun payment -> payment |> Payment.paymentId = paymentId) then Ok ()
                else
                    let paymentUuid = paymentId |> CashFlowComponent.PaymentId.value
                    let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
                    // a Payment that exists nowhere is not found; one that exists belongs to another Invoice
                    paymentId
                    |> Payment.fetchById context
                    |> whenNoRows (CashFlowError.CashflowPaymentIdDoesntExist paymentUuid)
                    |> Result.bind (fun _ ->
                        CashFlowError.error(CashFlowError.CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid))))
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let now = context |> Context.getInitiationInstant
        let! newPayments =
            invoiceCompositeUpdate.newPayments
            |> List.map (fun (transactionPointer, postedToFiDate, postedToLedgerDate, memo) ->
                transactionPointer
                |> lineAmount context
                |> Result.map (fun amount ->
                    let paymentId = CashFlowComponent.PaymentId.create ()
                    Payment.create paymentId invoiceId transactionPointer amount postedToFiDate postedToLedgerDate memo
                        now now))
            |> convertListOfResultsToResultsList
        let payments = updatedPayments @ newPayments
        let updatedInvoice = current.invoice |> Invoice.applyFieldUpdates invoiceCompositeUpdate.invoiceUpdates
        let! paymentState = derivePaymentState updatedInvoice payments
        let postedState = derivePostedState paymentState payments
        let derivedUpdates =
            invoiceCompositeUpdate.invoiceUpdates |> withDerivedStates paymentState postedState
        let invoice = current.invoice |> Invoice.applyFieldUpdates derivedUpdates
        return { invoice = invoice; payments = payments }, newPayments
    }

let private preConstructNewInvoiceComposite
    (context: Context.Context)
    (instanceId: CashFlowComponent.InstanceId)
    (newInvoice:
        CashFlowComponent.PaymentAgreementId *
        CashFlowComponent.ExternalInvoiceId option *
        CashFlowComponent.InvoiceDate *
        CashFlowComponent.DueDate *
        CashFlowComponent.InvoiceAmount *
        CashFlowComponent.InvoiceState *
        CashFlowComponent.Blocker option *
        CashFlowComponent.InvoiceMemo option *
        ( CashFlowComponent.TransactionPointer *
          CashFlowComponent.PostedToFiDate option *
          CashFlowComponent.PostedToLedgerDate option *
          CashFlowComponent.PaymentMemo option) list)
    : Result<InvoiceComposite, IAppError> =
    let paymentAgreementId, externalInvoiceId, invoiceDate, dueDate, amount, invoiceState, blocker, memo,
        paymentFieldsList = newInvoice
    result {
        let now = context |> Context.getInitiationInstant
        let invoiceId = CashFlowComponent.InvoiceId.create ()
        let! payments =
            paymentFieldsList
            |> List.map (fun (transactionPointer, postedToFiDate, postedToLedgerDate, paymentMemo) ->
                transactionPointer
                |> lineAmount context
                |> Result.map (fun paymentAmount ->
                    let paymentId = CashFlowComponent.PaymentId.create ()
                    Payment.create paymentId invoiceId transactionPointer paymentAmount postedToFiDate
                        postedToLedgerDate paymentMemo now now))
            |> convertListOfResultsToResultsList
        let invoiceWithLifeCycleState (lifeCycleState: CashFlowComponent.InvoiceLifeCycleState) =
            Invoice.create invoiceId instanceId paymentAgreementId externalInvoiceId invoiceDate dueDate amount
                lifeCycleState memo now now
        let preDerivation =
            invoiceWithLifeCycleState
                (CashFlowComponent.InvoiceLifeCycleState.create invoiceState CashFlowComponent.NotYetPaid CashFlowComponent.NotHandled blocker)
        let! paymentState = derivePaymentState preDerivation payments
        let postedState = derivePostedState paymentState payments
        let invoice =
            invoiceWithLifeCycleState
                (CashFlowComponent.InvoiceLifeCycleState.create invoiceState paymentState postedState blocker)
        return { invoice = invoice; payments = payments }
    }

/// updateInstanceComposite is the single door for editing an Instance and anything hanging off it. It assembles the
/// composite the package would produce, validates that, and only then writes -- payment state, posted state and
/// isFulfilled are derived here; no contract carries them (REQ-CF-9.11).
let updateInstanceComposite
    (context: Context.Context)
    (compositeUpdate: InstanceCompositeUpdate)
    : Result<InstanceComposite, IAppError> =
    result {
        do!
            if compositeUpdate |> isThereACompositeUpdate then Ok ()
            else CashFlowError.error CashFlowError.CashflowInstanceCompositeUpdateNoOp
        let instanceId = compositeUpdate.instanceUpdates.instanceIdToUpdate
        let! current =
            instanceId
            |> fetchCompositeByInstanceId context
            |> whenNoRows (CashFlowError.CashflowInstanceIdDoesntExist (instanceId |> CashFlowComponent.InstanceId.value))
        // cancellation is terminal: a cancelled Instance takes no change at all, and a cancelled Invoice takes no
        // update and no Payment
        do!
            if current.instance |> Instance.isCancelled |> not then Ok () else
            CashFlowError.error (CashFlowError.CashflowInstanceCancelled (instanceId |> CashFlowComponent.InstanceId.value))
        do!
            compositeUpdate.invoiceCompositeUpdates
            |> List.map (fun invoiceCompositeUpdate ->
                let invoiceId = invoiceCompositeUpdate.invoiceUpdates.invoiceIdToUpdate
                let isCancelled =
                    current.invoiceComposites
                    |> List.exists (fun invoiceComposite ->
                        invoiceComposite.invoice |> Invoice.invoiceId = invoiceId
                        && invoiceComposite.invoice |> Invoice.isCancelled)
                if isCancelled then
                    CashFlowError.error (CashFlowError.CashflowInvoiceCancelled (invoiceId |> CashFlowComponent.InvoiceId.value))
                else Ok ())
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let! preConstructed =
            compositeUpdate.invoiceCompositeUpdates
            |> List.map (preConstructInvoiceComposite context current.invoiceComposites)
            |> convertListOfResultsToResultsList
        let preConstructedInvoiceComposites = preConstructed |> List.map fst
        let touchedInvoiceIds =
            preConstructedInvoiceComposites
            |> List.map (fun invoiceComposite -> invoiceComposite.invoice |> Invoice.invoiceId)
        let untouchedInvoiceComposites =
            current.invoiceComposites
            |> List.filter (fun invoiceComposite ->
                touchedInvoiceIds |> List.contains (invoiceComposite.invoice |> Invoice.invoiceId) |> not)
        let! newInvoiceComposites =
            compositeUpdate.newInvoices
            |> List.map (preConstructNewInvoiceComposite context instanceId)
            |> convertListOfResultsToResultsList
        let invoiceComposites =
            preConstructedInvoiceComposites @ untouchedInvoiceComposites @ newInvoiceComposites
        let isFulfilled = invoiceComposites |> deriveIsFulfilled
        let instanceUpdates =
            { compositeUpdate.instanceUpdates with isFulfilledUpdate = FieldUpdate.SetTo isFulfilled }
        let instance = current.instance |> Instance.applyFieldUpdates instanceUpdates
        let preConstructedComposite = { instance = instance; invoiceComposites = invoiceComposites }
        do! preConstructedComposite |> confirmInstanceComposite context
        do!
            if isFulfilled <> (current.instance |> Instance.isFulfilled)
            then instanceUpdates |> Instance.update context |> Result.map ignore
            else Ok ()
        do!
            List.zip compositeUpdate.invoiceCompositeUpdates preConstructed
            |> List.map (fun (invoiceCompositeUpdate, (preConstructedInvoiceComposite, newPayments)) -> result {
                let lifeCycleState = preConstructedInvoiceComposite.invoice |> Invoice.invoiceLifeCycleState
                let invoiceUpdates =
                    invoiceCompositeUpdate.invoiceUpdates
                    |> withDerivedStates (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.paymentState) (lifeCycleState |> CashFlowComponent.InvoiceLifeCycleState.postedState)
                do! invoiceUpdates |> Invoice.update context |> Result.map ignore
                do!
                    invoiceCompositeUpdate.paymentUpdates
                    |> List.filter isThereAPaymentUpdate
                    |> List.map (fun paymentUpdate -> paymentUpdate |> Payment.update context |> Result.map ignore)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
                do!
                    invoiceCompositeUpdate.paymentIdsToDelete
                    |> List.map (Payment.delete context)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
                return!
                    newPayments
                    |> List.map (Payment.persist context)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore })
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do!
            newInvoiceComposites
            |> List.map (fun newInvoiceComposite -> result {
                do! newInvoiceComposite.invoice |> Invoice.persist context
                return!
                    newInvoiceComposite.payments
                    |> List.map (Payment.persist context)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore })
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let! fetched = instanceId |> fetchCompositeByInstanceId context
        do! fetched |> confirmInstanceComposite context
        return fetched
    }
    
let private confirmInstanceDateIsAfterLatestInstance
    (context: Context.Context)
    (masterAgreementID: CashFlowComponent.MasterAgreementId)
    (instanceDate: LocalDate)
    : Result<unit, IAppError> =
    result {
        let! existingInstances = [ masterAgreementID ] |> Instance.fetchByMasterAgreementIdList context
        let existingDates = existingInstances |> List.map Instance.instanceDate
        if existingDates |> List.isEmpty then return () else
        let latestDate = existingDates |> List.max
        if instanceDate > latestDate then return () else
        let agreementUuid = masterAgreementID |> CashFlowComponent.MasterAgreementId.value
        return! Error (CashFlowError.CashflowInstanceDateNotAfterLatestInstance(agreementUuid, instanceDate, latestDate))
    }

/// constructNewAndPersist takes each Invoice's invoice state and blocker only. Payment state, posted state
/// and is-fulfilled are derived from the Payments (REQ-CF-9.8 through 9.11), the same way adding an Invoice to an
/// existing Instance derives them.
let constructNewAndPersist
    (context: Context.Context)
    (masterAgreementID: CashFlowComponent.MasterAgreementId)
    (instanceDate: LocalDate)
    (newInvoices: (
        CashFlowComponent.PaymentAgreementId *
        CashFlowComponent.ExternalInvoiceId option *
        CashFlowComponent.InvoiceDate *
        CashFlowComponent.DueDate *
        CashFlowComponent.InvoiceAmount *
        CashFlowComponent.InvoiceState *
        CashFlowComponent.Blocker option *
        CashFlowComponent.InvoiceMemo option *
        ( // payments
            CashFlowComponent.TransactionPointer *
            CashFlowComponent.PostedToFiDate option *
            CashFlowComponent.PostedToLedgerDate option *
            CashFlowComponent.PaymentMemo option) list
        ) list)
    : Result<InstanceComposite, IAppError> =
    result {
        let! masterAgreement = masterAgreementID |> MasterAgreement.fetchById context
        let cadenceType = masterAgreement |> MasterAgreement.cadence |> Cadence.cadenceType
        do! instanceDate |> Cadence.confirmDateFitsCadenceType cadenceType
        do! instanceDate |> confirmInstanceDateIsAfterLatestInstance context masterAgreementID
        let instanceId = CashFlowComponent.InstanceId.create()
        let now = context |> Context.getInitiationInstant
        let masterAgreementName = masterAgreement |> MasterAgreement.agreementName
        let! invoiceComposites =
            newInvoices
            |> List.map (preConstructNewInvoiceComposite context instanceId)
            |> convertListOfResultsToResultsList
        let isFulfilled = invoiceComposites |> deriveIsFulfilled
        let newInstance =
            Instance.create instanceId masterAgreementID masterAgreementName instanceDate isFulfilled now now
        let instanceComposite = { instance = newInstance; invoiceComposites = invoiceComposites }
        do! instanceComposite |> confirmInstanceComposite context
        do! newInstance |> Instance.persist context
        do! invoiceComposites
            |> List.map(fun invoiceComposite -> result {
                do! invoiceComposite.invoice |> Invoice.persist context
                do! invoiceComposite.payments
                    |> List.map(fun payment -> payment |> Payment.persist context)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
                return () }
                )
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let newNextInstance = Cadence.determineNextDateFromPrior instanceDate cadenceType
        let! newCadence = Cadence.create cadenceType { nextInstance = newNextInstance }
        do! masterAgreement |> MasterAgreement.updateCadence context newCadence |> Result.map ignore
        return instanceComposite
    }
    

/// cancelInstance cancels an Instance and every Invoice it holds, each with the Instance's reason note. It is refused
/// while any of its Invoices has a Payment, naming those Invoices. An Invoice already cancelled keeps its own note.
let cancelInstance
    (context: Context.Context)
    (note: CashFlowComponent.CancellationReasonNote)
    (instanceId: CashFlowComponent.InstanceId)
    : Result<InstanceComposite, IAppError> =
    result {
        let instanceUuid = instanceId |> CashFlowComponent.InstanceId.value
        let! current =
            instanceId
            |> fetchCompositeByInstanceId context
            |> whenNoRows (CashFlowError.CashflowInstanceIdDoesntExist instanceUuid)
        do!
            if current.instance |> Instance.isCancelled then
                CashFlowError.error (CashFlowError.CashflowInstanceCancelled instanceUuid)
            else Ok ()
        let paidInvoiceUuids =
            current.invoiceComposites
            |> List.filter (fun invoiceComposite -> invoiceComposite.payments |> List.isEmpty |> not)
            |> List.map (fun invoiceComposite -> invoiceComposite.invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value)
        do!
            if paidInvoiceUuids |> List.isEmpty then Ok ()
            else CashFlowError.error (CashFlowError.CashflowInstanceCancellationBlockedByPayments(instanceUuid, paidInvoiceUuids))
        do! instanceId |> Instance.cancel context note
        do!
            current.invoiceComposites
            |> List.map (fun invoiceComposite -> invoiceComposite.invoice)
            |> List.filter (Invoice.isCancelled >> not)
            |> List.map (fun invoice -> invoice |> Invoice.invoiceId |> Invoice.cancel context note)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let! fetched = instanceId |> fetchCompositeByInstanceId context
        do! fetched |> confirmInstanceComposite context
        return fetched
    }

/// cancelInvoice cancels one Invoice with its reason note, refused while it has a Payment. Its Instance's is-fulfilled
/// is re-derived, since a cancelled Invoice no longer holds the Instance open.
let cancelInvoice
    (context: Context.Context)
    (note: CashFlowComponent.CancellationReasonNote)
    (invoiceId: CashFlowComponent.InvoiceId)
    : Result<InstanceComposite, IAppError> =
    result {
        let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
        let! invoiceComposite =
            invoiceId
            |> fetchCompositeByInvoiceId context
            |> whenNoRows (CashFlowError.CashflowInvoiceIdDoesntExist invoiceUuid)
        let instanceId = invoiceComposite.invoice |> Invoice.instanceId
        let! current = instanceId |> fetchCompositeByInstanceId context
        do!
            if current.instance |> Instance.isCancelled then
                CashFlowError.error (CashFlowError.CashflowInstanceCancelled (instanceId |> CashFlowComponent.InstanceId.value))
            else Ok ()
        do!
            if invoiceComposite.invoice |> Invoice.isCancelled then
                CashFlowError.error (CashFlowError.CashflowInvoiceCancelled invoiceUuid)
            else Ok ()
        do!
            if invoiceComposite.payments |> List.isEmpty then Ok ()
            else CashFlowError.error (CashFlowError.CashflowInvoiceCancellationBlockedByPayments invoiceUuid)
        do! invoiceId |> Invoice.cancel context note
        let! cancelled = instanceId |> fetchCompositeByInstanceId context
        let isFulfilled = cancelled.invoiceComposites |> deriveIsFulfilled
        let fulfilledUpdate: Instance.InstanceFieldUpdates =
            { instanceIdToUpdate = instanceId
              isFulfilledUpdate = FieldUpdate.SetTo isFulfilled }
        do!
            if isFulfilled = (cancelled.instance |> Instance.isFulfilled) then Ok ()
            else fulfilledUpdate |> Instance.update context |> Result.map ignore
        let! fetched = instanceId |> fetchCompositeByInstanceId context
        do! fetched |> confirmInstanceComposite context
        return fetched
    }
