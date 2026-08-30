module Model.DataIngestion.Classification.ClassificationRule

open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.Ledger.AccountComponent
open NodaTime
open Utilities.AppError
open Utilities.Json.Json
open Utilities.ResultHelper
open Model.DataIngestion.Classification.ClassificationRuleComponent
open Model.DataIngestion.Classification.ClassificationRuleGroup

/// ClassificationRule: The top-level classification rule. All groups must resolve to true for the rule to resolve to
/// true.
type ClassificationRule =
    private {
        classificationRuleId: ClassificationRuleId
        classificationRuleName: ClassificationRuleName
        accountIdAtMatch: AccountId
        priority: int // lower number wins when multiple rules match
        ruleGroups: ClassificationRuleGroup list
        isActive: bool
        createdAt: Instant
        modifiedAt: Instant
    }
        
let classificationRuleId (a: ClassificationRule) = a.classificationRuleId
let classificationRuleName (a: ClassificationRule) = a.classificationRuleName
let accountIdAtMatch (a: ClassificationRule) = a.accountIdAtMatch
let priority (a: ClassificationRule) = a.priority
let ruleGroups (a: ClassificationRule) = a.ruleGroups
let isActive (a: ClassificationRule) = a.isActive
let createdAt (a: ClassificationRule) = a.createdAt
let modifiedAt (a: ClassificationRule) = a.modifiedAt

let create
    (classificationRuleId: ClassificationRuleId)
    (classificationRuleName: ClassificationRuleName)
    (accountIdAtMatch: AccountId)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
    (isActive: bool)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : ClassificationRule = {
        classificationRuleId = classificationRuleId
        classificationRuleName = classificationRuleName
        accountIdAtMatch = accountIdAtMatch
        priority = priority
        ruleGroups = ruleGroups
        isActive = isActive
        createdAt = createdAt
        modifiedAt = modifiedAt
    }
    
let insertNewToDb (context: Context.Context) (classificationRule: ClassificationRule) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.classification_rule(
	        unique_id, rule_name, account_at_match, priority, rule_groups, is_active, created_at, modified_at)
        values (
	        @unique_id, 
            @rule_name, 
            @account_at_match, 
            @priority, 
            @rule_groups, 
            @is_active, 
            @created_at, 
            @modified_at);"""
    let uuid = classificationRule.classificationRuleId |> ClassificationRuleId.value
    let ruleName = classificationRule.classificationRuleName |> ClassificationRuleName.value
    let code = classificationRule.accountIdAtMatch |> AccountId.value
    let priority = classificationRule.priority
    let isActive = classificationRule.isActive
    let createdAt = classificationRule.createdAt
    let modifiedAt = classificationRule.modifiedAt
    result {
        let! ruleGroups = classificationRule.ruleGroups |> toJson<ClassificationRuleGroup list>
        let parameters =
            [
              { name = "@unique_id"; value = UniqueId(uuid) }
              { name = "@rule_name"; value = CharString(ruleName) }
              { name = "@account_at_match"; value = UniqueId(code) }
              { name = "@priority"; value = Integer(priority) }
              { name = "@rule_groups"; value = Jsonb(ruleGroups) }
              { name = "@is_active"; value = Boolean(isActive) }
              { name = "@created_at"; value = DbInstant createdAt }
              { name = "@modified_at"; value = DbInstant modifiedAt }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }
    
let private reconstitute raw =
    result {
        let (uuid,
             nameStr,
             accountUuid,
             priority,
             ruleGroupsStr,
             isActive,
             createdAt,
             modifiedAt) =
            raw
        let classificationRuleId = uuid |> ClassificationRuleId.fromGuid
        let! name = nameStr |> ClassificationRuleName.create
        let accountId = accountUuid |> AccountId.fromGuid
        let! ruleGroups = ruleGroupsStr |> fromJson<ClassificationRuleGroup list>
        return
            create
                classificationRuleId
                name
                accountId
                priority
                ruleGroups
                isActive
                createdAt
                modifiedAt
    }
    
let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "rule_name"),
    (row |> RowReader.getUuid "account_at_match"),
    (row |> RowReader.getInt "priority"),
    (row |> RowReader.getString "rule_groups"),
    (row |> RowReader.getBool "is_active"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let readRowsFromDb
    (context: Context.Context)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (orderBy: string option)
    (expectedRows: AcceptableExpectedRows)
    : Result<ClassificationRule list, AppError> =
    let select =
        """
        cr.unique_id, cr.rule_name, cr.account_at_match, cr.priority,
        cr.rule_groups, cr.is_active, cr.created_at, cr.modified_at
        """
    let from = "ingestion.classification_rule cr"
    let query = buildReadQuery None select from joinList predicate limit None orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        query
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchById (context: Context.Context) (ruleId: ClassificationRuleId) : Result<ClassificationRule, AppError> =
    let predicate = "cr.unique_id = @unique_id"
    let nameStr = ruleId |> ClassificationRuleId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId(nameStr) } ]
    readRowsFromDb context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

let fetchByName (context: Context.Context) (name: ClassificationRuleName) : Result<ClassificationRule, AppError> =
    let predicate = "cr.rule_name = @rule_name"
    let nameStr = name |> ClassificationRuleName.value
    let parameters = [ { name = "@rule_name"; value = CharString(nameStr) } ]
    readRowsFromDb context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

let doesMatch
    (candidate: MatchCandidate)
    (classificationRule: ClassificationRule)
    : bool =
    // empty lists would match everything. we have validation at construction. the empty check is a backstop
    if classificationRule.ruleGroups |> List.isEmpty then false
    else
        classificationRule.ruleGroups
        |> List.forall(fun ruleGroup ->
                ruleGroup |> doesMatch candidate)
        
