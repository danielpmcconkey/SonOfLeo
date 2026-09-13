module InterfaceBridge.BoundaryConverters.CashFlowFieldConverters

open InterfaceBridge.InterfaceContracts.CashFlowContracts
open Model
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.DataIngestion.StageEntryComponent
open Model.Ledger.JournalEntryComponent
open ModelOrchestrator
open Utilities.AppError
open Utilities.ResultHelper

let private fallibleConverterPaymentAgreementNameStringToPaymentAgreementUuid context nameString =
    result {
        // see if the string represents a valid name first
        let! _ = nameString |> PaymentAgreementName.create
        // now see if it matches a payment agreement ID
        return!
            match nameString |> LookupCache.paymentAgreementNameToId.fetch context with
            | Ok x -> Ok x
            | Error(DalResultantRowsDidntMatchExpectation _) ->
                Error(CashflowPaymentAgreementNameDoesntMatchId nameString)
            | Error e -> Error e
    }

let ``convert [PaymentAgreementNameString] to [PaymentAgreementId]``
    (context: Context.Context)
    (nameString: string)
    : Result<PaymentAgreementId, AppError> =
    result {
        let! uuid = nameString |> fallibleConverterPaymentAgreementNameStringToPaymentAgreementUuid context
        return uuid |> PaymentAgreementId.fromGuid
    }

let ``convert [PaymentAgreementNameString option] to [PaymentAgreementId option]``
    (context: Context.Context)
    (nameStringOption: string option)
    : Result<PaymentAgreementId option, AppError> =
    nameStringOption
    |> convertOptionToDesiredTypeWithFallibleConverter (``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context)

let ``convert [PaymentAgreementId] to [PaymentAgreementNameString]``
    (context: Context.Context)
    (paymentAgreementId: PaymentAgreementId)
    : Result<string, AppError> =
    paymentAgreementId |> PaymentAgreementId.value |> LookupCache.paymentAgreementIdToName.fetch context

let ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]``
    (context: Context.Context)
    (paymentAgreementIdOption: PaymentAgreementId option)
    : Result<string option, AppError> =
    paymentAgreementIdOption
    |> convertOptionToDesiredTypeWithFallibleConverter
        (``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context)

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
