module InterfaceBridge.BoundaryConverters.IngestionFieldConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.CashFlowFieldConverters
open InterfaceBridge.BoundaryConverters.ClassificationFieldConverters
open InterfaceBridge.BoundaryConverters.OrchestrationConverters
open InterfaceBridge.InterfaceContracts.ClassificationContracts
open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.DataIngestion.BaseStageEntry
open Business.FinancialServices.Classification
open Business.FinancialServices.DataIngestion.StageEntryStatusTransition
open Business.FinancialServices.Ledger.JournalEntryComponent
open ModelOrchestrator.FetchFilters
open ModelOrchestrator.StageEntryOrchestration
open App.Utility.AppError
open App.Utility.FieldUpdate.FieldUpdate
open App.Utility.Result
open Business.FinancialServices.Classification.StageDataClassificationComponent

let ``convert [StageEntryStatusTransition] to [StageEntryStatusTransitionReturn]``
    (model: StageEntryStatusTransition)
    : StageEntryStatusTransitionReturn =
    let stageEntryStatusTransitionId =
        model |> StageEntryStatusTransition.stageEntryStatusTransitionId |> StageEntryStatusTransitionId.value
    let stageEntryHeaderId = model |> StageEntryStatusTransition.stageEntryHeaderId |> StageEntryHeaderId.value
    let fromStatus = model |> StageEntryStatusTransition.fromStatus |> Option.map StagedEntryStatus.toString
    let toStatus = model |> StageEntryStatusTransition.toStatus |> StagedEntryStatus.toString
    let instant = model |> StageEntryStatusTransition.instant
    let stageStatusChangeMechanism =
        model |> StageEntryStatusTransition.stageStatusChangeMechanism |> StageStatusChangeMechanism.toString
    {
        stageEntryStatusTransitionId = stageEntryStatusTransitionId
        stageEntryHeaderId = stageEntryHeaderId
        fromStatus = fromStatus
        toStatus = toStatus
        instant = instant
        stageStatusChangeMechanism = stageStatusChangeMechanism } 

let ``convert [StageEntryStatusTransition list] to [StageEntryStatusTransitionReturn list]``
    (input: StageEntryStatusTransition list)
    : StageEntryStatusTransitionReturn list =
    input
    |> List.map(fun x -> x |> ``convert [StageEntryStatusTransition] to [StageEntryStatusTransitionReturn]``)

let ``convert [StageEntryHeader] to [StageEntryHeaderReturn]``
    (model: StageEntryHeader.StageEntryHeader)
    : StageEntryHeaderReturn =
    let sourceFile = model |> StageEntryHeader.sourceFile |> SourceFile.value
    let stageEntryHeaderId = model |> StageEntryHeader.stageEntryHeaderId |> StageEntryHeaderId.value
    let entryDate = model |> StageEntryHeader.entryDate
    let description = model |> StageEntryHeader.description |> JournalEntryDescription.value
    let ingestionSource = model |> StageEntryHeader.ingestionSource |> IngestionSource.name |> JournalRefFinancialInstitution.value
    let fiReference = model |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value
    let status = model |> StageEntryHeader.currentStatus |> Option.map StagedEntryStatus.toString
    let journalEntryHeaderId =
        model |> StageEntryHeader.journalEntryHeaderId |> Option.map JournalEntryHeaderId.value
    {   sourceFile = sourceFile
        stageEntryHeaderId = stageEntryHeaderId
        entryDate = entryDate
        description = description
        ingestionSource = ingestionSource
        fiReference = fiReference
        journalEntryHeaderId = journalEntryHeaderId
        status = status }

let ``convert [StageEntryLine] to [StageEntryLineReturn]``
    (context: Context.Context)
    (model: StageEntryLine.StageEntryLine)
    : Result<StageEntryLineReturn, AppError> = result {
    let stageEntryLineId = model |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value
    let stageEntryHeaderId = model |> StageEntryLine.stageEntryHeaderId |> StageEntryHeaderId.value
    let amount = model |> StageEntryLine.amount |> Money.amount
    let lineType = model |> StageEntryLine.lineType |> JournalEntryLineType.toString
    let! accountCode =
        model
        |> StageEntryLine.accountId
        |> ``convert AccountId Option to AccountCodeString Option`` context
    let! accountName =
        model
        |> StageEntryLine.accountId
        |> ``convert [AccountId option] to [AccountName string option]`` context
    let memo = model |> StageEntryLine.memo |> Option.map JournalEntryLineMemo.value
    let journalEntryLineId = model |> StageEntryLine.journalEntryLineId |> Option.map JournalEntryLineId.value
    return {    stageEntryLineId = stageEntryLineId
                stageEntryHeaderId = stageEntryHeaderId
                amount = amount
                lineType = lineType
                accountCode = accountCode
                accountName = accountName
                memo = memo
                journalEntryLineId = journalEntryLineId } }

let ``convert [StageEntryLine list] to [StageEntryLineReturn list]``
    (context: Context.Context)
    (input: StageEntryLine.StageEntryLine list)
    : Result<StageEntryLineReturn list, AppError> =
    input
    |> List.map(fun x -> x |> ``convert [StageEntryLine] to [StageEntryLineReturn]`` context)
    |> convertListOfResultsToResultsList

let ``convert [StageEntry] to [StageEntryReturn]``
    (context: Context.Context)
    (stageEntry: StageEntry)
    : Result<StageEntryReturn, AppError> = result {
    let! lines =
        stageEntry
        |> seLines
        |> ``convert [StageEntryLine list] to [StageEntryLineReturn list]`` context
    let stageEntryHeader =
        stageEntry
        |> stageEntryHeader
        |> ``convert [StageEntryHeader] to [StageEntryHeaderReturn]``
    let statusTransitions =
        stageEntry
        |> statusTransitions
        |> ``convert [StageEntryStatusTransition list] to [StageEntryStatusTransitionReturn list]``
    return {    stageEntryHeader = stageEntryHeader
                lines = lines
                statusTransitions = statusTransitions } }

let ``convert [StageEntry list] to [StageEntryReturn list]``
    (context: Context.Context)
    (stageEntries: StageEntry list)
    : Result<StageEntryReturn list, AppError> =
    stageEntries
    |> List.map(fun x -> x |> ``convert [StageEntry] to [StageEntryReturn]`` context)
    |> convertListOfResultsToResultsList

let ``convert [AccountClassificationResult] to [AccountClassificationResultReturn]``
    (context: Context.Context)
    (classificationResult: AccountClassificationResult)
    : Result<AccountClassificationResultReturn, AppError> = result {
    let! classificationResults =
        classificationResult.classificationResults
        |> ``convert [ClassificationResult list] to [ClassificationResultReturn list]`` context
    let! stagedEntries =
        classificationResult.stagedEntries
        |> ``convert [StageEntry list] to [StageEntryReturn list]`` context
    let sortedResults =
        classificationResults
        |> List.sortBy (fun r -> r.candidate.stageEntryHeaderId, r.candidate.stageEntryLineId)
    let sortedEntries =
        stagedEntries
        |> List.sortBy (fun e -> e.stageEntryHeader.entryDate, e.stageEntryHeader.stageEntryHeaderId)
    return {    runId = classificationResult.runId |> ClassificationRunId.value
                classificationResults = sortedResults
                stagedEntries = sortedEntries } }

let ``convert [IngestionSource] to [IngestionSourceReturn]``
    (source: IngestionSource.IngestionSource)
    : IngestionSourceReturn = {
        ingestionSourceId = source |> IngestionSource.ingestionSourceId |> IngestionSourceId.value
        name = source |> IngestionSource.name |> JournalRefFinancialInstitution.value
        createdAt = source |> IngestionSource.createdAt
        modifiedAt = source |> IngestionSource.modifiedAt
    }

let ``convert [UpdateStageEntryLineInput] to [StageEntryLineFieldUpdates]``
    (context: Context.Context)
    (line: UpdateStageEntryLineInput)
    : Result<StageEntryLine.StageEntryLineFieldUpdates, AppError> =
    result {
        let lineIdToUpdate = line.stageEntryLineId |> StageEntryLineId.fromGuid
        let! amountUpdate = line.amount |> convertFieldUpdateToNewTypeFallible Money.fromDecimal
        let! entryTypeUpdate = line.lineType |> convertFieldUpdateToNewTypeFallible JournalEntryLineType.fromString
        let! accountIdUpdate =
            line.accountCode
            |> convertFieldUpdateToNewTypeFallible (``convert AccountCodeString Option to AccountId Option`` context) 
        let! memoUpdate = line.memo |> convertFieldUpdateOptionToNewTypeOptionFallible JournalEntryLineMemo.create
        return {
          lineIdToUpdate = lineIdToUpdate
          amountUpdate = amountUpdate
          entryTypeUpdate = entryTypeUpdate
          accountIdUpdate = accountIdUpdate
          memoUpdate = memoUpdate
          journalEntryLineIdUpdate = App.Utility.FieldUpdate.NoChange } }

let ``convert [UpdateStageEntryLineInput list] to [StageEntryLineFieldUpdates list]``
    (context: Context.Context)
    (lines: UpdateStageEntryLineInput list)
    : Result<StageEntryLine.StageEntryLineFieldUpdates list, AppError> =
    lines
    |> List.map (``convert [UpdateStageEntryLineInput] to [StageEntryLineFieldUpdates]`` context)
    |> convertListOfResultsToResultsList

let ``convert [BaseStageRawRowInput] to [BaseStageRawRow]``
    (context: Context.Context)
    (rawInputRow: BaseStageRawRowInput)
    : Result<BaseStageRawRow, AppError> =
    result {
        let! baseStageEntryGroupId = rawInputRow.baseStageEntryGroupId |> BaseStageEntryGroupId.create
        let entryDate = rawInputRow.entryDate
        let! description = rawInputRow.description |> JournalEntryDescription.create
        let! fiSource = rawInputRow.fiSource |> JournalRefFinancialInstitution.create
        let! fiReference = rawInputRow.fiReference |> JournalExternalReferenceText.create
        let! amount = rawInputRow.amount |> Money.fromDecimal
        let! entryType = rawInputRow.entryType |> JournalEntryLineType.fromString
        let! accountId = rawInputRow.accountCode |> ``convert AccountCodeString Option to AccountId Option`` context
        let! memo = rawInputRow.memo |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        return {
            baseStageEntryGroupId = baseStageEntryGroupId
            entryDate = entryDate
            description = description
            fiSource = fiSource
            fiReference = fiReference
            amount = amount
            entryType = entryType
            accountId = accountId
            memo = memo } }
    
let ``convert [BaseStageRawRowInput list] to [BaseStageRawRow list]``
    (context: Context.Context)
    (rawInputRows: BaseStageRawRowInput list)
    : Result<BaseStageRawRow list, AppError> =
    rawInputRows
    |> List.map (``convert [BaseStageRawRowInput] to [BaseStageRawRow]`` context)
    |> convertListOfResultsToResultsList

let ``convert [StageEntryFetchFilterInput] to [StageEntryFetchFilter]``
    (context: Context.Context)
    (filterInput: StageEntryFetchFilterInput)
    : Result<StageEntryFetchFilter, AppError> = result {
        let stageEntryHeaderId = filterInput.stageEntryHeaderId |> Option.map StageEntryHeaderId.fromGuid
        let! sourceFile = filterInput.sourceFile |> convertOptionToDesiredTypeWithFallibleConverter SourceFile.create
        let! temporalFilter =
            filterInput.temporalFilter
            |> ``convert TemporalFilterInput Option To TemporalFilter Option`` context
        let! description =
            filterInput.description |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryDescription.create
        let! ingestionSource =
            filterInput.ingestionSource
            |> convertOptionToDesiredTypeWithFallibleConverter JournalRefFinancialInstitution.create
        let! fiReference =
            filterInput.fiReference
            |> convertOptionToDesiredTypeWithFallibleConverter JournalExternalReferenceText.create
        let! status = filterInput.status |> convertOptionToDesiredTypeWithFallibleConverter StagedEntryStatus.fromString
        let stageEntryLineId = filterInput.stageEntryLineId |> Option.map StageEntryLineId.fromGuid
        let! amount = filterInput.amount |> convertOptionToDesiredTypeWithFallibleConverter Money.fromDecimal
        let! lineType = filterInput.lineType |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineType.fromString
        let! accountId = filterInput.accountCode |> ``convert AccountCodeString Option to AccountId Option`` context
        let! memo = filterInput.memo |> convertOptionToDesiredTypeWithFallibleConverter JournalEntryLineMemo.create
        let journalEntryHeaderId = filterInput.journalEntryHeaderId |> Option.map JournalEntryHeaderId.fromGuid
        let journalEntryLineId = filterInput.journalEntryLineId |> Option.map JournalEntryLineId.fromGuid
        return {
            stageEntryHeaderId = stageEntryHeaderId
            sourceFile = sourceFile
            temporalFilter = temporalFilter
            description = description
            ingestionSource = ingestionSource
            fiReference = fiReference
            status = status
            stageEntryLineId = stageEntryLineId
            amount = amount
            lineType = lineType
            accountId = accountId
            memo = memo
            journalEntryHeaderId = journalEntryHeaderId
            journalEntryLineId = journalEntryLineId
        } }
