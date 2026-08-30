namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.DataIngestion
open Model.DataIngestion.StageEntryComponent
open Model.DataIngestion.StageEntryHeader
open Model.DataIngestion.StageEntryLine
open ModelOrchestrator.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Xunit
open Model.Ledger.AccountComponent
open Model.Ledger.JournalEntryComponent


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
          accountIdUpdate = NoChange
          memoUpdate = NoChange
          classificationRuleIdUpdate = NoChange }


    // =========================================================================
    // REQ-STG-6.2 — An update that sets nothing
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.2 updateStageEntry rejects an update that changes no field`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let lineId = entry |> seLines |> List.head |> StageEntryLine.stageEntryLineId
                return!
                    match updateStageEntry context (noChangeHeaderUpdates headerId) [ noChangeLineUpdates lineId ] with
                    | Error IngestionUpdateStageEntryNoOp -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {AppError.toMessage e}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper


    // =========================================================================
    // REQ-STG-6.1 — Override account_code on a staged line
    // =========================================================================

    [<Fact>]
    member _.``REQ-STG-6.1 updateStageEntry sets the account on a parser-assigned line to the one the caller supplies`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let newAccountId = fixture.Data.entertainment5650Id
                let! fullResult = StageTestData.runPipeline context
                // grp-007 payroll has parser-assigned lines
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "PAYROLL DEPOSIT ACME CORP"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let firstLine = entry |> seLines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountIdUpdate = SetTo (Some newAccountId) } ]
                let! updated = updateStageEntry context (noChangeHeaderUpdates headerId) lineUpdates
                let updatedLine =
                    updated |> seLines
                    |> List.find (fun l -> l |> StageEntryLine.stageEntryLineId = lineId)
                Assert.Equal(Some newAccountId, updatedLine |> StageEntryLine.accountId)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.1 updateStageEntry sets the account on a classifier-assigned line to the one the caller supplies`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let newAccountId = fixture.Data.entertainment5650Id
                let! fullResult = StageTestData.runPipeline context
                // grp-002 gas station: debit line was classified to F-5300 by generic rule
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let lineId = debitLine |> StageEntryLine.stageEntryLineId
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountIdUpdate = SetTo (Some newAccountId) } ]
                let! updated = updateStageEntry context (noChangeHeaderUpdates headerId) lineUpdates
                let updatedLine =
                    updated |> seLines
                    |> List.find (fun l -> l |> StageEntryLine.stageEntryLineId = lineId)
                Assert.Equal(Some newAccountId, updatedLine |> StageEntryLine.accountId)
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
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo (Reviewed, Operator) }
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
                let firstLine = entry |> seLines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let! badAmount = 999.99M |> Money.fromDecimal
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo (Reviewed, Operator) }
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
                let firstLine = entry |> seLines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let bogusAccountId = AccountId.create()
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo (Reviewed, Operator) }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountIdUpdate = SetTo (Some bogusAccountId) } ]
                return!
                    match updateStageEntry context headerUpdates lineUpdates with
                    | Error (AccountIdDoesntMatch _) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {AppError.toMessage e}")
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
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo (Ingested, Operator) }
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
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo (Reviewed, Operator) }
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
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo (Reviewed, Operator) }
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
