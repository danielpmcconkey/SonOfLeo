namespace Model.DataIngestion.Classification

open System
open Model
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
    
type MoneySearchPattern = {
        numericSearchOperator: NumericSearchOperator
        amount: Money
    }

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

type MatchCandidate = {
        ingestionSource: JournalRefFinancialInstitution
        description: JournalEntryDescription
        amount: Money
        lineType: JournalEntryLineType
        memo: JournalEntryLineMemo option
}
