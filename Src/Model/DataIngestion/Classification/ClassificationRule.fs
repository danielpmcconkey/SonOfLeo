module Model.DataIngestion.Classification.ClassificationRule

open Context
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.Ledger.Accounts.AccountComponent
open NodaTime
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.Json.Json
open Utilities.ResultHelper

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
        
let classificationRuleId (a: ClassificationRule) = a.classificationRuleId
let classificationRuleName (a: ClassificationRule) = a.classificationRuleName
let codeAtMatch (a: ClassificationRule) = a.codeAtMatch
let priority (a: ClassificationRule) = a.priority
let ruleGroups (a: ClassificationRule) = a.ruleGroups
let isActive (a: ClassificationRule) = a.isActive
let createdAt (a: ClassificationRule) = a.createdAt
let modifiedAt (a: ClassificationRule) = a.modifiedAt

let create
    (classificationRuleId: ClassificationRuleId)
    (classificationRuleName: ClassificationRuleName)
    (codeAtMatch: AccountCode)
    (priority: int)
    (ruleGroups: ClassificationRuleGroup list)
    (isActive: bool)
    (createdAt: Instant)
    (modifiedAt: Instant)
    : ClassificationRule = {
        classificationRuleId = classificationRuleId
        classificationRuleName = classificationRuleName
        codeAtMatch = codeAtMatch
        priority = priority
        ruleGroups = ruleGroups
        isActive = isActive
        createdAt = createdAt
        modifiedAt = modifiedAt
    }
    
// todo: don't forget to create a CLI route for making new rules 
let insertNewToDb (context: Context.Context) (classificationRule: ClassificationRule) : Result<unit, AppError> =
    let query =
        """
        insert into ingestion.classification_rule(
	        unique_id, rule_name, code_at_match, priority, rule_groups, is_active, created_at, modified_at)
        values (
	        @unique_id, 
            @rule_name, 
            @code_at_match, 
            @priority, 
            @rule_groups, 
            @is_active, 
            @created_at, 
            @modified_at);"""
    let uuid = classificationRule.classificationRuleId |> ClassificationRuleId.value
    let ruleName = classificationRule.classificationRuleName |> ClassificationRuleName.value
    let code = classificationRule.codeAtMatch |> AccountCode.value
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
              { name = "@code_at_match"; value = CharString(code) }
              { name = "@priority"; value = Integer(priority) }
              { name = "@rule_groups"; value = Jsonb(ruleGroups) }
              { name = "@is_active"; value = Boolean(isActive) }
              { name = "@created_at"; value = DbInstant createdAt }
              { name = "@modified_at"; value = DbInstant modifiedAt }
            ]
        return! executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne
    }
    
let reconstitute raw =
    result {
        let (uuid,
             nameStr,
             codeStr,
             priority,
             ruleGroupsStr,
             isActive,
             createdAt,
             modifiedAt) =
            raw
        let classificationRuleId = uuid |> ClassificationRuleId.fromGuid
        let! name = nameStr |> ClassificationRuleName.create
        let! codeAtMatch = codeStr |> AccountCode.create
        let! ruleGroups = ruleGroupsStr |> fromJson<ClassificationRuleGroup list>
        return
            create
                classificationRuleId
                name
                codeAtMatch
                priority
                ruleGroups
                isActive
                createdAt
                modifiedAt
    }
    
let mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getString "rule_name"),
    (row |> RowReader.getString "code_at_match"),
    (row |> RowReader.getInt "priority"),
    (row |> RowReader.getString "rule_groups"),
    (row |> RowReader.getBool "is_active"),
    (row |> RowReader.getInstant "created_at"),
    (row |> RowReader.getInstant "modified_at")

let private readRowsFromDb
    (context: Context.Context)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (orderBy: string option)
    (expectedRows: AcceptableExpectedRows)
    : Result<ClassificationRule list, AppError> =
    let select =
        """
        cr.unique_id, cr.rule_name, cr.code_at_match, cr.priority,
        cr.rule_groups, cr.is_active, cr.created_at, cr.modified_at
        """
    let from = "ingestion.classification_rule cr"
    let query = buildReadQuery select from None predicate limit None orderBy
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
    readRowsFromDb context (Some predicate) None parameters None ExactlyOne |> Result.map List.head

let fetchByName (context: Context.Context) (name: ClassificationRuleName) : Result<ClassificationRule, AppError> =
    let predicate = "cr.rule_name = @rule_name"
    let nameStr = name |> ClassificationRuleName.value
    let parameters = [ { name = "@rule_name"; value = CharString(nameStr) } ]
    readRowsFromDb context (Some predicate) None parameters None ExactlyOne |> Result.map List.head
    
let private updateDb
    (context: Context.Context)
    (classificationRuleNameUpdate: FieldUpdate<ClassificationRuleName>)
    (codeAtMatchUpdate: FieldUpdate<AccountCode>)
    (priorityUpdate: FieldUpdate<int>)
    (ruleGroupsUpdate: FieldUpdate<ClassificationRuleGroup list>)
    (isActiveUpdate: FieldUpdate<bool>)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    let uuid = classificationRuleId |> ClassificationRuleId.value
    let baseParams =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid } ]
    result {
        let! groupStr = // do this up here because it's a pain in the ass to do it down in the updates block
            match ruleGroupsUpdate with
            | NoChange -> Ok ""
            | SetTo x -> x |> toJson<ClassificationRuleGroup list> 
            
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
        return! classificationRuleId |> fetchById context
    }

/// updateCodeAtMatchById assumes the orchestrator is validating the code maps to a real account 
let private updateCodeAtMatchById
    (context: Context.Context)
    (accountCodeAtMatchUpdate: FieldUpdate<AccountCode>)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    classificationRuleId
    |> updateDb context NoChange accountCodeAtMatchUpdate NoChange NoChange NoChange

let private updateRuleGroupsById
    (context: Context.Context)
    (ruleGroupsUpdate: FieldUpdate<ClassificationRuleGroup list>)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    classificationRuleId
    |> updateDb context NoChange NoChange NoChange ruleGroupsUpdate NoChange 

let private updatePriorityById
    (context: Context.Context)
    (priorityUpdate: FieldUpdate<int>)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    classificationRuleId
    |> updateDb context NoChange NoChange priorityUpdate NoChange NoChange 

let private toggleActiveById
    (context: Context.Context)
    (newValue: bool)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    let enforcedCurrentValue = not newValue
    let uuid = classificationRuleId |> ClassificationRuleId.value
    let parameters =
        [ { name = "@modified"; value = DbInstant(context |> Context.getInitiationInstant) }
          { name = "@unique_id"; value = UniqueId uuid }
          { name = "@newValue"; value = Boolean newValue }
          { name = "@enforcedCurrentValue"; value = Boolean enforcedCurrentValue } ]
    let query =
        $"""
        UPDATE ingestion.classification_rule
        set
            modified_at = @modified
            , is_active = @newValue
        WHERE unique_id = @unique_id
        and is_active = @enforcedCurrentValue
        ;
    """
    result {
        do! match executeNonQuery (context |> Context.getDatabaseTransaction) query parameters ExactlyOne with
            | Ok _ -> Ok ()
            | Error (DalResultantRowsDidntMatchExpectation (expected, actual)) ->
                if actual = 0
                then Error IngestionClassificationRuleToggleOpenNoOp
                else Error (DalResultantRowsDidntMatchExpectation (expected, actual))
            | Error e -> Error e
        return! classificationRuleId |> fetchById context
    }

let deactivateRuleById
    (context: Context.Context)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    classificationRuleId |> toggleActiveById context false

let activateRuleById
    (context: Context.Context)
    (classificationRuleId: ClassificationRuleId)
    : Result<ClassificationRule, AppError> =
    classificationRuleId |> toggleActiveById context true

let doesMatch
    (candidate: MatchCandidate)
    (classificationRule: ClassificationRule)
    : bool =
    classificationRule.ruleGroups
    |> List.forall(fun ruleGroup ->
            ruleGroup |> ClassificationRuleGroup.doesMatch candidate)
        
