module Business.FinancialServices.DataIngestion.DataIngestionError

open System
open App.Utility.IAppError

/// IngestionRejectedRecord is one failure in a rejected file: the line or lines it concerns (counted from 1, blank lines
/// included), the group they belong to when that is known, and the error itself.
type IngestionRejectedRecord = { lineNumbers: int list; groupId: string option; error: IAppError }

type DataIngestionError = 
    | IngestionBaseStageEntryGroupIdIsEmpty of string
    | IngestionBaseStageEntryGroupIdTooLong of string * int
    | IngestionBaseStageGroupIdDistinctDataViolation of string
    | IngestionGroupsShareSourceAndReference of string * string * string * string
    | IngestionInvalidStagedEntryStatus of string
    | IngestionInvalidStageStatusChangeMechanism of string
    | IngestionInvalidStageStatusTransition of string option * string
    | IngestionStageLineNonPositiveAmount of decimal
    | IngestionStageEntryDebitCreditMismatch of decimal * decimal
    | IngestionStageEntryInsufficientLines of int
    | IngestionStageEntryHeaderIdDoesntExist of Guid
    | IngestionStageEntryLineIdDoesntExist of Guid
    | IngestionStageEntryLineIdListCannotBeEmpty
    | IngestionStageEntryHeaderNoOp
    | IngestionStageEntryLineNoOp
    | IngestionStageEntryLineNoMatchingJournalEntryLine of Guid
    | IngestionStageHeaderIdListCannotBeEmpty
    | IngestionSourceFileIsEmpty of string
    | IngestionSourceFileTooLong of string * int
    | IngestionStatusTransitionList
    | IngestionUpdateStageEntryLinesMustMatchHeader of Guid * Guid
    | IngestionNoneAccount of Guid
    | IngestionPrePostingReviewLineHasNoAccount of Guid * Guid
    | IngestionUpdateStageEntryNoOp
    | IngestionSourceNameNotFound of string
    | IngestionSourceNameAlreadyExists of string
    | IngestionStagedButFileNotMoved of string * string * string
    | IngestionFileRejected of string * IngestionRejectedRecord list
    | IngestionPostedStageEntryCannotBeModified of Guid
    | IngestionManualUpdateCannotSetStatus of string
    
    interface IAppError with
        member this.DomainName = nameof DataIngestionError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with    
            | IngestionBaseStageEntryGroupIdIsEmpty str -> $"BaseStageEntryGroupId cannot be empty. Provided value is {str}."
            | IngestionBaseStageEntryGroupIdTooLong (str, max) -> $"BaseStageEntryGroupId cannot exceed {max} characters. Provided value is {str}."
            | IngestionBaseStageGroupIdDistinctDataViolation str -> $"More than one combination of \"header\" data found for BaseStageEntryGroupId {str}"
            | IngestionGroupsShareSourceAndReference (firstGroup, secondGroup, source, reference) -> $"Groups {firstGroup} and {secondGroup} share source {source} and FI reference {reference}; no two groups in one file may."
            | IngestionInvalidStagedEntryStatus str -> $"Provided string of '{str}' is not a valid StagedEntryStatus."
            | IngestionInvalidStageStatusChangeMechanism str -> $"Provided string of '{str}' is not a valid StageStatusChangeMechanism."
            | IngestionInvalidStageStatusTransition (fromStr, toStr) -> $"Invalid stage status transition. Cannot move from {fromStr} to {toStr}."
            | IngestionStageEntryHeaderIdDoesntExist uuid -> $"Could not locate a stage entry header with the id of {uuid}."
            | IngestionStageEntryLineIdDoesntExist uuid -> $"Could not locate a stage entry line with the id of {uuid}."
            | IngestionStageEntryLineIdListCannotBeEmpty -> "The stageEntryLineIds list must contain at least 1 ID."
            | IngestionStageEntryHeaderNoOp -> "Updating the StageEntryHeader record failed because at least one updatable parameter must be set."
            | IngestionStageEntryLineNoOp -> "Updating the StageEntryLine record failed because at least one updatable parameter must be set."
            | IngestionStageEntryLineNoMatchingJournalEntryLine uuid -> $"Could not locate a journal entry line matching stage entry line {uuid} on account, line type, and amount."
            | IngestionStageHeaderIdListCannotBeEmpty -> "The stageEntryHeaderIds list must contain at least 1 Header ID."
            | IngestionStageLineNonPositiveAmount amount -> $"StageEntry Amount field ({amount}) cannot be less than or equal to 0.00."
            | IngestionStageEntryDebitCreditMismatch(debits, credits) -> $"Error in Base Stage Entry Group. The sum of all debit amounts ({debits}) must exactly equal the sum of all credit amounts ({credits})."
            | IngestionStageEntryInsufficientLines lineCount -> $"Insufficient number of lines ({lineCount}) for a stage entry. At least two are required."
            | IngestionSourceFileIsEmpty str -> $"Ingestion source file cannot be empty. Provided value is {str}."
            | IngestionSourceFileTooLong (str, max) -> $"Ingestion source file cannot exceed {max} characters. Provided value is {str}."
            | IngestionStatusTransitionList -> "StageEntryStatusTransition list cannot be empty."
            | IngestionUpdateStageEntryLinesMustMatchHeader (headerId, lineId) -> $"Error updating StageEntry {headerId}. Line {lineId} is for a different header."
            | IngestionNoneAccount uuid -> $"Stage Entry Line with an account of None is not allowed at this phase of the ingestion pipeline. Line ID: {uuid}"
            | IngestionPrePostingReviewLineHasNoAccount (entryId, lineId) -> $"Staged entry {entryId} is postable but its line {lineId} has no account. Shadow post fails on such a line, so run it and fix the line before the pre-posting review."
            | IngestionUpdateStageEntryNoOp -> "updateStageEntry failed because at least one updatable parameter must be set."
            | IngestionSourceNameNotFound str -> $"No ingestion source of {str} could be found."
            | IngestionSourceNameAlreadyExists str -> $"An ingestion source named {str} already exists. Source names must be unique."
            | IngestionStagedButFileNotMoved (filePath, targetPath, reason) -> $"The entries in {filePath} were staged and committed, but the file could not be moved to {targetPath} ({reason}). Move it by hand. Ingesting it again would stage the same entries a second time, which dedup would then flag."
            | IngestionPostedStageEntryCannotBeModified uuid ->
                $"Staged entry {uuid} is Posted and cannot be modified. Void its journal entry to return it to review."
            | IngestionManualUpdateCannotSetStatus status ->
                $"The manual update cannot set a staged entry's status to {status}; only batch post does that."
            | IngestionFileRejected (filePath, records) ->
                let describe (record: IngestionRejectedRecord) =
                    let lines =
                        match record.lineNumbers with
                        | [ one ] -> $"line {one}"
                        | many -> many |> List.map string |> String.concat ", " |> sprintf "lines %s"
                    let group = record.groupId |> Option.map (sprintf ", group %s") |> Option.defaultValue ""
                    $"  {lines}{group}: {record.error.ToMessage()}"
                let problems = if records.Length = 1 then "1 problem" else $"{records.Length} problems"
                (records |> List.map describe)
                |> String.concat Environment.NewLine
                |> sprintf "%s was rejected and nothing was staged. %s:%s%s" filePath problems Environment.NewLine
            
let toMessage (e: DataIngestionError) = (e :> IAppError).ToMessage()
let toAppError (e: DataIngestionError) : IAppError = e :> IAppError
let error (e: DataIngestionError) : Result<'T, IAppError> = Error (e :> IAppError)
