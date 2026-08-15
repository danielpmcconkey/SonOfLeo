module ModelOrchestrator.ClassificationOrchestration

open System

open DataAccessLayer.ExecuteReader
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryLine
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open Utilities.FieldUpdate

let createNewClassificationRule
    (context: Context.Context)
    (classificationRuleName: ClassificationRuleName)
    (codeAtMatch: AccountCode)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
    (isActive: bool)
    : Result<ClassificationRule.ClassificationRule, AppError> = 
    let classificationRuleId = ClassificationRuleId.create()
    let instant = context |> Context.getInitiationInstant
    let newRule =
        ClassificationRule.create
            classificationRuleId
            classificationRuleName
            codeAtMatch
            priority
            ruleGroups
            isActive
            instant
            instant
    result {
        do! newRule |> ClassificationRule.insertNewToDb context
        return newRule
    }

let fetchRulesFiltered
    (context: Context.Context)
    (filter: ClassificationRuleFilter)
    (sort: FetchSortClassificationRule option)
    : Result<ClassificationRule.ClassificationRule list, AppError> =
    result {
        let sourcePredicate = """
            and EXISTS (
                SELECT 1
                FROM jsonb_array_elements(cr.rule_groups) AS rg,
                     jsonb_array_elements(
                        CASE
                            WHEN rg.value -> 'chainTwo' IS NOT NULL
                                AND rg.value -> 'chainTwo' != 'null'::jsonb
                            THEN (rg.value -> 'chainOne' -> 'chain') || (rg.value -> 'chainTwo' -> 'chain')
                            ELSE rg.value -> 'chainOne' -> 'chain'
                        END
                ) AS fm
                WHERE fm.value ->> 'Case' = 'Source'
                AND fm.value -> 'Fields' ->> 0 LIKE @source_like
            )
            """
        
        let activeClause =
            match filter.activeOnly with
            | true -> "and cr.is_active = true"
            | false -> ""
            
        let sortClause =
            match sort with
            | None -> ""
            | Some AccountCodeAsc -> "order by cr.code_at_match asc"
            | Some AccountCodeDesc -> "order by cr.code_at_match desc"
            | Some PriorityAsc -> "order by cr.priority asc"
            | Some PriorityDesc -> "order by cr.priority desc"
            
        let whereClausesAndParams =
            [ filter.ruleId
              |> Option.map(fun x ->
                  ("and cr.unique_id = @rule_id", { name = "@rule_id"; value = UniqueId(x |> ClassificationRuleId.value) }))
        
              filter.nameLike
              |> Option.map(fun x ->
                  let ruleName = x |> ClassificationRuleName.value
                  ("and cr.rule_name like @rule_name",
                   { name = "@rule_name"; value = CharString $"%%{ruleName}%%"}))
        
              filter.codeAtMatch
              |> Option.map(fun x ->
                  ("and cr.code_at_match = @cr.code_at_match",
                   { name = "@cr.code_at_match"; value = CharString(x |> AccountCode.value) }))
        
              filter.sourceLike
              |> Option.map(fun x ->
                  (sourcePredicate, { name = "@source_like"; value = CharString $"%%{x}%%" }))
            ]
            |> List.choose id
        let whereClauses = whereClausesAndParams |> List.map fst |> String.concat Environment.NewLine
        let parameters = whereClausesAndParams |> List.map snd
        let query =
            $"""
            select
                cr.unique_id, cr.rule_name, cr.code_at_match, cr.priority,
                cr.rule_groups, cr.is_active, cr.created_at, cr.modified_at
            from ingestion.classification_rule cr
            where 1 = 1
            {whereClauses}
            {activeClause}
            {sortClause}
            """
        return!
            executeReaderQuery
                (context |> Context.getDatabaseTransaction)
                query
                parameters
                ClassificationRule.mapRawForDbRead
                ClassificationRule.reconstitute
                AnyQuantityIsAcceptable
    }

let updateLineWithMatch
    (context: Context.Context)
    (prioritizedMatch: PrioritizedMatch)
    (candidate: MatchCandidate)
    : Result<unit, AppError> =
    let code = Some prioritizedMatch.code // we can trust this because the DB has a foreign key constraint between ingestion.classification_rule and ledger.account 
    let codeUpdate = FieldUpdate.SetTo code
    let ruleId = Some prioritizedMatch.ruleId
    let ruleUpdate = FieldUpdate.SetTo ruleId
    match updateCodeAndRuleId context codeUpdate ruleUpdate candidate.stageEntryLineId with
    | Ok _ -> Ok ()
    | Error e -> Error e

let updateDbFromResultsList
    (context: Context.Context)
    (results: ClassificationResult list)
    : Result<unit, AppError> =
    result {
        // first update the line
        let! _ =
            results
            |> List.map (fun result ->
                    let candidate = result.candidate
                    match result.outcome with
                    | NoMatch -> Ok ()
                    | OneMatch prioritizedMatch -> candidate |> updateLineWithMatch context prioritizedMatch
                    | ManyMatchesClearWinner (winner, _) -> candidate |> updateLineWithMatch context winner
                    | ManyMatchesTied _ -> Ok () // no line update today
                )
            |> convertListOfResultsToResultsList
        // now update the header and status
        return ()
        }
    
let classifyMatchCandidatesAndUpdateLines
    (context: Context.Context)
    (candidates: MatchCandidate list)
    : Result<ClassificationResult list, AppError> =
    result {
        let ruleFilter =  {
            ruleId = None
            nameLike = None
            codeAtMatch = None
            sourceLike = None
            activeOnly = true }
        let! rules = fetchRulesFiltered context ruleFilter None
        let classificationResults = Classifier.classify rules candidates
        return classificationResults
    }
