module ModelOrchestrator.AgreementOrchestration

open DataAccessLayer.ExecuteReader
open Model
open Model.CashFlow
open Model.DataIngestion
open Model.DataIngestion.StageEntryComponent
open Model.Ledger.AccountComponent
open Model.Ledger
open Model.Ledger.JournalEntryComponent
open ModelOrchestrator.CashFlowCompositeFetcher
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.Calendar
open Utilities.FieldUpdate
open Utilities.ResultHelper

type Agreement = private {
    masterAgreement: MasterAgreement.MasterAgreement
    paymentAgreements: PaymentAgreement.PaymentAgreement list
    instances: Instance.Instance list
    invoices: Invoice.Invoice list
    payments: Payment.Payment list
}

let masterAgreement (agreement:Agreement) = agreement.masterAgreement
let paymentAgreements (agreement:Agreement) = agreement.paymentAgreements
let instances (agreement:Agreement) = agreement.instances
let invoices (agreement:Agreement) = agreement.invoices
let payments (agreement:Agreement) = agreement.payments

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

let private confirmPaymentAgreementBelongsToAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (fieldUpdates: PaymentAgreement.PaymentAgreementFieldUpdates)
    : Result<unit, AppError> =
    result {
        let! paymentAgreement = fieldUpdates.paymentAgreementIdToUpdate |> PaymentAgreement.fetchById context
        return!
            if paymentAgreement |> PaymentAgreement.masterAgreementID = agreementId then Ok ()
            else
                let paymentAgreementUuid = fieldUpdates.paymentAgreementIdToUpdate |> CashFlowComponent.PaymentAgreementId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowPaymentAgreementNotUnderMasterAgreement(paymentAgreementUuid, agreementUuid))
    }

let private confirmInstanceBelongsToAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (fieldUpdates: Instance.InstanceFieldUpdates)
    : Result<unit, AppError> =
    result {
        let! instance = fieldUpdates.instanceIdToUpdate |> Instance.fetchById context
        return!
            if instance |> Instance.masterAgreementID = agreementId then Ok ()
            else
                let instanceUuid = fieldUpdates.instanceIdToUpdate |> CashFlowComponent.InstanceId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowInstanceNotUnderMasterAgreement(instanceUuid, agreementUuid))
    }

let private confirmInvoiceBelongsToAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (fieldUpdates: Invoice.InvoiceFieldUpdates)
    : Result<unit, AppError> =
    result {
        let! invoice = fieldUpdates.invoiceIdToUpdate |> Invoice.fetchById context
        let! instance = invoice |> Invoice.instanceId |> Instance.fetchById context
        return!
            if instance |> Instance.masterAgreementID = agreementId then Ok ()
            else
                let invoiceUuid = fieldUpdates.invoiceIdToUpdate |> CashFlowComponent.InvoiceId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowInvoiceNotUnderMasterAgreement(invoiceUuid, agreementUuid))
    }

let private confirmPaymentBelongsToAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (fieldUpdates: Payment.PaymentFieldUpdates)
    : Result<unit, AppError> =
    result {
        let! payment = fieldUpdates.paymentIdToUpdate |> Payment.fetchById context
        let! invoice = payment |> Payment.invoiceId |> Invoice.fetchById context
        let! instance = invoice |> Invoice.instanceId |> Instance.fetchById context
        return!
            if instance |> Instance.masterAgreementID = agreementId then Ok ()
            else
                let paymentUuid = fieldUpdates.paymentIdToUpdate |> CashFlowComponent.PaymentId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowPaymentNotUnderMasterAgreement(paymentUuid, agreementUuid))
    }

let private confirmAuthorityAndCohesion
    (context: Context.Context)
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    (instanceUpdates: Instance.InstanceFieldUpdates list)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates list)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<unit, AppError> =
    // note: this runs super slow. It's not a common activity so that's likely okay. Start with this flow. If we
    // notice that it takes forever, we can implement some memoization down the line
    let agreementId = masterAgreementUpdates.agreementIdToUpdate
    result {
        do!
            paymentAgreementUpdates
            |> List.map (confirmPaymentAgreementBelongsToAgreement context agreementId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do!
            instanceUpdates
            |> List.map (confirmInstanceBelongsToAgreement context agreementId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do!
            invoiceUpdates
            |> List.map (confirmInvoiceBelongsToAgreement context agreementId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do!
            paymentUpdates
            |> List.map (confirmPaymentBelongsToAgreement context agreementId)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private confirmPayment
    (context: Context.Context)
    (payment: Payment.Payment)
    : Result<unit, AppError> =
    result {
        do!
            match payment |> Payment.invoiceId |> Invoice.fetchById context with
            | Ok _ -> Ok ()
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                let invoiceUuid = payment |> Payment.invoiceId |> CashFlowComponent.InvoiceId.value
                Error(CashflowInvoiceIdDoesntExist invoiceUuid)
            | Error e -> Error e
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
                if providedDate = actualDate then Ok ()
                else
                    let paymentUuid = payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value
                    Error(CashflowPaymentPostedToLedgerDateMismatch(paymentUuid, providedDate, actualDate))
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
        do!
            match invoice |> Invoice.instanceId |> Instance.fetchById context with
            | Ok _ -> Ok ()
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                let instanceUuid = invoice |> Invoice.instanceId |> CashFlowComponent.InstanceId.value
                Error(CashflowInstanceIdDoesntExist instanceUuid)
            | Error e -> Error e
        do!
            match invoice |> Invoice.paymentAgreementId |> PaymentAgreement.fetchById context with
            | Ok _ -> Ok ()
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                let paymentAgreementUuid = invoice |> Invoice.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                Error(CashflowPaymentAgreementIdDoesntExist paymentAgreementUuid)
            | Error e -> Error e
        let invoiceAmount = invoice |> Invoice.amount |> Money.amount
        return!
            if invoiceAmount > 0M then Ok ()
            else
                let uuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
                Error(CashflowInvoiceNonPositiveAmount(uuid, invoiceAmount))
    }

let private confirmInvoices
    (context: Context.Context)
    (invoices: Invoice.Invoice list)
    : Result<unit, AppError> =
    // todo: InvoiceOrchestration has much better invoice validation. We should use it here
    invoices
    |> List.map (confirmInvoice context)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmInstance
    (agreementId: CashFlowComponent.MasterAgreementId)
    (instance: Instance.Instance)
    : Result<unit, AppError> =
    if instance |> Instance.masterAgreementID = agreementId then Ok ()
    else
        let uuid = instance
                |> Instance.instanceId
                |> CashFlowComponent.InstanceId.value
        let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
        Error(CashflowInstanceNotUnderMasterAgreement(uuid, agreementUuid))

let private confirmInstances
    (agreementId: CashFlowComponent.MasterAgreementId)
    (instances: Instance.Instance list)
    : Result<unit, AppError> =
    instances
    |> List.map (confirmInstance agreementId)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmPaymentAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (paymentAgreement: PaymentAgreement.PaymentAgreement)
    : Result<unit, AppError> =
    result {
        do!
            if paymentAgreement |> PaymentAgreement.masterAgreementID = agreementId then Ok ()
            else
                let paymentAgreementUuid = paymentAgreement |> PaymentAgreement.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowPaymentAgreementNotUnderMasterAgreement(paymentAgreementUuid, agreementUuid))
        let (CashFlowComponent.DebitAccount debitAccountId) = paymentAgreement |> PaymentAgreement.debitAccount
        do!
            match debitAccountId |> confirmValidAccountId context with
            | Error(AccountIdDoesntMatch uuid) -> Error(CashflowPaymentAgreementDebitAccountInvalid uuid)
            | other -> other
        let (CashFlowComponent.CreditAccount creditAccountId) = paymentAgreement |> PaymentAgreement.creditAccount
        do!
            match creditAccountId |> confirmValidAccountId context with
            | Error(AccountIdDoesntMatch uuid) -> Error(CashflowPaymentAgreementCreditAccountInvalid uuid)
            | other -> other
        return!
            match paymentAgreement |> PaymentAgreement.expectedAmount with
            | None -> Ok ()
            | Some money when money |> Money.amount > 0M -> Ok ()
            | Some money ->
                let paymentAgreementUuid = paymentAgreement |> PaymentAgreement.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                let amount = money |> Money.amount
                Error(CashflowPaymentAgreementNonPositiveExpectedAmount(paymentAgreementUuid, amount))
    }

let private confirmPaymentAgreements
    (context: Context.Context)
    (agreementID: CashFlowComponent.MasterAgreementId)
    (paymentAgreements: PaymentAgreement.PaymentAgreement list)
    : Result<unit, AppError> =
    result {
        do! if paymentAgreements |> List.isEmpty then Error CashflowPaymentAgreementsListCannotBeEmpty else Ok ()
        return!
            paymentAgreements
            |> List.map (confirmPaymentAgreement context agreementID)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private confirmAgreementDates
    (agreementId: CashFlowComponent.MasterAgreementId)
    (agreementActivityPeriod: ActivityPeriod.ActivityPeriod)
    : Result<unit, AppError> =
    let referenceDate = today()
    match agreementActivityPeriod |> ActivityPeriod.isAvailable referenceDate with
    | true -> Ok ()
    | false ->
        let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
        let beginDate = agreementActivityPeriod |> ActivityPeriod.activeBegin
        let endDate = agreementActivityPeriod |> ActivityPeriod.activeEnd
        Error(CashflowMasterAgreementUnavailable(agreementUuid, referenceDate, beginDate, endDate))

let private confirmMasterAgreement
    (masterAgreement: MasterAgreement.MasterAgreement)
    : Result<unit, AppError> =
    result {
        let agreementId = masterAgreement |> MasterAgreement.agreementID
        let agreementActivityPeriod = masterAgreement |> MasterAgreement.activityPeriod
        return! confirmAgreementDates agreementId agreementActivityPeriod
    }

let private confirmComposite
    (context: Context.Context)
    (agreement: Agreement)
    : Result<unit, AppError> =
    result {
        do! agreement.masterAgreement |> confirmMasterAgreement
        do!
            agreement.paymentAgreements
            |> confirmPaymentAgreements context (agreement.masterAgreement |> MasterAgreement.agreementID)
        do!
            agreement.instances
            |> confirmInstances (agreement.masterAgreement |> MasterAgreement.agreementID)
        do! agreement.invoices |> confirmInvoices context
        do! agreement.payments |> confirmPayments context
    }

let constructNewAndSaveToDb
    (context: Context.Context)
    (agreementName: CashFlowComponent.AgreementName)
    (direction: CashFlowComponent.FlowDirection)
    (cadenceType: Cadence.CadenceType)
    (firstInstance: Cadence.CadenceNextInstance)
    (counterparty: CashFlowComponent.Counterparty)
    (agreementActivityPeriod: ActivityPeriod.ActivityPeriod)
    (memo: CashFlowComponent.AgreementMemo option)
    (paymentAgreementComponentsList:
        (CashFlowComponent.DebitAccount *
         CashFlowComponent.CreditAccount *
         Money option *
         CashFlowComponent.PaymentAgreementMemo option) list)
    : Result<Agreement, AppError> =
    result {
        let now = context |> Context.getInitiationInstant
        let agreementId = CashFlowComponent.MasterAgreementId.create()
        let! cadence = Cadence.create cadenceType firstInstance
        let masterAgreement =
            MasterAgreement.create agreementId agreementName direction cadence counterparty
                agreementActivityPeriod memo now now
        do! masterAgreement |> confirmMasterAgreement
        let paymentAgreements =
            paymentAgreementComponentsList |> List.map(fun (debitAccount, creditAccount, expectedAmount, memo) ->
                let paymentAgreementId = CashFlowComponent.PaymentAgreementId.create()
                PaymentAgreement.create paymentAgreementId agreementId debitAccount
                    creditAccount expectedAmount memo now now )
        do! paymentAgreements |> confirmPaymentAgreements context (masterAgreement |> MasterAgreement.agreementID)
        let agreement =
            { masterAgreement = masterAgreement
              paymentAgreements = paymentAgreements
              instances = []
              invoices = []
              payments = [] }
        do! agreement |> confirmComposite context
        do! masterAgreement |> MasterAgreement.insertNewToDb context
        do!
            paymentAgreements
            |> List.map (PaymentAgreement.insertNewToDb context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return agreement
    }

let private compileFromSubLists
    (masterAgreements: MasterAgreement.MasterAgreement list)
    (paymentAgreements: PaymentAgreement.PaymentAgreement list)
    (instances: Instance.Instance list)
    (invoices: Invoice.Invoice list)
    (payments: Payment.Payment list)
    : Agreement list =
    masterAgreements
    |> List.map (fun ma ->
        let agreementId = ma |> MasterAgreement.agreementID
        let paymentAgreementsAtMa =
            paymentAgreements |> List.filter (fun pa -> pa |> PaymentAgreement.masterAgreementID = agreementId)
        let instancesAtMa =
            instances |> List.filter (fun ins -> ins |> Instance.masterAgreementID = agreementId)
        let instanceIdsAtMa = instancesAtMa |> List.map Instance.instanceId
        let invoicesAtMa =
            invoices |> List.filter (fun inv -> instanceIdsAtMa |> List.contains (inv |> Invoice.instanceId))
        let invoiceIdsAtMa = invoicesAtMa |> List.map Invoice.invoiceId
        let paymentsAtMa =
            payments |> List.filter (fun pmt -> invoiceIdsAtMa |> List.contains (pmt |> Payment.invoiceId))
        { masterAgreement = ma
          paymentAgreements = paymentAgreementsAtMa
          instances = instancesAtMa
          invoices = invoicesAtMa
          payments = paymentsAtMa })

let fetchFiltered
    (context: Context.Context)
    (expectedRows: AcceptableExpectedRows)
    (filter: AgreementFilter)
    : Result<Agreement list, AppError> =
    result {
        let! masterAgreements =
            filter |> fetchCompositeFiltered context expectedRows MasterAgreement.readRowsFromDb TargetComposite.Agreement
        if masterAgreements |> List.isEmpty then return [] else
        let agreementIds = masterAgreements |> List.map MasterAgreement.agreementID
        let! paymentAgreements = agreementIds |> PaymentAgreement.fetchByMasterAgreementIdList context
        let! instances = agreementIds |> Instance.fetchByMasterAgreementIdList context
        let instanceIds = instances |> List.map Instance.instanceId
        let! invoices =
            if instanceIds |> List.isEmpty then Ok [] else instanceIds |> Invoice.fetchByInstanceIdList context
        let invoiceIds = invoices |> List.map Invoice.invoiceId
        let! payments =
            if invoiceIds |> List.isEmpty then Ok [] else invoiceIds |> Payment.fetchByInvoiceIdList context
        return compileFromSubLists masterAgreements paymentAgreements instances invoices payments
    }

let fetchByMasterAgreementId
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    : Result<Agreement, AppError> =
    result {
        let filter : AgreementFilter =
            { agreementIds = Some [ agreementId ]
              agreementNames = None
              direction = None
              activeAgreementsOnly = false
              accountIds = None
              paymentAgreementExpectedAmount = None
              instanceTemporalFilter = None
              externalInvoiceId = None
              invoiceDateTemporalFilter = None
              invoiceDueTemporalFilter = None
              invoiceAmount = None
              invoiceState = None
              invoicePaymentState = None
              invoicePostedState = None
              invoiceBlocker = None
              journalEntryHeaderId = None
              stageEntryHeaderId = None
              paymentAmount = None
              paymentPostedToLedgerTemporalFilter = None }
        let agreementsResult = filter |> fetchFiltered context ExactlyOne
        return!
            match agreementsResult with
            | Ok agreements -> Ok (agreements |> List.head)
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashflowMasterAgreementIdDoesntExist agreementUuid)
            | Error e -> Error e
    }
    
let fetchAllActiveAgreements
    (context: Context.Context) =
    let filter : AgreementFilter =
        { agreementIds = None
          agreementNames = None
          direction = None
          activeAgreementsOnly = true
          accountIds = None
          paymentAgreementExpectedAmount = None
          instanceTemporalFilter = None
          externalInvoiceId = None
          invoiceDateTemporalFilter = None
          invoiceDueTemporalFilter = None
          invoiceAmount = None
          invoiceState = None
          invoicePaymentState = None
          invoicePostedState = None
          invoiceBlocker = None
          journalEntryHeaderId = None
          stageEntryHeaderId = None
          paymentAmount = None
          paymentPostedToLedgerTemporalFilter = None }
    filter |> fetchFiltered context AnyQuantityIsAcceptable

let private isThereAMasterAgreementUpdate
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : bool =
    masterAgreementUpdates.agreementNameUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.directionUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.cadenceUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.counterpartyUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.activityPeriodUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.memoUpdate <> FieldUpdate.NoChange

let private isThereAPaymentAgreementUpdate
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    : bool =
    paymentAgreementUpdates
    |> List.map (fun u ->
        u.debitAccountUpdate <> FieldUpdate.NoChange
        || u.creditAccountUpdate <> FieldUpdate.NoChange
        || u.expectedAmountUpdate <> FieldUpdate.NoChange
        || u.memoUpdate <> FieldUpdate.NoChange)
    |> List.exists id

let private isThereAnInstanceUpdate
    (instanceUpdates: Instance.InstanceFieldUpdates list)
    : bool =
    instanceUpdates
    |> List.map (fun u -> u.instanceDateUpdate <> FieldUpdate.NoChange || u.isFulfilledUpdate <> FieldUpdate.NoChange)
    |> List.exists id

let private isThereAnInvoiceUpdate
    (invoiceUpdates: Invoice.InvoiceFieldUpdates list)
    : bool =
    invoiceUpdates
    |> List.map (fun u ->
        u.externalInvoiceIdUpdate <> FieldUpdate.NoChange
        || u.invoiceDateUpdate <> FieldUpdate.NoChange
        || u.dueDateUpdate <> FieldUpdate.NoChange
        || u.amountUpdate <> FieldUpdate.NoChange
        || u.invoiceStateUpdate <> FieldUpdate.NoChange
        || u.paymentStateUpdate <> FieldUpdate.NoChange
        || u.postedStateUpdate <> FieldUpdate.NoChange
        || u.blockerUpdate <> FieldUpdate.NoChange
        || u.memoUpdate <> FieldUpdate.NoChange)
    |> List.exists id

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
let updateAgreement
    (context: Context.Context)
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    (instanceUpdates: Instance.InstanceFieldUpdates list)
    (invoiceUpdates: Invoice.InvoiceFieldUpdates list)
    (paymentUpdates: Payment.PaymentFieldUpdates list)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<Agreement, AppError> =
    result {
        let shouldUpdateMasterAgreement = masterAgreementUpdates |> isThereAMasterAgreementUpdate
        let shouldUpdatePaymentAgreements = paymentAgreementUpdates |> isThereAPaymentAgreementUpdate
        let shouldUpdateInstances = instanceUpdates |> isThereAnInstanceUpdate
        let shouldUpdateInvoices = invoiceUpdates |> isThereAnInvoiceUpdate
        let shouldUpdatePayments = paymentUpdates |> isThereAPaymentUpdate
        do!
            if shouldUpdateMasterAgreement = false
               && shouldUpdatePaymentAgreements = false
               && shouldUpdateInstances = false
               && shouldUpdateInvoices = false
               && shouldUpdatePayments = false
            then Error CashflowAgreementUpdateNoOp
            else Ok ()
        do!
            confirmAuthorityAndCohesion
                context paymentAgreementUpdates instanceUpdates invoiceUpdates paymentUpdates masterAgreementUpdates
        do!
            if shouldUpdateMasterAgreement then masterAgreementUpdates |> MasterAgreement.updateDb context |> Result.map ignore
            else Ok ()
        do!
            if shouldUpdatePaymentAgreements then
                paymentAgreementUpdates
                |> List.map (PaymentAgreement.updateDb context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        do!
            if shouldUpdateInstances then
                instanceUpdates
                |> List.map (Instance.updateDb context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        do!
            if shouldUpdateInvoices then
                invoiceUpdates
                |> List.map (Invoice.updateDb context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        do!
            if shouldUpdatePayments then
                paymentUpdates
                |> List.map (Payment.updateDb context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        // fetch the composite to ensure it passes all validations. hopefully the caller rolls back on error
        let! fetched = masterAgreementUpdates.agreementIdToUpdate |> fetchByMasterAgreementId context
        do! fetched |> confirmComposite context
        return fetched
    }
