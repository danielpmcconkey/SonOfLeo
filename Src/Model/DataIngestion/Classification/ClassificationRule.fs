module Model.DataIngestion.Classification.ClassificationRule

open Context
open Context.Context
open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.Ledger.Accounts.AccountComponent
open NodaTime
open Utilities.AppError
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
let insertNewToDb (context: Context) (classificationRule: ClassificationRule) : Result<unit, AppError> =
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
              { name = "@rule_groups"; value = CharString(ruleGroups) }
              { name = "@is_active"; value = Boolean(isActive) }
              { name = "@created_at"; value = DbInstant createdAt }
              { name = "@modified_at"; value = DbInstant modifiedAt }
            ]
        return! executeNonQuery (context |> getDatabaseTransaction) query parameters ExactlyOne
    }
    
// let private reconstitute raw =
//     result {
//         let (uuid,
//              sourceString,
//              createdAt,
//              modifiedAt) =
//             raw
//         let ingestionSourceId = uuid |> IngestionSourceId.fromGuid
//         let! name = sourceString |> JournalRefFinancialInstitution.create
//         return
//             create
//                 ingestionSourceId
//                 name
//                 createdAt
//                 modifiedAt
//     }
//     
// let private mapRawForDbRead (row: RowReader) =
//     (row |> RowReader.getUuid "unique_id"),
//     (row |> RowReader.getString "source_name"),
//     (row |> RowReader.getInstant "created_at"),
//     (row |> RowReader.getInstant "modified_at")
//
// let private readRowsFromDb
//     (context: Context)
//     (predicate: string option)
//     (limit: int option)
//     (parameters: QueryParameter list)
//     (expectedRows: AcceptableExpectedRows)
//     : Result<IngestionSource list, AppError> =
//     let select =
//         """
//         s.unique_id, s.source_name, s.created_at, s.modified_at
//         """
//     let from = "ingestion.source s"
//     let query = buildReadQuery select from None predicate limit None None
//     executeReaderQuery
//         (context |> getDatabaseTransaction)
//         query
//         parameters
//         mapRawForDbRead
//         reconstitute
//         expectedRows
//
// let fetchByName (context: Context) (name: JournalRefFinancialInstitution) : Result<IngestionSource, AppError> =
//     let predicate = "s.source_name = @source_name"
//     let nameStr = name |> JournalRefFinancialInstitution.value
//     let parameters = [ { name = "@source_name"; value = CharString(nameStr) } ]
//     readRowsFromDb context (Some predicate) None parameters ExactlyOne |> Result.map List.head

    
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
        
