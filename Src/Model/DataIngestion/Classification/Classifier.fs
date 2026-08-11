module Model.DataIngestion.Classification.Classifier

open Model.DataIngestion.Classification.ClassificationRule

let classifyCandidate
    (rules: ClassificationRule list)
    (candidate: MatchCandidate)
    : ClassificationResult =
    let matches =
        rules
        |> List.filter(fun r -> r |> doesMatch candidate)
        |> List.map(fun x -> {
            code = x |> codeAtMatch
            ruleId = x |> classificationRuleId
            priority = x |> priority
        })
    match matches |> List.length with
    | 0 -> { candidate = candidate; outcome = NoMatch; }
    | 1 -> { candidate = candidate; outcome = OneMatch (matches |> List.head); }
    | _ -> { candidate = candidate; outcome = ManyMatches matches; }
    
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

