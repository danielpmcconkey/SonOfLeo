module InterfaceBridge.BoundaryConverters.ClassificationFieldConverters

open InterfaceBridge.BoundaryConverters.AccountFieldConverters
open InterfaceBridge.BoundaryConverters.CashFlowLookupConverters
open InterfaceBridge.InterfaceContracts.ClassificationContracts
open Model
open Model.DataIngestion.StageEntryComponent
open Model.Ledger.JournalEntryComponent
open Model.StageDataClassification
open Model.StageDataClassification.StageDataClassificationComponent
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.ResultHelper

let ``convert [FieldMatch] to [FieldMatchContract]``
    (fieldMatch: FieldMatch.FieldMatch)
    : FieldMatchContract =
    match fieldMatch with
    | FieldMatch.Source pattern -> FieldMatchContract.Source (pattern |> StringSearchPattern.value)
    | FieldMatch.Description pattern -> FieldMatchContract.Description (pattern |> StringSearchPattern.value)
    | FieldMatch.Memo pattern -> FieldMatchContract.Memo (pattern |> StringSearchPattern.value)
    | FieldMatch.LineType pattern -> FieldMatchContract.LineType (pattern |> JournalEntryLineType.toString)
    | FieldMatch.Amount pattern ->
        let numericSearchOperator = pattern.numericSearchOperator |> NumericSearchOperator.toString
        let amount = pattern.amount |> Money.amount
        FieldMatchContract.Amount {
            numericSearchOperator = numericSearchOperator
            amount = amount
        }

let ``convert [FieldMatchChain] to [FieldMatchChainContract]``
    (fieldMatchChain: FieldMatchChain.FieldMatchChain)
    : FieldMatchChainContract =
    let chain =
        fieldMatchChain |> FieldMatchChain.chain
        |> List.map(fun c -> c |> ``convert [FieldMatch] to [FieldMatchContract]``)
    { chain = chain }

let ``convert [ClassificationRuleGroup] to [ClassificationRuleGroupContract]``
    (ruleGroup: ClassificationRuleGroup.ClassificationRuleGroup)
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
    (ruleGroups: ClassificationRuleGroup.ClassificationRuleGroup list)
    : ClassificationRuleGroupContract list =
    ruleGroups
    |> List.map(fun x -> x |> ``convert [ClassificationRuleGroup] to [ClassificationRuleGroupContract]``)

let ``convert [FieldMatchContract] to [FieldMatch]``
    (fieldMatch: FieldMatchContract)
    : Result<FieldMatch.FieldMatch, AppError> =
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
    : Result<FieldMatchChain.FieldMatchChain, AppError> =
    result {
        let! chain =
            fieldMatchChainContract.chain
            |> List.map(fun c -> c |> ``convert [FieldMatchContract] to [FieldMatch]``)
            |> convertListOfResultsToResultsList
        return FieldMatchChain.create chain }

let ``convert [ClassificationRuleGroupContract] to [ClassificationRuleGroup]``
    (ruleGroup: ClassificationRuleGroupContract)
    : Result<ClassificationRuleGroup.ClassificationRuleGroup, AppError> =
    result {
        let! connector = ruleGroup.connector |> ClassificationGroupConnector.fromString
        let! chainOne = ruleGroup.chainOne |> ``convert [FieldMatchChainContract] to [FieldMatchChain]``
        let! chainTwo =
            ruleGroup.chainTwo
            |> convertOptionToDesiredTypeWithFallibleConverter ``convert [FieldMatchChainContract] to [FieldMatchChain]``
        return ClassificationRuleGroup.create connector chainOne chainTwo }

let ``convert [ClassificationRuleGroupContract list] to [ClassificationRuleGroup list]``
    (ruleGroups: ClassificationRuleGroupContract list)
    : Result<ClassificationRuleGroup.ClassificationRuleGroup list, AppError> =
    ruleGroups
    |> List.map(fun x -> x |> ``convert [ClassificationRuleGroupContract] to [ClassificationRuleGroup]``)
    |> convertListOfResultsToResultsList

let ``convert [ClassificationClaimant] to [ClassificationClaimantReturn]``
    (context: Context.Context)
    (claimant: ClassificationClaimant)
    : Result<ClassificationClaimantReturn, AppError> =
    match claimant with
    | ClassificationClaimant.Account accountId -> result {
        let! code = accountId |> ``convert AccountId to AccountCodeString`` context
        let! accountName = accountId |> ``convert AccountId to AccountNameString`` context
        return ClassificationClaimantReturn.Account { code = code; accountName = accountName } }
    | ClassificationClaimant.PaymentAgreement paymentAgreementId ->
        paymentAgreementId
        |> ``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context
        |> Result.map ClassificationClaimantReturn.PaymentAgreement

let ``convert [ClassificationClaimantInput] to [ClassificationClaimant]``
    (context: Context.Context)
    (claimantInput: ClassificationClaimantInput)
    : Result<ClassificationClaimant, AppError> =
    match claimantInput with
    | ClassificationClaimantInput.Account codeString ->
        codeString
        |> ``convert AccountCodeString to Id`` context
        |> Result.map ClassificationClaimant.Account
    | ClassificationClaimantInput.PaymentAgreement nameString ->
        nameString
        |> ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context
        |> Result.map ClassificationClaimant.PaymentAgreement

let ``convert [ClassificationRule] to [ClassificationRuleReturn]``
    (context: Context.Context)
    (rule: ClassificationRule.ClassificationRule)
    : Result<ClassificationRuleReturn, AppError> = result {
    let classificationRuleId = rule |> ClassificationRule.classificationRuleId |> ClassificationRuleId.value
    let classificationRuleName = rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value
    let! claimantAtMatch =
        rule
        |> ClassificationRule.classificationClaimant
        |> ``convert [ClassificationClaimant] to [ClassificationClaimantReturn]`` context
    let priority = rule |> ClassificationRule.priority
    let ruleGroups = rule |> ClassificationRule.ruleGroups |> ``convert [ClassificationRuleGroup list] to [ClassificationRuleGroupContract list]``
    let isActive = rule |> ClassificationRule.isActive
    let createdAt = rule |> ClassificationRule.createdAt
    let modifiedAt = rule |> ClassificationRule.modifiedAt
    return {    classificationRuleId = classificationRuleId
                classificationRuleName = classificationRuleName
                claimantAtMatch = claimantAtMatch
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
    let! accountCode =
        prioritizedMatch.accountId |> ``convert AccountId Option to AccountCodeString Option`` context
    let! accountName =
        prioritizedMatch.accountId |> ``convert [AccountId option] to [AccountName string option]`` context
    let! paymentAgreementName =
        prioritizedMatch.paymentAgreementId
        |> ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]`` context
    return {    accountCode = accountCode
                accountName = accountName
                paymentAgreementName = paymentAgreementName
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

let ``convert [ClassificationRuleFilterInput] to [ClassificationRuleFilter]``
    (context: Context.Context)
    (filterInput: ClassificationRuleFilterInput)
    : Result<ClassificationRuleFilter, AppError> = result {
    let ruleId = filterInput.ruleId |> Option.map ClassificationRuleId.fromGuid
    let! nameLike =
        filterInput.nameLike |> convertOptionToDesiredTypeWithFallibleConverter ClassificationRuleName.create
    let! accountAtMatch =
        filterInput.accountCodeAtMatch |> ``convert AccountCodeString Option to AccountId Option`` context
    let! paymentAgreementAtMatch =
        filterInput.paymentAgreementNameAtMatch
        |> ``convert [PaymentAgreementNameString option] to [PaymentAgreementId option]`` context
    let! claimantType =
        filterInput.claimantType
        |> convertOptionToDesiredTypeWithFallibleConverter ClassificationClaimantType.fromString
    return {
        ruleId = ruleId
        nameLike = nameLike
        accountAtMatch = accountAtMatch
        paymentAgreementAtMatch = paymentAgreementAtMatch
        claimantType = claimantType
        sourceLike = filterInput.sourceLike
        activeOnly = filterInput.activeOnly
    } }
