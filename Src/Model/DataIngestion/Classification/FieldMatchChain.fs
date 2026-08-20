namespace Model.DataIngestion.Classification

/// FieldMatchChain: chain all FieldMatch elements into a single "and-connected" grouping. Meaning, all FieldMatch
/// comparisons must be true for the chain to be true 
type FieldMatchChain =
    private {
        chain: FieldMatch list
    }

module FieldMatchChain =
    
    let chain fmc = fmc.chain
    
    let create (chain: FieldMatch list) = { chain = chain }
    
    let doesMatch
        (candidate: MatchCandidate)
        (fieldMatchChain: FieldMatchChain) 
        : bool =
        fieldMatchChain.chain
        |> List.forall(fun fieldMatch ->
                fieldMatch |> FieldMatch.doesMatch candidate)
