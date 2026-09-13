module InterfaceBridge.BoundaryConverters.CashFlowFieldConverters

open InterfaceBridge.BoundaryConverters.CashFlowLookupConverters
open InterfaceBridge.BoundaryConverters.ClassificationFieldConverters
open InterfaceBridge.InterfaceContracts.CashFlowContracts
open Model
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.DataIngestion.StageEntryComponent
open Model.Ledger.JournalEntryComponent
open Model.StageDataClassification
open ModelOrchestrator
open Utilities.AppError
open Utilities.ResultHelper

let ``convert [Blocker] to [BlockerContract]`` (blocker: Blocker) : BlockerContract =
    match blocker with
    | Blocker.NoFunds -> BlockerContract.NoFunds
    | Blocker.Irresponsible -> BlockerContract.Irresponsible
    | Blocker.NeedsDecision note -> BlockerContract.NeedsDecision(note |> BlockerNote.value)
    | Blocker.Other note -> BlockerContract.Other(note |> BlockerNote.value)

let ``convert [InvoiceLifeCycleState] to [InvoiceLifeCycleStateContract]``
    (lifeCycleState: InvoiceLifeCycleState)
    : InvoiceLifeCycleStateContract = {
        invoiceState = lifeCycleState.invoiceState |> InvoiceState.toString
        paymentState = lifeCycleState.paymentState |> PaymentState.toString
        postedState = lifeCycleState.postedState |> PostedState.toString
        blocker = lifeCycleState.blocker |> Option.map ``convert [Blocker] to [BlockerContract]`` }

let ``convert [TransactionPointer] to [TransactionPointerContract]``
    (transactionPointer: TransactionPointer)
    : TransactionPointerContract =
    match transactionPointer with
    | CashFlowComponent.Posted journalEntryLineId -> TransactionPointerContract.Posted(journalEntryLineId |> JournalEntryLineId.value)
    | CashFlowComponent.Staged stageEntryLineId -> TransactionPointerContract.Staged(stageEntryLineId |> StageEntryLineId.value)

let ``convert [Payment] to [PaymentReturn]`` (payment: Payment.Payment) : PaymentReturn = {
    paymentId = payment |> Payment.paymentId |> PaymentId.value
    invoiceId = payment |> Payment.invoiceId |> InvoiceId.value
    transactionPointer = payment |> Payment.transactionPointer |> ``convert [TransactionPointer] to [TransactionPointerContract]``
    amount = (payment |> Payment.amount).money |> Money.amount
    postedToFiDate = payment |> Payment.postedToFiDate |> Option.map _.localDate
    postedToLedgerDate = payment |> Payment.postedToLedgerDate |> Option.map _.localDate
    memo = payment |> Payment.memo |> Option.map PaymentMemo.value
    createdAt = payment |> Payment.createdAt
    modifiedAt = payment |> Payment.modifiedAt }

let ``convert [Invoice] to [InvoiceReturn]``
    (context: Context.Context)
    (invoice: Invoice.Invoice)
    : Result<InvoiceReturn, AppError> =
    result {
        let! paymentAgreementName =
            invoice |> Invoice.paymentAgreementId |> ``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context
        return {
            invoiceId = invoice |> Invoice.invoiceId |> InvoiceId.value
            instanceId = invoice |> Invoice.instanceId |> InstanceId.value
            paymentAgreementName = paymentAgreementName
            externalInvoiceId = invoice |> Invoice.externalInvoiceId |> Option.map ExternalInvoiceId.value
            invoiceDate = (invoice |> Invoice.invoiceDate).localDate
            dueDate = (invoice |> Invoice.dueDate).localDate
            amount = (invoice |> Invoice.amount).money |> Money.amount
            invoiceLifeCycleState =
                invoice |> Invoice.invoiceLifeCycleState |> ``convert [InvoiceLifeCycleState] to [InvoiceLifeCycleStateContract]``
            memo = invoice |> Invoice.memo |> Option.map InvoiceMemo.value
            createdAt = invoice |> Invoice.createdAt
            modifiedAt = invoice |> Invoice.modifiedAt } }

let ``convert [Instance] to [InstanceReturn]`` (instance: Instance.Instance) : InstanceReturn = {
    instanceId = instance |> Instance.instanceId |> InstanceId.value
    masterAgreementName = instance |> Instance.masterAgreementName |> AgreementName.value
    instanceDate = instance |> Instance.instanceDate
    isFulfilled = instance |> Instance.isFulfilled
    createdAt = instance |> Instance.createdAt
    modifiedAt = instance |> Instance.modifiedAt }

let ``convert [InvoiceComposite] to [InvoiceCompositeReturn]``
    (context: Context.Context)
    (invoiceComposite: InstanceOrchestration.InvoiceComposite)
    : Result<InvoiceCompositeReturn, AppError> =
    result {
        let! invoice =
            invoiceComposite |> InstanceOrchestration.invoice |> ``convert [Invoice] to [InvoiceReturn]`` context
        let payments =
            invoiceComposite |> InstanceOrchestration.payments |> List.map ``convert [Payment] to [PaymentReturn]``
        return { invoice = invoice; payments = payments } }

let ``convert [InstanceComposite] to [InstanceCompositeReturn]``
    (context: Context.Context)
    (instanceComposite: InstanceOrchestration.InstanceComposite)
    : Result<InstanceCompositeReturn, AppError> =
    result {
        let instance = instanceComposite |> InstanceOrchestration.instance |> ``convert [Instance] to [InstanceReturn]``
        let! invoiceComposites =
            instanceComposite
            |> InstanceOrchestration.invoiceComposites
            |> List.map (``convert [InvoiceComposite] to [InvoiceCompositeReturn]`` context)
            |> convertListOfResultsToResultsList
        return { instance = instance; invoiceComposites = invoiceComposites } }

let ``convert [InstanceComposite list] to [InstanceCompositeReturn list]``
    (context: Context.Context)
    (instanceComposites: InstanceOrchestration.InstanceComposite list)
    : Result<InstanceCompositeReturn list, AppError> =
    instanceComposites
    |> List.map (``convert [InstanceComposite] to [InstanceCompositeReturn]`` context)
    |> convertListOfResultsToResultsList

let ``convert [PaymentAgreementDecision] to [PaymentAgreementDecisionReturn]``
    (context: Context.Context)
    (decision: StageDataClassificationComponent.PaymentAgreementDecision)
    : Result<PaymentAgreementDecisionReturn, AppError> =
    result {
        let! paymentAgreementName =
            decision.paymentAgreementId |> ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]`` context
        return {
            stageEntryLineId = decision.stageEntryLineId |> StageEntryLineId.value
            paymentAgreementName = paymentAgreementName
            ruleIds = decision.ruleIds |> List.map StageDataClassificationComponent.ClassificationRuleId.value
            outcome = decision.outcome |> StageDataClassificationComponent.PaymentAgreementDecisionOutcome.toString } }

let ``convert [InvoiceDecision] to [InvoiceDecisionReturn]`` (decision: InvoiceDecision) : InvoiceDecisionReturn =
    let outcome =
        match decision.outcome with
        | CashFlowComponent.PaymentCreated lineId ->
            InvoiceDecisionOutcomeReturn.PaymentCreated(lineId |> StageEntryLineId.value)
        | CashFlowComponent.ManyCandidateEntries lineIds ->
            InvoiceDecisionOutcomeReturn.ManyCandidateEntries(lineIds |> List.map StageEntryLineId.value)
        | CashFlowComponent.Overpayment -> InvoiceDecisionOutcomeReturn.Overpayment
    { invoiceId = decision.invoiceId |> InvoiceId.value; outcome = outcome }

let ``convert [PaymentAgreementClassificationResult] to [PaymentAgreementClassificationResultReturn]``
    (context: Context.Context)
    (classificationResult: InstanceOrchestration.PaymentAgreementClassificationResult)
    : Result<PaymentAgreementClassificationResultReturn, AppError> =
    result {
        let! classificationResults =
            classificationResult.classificationResults
            |> ``convert [ClassificationResult list] to [ClassificationResultReturn list]`` context
        let! decisionLog =
            classificationResult.decisionLog
            |> List.map (``convert [PaymentAgreementDecision] to [PaymentAgreementDecisionReturn]`` context)
            |> convertListOfResultsToResultsList
        let! openInstances =
            classificationResult.openInstances
            |> ``convert [InstanceComposite list] to [InstanceCompositeReturn list]`` context
        return {
            runId = classificationResult.runId |> StageDataClassificationComponent.ClassificationRunId.value
            classificationResults = classificationResults
            decisionLog = decisionLog
            invoiceDecisionLog =
                classificationResult.invoiceDecisionLog |> List.map ``convert [InvoiceDecision] to [InvoiceDecisionReturn]``
            openInstances = openInstances } }
