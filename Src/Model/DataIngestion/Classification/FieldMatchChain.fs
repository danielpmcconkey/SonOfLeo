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
        let failureCount =
            fieldMatchChain.chain
            |> List.map (fun fieldMatch ->
                fieldMatch |> FieldMatch.doesMatch candidate)
            |> List.filter(fun x -> x = false)
            |> List.length
        failureCount = 0
