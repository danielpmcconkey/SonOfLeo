namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.DataIngestion
open Business.FinancialServices.DataIngestion.StageEntryComponent
open Business.FinancialServices.DataIngestion.StageEntryHeader
open Business.FinancialServices.DataIngestion.StageEntryLine
open Business.CrossDomainOrchestration.StageEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open App.Utility.FieldUpdate
open App.Utility.Result
open Xunit
open Business.FinancialServices.Ledger.AccountComponent
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.DataIngestion.DataIngestionError
open App.DataAccessLayer.DalError


[<Collection("SharedTestData")>]
type StageEntryUpdateTests(fixture: TestDataFixture) =

    let noChangeHeaderUpdates headerId : StageEntryHeaderFieldUpdates =
        { headerIdToUpdate = headerId
          sourceFileUpdate = NoChange
          entryDateUpdate = NoChange
          descriptionUpdate = NoChange
          ingestionSourceUpdate = NoChange
          fiReferenceUpdate = NoChange
          journalEntryHeaderIdUpdate = NoChange
          statusUpdate = NoChange }

    let noChangeLineUpdates lineId : StageEntryLineFieldUpdates =
        { lineIdToUpdate = lineId
          amountUpdate = NoChange
          entryTypeUpdate = NoChange
          accountIdUpdate = NoChange
          memoUpdate = NoChange
          journalEntryLineIdUpdate = NoChange }


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
                    | Error (AsError IngestionUpdateStageEntryNoOp) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {e.ToMessage()}")
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
                let firstLine = entry |> seLines |> List.head
                let lineId = firstLine |> StageEntryLine.stageEntryLineId
                let! badAmount = 999.99M |> Money.fromDecimal
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with amountUpdate = SetTo badAmount } ]
                let contextForUpdate = context |> Context.updateInitiationInstant
                return!
                    match updateStageEntry contextForUpdate headerUpdates lineUpdates with
                    | Error (AsError (IngestionStageEntryDebitCreditMismatch _)) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {e.ToMessage()}")
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
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Reviewed }
                let lineUpdates = [ { (noChangeLineUpdates lineId) with accountIdUpdate = SetTo (Some bogusAccountId) } ]
                return!
                    match updateStageEntry context headerUpdates lineUpdates with
                    | Error (AsError (AccountIdDoesntMatch _)) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error. {e.ToMessage()}")
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
                    | Error (AsError (IngestionInvalidStageStatusTransition _)) -> Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {e.ToMessage()}")
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

    // =========================================================================
    // Plan defects 8–17 (2026-09-27)
    // =========================================================================

    // every status the transition table lets an entry move to; Ingested is only ever a first status
    [<Theory>]
    [<InlineData("Duplicate")>]
    [<InlineData("Classified")>]
    [<InlineData("NoMatch")>]
    [<InlineData("Conflict")>]
    [<InlineData("Reviewed")>]
    [<InlineData("Posted")>]
    [<InlineData("Ignored")>]
    member _.``REQ-STG-6.2.1 every status transition the manual update makes, to each target status it allows, is recorded with change mechanism Operator and no other`` (targetStr: string) =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! target = targetStr |> StagedEntryStatus.fromString
                let! fullResult = StageTestData.runPipeline context
                let contextForUpdate = context |> Context.updateInitiationInstant
                // any entry the pipeline left in a status from which the target is a legal move
                let entry =
                    fullResult.stagedEntries
                    |> List.tryFind (fun candidate ->
                        Some (StageTestData.latestStatus candidate)
                        |> StageEntryStatusTransition.validTransitions
                        |> List.contains target)
                    |> Option.defaultWith (fun () -> failwith $"the pipeline left no entry that can move to {targetStr}")
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let headerUpdates = { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo target }
                let! updated = updateStageEntry contextForUpdate headerUpdates []
                let fromThisUpdate =
                    updated
                    |> statusTransitions
                    |> List.filter (fun t ->
                        t |> StageEntryStatusTransition.instant = (contextForUpdate |> Context.getInitiationInstant))
                let transition = fromThisUpdate |> List.exactlyOne
                Assert.Equal(target, transition |> StageEntryStatusTransition.toStatus)
                Assert.Equal(Operator, transition |> StageEntryStatusTransition.stageStatusChangeMechanism)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a staged entry by an ID no entry holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = StageEntryHeaderId.create ()
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            let headerUpdates = { (noChangeHeaderUpdates missingId) with journalEntryHeaderIdUpdate = SetTo None }
            match updateStageEntry context headerUpdates [] with
            | Error (AsError (IngestionStageEntryHeaderIdDoesntExist uuid)) ->
                Assert.Equal(missingId |> StageEntryHeaderId.value, uuid)
                Ok ()
            | Error e -> Error (TestingError $"Wrong error: {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
            | Ok _ -> Error (TestingError "Expected failure; got success"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a staged entry line by an ID no line holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = StageEntryLineId.create ()
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let lineUpdates =
                    [ { (noChangeLineUpdates missingId) with accountIdUpdate = SetTo (Some fixture.Data.entertainment5650Id) } ]
                do!
                    match updateStageEntry context (noChangeHeaderUpdates headerId) lineUpdates with
                    | Error (AsError (IngestionStageEntryLineIdDoesntExist uuid)) ->
                        Assert.Equal(missingId |> StageEntryLineId.value, uuid)
                        Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
                let! after = headerId |> fetchByStageEntryHeaderId context
                Assert.Equal(entry, after)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-4.1.2 recording a second status transition for a staged entry at an instant it already holds fails loudly, naming the database constraint that forbids it`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let contextForReview = context |> Context.updateInitiationInstant
                let instant = contextForReview |> Context.getInitiationInstant
                let transitionAt fromStatus toStatus =
                    StageEntryStatusTransition.create
                        (StageEntryStatusTransitionId.create ()) headerId (Some fromStatus) toStatus instant Operator
                do! transitionAt Classified Reviewed |> persistStatusTransition contextForReview
                return!
                    match transitionAt Reviewed Ignored |> persistStatusTransition contextForReview with
                    | Error (AsError (DalErrorDuringNonQueryExecution ex)) ->
                        Assert.Contains("staged_entry_audit_entry_id_modified_at_key", ex.Message)
                        Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-4.1.2 two different staged entries can each record a status transition at the same instant`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let headerIdOf description =
                    fullResult.stagedEntries
                    |> StageTestData.findByDescription description
                    |> stageEntryHeader
                    |> StageEntryHeader.stageEntryHeaderId
                let headerIds =
                    [ headerIdOf "MARATHON PETRO 7218 ANYTOWN US"; headerIdOf "PAYROLL DEPOSIT ACME CORP" ]
                let contextForReview = context |> Context.updateInitiationInstant
                let instant = contextForReview |> Context.getInitiationInstant
                do!
                    headerIds
                    |> List.map (fun headerId ->
                        StageEntryStatusTransition.create
                            (StageEntryStatusTransitionId.create ()) headerId (Some Classified) Reviewed instant Operator
                        |> persistStatusTransition contextForReview)
                    |> convertListOfResultsToResultsList
                    |> Result.map ignore
                let! recorded = headerIds |> StageEntryStatusTransition.fetchByHeaderIdList contextForReview
                let atInstant =
                    recorded
                    |> List.filter (fun t -> t |> StageEntryStatusTransition.instant = instant)
                    |> List.map StageEntryStatusTransition.stageEntryHeaderId
                    |> List.sortBy StageEntryHeaderId.value
                Assert.Equal<StageEntryHeaderId list>(headerIds |> List.sortBy StageEntryHeaderId.value, atInstant)
            })
        |> railroadWrapper
