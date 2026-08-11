namespace Model.DataIngestion.Classification

open Model.Ledger.Accounts.AccountComponent
open NodaTime

/// ClassificationRule: The top-level classification rule. All groups must resolve to true for the rule to resolve to
/// true.
type ClassificationRule =
    private {
        classificationRuleId: ClassificationRuleId
        classificationRuleName: ClassificationRuleName
        codeAtMatch: AccountCode
        priority: int // lower number wins when multiple rules match
        ruleGroups: ClassificationRuleGroup list
        isActive: bool
        createdAt: Instant
        modifiedAt: Instant
    }
    
module ClassificationRule =
    
    let classificationRuleId (a: ClassificationRule) = a.classificationRuleId
    let codeAtMatch (a: ClassificationRule) = a.codeAtMatch
    let priority (a: ClassificationRule) = a.priority
    let ruleGroups (a: ClassificationRule) = a.ruleGroups
    let isActive (a: ClassificationRule) = a.isActive
    let createdAt (a: ClassificationRule) = a.createdAt
    let modifiedAt (a: ClassificationRule) = a.modifiedAt
    
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
        
