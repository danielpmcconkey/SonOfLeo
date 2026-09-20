module Business.FinancialServices.Classification.FieldMatch

open System.Text.RegularExpressions
open Business.FinancialServices
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification.ClassificationComponent

type FieldMatch =
    | Source of StringSearchPattern
    | Description of StringSearchPattern
    | Memo of StringSearchPattern
    | LineType of JournalEntryLineType
    | Amount of MoneySearchPattern

let private isRegexMatch (source:string) (pattern:string) : bool =
    let rx = Regex(pattern, RegexOptions.Compiled)
    rx.IsMatch(source)

let private isMoneyMatch (source: Money.Money) (pattern: MoneySearchPattern): bool =
    let valueToCompare = source |> Money.amount
    let valueToCompareAgainst = pattern.amount |> Money.amount
    match pattern.numericSearchOperator with
    | GreaterThan -> valueToCompare > valueToCompareAgainst
    | LessThan -> valueToCompare < valueToCompareAgainst
    | GreaterThanOrEqualTo -> valueToCompare >= valueToCompareAgainst
    | LessThanOrEqualTo -> valueToCompare <= valueToCompareAgainst
    | ExactlyEqual -> valueToCompare = valueToCompareAgainst

let doesMatch
    (candidate: MatchCandidate)
    (fieldMatch: FieldMatch)
    : bool =
    match fieldMatch with
    | Source stringPattern ->
        let source = candidate.ingestionSource |> JournalRefFinancialInstitution.value
        let pattern = stringPattern |> StringSearchPattern.value
        isRegexMatch source pattern
    | Description stringPattern ->
        let source = candidate.description |> JournalEntryDescription.value
        let pattern = stringPattern |> StringSearchPattern.value
        isRegexMatch source pattern
    | Memo stringPattern ->
        match candidate.memo with
        | None -> false
        | Some x ->
            let source = x |> JournalEntryLineMemo.value
            let pattern = stringPattern |> StringSearchPattern.value
            isRegexMatch source pattern
    | LineType lineType ->
        candidate.lineType = lineType
    | Amount pattern -> isMoneyMatch candidate.amount pattern
