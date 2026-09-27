module Business.FinancialServices.Classification.Classifier

open System.Text.RegularExpressions
open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices.DataIngestion.DataIngestionError
open Business.FinancialServices.Classification.ClassificationRule
open Business.FinancialServices.Classification.ClassificationComponent

/// A pattern that runs past its time limit fails the run with a typed error naming the rule, never an exception.
let private ruleMatches (candidate: MatchCandidate) (rule: ClassificationRule) : Result<bool, IAppError> =
    try
        Ok(rule |> doesMatch candidate)
    with :? RegexMatchTimeoutException as ex ->
        let ruleUuid = rule |> classificationRuleId |> ClassificationRuleId.value
        Error(IngestionClassificationRulePatternTimedOut(ruleUuid, ex.Pattern))

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
            let accountIdOption, paymentAgreementIdOption =
                match matchCandidate |> classificationClaimant with
                | Account x -> Some x, None
                | PaymentAgreement x -> None, Some x
            { accountId = accountIdOption
              paymentAgreementId = paymentAgreementIdOption
              ruleId = matchCandidate |> classificationRuleId
              priority = matchCandidate |> priority })
    return
        match matches |> List.length with
        | 0 -> { candidate = candidate; outcome = NoMatch; }
        | 1 -> { candidate = candidate; outcome = OneMatch (matches |> List.head); }
        | _ ->
            let lowestPriority = matches |> List.map _.priority |> List.min
            let matchesAtLowest = matches |> List.filter(fun x -> x.priority = lowestPriority)
            if matchesAtLowest |> List.length = 1
            then  { candidate = candidate; outcome = ManyMatchesClearWinner (matchesAtLowest |> List.head, matches); }
            else { candidate = candidate; outcome = ManyMatchesTied matches }
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

