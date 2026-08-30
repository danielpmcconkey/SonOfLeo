module Model.DataIngestion.Classification.Classifier

open Model.DataIngestion.Classification.ClassificationRule
open Model.DataIngestion.Classification.ClassificationRuleComponent

let classifyCandidate
    (rules: ClassificationRule list)
    (candidate: MatchCandidate)
    : ClassificationResult =
    let matches =
        rules
        |> List.filter(isActive)
        |> List.filter(fun r -> r |> doesMatch candidate)
        |> List.map(fun x -> {
            accountId = x |> accountIdAtMatch
            ruleId = x |> classificationRuleId
            priority = x |> priority
        })
    match matches |> List.length with
    | 0 -> { candidate = candidate; outcome = NoMatch; }
    | 1 -> { candidate = candidate; outcome = OneMatch (matches |> List.head); }
    | _ ->
        let lowestPriority = matches |> List.map _.priority |> List.min
        let matchesAtLowest = matches |> List.filter(fun x -> x.priority = lowestPriority)
        if matchesAtLowest |> List.length = 1
        then  { candidate = candidate; outcome = ManyMatchesClearWinner (matchesAtLowest |> List.head, matches); }
        else { candidate = candidate; outcome = ManyMatchesTied matches }
    
let classify
    (rules: ClassificationRule list)
    (candidates: MatchCandidate list)
    : ClassificationResult list =
    (*
    At the start, we ensure that the list is active only. We do *not* check that the account code is already None.
    Presumably, someone sent us this list to classify. We're not overwriting, just letting the caller know which rules
    matched. We also don't sort here. We let the caller figure out what to do with multiple matches
     *)
    let rulesActive =
        rules
        |> List.filter(isActive)
    candidates
    |> List.map (classifyCandidate rulesActive)

