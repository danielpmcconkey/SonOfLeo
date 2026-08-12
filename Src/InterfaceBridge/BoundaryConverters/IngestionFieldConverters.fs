module InterfaceBridge.BoundaryConverters.IngestionFieldConverters

open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.StageEntryOrchestration

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
    (model: StageEntryHeader)
    : StageEntryHeaderReturn =
    let sourceFile = model |> StageEntryHeader.sourceFile |> SourceFile.value
    let stageEntryHeaderId = model |> StageEntryHeader.stageEntryHeaderId |> StageEntryHeaderId.value
    let entryDate = model |> StageEntryHeader.entryDate
    let description = model |> StageEntryHeader.description |> JournalEntryDescription.value
    let ingestionSource = model |> StageEntryHeader.ingestionSource |> IngestionSource.name |> JournalRefFinancialInstitution.value
    let fiReference = model |> StageEntryHeader.fiReference |> JournalExternalReferenceText.value
    let status = model |> StageEntryHeader.status |> StagedEntryStatus.toString
    {   sourceFile = sourceFile
        stageEntryHeaderId = stageEntryHeaderId
        entryDate = entryDate
        description = description
        ingestionSource = ingestionSource
        fiReference = fiReference
        status = status }

let ``convert [StageEntryLine] to [StageEntryLineReturn]``
    (model: StageEntryLine)
    : StageEntryLineReturn =
    let stageEntryLineId = model |> StageEntryLine.stageEntryLineId |> StageEntryLineId.value
    let stageEntryHeaderId = model |> StageEntryLine.stageEntryHeaderId |> StageEntryHeaderId.value
    let amount = model |> StageEntryLine.amount |> Money.amount
    let lineType = model |> StageEntryLine.lineType |> JournalEntryLineType.toString
    let accountCode = model |> StageEntryLine.accountCode |> Option.map AccountCode.value
    let memo = model |> StageEntryLine.memo |> Option.map JournalEntryLineMemo.value
    let classificationRuleId = model |> StageEntryLine.classificationRuleId |> Option.map ClassificationRuleId.value
    {   stageEntryLineId = stageEntryLineId
        stageEntryHeaderId = stageEntryHeaderId
        amount = amount
        lineType = lineType
        accountCode = accountCode
        memo = memo 
        classificationRuleId = classificationRuleId }

let ``convert [StageEntryLine list] to [StageEntryLineReturn list]``
    (input: StageEntryLine list)
    : StageEntryLineReturn list =
    input
    |> List.map(fun x -> x |> ``convert [StageEntryLine] to [StageEntryLineReturn]``)

let ``convert [StageEntry] to [StageEntryReturn]``
    (stageEntry: StageEntry)
    : StageEntryReturn =
    let lines =
        stageEntry
        |> lines
        |> ``convert [StageEntryLine list] to [StageEntryLineReturn list]``
    let stageEntryHeader =
        stageEntry
        |> stageEntryHeader
        |> ``convert [StageEntryHeader] to [StageEntryHeaderReturn]``
    let statusTransitions =
        stageEntry
        |> statusTransitions
        |> ``convert [StageEntryStatusTransition list] to [StageEntryStatusTransitionReturn list]``
    {   stageEntryHeader = stageEntryHeader
        lines = lines
        statusTransitions = statusTransitions }

let ``convert [StageEntry list] to [StageEntryReturn list]``
    (stageEntries: StageEntry list)
    : StageEntryReturn list =
    stageEntries
    |> List.map(fun x -> x |> ``convert [StageEntry] to [StageEntryReturn]``)
