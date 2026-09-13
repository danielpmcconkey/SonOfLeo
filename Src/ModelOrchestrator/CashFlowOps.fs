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

// a tie claims every agreement it tied across; a clear winner claims only the winner, since the losers lost
let private claimingMatches
    (result: StageDataClassificationComponent.ClassificationResult)
    : StageDataClassificationComponent.PrioritizedMatch list =
    match result.outcome with
    | StageDataClassificationComponent.NoMatch -> []
    | StageDataClassificationComponent.OneMatch prioritizedMatch -> [ prioritizedMatch ]
    | StageDataClassificationComponent.ManyMatchesClearWinner (winner, _) -> [ winner ]
    | StageDataClassificationComponent.ManyMatchesTied ties -> ties

let private paymentAgreementsClaimedBy
    (result: StageDataClassificationComponent.ClassificationResult)
    : PaymentAgreementId list =
    result |> claimingMatches |> List.choose _.paymentAgreementId

let private matchesClaimingPaymentAgreement
    (paymentAgreementId: PaymentAgreementId)
    (result: StageDataClassificationComponent.ClassificationResult)
    : StageDataClassificationComponent.PrioritizedMatch list =
    result
    |> claimingMatches
    |> List.filter (fun prioritizedMatch -> prioritizedMatch.paymentAgreementId = Some paymentAgreementId)

let private isTiedClaimant (result: StageDataClassificationComponent.ClassificationResult) : bool =
    match result.outcome with
    | StageDataClassificationComponent.ManyMatchesTied _ -> true
    | _ -> false

let private decisionFor
    (paymentAgreementId: PaymentAgreementId)
    (outcome: StageDataClassificationComponent.PaymentAgreementDecisionOutcome)
    (result: StageDataClassificationComponent.ClassificationResult)
    : StageDataClassificationComponent.PaymentAgreementDecision =
    let ruleIds =
        result
        |> matchesClaimingPaymentAgreement paymentAgreementId
        |> List.map (fun prioritizedMatch -> prioritizedMatch.ruleId)
    { stageEntryLineId = result.candidate.lineIdOfCandidate
      paymentAgreementId = Some paymentAgreementId
      ruleIds = ruleIds
      outcome = outcome }

/// pivotClaimsByPaymentAgreement flips the classifier's row-focused answer -- "which rules did this row match" -- onto
/// the rule axis: "which rows claimed this payment agreement". Two staged entries claiming one payment agreement is the
/// dangerous case, since paying the same bill twice looks like a fulfilled obligation, so a contested agreement is
/// handed to the operator whole rather than resolved here.
let pivotClaimsByPaymentAgreement
    (claims: (PaymentAgreementId * StageDataClassificationComponent.ClassificationResult) list)
    : StageDataClassificationComponent.PaymentAgreementClaimCluster list =
    claims
    |> List.groupBy fst
    |> List.map(fun (paymentAgreementId, pairs) ->
        let claimants = pairs |> List.map snd
        let cluster: StageDataClassificationComponent.PaymentAgreementClaimCluster =
            { paymentAgreementId = paymentAgreementId
              claimants = claimants
              containsUnwrittenTies = claimants |> List.exists isTiedClaimant }
        cluster)

let private fetchAgreementsWithDirection
    (context: Context.Context)
    (paymentAgreementIds: PaymentAgreementId list)
    : Result<Map<PaymentAgreementId, PaymentAgreement.PaymentAgreement * FlowDirection>, AppError> =
    result {
        if paymentAgreementIds |> List.isEmpty then return Map.empty else
        let! paymentAgreements = paymentAgreementIds |> PaymentAgreement.fetchByPaymentAgreementIdList context
        let masterAgreementIds =
            paymentAgreements |> List.map PaymentAgreement.masterAgreementID |> List.distinct
        let! masterAgreements = masterAgreementIds |> MasterAgreement.fetchByMasterAgreementIdList context
        let directionByMasterAgreementId =
            masterAgreements
            |> List.map (fun master -> (master |> MasterAgreement.agreementID), (master |> MasterAgreement.direction))
            |> Map.ofList
        return!
            paymentAgreements
            |> List.map (fun paymentAgreement ->
                let masterAgreementId = paymentAgreement |> PaymentAgreement.masterAgreementID
                match directionByMasterAgreementId |> Map.tryFind masterAgreementId with
                | Some direction ->
                    let paymentAgreementId = paymentAgreement |> PaymentAgreement.paymentAgreementId
                    Ok (paymentAgreementId, (paymentAgreement, direction))
                | None ->
                    let agreementUuid = masterAgreementId |> MasterAgreementId.value
                    Error (CashflowMasterAgreementIdDoesntExist agreementUuid))
            |> convertListOfResultsToResultsList
            |> Result.map Map.ofList
    }

/// selectLegsOfClaimedEntries collapses each (payment agreement, stage entry) claim down to the one line that carries
/// the obligation. A rule matches on description, amount and source, none of which separate an entry's two lines.
let private selectLegsOfClaimedEntries
    (agreementsById: Map<PaymentAgreementId, PaymentAgreement.PaymentAgreement * FlowDirection>)
    (rulesById: Map<StageDataClassificationComponent.ClassificationRuleId, ClassificationRule.ClassificationRule>)
    (linesById: Map<StageEntryComponent.StageEntryLineId, StageEntryLine.StageEntryLine>)
    (results: StageDataClassificationComponent.ClassificationResult list)
    : Result<
        (PaymentAgreementId * StageDataClassificationComponent.ClassificationResult) list *
        StageDataClassificationComponent.PaymentAgreementDecision list, AppError> =
    let expectedLineType (direction: FlowDirection) =
        match direction with
        | Income -> Model.Ledger.JournalEntryComponent.Credit
        | Outgo -> Model.Ledger.JournalEntryComponent.Debit
    let doesAnyClaimingRuleConstrainLineType
        (paymentAgreementId: PaymentAgreementId)
        (claims: (PaymentAgreementId * StageDataClassificationComponent.ClassificationResult) list)
        : Result<bool, AppError> =
        claims
        |> List.collect (fun (_, result) -> result |> matchesClaimingPaymentAgreement paymentAgreementId)
        |> List.map (fun prioritizedMatch ->
            match rulesById |> Map.tryFind prioritizedMatch.ruleId with
            | Some rule -> Ok (rule |> ClassificationRule.constrainsLineType)
            | None ->
                let ruleUuid = prioritizedMatch.ruleId |> StageDataClassificationComponent.ClassificationRuleId.value
                Error (IngestionClassificationRuleIdDoesntExist ruleUuid))
        |> convertListOfResultsToResultsList
        |> Result.map (List.exists id)
    result {
        let! selectionsAndDecisions =
            results
            |> List.collect (fun result ->
                result |> paymentAgreementsClaimedBy |> List.map (fun paymentAgreementId -> paymentAgreementId, result))
            |> List.groupBy (fun (paymentAgreementId, result) ->
                paymentAgreementId, result.candidate.headerIdOfCandidate)
            |> List.map (fun ((paymentAgreementId, _), claims) -> result {
                let! ruleChoseTheLeg = claims |> doesAnyClaimingRuleConstrainLineType paymentAgreementId
                let! survivors =
                    // the rule's author knew something the direction default doesn't -- an Outgo agreement taking a
                    // refund matches a Credit line, which the default would throw away
                    if ruleChoseTheLeg then Ok claims else
                    match agreementsById |> Map.tryFind paymentAgreementId with
                    | None ->
                        let agreementUuid = paymentAgreementId |> PaymentAgreementId.value
                        Error (CashflowPaymentAgreementIdDoesntExist agreementUuid)
                    | Some (paymentAgreement, direction) ->
                        let accountId = paymentAgreement |> PaymentAgreement.accountIdForFlowDirection direction
                        let lineType = expectedLineType direction
                        claims
                        |> List.filter (fun (_, result) ->
                            let lineAccountId =
                                linesById
                                |> Map.tryFind result.candidate.lineIdOfCandidate
                                |> Option.bind StageEntryLine.accountId
                            lineAccountId = Some accountId && result.candidate.lineType = lineType)
                        |> Ok
                match survivors with
                | [ single ] -> return [ single ], []
                | [] ->
                    let outcome = StageDataClassificationComponent.NoLineOnAgreementAccounts
                    return [], claims |> List.map (snd >> decisionFor paymentAgreementId outcome)
                | many ->
                    let outcome = StageDataClassificationComponent.ManyLinesOnAgreementAccount
                    return [], many |> List.map (snd >> decisionFor paymentAgreementId outcome) })
            |> convertListOfResultsToResultsList
        let selections = selectionsAndDecisions |> List.collect fst
        let decisions = selectionsAndDecisions |> List.collect snd
        return selections, decisions
    }

let private writeLinkagesForClaimClusters
    (context: Context.Context)
    (clusters: StageDataClassificationComponent.PaymentAgreementClaimCluster list)
    : Result<StageDataClassificationComponent.PaymentAgreementDecision list, AppError> =
    result {
        let! decisionsByCluster =
            clusters
            |> List.map (fun cluster -> result {
                let paymentAgreementId = cluster.paymentAgreementId
                match cluster.claimants with
                | [ claimant ] when cluster.containsUnwrittenTies |> not ->
                    let lineId = claimant.candidate.lineIdOfCandidate
                    let now = context |> Context.getInitiationInstant
                    let linkId = PaymentAgreementLinkId.create ()
                    let link = PaymentAgreementLink.create linkId paymentAgreementId lineId now now
                    do! link |> PaymentAgreementLink.persist context
                    return [ claimant |> decisionFor paymentAgreementId StageDataClassificationComponent.Linked ]
                // code is not allowed to break a tie, so a tied claimant contests its agreement however few rows
                // claimed it
                | claimants ->
                    return
                        claimants
                        |> List.map (fun claimant ->
                            let outcome =
                                if claimant |> isTiedClaimant then StageDataClassificationComponent.TiedClaimants
                                else StageDataClassificationComponent.ContestedAgreement
                            claimant |> decisionFor paymentAgreementId outcome) })
            |> convertListOfResultsToResultsList
        return decisionsByCluster |> List.concat
    }

let private matchInvoicesAndCreatePayments
    (context: Context.Context)
    (openInstances: InstanceOrchestration.InstanceComposite list)
    : Result<CashFlowComponent.InvoiceDecision list, AppError> =
    raise(NotImplementedException())

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
        let rosterLineIds =
            roster
            |> List.collect StageEntryOrchestration.seLines
            |> List.map StageEntryLine.stageEntryLineId
        let! existingLinks =
            if rosterLineIds |> List.isEmpty then Ok []
            else rosterLineIds |> PaymentAgreementLink.fetchByStageEntryLineIdList context
        let linkedLineIds =
            existingLinks |> List.map PaymentAgreementLink.stageEntryLineId |> Set.ofList
        // an entry whose lines all carry an account is still a candidate here. account assignment and obligation
        // linkage are independent questions about the same row
        let (matchCandidates: StageDataClassificationComponent.MatchCandidate list) =
            roster
            |> List.collect(fun entry ->
                let header = entry |> StageEntryOrchestration.stageEntryHeader
                entry
                |> StageEntryOrchestration.seLines
                |> List.filter (fun line ->
                    linkedLineIds |> Set.contains (line |> StageEntryLine.stageEntryLineId) |> not)
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
        let classificationResults = classificationRun.results
        let claimedAgreementIds =
            classificationResults |> List.collect paymentAgreementsClaimedBy |> List.distinct
        let! agreementsById = claimedAgreementIds |> fetchAgreementsWithDirection context
        let ruleFilter: FetchFilters.ClassificationRuleFilter = {
            ruleId = None
            nameLike = None
            accountAtMatch = None
            paymentAgreementAtMatch = None
            claimantType = Some StageDataClassificationComponent.PaymentAgreementClaimant
            sourceLike = None
            activeOnly = true }
        let! rules = ClassificationOrchestration.fetchRulesFiltered context ruleFilter None
        let rulesById =
            rules
            |> List.map (fun rule -> (rule |> ClassificationRule.classificationRuleId), rule)
            |> Map.ofList
        let linesById =
            roster
            |> List.collect StageEntryOrchestration.seLines
            |> List.map (fun line -> (line |> StageEntryLine.stageEntryLineId), line)
            |> Map.ofList
        let! selectedClaims, legDecisions =
            classificationResults |> selectLegsOfClaimedEntries agreementsById rulesById linesById
        let! linkageDecisions =
            selectedClaims |> pivotClaimsByPaymentAgreement |> writeLinkagesForClaimClusters context
        let! openInstancesToMatch = false |> InstanceOrchestration.fetchCompositesByIsFulfilled context
        let! invoiceDecisionLog = openInstancesToMatch |> matchInvoicesAndCreatePayments context
        // re-read rather than reuse: the invoice phase above creates payments against these instances
        let! openInstances = false |> InstanceOrchestration.fetchCompositesByIsFulfilled context
        let classificationResult: InstanceOrchestration.PaymentAgreementClassificationResult =
            { runId = classificationRun.runId
              classificationResults = classificationResults
              decisionLog = legDecisions @ linkageDecisions
              invoiceDecisionLog = invoiceDecisionLog
              openInstances = openInstances }
        return classificationResult
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
