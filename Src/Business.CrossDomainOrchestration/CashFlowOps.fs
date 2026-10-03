module Business.CrossDomainOrchestration.CashFlowOps

open NodaTime
open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer
open App.DataAccessLayer.DalError
open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.CashFlow
open Business.FinancialServices.Classification

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
    (daysOut: CashFlowComponent.ProjectionHorizonInDays)
    (agreement: AgreementOrchestration.Agreement)
    : Result<unit, IAppError> =
    result {
        let today = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
        let horizonEnd = today.PlusDays(daysOut |> CashFlowComponent.ProjectionHorizonInDays.value)
        let master = agreement |> AgreementOrchestration.masterAgreement
        // no Instance after the agreement's end date, even inside the horizon (REQ-CF-7.16)
        let cutOffDate =
            match master |> MasterAgreement.activityPeriod |> ActivityPeriod.activeEnd with
            | Some endDate when endDate < horizonEnd -> endDate
            | _ -> horizonEnd
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
                        let invoiceDate = {CashFlowComponent.InvoiceDate.localDate = neededDate}
                        let daysPastInvDateForDueDate =
                            daysDueAfterInvoiceDate |> CashFlowComponent.DaysDueAfterInvoiceDate.value
                        let dueDate =
                            {CashFlowComponent.DueDate.localDate = neededDate.PlusDays(daysPastInvDateForDueDate)}
                        let amount = { CashFlowComponent.InvoiceAmount.money = expectedAmount }
                        let direction = master |> MasterAgreement.direction
                        let invoiceState =
                            if direction = CashFlowComponent.Income
                            then CashFlowComponent.InvoiceGenerated
                            else CashFlowComponent.InvoiceExpected
                        let invMemo = None
                        Some (paId, extInvoiceId, invoiceDate, dueDate, amount, invoiceState, None, invMemo, [])
                    | _ -> None
                    )
                InstanceOrchestration.constructNewAndPersist
                        context agreementId neededDate invoiceCompositeFieldsList)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
    }

let private spawnInstancesFromAgreements
    (context: Context.Context)
    (daysOut: CashFlowComponent.ProjectionHorizonInDays)
    (agreements: AgreementOrchestration.Agreement list)
    : Result<unit, IAppError> =
    agreements
    |> List.map (spawnInstancesFromAgreement context daysOut)
    |> convertListOfResultsToResultsList
    |> Result.map ignore


let createUpcomingInstances
    (context: Context.Context)
    (daysOut: CashFlowComponent.ProjectionHorizonInDays)
    : Result<InstanceOrchestration.InstanceComposite list, IAppError> =
    result {
        let! agreements = AgreementOrchestration.fetchAllActiveAgreements context
        do! agreements |> spawnInstancesFromAgreements context daysOut
        return! InstanceOrchestration.fetchOpenComposites context
    }

// a tie claims every agreement it tied across; a clear winner claims only the winner, since the losers lost
let private claimingMatches
    (result: ClassificationComponent.ClassificationResult)
    : ClassificationComponent.PrioritizedMatch list =
    match result.outcome with
    | ClassificationComponent.NoMatch -> []
    | ClassificationComponent.OneMatch prioritizedMatch -> [ prioritizedMatch ]
    | ClassificationComponent.ManyMatchesClearWinner (winner, _) -> [ winner ]
    | ClassificationComponent.ManyMatchesTied ties -> ties

let private paymentAgreementsClaimedBy
    (result: ClassificationComponent.ClassificationResult)
    : CashFlowComponent.PaymentAgreementId list =
    // equal-priority rules naming the same agreement claim it once
    result |> claimingMatches |> List.choose _.paymentAgreementId |> List.distinct

let private matchesClaimingPaymentAgreement
    (paymentAgreementId: CashFlowComponent.PaymentAgreementId)
    (result: ClassificationComponent.ClassificationResult)
    : ClassificationComponent.PrioritizedMatch list =
    result
    |> claimingMatches
    |> List.filter (fun prioritizedMatch -> prioritizedMatch.paymentAgreementId = Some paymentAgreementId)

// equal-priority rules that all name the same payment agreement agree with each other; only a tie across different
// claimants is one code may not break
let private isTiedClaimant (result: ClassificationComponent.ClassificationResult) : bool =
    match result.outcome with
    | ClassificationComponent.ManyMatchesTied ties ->
        ties |> List.map _.paymentAgreementId |> List.distinct |> List.length > 1
    | _ -> false

let private decisionFor
    (paymentAgreementId: CashFlowComponent.PaymentAgreementId)
    (outcome: ClassificationComponent.PaymentAgreementDecisionOutcome)
    (result: ClassificationComponent.ClassificationResult)
    : ClassificationComponent.PaymentAgreementDecision =
    let ruleIds =
        result
        |> matchesClaimingPaymentAgreement paymentAgreementId
        |> List.map (fun prioritizedMatch -> prioritizedMatch.ruleId)
    { stageEntryLineId = result.candidate.lineIdOfCandidate
      paymentAgreementId = Some paymentAgreementId
      paymentAgreementLinkId = None
      ruleIds = ruleIds
      outcome = outcome }

/// pivotClaimsByPaymentAgreement flips the classifier's row-focused answer -- "which rules did this row match" -- onto
/// the rule axis: "which rows claimed this payment agreement". Two staged entries claiming one payment agreement is the
/// dangerous case, since paying the same bill twice looks like a fulfilled obligation, so a contested agreement is
/// handed to the operator whole rather than resolved here.
let pivotClaimsByPaymentAgreement
    (claims: (CashFlowComponent.PaymentAgreementId * ClassificationComponent.ClassificationResult) list)
    : ClassificationComponent.PaymentAgreementClaimCluster list =
    claims
    |> List.groupBy fst
    |> List.map(fun (paymentAgreementId, pairs) ->
        let claimants = pairs |> List.map snd
        let cluster: ClassificationComponent.PaymentAgreementClaimCluster =
            { paymentAgreementId = paymentAgreementId
              claimants = claimants
              containsUnwrittenTies = claimants |> List.exists isTiedClaimant }
        cluster)

let private fetchAgreementsWithDirection
    (context: Context.Context)
    (paymentAgreementIds: CashFlowComponent.PaymentAgreementId list)
    : Result<Map<CashFlowComponent.PaymentAgreementId, PaymentAgreement.PaymentAgreement * CashFlowComponent.FlowDirection>, IAppError> =
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
                    let agreementUuid = masterAgreementId |> CashFlowComponent.MasterAgreementId.value
                    CashFlowError.error (CashFlowError.CashflowMasterAgreementIdDoesntExist agreementUuid))
            |> convertListOfResultsToResultsList
            |> Result.map Map.ofList
    }

/// selectLegsOfClaimedEntries collapses each (payment agreement, stage entry) claim down to the one line that carries
/// the obligation. The kept lines are the lines a claiming rule matched; the expected-account default only breaks a tie
/// among several of them, and never vetoes a line a rule singled out (REQ-CF-12.4).
let private selectLegsOfClaimedEntries
    (agreementsById: Map<CashFlowComponent.PaymentAgreementId, PaymentAgreement.PaymentAgreement * CashFlowComponent.FlowDirection>)
    (linesById: Map<StageEntryComponent.StageEntryLineId, StageEntryLine.StageEntryLine>)
    (results: ClassificationComponent.ClassificationResult list)
    : Result<
        (CashFlowComponent.PaymentAgreementId * ClassificationComponent.ClassificationResult) list *
        ClassificationComponent.PaymentAgreementDecision list, IAppError> =
    let expectedLineType (direction: CashFlowComponent.FlowDirection) =
        match direction with
        | CashFlowComponent.FlowDirection.Income -> JournalEntryComponent.Credit
        | CashFlowComponent.FlowDirection.Outgo -> JournalEntryComponent.Debit
    result {
        let! selectionsAndDecisions =
            results
            |> List.collect (fun result ->
                result |> paymentAgreementsClaimedBy |> List.map (fun paymentAgreementId -> paymentAgreementId, result))
            |> List.groupBy (fun (paymentAgreementId, result) ->
                paymentAgreementId, result.candidate.headerIdOfCandidate)
            |> List.map (fun ((paymentAgreementId, _), claims) -> result {
                let! survivors =
                    match claims with
                    | [ _ ] -> Ok claims
                    | _ ->
                        match agreementsById |> Map.tryFind paymentAgreementId with
                        | None ->
                            let agreementUuid = paymentAgreementId |> CashFlowComponent.PaymentAgreementId.value
                            CashFlowError.error (CashFlowError.CashflowPaymentAgreementIdDoesntExist agreementUuid)
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
                    let outcome = ClassificationComponent.NoLineOnAgreementAccounts
                    return [], claims |> List.map (snd >> decisionFor paymentAgreementId outcome)
                | many ->
                    let outcome = ClassificationComponent.ManyLinesOnAgreementAccount
                    return [], many |> List.map (snd >> decisionFor paymentAgreementId outcome) })
            |> convertListOfResultsToResultsList
        let selections = selectionsAndDecisions |> List.collect fst
        let decisions = selectionsAndDecisions |> List.collect snd
        return selections, decisions
    }

let private writeLinkagesForClaimClusters
    (context: Context.Context)
    (clusters: ClassificationComponent.PaymentAgreementClaimCluster list)
    : Result<PaymentAgreementLink.PaymentAgreementLink list * ClassificationComponent.PaymentAgreementDecision list, IAppError> =
    result {
        let! linksAndDecisionsByCluster =
            clusters
            |> List.map (fun cluster -> result {
                let paymentAgreementId = cluster.paymentAgreementId
                match cluster.claimants with
                | [ claimant ] when cluster.containsUnwrittenTies |> not ->
                    let lineId = claimant.candidate.lineIdOfCandidate
                    let now = context |> Context.getInitiationInstant
                    let linkId = CashFlowComponent.PaymentAgreementLinkId.create ()
                    let link = PaymentAgreementLink.create linkId paymentAgreementId lineId now now
                    do! link |> PaymentAgreementLink.persist context
                    let decision = claimant |> decisionFor paymentAgreementId ClassificationComponent.Linked
                    return [ link ], [ { decision with paymentAgreementLinkId = Some linkId } ]
                // code is not allowed to break a tie, so a tied claimant contests its agreement however few rows
                // claimed it
                | claimants ->
                    return
                        [],
                        claimants
                        |> List.map (fun claimant ->
                            let outcome =
                                if claimant |> isTiedClaimant then ClassificationComponent.TiedClaimants
                                else ClassificationComponent.ContestedAgreement
                            claimant |> decisionFor paymentAgreementId outcome) })
            |> convertListOfResultsToResultsList
        return
            linksAndDecisionsByCluster |> List.collect fst,
            linksAndDecisionsByCluster |> List.collect snd
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
    (entryDate: LocalDate)
    (clearBlocker: bool)
    : Result<InstanceOrchestration.InstanceComposite, IAppError> =
    let invoiceUpdates =
        if clearBlocker then { (invoiceId |> noChangeInvoiceUpdates) with blockerUpdate = FieldUpdate.SetTo None }
        else invoiceId |> noChangeInvoiceUpdates
    let invoiceCompositeUpdate: InstanceOrchestration.InvoiceCompositeUpdate =
        { invoiceUpdates = invoiceUpdates
          paymentUpdates = []
          paymentIdsToDelete = []
          newPayments =
            [ CashFlowComponent.Staged lineId, Some { localDate = entryDate }, None, None ] }
    let compositeUpdate: InstanceOrchestration.InstanceCompositeUpdate =
        { instanceUpdates =
            { instanceIdToUpdate = instanceId
              isFulfilledUpdate = FieldUpdate.NoChange }
          invoiceCompositeUpdates = [ invoiceCompositeUpdate ]
          newInvoices = [] }
    compositeUpdate |> InstanceOrchestration.updateInstanceComposite context

let private isOverpaid
    (invoiceId: CashFlowComponent.InvoiceId)
    (instanceComposite: InstanceOrchestration.InstanceComposite)
    : Result<bool, IAppError> =
    result {
        let invoiceComposite =
            instanceComposite
            |> InstanceOrchestration.invoiceComposites
            |> List.find (fun invoiceComposite ->
                invoiceComposite |> InstanceOrchestration.invoice |> Invoice.invoiceId = invoiceId)
        let payments = invoiceComposite |> InstanceOrchestration.payments
        let! paidTotal = payments |> List.map Payment.amount |> List.map _.money |> Money.sumList
        let invoiceAmount = invoiceComposite |> InstanceOrchestration.invoice |> Invoice.amount
        return Money.isGreaterThan paidTotal invoiceAmount.money
    }

let private matchInvoicesAndCreatePayments
    (context: Context.Context)
    (openInstances: InstanceOrchestration.InstanceComposite list)
    : Result<CashFlowComponent.InvoiceDecision list, IAppError> =
    result {
        // a fully paid invoice has nothing left to match. its instance can still be open, waiting on a sibling leg. a
        // cancelled invoice is never matched
        let! openInvoices =
            openInstances
            |> List.collect (fun instanceComposite ->
                let instanceId = instanceComposite |> InstanceOrchestration.instance |> Instance.instanceId
                let masterAgreementId =
                    instanceComposite |> InstanceOrchestration.instance |> Instance.masterAgreementID
                instanceComposite
                |> InstanceOrchestration.invoiceComposites
                |> List.filter (fun invoiceComposite ->
                    let invoice = invoiceComposite |> InstanceOrchestration.invoice
                    let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
                    lifeCycleState.paymentState <> CashFlowComponent.FullyPaid && invoice |> Invoice.isCancelled |> not)
                |> List.map (fun invoiceComposite -> result {
                    let invoice = invoiceComposite |> InstanceOrchestration.invoice
                    let! overpaid = instanceComposite |> isOverpaid (invoice |> Invoice.invoiceId)
                    let! paidSoFar =
                        invoiceComposite |> InstanceOrchestration.payments |> List.map (Payment.amount >> _.money)
                        |> Money.sumList
                    return instanceId, masterAgreementId, invoice, paidSoFar, overpaid }))
            |> convertListOfResultsToResultsList
        // an overpaid invoice derives PartiallyPaid, so the state alone doesn't keep it from absorbing more payments.
        // it is kept aside only to explain the orphans it would otherwise have taken
        let unpaidInvoices =
            openInvoices
            |> List.filter (fun (_, _, _, _, overpaid) -> not overpaid)
            |> List.map (fun (instanceId, masterAgreementId, invoice, paidSoFar, _) ->
                instanceId, masterAgreementId, invoice, paidSoFar)
            // the oldest bill gets first claim on a line two invoices could both take, and fetch order never decides
            // it; a due-date tie goes to the Invoice entered first
            |> List.sortBy (fun (_, _, invoice, _) ->
                (invoice |> Invoice.dueDate).localDate, (invoice |> Invoice.createdAt))
        let overpaidInvoices =
            openInvoices
            |> List.filter (fun (_, _, _, _, overpaid) -> overpaid)
            |> List.map (fun (_, masterAgreementId, invoice, _, _) -> masterAgreementId, invoice)
        // every link, not only those on agreements with an open Invoice: a line whose agreement has no open Invoice at
        // all, or no Instances yet, is an orphan too (REQ-CF-13.7)
        let! links = PaymentAgreementLink.fetchAll context
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
        // links outlive posting, so a line paid last week is still linked this week. any Payment that references
        // the line, Staged or Posted, takes it out of matching for good
        let! paidLineIds = linkedLineIds |> Payment.fetchReferencedStageEntryLineIds context
        // an entry dedup or the operator has set aside moved no cash of its own
        let setAsideHeaderIds =
            headers
            |> List.filter (fun header ->
                match header |> StageEntryHeader.currentStatus with
                | Some StageEntryComponent.Duplicate
                | Some StageEntryComponent.Ignored -> true
                | _ -> false)
            |> List.map StageEntryHeader.stageEntryHeaderId
            |> Set.ofList
        let setAsideLineIds =
            linkedLines
            |> List.filter (fun line -> setAsideHeaderIds |> Set.contains (line |> StageEntryLine.stageEntryHeaderId))
            |> List.map StageEntryLine.stageEntryLineId
        // an ineligible line is neither offered to an invoice nor counted as an orphan
        let ineligibleLineIds = paidLineIds @ setAsideLineIds |> Set.ofList
        let masterAgreementIds = openInvoices |> List.map (fun (_, maId, _, _, _) -> maId) |> List.distinct
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
        let entryDateOfLine lineId =
            lineById
            |> Map.tryFind lineId
            |> Option.bind (fun line -> entryDateByHeaderId |> Map.tryFind (line |> StageEntryLine.stageEntryHeaderId))
        let windowCovers (masterAgreementId: CashFlowComponent.MasterAgreementId) (invoice: Invoice.Invoice) lineId =
            let graceInDays =
                match cadenceTypeByAgreementId |> Map.tryFind masterAgreementId with
                | Some cadenceType -> cadenceType |> gracePeriodInDaysFromCadenceType
                | None -> 0
            let windowStart = (invoice |> Invoice.invoiceDate).localDate.PlusDays(-graceInDays)
            let windowEnd = (invoice |> Invoice.dueDate).localDate.PlusDays(graceInDays)
            match lineId |> entryDateOfLine with
            | None -> false
            | Some entryDate -> entryDate >= windowStart && entryDate <= windowEnd
        let candidatesForInvoice
            (masterAgreementId: CashFlowComponent.MasterAgreementId)
            (invoice: Invoice.Invoice)
            (claimedLineIds: Set<StageEntryComponent.StageEntryLineId>)
            : StageEntryComponent.StageEntryLineId list =
            match linesByAgreementId |> Map.tryFind (invoice |> Invoice.paymentAgreementId) with
            | None -> []
            | Some agreementLineIds ->
                agreementLineIds
                |> List.filter (fun lineId ->
                    if ineligibleLineIds |> Set.contains lineId || claimedLineIds |> Set.contains lineId then false
                    else windowCovers masterAgreementId invoice lineId)
        let! decisions, claimedLineIds, consideredLineIds =
            unpaidInvoices
            |> List.fold
                (fun accumulator (instanceId, masterAgreementId, invoice, paidSoFar) -> result {
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
                        // cash arriving resolves whatever blocked the bill, so a Payment that brings a blocked Invoice
                        // to FullyPaid clears the blocker and says so
                        let! paidAfter = Money.add paidSoFar amount.money
                        let blockerToClear =
                            match (invoice |> Invoice.invoiceLifeCycleState).blocker with
                            | Some blocker when Money.isEqual paidAfter (invoice |> Invoice.amount).money -> Some blocker
                            | Some _
                            | None -> None
                        let! updated =
                            createPaymentForInvoice
                                context instanceId invoiceId lineId entryDate (blockerToClear |> Option.isSome)
                        let! overpaid = updated |> isOverpaid invoiceId
                        let created =
                            { CashFlowComponent.invoiceId = invoiceId
                              CashFlowComponent.outcome = CashFlowComponent.PaymentCreated lineId }
                        let cleared =
                            blockerToClear
                            |> Option.map (fun blocker ->
                                { CashFlowComponent.invoiceId = invoiceId
                                  CashFlowComponent.outcome = CashFlowComponent.BlockerCleared blocker })
                            |> Option.toList
                        let overpayment =
                            if overpaid then [ { CashFlowComponent.invoiceId = invoiceId
                                                 CashFlowComponent.outcome = CashFlowComponent.Overpayment } ]
                            else []
                        return
                            decisionsSoFar @ [ created ] @ cleared @ overpayment,
                            claimedSoFar |> Set.add lineId,
                            consideredSoFar
                    | manyLineIds ->
                        let contested =
                            { CashFlowComponent.invoiceId = invoiceId
                              CashFlowComponent.outcome = CashFlowComponent.ManyCandidateEntries manyLineIds }
                        return decisionsSoFar @ [ contested ], claimedSoFar, consideredSoFar })
                (Ok([], ineligibleLineIds, Set.empty))
        // a line that was offered to an invoice and lost is the operator's problem, not a data gap -- only a line no
        // invoice would even consider means the Instance or Invoice it needs is missing
        let accountedForLineIds = Set.union claimedLineIds consideredLineIds
        // every orphan is named at once, so the operator fixes them in one pass rather than one run per orphan
        let orphans =
            links
            |> List.filter (fun link -> accountedForLineIds |> Set.contains (link |> PaymentAgreementLink.stageEntryLineId) |> not)
            |> List.map (fun link ->
                let lineId = link |> PaymentAgreementLink.stageEntryLineId
                let agreementId = link |> PaymentAgreementLink.paymentAgreementId
                let coveredByOverpaid =
                    overpaidInvoices
                    |> List.exists (fun (masterAgreementId, invoice) ->
                        (invoice |> Invoice.paymentAgreementId) = agreementId
                        && windowCovers masterAgreementId invoice lineId)
                let reason =
                    if coveredByOverpaid then CashFlowError.CoveringInvoicesOverpaid
                    else CashFlowError.NoOpenInvoiceCoversDate
                (lineId |> StageEntryComponent.StageEntryLineId.value),
                (agreementId |> CashFlowComponent.PaymentAgreementId.value),
                reason)
        do!
            if orphans |> List.isEmpty then Ok ()
            else CashFlowError.error(CashFlowError.CashflowPaymentAgreementLinksOrphaned orphans)
        return decisions
    }

/// applyPaymentAgreementClassification turns a payment agreement classification run over the roster's lines into
/// linkages, then matches open Invoices to linked lines. It does not update a stage entry's status. That belongs to
/// the data ingestion domain.
let applyPaymentAgreementClassification
    (context: Context.Context)
    (roster: StageEntryOrchestration.StageEntry list)
    (classificationRun: ClassificationComponent.ClassificationRun)
    : Result<InstanceOrchestration.PaymentAgreementClassificationResult, IAppError> =
    result {
        let classificationResults = classificationRun.results
        let claimedAgreementIds =
            classificationResults |> List.collect paymentAgreementsClaimedBy |> List.distinct
        let! agreementsById = claimedAgreementIds |> fetchAgreementsWithDirection context
        let linesById =
            roster
            |> List.collect StageEntryOrchestration.seLines
            |> List.map (fun line -> (line |> StageEntryLine.stageEntryLineId), line)
            |> Map.ofList
        let! selectedClaims, legDecisions =
            classificationResults |> selectLegsOfClaimedEntries agreementsById linesById
        let! linksCreated, linkageDecisions =
            selectedClaims |> pivotClaimsByPaymentAgreement |> writeLinkagesForClaimClusters context
        let! openInstancesToMatch = InstanceOrchestration.fetchOpenComposites context
        let! invoiceDecisionLog = openInstancesToMatch |> matchInvoicesAndCreatePayments context
        // re-read rather than reuse: the invoice phase above creates payments against these instances
        let! openInstances = InstanceOrchestration.fetchOpenComposites context
        let classificationResult: InstanceOrchestration.PaymentAgreementClassificationResult =
            { runId = classificationRun.runId
              classificationResults = classificationResults
              linksCreated = linksCreated
              decisionLog = legDecisions @ linkageDecisions
              invoiceDecisionLog = invoiceDecisionLog
              openInstances = openInstances }
        return classificationResult
    }

let constructNewAndPersist
    (context: Context.Context)
    (paymentAgreementId: CashFlowComponent.PaymentAgreementId)
    (stageEntryLineId: StageEntryComponent.StageEntryLineId)
    : Result<PaymentAgreementLink.PaymentAgreementLink, IAppError> =
    result {
        let! _ =
            let lineUuid = stageEntryLineId |> StageEntryComponent.StageEntryLineId.value
            stageEntryLineId |> StageEntryLine.fetchById context
            |> whenNoRows (DataIngestionError.IngestionStageEntryLineIdDoesntExist lineUuid)
        let! existingLinks = stageEntryLineId |> PaymentAgreementLink.fetchByStageEntryLineId context
        do!
            match existingLinks with
            | [] -> Ok ()
            | existingLink :: _ ->
                result {
                    let lineUuid = stageEntryLineId |> StageEntryComponent.StageEntryLineId.value
                    let linkUuid =
                        existingLink
                        |> PaymentAgreementLink.paymentAgreementLinkId
                        |> CashFlowComponent.PaymentAgreementLinkId.value
                    let! linkedAgreement =
                        existingLink |> PaymentAgreementLink.paymentAgreementId |> PaymentAgreement.fetchById context
                    let agreementName =
                        linkedAgreement
                        |> PaymentAgreement.paymentAgreementName
                        |> CashFlowComponent.PaymentAgreementName.value
                    return!
                        CashFlowError.error (
                            CashFlowError.CashflowPaymentAgreementLinkLineAlreadyLinked(lineUuid, linkUuid, agreementName))
                }
        let now = context |> Context.getInitiationInstant
        let linkId = CashFlowComponent.PaymentAgreementLinkId.create ()
        let link = PaymentAgreementLink.create linkId paymentAgreementId stageEntryLineId now now
        do! link |> PaymentAgreementLink.persist context
        return link
    }

/// confirmLinkLineHasNoPayments rejects re-pointing or deleting a link whose staged line a Payment references: the
/// Payment would go on paying an obligation its link no longer names. Deleting the Payment removes the link instead.
let private confirmLinkLineHasNoPayments
    (context: Context.Context)
    (link: PaymentAgreementLink.PaymentAgreementLink)
    : Result<unit, IAppError> =
    result {
        let! payments = [ link |> PaymentAgreementLink.stageEntryLineId ] |> Payment.fetchByStageEntryLineIdList context
        if payments |> List.isEmpty then return () else
        let linkUuid = link |> PaymentAgreementLink.paymentAgreementLinkId |> CashFlowComponent.PaymentAgreementLinkId.value
        let paymentUuids = payments |> List.map (Payment.paymentId >> CashFlowComponent.PaymentId.value)
        return! CashFlowError.error (CashFlowError.CashflowPaymentAgreementLinkLineHasPayments(linkUuid, paymentUuids))
    }

let private fetchLink (context: Context.Context) (linkId: CashFlowComponent.PaymentAgreementLinkId) =
    let linkUuid = linkId |> CashFlowComponent.PaymentAgreementLinkId.value
    linkId |> PaymentAgreementLink.fetchById context
    |> whenNoRows (CashFlowError.CashflowPaymentAgreementLinkIdDoesntExist linkUuid)

/// updatePaymentAgreementLink re-points a link to a different Payment Agreement, unless a Payment references its line.
let updatePaymentAgreementLink
    (context: Context.Context)
    (fieldUpdates: PaymentAgreementLink.PaymentAgreementLinkFieldUpdates)
    : Result<PaymentAgreementLink.PaymentAgreementLink, IAppError> =
    result {
        let! link = fieldUpdates.linkIdToUpdate |> fetchLink context
        do! link |> confirmLinkLineHasNoPayments context
        return! fieldUpdates |> PaymentAgreementLink.update context
    }

/// deletePaymentAgreementLink removes a link, unless a Payment references its line. It returns the removed link.
let deletePaymentAgreementLink
    (context: Context.Context)
    (linkId: CashFlowComponent.PaymentAgreementLinkId)
    : Result<PaymentAgreementLink.PaymentAgreementLink, IAppError> =
    result {
        let! link = linkId |> fetchLink context
        do! link |> confirmLinkLineHasNoPayments context
        do! linkId |> PaymentAgreementLink.delete context
        return link
    }

/// deletePaymentAndItsLinkage also removes the payment agreement linkage that produced the payment, which hands the
/// stage entry line back to classification as an unclaimed row. The linkage survives when another payment still points
/// at the same line.
let deletePaymentAndItsLinkage
    (context: Context.Context)
    (paymentId: CashFlowComponent.PaymentId)
    : Result<InstanceOrchestration.InstanceComposite, IAppError> =
    result {
        let! payment =
            let paymentUuid = paymentId |> CashFlowComponent.PaymentId.value
            paymentId |> Payment.fetchById context
            |> whenNoRows (CashFlowError.CashflowPaymentIdDoesntExist paymentUuid)
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
                  isFulfilledUpdate = FieldUpdate.NoChange }
              invoiceCompositeUpdates = [ invoiceCompositeUpdate ]
              newInvoices = [] }
        return! compositeUpdate |> InstanceOrchestration.updateInstanceComposite context
    }

let projectCashFlowNDaysForward
    (context: Context.Context)
    (daysOut: CashFlowComponent.ProjectionHorizonInDays)
    : Result<CashFlowComponent.CashFlowProjection, IAppError> =
    result {
        let runDate = context |> Context.getInitiationInstant |> Calendar.dateFromInstant
        let horizonEnd = runDate.PlusDays(daysOut |> CashFlowComponent.ProjectionHorizonInDays.value)
        let! allAccounts = Account.fetchAll context true
        let cashAccounts =
            allAccounts
            |> List.filter (fun account ->
                account |> Account.accountSubType = Some AccountComponent.Cash)
        let cashAccountIds = cashAccounts |> List.map Account.accountId
        let! balances =
            if cashAccountIds |> List.isEmpty then Ok []
            else AccountBalance.fetchByAccountIdList context (Some cashAccountIds) (Some runDate)
        let balanceByAccountId =
            balances
            |> List.map (fun (balance: AccountBalance.AccountBalance) -> balance.accountId, balance.netBalance)
            |> Map.ofList
        let! openInstances = InstanceOrchestration.fetchOpenComposites context
        let masterAgreementIds =
            openInstances
            |> List.map (fun composite -> composite |> InstanceOrchestration.instance |> Instance.masterAgreementID)
            |> List.distinct
        let! masterAgreements =
            if masterAgreementIds |> List.isEmpty then Ok []
            else masterAgreementIds |> MasterAgreement.fetchByMasterAgreementIdList context
        let masterAgreementById =
            masterAgreements
            |> List.map (fun master -> (master |> MasterAgreement.agreementID), master)
            |> Map.ofList
        let invoicesWithAgreementId =
            openInstances
            |> List.collect (fun composite ->
                let masterAgreementId = composite |> InstanceOrchestration.instance |> Instance.masterAgreementID
                composite
                |> InstanceOrchestration.invoiceComposites
                |> List.map (fun invoiceComposite ->
                    masterAgreementId,
                    (invoiceComposite |> InstanceOrchestration.invoice),
                    (invoiceComposite |> InstanceOrchestration.payments)))
        let! paymentAgreements =
            if masterAgreementIds |> List.isEmpty then Ok []
            else masterAgreementIds |> PaymentAgreement.fetchByMasterAgreementIdList context
        let paymentAgreementById =
            paymentAgreements
            |> List.map (fun agreement -> (agreement |> PaymentAgreement.paymentAgreementId), agreement)
            |> Map.ofList
        let paymentAgreementsByMasterId =
            paymentAgreements |> List.groupBy PaymentAgreement.masterAgreementID |> Map.ofList
        let cashAccountIdSet = cashAccountIds |> Set.ofList
        // an already-overdue bill is the most urgent money to move, so the window has no lower bound
        let! invoicesWithOutstanding =
            invoicesWithAgreementId
            |> List.filter (fun (_, invoice, _) ->
                let lifeCycleState = invoice |> Invoice.invoiceLifeCycleState
                lifeCycleState.paymentState <> CashFlowComponent.FullyPaid
                && invoice |> Invoice.isCancelled |> not
                && (invoice |> Invoice.dueDate).localDate <= horizonEnd)
            |> List.map (fun (masterAgreementId, invoice, payments) -> result {
                // what is still owed, not what was billed: a part-paid bill must not be counted twice. an overpaid
                // bill owes nothing
                let! paid = payments |> List.map (fun payment -> (payment |> Payment.amount).money) |> Money.sumList
                let! owed = Money.subtractVal1FromVal2 paid (invoice |> Invoice.amount).money
                return masterAgreementId, invoice, owed |> Money.floorAtZero })
            |> convertListOfResultsToResultsList
        let projectedInvoicesByAccountId =
            invoicesWithOutstanding
            |> List.choose (fun (masterAgreementId, invoice, outstanding) ->
                let master = masterAgreementById |> Map.find masterAgreementId
                let paymentAgreement = paymentAgreementById |> Map.find (invoice |> Invoice.paymentAgreementId)
                let direction = master |> MasterAgreement.direction
                let accountId = paymentAgreement |> PaymentAgreement.cashAccountIdForFlowDirection direction
                if cashAccountIdSet |> Set.contains accountId |> not then None else
                let projected: CashFlowComponent.ProjectedInvoice =
                    { invoiceId = invoice |> Invoice.invoiceId
                      agreementName = master |> MasterAgreement.agreementName
                      direction = direction
                      dueDate = invoice |> Invoice.dueDate
                      amount = invoice |> Invoice.amount
                      outstanding = outstanding }
                Some(accountId, projected))
            |> List.groupBy fst
            |> List.map (fun (accountId, pairs) -> accountId, (pairs |> List.map snd))
            |> Map.ofList
        let! zero = Money.fromDecimal 0M
        let! projectedAccounts =
            cashAccounts
            |> List.map (fun account ->
                let accountId = account |> Account.accountId
                let invoices = projectedInvoicesByAccountId |> Map.tryFind accountId |> Option.defaultValue []
                let currentBalance = balanceByAccountId |> Map.tryFind accountId |> Option.defaultValue zero
                let amountsForDirection (direction: CashFlowComponent.FlowDirection) =
                    invoices
                    |> List.filter (fun (invoice: CashFlowComponent.ProjectedInvoice) -> invoice.direction = direction)
                    |> List.map (fun invoice -> invoice.outstanding)
                result {
                    let! knownInflows = CashFlowComponent.Income |> amountsForDirection |> Money.sumList
                    let! knownOutflows = CashFlowComponent.Outgo |> amountsForDirection |> Money.sumList
                    let! balanceWithInflows = Money.add currentBalance knownInflows
                    let! projectedLow = Money.subtractVal1FromVal2 knownOutflows balanceWithInflows
                    let projectedAccount: CashFlowComponent.ProjectedAccount =
                        { accountId = accountId
                          accountCode = account |> Account.code
                          accountName = account |> Account.accountName
                          currentBalance = currentBalance
                          knownInflows = knownInflows
                          knownOutflows = knownOutflows
                          projectedLow = projectedLow
                          invoices = invoices }
                    return projectedAccount
                })
            |> convertListOfResultsToResultsList
        let billsToChase =
            openInstances
            |> List.filter (fun composite ->
                (composite |> InstanceOrchestration.instance |> Instance.instanceDate) <= horizonEnd)
            |> List.collect (fun composite ->
                let instance = composite |> InstanceOrchestration.instance
                let masterAgreementId = instance |> Instance.masterAgreementID
                let master = masterAgreementById |> Map.find masterAgreementId
                let invoicedAgreementIds =
                    composite
                    |> InstanceOrchestration.invoiceComposites
                    |> List.map (fun invoiceComposite ->
                        invoiceComposite |> InstanceOrchestration.invoice |> Invoice.paymentAgreementId)
                    |> Set.ofList
                paymentAgreementsByMasterId
                |> Map.tryFind masterAgreementId
                |> Option.defaultValue []
                |> List.filter (fun agreement ->
                    invoicedAgreementIds |> Set.contains (agreement |> PaymentAgreement.paymentAgreementId) |> not)
                |> List.map (fun agreement ->
                    let billToChase: CashFlowComponent.BillToChase =
                        { instanceId = instance |> Instance.instanceId
                          agreementName = instance |> Instance.masterAgreementName
                          paymentAgreementName = agreement |> PaymentAgreement.paymentAgreementName
                          instanceDate = instance |> Instance.instanceDate
                          cadenceType = master |> MasterAgreement.cadence |> Cadence.cadenceType }
                    billToChase))
        let projection: CashFlowComponent.CashFlowProjection =
            { accounts = projectedAccounts; billsToChase = billsToChase }
        return projection
    }

let private transitionOneInstancesPaymentsToPosted
    (context: Context.Context)
    (instanceId: CashFlowComponent.InstanceId)
    (postings:
        (Payment.Payment * JournalEntryComponent.JournalEntryLineId * Invoice.Invoice) list)
    : Result<CashFlowComponent.PaymentPostingTransition list, IAppError> =
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
                              journalEntryLineIdUpdate = FieldUpdate.SetTo(Some journalEntryLineId) }
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

/// confirmNoPaymentTargetsAVoidedEntry fails the whole transition when any staged line was posted as a journal entry
/// that has since been voided, naming every such Payment and its journal entry.
let private confirmNoPaymentTargetsAVoidedEntry
    (context: Context.Context)
    (paymentsAndJournalEntryLines: (Payment.Payment * JournalEntryComponent.JournalEntryLineId) list)
    : Result<unit, IAppError> =
    result {
        let! voided =
            paymentsAndJournalEntryLines
            |> List.map (fun (payment, journalEntryLineId) -> result {
                let journalEntryLineUuid = journalEntryLineId |> JournalEntryComponent.JournalEntryLineId.value
                let! line =
                    journalEntryLineId |> JournalEntryLine.fetchById context
                    |> whenNoRows (LedgerError.JournalEntryLineIdDoesntExist journalEntryLineUuid)
                let headerId = line |> JournalEntryLine.journalEntryHeaderId
                let headerUuid = headerId |> JournalEntryComponent.JournalEntryHeaderId.value
                let! header =
                    headerId |> JournalEntryHeader.fetchById context
                    |> whenNoRows (LedgerError.JournalEntryHeaderIdDoesntExist headerUuid)
                return
                    match header |> JournalEntryHeader.voidedAt with
                    | Some _ -> Some (payment |> Payment.paymentId |> CashFlowComponent.PaymentId.value, headerUuid)
                    | None -> None })
            |> convertListOfResultsToResultsList
            |> Result.map (List.choose id)
        return!
            if voided |> List.isEmpty then Ok ()
            else CashFlowError.error (CashFlowError.CashflowPaymentsTargetVoidedEntries voided)
    }

let transitionPaymentsToPosted (context: Context.Context) : Result<CashFlowComponent.PaymentPostingTransition list, IAppError> =
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
        do! paymentsAndJournalEntryLines |> confirmNoPaymentTargetsAVoidedEntry context
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
                    let invoiceUuid = invoiceId |> CashFlowComponent.InvoiceId.value
                    CashFlowError.error(CashFlowError.CashflowInvoiceIdDoesntExist invoiceUuid))
            |> convertListOfResultsToResultsList
        let! transitions =
            postings
            |> List.groupBy (fun (_, _, invoice) -> invoice |> Invoice.instanceId)
            |> List.map (fun (instanceId, instancePostings) ->
                instancePostings |> transitionOneInstancesPaymentsToPosted context instanceId)
            |> convertListOfResultsToResultsList
        return transitions |> List.concat
    }
