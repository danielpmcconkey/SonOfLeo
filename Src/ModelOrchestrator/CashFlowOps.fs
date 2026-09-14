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

// how many days past an invoice's due date a payment may land and still be considered a match for it
let private gracePeriodInDaysFromCadenceType (cadenceType: Cadence.CadenceType) : int =
    match cadenceType with
    | Cadence.Daily -> 0
    | Cadence.Weekly _ -> 2
    | Cadence.EveryOtherWeek _ -> 4
    | Cadence.Monthly _ -> 7
    | Cadence.Annually _ -> 7

let private noChangeInvoiceUpdates (invoiceId: CashFlowComponent.InvoiceId) : Invoice.InvoiceFieldUpdates =
    { invoiceIdToUpdate = invoiceId
      externalInvoiceIdUpdate = FieldUpdate.NoChange
      invoiceDateUpdate = FieldUpdate.NoChange
      dueDateUpdate = FieldUpdate.NoChange
      amountUpdate = FieldUpdate.NoChange
      invoiceStateUpdate = FieldUpdate.NoChange
      paymentStateUpdate = FieldUpdate.NoChange
      postedStateUpdate = FieldUpdate.NoChange
      blockerUpdate = FieldUpdate.NoChange
      memoUpdate = FieldUpdate.NoChange }

let private createPaymentForInvoice
    (context: Context.Context)
    (instanceId: CashFlowComponent.InstanceId)
    (invoiceId: CashFlowComponent.InvoiceId)
    (lineId: StageEntryComponent.StageEntryLineId)
    (amount: CashFlowComponent.PaymentAmount)
    (entryDate: LocalDate)
    : Result<InstanceOrchestration.InstanceComposite, AppError> =
    let invoiceCompositeUpdate: InstanceOrchestration.InvoiceCompositeUpdate =
        { invoiceUpdates = invoiceId |> noChangeInvoiceUpdates
          paymentUpdates = []
          paymentIdsToDelete = []
          newPayments =
            [ CashFlowComponent.Staged lineId, amount, Some { localDate = entryDate }, None, None ] }
    let compositeUpdate: InstanceOrchestration.InstanceCompositeUpdate =
        { instanceUpdates =
            { instanceIdToUpdate = instanceId
              instanceDateUpdate = FieldUpdate.NoChange
              isFulfilledUpdate = FieldUpdate.NoChange }
          invoiceCompositeUpdates = [ invoiceCompositeUpdate ]
          newInvoices = [] }
    compositeUpdate |> InstanceOrchestration.updateInstanceComposite context

let private isOverpaid
    (invoiceId: CashFlowComponent.InvoiceId)
    (instanceComposite: InstanceOrchestration.InstanceComposite)
    : Result<bool, AppError> =
    result {
        let invoiceComposite =
            instanceComposite
            |> InstanceOrchestration.invoiceComposites
            |> List.find (fun invoiceComposite ->
                invoiceComposite |> InstanceOrchestration.invoice |> Invoice.invoiceId = invoiceId)
        let payments = invoiceComposite |> InstanceOrchestration.payments
        let! paidTotal = payments |> List.map Payment.amount |> List.map _.money |> Model.Money.sumList
        let invoiceAmount = invoiceComposite |> InstanceOrchestration.invoice |> Invoice.amount
        return paidTotal > invoiceAmount.money
    }

let private matchInvoicesAndCreatePayments
    (context: Context.Context)
    (openInstances: InstanceOrchestration.InstanceComposite list)
    : Result<CashFlowComponent.InvoiceDecision list, AppError> =
    result {
        // a fully paid invoice has nothing left to match. its instance can still be open, waiting on a sibling leg
        let unpaidInvoices =
            openInstances
            |> List.collect (fun instanceComposite ->
                let instanceId = instanceComposite |> InstanceOrchestration.instance |> Instance.instanceId
                let masterAgreementId =
                    instanceComposite |> InstanceOrchestration.instance |> Instance.masterAgreementID
                instanceComposite
                |> InstanceOrchestration.invoiceComposites
                |> List.map (fun invoiceComposite ->
                    instanceId, masterAgreementId, (invoiceComposite |> InstanceOrchestration.invoice))
                |> List.filter (fun (_, _, invoice) ->
                    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
                    lifeCycleState.paymentState <> CashFlowComponent.FullyPaid))
            // the oldest bill gets first claim on a line two invoices could both take, and fetch order never decides it
            |> List.sortBy (fun (_, _, invoice) ->
                (invoice |> Invoice.dueDate).localDate,
                (invoice |> Invoice.invoiceId |> CashFlowComponent.InvoiceId.value))
        if unpaidInvoices |> List.isEmpty then return [] else
        let agreementIds =
            unpaidInvoices |> List.map (fun (_, _, invoice) -> invoice |> Invoice.paymentAgreementId) |> List.distinct
        let! links = agreementIds |> PaymentAgreementLink.fetchByPaymentAgreementIdList context
        if links |> List.isEmpty then return [] else
        let linkedLineIds = links |> List.map PaymentAgreementLink.stageEntryLineId |> List.distinct
        let! linkedLines = linkedLineIds |> StageEntryLine.fetchByIdList context
        let headerIds = linkedLines |> List.map StageEntryLine.stageEntryHeaderId |> List.distinct
        let! headers = headerIds |> StageEntryHeader.fetchByIdList context
        let entryDateByHeaderId =
            headers
            |> List.map (fun header ->
                (header |> StageEntryHeader.stageEntryHeaderId), (header |> StageEntryHeader.entryDate))
            |> Map.ofList
        let lineById =
            linkedLines |> List.map (fun line -> (line |> StageEntryLine.stageEntryLineId), line) |> Map.ofList
        let! paymentsOnLinkedLines = linkedLineIds |> Payment.fetchByStageEntryLineIdList context
        // a payment that has reached the ledger no longer names the staged line it came from, but its invoice is
        // FullyPaid by then and was filtered out above, so its line cannot be a candidate here either way
        let paidLineIds =
            paymentsOnLinkedLines
            |> List.choose (fun payment ->
                match payment |> Payment.transactionPointer with
                | CashFlowComponent.Staged lineId -> Some lineId
                | CashFlowComponent.Posted _ -> None)
            |> Set.ofList
        let masterAgreementIds = unpaidInvoices |> List.map (fun (_, maId, _) -> maId) |> List.distinct
        let! masterAgreements = masterAgreementIds |> MasterAgreement.fetchByMasterAgreementIdList context
        let cadenceTypeByAgreementId =
            masterAgreements
            |> List.map (fun masterAgreement ->
                (masterAgreement |> MasterAgreement.agreementID),
                (masterAgreement |> MasterAgreement.cadence |> Cadence.cadenceType))
            |> Map.ofList
        let linesByAgreementId =
            links
            |> List.groupBy PaymentAgreementLink.paymentAgreementId
            |> List.map (fun (agreementId, agreementLinks) ->
                agreementId, (agreementLinks |> List.map PaymentAgreementLink.stageEntryLineId))
            |> Map.ofList
        let candidatesForInvoice
            (masterAgreementId: CashFlowComponent.MasterAgreementId)
            (invoice: Invoice.Invoice)
            (claimedLineIds: Set<StageEntryComponent.StageEntryLineId>)
            : StageEntryComponent.StageEntryLineId list =
            let graceInDays =
                match cadenceTypeByAgreementId |> Map.tryFind masterAgreementId with
                | Some cadenceType -> cadenceType |> gracePeriodInDaysFromCadenceType
                | None -> 0
            let windowStart = (invoice |> Invoice.invoiceDate).localDate.PlusDays(-graceInDays)
            let windowEnd = (invoice |> Invoice.dueDate).localDate.PlusDays(graceInDays)
            match linesByAgreementId |> Map.tryFind (invoice |> Invoice.paymentAgreementId) with
            | None -> []
            | Some agreementLineIds ->
                agreementLineIds
                |> List.filter (fun lineId ->
                    if paidLineIds |> Set.contains lineId || claimedLineIds |> Set.contains lineId then false else
                    match lineById |> Map.tryFind lineId with
                    | None -> false
                    | Some line ->
                        match entryDateByHeaderId |> Map.tryFind (line |> StageEntryLine.stageEntryHeaderId) with
                        | None -> false
                        | Some entryDate -> entryDate >= windowStart && entryDate <= windowEnd)
        let! decisions, claimedLineIds, consideredLineIds =
            unpaidInvoices
            |> List.fold
                (fun accumulator (instanceId, masterAgreementId, invoice) -> result {
                    let! decisionsSoFar, claimedSoFar, consideredSoFar = accumulator
                    let invoiceId = invoice |> Invoice.invoiceId
                    let candidates = candidatesForInvoice masterAgreementId invoice claimedSoFar
                    let consideredSoFar = Set.union consideredSoFar (candidates |> Set.ofList)
                    match candidates with
                    | [] -> return decisionsSoFar, claimedSoFar, consideredSoFar
                    | [ lineId ] ->
                        let line = lineById |> Map.find lineId
                        let entryDate = entryDateByHeaderId |> Map.find (line |> StageEntryLine.stageEntryHeaderId)
                        let amount: CashFlowComponent.PaymentAmount = { money = line |> StageEntryLine.amount }
                        let! updated =
                            entryDate |> createPaymentForInvoice context instanceId invoiceId lineId amount
                        let! overpaid = updated |> isOverpaid invoiceId
                        let created =
                            { invoiceId = invoiceId; outcome = CashFlowComponent.PaymentCreated lineId }
                        let overpayment =
                            if overpaid then [ { invoiceId = invoiceId; outcome = CashFlowComponent.Overpayment } ]
                            else []
                        return
                            decisionsSoFar @ [ created ] @ overpayment,
                            claimedSoFar |> Set.add lineId,
                            consideredSoFar
                    | manyLineIds ->
                        let contested =
                            { invoiceId = invoiceId
                              outcome = CashFlowComponent.ManyCandidateEntries manyLineIds }
                        return decisionsSoFar @ [ contested ], claimedSoFar, consideredSoFar })
                (Ok([], paidLineIds, Set.empty))
        // a line that was offered to an invoice and lost is the operator's problem, not a data gap -- only a line no
        // invoice would even consider means the Instance or Invoice it needs is missing
        let accountedForLineIds = Set.union claimedLineIds consideredLineIds
        do!
            links
            |> List.map (fun link ->
                let lineId = link |> PaymentAgreementLink.stageEntryLineId
                if accountedForLineIds |> Set.contains lineId then Ok () else
                let lineUuid = lineId |> StageEntryComponent.StageEntryLineId.value
                let agreementUuid =
                    link |> PaymentAgreementLink.paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                Error(CashflowPaymentAgreementLinkNoInvoiceToMatch(lineUuid, agreementUuid)))
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return decisions
    }

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

let constructNewPaymentAgreementLinkAndPersist
    (context: Context.Context)
    (paymentAgreementId: PaymentAgreementId)
    (stageEntryLineId: StageEntryComponent.StageEntryLineId)
    : Result<PaymentAgreementLink.PaymentAgreementLink, AppError> =
    result {
        let! _ =
            match stageEntryLineId |> StageEntryLine.fetchById context with
            | Ok line -> Ok line
            | Error(DalResultantRowsDidntMatchExpectation(_, 0)) ->
                let lineUuid = stageEntryLineId |> StageEntryComponent.StageEntryLineId.value
                Error(IngestionStageEntryLineIdDoesntExist lineUuid)
            | Error e -> Error e
        let! existingLinks = stageEntryLineId |> PaymentAgreementLink.fetchByStageEntryLineId context
        do!
            match existingLinks with
            | [] -> Ok ()
            | existingLink :: _ ->
                let lineUuid = stageEntryLineId |> StageEntryComponent.StageEntryLineId.value
                let agreementUuid =
                    existingLink |> PaymentAgreementLink.paymentAgreementId |> PaymentAgreementId.value
                Error(CashflowPaymentAgreementLinkLineAlreadyLinked(lineUuid, agreementUuid))
        let now = context |> Context.getInitiationInstant
        let linkId = PaymentAgreementLinkId.create ()
        let link = PaymentAgreementLink.create linkId paymentAgreementId stageEntryLineId now now
        do! link |> PaymentAgreementLink.persist context
        return link
    }

/// deletePaymentAndItsLinkage also removes the payment agreement linkage that produced the payment, which hands the
/// stage entry line back to classification as an unclaimed row. The linkage survives when another payment still points
/// at the same line.
let deletePaymentAndItsLinkage
    (context: Context.Context)
    (paymentId: PaymentId)
    : Result<InstanceOrchestration.InstanceComposite, AppError> =
    result {
        let! payment =
            match paymentId |> Payment.fetchById context with
            | Ok found -> Ok found
            | Error(DalResultantRowsDidntMatchExpectation(_, 0)) ->
                let paymentUuid = paymentId |> PaymentId.value
                Error(CashflowPaymentIdDoesntExist paymentUuid)
            | Error e -> Error e
        let invoiceId = payment |> Payment.invoiceId
        let! invoice = invoiceId |> Invoice.fetchById context
        let instanceId = invoice |> Invoice.instanceId
        let! stageEntryLineId = paymentId |> Payment.fetchStageEntryLineIdById context
        do!
            match stageEntryLineId with
            | None -> Ok ()
            | Some lineId ->
                result {
                    let! paymentsOnLine = [ lineId ] |> Payment.fetchByStageEntryLineIdList context
                    let otherPaymentsOnLine =
                        paymentsOnLine |> List.filter (fun other -> other |> Payment.paymentId <> paymentId)
                    if otherPaymentsOnLine |> List.isEmpty |> not then return () else
                    let! links = lineId |> PaymentAgreementLink.fetchByStageEntryLineId context
                    return!
                        links
                        |> List.map (fun link ->
                            link |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLink.delete context)
                        |> convertListOfResultsToResultsList
                        |> Result.map ignore
                }
        let invoiceCompositeUpdate: InstanceOrchestration.InvoiceCompositeUpdate =
            { invoiceUpdates = invoiceId |> noChangeInvoiceUpdates
              paymentUpdates = []
              paymentIdsToDelete = [ paymentId ]
              newPayments = [] }
        let compositeUpdate: InstanceOrchestration.InstanceCompositeUpdate =
            { instanceUpdates =
                { instanceIdToUpdate = instanceId
                  instanceDateUpdate = FieldUpdate.NoChange
                  isFulfilledUpdate = FieldUpdate.NoChange }
              invoiceCompositeUpdates = [ invoiceCompositeUpdate ]
              newInvoices = [] }
        return! compositeUpdate |> InstanceOrchestration.updateInstanceComposite context
    }

let projectCashFlowNDaysForward() =
    // Takes a horizon. Reads ledger balances + open invoices. Returns per-account `{ currentBalance, knownInflows,
    // knownOutflows, projectedLow }` + `billsToChase` (instances with no invoice).
    raise(NotImplementedException())

let private transitionOneInstancesPaymentsToPosted
    (context: Context.Context)
    (instanceId: InstanceId)
    (postings:
        (Payment.Payment * Model.Ledger.JournalEntryComponent.JournalEntryLineId * Invoice.Invoice) list)
    : Result<PaymentPostingTransition list, AppError> =
    result {
        let invoiceCompositeUpdates =
            postings
            |> List.groupBy (fun (_, _, invoice) -> invoice |> Invoice.invoiceId)
            |> List.map (fun (invoiceId, invoicePostings) ->
                let paymentUpdates =
                    invoicePostings
                    |> List.map (fun (payment, journalEntryLineId, _) ->
                        let paymentUpdate: Payment.PaymentFieldUpdates =
                            { paymentIdToUpdate = payment |> Payment.paymentId
                              journalEntryLineIdUpdate = FieldUpdate.SetTo(Some journalEntryLineId)
                              stageEntryLineIdUpdate = FieldUpdate.NoChange
                              postedToFiDateUpdate = FieldUpdate.NoChange
                              memoUpdate = FieldUpdate.NoChange }
                        paymentUpdate)
                let invoiceCompositeUpdate: InstanceOrchestration.InvoiceCompositeUpdate =
                    { invoiceUpdates = invoiceId |> noChangeInvoiceUpdates
                      paymentUpdates = paymentUpdates
                      paymentIdsToDelete = []
                      newPayments = [] }
                invoiceCompositeUpdate)
        let compositeUpdate: InstanceOrchestration.InstanceCompositeUpdate =
            { instanceUpdates =
                { instanceIdToUpdate = instanceId
                  instanceDateUpdate = FieldUpdate.NoChange
                  isFulfilledUpdate = FieldUpdate.NoChange }
              invoiceCompositeUpdates = invoiceCompositeUpdates
              newInvoices = [] }
        let! composite = compositeUpdate |> InstanceOrchestration.updateInstanceComposite context
        let agreementName = composite |> InstanceOrchestration.instance |> Instance.masterAgreementName
        return
            postings
            |> List.map (fun (payment, journalEntryLineId, invoice) ->
                { paymentId = payment |> Payment.paymentId
                  agreementName = agreementName
                  invoiceAmount = invoice |> Invoice.amount
                  journalEntryLineId = journalEntryLineId })
    }

let transitionPaymentsToPosted (context: Context.Context) : Result<PaymentPostingTransition list, AppError> =
    result {
        let! stagedPayments = Payment.fetchByStagedTransactionPointer context
        let paymentsAndLines =
            stagedPayments
            |> List.choose (fun payment ->
                match payment |> Payment.transactionPointer with
                | CashFlowComponent.Staged stageEntryLineId -> Some(payment, stageEntryLineId)
                | CashFlowComponent.Posted _ -> None)
        if paymentsAndLines |> List.isEmpty then return [] else
        let! stageEntryLines =
            paymentsAndLines
            |> List.map snd
            |> List.distinct
            |> StageEntryLine.fetchByIdList context
        let postedLines =
            stageEntryLines
            |> List.choose (fun line ->
                line
                |> StageEntryLine.journalEntryLineId
                |> Option.map (fun journalEntryLineId -> (line |> StageEntryLine.stageEntryLineId), journalEntryLineId))
        let paymentsAndJournalEntryLines =
            paymentsAndLines
            |> List.choose (fun (payment, stageEntryLineId) ->
                postedLines
                |> List.tryFind (fun (lineId, _) -> lineId = stageEntryLineId)
                |> Option.map (fun (_, journalEntryLineId) -> payment, journalEntryLineId))
        if paymentsAndJournalEntryLines |> List.isEmpty then return [] else
        let! invoices =
            paymentsAndJournalEntryLines
            |> List.map (fun (payment, _) -> payment |> Payment.invoiceId)
            |> List.distinct
            |> Invoice.fetchByIdList context
        let! postings =
            paymentsAndJournalEntryLines
            |> List.map (fun (payment, journalEntryLineId) ->
                let invoiceId = payment |> Payment.invoiceId
                match invoices |> List.tryFind (fun invoice -> invoice |> Invoice.invoiceId = invoiceId) with
                | Some invoice -> Ok(payment, journalEntryLineId, invoice)
                | None ->
                    let invoiceUuid = invoiceId |> InvoiceId.value
                    Error(CashflowInvoiceIdDoesntExist invoiceUuid))
            |> convertListOfResultsToResultsList
        let! transitions =
            postings
            |> List.groupBy (fun (_, _, invoice) -> invoice |> Invoice.instanceId)
            |> List.map (fun (instanceId, instancePostings) ->
                instancePostings |> transitionOneInstancesPaymentsToPosted context instanceId)
            |> convertListOfResultsToResultsList
        return
            transitions
            |> List.concat
            |> List.sortBy (fun transition ->
                (transition.agreementName |> AgreementName.value), (transition.paymentId |> PaymentId.value))
    }
