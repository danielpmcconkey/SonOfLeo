module Business.FinancialServices.DataIngestion.DataIngestionError

open System
open App.Utility.IAppError

type DataIngestionError = 
    | IngestionBaseStageEntryGroupIdIsEmpty of string
    | IngestionBaseStageEntryGroupIdTooLong of string * int
    | IngestionBaseStageGroupIdDistinctDataViolation of string
    | IngestionClassificationRuleGroupsEmpty
    | IngestionClassificationRuleIdDoesntExist of Guid
    | IngestionClassificationRuleIdListCannotBeEmpty
    | IngestionClassificationRuleInvalidClaimant of Guid * Guid option * Guid option
    | IngestionClassificationRuleNameIsEmpty of string
    | IngestionClassificationRuleNameTooLong of string * int 
    | IngestionClassificationRuleUpdateNoOp
    | IngestionFieldMatchChainEmpty
    | IngestionInvalidClassificationClaimantType of string
    | IngestionInvalidClassificationGroupConnector of string
    | IngestionInvalidNumericSearchOperator of string
    | IngestionInvalidStagedEntryStatus of string
    | IngestionInvalidStageStatusChangeMechanism of string
    | IngestionInvalidStageStatusTransition of string option * string
    | IngestionSearchPatternIsEmpty of string
    | IngestionSearchPatternTooLong of string * int
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
    | IngestionUpdateStageEntryNoOp
    | IngestionSourceNameNotFound of string
    
    interface IAppError with
        member this.DomainName = nameof DataIngestionError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with    
            | IngestionBaseStageEntryGroupIdIsEmpty str -> $"BaseStageEntryGroupId cannot be empty. Provided value is {str}."
            | IngestionBaseStageEntryGroupIdTooLong (str, max) -> $"BaseStageEntryGroupId cannot exceed {max} characters. Provided value is {str}."
            | IngestionBaseStageGroupIdDistinctDataViolation str -> $"More than one combination of \"header\" data found for BaseStageEntryGroupId {str}"
            | IngestionClassificationRuleGroupsEmpty -> "A ClassificationRule's ClassificationRuleGroup list cannot be empty."
            | IngestionClassificationRuleIdDoesntExist uuid -> $"Could not locate a ClassificationRule with the id of {uuid}."
            | IngestionClassificationRuleIdListCannotBeEmpty -> "The classificationRuleIds list must contain at least 1 ID."
            | IngestionClassificationRuleInvalidClaimant(ruleUuid, accountUuid, paymentAgreementUuid) ->
                let accountStr =
                    match accountUuid with
                    | Some x -> x.ToString()
                    | None -> "None"
                let paymentAgreementStr =
                    match paymentAgreementUuid with
                    | Some x -> x.ToString()
                    | None -> "None"
                $"ClassificationRule ({ruleUuid}) must claim exactly one of an account or a payment agreement. Account at match is {accountStr}; payment agreement at match is {paymentAgreementStr}."
            | IngestionClassificationRuleNameIsEmpty str -> $"ClassificationRuleName cannot be empty. Provided value is {str}."
            | IngestionClassificationRuleNameTooLong (str, max) -> $"ClassificationRuleName cannot exceed {max} characters. Provided value is {str}."
            | IngestionClassificationRuleUpdateNoOp -> "Updating the ClassificationRule record failed because at least one updatable parameter must be set."
            | IngestionFieldMatchChainEmpty -> "A FieldMatchChain's chain cannot be empty."
            | IngestionInvalidClassificationClaimantType str -> $"Invalid ClassificationClaimantType of \"{str}\"."
            | IngestionInvalidClassificationGroupConnector str -> $"Invalid ClassificationConnector of \"{str}\"."
            | IngestionInvalidNumericSearchOperator str -> $"Invalid NumericSearchOperator of \"{str}\"."
            | IngestionInvalidStagedEntryStatus str -> $"Provided string of '{str}' is not a valid StagedEntryStatus."
            | IngestionInvalidStageStatusChangeMechanism str -> $"Provided string of '{str}' is not a valid StageStatusChangeMechanism."
            | IngestionInvalidStageStatusTransition (fromStr, toStr) -> $"Invalid stage status transition. Cannot move from {fromStr} to {toStr}."
            | IngestionSearchPatternIsEmpty str -> $"SearchPattern cannot be empty. Provided value is {str}."
            | IngestionSearchPatternTooLong (str, max) -> $"SearchPattern cannot exceed {max} characters. Provided value is {str}."
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
            | IngestionUpdateStageEntryNoOp -> "updateStageEntry failed because at least one updatable parameter must be set."
            | IngestionSourceNameNotFound str -> $"No ingestion source of {str} could be found."
            
let toMessage (e: DataIngestionError) = (e :> IAppError).ToMessage()
let toAppError (e: DataIngestionError) : IAppError = e :> IAppError
let error (e: DataIngestionError) : Result<'T, IAppError> = Error (e :> IAppError)
