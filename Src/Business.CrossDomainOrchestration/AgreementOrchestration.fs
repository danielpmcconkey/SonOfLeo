module Business.CrossDomainOrchestration.AgreementOrchestration

open App.Utility.IAppError
open App.DataAccessLayer.DalError
open App.Utility.Calendar
open App.Utility.FieldUpdate
open App.Utility.Result
open App.DataAccessLayer
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.CashFlow
open Business.CrossDomainOrchestration.CashFlowCompositeFetcher
open Business.CrossDomainOrchestration.FetchFilters

type Agreement = private {
    masterAgreement: MasterAgreement.MasterAgreement
    paymentAgreements: PaymentAgreement.PaymentAgreement list
    instances: Instance.Instance list
    invoices: Invoice.Invoice list
    payments: Payment.Payment list
}

/// PaymentAgreementPrimitives are the validated fields of a Payment Agreement not yet created.
type PaymentAgreementPrimitives =
    CashFlowComponent.PaymentAgreementName *
    CashFlowComponent.DebitAccount *
    CashFlowComponent.CreditAccount *
    Money.Money option *
    CashFlowComponent.DaysDueAfterInvoiceDate option *
    CashFlowComponent.PaymentAgreementMemo option

let masterAgreement (agreement:Agreement) = agreement.masterAgreement
let paymentAgreements (agreement:Agreement) = agreement.paymentAgreements
let instances (agreement:Agreement) = agreement.instances
let invoices (agreement:Agreement) = agreement.invoices
let payments (agreement:Agreement) = agreement.payments

let private confirmValidAccountId
    (context: Context.Context)
    (accountId: AccountId)
    : Result<unit, IAppError> =
    let accountUuid = accountId |> AccountId.value
    let lookupResult = // we don't need the code; we just check that the ID is in the DB this way
        accountUuid |> LookupCache.accountIdToCode.fetch (context |> Context.getDatabaseTransaction)
    lookupResult
    |> whenNoRows (LedgerError.AccountIdDoesntMatch accountUuid)
    |> Result.map ignore

let private confirmPaymentAgreementBelongsToAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (fieldUpdates: PaymentAgreement.PaymentAgreementFieldUpdates)
    : Result<unit, IAppError> =
    result {
        let! paymentAgreement = fieldUpdates.paymentAgreementIdToUpdate |> PaymentAgreement.fetchById context
        return!
            if paymentAgreement |> PaymentAgreement.masterAgreementID = agreementId then Ok ()
            else
                let paymentAgreementUuid =
                    fieldUpdates.paymentAgreementIdToUpdate |> CashFlowComponent.PaymentAgreementId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(
                    CashFlowError.CashflowPaymentAgreementNotUnderMasterAgreement(paymentAgreementUuid, agreementUuid))
    }

let private confirmAuthorityAndCohesion
    (context: Context.Context)
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<unit, IAppError> =
    let agreementId = masterAgreementUpdates.agreementIdToUpdate
    paymentAgreementUpdates
    |> List.map (confirmPaymentAgreementBelongsToAgreement context agreementId)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmInstance
    (agreementId: CashFlowComponent.MasterAgreementId)
    (instance: Instance.Instance)
    : Result<unit, IAppError> =
    if instance |> Instance.masterAgreementID = agreementId then Ok ()
    else
        let uuid = instance
                |> Instance.instanceId
                |> CashFlowComponent.InstanceId.value
        let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
        Error(CashFlowError.CashflowInstanceNotUnderMasterAgreement(uuid, agreementUuid))

let private confirmInstances
    (agreementId: CashFlowComponent.MasterAgreementId)
    (instances: Instance.Instance list)
    : Result<unit, IAppError> =
    instances
    |> List.map (confirmInstance agreementId)
    |> convertListOfResultsToResultsList
    |> Result.map ignore

let private confirmPaymentAgreement
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (paymentAgreement: PaymentAgreement.PaymentAgreement)
    : Result<unit, IAppError> =
    result {
        do!
            if paymentAgreement |> PaymentAgreement.masterAgreementID = agreementId then Ok ()
            else
                let paymentAgreementUuid =
                    paymentAgreement
                    |> PaymentAgreement.paymentAgreementId
                    |> CashFlowComponent.PaymentAgreementId.value
                let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                Error(CashFlowError.CashflowPaymentAgreementNotUnderMasterAgreement(paymentAgreementUuid, agreementUuid))
        let (CashFlowComponent.DebitAccount debitAccountId) = paymentAgreement |> PaymentAgreement.debitAccount
        do!
            match debitAccountId |> confirmValidAccountId context with
            | Error (AsError (LedgerError.AccountIdDoesntMatch uuid)) ->
                Error (CashFlowError.CashflowPaymentAgreementDebitAccountInvalid uuid)
            | other -> other
        let (CashFlowComponent.CreditAccount creditAccountId) = paymentAgreement |> PaymentAgreement.creditAccount
        do!
            match creditAccountId |> confirmValidAccountId context with
            | Error (AsError (LedgerError.AccountIdDoesntMatch uuid)) ->
                Error (CashFlowError.CashflowPaymentAgreementCreditAccountInvalid uuid)
            | other -> other
        do!
            if debitAccountId <> creditAccountId then Ok ()
            else Error (CashFlowError.CashflowPaymentAgreementDebitEqualsCredit(debitAccountId |> AccountId.value))
        return!
            match paymentAgreement |> PaymentAgreement.expectedAmount with
            | None -> Ok ()
            | Some money when money |> Money.amount > 0M -> Ok ()
            | Some money ->
                let paymentAgreementUuid =
                    paymentAgreement
                    |> PaymentAgreement.paymentAgreementId
                    |> CashFlowComponent.PaymentAgreementId.value
                let amount = money |> Money.amount
                Error(CashFlowError.CashflowPaymentAgreementNonPositiveExpectedAmount(paymentAgreementUuid, amount))
    }

let private confirmPaymentAgreements
    (context: Context.Context)
    (agreementID: CashFlowComponent.MasterAgreementId)
    (paymentAgreements: PaymentAgreement.PaymentAgreement list)
    : Result<unit, IAppError> =
    result {
        do!
            if paymentAgreements |> List.isEmpty
            then Error CashFlowError.CashflowPaymentAgreementsListCannotBeEmpty
            else Ok ()
        return!
            paymentAgreements
            |> List.map (confirmPaymentAgreement context agreementID)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private confirmAgreementDates
    (context: Context.Context)
    (agreementId: CashFlowComponent.MasterAgreementId)
    (agreementActivityPeriod: ActivityPeriod.ActivityPeriod)
    : Result<unit, IAppError> =
    let referenceDate = context |> Context.getInitiationInstant |> dateFromInstant
    match agreementActivityPeriod |> ActivityPeriod.isAvailable referenceDate with
    | true -> Ok ()
    | false ->
        let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
        let beginDate = agreementActivityPeriod |> ActivityPeriod.activeBegin
        let endDate = agreementActivityPeriod |> ActivityPeriod.activeEnd
        Error(CashFlowError.CashflowMasterAgreementUnavailable(agreementUuid, referenceDate, beginDate, endDate))

let private confirmMasterAgreement
    (context: Context.Context)
    (masterAgreement: MasterAgreement.MasterAgreement)
    : Result<unit, IAppError> =
    result {
        let agreementId = masterAgreement |> MasterAgreement.agreementID
        let agreementActivityPeriod = masterAgreement |> MasterAgreement.activityPeriod
        return! confirmAgreementDates context agreementId agreementActivityPeriod
    }

let private confirmComposite
    (context: Context.Context)
    (agreement: Agreement)
    : Result<unit, IAppError> =
    result {
        do! agreement.masterAgreement |> confirmMasterAgreement context
        do!
            agreement.paymentAgreements
            |> confirmPaymentAgreements context (agreement.masterAgreement |> MasterAgreement.agreementID)
        do!
            agreement.instances
            |> confirmInstances (agreement.masterAgreement |> MasterAgreement.agreementID)
        return!
            InstanceOrchestration.compileInstanceCompositesFromSubLists
                agreement.instances agreement.invoices agreement.payments
            |> List.map (InstanceOrchestration.confirmInstanceComposite context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private newPaymentAgreement
    (now: NodaTime.Instant)
    (agreementId: CashFlowComponent.MasterAgreementId)
    ((paymentAgreementName, debitAccount, creditAccount, expectedAmount, daysDueAfterInvoiceDate, memo):
        PaymentAgreementPrimitives)
    : PaymentAgreement.PaymentAgreement =
    let paymentAgreementId = CashFlowComponent.PaymentAgreementId.create()
    PaymentAgreement.create paymentAgreementId agreementId paymentAgreementName debitAccount
        creditAccount expectedAmount daysDueAfterInvoiceDate memo now now

let constructNewAndPersist
    (context: Context.Context)
    (agreementName: CashFlowComponent.AgreementName)
    (direction: CashFlowComponent.FlowDirection)
    (cadenceType: Cadence.CadenceType)
    (firstInstance: Cadence.CadenceNextInstance)
    (counterparty: CashFlowComponent.Counterparty)
    (agreementActivityPeriod: ActivityPeriod.ActivityPeriod)
    (memo: CashFlowComponent.AgreementMemo option)
    (paymentAgreementComponentsList: PaymentAgreementPrimitives list)
    : Result<Agreement, IAppError> =
    result {
        let now = context |> Context.getInitiationInstant
        let agreementId = CashFlowComponent.MasterAgreementId.create()
        let! cadence = Cadence.create cadenceType firstInstance
        let masterAgreement =
            MasterAgreement.create agreementId agreementName direction cadence counterparty
                agreementActivityPeriod memo now now
        do! masterAgreement |> confirmMasterAgreement context
        let paymentAgreements = paymentAgreementComponentsList |> List.map (newPaymentAgreement now agreementId)
        do! paymentAgreements |> confirmPaymentAgreements context (masterAgreement |> MasterAgreement.agreementID)
        let agreement =
            { masterAgreement = masterAgreement
              paymentAgreements = paymentAgreements
              instances = []
              invoices = []
              payments = [] }
        do! agreement |> confirmComposite context
        do! masterAgreement |> MasterAgreement.persist context
        do!
            paymentAgreements
            |> List.map (PaymentAgreement.persist context)
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
    : Result<Agreement list, IAppError> =
    result {
        let! masterAgreements =
            filter |> fetchCompositeFiltered context expectedRows MasterAgreement.query TargetComposite.Agreement
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
    : Result<Agreement, IAppError> =
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
              journalEntryLineId = None
              stageEntryLineId = None
              paymentAmount = None
              paymentPostedToLedgerTemporalFilter = None }
        let agreementsResult = filter |> fetchFiltered context ExactlyOne
        let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
        return!
            agreementsResult
            |> whenNoRows (CashFlowError.CashflowMasterAgreementIdDoesntExist agreementUuid)
            |> Result.map (fun agreements -> agreements |> List.head)
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
          journalEntryLineId = None
          stageEntryLineId = None
          paymentAmount = None
          paymentPostedToLedgerTemporalFilter = None }
    filter |> fetchFiltered context AnyQuantityIsAcceptable

/// confirmNextInstanceAfterExistingInstances rejects a cadence update whose next-instance date is on or before an
/// existing Instance's date: the next sweep would try to create that Instance again. A cadence change does not
/// re-check existing Instances against the new cadence.
let private confirmNextInstanceAfterExistingInstances
    (context: Context.Context)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<unit, IAppError> =
    match masterAgreementUpdates.cadenceUpdate with
    | FieldUpdate.NoChange -> Ok ()
    | FieldUpdate.SetTo cadence ->
        result {
            let agreementId = masterAgreementUpdates.agreementIdToUpdate
            let! instances = [ agreementId ] |> Instance.fetchByMasterAgreementIdList context
            let nextInstance = (cadence |> Cadence.nextInstance).nextInstance
            return!
                match instances |> List.map Instance.instanceDate |> List.sortDescending |> List.tryHead with
                | Some latest when nextInstance <= latest ->
                    let agreementUuid = agreementId |> CashFlowComponent.MasterAgreementId.value
                    CashFlowError.error (
                        CashFlowError.CashflowMasterAgreementNextInstanceNotAfterExistingInstances(
                            agreementUuid, nextInstance, latest))
                | Some _
                | None -> Ok ()
        }

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
        u.paymentAgreementNameUpdate <> FieldUpdate.NoChange
        || u.debitAccountUpdate <> FieldUpdate.NoChange
        || u.creditAccountUpdate <> FieldUpdate.NoChange
        || u.expectedAmountUpdate <> FieldUpdate.NoChange
        || u.daysDueAfterInvoiceDateUpdate <> FieldUpdate.NoChange
        || u.memoUpdate <> FieldUpdate.NoChange)
    |> List.exists id

/// updateAgreement changes the master agreement and its legs, and adds new legs. No leg is removed. Instances, Invoices
/// and Payments change through the instance composite, which derives payment state, posted state and is-fulfilled
/// (REQ-CF-9.11); a leg's new amount or accounts leave its existing Invoices and Payments as they are.
/// Note to caller, the updates are sent to the DB *before* aggregate validation. Make sure you wrap this in a
/// transaction you can roll back
let updateAgreement
    (context: Context.Context)
    (paymentAgreementUpdates: PaymentAgreement.PaymentAgreementFieldUpdates list)
    (newPaymentAgreements: PaymentAgreementPrimitives list)
    (masterAgreementUpdates: MasterAgreement.MasterAgreementFieldUpdates)
    : Result<Agreement, IAppError> =
    result {
        let shouldUpdateMasterAgreement = masterAgreementUpdates |> isThereAMasterAgreementUpdate
        let shouldUpdatePaymentAgreements = paymentAgreementUpdates |> isThereAPaymentAgreementUpdate
        do!
            if shouldUpdateMasterAgreement = false
               && shouldUpdatePaymentAgreements = false
               && newPaymentAgreements |> List.isEmpty
            then Error CashFlowError.CashflowAgreementUpdateNoOp
            else Ok ()
        do! confirmAuthorityAndCohesion context paymentAgreementUpdates masterAgreementUpdates
        do! masterAgreementUpdates |> confirmNextInstanceAfterExistingInstances context
        do!
            if shouldUpdateMasterAgreement then masterAgreementUpdates |> MasterAgreement.update context |> Result.map ignore
            else Ok ()
        do!
            if shouldUpdatePaymentAgreements then
                paymentAgreementUpdates
                |> List.map (PaymentAgreement.update context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        let agreementId = masterAgreementUpdates.agreementIdToUpdate
        let addedLegs =
            newPaymentAgreements |> List.map (newPaymentAgreement (context |> Context.getInitiationInstant) agreementId)
        do!
            addedLegs
            |> List.map (fun leg -> result {
                do! leg |> confirmPaymentAgreement context agreementId
                return! leg |> PaymentAgreement.persist context })
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        // fetch the composite to ensure it passes all validations. hopefully the caller rolls back on error
        let! fetched = masterAgreementUpdates.agreementIdToUpdate |> fetchByMasterAgreementId context
        do! fetched |> confirmComposite context
        return fetched
    }
