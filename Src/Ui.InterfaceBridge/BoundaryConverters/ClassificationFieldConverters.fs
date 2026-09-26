module Ui.InterfaceBridge.BoundaryConverters.ClassificationFieldConverters

open App.Utility
open App.Utility.IAppError
open App.Utility.Result
open App.Session
open Business.FinancialServices
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.CashFlow
open Business.FinancialServices.CashFlow.CashFlowComponent
open Business.FinancialServices.Classification
open Business.FinancialServices.Classification.ClassificationComponent
open Business.CrossDomainOrchestration
open Business.CrossDomainOrchestration.FetchFilters
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Ui.InterfaceBridge.BoundaryConverters.AccountFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.IngestionFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.CashFlowFieldConverters
open Ui.InterfaceBridge.BoundaryConverters.CashFlowLookupConverters
open Ui.InterfaceBridge.InterfaceContracts.ClassificationContracts

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
    : Result<FieldMatch.FieldMatch, IAppError> =
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
    : Result<FieldMatchChain.FieldMatchChain, IAppError> =
    result {
        let! chain =
            fieldMatchChainContract.chain
            |> List.map(fun c -> c |> ``convert [FieldMatchContract] to [FieldMatch]``)
            |> convertListOfResultsToResultsList
        return FieldMatchChain.create chain }

let ``convert [ClassificationRuleGroupContract] to [ClassificationRuleGroup]``
    (ruleGroup: ClassificationRuleGroupContract)
    : Result<ClassificationRuleGroup.ClassificationRuleGroup, IAppError> =
    result {
        let! connector = ruleGroup.connector |> ClassificationGroupConnector.fromString
        let! chainOne = ruleGroup.chainOne |> ``convert [FieldMatchChainContract] to [FieldMatchChain]``
        let! chainTwo =
            ruleGroup.chainTwo
            |> convertOptionToDesiredTypeWithFallibleConverter ``convert [FieldMatchChainContract] to [FieldMatchChain]``
        return ClassificationRuleGroup.create connector chainOne chainTwo }

let ``convert [ClassificationRuleGroupContract list] to [ClassificationRuleGroup list]``
    (ruleGroups: ClassificationRuleGroupContract list)
    : Result<ClassificationRuleGroup.ClassificationRuleGroup list, IAppError> =
    ruleGroups
    |> List.map(fun x -> x |> ``convert [ClassificationRuleGroupContract] to [ClassificationRuleGroup]``)
    |> convertListOfResultsToResultsList

let ``convert [ClassificationClaimant] to [ClassificationClaimantReturn]``
    (context: Context.Context)
    (claimant: ClassificationClaimant)
    : Result<ClassificationClaimantReturn, IAppError> =
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
    : Result<ClassificationClaimant, IAppError> =
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
    : Result<ClassificationRuleReturn, IAppError> = result {
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
    : Result<ClassificationRuleReturn list, IAppError> =
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
    : Result<PrioritizedMatchReturn, IAppError> = result {
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
    : Result<ClassifierOutcomeReturn, IAppError> = 
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
    : Result<ClassificationResultReturn, IAppError> = result {
    let candidate = classificationResults.candidate |>  ``convert [MatchCandidate] to [MatchCandidateReturn]``
    let! outcome = classificationResults.outcome |> ``convert [ClassifierOutcome] to [ClassifierOutcomeReturn]`` context
    return {    candidate = candidate
                outcome = outcome } }

let ``convert [ClassificationResult list] to [ClassificationResultReturn list]``
    (context: Context.Context)
    (classificationResults: ClassificationResult list)
    : Result<ClassificationResultReturn list, IAppError> =
    classificationResults
    |> List.map (``convert [ClassificationResult] to [ClassificationResultReturn]`` context)
    |> convertListOfResultsToResultsList

let ``convert [ClassificationRuleFilterInput] to [ClassificationRuleFilter]``
    (context: Context.Context)
    (filterInput: ClassificationRuleFilterInput)
    : Result<ClassificationRuleFilter, IAppError> = result {
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

let ``convert [RuleMatch] to [RuleMatchReturn]``
    (context: Context.Context)
    (rule: ClassificationRule.ClassificationRule)
    (ruleMatch: RuleMatch.RuleMatch)
    : Result<RuleMatchReturn, IAppError> =
    result {
        let! claimantAtMatch =
            rule
            |> ClassificationRule.classificationClaimant
            |> ``convert [ClassificationClaimant] to [ClassificationClaimantReturn]`` context
        return {
            ruleMatchId = ruleMatch |> RuleMatch.classificationMatchId |> ClassificationMatchId.value
            stageEntryLineId = ruleMatch |> RuleMatch.stageEntryLineId |> StageEntryLineId.value
            classificationRuleId = ruleMatch |> RuleMatch.classificationRuleId |> ClassificationRuleId.value
            classificationRuleName = rule |> ClassificationRule.classificationRuleName |> ClassificationRuleName.value
            claimantAtMatch = claimantAtMatch
            priority = rule |> ClassificationRule.priority
            createdAt = ruleMatch |> RuleMatch.createdAt } }

let ``convert [AccountClassificationResult] to [AccountClassificationResultReturn]``
    (context: Context.Context)
    (classificationResult: AccountClassificationResult)
    : Result<AccountClassificationResultReturn, IAppError> = result {
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

let ``convert [PaymentAgreementDecision] to [PaymentAgreementDecisionReturn]``
    (context: Context.Context)
    (decision: PaymentAgreementDecision)
    : Result<PaymentAgreementDecisionReturn, IAppError> =
    result {
        let! paymentAgreementName =
            decision.paymentAgreementId |> ``convert [PaymentAgreementId option] to [PaymentAgreementNameString option]`` context
        return {
            stageEntryLineId = decision.stageEntryLineId |> StageEntryLineId.value
            paymentAgreementName = paymentAgreementName
            ruleIds = decision.ruleIds |> List.map ClassificationRuleId.value
            outcome = decision.outcome |> PaymentAgreementDecisionOutcome.toString } }

let ``convert [InvoiceDecision] to [InvoiceDecisionReturn]`` (decision: InvoiceDecision) : InvoiceDecisionReturn =
    let outcome =
        match decision.outcome with
        | CashFlowComponent.PaymentCreated lineId ->
            InvoiceDecisionOutcomeReturn.PaymentCreated(lineId |> StageEntryLineId.value)
        | CashFlowComponent.ManyCandidateEntries lineIds ->
            InvoiceDecisionOutcomeReturn.ManyCandidateEntries(lineIds |> List.map StageEntryLineId.value)
        | CashFlowComponent.Overpayment -> InvoiceDecisionOutcomeReturn.Overpayment
    { invoiceId = decision.invoiceId |> InvoiceId.value; outcome = outcome }

let ``convert [PaymentAgreementClassificationResult] to [PaymentAgreementClassificationResultReturn]``
    (context: Context.Context)
    (classificationResult: InstanceOrchestration.PaymentAgreementClassificationResult)
    : Result<PaymentAgreementClassificationResultReturn, IAppError> =
    result {
        let! classificationResults =
            classificationResult.classificationResults
            |> ``convert [ClassificationResult list] to [ClassificationResultReturn list]`` context
        let! decisionLog =
            classificationResult.decisionLog
            |> List.map (``convert [PaymentAgreementDecision] to [PaymentAgreementDecisionReturn]`` context)
            |> convertListOfResultsToResultsList
        let! openInstances =
            classificationResult.openInstances
            |> ``convert [InstanceComposite list] to [InstanceCompositeReturn list]`` context
        let sortedResults =
            classificationResults
            |> List.sortBy (fun result -> result.candidate.stageEntryHeaderId, result.candidate.stageEntryLineId)
        let sortedDecisionLog =
            decisionLog
            |> List.sortBy (fun (decision: PaymentAgreementDecisionReturn) ->
                decision.paymentAgreementName, decision.stageEntryLineId)
        let sortedInvoiceDecisionLog =
            classificationResult.invoiceDecisionLog
            |> List.map ``convert [InvoiceDecision] to [InvoiceDecisionReturn]``
            |> List.sortBy (fun (decision: InvoiceDecisionReturn) -> decision.invoiceId, decision.outcome)
        return {
            runId = classificationResult.runId |> ClassificationRunId.value
            classificationResults = sortedResults
            decisionLog = sortedDecisionLog
            invoiceDecisionLog = sortedInvoiceDecisionLog
            openInstances = openInstances } }

let ``convert [PaymentAgreementLink] to [PaymentAgreementLinkReturn]``
    (context: Context.Context)
    (link: PaymentAgreementLink.PaymentAgreementLink)
    : Result<PaymentAgreementLinkReturn, IAppError> =
    result {
        let! paymentAgreementName =
            link
            |> PaymentAgreementLink.paymentAgreementId
            |> ``convert [PaymentAgreementId] to [PaymentAgreementNameString]`` context
        return {
            paymentAgreementLinkId =
                link |> PaymentAgreementLink.paymentAgreementLinkId |> PaymentAgreementLinkId.value
            paymentAgreementName = paymentAgreementName
            stageEntryLineId = link |> PaymentAgreementLink.stageEntryLineId |> StageEntryLineId.value
            createdAt = link |> PaymentAgreementLink.createdAt
            modifiedAt = link |> PaymentAgreementLink.modifiedAt } }

let ``convert [UpdatePaymentAgreementLinkInput] to [PaymentAgreementLinkFieldUpdates]``
    (context: Context.Context)
    (input: UpdatePaymentAgreementLinkInput)
    : Result<PaymentAgreementLink.PaymentAgreementLinkFieldUpdates, IAppError> =
    result {
        let linkId = input.paymentAgreementLinkId |> PaymentAgreementLinkId.fromGuid
        let! paymentAgreementIdUpdate =
            input.paymentAgreementNameUpdate
            |> FieldUpdate.convertFieldUpdateToNewTypeFallible (
                ``convert [PaymentAgreementNameString] to [PaymentAgreementId]`` context)
        return { linkIdToUpdate = linkId; paymentAgreementIdUpdate = paymentAgreementIdUpdate } }
