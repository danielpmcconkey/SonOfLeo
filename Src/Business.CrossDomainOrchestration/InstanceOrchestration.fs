module Business.CrossDomainOrchestration.InstanceOrchestration

open NodaTime
open App.Utility.IAppError
open App.Utility.FieldUpdate
open App.Utility.Result
open App.DataAccessLayer
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Classification
open Business.CrossDomainOrchestration.CashFlowCompositeFetcher
open Business.CrossDomainOrchestration.FetchFilters

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

/// confirmPayment checks the account its line sits on, not the line type. A classification rule may deliberately claim
/// the opposite leg -- an Outgo agreement taking a refund matches a Credit line -- and an operator repointing a payment
/// by hand can do the same.
let confirmPayment
    (context: Context.Context)
    (expectedAccountId: AccountComponent.AccountId)
    (payment: Payment.Payment)
    : Result<unit, IAppError> =
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
                    | Error e ->
                        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                        then
                            let journalEntryHeaderUuid = headerId |> JournalEntryHeaderId.value
                            LedgerError.error(LedgerError.JournalEntryHeaderIdDoesntExist journalEntryHeaderUuid)
                        else Error e
                | Error e ->
                    if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                    then
                        let journalEntryLineUuid = journalEntryLineId |> JournalEntryLineId.value
                        LedgerError.error(LedgerError.JournalEntryLineIdDoesntExist journalEntryLineUuid)
                    else Error e
            | CashFlowComponent.Staged stageEntryLineId ->
                match stageEntryLineId |> StageEntryLine.fetchById context with
                | Ok line -> Ok(None, line |> StageEntryLine.accountId)
                | Error e ->
                    if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                    then
                        let stageEntryLineUuid = stageEntryLineId |> StageEntryLineId.value
                        Error (DataIngestionError.IngestionStageEntryLineIdDoesntExist stageEntryLineUuid)
                    else Error e
        do!
            if lineAccountId = Some expectedAccountId then Ok ()
            else
                let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                let actualAccountUuid = lineAccountId |> Option.map AccountComponent.AccountId.value
                let expectedAccountUuid = expectedAccountId |> AccountComponent.AccountId.value
                Error(CashFlowError.CashflowPaymentLineNotOnAgreementAccount(
                    paymentUuid, actualAccountUuid, expectedAccountUuid))
        return!
            match payment |> Payment.postedToLedgerDate, journalEntryHeader with
            | None, _ -> Ok ()
            | Some _, None ->
                let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                Error(CashFlowError.CashflowPaymentPostedToLedgerDateWithoutJournalEntry paymentUuid)
            | Some providedDate, Some header ->
                let actualDate = header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                if providedDate.localDate = actualDate then Ok ()
                else
                    let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                    Error(CashFlowError.CashflowPaymentPostedToLedgerDateMismatch(
                        paymentUuid, providedDate.localDate, actualDate))
    }

let private confirmInvoiceAmountIsPositive
    (invoice: Invoice.Invoice)
    : Result<unit, IAppError> =
    let invoiceAmount = invoice |> Invoice.amount
    let invoiceAmountDecimal = invoiceAmount.money |> Money.amount
    if invoiceAmountDecimal > 0M then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoiceNonPositiveAmount(invoiceUuid, invoiceAmountDecimal))

let private confirmFullyPaidAmountMatches
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
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
                Error(CashFlowError.CashflowInvoiceFullyPaidAmountMismatch(invoiceUuid, paidDec, invoiceDec))
    }

let private confirmPostedToLedgerRequiresFullyPaid
    (invoice: Invoice.Invoice)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.postedState <> CashFlowComponent.PostedToLedger
       || lifeCycleState.paymentState = CashFlowComponent.FullyPaid then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePostedToLedgerRequiresFullyPaid invoiceUuid)

let private confirmFullyPaidHasNoBlocker
    (invoice: Invoice.Invoice)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.paymentState <> CashFlowComponent.FullyPaid || lifeCycleState.blocker |> Option.isNone then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoiceFullyPaidWithBlocker invoiceUuid)

let private confirmPartiallyPaidHasPayments
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.paymentState <> CashFlowComponent.PartiallyPaid || (payments |> List.isEmpty |> not) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePartiallyPaidWithNoPayments invoiceUuid)

let private confirmPostedToLedgerRequiresAllPaymentsPosted
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.postedState <> CashFlowComponent.PostedToLedger
       || (payments |> List.forall isPostedPayment) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePostedToLedgerWithUnpostedPayment invoiceUuid)

let private confirmPartiallyPostedHasAPostedPayment
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<unit, IAppError> =
    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
    if lifeCycleState.postedState <> CashFlowComponent.PartiallyPosted
       || (payments |> List.exists isPostedPayment) then Ok ()
    else
        let invoiceUuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
        Error(CashFlowError.CashflowInvoicePartiallyPostedWithNoPostedPayment invoiceUuid)

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
                    | Error e ->
                        if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                        then
                            let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                            Error (CashFlowError.CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
                        else Error e
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
            match paymentAgreementId |> PaymentAgreement.fetchById context with
            | Ok pa -> Ok pa
            | Error e ->
                if e.DomainName = nameof DalError && e.CaseName = nameof DalError.DalResultantRowsDidntMatchExpectation
                then
                    let paymentAgreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                    CashFlowError.error(CashFlowError.CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
                else Error e
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
        if lifeCycleState.paymentState = CashFlowComponent.FullyPaid then Ok ()
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

let fetchFiltered
    (context: Context.Context)
    (expectedRows: AcceptableExpectedRows)
    (filter: AgreementFilter)
    : Result<InvoiceComposite list, IAppError> =
    result {
        let! invoices =
            filter |> fetchCompositeFiltered context expectedRows Invoice.query TargetComposite.Invoice
        if invoices |> List.isEmpty then return [] else
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments = invoiceIds |> Payment.fetchByInvoiceIdList context
        return compileInvoiceCompositesFromSubLists invoices payments
    }

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

let fetchCompositesByIsFulfilled
    (context: Context.Context)
    (isFulfilled: bool)
    : Result<InstanceComposite list, IAppError> =
    result {
        let! instances = isFulfilled |> Instance.fetchByIsFulfilled context
        if instances |> List.isEmpty then return [] else
        let instanceIds = instances |> List.map Instance.instanceId
        let! invoices = instanceIds |> Invoice.fetchByInstanceIdList context
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments =
            if invoiceIds |> List.isEmpty then Ok [] else invoiceIds |> Payment.fetchByInvoiceIdList context
        return compileInstanceCompositesFromSubLists instances invoices payments
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
    paymentIdsToDelete: CashFlowComponent.PaymentId list
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
            CashFlowComponent.PaymentAmount *
            CashFlowComponent.PostedToFiDate option *
            CashFlowComponent.PostedToLedgerDate option *
            CashFlowComponent.PaymentMemo option) list) list
}

let private isThereACompositeUpdate (compositeUpdate: InstanceCompositeUpdate) : bool =
    compositeUpdate.instanceUpdates.instanceDateUpdate <> FieldUpdate.NoChange
    || compositeUpdate.invoiceCompositeUpdates
       |> List.exists (fun invoiceCompositeUpdate ->
           invoiceCompositeUpdate.invoiceUpdates |> isThereAnInvoiceUpdate
           || invoiceCompositeUpdate.paymentUpdates |> List.exists isThereAPaymentUpdate
           || invoiceCompositeUpdate.paymentIdsToDelete |> List.isEmpty |> not
           || invoiceCompositeUpdate.newPayments |> List.isEmpty |> not)
    || compositeUpdate.newInvoices |> List.isEmpty |> not

let private confirmNoDerivedFieldIsSet (compositeUpdate: InstanceCompositeUpdate) : Result<unit, IAppError> =
    let setDerivedFields =
        [ if compositeUpdate.instanceUpdates.isFulfilledUpdate <> FieldUpdate.NoChange then "isFulfilled"
          for invoiceCompositeUpdate in compositeUpdate.invoiceCompositeUpdates do
              if invoiceCompositeUpdate.invoiceUpdates.paymentStateUpdate <> FieldUpdate.NoChange then "paymentState"
              if invoiceCompositeUpdate.invoiceUpdates.postedStateUpdate <> FieldUpdate.NoChange then "postedState" ]
    match setDerivedFields with
    | [] -> Ok ()
    | fieldName :: _ -> Error(CashFlowError.CashflowInstanceCompositeDerivedFieldSet fieldName)

let private derivePaymentState
    (invoice: Invoice.Invoice)
    (payments: Payment.Payment list)
    : Result<CashFlowComponent.PaymentState, IAppError> =
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
                    CashFlowError.error(CashFlowError.CashflowPaymentNotUnderInvoice(paymentUuid, invoiceUuid)))
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
          CashFlowComponent.PaymentAmount *
          CashFlowComponent.PostedToFiDate option *
          CashFlowComponent.PostedToLedgerDate option *
          CashFlowComponent.PaymentMemo option) list)
    : Result<InvoiceComposite, IAppError> =
    let paymentAgreementId, externalInvoiceId, invoiceDate, dueDate, amount, invoiceState, blocker, memo,
        paymentFieldsList = newInvoice
    result {
        let now = context |> Context.getInitiationInstant
        let invoiceId = CashFlowComponent.InvoiceId.create ()
        let payments =
            paymentFieldsList
            |> List.map (fun (transactionPointer, paymentAmount, postedToFiDate, postedToLedgerDate, paymentMemo) ->
                let paymentId = CashFlowComponent.PaymentId.create ()
                Payment.create paymentId invoiceId transactionPointer paymentAmount postedToFiDate postedToLedgerDate
                    paymentMemo now now)
        let invoiceWithLifeCycleState (lifeCycleState: CashFlowComponent.InvoiceLifeCycleState) =
            Invoice.create invoiceId instanceId paymentAgreementId externalInvoiceId invoiceDate dueDate amount
                lifeCycleState memo now now
        let preDerivation =
            invoiceWithLifeCycleState
                { invoiceState = invoiceState
                  paymentState = CashFlowComponent.NotYetPaid
                  postedState = CashFlowComponent.NotHandled
                  blocker = blocker }
        let! paymentState = derivePaymentState preDerivation payments
        let postedState = derivePostedState paymentState payments
        let invoice =
            invoiceWithLifeCycleState
                { invoiceState = invoiceState
                  paymentState = paymentState
                  postedState = postedState
                  blocker = blocker }
        return { invoice = invoice; payments = payments }
    }

/// updateInstanceComposite is the single door for editing an Instance and anything hanging off it. It assembles the
/// composite the package would produce, validates that, and only then writes -- payment state, posted state and
/// isFulfilled are derived here, so a package that sets them is rejected rather than obeyed.
let updateInstanceComposite
    (context: Context.Context)
    (compositeUpdate: InstanceCompositeUpdate)
    : Result<InstanceComposite, IAppError> =
    result {
        do! compositeUpdate |> confirmNoDerivedFieldIsSet
        do!
            if compositeUpdate |> isThereACompositeUpdate then Ok ()
            else CashFlowError.error CashFlowError.CashflowInstanceCompositeUpdateNoOp
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
    : Result<InstanceComposite, IAppError> =
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
    


