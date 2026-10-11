module Business.FinancialServices.Classification.FieldMatch

open System.Text.RegularExpressions
open Business.FinancialServices
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Classification.ClassificationComponent

type FieldMatch =
    | Source of StringSearchPattern
    | Description of StringSearchPattern
    | LineType of JournalEntryLineType
    | Amount of MoneySearchPattern

/// A pattern that backtracks without end is stopped here. Evaluation raises RegexMatchTimeoutException, which the
/// classifier turns into a typed error naming the rule.
let matchTimeout = System.TimeSpan.FromSeconds 1.0

// each pattern is built once, not once per candidate
let private regexCache = System.Collections.Concurrent.ConcurrentDictionary<string, Regex>()

let private isRegexMatch (source:string) (pattern:string) : bool =
    let rx = regexCache.GetOrAdd(pattern, fun p -> Regex(p, RegexOptions.None, matchTimeout))
    rx.IsMatch(source)

let private isMoneyMatch (source: Money.Money) (pattern: MoneySearchPattern): bool =
    match (pattern |> MoneySearchPattern.numericSearchOperator) with
    | GreaterThan -> Money.isGreaterThan source (pattern |> MoneySearchPattern.amount)
    | LessThan -> Money.isLessThan source (pattern |> MoneySearchPattern.amount)
    | GreaterThanOrEqualTo -> Money.isGreaterThanOrEqual source (pattern |> MoneySearchPattern.amount)
    | LessThanOrEqualTo -> Money.isLessThanOrEqual source (pattern |> MoneySearchPattern.amount)
    | ExactlyEqual -> Money.isEqual source (pattern |> MoneySearchPattern.amount)

let doesMatch
    (candidate: MatchCandidate)
    (fieldMatch: FieldMatch)
    : bool =
    match fieldMatch with
    | Source stringPattern ->
        let source = candidate |> MatchCandidate.ingestionSource |> JournalRefFinancialInstitution.value
        let pattern = stringPattern |> StringSearchPattern.value
        isRegexMatch source pattern
    | Description stringPattern ->
        let source = candidate |> MatchCandidate.description |> JournalEntryDescription.value
        let pattern = stringPattern |> StringSearchPattern.value
        isRegexMatch source pattern
    | LineType lineType ->
        (candidate |> MatchCandidate.lineType) = lineType
    | Amount pattern -> isMoneyMatch (candidate |> MatchCandidate.amount) pattern
