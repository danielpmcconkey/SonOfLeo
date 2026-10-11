module Business.FinancialServices.DataIngestion.StageEntryComponent

open System
open NodaTime
open App.Utility.IAppError
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.DataIngestion.DataIngestionError

type IngestionSourceId = private IngestionSourceId of Guid

module IngestionSourceId =
    let create () : IngestionSourceId = IngestionSourceId(Guid.NewGuid())
    let fromGuid g = IngestionSourceId g
    let value (IngestionSourceId g) : Guid = g

type StagedEntryStatus =
    | Ingested // added to stage table, not yet classified
    | Classified // classifier ran, found a match
    | NoMatch // classifier ran, no match found
    | Conflict // classifier ran, found conflicting match candidates
    | Reviewed // approved, ready to be posted
    | Duplicate // the entry already exists in the ledger. usually a terminal status, can go to reviewed
    | Posted // the entry has been added to the ledger. terminal status
    | Ignored // the entry has problems and should not be added. usually a terminal status, can go to reviewed

module StagedEntryStatus =
    let fromString (str: string) = str.Trim() |> function
        | "Ingested" -> Ok Ingested
        | "Classified" -> Ok Classified
        | "NoMatch" -> Ok NoMatch
        | "Conflict" -> Ok Conflict
        | "Reviewed" -> Ok Reviewed
        | "Duplicate" -> Ok Duplicate
        | "Posted" -> Ok Posted
        | "Ignored" -> Ok Ignored
        | _ -> error(IngestionInvalidStagedEntryStatus str)
    
    let toString ``type`` = ``type`` |> function
        | Ingested -> "Ingested"
        | Classified -> "Classified"
        | NoMatch -> "NoMatch"
        | Conflict -> "Conflict"
        | Reviewed -> "Reviewed"
        | Duplicate -> "Duplicate"
        | Posted -> "Posted"
        | Ignored -> "Ignored"
        
type StageStatusChangeMechanism =
    | StageIngestion // the process that initially loads the staged entry into staged tables
    | Classifier // the process that runs vendor classification rules and updates "the other legs" of a JE
    | Deduplicator // the process that recognizes when an entry already has been ingested
    | Operator // a human or agent running the CLI
    | LedgerPoster // the process that posts staged data to the ledger

module StageStatusChangeMechanism =
    let fromString (str: string) = str.Trim() |> function
        | "StageIngestion" -> Ok StageIngestion
        | "Classifier" -> Ok Classifier
        | "Deduplicator" -> Ok Deduplicator
        | "Operator" -> Ok Operator
        | "LedgerPoster" -> Ok LedgerPoster
        | _ -> error(IngestionInvalidStageStatusChangeMechanism str)
    
    let toString ``type`` = ``type`` |> function
        | StageIngestion -> "StageIngestion"
        | Classifier -> "Classifier"
        | Deduplicator -> "Deduplicator"
        | Operator -> "Operator"
        | LedgerPoster -> "LedgerPoster"

type SourceFile = private SourceFile of string

module SourceFile =
    let maxLength = 150
    let value (SourceFile ac) = ac 
    let create (raw: string) : Result<SourceFile, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            error(IngestionSourceFileIsEmpty raw)
        elif trimmed.Length > maxLength then
            error(IngestionSourceFileTooLong(raw, maxLength))
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

/// AccountValidationType says whether a staged line may still lack an account: allowed while staging, not at posting.
type AccountValidationType =
    | AllowNone
    | DisallowNone

type FetchStageEntrySort =
    | EntryDateAsc
    | EntryDateDesc
    | FiAsc
    | FiDesc
    | StatusAsc
    | StatusDesc
    | DescriptionAsc
    | DescriptionDesc

type BaseStageEntryGroupId = private BaseStageEntryGroupId of string

module BaseStageEntryGroupId =
    let maxLength = 36 // accommodates a uuid if needed
    let value (BaseStageEntryGroupId gid) = gid
    let create (raw: string) : Result<BaseStageEntryGroupId, IAppError> =
        let trimmed = raw.Trim()
        if String.IsNullOrWhiteSpace trimmed then
            Error(IngestionBaseStageEntryGroupIdIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionBaseStageEntryGroupIdTooLong(raw, maxLength))
        else
            Ok(BaseStageEntryGroupId trimmed)

type BaseStageRawRow =
    private {
        baseStageEntryGroupId: BaseStageEntryGroupId
        entryDate: LocalDate
        description: JournalEntryDescription
        fiSource: JournalRefFinancialInstitution
        fiReference: JournalExternalReferenceText
        amount: Money.Money
        entryType: JournalEntryLineType
        accountId: AccountId option
        memo: JournalEntryLineMemo option
    }

module BaseStageRawRow =
    let baseStageEntryGroupId (row: BaseStageRawRow) = row.baseStageEntryGroupId
    let entryDate (row: BaseStageRawRow) = row.entryDate
    let description (row: BaseStageRawRow) = row.description
    let fiSource (row: BaseStageRawRow) = row.fiSource
    let fiReference (row: BaseStageRawRow) = row.fiReference
    let amount (row: BaseStageRawRow) = row.amount
    let entryType (row: BaseStageRawRow) = row.entryType
    let accountId (row: BaseStageRawRow) = row.accountId
    let memo (row: BaseStageRawRow) = row.memo
    let create
        (baseStageEntryGroupId: BaseStageEntryGroupId)
        (entryDate: LocalDate)
        (description: JournalEntryDescription)
        (fiSource: JournalRefFinancialInstitution)
        (fiReference: JournalExternalReferenceText)
        (amount: Money.Money)
        (entryType: JournalEntryLineType)
        (accountId: AccountId option)
        (memo: JournalEntryLineMemo option)
        : BaseStageRawRow =
        { baseStageEntryGroupId = baseStageEntryGroupId
          entryDate = entryDate
          description = description
          fiSource = fiSource
          fiReference = fiReference
          amount = amount
          entryType = entryType
          accountId = accountId
          memo = memo }
