module Business.FinancialServices.Classification.Classifier

open System.Text.RegularExpressions
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.Classification.ClassificationError
open Business.FinancialServices.Classification.ClassificationComponent
open Business.FinancialServices.Classification.ClassificationRule

/// A pattern that runs past its time limit fails the run with a typed error naming the rule, never an exception.
let private ruleMatches (candidate: MatchCandidate) (rule: ClassificationRule) : Result<bool, IAppError> =
    try
        Ok(rule |> doesMatch candidate)
    with :? RegexMatchTimeoutException as ex ->
        let ruleUuid = rule |> classificationRuleId |> ClassificationRuleId.value
        Error(ClassificationRulePatternTimedOut(ruleUuid, ex.Pattern))

let classifyCandidate
    (rules: ClassificationRule list)
    (candidate: MatchCandidate)
    : Result<ClassificationResult, IAppError> =
    result {
    let! matchedRules =
        rules
        |> List.filter(isActive)
        |> List.map (fun r -> r |> ruleMatches candidate |> Result.map (fun matched -> r, matched))
        |> convertListOfResultsToResultsList
    let matches =
        matchedRules
        |> List.filter snd
        |> List.map fst
        |> List.map(fun matchCandidate ->
            (PrioritizedMatch.create (matchCandidate |> classificationClaimant) (matchCandidate |> classificationRuleId) (matchCandidate |> priority)))
    return
        match matches |> List.length with
        | 0 -> ClassificationResult.create candidate NoMatch
        | 1 -> ClassificationResult.create candidate (OneMatch (matches |> List.head))
        | _ ->
            let winningPriority = matches |> List.map PrioritizedMatch.priority |> List.reduce RulePriority.winner
            let matchesAtLowest = matches |> List.filter(fun x -> (x |> PrioritizedMatch.priority) = winningPriority)
            if matchesAtLowest |> List.length = 1
            then  ClassificationResult.create candidate (ManyMatchesClearWinner (matchesAtLowest |> List.head, matches))
            else ClassificationResult.create candidate (ManyMatchesTied matches)
    }
    
let classify
    (rules: ClassificationRule list)
    (candidates: MatchCandidate list)
    : Result<ClassificationResult list, IAppError> =
    // At the start, we ensure that the list is active only. We do *not* check that the account code is already None.
    // Presumably, someone sent us this list to classify. We're not overwriting, just letting the caller know which
    // rules matched. We also don't sort here. We let the caller figure out what to do with multiple matches
    let rulesActive =
        rules
        |> List.filter(isActive)
    candidates
    |> List.map (classifyCandidate rulesActive)
    |> convertListOfResultsToResultsList

