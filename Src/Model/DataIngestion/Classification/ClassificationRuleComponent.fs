namespace Model.DataIngestion.Classification

open System
open Model
open Model.DataIngestion
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError



type ClassificationRuleId = private ClassificationRuleId of Guid

module ClassificationRuleId =
    let create () : ClassificationRuleId = ClassificationRuleId(Guid.NewGuid())
    let fromGuid g = ClassificationRuleId g
    let value (ClassificationRuleId g) : Guid = g

type NumericSearchOperator =
    | GreaterThan
    | LessThan
    | GreaterThanOrEqualTo
    | LessThanOrEqualTo
    | ExactlyEqual

module NumericSearchOperator =
    let toString o =
        match o with
        | GreaterThan -> "GreaterThan"
        | LessThan -> "LessThan"
        | GreaterThanOrEqualTo -> "GreaterThanOrEqualTo"
        | LessThanOrEqualTo -> "LessThanOrEqualTo"
        | ExactlyEqual -> "ExactlyEqual"
        
    let fromString str =
        match str with
        | "GreaterThan" -> Ok GreaterThan
        | "LessThan" -> Ok LessThan
        | "GreaterThanOrEqualTo" -> Ok GreaterThanOrEqualTo
        | "LessThanOrEqualTo" -> Ok LessThanOrEqualTo
        | "ExactlyEqual" -> Ok ExactlyEqual
        | _ -> Error (IngestionInvalidNumericSearchOperator str)
    
type MoneySearchPattern = {
        numericSearchOperator: NumericSearchOperator
        amount: Money
    }

type ClassificationRuleName = private ClassificationRuleName of string

module ClassificationRuleName =
    let maxLength = 250
    let value (ClassificationRuleName reference) = reference 
    let create (raw: string) : Result<ClassificationRuleName, AppError> =
        let trimmed = raw.Trim()
        if trimmed = String.Empty then
            Error(IngestionClassificationRuleNameIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionClassificationRuleNameTooLong(raw, maxLength))
        else
            Ok(ClassificationRuleName trimmed)

type StringSearchPattern = private StringSearchPattern of string

module StringSearchPattern =
    let maxLength = 500
    let value (StringSearchPattern reference) = reference 
    let create (raw: string) : Result<StringSearchPattern, AppError> =
        let trimmed = raw.Trim()
        if trimmed = String.Empty then
            Error(IngestionSearchPatternIsEmpty raw)
        elif trimmed.Length > maxLength then
            Error(IngestionSearchPatternTooLong(raw, maxLength))
        else
            Ok(StringSearchPattern trimmed)

    
type ClassificationGroupConnector =
    | And
    | Or

module ClassificationGroupConnector =
    
    let toString c =
        match c with
        | And -> "And"
        | Or -> "Or"
    
    let fromString str =
        match str with
        | "And" -> Ok And
        | "Or" -> Ok Or
        | _ -> Error (IngestionInvalidClassificationGroupConnector str)

type MatchCandidate = {
        stageEntryHeaderId: StageEntryHeaderId
        stageEntryLineId: StageEntryLineId
        ingestionSource: JournalRefFinancialInstitution
        description: JournalEntryDescription
        amount: Money
        lineType: JournalEntryLineType
        memo: JournalEntryLineMemo option
}

type PrioritizedMatch = {
    code: AccountCode
    ruleId: ClassificationRuleId
    priority: int
}

type ClassifierOutcome =
    | NoMatch
    | OneMatch of PrioritizedMatch
    | ManyMatchesClearWinner of PrioritizedMatch * PrioritizedMatch list
    | ManyMatchesTied of PrioritizedMatch list 

type ClassificationResult = {
        candidate: MatchCandidate
        outcome: ClassifierOutcome
    }
