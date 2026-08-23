module InterfaceBridge.BoundaryConverters.IngestionFieldConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.OrchestrationConverters
open InterfaceBridge.InterfaceContracts.IngestionContracts
open Model
open Model.DataIngestion
open Model.DataIngestion.BaseStageRaw
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.FetchFilters
open ModelOrchestrator.StageEntryOrchestration
open Utilities.AppError
open Utilities.FieldUpdate.FieldUpdate
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
    let classificationRuleId = model |> StageEntryLine.classificationRuleId |> Option.map ClassificationRuleId.value
    return {    stageEntryLineId = stageEntryLineId
                stageEntryHeaderId = stageEntryHeaderId
                amount = amount
                lineType = lineType
                accountCode = accountCode
                accountName = accountName
                memo = memo 
                classificationRuleId = classificationRuleId } }

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
        |> lines
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
    (context: Context.Context)
    (rule: ClassificationRule.ClassificationRule)
    : Result<ClassificationRuleReturn, AppError> = result {
    let classificationRuleId = rule |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value
    let classificationRuleName = rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value
    let! codeAtMatch =
        rule |> ClassificationRule.accountIdAtMatch |> ``convert AccountId to AccountCodeString`` context
    let! accountNameAtMach =
        rule
        |> ClassificationRule.accountIdAtMatch
        |> ``convert AccountId to AccountNameString`` context
    let priority = rule |> ClassificationRule.priority
    let ruleGroups = rule |> ClassificationRule.ruleGroups |> ``convert [ClassificationRuleGroup list] to [ClassificationRuleGroupContract list]``
    let isActive = rule |> ClassificationRule.isActive
    let createdAt = rule |> ClassificationRule.createdAt
    let modifiedAt = rule |> ClassificationRule.modifiedAt
    return {    classificationRuleId = classificationRuleId
                classificationRuleName = classificationRuleName
                codeAtMatch = codeAtMatch
                accountNameAtMatch = accountNameAtMach
                priority = priority
                ruleGroups = ruleGroups
                isActive = isActive
                createdAt = createdAt
                modifiedAt = modifiedAt } }

let ``convert [ClassificationRule list] to [ClassificationRuleReturn list]``
    (context: Context.Context)
    (rules: ClassificationRule.ClassificationRule list)
    : Result<ClassificationRuleReturn list, AppError> =
    rules
    |> List.map (``convert [ClassificationRule] to [ClassificationRuleReturn]`` context)
    |> convertListOfResultsToResultsList

let ``convert [IngestionSource] to [IngestionSourceReturn]``
    (source: IngestionSource.IngestionSource)
    : IngestionSourceReturn = {
        ingestionSourceId = source |> IngestionSource.ingestionSourceId |> IngestionSourceId.value
        name = source |> IngestionSource.name |> JournalRefFinancialInstitution.value
        createdAt = source |> IngestionSource.createdAt
        modifiedAt = source |> IngestionSource.modifiedAt
    }

let ``convert [MatchCandidate] to [MatchCandidateReturn]``
    (candidate: MatchCandidate)
    : MatchCandidateReturn = {
        stageEntryHeaderId = candidate.headerIdOfCandidate |> StageEntryHeaderId.value
        stageEntryLineId = candidate.lineIdOfCandidate |> StageEntryLineId.value
        ingestionSource = candidate.ingestionSource |> JournalRefFinancialInstitution.value
        description = candidate.description |> JournalEntryDescription.value
        amount = candidate.amount |> Money.amount
        lineType = candidate.lineType |> JournalEntryLineType.toString
        memo = candidate.memo |> Option.map JournalEntryLineMemo.value }

let ``convert [PrioritizedMatch] to [PrioritizedMatchReturn]``
    (context: Context.Context)
    (prioritizedMatch: PrioritizedMatch)
    : Result<PrioritizedMatchReturn, AppError> = result {
    let! code = prioritizedMatch.accountId |> ``convert AccountId to AccountCodeString`` context
    let! accountName = prioritizedMatch.accountId |> ``convert AccountId to AccountNameString`` context
    return {    code = code
                accountName = accountName
                ruleId = prioritizedMatch.ruleId |> ClassificationRuleId.value
                priority = prioritizedMatch.priority } }

let ``convert [ClassifierOutcome] to [ClassifierOutcomeReturn]``
    (context: Context.Context)
    (outcome: ClassifierOutcome)
    : Result<ClassifierOutcomeReturn, AppError> = 
    match outcome with
    | ClassifierOutcome.NoMatch -> Ok ClassifierOutcomeReturn.NoMatch
    | ClassifierOutcome.OneMatch pm -> result {
        let! matchReturn = pm |> ``convert [PrioritizedMatch] to [PrioritizedMatchReturn]`` context
        return ClassifierOutcomeReturn.OneMatch matchReturn }
    | ClassifierOutcome.ManyMatchesClearWinner (pm, pml) -> result {
        let! returnMatch = pm |> ``convert [PrioritizedMatch] to [PrioritizedMatchReturn]`` context
        let! returnList =
            pml
            |> List.map (``convert [PrioritizedMatch] to [PrioritizedMatchReturn]`` context)
            |> convertListOfResultsToResultsList
        return ClassifierOutcomeReturn.ManyMatchesClearWinner (returnMatch, returnList) }
    | ClassifierOutcome.ManyMatchesTied pml -> result {
        let! returnList =
            pml
            |> List.map (``convert [PrioritizedMatch] to [PrioritizedMatchReturn]`` context)
            |> convertListOfResultsToResultsList
        return ClassifierOutcomeReturn.ManyMatchesTied returnList }

let ``convert [ClassificationResult] to [ClassificationResultReturn]``
    (context: Context.Context)
    (classificationResults: ClassificationResult)
    : Result<ClassificationResultReturn, AppError> = result {
    let candidate = classificationResults.candidate |>  ``convert [MatchCandidate] to [MatchCandidateReturn]``
    let! outcome = classificationResults.outcome |> ``convert [ClassifierOutcome] to [ClassifierOutcomeReturn]`` context
    return {    candidate = candidate
                outcome = outcome } }

let ``convert [ClassificationResult list] to [ClassificationResultReturn list]``
    (context: Context.Context)
    (classificationResults: ClassificationResult list)
    : Result<ClassificationResultReturn list, AppError> =
    classificationResults
    |> List.map (``convert [ClassificationResult] to [ClassificationResultReturn]`` context)
    |> convertListOfResultsToResultsList

let ``convert [IngestionFullResult] to [IngestionFullResultReturn]``
    (context: Context.Context)
    (fullResult: IngestionFullResult)
    : Result<IngestionFullResultReturn, AppError> = result {
    let! stageEntryReturn =
        fullResult.stagedEntries
        |> List.map (``convert [StageEntry] to [StageEntryReturn]`` context)
        |> convertListOfResultsToResultsList
    let newDuplicatesReturn =
        fullResult.newDuplicates
        |> List.map ``convert [StageEntryHeader] to [StageEntryHeaderReturn]``
    let! classificationResultsReturn =
        fullResult.classificationResults
        |> ``convert [ClassificationResult list] to [ClassificationResultReturn list]`` context
    return {  stagedEntries = stageEntryReturn
              newDuplicates = newDuplicatesReturn
              classificationResults = classificationResultsReturn } }

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
        let classificationRuleIdUpdate =
            line.classificationRuleId
            |> convertFieldUpdateOptionToNewTypeOption ClassificationRuleId.fromGuid
        return {
          lineIdToUpdate = lineIdToUpdate
          amountUpdate = amountUpdate
          entryTypeUpdate = entryTypeUpdate
          accountIdUpdate = accountIdUpdate
          memoUpdate = memoUpdate
          classificationRuleIdUpdate = classificationRuleIdUpdate } }

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
        let classificationRuleId = filterInput.classificationRuleId |> Option.map ClassificationRuleId.fromGuid
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
            classificationRuleId = classificationRuleId
        } }
let ``convert [ClassificationRuleFilterInput] to [ClassificationRuleFilter]``
    (context: Context.Context)
    (filterInput: ClassificationRuleFilterInput)
    : Result<ClassificationRuleFilter, AppError> = result {
    let ruleId = filterInput.ruleId |> Option.map ClassificationRuleId.fromGuid
    let! nameLike =
        filterInput.nameLike |> convertOptionToDesiredTypeWithFallibleConverter ClassificationRuleName.create
    let! accountAtMatch =
        filterInput.accountCodeAtMatch |> ``convert AccountCodeString Option to AccountId Option`` context
    return {
        ruleId = ruleId
        nameLike = nameLike
        accountAtMatch = accountAtMatch
        sourceLike = filterInput.sourceLike
        activeOnly = filterInput.activeOnly
    } }
