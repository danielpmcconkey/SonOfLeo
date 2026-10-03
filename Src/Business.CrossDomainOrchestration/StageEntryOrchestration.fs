module Business.CrossDomainOrchestration.StageEntryOrchestration

open System
open App.Utility.IAppError
open App.Utility.Result
open App.Utility.FieldUpdate
open App.DataAccessLayer
open App.DataAccessLayer.DalError
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.DataIngestion.BaseStageEntry
open Business.FinancialServices.Classification.ClassificationComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.FetchFilters

type StageEntry =
    private {
        stageEntryHeader: StageEntryHeader.StageEntryHeader
        seLines: StageEntryLine.StageEntryLine list
        statusTransitions: StageEntryStatusTransition.StageEntryStatusTransition list
    }

/// DeduplicationResult is every entry still Ingested after the pass, and every repeat the pass declined to flag
/// because a Payment references one of its lines, whatever its status.
type DeduplicationResult = {
    ingested: StageEntry list
    declinedForPayment: StageEntry list
}

type AccountClassificationResult = {
    runId: ClassificationRunId
    classificationResults: ClassificationResult list
    stagedEntries: StageEntry list
}

    

let stageEntryHeader se = se.stageEntryHeader
let seLines se = se.seLines
let statusTransitions se = se.statusTransitions

let private sumLinesByType
    (debitOrCredit: JournalEntryLineType)
    (lines: StageEntryLine.StageEntryLine list)
    : Result<Money.Money, IAppError> =
    lines
    |> List.filter(fun x -> x |> StageEntryLine.lineType = debitOrCredit)
    |> List.map(fun x -> x |> StageEntryLine.amount) |> Money.sumList
    
let private confirmAmountEquality (lines: StageEntryLine.StageEntryLine list) : Result<unit, IAppError> =
    result {
        let! totalDebits = lines |> sumLinesByType JournalEntryLineType.Debit
        let! totalCredits = lines |> sumLinesByType JournalEntryLineType.Credit
        return!
            if Money.isEqual totalCredits totalDebits then
                Ok()
            else
                Error(DataIngestionError.IngestionStageEntryDebitCreditMismatch(
                    totalDebits |> Money.amount, totalCredits |> Money.amount))
    }

let private confirmLineCount (lines: StageEntryLine.StageEntryLine list) : Result<unit, IAppError> =
    if lines |> List.length < 2 then
        Error(DataIngestionError.IngestionStageEntryInsufficientLines(lines |> List.length))
    else
        Ok()

let private confirmLinesAreAllPositive (lines: StageEntryLine.StageEntryLine list) : Result<unit, IAppError> =
    let checkedLines =
        lines
        |> List.map(fun x ->
            let amount = x |> StageEntryLine.amount
            if amount |> Money.isPositive |> not
            then DataIngestionError.error(DataIngestionError.IngestionStageLineNonPositiveAmount(amount |> Money.amount))
            else Ok ()
            )
        |> convertListOfResultsToResultsList
    match checkedLines with
    | Error e -> Error e
    | Ok _ -> Ok ()

let private confirmLinesAccountCodes
    (context: Context.Context)
    (accountValidationType: AccountValidationType)
    (lines: StageEntryLine.StageEntryLine list)
    : Result<unit, IAppError> =
    let checkedLines =
        lines
        |> List.map(fun x ->
            let accountIdOption = x |> StageEntryLine.accountId
            match accountValidationType, accountIdOption with
            | AccountValidationType.AllowNone, None -> Ok ()
            | AccountValidationType.DisallowNone, None ->
                DataIngestionError.error (
                    DataIngestionError.IngestionNoneAccount (x |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value))
            | _, Some accountId ->
                let accountUuid = accountId |> AccountId.value
                let lookupResult =
                    accountUuid |> Account.idToCode.fetch (context |> Context.getDatabaseTransaction) // we don't need the code; we just check that the ID is in the DB this way 
                lookupResult
                |> whenNoRows (LedgerError.AccountIdDoesntMatch accountUuid)
                |> Result.map ignore
            )
        |> convertListOfResultsToResultsList
    match checkedLines with
    | Error e -> Error e
    | Ok _ -> Ok ()

let private confirmLines
    (context: Context.Context)
    (accountCodeValidationType: AccountValidationType)
    (lines: StageEntryLine.StageEntryLine list)
    : Result<unit, IAppError> =
    result {
        do! lines |> confirmLineCount
        do! lines |> confirmAmountEquality
        do! lines |> confirmLinesAreAllPositive
        do! lines |> confirmLinesAccountCodes context accountCodeValidationType // do the expensive one last
    }

let private confirmValidTransitions transitions =
    let check =
        transitions
        |> List.map StageEntryStatusTransition.confirmValidTransition
        |> convertListOfResultsToResultsList
    match check with
    | Error e -> Error e
    | Ok _ -> Ok ()

let private confirmStageEntryCompositeIsValid
    (context: Context.Context)
    (accountCodeValidationType: AccountValidationType)
    (stageEntry: StageEntry)
    : Result<unit, IAppError> =
    result {
        do! stageEntry.seLines |> confirmLines context accountCodeValidationType
        do! stageEntry.statusTransitions |> confirmValidTransitions
        do!
            if stageEntry.statusTransitions |> List.isEmpty
            then Error DataIngestionError.IngestionStatusTransitionList
            else Ok ()
    }

let createStageEntry
    (context: Context.Context)
    (header: StageEntryHeader.StageEntryHeader)
    (lines: StageEntryLine.StageEntryLine list)
    (transitions: StageEntryStatusTransition.StageEntryStatusTransition list)
    : Result<StageEntry, IAppError> =
    result {
        let stageEntry = {
            stageEntryHeader = header
            seLines = lines
            statusTransitions = transitions }
        do! stageEntry |> confirmStageEntryCompositeIsValid context AccountValidationType.AllowNone
        return stageEntry
    }
    
let private constructGroup
    (context: Context.Context)
    (sourceFile: SourceFile)
    (baseStageEntryGroupId: BaseStageEntryGroupId, rawRowsAtGroupId: BaseStageRawRow list)
    : Result<StageEntry, IAppError> =
        let distinctHeadersList =
            rawRowsAtGroupId
            |> List.groupBy(fun x -> x.entryDate, x.description, x.fiSource, x.fiReference)
        if distinctHeadersList |> List.length > 1
        then DataIngestionError.error (
            DataIngestionError.IngestionBaseStageGroupIdDistinctDataViolation (
                baseStageEntryGroupId |> BaseStageEntryGroupId.value))
        else
            let theOnly = distinctHeadersList |> List.head
            let entryDate, description, fiSource, fiReference = theOnly |> fst
            let rawRowsAtTheOnly = theOnly |> snd
            result {
                let stageEntryId = StageEntryHeaderId.create ()
                let lines =
                    rawRowsAtTheOnly
                    |> List.map (fun row -> 
                        let lineId = StageEntryLineId.create ()
                        StageEntryLine.create
                            lineId stageEntryId row.amount row.entryType row.accountId row.memo None
                        )
                let! ingestionSource = fiSource |> IngestionSource.fetchByName context
                let header =
                    StageEntryHeader.create
                        sourceFile stageEntryId entryDate description ingestionSource fiReference None
                            (Some Ingested)
                let transitionId = StageEntryStatusTransitionId.create ()
                let transition = StageEntryStatusTransition.create transitionId stageEntryId
                                      None Ingested (context |> Context.getInitiationInstant) StageIngestion
                return! createStageEntry context header lines [transition]
            }

/// constructFromRaw builds one staged entry per group without writing anything. Every group is checked, and every group
/// that fails is returned with its error, not only the first.
let constructFromRaw
    (context: Context.Context)
    (sourceFile: SourceFile)
    (rawRows: BaseStageRawRow list)
    : Result<StageEntry list, (BaseStageEntryGroupId * IAppError) list> =
    let constructed =
        rawRows
        |> List.groupBy(_.baseStageEntryGroupId)
        |> List.map(fun group -> fst group, group |> constructGroup context sourceFile)
    match constructed |> List.choose (fun (groupId, built) -> match built with Error e -> Some(groupId, e) | Ok _ -> None) with
    | [] -> Ok (constructed |> List.choose (fun (_, built) -> match built with Ok entry -> Some entry | Error _ -> None))
    | failures -> Error failures

let private fetchAllLinesByHeaders
    (context: Context.Context)
    (headers: StageEntryHeader.StageEntryHeader list)
    : Result<StageEntryLine.StageEntryLine list, IAppError> =
    headers
    |> List.map(fun x -> x |> StageEntryHeader.stageEntryHeaderId)
    |> StageEntryLine.fetchByHeaderIdList context

let private fetchAllTransitionsByHeaders
    (context: Context.Context)
    (headers: StageEntryHeader.StageEntryHeader list)
    : Result<StageEntryStatusTransition.StageEntryStatusTransition list, IAppError> =
    headers
    |> List.map(fun x -> x |> StageEntryHeader.stageEntryHeaderId)
    |> StageEntryStatusTransition.fetchByHeaderIdList context

let private compileFromSubLists
    (headers: StageEntryHeader.StageEntryHeader list)
    (lines: StageEntryLine.StageEntryLine list)
    (statusTransitions: StageEntryStatusTransition.StageEntryStatusTransition list)
    : StageEntry list =
    headers
    |> List.map (fun h ->
        let headerId = h |> StageEntryHeader.stageEntryHeaderId
        let linesAtH = lines |> List.filter(fun l -> l |> StageEntryLine.stageEntryHeaderId = headerId)
        let transitionsAtH =
            statusTransitions
            |> List.filter(fun l -> l |> StageEntryStatusTransition.stageEntryHeaderId = headerId)
        { stageEntryHeader = h
          seLines = linesAtH
          statusTransitions = transitionsAtH } )
    
let fetchAllByFile
    (context: Context.Context)
    (statusFilter: StagedEntryStatus list option)
    (sourceFile: SourceFile)
    : Result<StageEntry list, IAppError> =
    result {
        let! headers = sourceFile |> StageEntryHeader.fetchBySourceFile context statusFilter
        if headers |> List.isEmpty then return [] else
        let! lines = headers |> fetchAllLinesByHeaders context
        let! statuses = headers |> fetchAllTransitionsByHeaders context
        return compileFromSubLists headers lines statuses
    }

let fetchAllForPosting
    (context: Context.Context)
    : Result<StageEntry list, IAppError> =
    result {
        let! headersReviewed = StageEntryHeader.fetchByStatus context StagedEntryStatus.Reviewed
        let! headersClassified = StageEntryHeader.fetchByStatus context StagedEntryStatus.Classified
        let headersToBePosted = headersReviewed @ headersClassified
        if headersToBePosted |> List.isEmpty then return [] else
        let headerIds = 
            headersToBePosted
            |> List.map (fun x -> x|> StageEntryHeader.stageEntryHeaderId)
        let! lines = headerIds |> StageEntryLine.fetchByHeaderIdList context
        let! statusTransitions = headerIds |> StageEntryStatusTransition.fetchByHeaderIdList context
        return compileFromSubLists headersToBePosted lines statusTransitions
    }


let fetchByStatusList
    (context: Context.Context)
    (statuses: StagedEntryStatus list)
    : Result<StageEntry list, IAppError> =
    result {
        let! headersByStatus =
            statuses
            |> List.map (fun status -> StageEntryHeader.fetchByStatus context status)
            |> convertListOfResultsToResultsList
        let headers = headersByStatus |> List.concat
        if headers |> List.isEmpty then return [] else
        let headerIds = headers |> List.map (fun x -> x |> StageEntryHeader.stageEntryHeaderId)
        let! lines = headerIds |> StageEntryLine.fetchByHeaderIdList context
        let! statusTransitions = headerIds |> StageEntryStatusTransition.fetchByHeaderIdList context
        return compileFromSubLists headers lines statusTransitions
    }

let fetchByStageEntryHeaderId
    (context: Context.Context)
    (headerId: StageEntryHeaderId)
    : Result<StageEntry, IAppError> =
    result {
        let! header = headerId |> StageEntryHeader.fetchById context
        let! lines = headerId |> StageEntryLine.fetchByHeaderId context
        let! statusTransitions = headerId |> StageEntryStatusTransition.fetchByHeaderId context
        return { stageEntryHeader = header
                 seLines = lines
                 statusTransitions = statusTransitions }
    }

let constructNewAndPersist
    (context: Context.Context)
    (name: JournalRefFinancialInstitution)
    : Result<IngestionSource.IngestionSource, IAppError> =
    result {
        // records resolve their source by name, so a second holder of a name would make every file from it unresolvable.
        // the unique constraint backs this up; checking first is what gives the caller a typed error
        let! holders = name |> IngestionSource.fetchAllByName context
        do!
            if holders |> List.isEmpty then Ok ()
            else Error(DataIngestionError.IngestionSourceNameAlreadyExists(name |> JournalRefFinancialInstitution.value))
        let instant = context |> Context.getInitiationInstant
        let uuid = IngestionSourceId.create()
        let newSource = IngestionSource.create uuid name instant instant
        do! newSource |> IngestionSource.persist context
        return newSource }

/// headerIdsWithAPaidLine returns which of the given staged entries have a line a Payment references, Staged or Posted.
let private headerIdsWithAPaidLine
    (context: Context.Context)
    (headerIds: StageEntryHeaderId list)
    : Result<StageEntryHeaderId list, IAppError> =
    if headerIds |> List.isEmpty then Ok [] else
    result {
        let! lines = headerIds |> StageEntryLine.fetchByHeaderIdList context
        if lines |> List.isEmpty then return [] else
        let! paidLineIds =
            lines |> List.map StageEntryLine.stageEntryLineId |> CashFlow.Payment.fetchReferencedStageEntryLineIds context
        return
            lines
            |> List.filter (fun line -> paidLineIds |> List.contains (line |> StageEntryLine.stageEntryLineId))
            |> List.map StageEntryLine.stageEntryHeaderId
            |> List.distinct
    }

let deduplicateStagedEntries
    (context: Context.Context)
    : Result<DeduplicationResult, IAppError> =
    result {
        let! repeatedHeaders = StageEntryHeader.fetchDuplicates context
        // an entry a Payment references is never flagged; it stays at its status and is reported (REQ-STG-6.7)
        let! paidHeaderIds =
            repeatedHeaders |> List.map StageEntryHeader.stageEntryHeaderId |> headerIdsWithAPaidLine context
        let duplicateHeaders =
            repeatedHeaders
            |> List.filter (fun header -> paidHeaderIds |> List.contains (header |> StageEntryHeader.stageEntryHeaderId) |> not)
        let toStatus = StagedEntryStatus.Duplicate
        let mechanism = StageStatusChangeMechanism.Deduplicator
        let! _ = duplicateHeaders
                 |> List.map(fun dup ->
                     dup
                     |> StageEntryHeader.stageEntryHeaderId
                     |> StageEntryHeader.updateHeaderStatus context toStatus mechanism
                     )
                 |> convertListOfResultsToResultsList
        let declinedHeaders =
            repeatedHeaders
            |> List.filter (fun header -> paidHeaderIds |> List.contains (header |> StageEntryHeader.stageEntryHeaderId))
        let! declined =
            if declinedHeaders |> List.isEmpty then Ok [] else
            result {
                let headerIds = declinedHeaders |> List.map StageEntryHeader.stageEntryHeaderId
                let! lines = headerIds |> StageEntryLine.fetchByHeaderIdList context
                let! statusTransitions = headerIds |> StageEntryStatusTransition.fetchByHeaderIdList context
                return compileFromSubLists declinedHeaders lines statusTransitions
            }
        let! ingested = [ StagedEntryStatus.Ingested ] |> fetchByStatusList context
        return { ingested = ingested; declinedForPayment = declined }
    }

/// accountClassificationStatuses are the statuses of the entries an account classification run considers.
let accountClassificationStatuses = [ Ingested; StagedEntryStatus.NoMatch; Conflict ]

/// applyAccountClassification writes each clear winner's account to its line, then sets every considered entry's
/// status from the accounts it now holds and the run's ties.
let applyAccountClassification
    (context: Context.Context)
    (classificationRun: ClassificationRun)
    : Result<AccountClassificationResult, IAppError> =
    result {
        let isFullyAssigned (entry: StageEntry) : bool =
            entry |> seLines |> List.forall (fun line -> line |> StageEntryLine.accountId |> Option.isSome)
        let classificationResults = classificationRun.results
        let claimedAccountId (prioritizedMatch: PrioritizedMatch) : AccountId option =
            match prioritizedMatch.claimant with
            | ClassificationClaimant.Account accountId -> Some accountId
            | ClassificationClaimant.PaymentAgreement _ -> None
        let winningAccountId (outcome: ClassifierOutcome) : AccountId option =
            match outcome with
            | OneMatch prioritizedMatch -> prioritizedMatch |> claimedAccountId
            | ManyMatchesClearWinner (winner, _) -> winner |> claimedAccountId
            | ClassifierOutcome.NoMatch | ManyMatchesTied _ -> None
        let! _ =
            classificationResults
            |> List.choose (fun result ->
                result.outcome
                |> winningAccountId
                |> Option.map (fun accountId -> result.candidate.lineIdOfCandidate, accountId))
            |> List.map (fun (lineId, accountId) ->
                lineId
                |> StageEntryLine.updateAccountId context (SetTo (Some accountId))
                |> Result.map ignore)
            |> convertListOfResultsToResultsList
        let headerIdsWithATie =
            classificationResults
            |> List.filter (fun result ->
                match result.outcome with | ManyMatchesTied _ -> true | _ -> false)
            |> List.map (fun result -> result.candidate.headerIdOfCandidate)
            |> List.distinct
        // re-read so the status below is derived from the accounts just written rather than the pre-write roster
        let! rosterAfterWrites = accountClassificationStatuses |> fetchByStatusList context
        let! _ =
            rosterAfterWrites
            |> List.map (fun entry ->
                let headerId = entry.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let mechanism = StageStatusChangeMechanism.Classifier
                let newStatus =
                    if entry |> isFullyAssigned then Classified
                    elif headerIdsWithATie |> List.contains headerId then Conflict
                    else StagedEntryStatus.NoMatch
                headerId |> StageEntryHeader.updateHeaderStatus context newStatus mechanism)
            |> convertListOfResultsToResultsList
        let! stagedEntries =
            [ Ingested; Classified; StagedEntryStatus.NoMatch; Conflict; Reviewed ] |> fetchByStatusList context
        return { runId = classificationRun.runId
                 classificationResults = classificationResults
                 stagedEntries = stagedEntries }
    }

/// persistConstructed writes entries built by constructFromRaw.
let persistConstructed
    (context: Context.Context)
    (entries: StageEntry list)
    : Result<unit, IAppError> =
    result {
        // the transitions written are the ones constructFromRaw built, so the entries handed in match what is stored
        // (REQ-STG-3.13)
        let! _ =
            entries
            |> List.map(fun e -> e |> stageEntryHeader |> StageEntryHeader.persist context)
            |> convertListOfResultsToResultsList
        let! _ =
            entries
            |> List.collect statusTransitions
            |> List.map (StageEntryHeader.persistStatusTransition context)
            |> convertListOfResultsToResultsList
        let! _ =
            entries
            |> List.collect seLines
            |> List.map(fun l -> l |> StageEntryLine.persist context )
            |> convertListOfResultsToResultsList
        return ()
    }

let ingestRawToStage
    (context: Context.Context)
    (sourceFile: SourceFile)
    (rawRows: BaseStageRawRow list)
    : Result<StageEntry list, IAppError> =
    result {
        // a caller handing over rows rather than a file gets the first failing group; the route reports every one
        let! entries = rawRows |> constructFromRaw context sourceFile |> Result.mapError (List.head >> snd)
        do! entries |> persistConstructed context
        return entries
    }

let private confirmUpdateLinesMatchUpdateHeader
    (context: Context.Context)
    (headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates)
    (lineUpdates: StageEntryLine.StageEntryLineFieldUpdates list)
    : Result<unit, IAppError> =
    lineUpdates
    |> List.map (fun lineUpdate ->
        result {
            let! lineHeaderIdToCompare =
                lineUpdate.lineIdToUpdate
                |> StageEntryLine.fetchById context
                |> whenNoRows (
                    DataIngestionError.IngestionStageEntryLineIdDoesntExist (lineUpdate.lineIdToUpdate |> StageEntryLineId.value))
                |> Result.map StageEntryLine.stageEntryHeaderId
            let headerId = headerUpdates.headerIdToUpdate
            return!
                if lineHeaderIdToCompare = headerId then Ok ()
                else
                    let headerUuid = headerId |> StageEntryHeaderId.value
                    let lineUuid = lineUpdate.lineIdToUpdate |> StageEntryLineId.value
                    Error (DataIngestionError.IngestionUpdateStageEntryLinesMustMatchHeader(headerUuid, lineUuid))
        } )
    |> convertListOfResultsToResultsList
    |> Result.map ignore

// if the updateStageEntry only wants to update lines, this is a way to know that you don't have to try to update the
// header (and risk a no-op error)
let isThereAHeaderUpdate
    (headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates)
    : bool =
    headerUpdates.sourceFileUpdate <> FieldUpdate.NoChange
    || headerUpdates.entryDateUpdate <> FieldUpdate.NoChange
    || headerUpdates.descriptionUpdate <> FieldUpdate.NoChange
    || headerUpdates.ingestionSourceUpdate <> FieldUpdate.NoChange
    || headerUpdates.fiReferenceUpdate <> FieldUpdate.NoChange
    || headerUpdates.journalEntryHeaderIdUpdate <> FieldUpdate.NoChange
    || headerUpdates.statusUpdate <> FieldUpdate.NoChange
    
// if the updateStageEntry only wants to update the header, this is a way to know that you don't have to try to update the
// lines (and risk a no-op error)
let isThereALineUpdate
    (lineUpdates: StageEntryLine.StageEntryLineFieldUpdates list)
    : bool =
    lineUpdates
    |> List.map (fun lu ->
        lu.amountUpdate <> FieldUpdate.NoChange
        || lu.entryTypeUpdate <> FieldUpdate.NoChange
        || lu.accountIdUpdate <> FieldUpdate.NoChange
        || lu.memoUpdate <> FieldUpdate.NoChange
        || lu.journalEntryLineIdUpdate <> FieldUpdate.NoChange
        )
    |> List.exists id
    
/// protectionsOf says, for each of the given lines, whatever keeps it from being removed or having its amount, line type
/// or account changed (REQ-STG-6.5): a payment agreement link, a Payment, or a classification run's record of it.
let private protectionsOf
    (context: Context.Context)
    (lineIds: StageEntryLineId list)
    : Result<(StageEntryLineId * Classification.ClassificationError.StageLineProtection) list, IAppError> =
    if lineIds |> List.isEmpty then Ok [] else
    result {
        let! links = lineIds |> CashFlow.PaymentAgreementLink.fetchByStageEntryLineIdList context
        let! paidLineIds = lineIds |> CashFlow.Payment.fetchReferencedStageEntryLineIds context
        let! ruleMatches = lineIds |> Classification.RuleMatch.fetchByStageEntryLineIdList context
        let linkedLineIds = links |> List.map CashFlow.PaymentAgreementLink.stageEntryLineId
        let recordedLineIds = ruleMatches |> List.map Classification.RuleMatch.stageEntryLineId
        return
            [ yield! linkedLineIds |> List.map (fun id -> id, Classification.ClassificationError.LinkedToPaymentAgreement)
              yield! paidLineIds |> List.map (fun id -> id, Classification.ClassificationError.ReferencedByPayment)
              yield! recordedLineIds |> List.map (fun id -> id, Classification.ClassificationError.RecordedInClassificationRun) ]
            |> List.distinct
    }

/// changesProtectedField is true when an update would change a line's amount, line type or account from what it holds;
/// setting a field to the value it already has changes nothing.
let private changesProtectedField (line: StageEntryLine.StageEntryLine) (lineUpdate: StageEntryLine.StageEntryLineFieldUpdates) =
    let differs current update =
        match update with
        | NoChange -> false
        | SetTo x -> x <> current
    differs (line |> StageEntryLine.amount) lineUpdate.amountUpdate
    || differs (line |> StageEntryLine.lineType) lineUpdate.entryTypeUpdate
    || differs (line |> StageEntryLine.accountId) lineUpdate.accountIdUpdate

/// updateStageEntry is the operator's manual update. It can edit the entry and its lines, add lines and remove them in
/// one operation, and only the entry as the whole operation leaves it is validated (REQ-STG-6.4): a split passes
/// through an unbalanced state on the way. Everything is checked before anything is written.
let updateStageEntry
    (context: Context.Context)
    (headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates)
    (lineUpdates: StageEntryLine.StageEntryLineFieldUpdates list)
    // each line the operator adds: amount, line type, account and memo
    (linesToAdd: (Money.Money * JournalEntryLineType * AccountId option * JournalEntryLineMemo option) list)
    (lineIdsToRemove: StageEntryLineId list)
    : Result<StageEntry, IAppError> =
    result {
        let shouldUpdateHeader = headerUpdates |> isThereAHeaderUpdate
        let shouldUpdateLines = lineUpdates |> isThereALineUpdate
        do! if shouldUpdateHeader = false && shouldUpdateLines = false
               && linesToAdd.IsEmpty && lineIdsToRemove.IsEmpty
            then (Error DataIngestionError.IngestionUpdateStageEntryNoOp)
            else Ok ()
        let headerId = headerUpdates.headerIdToUpdate
        let headerUuid = headerId |> StageEntryHeaderId.value
        let! current =
            headerId
            |> fetchByStageEntryHeaderId context
            |> whenNoRows (DataIngestionError.IngestionStageEntryHeaderIdDoesntExist headerUuid)
        // a posted entry's lines are paired to its journal entry's lines; correcting it is void and repost (REQ-STG-6.6)
        do!
            if current.stageEntryHeader |> StageEntryHeader.currentStatus = Some Posted
            then Error(DataIngestionError.IngestionPostedStageEntryCannotBeModified headerUuid)
            else Ok ()
        // an entry a Payment references cannot leave the set that posts (REQ-STG-6.7)
        do!
            match headerUpdates.statusUpdate with
            | SetTo (StagedEntryStatus.Duplicate | Ignored as excluded) ->
                headerIdsWithAPaidLine context [ headerId ]
                |> Result.bind (fun paid ->
                    if paid.IsEmpty then Ok ()
                    else
                        Classification.ClassificationError.error(
                            Classification.ClassificationError.ClassificationPaidStageEntryCannotBeExcluded(
                                headerUuid, excluded |> StagedEntryStatus.toString)))
            | _ -> Ok ()
        // only batch post moves an entry to Posted (REQ-STG-4.8)
        do!
            match headerUpdates.statusUpdate with
            | SetTo Posted -> Error(DataIngestionError.IngestionManualUpdateCannotSetStatus(Posted |> StagedEntryStatus.toString))
            | _ -> Ok ()
        do! confirmUpdateLinesMatchUpdateHeader context headerUpdates lineUpdates
        let lineIdsToCheck = lineIdsToRemove |> List.distinct
        do!
            lineIdsToCheck
            |> List.map (fun lineId ->
                if current.seLines |> List.exists (fun line -> line |> StageEntryLine.stageEntryLineId = lineId) then Ok ()
                else
                    let lineUuid = lineId |> StageEntryLineId.value
                    lineId
                    |> StageEntryLine.fetchById context
                    |> whenNoRows (DataIngestionError.IngestionStageEntryLineIdDoesntExist lineUuid)
                    |> Result.bind (fun _ ->
                        Error(DataIngestionError.IngestionUpdateStageEntryLinesMustMatchHeader(headerUuid, lineUuid))))
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let changedLines =
            lineUpdates
            |> List.filter (fun lineUpdate ->
                current.seLines
                |> List.exists (fun line ->
                    line |> StageEntryLine.stageEntryLineId = lineUpdate.lineIdToUpdate
                    && changesProtectedField line lineUpdate))
            |> List.map _.lineIdToUpdate
        let! protections = (lineIdsToCheck @ changedLines) |> List.distinct |> protectionsOf context
        let firstProtectionOf lineId =
            protections |> List.tryFind (fun (id, _) -> id = lineId) |> Option.map snd
        do!
            lineIdsToCheck
            |> List.map (fun lineId ->
                match firstProtectionOf lineId with
                | Some protection ->
                    Classification.ClassificationError.error(
                        Classification.ClassificationError.ClassificationStageEntryLineCannotBeRemoved(lineId |> StageEntryLineId.value, protection))
                | None -> Ok ())
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        // a classification run's record does not stop an edit, only a removal: the record stays true of the line
        do!
            changedLines
            |> List.map (fun lineId ->
                match
                    protections
                    |> List.tryFind (fun (id, protection) ->
                        id = lineId && protection <> Classification.ClassificationError.RecordedInClassificationRun)
                with
                | Some (_, protection) ->
                    Classification.ClassificationError.error(
                        Classification.ClassificationError.ClassificationStageEntryLineCannotBeChanged(lineId |> StageEntryLineId.value, protection))
                | None -> Ok ())
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        let addedLines =
            linesToAdd
            |> List.map (fun (amount, lineType, accountId, memo) ->
                StageEntryLine.create (StageEntryLineId.create ()) headerId amount lineType accountId memo None)
        let finalLines =
            (current.seLines
             |> List.filter (fun line -> lineIdsToCheck |> List.contains (line |> StageEntryLine.stageEntryLineId) |> not)
             |> List.map (fun line ->
                 lineUpdates
                 |> List.filter (fun lineUpdate -> lineUpdate.lineIdToUpdate = (line |> StageEntryLine.stageEntryLineId))
                 |> List.fold (fun updated lineUpdate -> updated |> StageEntryLine.applyFieldUpdates lineUpdate) line))
            @ addedLines
        do! finalLines |> confirmLines context AccountValidationType.AllowNone
        do! if shouldUpdateLines
            then
                lineUpdates
                |> List.filter (fun lineUpdate -> [ lineUpdate ] |> isThereALineUpdate)
                |> List.map(fun lineUpdate -> lineUpdate |> StageEntryLine.update context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        do! lineIdsToCheck
            |> List.map (StageEntryLine.delete context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do! addedLines
            |> List.map (StageEntryLine.persist context)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        do! if shouldUpdateHeader then headerUpdates |> StageEntryHeader.update context |> Result.map ignore
            else Ok ()
        // now that we updated everything, we should read it back and ensure it still meets composite requirements
        let! fetched = headerId |> fetchByStageEntryHeaderId context
        do! fetched |> confirmStageEntryCompositeIsValid context AccountValidationType.AllowNone
        return fetched
    }

let private isSameLine
    (stageEntryLine: StageEntryLine.StageEntryLine)
    (journalEntryLine: JournalEntryLine.JournalEntryLine)
    : bool =
    let stageAccountId = stageEntryLine |> StageEntryLine.accountId
    let journalAccountId = journalEntryLine |> JournalEntryLine.accountId
    let stageAmount = stageEntryLine |> StageEntryLine.amount
    let journalAmount = journalEntryLine |> JournalEntryLine.amount
    let stageLineType = stageEntryLine |> StageEntryLine.lineType
    let journalLineType = journalEntryLine |> JournalEntryLine.lineType
    stageAccountId = Some journalAccountId && Money.isEqual stageAmount journalAmount && stageLineType = journalLineType

/// pairStageLinesToJournalEntryLines matches on account, line type, and amount rather than trusting the two lists to
/// arrive in the same order. A matched journal entry line leaves the pool, so two identical staged lines still pair
/// one to one.
let rec private pairStageLinesToJournalEntryLines
    (pairs: (StageEntryLine.StageEntryLine * JournalEntryLine.JournalEntryLine) list)
    (unpairedJournalEntryLines: JournalEntryLine.JournalEntryLine list)
    (stageEntryLines: StageEntryLine.StageEntryLine list)
    : Result<(StageEntryLine.StageEntryLine * JournalEntryLine.JournalEntryLine) list, IAppError> =
    match stageEntryLines with
    | [] -> Ok (pairs |> List.rev)
    | stageEntryLine :: remainingStageEntryLines ->
        match unpairedJournalEntryLines |> List.tryFind (isSameLine stageEntryLine) with
        | None ->
            let uuid = stageEntryLine |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value
            Error (DataIngestionError.IngestionStageEntryLineNoMatchingJournalEntryLine uuid)
        | Some journalEntryLine ->
            let pairedId = journalEntryLine |> JournalEntryLine.journalEntryLineId
            let stillUnpaired =
                unpairedJournalEntryLines
                |> List.filter (fun x -> (x |> JournalEntryLine.journalEntryLineId) <> pairedId)
            remainingStageEntryLines
            |> pairStageLinesToJournalEntryLines ((stageEntryLine, journalEntryLine) :: pairs) stillUnpaired

let postStageEntry
    (context: Context.Context)
    (jeHeaderSource: JournalEntrySource option)
    (stageEntry: StageEntry)
    : Result<unit, IAppError> =
    result {
        let description = stageEntry.stageEntryHeader |> StageEntryHeader.description
        let! entryDate =
            stageEntry.stageEntryHeader
            |> StageEntryHeader.entryDate
            |> EntryDate.create context
        let fi = stageEntry.stageEntryHeader |> StageEntryHeader.ingestionSource |> IngestionSource.name
        let fiReference = stageEntry.stageEntryHeader |> StageEntryHeader.fiReference
        let references = [(fi, fiReference)]
        let comments = []
        let! lines =
            stageEntry.seLines
            |> List.map (fun line ->
                result {
                    let! accountId =
                        match line |> StageEntryLine.accountId with
                        | None -> DataIngestionError.error (DataIngestionError.IngestionNoneAccount (
                                line |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value))
                        | Some x -> Ok x
                    let amount = line |> StageEntryLine.amount
                    let lineType = line |> StageEntryLine.lineType
                    let memo = line |> StageEntryLine.memo
                    return accountId, amount, lineType, memo
                } )
            |> convertListOfResultsToResultsList
        let! journalEntry =
            JournalEntryOrchestration.constructNewAndPersist
                context
                description
                jeHeaderSource
                entryDate
                lines
                references
                comments
        let headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates = {
            headerIdToUpdate = stageEntry.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
            sourceFileUpdate = FieldUpdate.NoChange
            entryDateUpdate = FieldUpdate.NoChange
            descriptionUpdate = FieldUpdate.NoChange
            ingestionSourceUpdate = FieldUpdate.NoChange
            fiReferenceUpdate = FieldUpdate.NoChange
            journalEntryHeaderIdUpdate =
                journalEntry
                |> JournalEntryOrchestration.header
                |> JournalEntryHeader.journalEntryHeaderId
                |> Some
                |> FieldUpdate.SetTo
            statusUpdate = FieldUpdate.NoChange }
        do! headerUpdates |> StageEntryHeader.update context |> Result.map ignore
        let! pairedLines =
            stageEntry.seLines
            |> pairStageLinesToJournalEntryLines [] (journalEntry |> JournalEntryOrchestration.jeLines)
        let! _ =
            pairedLines
            |> List.map (fun (stageEntryLine, journalEntryLine) ->
                let journalEntryLineIdUpdate =
                    journalEntryLine
                    |> JournalEntryLine.journalEntryLineId
                    |> Some
                    |> FieldUpdate.SetTo
                stageEntryLine
                |> StageEntryLine.stageEntryLineId
                |> StageEntryLine.updateJournalEntryLineId context journalEntryLineIdUpdate)
            |> convertListOfResultsToResultsList
        return ()}
    
/// post writes new journal entries to the ledger tables and updates the status in stage. That's it. This is not a
/// set-based operation, allowing the ledger types and modules to do their jobs in keeping stupid out of the ledger.
let post
    (context: Context.Context)
    : Result<unit, IAppError> =
    result {
        let! stageEntries = fetchAllForPosting context
        if stageEntries |> List.isEmpty then return () else
        let! jeHeaderSource =
            Some "Data ingestion import"
            |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
        // check the lines one last time just to be sure we're not trying to post any records whose accounts aren't set
        do! stageEntries
            |> List.map(fun stageEntry ->
                stageEntry.seLines |> confirmLinesAccountCodes context AccountValidationType.DisallowNone
                )
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        // post each
        do! stageEntries
            |> List.map(fun stageEntry -> stageEntry |> postStageEntry context jeHeaderSource)
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        // update our stage entry statuses
        do! stageEntries
            |> List.map(fun stageEntry ->
                let headerId = stageEntry.stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let newStatus = StagedEntryStatus.Posted
                let mechanism = StageStatusChangeMechanism.LedgerPoster
                headerId |> StageEntryHeader.updateHeaderStatus context newStatus mechanism
                )
            |> convertListOfResultsToResultsList
            |> Result.map ignore
        return ()
    }
    
let fetchFiltered
    (context: Context.Context)
    (sort: FetchStageEntrySort option)
    (filter: StageEntryFetchFilter)
    : Result<StageEntry list, IAppError> = result {
    let! filterDateRangeOption =
        filter.temporalFilter
        |> convertOptionToDesiredTypeWithFallibleConverter (getDateRangeFromTemporalFilter context)
    let dateRange = filterDateRangeOption |> Option.map (fun x -> x.beginDate, x.endInclusive)
    let sortClause =
        match sort with
        | None -> None
        | Some EntryDateAsc -> Some "se.entry_date asc"
        | Some EntryDateDesc -> Some "se.entry_date desc"
        | Some FiAsc -> Some "src.source_name asc"
        | Some FiDesc -> Some "src.source_name desc"
        | Some StatusAsc -> Some "latest_statuses.to_status asc"
        | Some StatusDesc -> Some "latest_statuses.to_status desc"
        | Some DescriptionAsc -> Some "se.description asc"
        | Some DescriptionDesc -> Some "se.description desc"
    let whereClausesAndParams =
        [
          filter.stageEntryHeaderId
          |> Option.map(fun x ->
              ("stage_entry_id = @stage_entry_id", { name = "@stage_entry_id"; value = UniqueId(x |> StageEntryHeaderId.value) }))

          filter.sourceFile
          |> Option.map(fun x ->
              ("source_file = @source_file",
               { name = "@source_file"; value = CharString(x |> SourceFile.value) }))

          dateRange
          |> Option.map(fun (x, _) ->
              ("entry_date >= @begin_date", { name = "@begin_date"; value = DbLocalDate x }))

          dateRange
          |> Option.map(fun (_, x) ->
              ("entry_date <= @end_date", { name = "@end_date"; value = DbLocalDate x }))
          
          filter.description
          |> Option.map(fun x ->
              let likeStr = x |> JournalEntryDescription.value |> containsPattern
              ("stage_entry_description like @stage_entry_description",
               { name = "@stage_entry_description"; value = CharString(likeStr) }))

          filter.ingestionSource
          |> Option.map(fun x ->
              ("source_name = @source_name",
               { name = "@source_name"; value = CharString(x |> JournalRefFinancialInstitution.value) }))

          filter.fiReference
          |> Option.map(fun x ->
              ("fi_reference = @fi_reference",
               { name = "@fi_reference"; value = CharString(x |> JournalExternalReferenceText.value) }))

          filter.status
          |> Option.map(fun x ->
              ("stage_entry_status = @stage_entry_status",
               { name = "@stage_entry_status"; value = CharString (x |> StagedEntryStatus.toString) }))

          filter.stageEntryLineId
          |> Option.map(fun x ->
              ("stage_line_entry_id = @stage_line_entry_id",
               { name = "@stage_line_entry_id"; value = UniqueId(x |> StageEntryLineId.value) }))

          filter.amount
          |> Option.map(fun x ->
              ("amount = @amount", { name = "@amount"; value = Numeric(x |> Money.amount) }))
          
          filter.lineType
          |> Option.map(fun x ->
              ("line_type = @line_type",
               { name = "@line_type"; value = CharString(x |> JournalEntryLineType.toString) }))
          
          filter.accountId
          |> Option.map(fun x ->
              ("account_id = @account_id",
               { name = "@account_id"; value = UniqueId(x |> AccountId.value) }))
          
          filter.memo
          |> Option.map(fun x ->
              ("memo = @memo",
               { name = "@memo"; value = CharString(x |> JournalEntryLineMemo.value) }))

          filter.journalEntryHeaderId
          |> Option.map(fun x ->
              ("journal_entry_header_id = @journal_entry_header_id",
               { name = "@journal_entry_header_id"; value = UniqueId(x |> JournalEntryHeaderId.value) }))

          filter.journalEntryLineId
          |> Option.map(fun x ->
              ("journal_entry_line_id = @journal_entry_line_id",
               { name = "@journal_entry_line_id"; value = UniqueId(x |> JournalEntryLineId.value) })) ]
        |> List.choose id
    let whereClauses =
        if whereClausesAndParams |> List.isEmpty then ""
        else
            let catClauses = whereClausesAndParams |> List.map fst |> String.concat $" and{Environment.NewLine}"
            $"where {catClauses}"
        
    let parameters = whereClausesAndParams |> List.map snd  
    let latestStatusCtes = StageEntryStatusTransition.formLatestStatusCte
    let multiFetchCtes =
        [
            """all_in_stage as (
            select
                se.unique_id as stage_entry_id,
                se.entry_date,
                se.description as stage_entry_description,
                src.source_name,
                se.fi_reference,
                se.source_file,
                se.journal_entry_header_id,
                sel.unique_id as stage_line_entry_id,
                sel.amount,
                sel.line_type,
                sel.account_id,
                sel.memo,
                sel.journal_entry_line_id,
                latest_statuses.to_status as stage_entry_status,
                latest_statuses.modified_at as latest_status_time_stamp
            from ingestion.staged_entry se
            join ingestion.source src on se.source_id = src.unique_id
            left join latest_statuses on se.unique_id = latest_statuses.entry_id
            left join ingestion.staged_entry_line sel on se.unique_id = sel.entry_id
            )"""
            $"""header_ids as (
            select distinct 
                ais.stage_entry_id
            from all_in_stage ais
            {whereClauses}
            )"""
        ]
    let select = """
            se.unique_id, se.entry_date, se.description, se.source_id, se.fi_reference, se.source_file,
            se.journal_entry_header_id, latest_statuses.to_status as current_status, src.source_name,
            src.created_at as source_created, src.modified_at as source_modified"""
    let joinList =
        [
            "join header_ids h on se.unique_id = h.stage_entry_id"
            "left join ingestion.source src on se.source_id = src.unique_id"
            "left join latest_statuses on se.unique_id = latest_statuses.entry_id"
        ]
    let cteList = latestStatusCtes@multiFetchCtes
    let! headers =
        StageEntryHeader.query context (Some cteList) select (Some joinList)
            None None None sortClause parameters AnyQuantityIsAcceptable
    let headerIds = 
        headers
        |> List.map (fun x -> x|> StageEntryHeader.stageEntryHeaderId)
    if headers |> List.isEmpty then return []
    else
        let! lines = headerIds |> StageEntryLine.fetchByHeaderIdList context
        let! statusTransitions = headerIds |> StageEntryStatusTransition.fetchByHeaderIdList context
        return compileFromSubLists headers lines statusTransitions
    }
