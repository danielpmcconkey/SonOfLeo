namespace Model.DataIngestion.Classification


/// ClassificationRuleGroup: two chains with an "and" or "or" relationship. If "and" then all FieldMatch comparisons in
/// both chains must resolve to true for the group to resolve to true. If "or" then all FieldMatch comparisons in one or
/// both of the chains must resolve to true for the group to resolve to true.
type ClassificationRuleGroup =
    private {
        connector: ClassificationGroupConnector
        chainOne: FieldMatchChain
        chainTwo: FieldMatchChain option
    }

module ClassificationRuleGroup =
    
    let connector g = g.connector
    let chainOne g = g.chainOne
    let chainTwo g = g.chainTwo
    
    let create
        (connector: ClassificationGroupConnector)
        (chainOne: FieldMatchChain)
        (chainTwo: FieldMatchChain option) =
        {
            connector = connector
            chainOne = chainOne
            chainTwo = chainTwo }
    
    let doesMatch
        (candidate: MatchCandidate)
        (classificationRuleGroup: ClassificationRuleGroup)
        : bool =
        let chainOneMatches = classificationRuleGroup.chainOne |> FieldMatchChain.doesMatch candidate
        match classificationRuleGroup.chainTwo with
        | None -> chainOneMatches
        | Some x ->
            let chainTwoMatches = x |> FieldMatchChain.doesMatch candidate
            match classificationRuleGroup.connector with
            | And -> chainOneMatches && chainTwoMatches
            | Or -> chainOneMatches || chainTwoMatches

