module Model.StageDataClassification.RuleMatch

open DataAccessLayer.ExecuteNonQuery
open DataAccessLayer.ExecuteReader
open DataAccessLayer.QueryParameters
open Model.DataIngestion.StageEntryComponent
open NodaTime
open Utilities.AppError
open Utilities.ResultHelper
open Model.StageDataClassification.StageDataClassificationComponent

/// RuleMatch: one classification rule matched one staged entry line during one classification run. Matches only -- a
/// line that matched nothing gets no row, so the absence of a row says nothing about whether the line was even a
/// candidate. The classification step returns its full result list for that; this table is the durable diagnostic
/// Hobson reads when a rule misbehaves. Rows are never updated: a run is a historical fact, so there is deliberately
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

let persist (context: Context.Context) (ruleMatch: RuleMatch) : Result<unit, AppError> =
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
        let (uuid, runUuid, lineUuid, ruleUuid, createdAt) = raw
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
    : Result<RuleMatch list, AppError> =
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

let fetchById (context: Context.Context) (matchId: ClassificationMatchId) : Result<RuleMatch, AppError> =
    let predicate = "rm.unique_id = @unique_id"
    let uuid = matchId |> ClassificationMatchId.value
    let parameters = [ { name = "@unique_id"; value = UniqueId uuid } ]
    query context None (Some predicate) None parameters None ExactlyOne |> Result.map List.head

let fetchByRunId (context: Context.Context) (runId: ClassificationRunId) : Result<RuleMatch list, AppError> =
    let predicate = "rm.run_id = @run_id"
    let runUuid = runId |> ClassificationRunId.value
    let parameters = [ { name = "@run_id"; value = UniqueId runUuid } ]
    query context None (Some predicate) None parameters None AnyQuantityIsAcceptable

/// fetchByRunIdAndClaimantType is the read the Saturday routine actually uses: account resolution wants only the rows
/// whose rule claimed an account, payment agreement resolution only the rows whose rule claimed a payment agreement.
/// Claimant type is not stored anywhere -- the two claimant columns on the rule are mutually exclusive, so testing one
/// for null is the whole test.
let fetchByRunIdAndClaimantType
    (context: Context.Context)
    (runId: ClassificationRunId)
    (claimantType: ClassificationClaimantType)
    : Result<RuleMatch list, AppError> =
    let joinList =
        [ "join classification.classification_rule cr on rm.classification_rule_id = cr.unique_id" ]
    let claimantClause =
        match claimantType with
        | AccountClaimant -> "cr.account_at_match is not null"
        | PaymentAgreementClaimant -> "cr.payment_agreement_at_match is not null"
    let predicate = $"rm.run_id = @run_id and {claimantClause}"
    let runUuid = runId |> ClassificationRunId.value
    let parameters = [ { name = "@run_id"; value = UniqueId runUuid } ]
    query context (Some joinList) (Some predicate) None parameters None AnyQuantityIsAcceptable
