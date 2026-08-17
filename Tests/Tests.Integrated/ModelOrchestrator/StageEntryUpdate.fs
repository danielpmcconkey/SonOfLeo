namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open Model.DataIngestion.StageEntryStatusTransition
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.FieldUpdate.FieldUpdate
open Utilities.ResultHelper
open Xunit
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling.JournalEntryComponent


[<Collection("SharedTestData")>]
type StageEntryUpdateTests(fixture: TestDataFixture) =

    let noChangeHeaderUpdates headerId : StageEntryHeaderFieldUpdates =
        { headerIdToUpdate = headerId
          sourceFileUpdate = NoChange
          entryDateUpdate = NoChange
          descriptionUpdate = NoChange
          ingestionSourceUpdate = NoChange
          fiReferenceUpdate = NoChange
          statusUpdate = NoChange }

    let noChangeLineUpdates lineId : StageEntryLineFieldUpdates =
        { lineIdToUpdate = lineId
          amountUpdate = NoChange
          entryTypeUpdate = NoChange
          accountCodeUpdate = NoChange
          memoUpdate = NoChange
          classificationRuleIdUpdate = NoChange }


    // =========================================================================
    // REQ-STG-6.1 — Override account_code on a staged line
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.1 operator can override account_code on a parser-assigned line`` () =
        let newCode =
            "F-5650" |> AccountCode.create
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-007 payroll has parser-assigned lines
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "PAYROLL DEPOSIT ACME CORP"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let firstLine = entry |> lines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountCodeUpdate = SetTo (Some newCode) } ]
                let! updated = updateStageEntry context headerUpdates lineUpdates
                let updatedLine =
                    updated |> lines
                    |> List.find (fun l -> l |> StageEntryLine.stageEntryLineId = lineId)
                Assert.Equal(Some "F-5650", updatedLine |> StageEntryLine.accountCode |> Option.map AccountCode.value)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.1 operator can override account_code on a classifier-assigned line`` () =
        let newCode =
            "F-5650" |> AccountCode.create
            |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                // grp-002 gas station: debit line was classified to F-5300 by generic rule
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> lines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let lineId = debitLine |> StageEntryLine.stageEntryLineId
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountCodeUpdate = SetTo (Some newCode) } ]
                let! updated = updateStageEntry context headerUpdates lineUpdates
                let updatedLine =
                    updated |> lines
                    |> List.find (fun l -> l |> StageEntryLine.stageEntryLineId = lineId)
                Assert.Equal(Some "F-5650", updatedLine |> StageEntryLine.accountCode |> Option.map AccountCode.value)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-6.2 — Operator sets fields and status explicitly
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry allows operator to set status explicitly`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let contextForUpdate = context |> Context.updateInitiationInstant
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let! updated = updateStageEntry contextForUpdate headerUpdates []
                Assert.Equal(Reviewed, StageTestData.latestStatus updated)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry validates balanced entry after update`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let firstLine = entry |> lines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let! badAmount = 999.99M |> Money.fromDecimal
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with amountUpdate = SetTo badAmount } ]
                return!
                    match updateStageEntry context headerUpdates lineUpdates with
                    | Error (IngestionStageEntryDebitCreditMismatch _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry validates account codes exist after update`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let firstLine = entry |> lines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let! badCode = "BOGUS-9999" |> AccountCode.create
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountCodeUpdate = SetTo (Some badCode) } ]
                return!
                    match updateStageEntry context headerUpdates lineUpdates with
                    | Error _ -> Ok ()
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry validates legal status transition`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                // Classified → Ingested is not a valid transition
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Ingested }
                return!
                    match updateStageEntry context headerUpdates [] with
                    | Error (IngestionInvalidStageStatusTransition _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-6.3 — Override duplicate flag
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.3 operator can transition entry from Duplicate to Reviewed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let contextForUpdate = context |> Context.updateInitiationInstant
                // grp-008 is a ledger dup
                let dupEntry = fullResult.stagedEntries |> StageTestData.findByDescription "Fixture JE with reference"
                Assert.Equal(Duplicate, StageTestData.latestStatus dupEntry)
                let headerId = dupEntry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let! updated = updateStageEntry contextForUpdate headerUpdates []
                Assert.Equal(Reviewed, StageTestData.latestStatus updated)
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-4.3 — Every status transition creates audit record
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-4.3 manual status transition creates audit record`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let contextForUpdate = context |> Context.updateInitiationInstant
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "HARRIS TEETER 0381 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let transitionCountBefore = entry |> statusTransitions |> List.length
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let! updated = updateStageEntry contextForUpdate headerUpdates []
                let transitionCountAfter = updated |> statusTransitions |> List.length
                Assert.Equal(transitionCountBefore + 1, transitionCountAfter)
                let latestTransition =
                    updated |> statusTransitions
                    |> List.sortByDescending (fun t -> t |> StageEntryStatusTransition.instant)
                    |> List.head
                Assert.Equal(Operator, latestTransition |> StageEntryStatusTransition.stageStatusChangeMechanism)
            })
        |> railroadWrapper
