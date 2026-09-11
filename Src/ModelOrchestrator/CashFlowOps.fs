module ModelOrchestrator.CashFlowOps

open System
open Model.CashFlow
open Model.CashFlow.CashFlowComponent
open Model.DataIngestion
open Model.StageDataClassification
open ModelOrchestrator
open NodaTime
open Utilities
open Utilities.AppError
open Utilities.ResultHelper

let rec private fillInstanceDatesToCutOff
    (nextDate: LocalDate)
    (cutOffDate: LocalDate)
    (cadenceType: Cadence.CadenceType)
    (accumulator: LocalDate list)
    : LocalDate list =
    if nextDate > cutOffDate then accumulator // break out of the recursion
    else 
    let nextNextDate = Cadence.determineNextDateFromPrior nextDate cadenceType
    fillInstanceDatesToCutOff nextNextDate cutOffDate cadenceType (nextDate::accumulator)

let private spawnInstancesFromAgreement
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    (agreement: AgreementOrchestration.Agreement)
    : Result<unit, AppError> =
    result {
        let today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
        let cutOffDate = today.PlusDays(daysOut |> ProjectionHorizonInDays.value)
        let master = agreement |> AgreementOrchestration.masterAgreement
        let agreementId = master |> MasterAgreement.agreementID
        let cadence = master |> MasterAgreement.cadence
        let cadenceType = cadence |> Cadence.cadenceType
        let nextInstance = cadence |> Cadence.nextInstance
        let nextInstanceDate = nextInstance.nextInstance
        // ascending because each create validates against the agreement's latest existing instance and only ever
        // moves forward
        let neededDates =
            fillInstanceDatesToCutOff nextInstanceDate cutOffDate cadenceType []
            |> List.sortBy id
        if neededDates |> List.isEmpty then return () else
        do! neededDates
            |> List.map(fun neededDate ->
                // check if we have any fixed-amount payment agreements and add invoices for those with our instance.
                // a payment agreement without a daysDueAfterInvoiceDate gives us no way to derive a due date, so we
                // skip it here and let the invoice get created later, once the real bill is in hand
                let paymentAgreements = agreement |> AgreementOrchestration.paymentAgreements
                let invoiceCompositeFieldsList = paymentAgreements |> List.choose(fun paymentAgreement ->
                    match paymentAgreement |> PaymentAgreement.expectedAmount,
                          paymentAgreement |> PaymentAgreement.daysDueAfterInvoiceDate with
                    | Some expectedAmount, Some daysDueAfterInvoiceDate ->
                        let paId = paymentAgreement |> PaymentAgreement.paymentAgreementId
                        let extInvoiceId = None
                        let invoiceDate = {InvoiceDate.localDate = neededDate}
                        let daysPastInvDateForDueDate = daysDueAfterInvoiceDate |> DaysDueAfterInvoiceDate.value
                        let dueDate = {DueDate.localDate = neededDate.PlusDays(daysPastInvDateForDueDate)}
                        let amount = { InvoiceAmount.money = expectedAmount }
                        let direction = master |> MasterAgreement.direction
                        let invoiceState = if direction = Income then InvoiceGenerated else InvoiceReceived
                        let lifecycle = { invoiceState = invoiceState; paymentState = NotYetPaid
                                          postedState = NotHandled; blocker = None }
                        let invMemo = None
                        Some (paId, extInvoiceId, invoiceDate, dueDate, amount, lifecycle, invMemo, [])
                    | _ -> None
                    )
                InstanceOrchestration.createInstanceCompositeAndSaveToDb
                        context agreementId neededDate false invoiceCompositeFieldsList)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private spawnInstancesFromAgreements
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    (agreements: AgreementOrchestration.Agreement list)
    : Result<unit, AppError> =
    agreements
    |> List.map (spawnInstancesFromAgreement context daysOut)
    |> convertListOfResultsToResultsList
    |> Result.map ignore


let createUpcomingInstances
    (context: Context.Context)
    (daysOut: ProjectionHorizonInDays)
    : Result<InstanceOrchestration.InstanceComposite list, AppError> =
    result {
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        do! agreements |> spawnInstancesFromAgreements context daysOut
        return! false |> InstanceOrchestration.fetchCompositesByIsFulfilled context
    }

let private paymentAgreementsClaimedBy
    (result: StageDataClassificationComponent.ClassificationResult)
    : PaymentAgreementId list =
    let idsFromMatches (matches: StageDataClassificationComponent.PrioritizedMatch list) =
        matches |> List.choose _.paymentAgreementId
    match result.outcome with
    | StageDataClassificationComponent.NoMatch -> []
    | StageDataClassificationComponent.OneMatch prioritizedMatch -> idsFromMatches [ prioritizedMatch ]
    | StageDataClassificationComponent.ManyMatchesClearWinner (winner, _) -> idsFromMatches [ winner ]
    | StageDataClassificationComponent.ManyMatchesTied ties -> idsFromMatches ties

/// pivotClassificationResultsByPaymentAgreement flips the classifier's row-focused answer -- "which rules did this row
/// match" -- onto the rule axis: "which rows claimed this payment agreement". Two staged entries claiming one payment
/// agreement is the dangerous case, since paying the same bill twice looks like a fulfilled obligation, so a contested
/// agreement is handed to the operator whole rather than resolved here.
let pivotClassificationResultsByPaymentAgreement
    (results: StageDataClassificationComponent.ClassificationResult list)
    : StageDataClassificationComponent.PaymentAgreementTaggingResult =
    let isTied (result: StageDataClassificationComponent.ClassificationResult) =
        match result.outcome with
        | StageDataClassificationComponent.ManyMatchesTied _ -> true
        | _ -> false
    let clusters =
        results
        |> List.collect(fun result ->
            result |> paymentAgreementsClaimedBy |> List.map (fun paymentAgreementId -> paymentAgreementId, result))
        |> List.groupBy fst
        |> List.map(fun (paymentAgreementId, pairs) ->
            let claimants = pairs |> List.map snd
            let cluster: StageDataClassificationComponent.PaymentAgreementClaimCluster =
                { paymentAgreementId = paymentAgreementId
                  claimants = claimants
                  containsUnwrittenTies = claimants |> List.exists isTied }
            cluster)
    // code is not allowed to break a tie, so a tied claimant contests its agreement however few rows claimed it
    let isContested (cluster: StageDataClassificationComponent.PaymentAgreementClaimCluster) =
        cluster.claimants |> List.length > 1 || cluster.containsUnwrittenTies
    { clean = clusters |> List.filter (isContested >> not)
      multiClaimant = clusters |> List.filter isContested
      unmatched = results |> List.filter (fun result -> result |> paymentAgreementsClaimedBy |> List.isEmpty) }

/// classifyPaymentAgreements does not update a stage entry's status. That belongs to the data ingestion domain.
let classifyPaymentAgreements
    (context: Context.Context)
    : Result<InstanceOrchestration.PaymentAgreementClassificationResult, AppError> =
    result {
        let rosterStatuses =
            [ StageEntryComponent.Ingested
              StageEntryComponent.Classified
              StageEntryComponent.NoMatch
              StageEntryComponent.Conflict ]
        let! roster = rosterStatuses |> StageEntryOrchestration.fetchByStatusList context
        // an entry whose lines all carry an account is still a candidate here. account assignment and obligation
        // linkage are independent questions about the same row
        let (matchCandidates: StageDataClassificationComponent.MatchCandidate list) =
            roster
            |> List.collect(fun entry ->
                let header = entry |> StageEntryOrchestration.stageEntryHeader
                entry
                |> StageEntryOrchestration.seLines
                |> List.map (fun line -> {
                    headerIdOfCandidate = header |> StageEntryHeader.stageEntryHeaderId
                    lineIdOfCandidate = line |> StageEntryLine.stageEntryLineId
                    ingestionSource = header |> StageEntryHeader.ingestionSource |> IngestionSource.name
                    description = header |> StageEntryHeader.description
                    amount = line |> StageEntryLine.amount
                    lineType = line |> StageEntryLine.lineType
                    memo = line |> StageEntryLine.memo }))
        let! classificationRun =
            matchCandidates
            |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches
                context StageDataClassificationComponent.PaymentAgreementClaimant
        return raise(NotImplementedException())
    }

// how many days past an invoice's due date a payment may land and still be considered a match for it
let private gracePeriodInDaysFromCadenceType (cadenceType: Cadence.CadenceType) : int =
    match cadenceType with
    | Cadence.Daily -> 0
    | Cadence.Weekly _ -> 2
    | Cadence.EveryOtherWeek _ -> 4
    | Cadence.Monthly _ -> 7
    | Cadence.Annually _ -> 7

let Projection() =
    // Takes a horizon. Reads ledger balances + open invoices. Returns per-account `{ currentBalance, knownInflows,
    // knownOutflows, projectedLow }` + `billsToChase` (instances with no invoice).
    raise(NotImplementedException())

let transitionPaymentsToPosted() =
    // No input. For every Payment pointing at a staged entry that now has a JE, transitions pointer to Posted + updates
    // invoice posted state. Returns the list.
    raise(NotImplementedException())
