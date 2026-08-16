module InterfaceBridge.InterfaceContracts.IngestionContracts

open System
open Model.DataIngestion
open Model.DataIngestion.IngestionSource
open Model.Ledger.Journaling.JournalEntryComponent
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
        status: string 
    }
                
type StageEntryLineReturn =  {
        stageEntryLineId: Guid
        stageEntryHeaderId: Guid
        amount: decimal
        lineType: string
        accountCode: string option
        memo: string option
        classificationRuleId: Guid option
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
    code: string
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

type IngestionFullResultReturn = {
    stagedEntries: StageEntryReturn list
    newDuplicates: StageEntryHeaderReturn list
    classificationResults: ClassificationResultReturn list
}

type ClassificationRuleReturn = {
        classificationRuleId: Guid
        classificationRuleName: string
        codeAtMatch: string
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

// ****************************************
// Input
// ****************************************


type IngestRawFileToStageInput = {
    fileName: string // just the name of the file and its extension. no path info
    importDir: string // the directory where raw file can be read 
    processedDir: string // where to put the raw file when done with it
}

type NewClassificationRuleInput = {
        classificationRuleName: string
        codeAtMatch: string
        priority: int 
        ruleGroups: ClassificationRuleGroupContract list
    }

type FetchClassificationRuleFilteredInput = {
    filter: ClassificationRuleFilter
    sort: FetchSortClassificationRule option
}

type FetchClassificationRuleByIdInput = { classificationRuleId: Guid }
type FetchClassificationRuleByNameInput = { classificationRuleName: string }
type CreateNewIngestionSourceInput = { name: string }

type UpdateStageEntryLineInput = {
    stageEntryLineId: Guid
    amount: FieldUpdate<decimal>
    lineType: FieldUpdate<string>
    accountCode: FieldUpdate<string option>
    memo: FieldUpdate<string option>
    classificationRuleId: FieldUpdate<Guid option>
}

type UpdateStageEntryInput = {
    stageEntryHeaderId: Guid
    sourceFileUpdate: FieldUpdate<string>
    entryDate: FieldUpdate<LocalDate>
    description: FieldUpdate<string>
    ingestionSource: FieldUpdate<string>
    fiReference: FieldUpdate<string>
    status: FieldUpdate<string>
    lines: UpdateStageEntryLineInput list
}
