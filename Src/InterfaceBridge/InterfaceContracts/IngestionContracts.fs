module InterfaceBridge.InterfaceContracts.IngestionContracts

open System
open Model.DataIngestion
open Model.DataIngestion.IngestionSource
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.FetchFilters
open NodaTime

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
