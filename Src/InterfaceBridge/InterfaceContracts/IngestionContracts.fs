module InterfaceBridge.InterfaceContracts.IngestionContracts

open System
open InterfaceBridge.InterfaceContracts.ClassificationContracts
open InterfaceBridge.InterfaceContracts.ReportsContracts
open InterfaceBridge.InterfaceContracts.SharedContracts
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.FieldUpdate

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

type AccountClassificationResultReturn = {
    runId: Guid
    classificationResults: ClassificationResultReturn list
    stagedEntries: StageEntryReturn list
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
