module InterfaceBridge.BoundaryConverters.IngestionFieldConverters

open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model
open Model.DataIngestion
open Model.DataIngestion.Classification
open Model.DataIngestion.Classification.ClassificationRule
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.StageEntryOrchestration
open Utilities.AppError
open Utilities.ResultHelper

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

let ``convert [FieldMatch] to [FieldMatchContract]``
    (fieldMatch: FieldMatch)
    : FieldMatchContract =
    match fieldMatch with
    | Source pattern -> FieldMatchContract.Source (pattern |> StringSearchPattern.value)
    | Description pattern -> FieldMatchContract.Description (pattern |> StringSearchPattern.value)
    | Memo pattern -> FieldMatchContract.Memo (pattern |> StringSearchPattern.value)
    | LineType pattern -> FieldMatchContract.LineType (pattern |> JournalEntryLineType.toString)
    | Amount pattern ->
        //let moneySearchPattern = FieldMatchContract.Amount
        let numericSearchOperator = pattern.numericSearchOperator |> NumericSearchOperator.toString
        let amount = pattern.amount |> Money.amount
        FieldMatchContract.Amount {
            numericSearchOperator = numericSearchOperator
            amount = amount
        }

let ``convert [FieldMatchChain] to [FieldMatchChainContract]``
    (fieldMatchChain: FieldMatchChain)
    : FieldMatchChainContract =
    let chain =
        fieldMatchChain |> FieldMatchChain.chain
        |> List.map(fun c -> c |> ``convert [FieldMatch] to [FieldMatchContract]``)
    { chain = chain }

let ``convert [ClassificationRuleGroup] to [ClassificationRuleGroupContract]``
    (ruleGroup: ClassificationRuleGroup)
    : ClassificationRuleGroupContract  =
    let connector = ruleGroup |> ClassificationRuleGroup.connector |> ClassificationGroupConnector.toString
    let chainOne =
        ruleGroup |> ClassificationRuleGroup.chainOne |> ``convert [FieldMatchChain] to [FieldMatchChainContract]``
    let chainTwo =
        ruleGroup
        |> ClassificationRuleGroup.chainTwo
        |> Option.map ``convert [FieldMatchChain] to [FieldMatchChainContract]``
    {   connector = connector
        chainOne = chainOne
        chainTwo = chainTwo }
    
let ``convert [ClassificationRuleGroup list] to [ClassificationRuleGroupContract list]``
    (ruleGroups: ClassificationRuleGroup list)
    : ClassificationRuleGroupContract list =
    ruleGroups
    |> List.map(fun x -> x |> ``convert [ClassificationRuleGroup] to [ClassificationRuleGroupContract]``)

let ``convert [FieldMatchContract] to [FieldMatch]``
    (fieldMatch: FieldMatchContract)
    : Result<FieldMatch, AppError> =
        match fieldMatch with
        | FieldMatchContract.Source patternStr ->
            match patternStr |> StringSearchPattern.create with
            | Error e -> Error e
            | Ok x -> FieldMatch.Source x |> Ok
        | FieldMatchContract.Description patternStr ->
            match patternStr |> StringSearchPattern.create with
            | Error e -> Error e
            | Ok x -> FieldMatch.Description x |> Ok
        | FieldMatchContract.Memo patternStr ->
            match patternStr |> StringSearchPattern.create with
            | Error e -> Error e
            | Ok x -> FieldMatch.Memo x |> Ok
        | FieldMatchContract.LineType patternStr ->
            match patternStr |> JournalEntryLineType.fromString with
            | Error e -> Error e
            | Ok x -> FieldMatch.LineType x |> Ok
        | FieldMatchContract.Amount pattern -> result {
            let! numericSearchOperator = pattern.numericSearchOperator |> NumericSearchOperator.fromString
            let! amount = pattern.amount |> Money.fromDecimal
            return FieldMatch.Amount {
                numericSearchOperator = numericSearchOperator
                amount = amount } }

let ``convert [FieldMatchChainContract] to [FieldMatchChain]``
    (fieldMatchChainContract: FieldMatchChainContract)
    : Result<FieldMatchChain, AppError> =
    result {
        let! chain =
            fieldMatchChainContract.chain
            |> List.map(fun c -> c |> ``convert [FieldMatchContract] to [FieldMatch]``)
            |> convertListOfResultsToResultsList
        return FieldMatchChain.create chain }

let ``convert [ClassificationRuleGroupContract] to [ClassificationRuleGroup]``
    (ruleGroup: ClassificationRuleGroupContract)
    : Result<ClassificationRuleGroup, AppError> =
    result {
        let! connector = ruleGroup.connector |> ClassificationGroupConnector.fromString
        let! chainOne = ruleGroup.chainOne |> ``convert [FieldMatchChainContract] to [FieldMatchChain]``
        let! chainTwo =
            ruleGroup.chainTwo
            |> convertOptionToDesiredTypeWithFallibleConverter ``convert [FieldMatchChainContract] to [FieldMatchChain]``
        return ClassificationRuleGroup.create connector chainOne chainTwo }

let ``convert [ClassificationRuleGroupContract list] to [ClassificationRuleGroup list]``
    (ruleGroups: ClassificationRuleGroupContract list)
    : Result<ClassificationRuleGroup list, AppError> =
    ruleGroups
    |> List.map(fun x -> x |> ``convert [ClassificationRuleGroupContract] to [ClassificationRuleGroup]``)
    |> convertListOfResultsToResultsList

let ``convert [ClassificationRule] to [ClassificationRuleReturn]``
    (rule: ClassificationRule)
    : ClassificationRuleReturn =
    let classificationRuleId = rule |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value
    let classificationRuleName = rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value
    let codeAtMatch = rule |> ClassificationRule.codeAtMatch |> AccountCode.value
    let priority = rule |> ClassificationRule.priority
    let ruleGroups = rule |> ClassificationRule.ruleGroups |> ``convert [ClassificationRuleGroup list] to [ClassificationRuleGroupContract list]``
    let isActive = rule |> ClassificationRule.isActive
    let createdAt = rule |> ClassificationRule.createdAt
    let modifiedAt = rule |> ClassificationRule.modifiedAt
    {   classificationRuleId = classificationRuleId
        classificationRuleName = classificationRuleName
        codeAtMatch = codeAtMatch
        priority = priority
        ruleGroups = ruleGroups
        isActive = isActive
        createdAt = createdAt
        modifiedAt = modifiedAt
    }
