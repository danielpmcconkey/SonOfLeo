module ModelOrchestrator.ClassificationOrchestration

open System

open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open Model
open Model.DataIngestion.Classification
open Model.DataIngestion.StageEntryLine
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.FetchFilters
open Utilities.AppError
open Utilities.Json
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters
open Utilities.FieldUpdate

let private confirmAccountCode
    (context: Context.Context)
    (accountCode: AccountCode)
    : Result<unit, AppError> =
    let codeStr = accountCode |> AccountCode.value
    let confirmed = codeStr |> LookupCache.accountCodeToId.fetch context
    match confirmed with
    | Ok _ -> Ok ()
    | Error (DalResultantRowsDidntMatchExpectation _) -> Error (AccountCodeDoesntMatchAccountId codeStr)
    | Error e -> Error e

let private confirmFieldMatchChain
    (fieldMatchChain: FieldMatchChain)
    : Result<unit, AppError> =
    let chain = fieldMatchChain |> FieldMatchChain.chain
    if chain |> List.isEmpty then Error IngestionFieldMatchChainEmpty else Ok ()
    
let private confirmRuleGroup
    (ruleGroup: ClassificationRuleGroup)
    : Result<unit, AppError> = result {
        do! ruleGroup |> ClassificationRuleGroup.chainOne |> confirmFieldMatchChain
        do! match ruleGroup |> ClassificationRuleGroup.chainTwo with
            | None -> Ok ()
            | Some x -> x |> confirmFieldMatchChain
        return ()
    }
    
let private confirmRuleGroups
    (ruleGroups: ClassificationRuleGroup list)
    : Result<unit, AppError> = 
    if ruleGroups |> List.isEmpty then Error IngestionClassificationRuleGroupsEmpty
    else
        ruleGroups
        |> List.map(confirmRuleGroup)
        |> convertListOfResultsToResultsList
        |> Result.map ignore

let createNewClassificationRule
    (context: Context.Context)
    (classificationRuleName: ClassificationRuleName)
    (codeAtMatch: AccountCode)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
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
            true // no new rules that are already inactive
            instant
            instant
    result {
        do! ruleGroups |> confirmRuleGroups
        do! codeAtMatch |> confirmAccountCode context
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
            EXISTS (
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
            
        let orderBy =
            match sort with
            | None -> None
            | Some AccountCodeAsc -> Some "cr.code_at_match asc"
            | Some AccountCodeDesc -> Some "cr.code_at_match desc"
            | Some PriorityAsc -> Some "cr.priority asc"
            | Some PriorityDesc -> Some "cr.priority desc"
            
        let whereClausesAndParams =
            [ filter.ruleId
              |> Option.map(fun x ->
                  ("cr.unique_id = @rule_id", { name = "@rule_id"; value = UniqueId(x |> ClassificationRuleId.value) }))
        
              filter.nameLike
              |> Option.map(fun x ->
                  let ruleName = x |> ClassificationRuleName.value
                  ("cr.rule_name like @rule_name",
                   { name = "@rule_name"; value = CharString $"%%{ruleName}%%"}))
        
              filter.codeAtMatch
              |> Option.map(fun x ->
                  ("cr.code_at_match = @code_at_match",
                   { name = "@code_at_match"; value = CharString(x |> AccountCode.value) }))
        
              filter.sourceLike
              |> Option.map(fun x ->
                  (sourcePredicate, { name = "@source_like"; value = CharString $"%%{x}%%" }))
            ]
            |> List.choose id
        let whereClauses = whereClausesAndParams |> List.map fst |> String.concat $" and {Environment.NewLine}"        
        let predicate = Some $"""
            {if whereClausesAndParams |> List.isEmpty then "1 = 1" else whereClauses}
            {activeClause}
        """
        let limit = None
        let parameters = whereClausesAndParams |> List.map snd
        return!
            ClassificationRule.readRowsFromDb context predicate limit parameters orderBy AnyQuantityIsAcceptable
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
    match updateCodeAndRuleId context codeUpdate ruleUpdate candidate.lineIdOfCandidate with
    | Ok _ -> Ok ()
    | Error e -> Error e

let updateDbLinesFromResultsList
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
    
/// classifyMatchCandidatesAndUpdateLines only updates the lines. StageEntryOrchestration owns making sure that status
/// transitions are viable. This runs the risk of an "orphan" line update if subsequent updates to the entry or audit
/// table fail. But this should all be under one transaction. Caveat emptor if you use individual transactions for this.
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
        do! classificationResults |> updateDbLinesFromResultsList context
        return classificationResults
    }
    
let updateClassificationRule
    (context: Context.Context)
    (classificationRuleNameUpdate: FieldUpdate<ClassificationRuleName>)
    (codeAtMatchUpdate: FieldUpdate<AccountCode>)
    (priorityUpdate: FieldUpdate<int>)
    (ruleGroupsUpdate: FieldUpdate<ClassificationRuleGroup list>)
    (isActiveUpdate: FieldUpdate<bool>)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule.ClassificationRule, AppError> =
    let uuid = classificationRuleId |> ClassificationRuleId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    result {
        do! match codeAtMatchUpdate with
            | NoChange -> Ok ()
            | SetTo x -> x |> confirmAccountCode context
        do! match ruleGroupsUpdate with
            | NoChange -> Ok ()
            | SetTo x -> x |> confirmRuleGroups
        let! groupStr = // do this up here because it's a pain in the ass to do it down in the updates block
            match ruleGroupsUpdate with
            | NoChange -> Ok ""
            | SetTo x -> x |> Json.toJson<ClassificationRuleGroup list> 
        let updates =
            [
                  classificationRuleNameUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("rule_name = @rule_name",
                       { name = "@rule_name"; value = CharString(n |> ClassificationRuleName.value) }))
                  
                  codeAtMatchUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("code_at_match = @code_at_match",
                       { name = "@code_at_match"; value = CharString(n |> AccountCode.value) }))
                  
                  priorityUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("priority = @priority",
                       { name = "@priority"; value = Integer(n) }))
                  
                  ruleGroupsUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun _ ->
                      ("rule_groups = @rule_groups",
                       { name = "@rule_groups"; value = Jsonb(groupStr) }))
                  
                  isActiveUpdate
                  |> FieldUpdate.mapNoChangeToOptionWithConversion(fun n ->
                      ("is_active = @is_active",
                       { name = "@is_active"; value = Boolean(n) }))
            ]
            |> List.choose id
        let setClauses = updates |> List.map fst |> String.concat ", "
        let parameters = baseParams @ (updates |> List.map snd)
        let query =
            $"""
            UPDATE ingestion.classification_rule
            set
                {setClauses},
                modified_at = @modified
            WHERE unique_id = @unique_id;
        """
        do! if updates.IsEmpty then Error(IngestionClassificationRuleUpdateNoOp) else Ok()
        let! () = executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
        return! classificationRuleId |> ClassificationRule.fetchById context
    }
