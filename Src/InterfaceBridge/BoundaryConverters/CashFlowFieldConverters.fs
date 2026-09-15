module InterfaceBridge.BoundaryConverters.CashFlowFieldConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
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
open Utilities.FieldUpdate
open Utilities.FieldUpdate.FieldUpdate
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

let ``convert [MonthDay] to [MonthDayContract]`` (monthDay: Cadence.MonthDay) : MonthDayContract =
    match monthDay with
    | Cadence.DateInMonth dateInMonth ->
        MonthDayContract.DateInMonth(dateInMonth |> Cadence.DateInMonthNumber.value)
    | Cadence.NthWeekDay(weekInMonth, weekDay) ->
        MonthDayContract.NthWeekDay(
            weekInMonth |> Cadence.WeekInMonthNumber.value, weekDay |> Cadence.WeekDay.toString)
    | Cadence.Last -> MonthDayContract.Last

let ``convert [MonthDayContract] to [MonthDay]``
    (monthDayContract: MonthDayContract)
    : Result<Cadence.MonthDay, AppError> =
    match monthDayContract with
    | MonthDayContract.DateInMonth dateInMonth ->
        dateInMonth |> Cadence.DateInMonthNumber.fromInt |> Result.map Cadence.DateInMonth
    | MonthDayContract.NthWeekDay(weekInMonth, weekDayString) ->
        result {
            let! weekInMonthNumber = weekInMonth |> Cadence.WeekInMonthNumber.fromInt
            let! weekDay = weekDayString |> Cadence.WeekDay.fromString
            return Cadence.NthWeekDay(weekInMonthNumber, weekDay)
        }
    | MonthDayContract.Last -> Ok Cadence.Last

let ``convert [CadenceType] to [CadenceTypeContract]`` (cadenceType: Cadence.CadenceType) : CadenceTypeContract =
    match cadenceType with
    | Cadence.Daily -> CadenceTypeContract.Daily
    | Cadence.Weekly weekDay -> CadenceTypeContract.Weekly(weekDay |> Cadence.WeekDay.toString)
    | Cadence.EveryOtherWeek weekDay -> CadenceTypeContract.EveryOtherWeek(weekDay |> Cadence.WeekDay.toString)
    | Cadence.Monthly monthDay ->
        CadenceTypeContract.Monthly(monthDay |> ``convert [MonthDay] to [MonthDayContract]``)
    | Cadence.Annually(month, monthDay) ->
        CadenceTypeContract.Annually(
            month |> Cadence.Month.toString, monthDay |> ``convert [MonthDay] to [MonthDayContract]``)

let ``convert [CadenceTypeContract] to [CadenceType]``
    (cadenceTypeContract: CadenceTypeContract)
    : Result<Cadence.CadenceType, AppError> =
    match cadenceTypeContract with
    | CadenceTypeContract.Daily -> Ok Cadence.Daily
    | CadenceTypeContract.Weekly weekDayString ->
        weekDayString |> Cadence.WeekDay.fromString |> Result.map Cadence.Weekly
    | CadenceTypeContract.EveryOtherWeek weekDayString ->
        weekDayString |> Cadence.WeekDay.fromString |> Result.map Cadence.EveryOtherWeek
    | CadenceTypeContract.Monthly monthDayContract ->
        monthDayContract |> ``convert [MonthDayContract] to [MonthDay]`` |> Result.map Cadence.Monthly
    | CadenceTypeContract.Annually(monthString, monthDayContract) ->
        result {
            let! month = monthString |> Cadence.Month.fromString
            let! monthDay = monthDayContract |> ``convert [MonthDayContract] to [MonthDay]``
            return Cadence.Annually(month, monthDay)
        }

let ``convert [Cadence] to [CadenceContract]`` (cadence: Cadence.Cadence) : CadenceContract = {
    cadenceType = cadence |> Cadence.cadenceType |> ``convert [CadenceType] to [CadenceTypeContract]``
    nextInstance = (cadence |> Cadence.nextInstance).nextInstance }

let ``convert [CadenceContract] to [Cadence]`` (cadenceContract: CadenceContract) : Result<Cadence.Cadence, AppError> =
    result {
        let! cadenceType = cadenceContract.cadenceType |> ``convert [CadenceTypeContract] to [CadenceType]``
        let nextInstance : Cadence.CadenceNextInstance = { nextInstance = cadenceContract.nextInstance }
        return! Cadence.create cadenceType nextInstance
    }

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
            invoiceComposite
            |> InstanceOrchestration.payments
            |> List.map ``convert [Payment] to [PaymentReturn]``
            |> List.sortBy (fun (payment: PaymentReturn) -> payment.postedToFiDate, payment.paymentId)
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
        let sorted =
            invoiceComposites
            |> List.sortBy (fun (invoiceComposite: InvoiceCompositeReturn) ->
                invoiceComposite.invoice.dueDate, invoiceComposite.invoice.invoiceId)
        return { instance = instance; invoiceComposites = sorted } }

let ``convert [InstanceComposite list] to [InstanceCompositeReturn list]``
    (context: Context.Context)
    (instanceComposites: InstanceOrchestration.InstanceComposite list)
    : Result<InstanceCompositeReturn list, AppError> =
    instanceComposites
    |> List.map (``convert [InstanceComposite] to [InstanceCompositeReturn]`` context)
    |> convertListOfResultsToResultsList
    |> Result.map (
        List.sortBy (fun (instanceComposite: InstanceCompositeReturn) ->
            instanceComposite.instance.masterAgreementName,
            instanceComposite.instance.instanceDate,
            instanceComposite.instance.instanceId))

let ``convert [MasterAgreement] to [MasterAgreementReturn]``
    (masterAgreement: MasterAgreement.MasterAgreement)
    : MasterAgreementReturn = {
        agreementId = masterAgreement |> MasterAgreement.agreementID |> MasterAgreementId.value
        agreementName = masterAgreement |> MasterAgreement.agreementName |> AgreementName.value
        direction = masterAgreement |> MasterAgreement.direction |> FlowDirection.toString
        cadence = masterAgreement |> MasterAgreement.cadence |> ``convert [Cadence] to [CadenceContract]``
        counterparty = masterAgreement |> MasterAgreement.counterparty |> Counterparty.value
        activeBegin = masterAgreement |> MasterAgreement.activityPeriod |> ActivityPeriod.activeBegin
        activeEnd = masterAgreement |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd
        memo = masterAgreement |> MasterAgreement.memo |> Option.map AgreementMemo.value
        createdAt = masterAgreement |> MasterAgreement.createdAt
        modifiedAt = masterAgreement |> MasterAgreement.modifiedAt }

let ``convert [PaymentAgreement] to [PaymentAgreementReturn]``
    (context: Context.Context)
    (paymentAgreement: PaymentAgreement.PaymentAgreement)
    : Result<PaymentAgreementReturn, AppError> =
    result {
        let! masterAgreementName =
            paymentAgreement
            |> PaymentAgreement.masterAgreementID
            |> ``convert [MasterAgreementId] to [AgreementNameString]`` context
        let (DebitAccount debitAccountId) = paymentAgreement |> PaymentAgreement.debitAccount
        let (CreditAccount creditAccountId) = paymentAgreement |> PaymentAgreement.creditAccount
        let! debitAccountCode = debitAccountId |> ``convert AccountId to AccountCodeString`` context
        let! creditAccountCode = creditAccountId |> ``convert AccountId to AccountCodeString`` context
        return {
            paymentAgreementId = paymentAgreement |> PaymentAgreement.paymentAgreementId |> PaymentAgreementId.value
            masterAgreementName = masterAgreementName
            paymentAgreementName = paymentAgreement |> PaymentAgreement.paymentAgreementName |> PaymentAgreementName.value
            debitAccountCode = debitAccountCode
            creditAccountCode = creditAccountCode
            expectedAmount = paymentAgreement |> PaymentAgreement.expectedAmount |> Option.map Money.amount
            daysDueAfterInvoiceDate =
                paymentAgreement |> PaymentAgreement.daysDueAfterInvoiceDate |> Option.map DaysDueAfterInvoiceDate.value
            memo = paymentAgreement |> PaymentAgreement.memo |> Option.map PaymentAgreementMemo.value
            createdAt = paymentAgreement |> PaymentAgreement.createdAt
            modifiedAt = paymentAgreement |> PaymentAgreement.modifiedAt } }

let ``convert [Agreement] to [AgreementReturn]``
    (context: Context.Context)
    (agreement: AgreementOrchestration.Agreement)
    : Result<AgreementReturn, AppError> =
    result {
        let masterAgreement =
            agreement |> AgreementOrchestration.masterAgreement |> ``convert [MasterAgreement] to [MasterAgreementReturn]``
        let! paymentAgreements =
            agreement
            |> AgreementOrchestration.paymentAgreements
            |> List.map (``convert [PaymentAgreement] to [PaymentAgreementReturn]`` context)
            |> convertListOfResultsToResultsList
        let instances =
            agreement |> AgreementOrchestration.instances |> List.map ``convert [Instance] to [InstanceReturn]``
        let! invoices =
            agreement
            |> AgreementOrchestration.invoices
            |> List.map (``convert [Invoice] to [InvoiceReturn]`` context)
            |> convertListOfResultsToResultsList
        let payments =
            agreement |> AgreementOrchestration.payments |> List.map ``convert [Payment] to [PaymentReturn]``
        return {
            masterAgreement = masterAgreement
            paymentAgreements = paymentAgreements |> List.sortBy _.paymentAgreementName
            instances = instances |> List.sortBy (fun i -> i.instanceDate, i.instanceId)
            invoices = invoices |> List.sortBy (fun i -> i.invoiceDate, i.amount, i.invoiceId)
            payments = payments |> List.sortBy (fun p -> p.postedToFiDate, p.amount, p.paymentId) } }

let ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]``
    (context: Context.Context)
    (link: PaymentAgreementLink.PaymentAgreementLink)
    : Result<PaymentAgreementLinkReturn, AppError> =
    result {
        let! paymentAgreementName =
            link
            |> PaymentAgreementLink.paymentAgreementId
            |> ``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context
        return {
            paymentAgreementLinkId = link |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLinkId.value
            paymentAgreementName = paymentAgreementName
            stageEntryLineId = link |> PaymentAgreementLink.stageEntryLineId |> StageEntryLineId.value
            createdAt = link |> PaymentAgreementLink.createdAt
            modifiedAt = link |> PaymentAgreementLink.modifiedAt } }

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

let ``convert [PaymentPostingTransition] to [PaymentPostingTransitionReturn]``
    (transition: PaymentPostingTransition)
    : PaymentPostingTransitionReturn =
    { paymentId = transition.paymentId |> PaymentId.value
      agreementName = transition.agreementName |> AgreementName.value
      invoiceAmount = transition.invoiceAmount.money |> Money.amount
      journalEntryLineId = transition.journalEntryLineId |> JournalEntryLineId.value }

let ``convert [PaymentPostingTransition list] to [PaymentPostingTransitionReturn list]``
    (transitions: PaymentPostingTransition list)
    : PaymentPostingTransitionReturn list =
    transitions
    |> List.map ``convert [PaymentPostingTransition] to [PaymentPostingTransitionReturn]``
    |> List.sortBy (fun (transition: PaymentPostingTransitionReturn) ->
        transition.agreementName, transition.paymentId)

let ``convert [ProjectedInvoice] to [ProjectedInvoiceReturn]`` (invoice: ProjectedInvoice) : ProjectedInvoiceReturn =
    { invoiceId = invoice.invoiceId |> InvoiceId.value
      agreementName = invoice.agreementName |> AgreementName.value
      direction = invoice.direction |> FlowDirection.toString
      dueDate = invoice.dueDate.localDate
      amount = invoice.amount.money |> Money.amount }

let ``convert [ProjectedAccount] to [ProjectedAccountReturn]`` (account: ProjectedAccount) : ProjectedAccountReturn =
    { accountCode = account.accountCode |> Model.Ledger.AccountComponent.AccountCode.value
      accountName = account.accountName |> Model.Ledger.AccountComponent.AccountName.value
      currentBalance = account.currentBalance |> Money.amount
      knownInflows = account.knownInflows |> Money.amount
      knownOutflows = account.knownOutflows |> Money.amount
      projectedLow = account.projectedLow |> Money.amount
      invoices =
        account.invoices
        |> List.sortBy (fun (invoice: ProjectedInvoice) ->
            invoice.dueDate.localDate, (invoice.invoiceId |> InvoiceId.value))
        |> List.map ``convert [ProjectedInvoice] to [ProjectedInvoiceReturn]`` }

let ``convert [BillToChase] to [BillToChaseReturn]`` (bill: BillToChase) : BillToChaseReturn =
    { instanceId = bill.instanceId |> InstanceId.value
      agreementName = bill.agreementName |> AgreementName.value
      paymentAgreementName = bill.paymentAgreementName |> PaymentAgreementName.value
      instanceDate = bill.instanceDate
      cadenceType = bill.cadenceType |> ``convert [CadenceType] to [CadenceTypeContract]`` }

let ``convert [CashFlowProjection] to [CashFlowProjectionReturn]``
    (projection: CashFlowProjection)
    : CashFlowProjectionReturn =
    { accounts =
        projection.accounts
        |> List.sortBy (fun (account: ProjectedAccount) ->
            account.accountCode |> Model.Ledger.AccountComponent.AccountCode.value)
        |> List.map ``convert [ProjectedAccount] to [ProjectedAccountReturn]``
      billsToChase =
        projection.billsToChase
        |> List.sortBy (fun (bill: BillToChase) ->
            bill.instanceDate, (bill.paymentAgreementName |> PaymentAgreementName.value))
        |> List.map ``convert [BillToChase] to [BillToChaseReturn]`` }

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
        let sortedResults =
            classificationResults
            |> List.sortBy (fun result -> result.candidate.stageEntryHeaderId, result.candidate.stageEntryLineId)
        let sortedDecisionLog =
            decisionLog
            |> List.sortBy (fun (decision: PaymentAgreementDecisionReturn) ->
                decision.paymentAgreementName, decision.stageEntryLineId)
        let sortedInvoiceDecisionLog =
            classificationResult.invoiceDecisionLog
            |> List.map ``convert [InvoiceDecision] to [InvoiceDecisionReturn]``
            |> List.sortBy (fun (decision: InvoiceDecisionReturn) -> decision.invoiceId, decision.outcome)
        return {
            runId = classificationResult.runId |> StageDataClassificationComponent.ClassificationRunId.value
            classificationResults = sortedResults
            decisionLog = sortedDecisionLog
            invoiceDecisionLog = sortedInvoiceDecisionLog
            openInstances = openInstances } }

let ``convert [BlockerContract] to [Blocker]`` (blockerContract: BlockerContract) : Result<Blocker, AppError> =
    match blockerContract with
    | BlockerContract.NoFunds -> Ok Blocker.NoFunds
    | BlockerContract.Irresponsible -> Ok Blocker.Irresponsible
    | BlockerContract.NeedsDecision note -> note |> BlockerNote.create |> Result.map Blocker.NeedsDecision
    | BlockerContract.Other note -> note |> BlockerNote.create |> Result.map Blocker.Other

let ``convert [InvoiceLifeCycleStateContract] to [InvoiceLifeCycleState]``
    (lifeCycleStateContract: InvoiceLifeCycleStateContract)
    : Result<InvoiceLifeCycleState, AppError> =
    result {
        let! invoiceState = lifeCycleStateContract.invoiceState |> InvoiceState.fromString
        let! paymentState = lifeCycleStateContract.paymentState |> PaymentState.fromString
        let! postedState = lifeCycleStateContract.postedState |> PostedState.fromString
        let! blocker =
            lifeCycleStateContract.blocker
            |> convertOptionToDesiredTypeWithFallibleConverter ``convert [BlockerContract] to [Blocker]``
        let lifeCycleState : InvoiceLifeCycleState = {
            invoiceState = invoiceState
            paymentState = paymentState
            postedState = postedState
            blocker = blocker }
        return lifeCycleState
    }

let ``convert [TransactionPointerContract] to [TransactionPointer]``
    (transactionPointerContract: TransactionPointerContract)
    : TransactionPointer =
    match transactionPointerContract with
    | TransactionPointerContract.Posted journalEntryLineUuid ->
        CashFlowComponent.Posted(journalEntryLineUuid |> JournalEntryLineId.fromGuid)
    | TransactionPointerContract.Staged stageEntryLineUuid ->
        CashFlowComponent.Staged(stageEntryLineUuid |> StageEntryLineId.fromGuid)

let ``convert [CreatePaymentFieldsInput] to [PaymentPrimitives]``
    (input: CreatePaymentFieldsInput)
    : Result<
        TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option,
        AppError> =
    result {
        let transactionPointer =
            input.transactionPointer |> ``convert [TransactionPointerContract] to [TransactionPointer]``
        let! money = input.amount |> Money.fromDecimal
        let amount : PaymentAmount = { money = money }
        let postedToFiDate =
            input.postedToFiDate |> Option.map (fun localDate -> ({ localDate = localDate } : PostedToFiDate))
        let postedToLedgerDate =
            input.postedToLedgerDate |> Option.map (fun localDate -> ({ localDate = localDate } : PostedToLedgerDate))
        let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter PaymentMemo.create
        return transactionPointer, amount, postedToFiDate, postedToLedgerDate, memo
    }

let ``convert [CreatePaymentFieldsInput list] to [PaymentPrimitives list]``
    (input: CreatePaymentFieldsInput list)
    : Result<
        (TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option) list,
        AppError> =
    input
    |> List.map ``convert [CreatePaymentFieldsInput] to [PaymentPrimitives]``
    |> convertListOfResultsToResultsList

let ``convert [CreateInvoiceFieldsInput] to [InvoiceCompositePrimitives]``
    (context: Context.Context)
    (input: CreateInvoiceFieldsInput)
    : Result<
        PaymentAgreementId * ExternalInvoiceId option * InvoiceDate * DueDate * InvoiceAmount * InvoiceLifeCycleState *
        InvoiceMemo option *
        (TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option) list,
        AppError> =
    result {
        let! paymentAgreementId =
            input.paymentAgreementName |> ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context
        let! externalInvoiceId =
            input.externalInvoiceId |> convertOptionToDesiredTypeWithFallibleConverter ExternalInvoiceId.create
        let invoiceDate : InvoiceDate = { localDate = input.invoiceDate }
        let dueDate : DueDate = { localDate = input.dueDate }
        let! money = input.amount |> Money.fromDecimal
        let amount : InvoiceAmount = { money = money }
        let! lifeCycleState =
            input.invoiceLifeCycleState |> ``convert [InvoiceLifeCycleStateContract] to [InvoiceLifeCycleState]``
        let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter InvoiceMemo.create
        let! payments = input.payments |> ``convert [CreatePaymentFieldsInput list] to [PaymentPrimitives list]``
        return paymentAgreementId, externalInvoiceId, invoiceDate, dueDate, amount, lifeCycleState, memo, payments
    }

let ``convert [CreateInvoiceFieldsInput list] to [InvoiceCompositePrimitives list]``
    (context: Context.Context)
    (input: CreateInvoiceFieldsInput list)
    : Result<
        (PaymentAgreementId * ExternalInvoiceId option * InvoiceDate * DueDate * InvoiceAmount * InvoiceLifeCycleState *
         InvoiceMemo option *
         (TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option) list) list,
        AppError> =
    input
    |> List.map (``convert [CreateInvoiceFieldsInput] to [InvoiceCompositePrimitives]`` context)
    |> convertListOfResultsToResultsList

let ``convert [NewInvoiceFieldsInput] to [NewInvoicePrimitives]``
    (context: Context.Context)
    (input: NewInvoiceFieldsInput)
    : Result<
        PaymentAgreementId * ExternalInvoiceId option * InvoiceDate * DueDate * InvoiceAmount * InvoiceState *
        Blocker option * InvoiceMemo option *
        (TransactionPointer * PaymentAmount * PostedToFiDate option * PostedToLedgerDate option * PaymentMemo option) list,
        AppError> =
    result {
        let! paymentAgreementId =
            input.paymentAgreementName |> ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context
        let! externalInvoiceId =
            input.externalInvoiceId |> convertOptionToDesiredTypeWithFallibleConverter ExternalInvoiceId.create
        let invoiceDate : InvoiceDate = { localDate = input.invoiceDate }
        let dueDate : DueDate = { localDate = input.dueDate }
        let! money = input.amount |> Money.fromDecimal
        let amount : InvoiceAmount = { money = money }
        let! invoiceState = input.invoiceState |> InvoiceState.fromString
        let! blocker =
            input.blocker |> convertOptionToDesiredTypeWithFallibleConverter ``convert [BlockerContract] to [Blocker]``
        let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter InvoiceMemo.create
        let! payments = input.payments |> ``convert [CreatePaymentFieldsInput list] to [PaymentPrimitives list]``
        return
            paymentAgreementId, externalInvoiceId, invoiceDate, dueDate, amount, invoiceState, blocker, memo, payments
    }

let private noChangeInvoiceUpdates (invoiceId: InvoiceId) : Invoice.InvoiceFieldUpdates =
    { invoiceIdToUpdate = invoiceId
      externalInvoiceIdUpdate = NoChange
      invoiceDateUpdate = NoChange
      dueDateUpdate = NoChange
      amountUpdate = NoChange
      invoiceStateUpdate = NoChange
      paymentStateUpdate = NoChange
      postedStateUpdate = NoChange
      blockerUpdate = NoChange
      memoUpdate = NoChange }

let private noChangeInstanceUpdates (instanceId: InstanceId) : Instance.InstanceFieldUpdates =
    { instanceIdToUpdate = instanceId
      instanceDateUpdate = NoChange
      isFulfilledUpdate = NoChange }

let ``convert [CreateInvoiceInput] to [InstanceCompositeUpdate]``
    (context: Context.Context)
    (input: CreateInvoiceInput)
    : Result<InstanceOrchestration.InstanceCompositeUpdate, AppError> =
    result {
        let instanceId = input.instanceId |> InstanceId.fromGuid
        let! newInvoice = input.invoice |> ``convert [NewInvoiceFieldsInput] to [NewInvoicePrimitives]`` context
        return {
            instanceUpdates = instanceId |> noChangeInstanceUpdates
            invoiceCompositeUpdates = []
            newInvoices = [ newInvoice ] }
    }

let ``convert [UpdateInvoiceInput] to [InstanceCompositeUpdate]``
    (context: Context.Context)
    (input: UpdateInvoiceInput)
    : Result<InstanceOrchestration.InstanceCompositeUpdate, AppError> =
    result {
        let invoiceId = input.invoiceId |> InvoiceId.fromGuid
        let! invoice = invoiceId |> Invoice.fetchById context
        let instanceId = invoice |> Invoice.instanceId
        let! externalInvoiceIdUpdate =
            input.externalInvoiceIdUpdate |> convertFieldUpdateOptionToNewTypeOptionFallible ExternalInvoiceId.create
        let invoiceDateUpdate =
            input.invoiceDateUpdate |> FieldUpdate.map (fun localDate -> ({ localDate = localDate } : InvoiceDate))
        let dueDateUpdate =
            input.dueDateUpdate |> FieldUpdate.map (fun localDate -> ({ localDate = localDate } : DueDate))
        let! amountUpdate =
            input.amountUpdate
            |> convertFieldUpdateToNewTypeFallible (fun amount ->
                amount |> Money.fromDecimal |> Result.map (fun money -> ({ money = money } : InvoiceAmount)))
        let! invoiceStateUpdate = input.invoiceStateUpdate |> convertFieldUpdateToNewTypeFallible InvoiceState.fromString
        let! paymentStateUpdate = input.paymentStateUpdate |> convertFieldUpdateToNewTypeFallible PaymentState.fromString
        let! postedStateUpdate = input.postedStateUpdate |> convertFieldUpdateToNewTypeFallible PostedState.fromString
        let! blockerUpdate =
            input.blockerUpdate
            |> convertFieldUpdateOptionToNewTypeOptionFallible ``convert [BlockerContract] to [Blocker]``
        let! memoUpdate = input.memoUpdate |> convertFieldUpdateOptionToNewTypeOptionFallible InvoiceMemo.create
        let invoiceUpdates : Invoice.InvoiceFieldUpdates = {
            invoiceIdToUpdate = invoiceId
            externalInvoiceIdUpdate = externalInvoiceIdUpdate
            invoiceDateUpdate = invoiceDateUpdate
            dueDateUpdate = dueDateUpdate
            amountUpdate = amountUpdate
            invoiceStateUpdate = invoiceStateUpdate
            paymentStateUpdate = paymentStateUpdate
            postedStateUpdate = postedStateUpdate
            blockerUpdate = blockerUpdate
            memoUpdate = memoUpdate }
        let invoiceCompositeUpdate : InstanceOrchestration.InvoiceCompositeUpdate =
            { invoiceUpdates = invoiceUpdates; paymentUpdates = []; paymentIdsToDelete = []; newPayments = [] }
        return {
            instanceUpdates = instanceId |> noChangeInstanceUpdates
            invoiceCompositeUpdates = [ invoiceCompositeUpdate ]
            newInvoices = [] }
    }

let ``convert [CreatePaymentInput] to [InstanceCompositeUpdate]``
    (context: Context.Context)
    (input: CreatePaymentInput)
    : Result<InstanceOrchestration.InstanceCompositeUpdate, AppError> =
    result {
        let invoiceId = input.invoiceId |> InvoiceId.fromGuid
        let! invoice = invoiceId |> Invoice.fetchById context
        let instanceId = invoice |> Invoice.instanceId
        let! newPayment = input.payment |> ``convert [CreatePaymentFieldsInput] to [PaymentPrimitives]``
        let invoiceCompositeUpdate : InstanceOrchestration.InvoiceCompositeUpdate =
            { invoiceUpdates = invoiceId |> noChangeInvoiceUpdates
              paymentUpdates = []
              paymentIdsToDelete = []
              newPayments = [ newPayment ] }
        return {
            instanceUpdates = instanceId |> noChangeInstanceUpdates
            invoiceCompositeUpdates = [ invoiceCompositeUpdate ]
            newInvoices = [] }
    }

let ``convert [UpdatePaymentAgreementLinkInput] to [PaymentAgreementLinkFieldUpdates]``
    (context: Context.Context)
    (input: UpdatePaymentAgreementLinkInput)
    : Result<PaymentAgreementLink.PaymentAgreementLinkFieldUpdates, AppError> =
    result {
        let linkId = input.paymentAgreementLinkId |> PaymentAgreementLinkId.fromGuid
        let! paymentAgreementIdUpdate =
            input.paymentAgreementNameUpdate
            |> convertFieldUpdateToNewTypeFallible (``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context)
        return { linkIdToUpdate = linkId; paymentAgreementIdUpdate = paymentAgreementIdUpdate } }

let ``convert [CreatePaymentAgreementFieldsInput] to [PaymentAgreementPrimitives]``
    (context: Context.Context)
    (input: CreatePaymentAgreementFieldsInput)
    : Result<
        PaymentAgreementName * DebitAccount * CreditAccount * Money option * DaysDueAfterInvoiceDate option *
        PaymentAgreementMemo option, AppError> =
    result {
        let! paymentAgreementName = input.paymentAgreementName |> PaymentAgreementName.create
        let! debitAccountId = input.debitAccountCode |> ``convert AccountCodeString to Id`` context
        let! creditAccountId = input.creditAccountCode |> ``convert AccountCodeString to Id`` context
        let! expectedAmount =
            input.expectedAmount |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        let! daysDueAfterInvoiceDate =
            input.daysDueAfterInvoiceDate |> convertOptionToDesiredTypeWithFallibleConverter DaysDueAfterInvoiceDate.create
        let! memo = input.memo |> convertOptionToDesiredTypeWithFallibleConverter PaymentAgreementMemo.create
        return
            paymentAgreementName,
            DebitAccount debitAccountId,
            CreditAccount creditAccountId,
            expectedAmount,
            daysDueAfterInvoiceDate,
            memo
    }

let ``convert [CreatePaymentAgreementFieldsInput list] to [PaymentAgreementPrimitives list]``
    (context: Context.Context)
    (input: CreatePaymentAgreementFieldsInput list)
    : Result<
        (PaymentAgreementName * DebitAccount * CreditAccount * Money option * DaysDueAfterInvoiceDate option *
         PaymentAgreementMemo option) list, AppError> =
    input
    |> List.map (``convert [CreatePaymentAgreementFieldsInput] to [PaymentAgreementPrimitives]`` context)
    |> convertListOfResultsToResultsList

let ``convert [UpdateAgreementInput] to [MasterAgreementFieldUpdates]``
    (context: Context.Context)
    (input: UpdateAgreementInput)
    : Result<MasterAgreement.MasterAgreementFieldUpdates, AppError> =
    result {
        let! agreementId = input.agreementName |> ``convert [AgreementNameString] to [MasterAgreementId]`` context
        let! agreementNameUpdate = input.agreementNameUpdate |> convertFieldUpdateToNewTypeFallible AgreementName.create
        let! directionUpdate = input.directionUpdate |> convertFieldUpdateToNewTypeFallible FlowDirection.fromString
        let! cadenceUpdate =
            input.cadenceUpdate |> convertFieldUpdateToNewTypeFallible ``convert [CadenceContract] to [Cadence]``
        let! counterpartyUpdate = input.counterpartyUpdate |> convertFieldUpdateToNewTypeFallible Counterparty.create
        // the model holds the two dates as one ActivityPeriod, so setting either one has to carry the other over
        let! activityPeriodUpdate =
            if input.activeBeginUpdate = NoChange && input.activeEndUpdate = NoChange then Ok NoChange
            else
                result {
                    let! current = agreementId |> MasterAgreement.fetchById context
                    let currentActivityPeriod = current |> MasterAgreement.activityPeriod
                    let activeBegin =
                        input.activeBeginUpdate |> valueOrCurrent (currentActivityPeriod |> ActivityPeriod.activeBegin)
                    let activeEnd =
                        input.activeEndUpdate |> valueOrCurrent (currentActivityPeriod |> ActivityPeriod.activeEnd)
                    let! activityPeriod =
                        ActivityPeriod.create activeBegin activeEnd ActivityPeriod.ConsideredAvailableBeforeBeginDate
                    return SetTo activityPeriod
                }
        let! memoUpdate = input.memoUpdate |> convertFieldUpdateOptionToNewTypeOptionFallible AgreementMemo.create
        return {
            agreementIdToUpdate = agreementId
            agreementNameUpdate = agreementNameUpdate
            directionUpdate = directionUpdate
            cadenceUpdate = cadenceUpdate
            counterpartyUpdate = counterpartyUpdate
            activityPeriodUpdate = activityPeriodUpdate
            memoUpdate = memoUpdate } }
