module ModelOrchestrator.StageEntryOrchestration

open System
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model
open Model.DataIngestion
open Model.DataIngestion.BaseStageEntry
open Model.Ledger.AccountComponent
open Model.Ledger.JournalEntryComponent
open ModelOrchestrator.FetchFilters
open ModelOrchestrator.JournalEntries
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Model.StageDataClassification.StageDataClassificationComponent
open Model.DataIngestion.StageEntryComponent

type StageEntry =
    private {
        stageEntryHeader: StageEntryHeader.StageEntryHeader
        seLines: StageEntryLine.StageEntryLine list
        statusTransitions: StageEntryStatusTransition.StageEntryStatusTransition list
    }

type AccountClassificationResult = {
    runId: ClassificationRunId
    classificationResults: ClassificationResult list
    stagedEntries: StageEntry list
}

type AccountValidationType =
    | AllowNone
    | DisallowNone
    

let stageEntryHeader se = se.stageEntryHeader
let seLines se = se.seLines
let statusTransitions se = se.statusTransitions

let private sumLinesByType
    (debitOrCredit: JournalEntryLineType)
    (lines: StageEntryLine.StageEntryLine list)
    : Result<Money, AppError> =
    lines
    |> List.filter(fun x -> x |> StageEntryLine.lineType = debitOrCredit)
    |> List.map(fun x -> x |> StageEntryLine.amount) |> Money.sumList
    
let private confirmAmountEquality (lines: StageEntryLine.StageEntryLine list) : Result<unit, AppError> =
    result {
        let! totalDebits = lines |> sumLinesByType Debit
        let! totalCredits = lines |> sumLinesByType Credit
        return!
            if totalCredits = totalDebits then
                Ok()
            else
                Error(IngestionStageEntryDebitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
    }

let private confirmLineCount (lines: StageEntryLine.StageEntryLine list) : Result<unit, AppError> =
    if lines |> List.length < 2 then
        Error(IngestionStageEntryInsufficientLines(lines |> List.length))
    else
        Ok()

let private confirmLinesAreAllPositive (lines: StageEntryLine.StageEntryLine list) : Result<unit, AppError> =
    let checkedLines =
        lines
        |> List.map(fun x ->
            let amountDec = x |> StageEntryLine.amount |> Money.amount
            if amountDec <= 0M then Error(IngestionStageLineNonPositiveAmount(amountDec))
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
    : Result<unit, AppError> =
    let checkedLines =
        lines
        |> List.map(fun x ->
            let accountIdOption = x |> StageEntryLine.accountId
            match accountValidationType, accountIdOption with
            | AllowNone, None -> Ok ()
            | DisallowNone, None ->
                Error (IngestionNoneAccount (x |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value))
            | _, Some accountId ->
                let accountUuid = accountId |> AccountId.value
                let lookupResult =
                    accountUuid |> LookupCache.accountIdToCode.fetch context // we don't need the code; we just check that the ID is in the DB this way 
                match lookupResult with
                | Ok _ -> Ok ()
                | Error(DalResultantRowsDidntMatchExpectation (_, 0)) ->
                    Error (AccountIdDoesntMatch accountUuid)
                | Error e -> Error e
            )
        |> convertListOfResultsToResultsList
    match checkedLines with
    | Error e -> Error e
    | Ok _ -> Ok ()

let private confirmLines
    (context: Context.Context)
    (accountCodeValidationType: AccountValidationType)
    (lines: StageEntryLine.StageEntryLine list)
    : Result<unit, AppError> =
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
    : Result<unit, AppError> =
    result {
        do! stageEntry.seLines |> confirmLines context accountCodeValidationType
        do! stageEntry.statusTransitions |> confirmValidTransitions
        do! if stageEntry.statusTransitions |> List.isEmpty then Error IngestionStatusTransitionList else Ok ()
    }

let createStageEntry
    (context: Context.Context)
    (header: StageEntryHeader.StageEntryHeader)
    (lines: StageEntryLine.StageEntryLine list)
    (transitions: StageEntryStatusTransition.StageEntryStatusTransition list)
    : Result<StageEntry, AppError> =
    result {
        let stageEntry = {
            stageEntryHeader = header
            seLines = lines
            statusTransitions = transitions }
        do! stageEntry |> confirmStageEntryCompositeIsValid context AllowNone
        return stageEntry
    }
    
let private constructSetFromRaw
    (context: Context.Context)
    (sourceFile: SourceFile)
    (rawRows: BaseStageRawRow list)
    : Result<StageEntry list, AppError> =
    rawRows
    |> List.groupBy(_.baseStageEntryGroupId)
    |> List.map(fun (baseStageEntryGroupId, rawRowsAtGroupId) ->
        let distinctHeadersList =
            rawRowsAtGroupId
            |> List.groupBy(fun x -> x.entryDate, x.description, x.fiSource, x.fiReference)
        if distinctHeadersList |> List.length > 1
        then Error (IngestionBaseStageGroupIdDistinctDataViolation (baseStageEntryGroupId |> BaseStageEntryGroupId.value))
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
        )
    |> convertListOfResultsToResultsList

let private fetchAllLinesByHeaders
    (context: Context.Context)
    (headers: StageEntryHeader.StageEntryHeader list)
    : Result<StageEntryLine.StageEntryLine list, AppError> =
    headers
    |> List.map(fun x -> x |> StageEntryHeader.stageEntryHeaderId)
    |> StageEntryLine.fetchByHeaderIdList context

let private fetchAllTransitionsByHeaders
    (context: Context.Context)
    (headers: StageEntryHeader.StageEntryHeader list)
    : Result<StageEntryStatusTransition.StageEntryStatusTransition list, AppError> =
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
    : Result<StageEntry list, AppError> =
    result {
        let! headers = sourceFile |> StageEntryHeader.fetchBySourceFile context statusFilter
        if headers |> List.isEmpty then return [] else
        let! lines = headers |> fetchAllLinesByHeaders context
        let! statuses = headers |> fetchAllTransitionsByHeaders context
        return compileFromSubLists headers lines statuses
    }

let fetchAllForPosting
    (context: Context.Context)
    : Result<StageEntry list, AppError> =
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


let private fetchByStatusList
    (context: Context.Context)
    (statuses: StagedEntryStatus list)
    : Result<StageEntry list, AppError> =
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
    : Result<StageEntry, AppError> =
    result {
        let! header = headerId |> StageEntryHeader.fetchById context
        let! lines = headerId |> StageEntryLine.fetchByHeaderId context
        let! statusTransitions = headerId |> StageEntryStatusTransition.fetchByHeaderId context
        return { stageEntryHeader = header
                 seLines = lines
                 statusTransitions = statusTransitions }
    }

let createNewSource
    (context: Context.Context)
    (name: JournalRefFinancialInstitution)
    : Result<IngestionSource.IngestionSource, AppError> =
    result {
        let instant = context |> Context.getInitiationInstant
        let uuid = IngestionSourceId.create()
        let newSource = IngestionSource.create uuid name instant instant
        do! newSource |> IngestionSource.persist context
        return newSource }

let deduplicateStagedEntries
    (context: Context.Context)
    : Result<StageEntry list, AppError> =
    result {
        let! duplicateHeaders = StageEntryHeader.fetchDuplicates context
        let toStatus = StagedEntryStatus.Duplicate
        let mechanism = StageStatusChangeMechanism.Deduplicator
        let! _ = duplicateHeaders
                 |> List.map(fun dup ->
                     dup
                     |> StageEntryHeader.stageEntryHeaderId
                     |> StageEntryHeader.updateHeaderStatus context toStatus mechanism
                     )
                 |> convertListOfResultsToResultsList
        return! [ StagedEntryStatus.Ingested ] |> fetchByStatusList context
    }

let classifyAccounts
    (context: Context.Context)
    : Result<AccountClassificationResult, AppError> =
    result {
        let rosterStatuses = [ Ingested; StagedEntryStatus.NoMatch; Conflict ]
        let! roster = rosterStatuses |> fetchByStatusList context
        let isFullyAssigned (entry: StageEntry) : bool =
            entry |> seLines |> List.forall (fun line -> line |> StageEntryLine.accountId |> Option.isSome)
        let (matchCandidates: MatchCandidate list) =
            roster
            |> List.collect (fun entry ->
                let header = entry.stageEntryHeader
                entry
                |> seLines
                |> List.filter (fun line -> line |> StageEntryLine.accountId |> Option.isNone)
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
            |> ClassificationOrchestration.classifyMatchCandidatesAndRecordMatches context AccountClaimant
        let classificationResults = classificationRun.results
        let winningAccountId (outcome: ClassifierOutcome) : AccountId option =
            match outcome with
            | OneMatch prioritizedMatch -> prioritizedMatch.accountId
            | ManyMatchesClearWinner (winner, _) -> winner.accountId
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
        let! rosterAfterWrites = rosterStatuses |> fetchByStatusList context
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

let ingestRawToStage
    (context: Context.Context)
    (sourceFile: SourceFile)
    (rawRows: BaseStageRawRow list)
    : Result<StageEntry list, AppError> =
    result {
        let! entries = rawRows |> constructSetFromRaw context sourceFile
        let! _ =
            entries
            |> List.map(fun e ->
                e
                |> stageEntryHeader
                |> StageEntryHeader.persist context Ingested StageIngestion )
            |> convertListOfResultsToResultsList
        let! _ =
            entries
            |> List.collect seLines
            |> List.map(fun l -> l |> StageEntryLine.persist context )
            |> convertListOfResultsToResultsList
        return entries
    }

let private confirmUpdateLinesMatchUpdateHeader
    (context: Context.Context)
    (headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates)
    (lineUpdates: StageEntryLine.StageEntryLineFieldUpdates list)
    : Result<unit, AppError> =
    lineUpdates
    |> List.map (fun lineUpdate ->
        result {
            let! lineHeaderIdToCompare =
                lineUpdate.lineIdToUpdate
                |> StageEntryLine.fetchById context
                |> Result.map StageEntryLine.stageEntryHeaderId
            let headerId = headerUpdates.headerIdToUpdate
            return!
                if lineHeaderIdToCompare = headerId then Ok ()
                else
                    let headerUuid = headerId |> StageEntryHeaderId.value
                    let lineUuid = lineUpdate.lineIdToUpdate |> StageEntryLineId.value
                    Error (IngestionUpdateStageEntryLinesMustMatchHeader(headerUuid, lineUuid))
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
    
let updateStageEntry
    (context: Context.Context)
    (headerUpdates: StageEntryHeader.StageEntryHeaderFieldUpdates)
    (lineUpdates: StageEntryLine.StageEntryLineFieldUpdates list)
    : Result<StageEntry, AppError> =
    result {
        let shouldUpdateHeader = headerUpdates |> isThereAHeaderUpdate
        let shouldUpdateLines = lineUpdates |> isThereALineUpdate
        do! if shouldUpdateHeader = false && shouldUpdateLines = false
            then (Error IngestionUpdateStageEntryNoOp)
            else Ok ()
        do! confirmUpdateLinesMatchUpdateHeader context headerUpdates lineUpdates
        do! if shouldUpdateLines
            then
                lineUpdates
                |> List.map(fun lineUpdate -> lineUpdate |> StageEntryLine.update context)
                |> convertListOfResultsToResultsList
                |> Result.map ignore
            else Ok ()
        do! if shouldUpdateHeader then headerUpdates |> StageEntryHeader.update context |> Result.map ignore
            else Ok ()
        // now that we updated everything, we should read it back and ensure it still meets composite requirements
        let! fetched = headerUpdates.headerIdToUpdate |> fetchByStageEntryHeaderId context
        do! fetched |> confirmStageEntryCompositeIsValid context AllowNone
        return fetched
    }

let private isSameLine
    (stageEntryLine: StageEntryLine.StageEntryLine)
    (journalEntryLine: Model.Ledger.JournalEntryLine.JournalEntryLine)
    : bool =
    let stageAccountId = stageEntryLine |> StageEntryLine.accountId
    let journalAccountId = journalEntryLine |> Model.Ledger.JournalEntryLine.accountId
    let stageAmount = stageEntryLine |> StageEntryLine.amount |> Money.amount
    let journalAmount = journalEntryLine |> Model.Ledger.JournalEntryLine.amount |> Money.amount
    let stageLineType = stageEntryLine |> StageEntryLine.lineType
    let journalLineType = journalEntryLine |> Model.Ledger.JournalEntryLine.lineType
    stageAccountId = Some journalAccountId && stageAmount = journalAmount && stageLineType = journalLineType

/// pairStageLinesToJournalEntryLines matches on account, line type, and amount rather than trusting the two lists to
/// arrive in the same order. A matched journal entry line leaves the pool, so two identical staged lines still pair
/// one to one.
let rec private pairStageLinesToJournalEntryLines
    (pairs: (StageEntryLine.StageEntryLine * Model.Ledger.JournalEntryLine.JournalEntryLine) list)
    (unpairedJournalEntryLines: Model.Ledger.JournalEntryLine.JournalEntryLine list)
    (stageEntryLines: StageEntryLine.StageEntryLine list)
    : Result<(StageEntryLine.StageEntryLine * Model.Ledger.JournalEntryLine.JournalEntryLine) list, AppError> =
    match stageEntryLines with
    | [] -> Ok (pairs |> List.rev)
    | stageEntryLine :: remainingStageEntryLines ->
        match unpairedJournalEntryLines |> List.tryFind (isSameLine stageEntryLine) with
        | None ->
            let uuid = stageEntryLine |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value
            Error (IngestionStageEntryLineNoMatchingJournalEntryLine uuid)
        | Some journalEntryLine ->
            let pairedId = journalEntryLine |> Model.Ledger.JournalEntryLine.journalEntryLineId
            let stillUnpaired =
                unpairedJournalEntryLines
                |> List.filter (fun x -> (x |> Model.Ledger.JournalEntryLine.journalEntryLineId) <> pairedId)
            remainingStageEntryLines
            |> pairStageLinesToJournalEntryLines ((stageEntryLine, journalEntryLine) :: pairs) stillUnpaired

let postStageEntry
    (context: Context.Context)
    (jeHeaderSource: JournalEntrySource option)
    (stageEntry: StageEntry)
    : Result<unit, AppError> =
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
                        | None -> Error (IngestionNoneAccount (
                                line |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value))
                        | Some x -> Ok x
                    let amount = line |> StageEntryLine.amount
                    let lineType = line |> StageEntryLine.lineType
                    let memo = line |> StageEntryLine.memo
                    return accountId, amount, lineType, memo
                } )
            |> convertListOfResultsToResultsList
        let! journalEntry =
            JournalEntry.constructNewAndPersist
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
                |> JournalEntry.header
                |> Model.Ledger.JournalEntryHeader.journalEntryHeaderId
                |> Some
                |> FieldUpdate.SetTo
            statusUpdate = FieldUpdate.NoChange }
        do! headerUpdates |> StageEntryHeader.update context |> Result.map ignore
        let! pairedLines =
            stageEntry.seLines
            |> pairStageLinesToJournalEntryLines [] (journalEntry |> JournalEntry.jeLines)
        let! _ =
            pairedLines
            |> List.map (fun (stageEntryLine, journalEntryLine) ->
                let journalEntryLineIdUpdate =
                    journalEntryLine
                    |> Model.Ledger.JournalEntryLine.journalEntryLineId
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
    : Result<unit, AppError> =
    result {
        let! stageEntries = fetchAllForPosting context
        if stageEntries |> List.isEmpty then return () else
        let! jeHeaderSource =
            Some "Data ingestion import"
            |> convertOptionToDesiredTypeWithFallibleConverter JournalEntrySource.create
        // check the lines one last time just to be sure we're not trying to post any records whose accounts aren't set
        do! stageEntries
            |> List.map(fun stageEntry ->
                stageEntry.seLines |> confirmLinesAccountCodes context DisallowNone
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
    : Result<StageEntry list, AppError> = result {
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
              let likeStr = $"%%{x |> JournalEntryDescription.value}%%"
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
