module ModelOrchestrator.StageEntryOrchestration


open Model
open Model.DataIngestion
open Model.DataIngestion.BaseStageRaw
open Model.DataIngestion.IngestionSource
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.ResultHelper

type StageEntry =
    private {
        stageEntryHeader: StageEntryHeader
        lines: StageEntryLine list
        statusTransitions: StageEntryStatusTransition list
    }

let stageEntryHeader se = se.stageEntryHeader
let lines se = se.lines
let statusTransitions se = se.statusTransitions

let private sumLinesByType
    (debitOrCredit: JournalEntryLineType)
    (lines: StageEntryLine list)
    : Result<Money, AppError> =
    lines
    |> List.filter(fun x -> x |> lineType = debitOrCredit)
    |> List.map(fun x -> x |> amount) |> Money.sumList
    
let private confirmAmountEquality (lines: StageEntryLine list) : Result<unit, AppError> =
    result {
        let! totalDebits = lines |> sumLinesByType Debit
        let! totalCredits = lines |> sumLinesByType Credit
        return!
            if totalCredits = totalDebits then
                Ok()
            else
                Error(IngestionStageEntryDebitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
    }

let private confirmLineCount (lines: StageEntryLine list) : Result<unit, AppError> =
    if lines |> List.length < 2 then
        Error(IngestionStageEntryInsufficientLines(lines |> List.length))
    else
        Ok()

let private confirmLinesAreAllPositive (lines: StageEntryLine list) : Result<unit, AppError> =
    let checkedLines =
        lines
        |> List.map(fun x ->
            let amountDec = x |> amount |> Money.amount
            if amountDec <= 0M then Error(IngestionStageLineNonPositiveAmount(amountDec))
            else Ok ()
            )
        |> convertListOfResultsToResultsList
    match checkedLines with
    | Error e -> Error e
    | Ok _ -> Ok ()

let private confirmLinesAccountCodes
    (context: Context.Context)
    (lines: StageEntryLine list)
    : Result<unit, AppError> =
    let checkedLines =
        lines
        |> List.map(fun x ->
            let code = x |> accountCode
            if code |> Option.isNone then Ok ()
            else 
                let lookupResult =
                    code
                    |> Option.get
                    |> AccountCode.value
                    |> LookupCache.accountCodeToId.fetch context 
                match lookupResult with
                | Error e -> Error e
                | Ok _ -> Ok ()
            )
        |> convertListOfResultsToResultsList
    match checkedLines with
    | Error e -> Error e
    | Ok _ -> Ok ()

let private confirmLines
    (context: Context.Context)
    (lines: StageEntryLine list)
    : Result<unit, AppError> =
    result {
        do! lines |> confirmLineCount
        do! lines |> confirmAmountEquality
        do! lines |> confirmLinesAreAllPositive
        do! lines |> confirmLinesAccountCodes context // do the expensive one last
    }

let private confirmValidTransition transition =
    let fromType = transition |> fromStatus
    let toType = transition |> toStatus
    if fromType |> validTransitions |> List.contains toType then Ok ()
    else
        let fromStr = fromType |> Option.map StagedEntryStatus.toString
        let toStr = toType |> StagedEntryStatus.toString
        Error (IngestionInvalidStageStatusTransition (fromStr, toStr))

let private confirmValidTransitions transitions =
    let check =
        transitions
        |> List.map confirmValidTransition
        |> convertListOfResultsToResultsList
    match check with
    | Error e -> Error e
    | Ok _ -> Ok ()

let createStageEntry
    (context: Context.Context)
    (header: StageEntryHeader)
    (lines: StageEntryLine list)
    (transitions: StageEntryStatusTransition list)
    : Result<StageEntry, AppError> =
    result {
        do! lines |> confirmLines context
        do! transitions |> confirmValidTransitions
        return {
            stageEntryHeader = header
            lines = lines
            statusTransitions = transitions
        }
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
                            lineId stageEntryId row.amount row.entryType row.accountCode row.memo None
                        )
                let! ingestionSource = fiSource |> IngestionSource.fetchByName context
                let header =
                    StageEntryHeader.create
                        sourceFile stageEntryId entryDate description ingestionSource fiReference Ingested
                let transitionId = StageEntryStatusTransitionId.create ()
                let transition = StageEntryStatusTransition.create transitionId stageEntryId
                                      None Ingested (context |> Context.getInitiationInstant) StageIngestion
                return! createStageEntry context header lines [transition]
            }
        )
    |> convertListOfResultsToResultsList

let private fetchAllLinesByHeaders
    (context: Context.Context)
    (headers: StageEntryHeader list)
    : Result<StageEntryLine list, AppError> =
    headers
    |> List.map(fun x -> x |> StageEntryHeader.stageEntryHeaderId)
    |> StageEntryLine.fetchByHeaderIdList context

let private fetchAllTransitionsByHeaders
    (context: Context.Context)
    (headers: StageEntryHeader list)
    : Result<StageEntryStatusTransition list, AppError> =
    headers
    |> List.map(fun x -> x |> StageEntryHeader.stageEntryHeaderId)
    |> StageEntryStatusTransition.fetchByHeaderIdList context

let private compileFromSubLists
    (headers: StageEntryHeader list)
    (lines: StageEntryLine list)
    (statusTransitions: StageEntryStatusTransition list)
    : StageEntry list =
    headers
    |> List.map (fun h ->
        let headerId = h |> StageEntryHeader.stageEntryHeaderId
        let linesAtH = lines |> List.filter(fun l -> l |> StageEntryLine.stageEntryHeaderId = headerId)
        let transitionsAtH =
            statusTransitions
            |> List.filter(fun l -> l |> StageEntryStatusTransition.stageEntryHeaderId = headerId)
        { stageEntryHeader = h
          lines = linesAtH
          statusTransitions = transitionsAtH } )
    
let fetchAllByFile
    (context: Context.Context)
    (sourceFile: SourceFile)
    : Result<StageEntry list, AppError> =
    result {
        let! headers = fetchBySourceFile context sourceFile
        let! lines = headers |> fetchAllLinesByHeaders context
        let! statuses = headers |> fetchAllTransitionsByHeaders context
        return compileFromSubLists headers lines statuses
    }

let createNewSource
    (context: Context.Context)
    (name: JournalRefFinancialInstitution)
    : Result<IngestionSource, AppError> =
    result {
        let instant = context |> Context.getInitiationInstant
        let uuid = IngestionSourceId.create()
        let newSource = IngestionSource.create uuid name instant instant
        do! newSource |> IngestionSource.insertNewToDb context
        return newSource }

let deduplicateStagedEntries
    (context: Context.Context)
    : Result<unit, AppError> =
    result {
        let! duplicateHeaders = fetchDuplicates context
        // update the status on the header record first
        let! _ = duplicateHeaders
                 |> List.map(fun dup ->
                     dup |> StageEntryHeader.stageEntryHeaderId |> StageEntryHeader.updateStatus context Duplicate
                     )
                 |> convertListOfResultsToResultsList
        // now add an audit row entry
        let! statusTransitions = duplicateHeaders |> fetchAllTransitionsByHeaders context
        let headerIdAndLatestTran =
            duplicateHeaders
            |> List.map(fun dup ->
                let headerId = dup |> StageEntryHeader.stageEntryHeaderId
                let mostRecentTransition =
                    statusTransitions
                    |> List.filter(fun s -> s |> StageEntryStatusTransition.stageEntryHeaderId = headerId)
                    |> List.sortByDescending(fun s -> s |> StageEntryStatusTransition.instant)
                    |> List.head
                headerId, mostRecentTransition
                )
        let! _ =
            headerIdAndLatestTran
            |> List.map(fun (headerId, mostRecentTran) ->
                let fromStatus = mostRecentTran |> StageEntryStatusTransition.toStatus |> Some
                let newTransitionId = StageEntryStatusTransitionId.create()
                let instant = context |> Context.getInitiationInstant
                let toStatus = StagedEntryStatus.Duplicate
                let mechanism = StageStatusChangeMechanism.Deduplicator
                let newTransition =
                    StageEntryStatusTransition.create newTransitionId headerId
                        fromStatus toStatus instant mechanism
                result {
                    do! newTransition |> confirmValidTransition
                    return! newTransition |> StageEntryStatusTransition.insertNewToDb context
                }
                )
            |> convertListOfResultsToResultsList
        Ok ()
    }

let ingestRawToStageThenDedupAndClassify
    (context: Context.Context)
    (sourceFile: SourceFile)
    (rawRows: BaseStageRawRow list)
    : Result<StageEntry list, AppError> =
    result {
        let! entries = rawRows |> constructSetFromRaw context sourceFile
        let! _ =
            entries
            |> List.map(fun e -> e |> stageEntryHeader  |> StageEntryHeader.insertNewToDb context )
            |> convertListOfResultsToResultsList
        let! _ =
            entries
            |> List.collect lines
            |> List.map(fun l -> l |> StageEntryLine.insertNewToDb context )
            |> convertListOfResultsToResultsList
        let! _ =
            entries
            |> List.collect statusTransitions
            |> List.map(fun l -> l |> StageEntryStatusTransition.insertNewToDb context )
            |> convertListOfResultsToResultsList
        do! deduplicateStagedEntries context
        return! sourceFile |> fetchAllByFile context 
    }
