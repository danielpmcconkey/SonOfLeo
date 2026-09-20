module InterfaceBridge.InterfaceContracts.ClassificationContracts

open System
open InterfaceBridge.InterfaceContracts.AccountContracts
open Business.FinancialServices.FetchFilters
open NodaTime
open App.Utility.FieldUpdate

// ****************************************
// Bi-directional contracts
// ****************************************

type MoneySearchPatternContract = {
        numericSearchOperator: string
        amount: decimal
    }

type FieldMatchContract =
    | Source of string
    | Description of string
    | Memo of string
    | LineType of string
    | Amount of MoneySearchPatternContract

type FieldMatchChainContract = {
        chain: FieldMatchContract list
    }

type ClassificationRuleGroupContract = {
        connector: string
        chainOne: FieldMatchChainContract
        chainTwo: FieldMatchChainContract option
    }

// ****************************************
// Return
// ****************************************

type ClassificationClaimantReturn =
    | Account of AccountClaimantReturn
    | PaymentAgreement of string // the payment agreement's name

type MatchCandidateReturn = {
        stageEntryHeaderId: Guid
        stageEntryLineId: Guid
        ingestionSource: string
        description: string
        amount: decimal
        lineType: string
        memo: string option
}

type PrioritizedMatchReturn = {
    accountCode: string option
    accountName: string option
    paymentAgreementName: string option
    ruleId: Guid
    priority: int
}

type ClassifierOutcomeReturn =
    | NoMatch
    | OneMatch of PrioritizedMatchReturn
    | ManyMatchesClearWinner of PrioritizedMatchReturn * PrioritizedMatchReturn list
    | ManyMatchesTied of PrioritizedMatchReturn list

type ClassificationResultReturn = {
        candidate: MatchCandidateReturn
        outcome: ClassifierOutcomeReturn
    }

// priority is read off the live rule rather than the recorded match, so a rule re-prioritized since the run reports its
// current priority, not the one that decided the match
type RuleMatchReturn = {
    ruleMatchId: Guid
    stageEntryLineId: Guid
    classificationRuleId: Guid
    classificationRuleName: string
    claimantAtMatch: ClassificationClaimantReturn
    priority: int
    createdAt: Instant
}

type ClassificationRunReturn = {
    runId: Guid
    matches: RuleMatchReturn list
}

type ClassificationRuleReturn = {
        classificationRuleId: Guid
        classificationRuleName: string
        claimantAtMatch: ClassificationClaimantReturn
        priority: int
        ruleGroups: ClassificationRuleGroupContract list
        isActive: bool
        createdAt: Instant
        modifiedAt: Instant
    }

// ****************************************
// Input
// ****************************************

type ClassificationClaimantInput =
    | Account of string // the account's code
    | PaymentAgreement of string // the payment agreement's name

type NewClassificationRuleInput = {
        classificationRuleName: string
        claimantAtMatch: ClassificationClaimantInput
        priority: int
        ruleGroups: ClassificationRuleGroupContract list
    }

type UpdateClassificationRuleInput = {
        classificationRuleId: Guid
        classificationRuleNameUpdate: FieldUpdate<string>
        claimantAtMatchUpdate: FieldUpdate<ClassificationClaimantInput>
        priorityUpdate: FieldUpdate<int>
        ruleGroupsUpdate: FieldUpdate<ClassificationRuleGroupContract list>
        isActiveUpdate: FieldUpdate<bool>
    }

type ClassificationRuleFilterInput =  {
      ruleId: Guid option
      nameLike: string option
      accountCodeAtMatch: string option
      paymentAgreementNameAtMatch: string option
      claimantType: string option
      sourceLike: string option
      activeOnly: bool }

type FetchClassificationRuleFilteredInput = {
    filter: ClassificationRuleFilterInput
    sort: FetchSortClassificationRule option
}

type FetchClassificationRunInput = { runId: Guid }
type FetchClassificationRuleByIdInput = { classificationRuleId: Guid }
type FetchClassificationRuleByNameInput = { classificationRuleName: string }
