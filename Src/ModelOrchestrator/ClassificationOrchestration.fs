module ModelOrchestrator.ClassificationOrchestration

open System
open Context.Context
open DataAccessLayer.ExecuteReader
open Model.DataIngestion.Classification
open Model.DataIngestion.Classification.ClassificationRule
open Model.Ledger.Accounts.AccountComponent
open ModelOrchestrator.FetchFilters
open NodaTime
open Utilities.AppError
open Utilities.Json
open Utilities.ResultHelper
open DataAccessLayer.QueryParameters

let createNewClassificationRule
    (context: Context)
    (classificationRuleName: ClassificationRuleName)
    (codeAtMatch: AccountCode)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
    (isActive: bool)
    : Result<ClassificationRule, AppError> = 
    let classificationRuleId = ClassificationRuleId.create()
    let instant = context |> getInitiationInstant
    let newRule =
        create
            classificationRuleId
            classificationRuleName
            codeAtMatch
            priority
            ruleGroups
            isActive
            instant
            instant
    result {
        do! newRule |> insertNewToDb context
        return newRule
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "rule_name"),
    (row |> RowReader.getString "code_at_match"),
    (row |> RowReader.getInt "priority"),
    (row |> RowReader.getString "rule_groups"),
    (row |> RowReader.getBool "is_active"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private reconstitute
    (raw: Guid * string * string * int * string * bool * Instant * Instant)
    : Result<ClassificationRule, AppError> =
    let uuid, nameStr, codeStr, priority, ruleGroupsStr, isActive, createdAt, modifiedAt = raw
    result {
        let classificationRuleId = uuid |> ClassificationRuleId.fromGuid
        let! classificationRuleName = nameStr |> ClassificationRuleName.create
        let! codeAtMatch = codeStr |> AccountCode.create
        let! ruleGroups = ruleGroupsStr |> Json.fromJson<ClassificationRuleGroup list>
        return create
                classificationRuleId
                classificationRuleName
                codeAtMatch
                priority
                ruleGroups
                isActive
                createdAt
                modifiedAt }

let fetchFiltered
    (context: Context)
    (filter: ClassificationRuleFilter)
    (sort: FetchSortClassificationRule option)
    : Result<ClassificationRule list, AppError> =
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
                (context |> getDatabaseTransaction)
                query
                parameters
                mapRawForDbRead
                reconstitute
                AnyQuantityIsAcceptable
    }
