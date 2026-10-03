module Business.FinancialServices.Classification.RuleMatch

open NodaTime
open App.Utility.IAppError
open App.Utility.Result
open App.DataAccessLayer.QueryParameter
open App.DataAccessLayer.ExecuteReader
open App.DataAccessLayer.ExecuteNonQuery
open App.Session
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.Classification.ClassificationComponent

/// RuleMatch: is the durable record of a classification run. A run is a historical fact, so there is deliberately
/// no update function or FieldUpdates record here.
type RuleMatch = private {
    classificationMatchId: ClassificationMatchId
    runId: ClassificationRunId
    stageEntryLineId: StageEntryLineId
    classificationRuleId: ClassificationRuleId
    createdAt: Instant
}

let classificationMatchId m = m.classificationMatchId
let runId m = m.runId
let stageEntryLineId m = m.stageEntryLineId
let classificationRuleId m = m.classificationRuleId
let createdAt m = m.createdAt

let create
    (classificationMatchId: ClassificationMatchId)
    (runId: ClassificationRunId)
    (stageEntryLineId: StageEntryLineId)
    (classificationRuleId: ClassificationRuleId)
    (createdAt: Instant)
    : RuleMatch =
    { classificationMatchId = classificationMatchId
      runId = runId
      stageEntryLineId = stageEntryLineId
      classificationRuleId = classificationRuleId
      createdAt = createdAt }

let persist (context: Context.Context) (ruleMatch: RuleMatch) : Result<unit, IAppError> =
    let queryStatement =
        """
        insert into classification.rule_match(
            unique_id, run_id, stage_entry_line_id, classification_rule_id, created_at)
        values (
            @unique_id,
            @run_id,
            @stage_entry_line_id,
            @classification_rule_id,
            @created_at);"""
    let uuid = ruleMatch.classificationMatchId |> ClassificationMatchId.value
    let runUuid = ruleMatch.runId |> ClassificationRunId.value
    let lineUuid = ruleMatch.stageEntryLineId |> StageEntryLineId.value
    let ruleUuid = ruleMatch.classificationRuleId |> ClassificationRuleId.value
    let parameters =
        [ { name = "@unique_id"; value = UniqueId uuid }
          { name = "@run_id"; value = UniqueId runUuid }
          { name = "@stage_entry_line_id"; value = UniqueId lineUuid }
          { name = "@classification_rule_id"; value = UniqueId ruleUuid }
          { name = "@created_at"; value = DbInstant ruleMatch.createdAt } ]
    executeNonQuery (context |> Context.getDatabaseTransaction) queryStatement parameters ExactlyOne

let private reconstitute raw =
    result {
        let uuid, runUuid, lineUuid, ruleUuid, createdAt = raw
        let classificationMatchId = uuid |> ClassificationMatchId.fromGuid
        let runId = runUuid |> ClassificationRunId.fromGuid
        let stageEntryLineId = lineUuid |> StageEntryLineId.fromGuid
        let classificationRuleId = ruleUuid |> ClassificationRuleId.fromGuid
        return create classificationMatchId runId stageEntryLineId classificationRuleId createdAt
    }

let private mapRawForDbRead (row: RowReader) =
    (row |> RowReader.getUuid "unique_id"),
    (row |> RowReader.getUuid "run_id"),
    (row |> RowReader.getUuid "stage_entry_line_id"),
    (row |> RowReader.getUuid "classification_rule_id"),
    (row |> RowReader.getInstant "created_at")

let query
    (context: Context.Context)
    (joinList: string list option)
    (predicate: string option)
    (limit: int option)
    (parameters: QueryParameter list)
    (orderBy: string option)
    (expectedRows: AcceptableExpectedRows)
    : Result<RuleMatch list, IAppError> =
    let select =
        """
        rm.unique_id, rm.run_id, rm.stage_entry_line_id, rm.classification_rule_id, rm.created_at
        """
    let from = "classification.rule_match rm"
    let queryStatement = buildReadQuery None select from joinList predicate limit None orderBy
    executeReaderQuery
        (context |> Context.getDatabaseTransaction)
        queryStatement
        parameters
        mapRawForDbRead
        reconstitute
        expectedRows

let fetchByRunId (context: Context.Context) (runId: ClassificationRunId) : Result<RuleMatch list, IAppError> =
    let predicate = "rm.run_id = @run_id"
    let runUuid = runId |> ClassificationRunId.value
    let parameters = [ { name = "@run_id"; value = UniqueId runUuid } ]
    query context None (Some predicate) None parameters None AnyQuantityIsAcceptable

/// fetchByStageEntryLineIdList returns every match any classification run recorded against the given lines.
let fetchByStageEntryLineIdList
    (context: Context.Context)
    (lineIds: StageEntryLineId list)
    : Result<RuleMatch list, IAppError> =
    if lineIds |> List.isEmpty then Ok [] else
    let namesAndParameters =
        List.zip [ 1 .. lineIds.Length ] lineIds
        |> List.map (fun (ordinal, id) ->
            let name = $"@stageEntryLineId{ordinal}"
            name, { name = name; value = UniqueId(id |> StageEntryLineId.value) })
    let names = namesAndParameters |> List.map fst |> String.concat ", "
    let parameters = namesAndParameters |> List.map snd
    let predicate = $"rm.stage_entry_line_id in ({names})"
    query context None (Some predicate) None parameters None AnyQuantityIsAcceptable
