namespace Model.DataIngestion.Classification

open Model.Ledger.Accounts.AccountComponent

/// ClassificationRule: The top-level classification rule. All groups must resolve to true for the rule to resolve to
/// true.
type ClassificationRule =
    private {
        classificationRuleId: ClassificationRuleId
        codeAtMatch: AccountCode
        priority: int // lower number wins when multiple rules match
        ruleGroups: ClassificationRuleGroup list
    }

module ClassificationRule =
    let doesMatch
        (candidate: MatchCandidate)
        (classificationRule: ClassificationRule)
        : bool =
        let failureCount =
            classificationRule.ruleGroups
            |> List.map (fun ruleGroup ->
                ruleGroup |> ClassificationRuleGroup.doesMatch candidate)
            |> List.filter(fun x -> x = false)
            |> List.length
        failureCount = 0
        
