module ModelOrchestrator.StageEntryOrchestration

open Context.Context
open Model
open Model.DataIngestion
open Model.DataIngestion.BaseStageRaw
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities.AppError
open Utilities.ResultHelper

type StageEntry =
    private {
        stageEntryHeader: StageEntryHeader
        lines: StageEntryLine list
        statusTransitions: StageEntryStatusTransition list
    }

let stageEntryHeader se = se.stageEntryHeader
let lines se = se.lines
let statusTransitions se = se.statusTransitions

let private sumLinesByType
    (debitOrCredit: JournalEntryLineType)
    (lines: StageEntryLine list)
    : Result<Money, AppError> =
    lines
    |> List.filter(fun x -> x |> lineType = debitOrCredit)
    |> List.map(fun x -> x |> amount) |> Money.sumList
    
let private confirmAmountEquality (lines: StageEntryLine list) : Result<unit, AppError> =
    result {
        let! totalDebits = lines |> sumLinesByType Debit
        let! totalCredits = lines |> sumLinesByType Credit
        return!
            if totalCredits = totalDebits then
                Ok()
            else
                Error(IngestionStageEntryDebitCreditMismatch(totalDebits |> Money.amount, totalCredits |> Money.amount))
    }

let private confirmLineCount (lines: StageEntryLine list) : Result<unit, AppError> =
    if lines |> List.length < 2 then
        Error(IngestionStageEntryInsufficientLines(lines |> List.length))
    else
        Ok()

let private confirmLines (lines: StageEntryLine list) : Result<unit, AppError> =
    result {
        do! lines |> confirmLineCount
        do! lines |> confirmAmountEquality }
    
let private createStageEntriesFromRaw
    (context: Context)
    (sourceFile: SourceFile)
    (rawRows: BaseStageRawRow list)
    : Result<StageEntry list, AppError> =
    rawRows
    |> List.groupBy(_.baseStageEntryGroupId)
    |> List.map(fun (baseStageEntryGroupId, rawRowsAtGroupId) ->
        let distinctHeadersList =
            rawRowsAtGroupId
            |> List.groupBy(fun x -> x.entryDate, x.description, x.fiSource, x.fiReference)
        if distinctHeadersList |> List.length > 1
        then Error (IngestionBaseStageGroupIdDistinctDataViolation (baseStageEntryGroupId |> BaseStageEntryGroupId.value))
        else
            let theOnly = distinctHeadersList |> List.head
            let entryDate, description, fiSource, fiReference = theOnly |> fst
            let rawRowsAtTheOnly = theOnly |> snd
            result {
                let! lines =
                    rawRowsAtTheOnly
                    |> List.map (fun row -> 
                        StageEntryLine.create
                            row.amount row.entryType row.accountCode row.memo
                        )
                    |> convertListOfResultsToResultsList
                let stageEntryId = StageEntryHeaderId.create ()
                do! lines |> confirmLines
                let! ingestionSource = fiSource |> IngestionSource.fetchByName context
                let header =
                    StageEntryHeader.create
                        sourceFile stageEntryId entryDate description ingestionSource fiReference Read
                let! transition = StageEntryStatusTransition.create
                                      NoStatus Read (context |> getInitiationInstant) BaseParser
                return {
                    stageEntryHeader = header
                    lines = lines
                    statusTransitions = [transition]
                }
            }
        )
    |> convertListOfResultsToResultsList
