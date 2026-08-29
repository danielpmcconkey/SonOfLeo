module ModelOrchestrator.AgreementOrchestration

open DataAccessLayer.ExecuteReader
open Model
open Model.CashFlow
open Model.DataIngestion
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.CashFlowCompositeFetcher
open ModelOrchestrator.FetchFilters
open NodaTime
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
                Error(
                    CashflowPaymentAgreementNotUnderMasterAgreement(
                        fieldUpdates.paymentAgreementIdToUpdate |> CashFlowComponent.PaymentAgreementId.value,
                        agreementId |> CashFlowComponent.MasterAgreementId.value))
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
                Error(
                    CashflowInstanceNotUnderMasterAgreement(
                        fieldUpdates.instanceIdToUpdate |> CashFlowComponent.InstanceId.value,
                        agreementId |> CashFlowComponent.MasterAgreementId.value))
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
                Error(
                    CashflowInvoiceNotUnderMasterAgreement(
                        fieldUpdates.invoiceIdToUpdate |> CashFlowComponent.InvoiceId.value,
                        agreementId |> CashFlowComponent.MasterAgreementId.value))
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
                Error(
                    CashflowPaymentNotUnderMasterAgreement(
                        fieldUpdates.paymentIdToUpdate |> CashFlowComponent.PaymentId.value,
                        agreementId |> CashFlowComponent.MasterAgreementId.value))
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
                Error(CashflowInvoiceIdDoesntExist(payment |> Payment.invoiceId |> CashFlowComponent.InvoiceId.value))
            | Error e -> Error e
        // JE and SE existence is checked below via whichever half of the transactionPointer is actually populated;
        // the other half isn't reachable off a reconstituted Payment (see transactionPointerFromColumns).
        let! journalEntryHeader =
            match payment |> Payment.transactionPointer with
            | CashFlowComponent.Posted journalEntryHeaderId ->
                match journalEntryHeaderId |> JournalEntryHeader.fetchById context with
                | Ok header -> Ok(Some header)
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    Error(JournalEntryHeaderIdDoesntExist(journalEntryHeaderId |> JournalEntryHeaderId.value))
                | Error e -> Error e
            | CashFlowComponent.Staged stageEntryHeaderId ->
                match stageEntryHeaderId |> StageEntryHeader.fetchById context with
                | Ok _ -> Ok None
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    Error(IngestionStageEntryHeaderIdDoesntExist(stageEntryHeaderId |> StageEntryHeaderId.value))
                | Error e -> Error e
        return!
            match payment |> Payment.postedToLedgerDate, journalEntryHeader with
            | None, _ -> Ok ()
            | Some _, None ->
                Error(CashflowPaymentPostedToLedgerDateWithoutJournalEntry(
                    payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value))
            | Some providedDate, Some header ->
                let actualDate = header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                if providedDate = actualDate then Ok ()
                else
                    Error(
                        CashflowPaymentPostedToLedgerDateMismatch(
                            payment
                            |> Payment.paymentId
                            |> CashFlowComponent.PaymentId.value, providedDate, actualDate))
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
                Error(CashflowInstanceIdDoesntExist(
                    invoice |> Invoice.instanceId |> CashFlowComponent.InstanceId.value))
            | Error e -> Error e
        do!
            match invoice |> Invoice.paymentAgreementId |> PaymentAgreement.fetchById context with
            | Ok _ -> Ok ()
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                Error(CashflowPaymentAgreementIdDoesntExist(
                    invoice |> Invoice.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value))
            | Error e -> Error e
        let invoiceAmount = invoice |> Invoice.amount |> Money.amount
        return!
            if invoiceAmount >= 0M then Ok ()
            else
                let uuid = invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value
                Error(CashflowInvoiceNegativeAmount(uuid, invoiceAmount))
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
                Error(
                    CashflowPaymentAgreementNotUnderMasterAgreement(
                        paymentAgreement |> PaymentAgreement.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value,
                        agreementId |> CashFlowComponent.MasterAgreementId.value))
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
                Error(
                    CashflowPaymentAgreementNonPositiveExpectedAmount(
                        paymentAgreement |> PaymentAgreement.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value,
                        money |> Money.amount))
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
    (startDate: LocalDate)
    (endDate: LocalDate option)
    : Result<unit, AppError> =
    if endDate |> Option.isNone then Ok () else
    let endDateValue = endDate |> Option.get
    if endDateValue < startDate then Error(CashflowAgreementEndDateBeforeStartDate(startDate, endDateValue))
    elif endDateValue < today() then Error(CashflowAgreementEndDateInPast endDateValue)
    else Ok ()

let private confirmMasterAgreement
    (masterAgreement: MasterAgreement.MasterAgreement)
    : Result<unit, AppError> =
    confirmAgreementDates (masterAgreement |> MasterAgreement.startDate) (masterAgreement |> MasterAgreement.endDate)

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

/// Note to caller, master agreement and payment agreements are persisted into the DB *before* composite validation.
/// Make sure you wrap this in a transaction you can roll back
let constructNewAndSaveToDb
    (context: Context.Context)
    (paymentAgreements: PaymentAgreement.PaymentAgreement list)
    (masterAgreement: MasterAgreement.MasterAgreement)
    : Result<Agreement, AppError> =
    result {
        do! masterAgreement |> confirmMasterAgreement
        do! paymentAgreements |> confirmPaymentAgreements context (masterAgreement |> MasterAgreement.agreementID)
        do! masterAgreement |> MasterAgreement.insertNewToDb context
        do!
            paymentAgreements
            |> List.map (PaymentAgreement.insertNewToDb context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let agreement =
            { masterAgreement = masterAgreement
              paymentAgreements = paymentAgreements
              instances = []
              invoices = []
              payments = [] }
        do! agreement |> confirmComposite context
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
        let agreementsResult = filter |> fetchFiltered context AnyQuantityIsAcceptable
        return!
            match agreementsResult with
            | Ok x -> Ok (x |> List.head)
            | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                Error(CashflowMasterAgreementIdDoesntExist(agreementId |> CashFlowComponent.MasterAgreementId.value))
            | Error e -> Error e
    }

let private isThereAMasterAgreementUpdate
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : bool =
    masterAgreementUpdates.agreementNameUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.directionUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.cadenceUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.counterpartyUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.startDateUpdate <> FieldUpdate.NoChange
    || masterAgreementUpdates.endDateUpdate <> FieldUpdate.NoChange
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
