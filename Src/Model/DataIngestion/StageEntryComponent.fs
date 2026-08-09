namespace Model.DataIngestion
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open System

type IngestionSourceId = private IngestionSourceId of Guid

module IngestionSourceId =
    let create () : IngestionSourceId = IngestionSourceId(Guid.NewGuid())
    let fromGuid g = IngestionSourceId g
    let value (IngestionSourceId g) : Guid = g

type StagedEntryStatus =
    | Read // read from file, not yet loaded into stage table
    | Ingested // added to stage table, not yet classified
    | Classified // classifier ran, found a match
    | NoMatch // classifier ran, no match found
    | Conflict // classifier ran, found conflicting match candidates
    | Reviewed // approved, ready to be posted
    | Duplicate // the entry already exists in the ledger. usually a terminal status, can go to reviewed
    | Posted // the entry has been added to the ledger. terminal status
    | Ignored // the entry has problems and should not be added. usually a terminal status, can go to reviewed

module StagedEntryStatus =
    let fromString str = str |> function
        | "Read" -> Ok Read
        | "Ingested" -> Ok Ingested
        | "Classified" -> Ok Classified
        | "NoMatch" -> Ok NoMatch
        | "Conflict" -> Ok Conflict
        | "Reviewed" -> Ok Reviewed
        | "Duplicate" -> Ok Duplicate
        | "Posted" -> Ok Posted
        | "Ignored" -> Ok Ignored
        | _ -> Error(IngestionInvalidStagedEntryStatus str)
    
    let toString ``type`` = ``type`` |> function
        | Read -> "Read"
        | Ingested -> "Ingested"
        | Classified -> "Classified"
        | NoMatch -> "NoMatch"
        | Conflict -> "Conflict"
        | Reviewed -> "Reviewed"
        | Duplicate -> "Duplicate"
        | Posted -> "Posted"
        | Ignored -> "Ignored"
        
type StageStatusChangeMechanism =
    | BaseParser // the process that reads the base file and hands off to the stage ingestion
    | StageIngestion // the process that initially loads the staged entry into staged tables
    | Classifier // the process that runs vendor classification rules and updates "the other legs" of a JE
    | Deduplicator // the process that recognizes when an entry already has been ingested
    | Operator // a human or agent running the CLI
    | LedgerPoster // the process that posts staged data to the ledger

module StageStatusChangeMechanism =
    let fromString str = str |> function
        | "BaseParser" -> Ok BaseParser
        | "StageIngestion" -> Ok StageIngestion
        | "Classifier" -> Ok Classifier
        | "Deduplicator" -> Ok Deduplicator
        | "Operator" -> Ok Operator
        | "LedgerPoster" -> Ok LedgerPoster
        | _ -> Error(IngestionInvalidStageStatusChangeMechanism str)
    
    let toString ``type`` = ``type`` |> function
        | BaseParser -> "BaseParser"
        | StageIngestion -> "StageIngestion"
        | Classifier -> "Classifier"
        | Deduplicator -> "Deduplicator"
        | Operator -> "Operator"
        | LedgerPoster -> "LedgerPoster"

type SourceFile = private SourceFile of string

module SourceFile =
    let maxLength = 150
    let value (SourceFile ac) = ac 
    let create (raw: string) : Result<SourceFile, AppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(IngestionSourceFileIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionSourceFileTooLong(raw, maxLength))
        else
            Ok(SourceFile trimmed)

type StageEntryStatusTransitionId = private StageEntryStatusTransitionId of Guid

module StageEntryStatusTransitionId =
    let create () : StageEntryStatusTransitionId = StageEntryStatusTransitionId(Guid.NewGuid())
    let fromGuid g = StageEntryStatusTransitionId g
    let value (StageEntryStatusTransitionId g) : Guid = g

type StageEntryLineId = private StageEntryLineId of Guid

module StageEntryLineId =
    let create () : StageEntryLineId = StageEntryLineId(Guid.NewGuid())
    let fromGuid g = StageEntryLineId g
    let value (StageEntryLineId g) : Guid = g
    
type StageEntryHeaderId = private StageEntryHeaderId of Guid

module StageEntryHeaderId =
    let create () : StageEntryHeaderId = StageEntryHeaderId(Guid.NewGuid())
    let fromGuid g = StageEntryHeaderId g
    let value (StageEntryHeaderId g) : Guid = g
