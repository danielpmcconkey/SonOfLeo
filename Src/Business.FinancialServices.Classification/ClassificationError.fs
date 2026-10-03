module Business.FinancialServices.Classification.ClassificationError

open System
open App.Utility.IAppError

/// StageLineProtection is why a staged line cannot be removed or have its amount, line type or account changed
/// (REQ-STG-6.5).
type StageLineProtection =
    | LinkedToPaymentAgreement
    | ReferencedByPayment
    | RecordedInClassificationRun

let private describeProtection (protection: StageLineProtection) =
    match protection with
    | LinkedToPaymentAgreement -> "it is linked to a payment agreement; remove the link first"
    | ReferencedByPayment -> "a Payment references it; remove the Payment first"
    | RecordedInClassificationRun -> "a classification run recorded it, and those records are permanent"

type ClassificationError =
    | ClassificationRuleGroupsEmpty
    | ClassificationRuleIdDoesntExist of Guid
    | ClassificationRuleIdListCannotBeEmpty
    | ClassificationRuleInvalidClaimant of Guid * Guid option * Guid option
    | ClassificationRuleNameIsEmpty of string
    | ClassificationRuleNameTooLong of string * int 
    | ClassificationRuleUpdateNoOp
    | ClassificationFieldMatchChainEmpty
    | ClassificationInvalidClaimantType of string
    | ClassificationInvalidGroupConnector of string
    | ClassificationInvalidNumericSearchOperator of string
    | ClassificationSearchPatternIsEmpty of string
    | ClassificationSearchPatternTooLong of string * int
    | ClassificationSearchPatternInvalidRegex of string * string
    | ClassificationRuleStoredPatternInvalid of Guid * string * string
    | ClassificationRuleStoredGroupsInvalid of Guid * string
    | ClassificationRulePatternTimedOut of Guid * string
    | ClassificationStageEntryLineCannotBeRemoved of Guid * StageLineProtection
    | ClassificationStageEntryLineCannotBeChanged of Guid * StageLineProtection
    | ClassificationPaidStageEntryCannotBeExcluded of Guid * string

    interface IAppError with
        member this.DomainName = nameof ClassificationError
        member this.CaseName = getUnionCaseName this
        member this.ToMessage() =
            match this with
            | ClassificationRuleGroupsEmpty -> "A ClassificationRule's ClassificationRuleGroup list cannot be empty."
            | ClassificationRuleIdDoesntExist uuid -> $"Could not locate a ClassificationRule with the id of {uuid}."
            | ClassificationRuleIdListCannotBeEmpty -> "The classificationRuleIds list must contain at least 1 ID."
            | ClassificationRuleInvalidClaimant(ruleUuid, accountUuid, paymentAgreementUuid) ->
                let accountStr =
                    match accountUuid with
                    | Some x -> x.ToString()
                    | None -> "None"
                let paymentAgreementStr =
                    match paymentAgreementUuid with
                    | Some x -> x.ToString()
                    | None -> "None"
                $"ClassificationRule ({ruleUuid}) must claim exactly one of an account or a payment agreement. Account at match is {accountStr}; payment agreement at match is {paymentAgreementStr}."
            | ClassificationRuleNameIsEmpty str -> $"ClassificationRuleName cannot be empty. Provided value is {str}."
            | ClassificationRuleNameTooLong (str, max) -> $"ClassificationRuleName cannot exceed {max} characters. Provided value is {str}."
            | ClassificationRuleUpdateNoOp -> "Updating the ClassificationRule record failed because at least one updatable parameter must be set."
            | ClassificationFieldMatchChainEmpty -> "A FieldMatchChain's chain cannot be empty."
            | ClassificationInvalidClaimantType str -> $"Invalid ClassificationClaimantType of \"{str}\"."
            | ClassificationInvalidGroupConnector str -> $"Invalid ClassificationConnector of \"{str}\"."
            | ClassificationInvalidNumericSearchOperator str -> $"Invalid NumericSearchOperator of \"{str}\"."
            | ClassificationSearchPatternIsEmpty str -> $"SearchPattern cannot be empty. Provided value is {str}."
            | ClassificationSearchPatternTooLong (str, max) -> $"SearchPattern cannot exceed {max} characters. Provided value is {str}."
            | ClassificationSearchPatternInvalidRegex (pattern, reason) -> $"Search pattern {pattern} is not a valid regular expression: {reason}"
            | ClassificationRuleStoredPatternInvalid (ruleUuid, pattern, reason) -> $"Classification rule {ruleUuid} is stored with search pattern {pattern}, which is not a valid regular expression: {reason} Correct the rule before running classification."
            | ClassificationRuleStoredGroupsInvalid (ruleUuid, reason) -> $"Classification rule {ruleUuid} is stored with rule groups that cannot be read: {reason} Correct the rule before running classification."
            | ClassificationRulePatternTimedOut (ruleUuid, pattern) -> $"Classification rule {ruleUuid}'s search pattern {pattern} took too long to evaluate and was stopped. Simplify the pattern; the classification run did not complete."
            | ClassificationStageEntryLineCannotBeRemoved (uuid, protection) ->
                $"Staged line {uuid} cannot be removed because {protection |> describeProtection}."
            | ClassificationStageEntryLineCannotBeChanged (uuid, protection) ->
                $"Staged line {uuid} cannot have its amount, line type or account changed because {protection |> describeProtection}."
            | ClassificationPaidStageEntryCannotBeExcluded (uuid, status) ->
                $"Staged entry {uuid} cannot become {status}: a Payment references one of its lines."

let toMessage (e: ClassificationError) = (e :> IAppError).ToMessage()
let toAppError (e: ClassificationError) : IAppError = e :> IAppError
let error (e: ClassificationError) : Result<'T, IAppError> = Error (e :> IAppError)
