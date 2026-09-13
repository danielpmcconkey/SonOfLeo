module ModelOrchestrator.InstanceOrchestration

open DataAccessLayer.ExecuteReader
open Model
open Model.CashFlow
open Model.DataIngestion
open Model.DataIngestion.StageEntryComponent
open Model.Ledger
open Model.Ledger.JournalEntryComponent
open Model.StageDataClassification
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

type PaymentAgreementClassificationResult = {
    runId: StageDataClassificationComponent.ClassificationRunId
    classificationResults: StageDataClassificationComponent.ClassificationResult list
    decisionLog: StageDataClassificationComponent.PaymentAgreementDecision list
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
    : Result<unit, AppError> =
    if payment |> Payment.invoiceId = invoiceId then Ok ()
    else
        let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
        let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid))

/// confirmPayment checks the account its line sits on, not the line type. A classification rule may deliberately claim
/// the opposite leg -- an Outgo agreement taking a refund matches a Credit line -- and an operator repointing a payment
/// by hand can do the same.
let confirmPayment
    (context: Context.Context)
    (expectedAccountId: AccountComponent.AccountId)
    (payment: Payment.Payment)
    : Result<unit, AppError> =
    result {
        // JE and SE existence is checked below via whichever half of the transactionPointer is actually populated;
        // the other half isn't reachable off a reconstituted Payment (see transactionPointerFromColumns).
        let! journalEntryHeader, lineAccountId =
            match payment |> Payment.transactionPointer with
            | CashFlowComponent.Posted journalEntryLineId ->
                // the pointer names a line, but the date checked below lives on the header, so this branch resolves
                // one hop further than the staged branch needs to
                match journalEntryLineId |> JournalEntryLine.fetchById context with
                | Ok line ->
                    let headerId = line |> JournalEntryLine.journalEntryHeaderId
                    let accountId = line |> JournalEntryLine.accountId
                    match headerId |> JournalEntryHeader.fetchById context with
                    | Ok header -> Ok(Some header, Some accountId)
                    | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                        let journalEntryHeaderUuid = headerId |> JournalEntryHeaderId.value
                        Error(JournalEntryHeaderIdDoesntExist journalEntryHeaderUuid)
                    | Error e -> Error e
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    let journalEntryLineUuid = journalEntryLineId |> JournalEntryLineId.value
                    Error(JournalEntryLineIdDoesntExist journalEntryLineUuid)
                | Error e -> Error e
            | CashFlowComponent.Staged stageEntryLineId ->
                match stageEntryLineId |> StageEntryLine.fetchById context with
                | Ok line -> Ok(None, line |> StageEntryLine.accountId)
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    let stageEntryLineUuid = stageEntryLineId |> StageEntryLineId.value
                    Error(IngestionStageEntryLineIdDoesntExist stageEntryLineUuid)
                | Error e -> Error e
        do!
            if lineAccountId = Some expectedAccountId then Ok ()
            else
                let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                let actualAccountUuid = lineAccountId |> Option.map AccountComponent.AccountId.value
                let expectedAccountUuid = expectedAccountId |> AccountComponent.AccountId.value
                Error(CashflowPaymentLineNotOnAgreementAccount(paymentUuid, actualAccountUuid, expectedAccountUuid))
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
            if payments |> List.isEmpty then Ok () else
            result {
                let paymentAgreementId = invoice |> Invoice.paymentAgreementId
                let! paymentAgreement =
                    match paymentAgreementId |> PaymentAgreement.fetchById context with
                    | Ok fetched -> Ok fetched
                    | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                        let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                        Error(CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
                    | Error e -> Error e
                let! masterAgreement =
                    paymentAgreement |> PaymentAgreement.masterAgreementID |> MasterAgreement.fetchById context
                let direction = masterAgreement |> MasterAgreement.direction
                let expectedAccountId = paymentAgreement |> PaymentAgreement.accountIdForFlowDirection direction
                return!
                    payments
                    |> List.map (confirmPayment context expectedAccountId)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
            }
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
            filter |> fetchCompositeFiltered context expectedRows Invoice.query TargetComposite.Invoice
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

let fetchCompositesByIsFulfilled
    (context: Context.Context)
    (isFulfilled: bool)
    : Result<InstanceComposite list, AppError> =
    result {
        let! instances = isFulfilled |> Instance.fetchByIsFulfilled context
        if instances |> List.isEmpty then return [] else
        let instanceIds = instances |> List.map Instance.instanceId
        let! invoices = instanceIds |> Invoice.fetchByInstanceIdList context
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments =
            if invoiceIds |> List.isEmpty then Ok [] else invoiceIds |> Payment.fetchByInvoiceIdList context
        let invoiceComposites = compileFromSubLists invoices payments
        return
            instances
            |> List.map (fun instance ->
                let instanceId = instance |> Instance.instanceId
                let compositesAtInstance =
                    invoiceComposites
                    |> List.filter (fun composite -> composite.invoice |> Invoice.instanceId = instanceId)
                { instance = instance; invoiceComposites = compositesAtInstance })
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
    || paymentUpdates.stageEntryLineIdUpdate <> FieldUpdate.NoChange
    || paymentUpdates.postedToFiDateUpdate <> FieldUpdate.NoChange
    || paymentUpdates.memoUpdate <> FieldUpdate.NoChange

type InvoiceCompositeUpdate = {
    invoiceUpdates: Invoice.InvoiceFieldUpdates
    paymentUpdates: Payment.PaymentFieldUpdates list
    newPayments: (
        CashFlowComponent.TransactionPointer *
        CashFlowComponent.PaymentAmount *
        CashFlowComponent.PostedToFiDate option *
        CashFlowComponent.PostedToLedgerDate option *
        CashFlowComponent.PaymentMemo option) list
}

type InstanceCompositeUpdate = {
    instanceUpdates: Instance.InstanceFieldUpdates
    invoiceCompositeUpdates: InvoiceCompositeUpdate list
}

let private isThereACompositeUpdate (compositeUpdate: InstanceCompositeUpdate) : bool =
    compositeUpdate.instanceUpdates.instanceDateUpdate <> FieldUpdate.NoChange
    || compositeUpdate.invoiceCompositeUpdates
       |> List.exists (fun invoiceCompositeUpdate ->
           invoiceCompositeUpdate.invoiceUpdates |> isThereAnInvoiceUpdate
           || invoiceCompositeUpdate.paymentUpdates |> List.exists isThereAPaymentUpdate
           || invoiceCompositeUpdate.newPayments |> List.isEmpty |> not)

let private confirmNoDerivedFieldIsSet (compositeUpdate: InstanceCompositeUpdate) : Result<unit, AppError> =
    let setDerivedFields =
        [ if compositeUpdate.instanceUpdates.isFulfilledUpdate <> FieldUpdate.NoChange then "isFulfilled"
          for invoiceCompositeUpdate in compositeUpdate.invoiceCompositeUpdates do
              if invoiceCompositeUpdate.invoiceUpdates.paymentStateUpdate <> FieldUpdate.NoChange then "paymentState"
              if invoiceCompositeUpdate.invoiceUpdates.postedStateUpdate <> FieldUpdate.NoChange then "postedState" ]
    match setDerivedFields with
    | [] -> Ok ()
    | fieldName :: _ -> Error(CashflowInstanceCompositeDerivedFieldSet fieldName)

let private derivePaymentState
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<CashFlowComponent.PaymentState, AppError> =
    if payments |> List.isEmpty then Ok CashFlowComponent.NotYetPaid else
    result {
        let! paidTotal = payments |> List.map Payment.amount |> List.map _.money |> Money.sumList
        let paidDecimal = paidTotal |> Money.amount
        let invoiceAmount = invoice |> Invoice.amount
        let invoiceDecimal = invoiceAmount.money |> Money.amount
        return
            if paidDecimal = invoiceDecimal then CashFlowComponent.FullyPaid
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

let private deriveIsFulfilled (invoiceComposites: InvoiceComposite list) : bool =
    if invoiceComposites |> List.isEmpty then false else
    invoiceComposites
    |> List.forall (fun invoiceComposite ->
        let lifeCycleState = invoiceComposite.invoice |> Invoice.invoiceLifeCycleState
        lifeCycleState.paymentState = CashFlowComponent.FullyPaid)

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
    : Result<InvoiceComposite * Payment.Payment list, AppError> =
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
                Error(CashflowInvoiceIdDoesntExist invoiceUuid)
        let! updatedPayments =
            current.payments
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
            invoiceCompositeUpdate.paymentUpdates
            |> List.map (fun paymentUpdate ->
                let paymentId = paymentUpdate.paymentIdToUpdate
                if current.payments |> List.exists (fun payment -> payment |> Payment.paymentId = paymentId) then Ok ()
                else
                    let paymentUuid = paymentId |> CashFlowComponent.PaymentId.value
                    let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
                    Error(CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid)))
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let now = context |> Context.getInitiationInstant
        let newPayments =
            invoiceCompositeUpdate.newPayments
            |> List.map (fun (transactionPointer, amount, postedToFiDate, postedToLedgerDate, memo) ->
                let paymentId = CashFlowComponent.PaymentId.create ()
                Payment.create paymentId invoiceId transactionPointer amount postedToFiDate postedToLedgerDate memo
                    now now)
        let payments = updatedPayments @ newPayments
        let updatedInvoice = current.invoice |> Invoice.applyFieldUpdates invoiceCompositeUpdate.invoiceUpdates
        let! paymentState = derivePaymentState updatedInvoice payments
        let postedState = derivePostedState paymentState payments
        let derivedUpdates =
            invoiceCompositeUpdate.invoiceUpdates |> withDerivedStates paymentState postedState
        let invoice = current.invoice |> Invoice.applyFieldUpdates derivedUpdates
        return { invoice = invoice; payments = payments }, newPayments
    }

/// updateInstanceComposite is the single door for editing an Instance and anything hanging off it. It assembles the
/// composite the package would produce, validates that, and only then writes -- payment state, posted state and
/// isFulfilled are derived here, so a package that sets them is rejected rather than obeyed.
let updateInstanceComposite
    (context: Context.Context)
    (compositeUpdate: InstanceCompositeUpdate)
    : Result<InstanceComposite, AppError> =
    result {
        do! compositeUpdate |> confirmNoDerivedFieldIsSet
        do!
            if compositeUpdate |> isThereACompositeUpdate then Ok ()
            else Error CashflowInstanceCompositeUpdateNoOp
        let instanceId = compositeUpdate.instanceUpdates.instanceIdToUpdate
        let! current = instanceId |> fetchCompositeByInstanceId context
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
        let invoiceComposites = preConstructedInvoiceComposites @ untouchedInvoiceComposites
        let isFulfilled = invoiceComposites |> deriveIsFulfilled
        let instanceUpdates =
            { compositeUpdate.instanceUpdates with isFulfilledUpdate = FieldUpdate.SetTo isFulfilled }
        let instance = current.instance |> Instance.applyFieldUpdates instanceUpdates
        let preConstructedComposite = { instance = instance; invoiceComposites = invoiceComposites }
        do! preConstructedComposite |> confirmInstanceComposite context
        do!
            if instanceUpdates.instanceDateUpdate <> FieldUpdate.NoChange
               || isFulfilled <> (current.instance |> Instance.isFulfilled)
            then instanceUpdates |> Instance.update context |> Result.map ignore
            else Ok ()
        do!
            List.zip compositeUpdate.invoiceCompositeUpdates preConstructed
            |> List.map (fun (invoiceCompositeUpdate, (preConstructedInvoiceComposite, newPayments)) -> result {
                let lifeCycleState = preConstructedInvoiceComposite.invoice |> Invoice.invoiceLifeCycleState
                let invoiceUpdates =
                    invoiceCompositeUpdate.invoiceUpdates
                    |> withDerivedStates lifeCycleState.paymentState lifeCycleState.postedState
                do! invoiceUpdates |> Invoice.update context |> Result.map ignore
                do!
                    invoiceCompositeUpdate.paymentUpdates
                    |> List.filter isThereAPaymentUpdate
                    |> List.map (fun paymentUpdate -> paymentUpdate |> Payment.update context |> Result.map ignore)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
                return!
                    newPayments
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
    : Result<unit, AppError> =
    result {
        let! existingInstances = [ masterAgreementID ] |> Instance.fetchByMasterAgreementIdList context
        let existingDates = existingInstances |> List.map Instance.instanceDate
        if existingDates |> List.isEmpty then return () else
        let latestDate = existingDates |> List.max
        if instanceDate > latestDate then return () else
        let agreementUuid = masterAgreementID |> CashFlowComponent.MasterAgreementId.value
        return! Error (CashflowInstanceDateNotAfterLatestInstance(agreementUuid, instanceDate, latestDate))
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
        let! masterAgreement = masterAgreementID |> MasterAgreement.fetchById context
        let cadenceType = masterAgreement |> MasterAgreement.cadence |> Cadence.cadenceType
        do! instanceDate |> Cadence.confirmDateFitsCadenceType cadenceType
        do! instanceDate |> confirmInstanceDateIsAfterLatestInstance context masterAgreementID
        let instanceId = CashFlowComponent.InstanceId.create()
        let now = context |> Context.getInitiationInstant
        let masterAgreementName = masterAgreement |> MasterAgreement.agreementName
        let newInstance =
            Instance.create instanceId masterAgreementID masterAgreementName instanceDate isFulfilled now now
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
    


