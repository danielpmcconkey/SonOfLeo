module InterfaceBridge.InterfaceContracts.IngestionContracts

open System
open InterfaceBridge.InterfaceContracts.ReportsContracts
open InterfaceBridge.InterfaceContracts.SharedContracts
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.FieldUpdate

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

type StageEntryHeaderReturn = {
        sourceFile: string
        stageEntryHeaderId: Guid
        entryDate: LocalDate
        description: string
        ingestionSource: string
        fiReference: string
        journalEntryHeaderId: Guid option
        status: string option
    }

type AccountClaimantReturn = {
        code: string
        accountName: string
    }

type ClassificationClaimantReturn =
    | Account of AccountClaimantReturn
    | PaymentAgreement of string // the payment agreement's name

type StageEntryLineReturn =  {
        stageEntryLineId: Guid
        stageEntryHeaderId: Guid
        amount: decimal
        lineType: string
        accountCode: string option
        accountName: string option
        memo: string option
        journalEntryLineId: Guid option
    }

type StageEntryStatusTransitionReturn = {
        stageEntryStatusTransitionId: Guid
        stageEntryHeaderId: Guid
        fromStatus: string option
        toStatus: string
        instant: Instant
        stageStatusChangeMechanism: string
    }

type StageEntryReturn = {
        stageEntryHeader: StageEntryHeaderReturn
        lines: StageEntryLineReturn list
        statusTransitions: StageEntryStatusTransitionReturn list
}

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

type AccountClassificationResultReturn = {
    runId: Guid
    classificationResults: ClassificationResultReturn list
    stagedEntries: StageEntryReturn list
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

type IngestionSourceReturn = {
        ingestionSourceId: Guid
        name: string
        createdAt: Instant
        modifiedAt: Instant
    }

type PostStageEntriesTrialBalancesResult = {
    trialBalanceBefore: TrialBalanceReturnRow list
    trialBalanceAfter: TrialBalanceReturnRow list
}

type PostStageEntriesFullResult = {
    trialBalanceBefore: TrialBalanceReturnRow list
    trialBalanceAfter: TrialBalanceReturnRow list
    wasRolledBack: bool
}

// ****************************************
// Input
// ****************************************


type IngestRawFileToStageInput = {
    fileName: string // just the name of the file and its extension. no path info
    importDir: string // the directory where raw file can be read 
    processedDir: string // where to put the raw file when done with it
}

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
type CreateNewIngestionSourceInput = { name: string }

type UpdateStageEntryLineInput = {
    stageEntryLineId: Guid
    amount: FieldUpdate<decimal>
    lineType: FieldUpdate<string>
    accountCode: FieldUpdate<string option>
    memo: FieldUpdate<string option>
}

type StageEntryStatusUpdateInput = {
    newStatus: string
    stageStatusChangeMechanism: string
}

type UpdateStageEntryInput = {
    stageEntryHeaderId: Guid
    sourceFileUpdate: FieldUpdate<string>
    entryDate: FieldUpdate<LocalDate>
    description: FieldUpdate<string>
    ingestionSource: FieldUpdate<string>
    fiReference: FieldUpdate<string>
    status: FieldUpdate<StageEntryStatusUpdateInput>
    lines: UpdateStageEntryLineInput list
}

type PostStageEntriesInput = { isShadow: bool }

type BaseStageRawRowInput = {
    baseStageEntryGroupId : string
    entryDate : LocalDate
    description: string
    fiSource: string
    fiReference: string
    amount : decimal
    entryType : string
    accountCode: string option
    memo: string option
}

type StageEntryFetchFilterInput =
    { stageEntryHeaderId : Guid option
      sourceFile: string option
      temporalFilter: TemporalFilterInput option
      description: string option
      ingestionSource: string option
      fiReference: string option
      status: string option
      stageEntryLineId: Guid option
      amount: decimal option
      lineType: string option
      accountCode: string option
      memo: string option
      journalEntryHeaderId: Guid option
      journalEntryLineId: Guid option }
    
type StageEntryFetchFilteredInput = { filter: StageEntryFetchFilterInput; sort: FetchStageEntrySort option }
