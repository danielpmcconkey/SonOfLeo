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
open Business.FinancialServices.CashFlow
open Tests.Helpers.EntityFunctions
open NodaTime
open App.Utility
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

    let accountIdOf code =
        fixture.Data.accounts
        |> List.find (fun a -> a |> Account.code |> AccountCode.value = code)
        |> Account.accountId

    let addition amount lineType accountId : StageEntryLineAddition =
        { amount = amount; lineType = lineType; accountId = accountId; memo = None }

    let lineIdsOf entry = entry |> seLines |> List.map StageEntryLine.stageEntryLineId

    let lineShapesOf entry =
        entry
        |> seLines
        |> List.map (fun l ->
            (l |> StageEntryLine.amount |> Money.amount), (l |> StageEntryLine.lineType), (l |> StageEntryLine.accountId))
        |> List.sort

    /// A Classified 100.00 outgo entry on the fixture's cash flow accounts (debit F-2230, credit F-1280), the shape a
    /// payment agreement leg matches. Returns the entry and its debit line's id. No classification run has seen it.
    let createOutgoEntry (context: Context.Context) (description: string) =
        result {
            let later = (context |> Context.getInitiationInstant).Plus(Duration.FromSeconds 1L)
            let! entry =
                createStageEntryForTest context "/tmp/stage-update-outgo.dat" description (System.Guid.NewGuid().ToString())
                    (fixture.Data.ingestionSources |> List.head) (Calendar.today())
                    [ (100.00M, "Debit", Some "F-2230", None, None); (100.00M, "Credit", Some "F-1280", None, None) ]
                    [ (Some "Ingested", "Classified", later, "Classifier") ]
            let debitLineId =
                entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit) |> StageEntryLine.stageEntryLineId
            return entry, debitLineId
        }

    /// Puts a 100.00 Payment on the fixture's open invoice on agreement A, pointing at the given staged line.
    let payWithLine (context: Context.Context) (lineId: StageEntryLineId) =
        result {
            let! amount = Money.fromDecimal 100.00M
            let invoiceId = fixture.Data.cashFlow.openInvoiceAId
            let update : InstanceOrchestration.InstanceCompositeUpdate =
                { instanceUpdates =
                    { instanceIdToUpdate = fixture.Data.cashFlow.openInstanceAId
                      instanceDateUpdate = NoChange
                      isFulfilledUpdate = NoChange }
                  invoiceCompositeUpdates =
                    [ { invoiceUpdates =
                          { invoiceIdToUpdate = invoiceId
                            externalInvoiceIdUpdate = NoChange
                            invoiceDateUpdate = NoChange
                            dueDateUpdate = NoChange
                            amountUpdate = NoChange
                            invoiceStateUpdate = NoChange
                            paymentStateUpdate = NoChange
                            postedStateUpdate = NoChange
                            blockerUpdate = NoChange
                            memoUpdate = NoChange }
                        paymentUpdates = []
                        paymentIdsToDelete = []
                        newPayments = [ (CashFlowComponent.Staged lineId, { money = amount }, None, None, None) ] } ]
                  newInvoices = [] }
            let! _ = InstanceOrchestration.updateInstanceComposite context update
            return ()
        }

    /// expectRejection passes when the update fails with the expected error and leaves the entry exactly as it was.
    let expectRejection
        context
        headerId
        (isExpected: IAppError -> bool)
        (before: StageEntry)
        (updateResult: Result<StageEntry, IAppError>)
        : Result<unit, IAppError> =
        result {
            do!
                match updateResult with
                | Error e when isExpected e -> Ok ()
                | Error (e: IAppError) -> Error (TestingError $"Wrong error: {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
                | Ok _ -> Error (TestingError "Expected failure; got success")
            let! after = headerId |> fetchByStageEntryHeaderId context
            Assert.Equal(before, after)
        }


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
                    match updateStageEntry context (noChangeHeaderUpdates headerId) [ noChangeLineUpdates lineId ] [] [] with
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
                let! updated = updateStageEntry context (noChangeHeaderUpdates headerId) lineUpdates [] []
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
                let! updated = updateStageEntry context (noChangeHeaderUpdates headerId) lineUpdates [] []
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
                let! updated = updateStageEntry contextForUpdate headerUpdates [] [] []
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
                    match updateStageEntry contextForUpdate headerUpdates lineUpdates [] [] with
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
                    match updateStageEntry context headerUpdates lineUpdates [] [] with
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
                    match updateStageEntry context headerUpdates [] [] [] with
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
                let! updated = updateStageEntry contextForUpdate headerUpdates [] [] []
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
                let! updated = updateStageEntry contextForUpdate headerUpdates [] [] []
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
                let! updated = updateStageEntry contextForUpdate headerUpdates [] [] []
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
            match updateStageEntry context headerUpdates [] [] [] with
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
                    match updateStageEntry context (noChangeHeaderUpdates headerId) lineUpdates [] [] with
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

    [<Fact>]
    member _.``REQ-STG-6.4 a manual update that lowers a line's amount and adds lines for the difference, in one operation, leaves the entry with exactly those lines, balanced`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let! lowered = Money.fromDecimal 30.00M
                let! difference = Money.fromDecimal 18.12M
                let lineUpdates =
                    [ { (noChangeLineUpdates (debitLine |> StageEntryLine.stageEntryLineId)) with amountUpdate = SetTo lowered } ]
                let! updated =
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) lineUpdates
                        [ addition difference Debit (Some fixture.Data.entertainment5650Id) ] []
                let expected =
                    [ (30.00M, Debit, debitLine |> StageEntryLine.accountId)
                      (18.12M, Debit, Some fixture.Data.entertainment5650Id)
                      (48.12M, Credit, creditLine |> StageEntryLine.accountId) ]
                    |> List.sort
                Assert.Equal<(decimal * JournalEntryLineType * AccountId option) list>(expected, updated |> lineShapesOf)
                let! stored = headerId |> fetchByStageEntryHeaderId contextForUpdate
                Assert.Equal<(decimal * JournalEntryLineType * AccountId option) list>(expected, stored |> lineShapesOf)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.4 a manual update removes a line the operator added, in the same operation as a field edit`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let debitLineId = debitLine |> StageEntryLine.stageEntryLineId
                let! lowered = Money.fromDecimal 30.00M
                let! difference = Money.fromDecimal 18.12M
                let! split =
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId)
                        [ { (noChangeLineUpdates debitLineId) with amountUpdate = SetTo lowered } ]
                        [ addition difference Debit (Some fixture.Data.entertainment5650Id) ] []
                let addedLineId = split |> lineIdsOf |> List.except (before |> lineIdsOf) |> List.exactlyOne
                let contextForUndo = contextForUpdate |> Context.updateInitiationInstant
                let! description = "MARATHON PETRO, split undone" |> JournalEntryDescription.create
                let! restored =
                    updateStageEntry contextForUndo
                        { (noChangeHeaderUpdates headerId) with descriptionUpdate = SetTo description }
                        [ { (noChangeLineUpdates debitLineId) with amountUpdate = SetTo (debitLine |> StageEntryLine.amount) } ]
                        [] [ addedLineId ]
                Assert.Equal<(decimal * JournalEntryLineType * AccountId option) list>(before |> lineShapesOf, restored |> lineShapesOf)
                Assert.DoesNotContain(addedLineId, restored |> lineIdsOf)
                Assert.Equal(description, restored |> stageEntryHeader |> StageEntryHeader.description)
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.4 an update that would leave the entry with fewer than two lines is rejected with a typed error, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [] []
                        [ creditLine |> StageEntryLine.stageEntryLineId ]
                    |> expectRejection contextForUpdate headerId
                        (function AsError (IngestionStageEntryInsufficientLines 1) -> true | _ -> false) before
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.4 an update whose added lines leave the entry unbalanced is rejected with a typed error, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let! extra = Money.fromDecimal 10.00M
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) []
                        [ addition extra Debit (Some fixture.Data.entertainment5650Id) ] []
                    |> expectRejection contextForUpdate headerId
                        (function AsError (IngestionStageEntryDebitCreditMismatch _) -> true | _ -> false) before
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.4 an added line that fails a staged-line rule is rejected with a typed error, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let! extra = Money.fromDecimal 10.00M
                let missingAccountId = AccountId.create ()
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) []
                        [ addition extra Credit (creditLine |> StageEntryLine.accountId)
                          addition extra Debit (Some missingAccountId) ] []
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (AccountIdDoesntMatch uuid) -> uuid = (missingAccountId |> AccountId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.5 removing a line linked to a payment agreement is rejected with a typed error naming the line and the reason, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! entry, debitLineId = createOutgoEntry context "REQ-STG-6.5 linked line removal"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let! _ = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context fixture.Data.cashFlow.legAId debitLineId
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [] [] [ debitLineId ]
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (IngestionStageEntryLineCannotBeRemoved (uuid, LinkedToPaymentAgreement)) ->
                             uuid = (debitLineId |> StageEntryLineId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.5 removing a line a Payment references is rejected with a typed error naming the line and the reason, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! entry, debitLineId = createOutgoEntry context "REQ-STG-6.5 paid line removal"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                do! payWithLine context debitLineId
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [] [] [ debitLineId ]
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (IngestionStageEntryLineCannotBeRemoved (uuid, ReferencedByPayment)) ->
                             uuid = (debitLineId |> StageEntryLineId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-6.5 removing a line a classification run recorded is rejected with a typed error naming the line and the reason, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let debitLineId = debitLine |> StageEntryLine.stageEntryLineId
                // the classifier assigned this line's account, so the run recorded it
                let! recorded = [ debitLineId ] |> Classification.RuleMatch.fetchByStageEntryLineIdList contextForUpdate
                Assert.NotEmpty(recorded)
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [] [] [ debitLineId ]
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (IngestionStageEntryLineCannotBeRemoved (uuid, RecordedInClassificationRun)) ->
                             uuid = (debitLineId |> StageEntryLineId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("amount")>]
    [<InlineData("lineType")>]
    [<InlineData("account")>]
    member _.``REQ-STG-6.5 changing the amount, line type or account of a line linked to a payment agreement is rejected with a typed error naming the line and the reason, and nothing is changed`` (field: string) =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! entry, debitLineId = createOutgoEntry context "REQ-STG-6.5 linked line change"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let! _ = CashFlowOps.constructNewPaymentAgreementLinkAndPersist context fixture.Data.cashFlow.legAId debitLineId
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let! newAmount = Money.fromDecimal 90.00M
                let lineUpdate =
                    match field with
                    | "amount" -> { (noChangeLineUpdates debitLineId) with amountUpdate = SetTo newAmount }
                    | "lineType" -> { (noChangeLineUpdates debitLineId) with entryTypeUpdate = SetTo Credit }
                    | _ -> { (noChangeLineUpdates debitLineId) with accountIdUpdate = SetTo (Some fixture.Data.entertainment5650Id) }
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [ lineUpdate ] [] []
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (IngestionStageEntryLineCannotBeChanged (uuid, LinkedToPaymentAgreement)) ->
                             uuid = (debitLineId |> StageEntryLineId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("amount")>]
    [<InlineData("lineType")>]
    [<InlineData("account")>]
    member _.``REQ-STG-6.5 changing the amount, line type or account of a line a Payment references is rejected with a typed error naming the line and the reason, and nothing is changed`` (field: string) =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! entry, debitLineId = createOutgoEntry context "REQ-STG-6.5 paid line change"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                do! payWithLine context debitLineId
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                let! newAmount = Money.fromDecimal 90.00M
                let lineUpdate =
                    match field with
                    | "amount" -> { (noChangeLineUpdates debitLineId) with amountUpdate = SetTo newAmount }
                    | "lineType" -> { (noChangeLineUpdates debitLineId) with entryTypeUpdate = SetTo Credit }
                    | _ -> { (noChangeLineUpdates debitLineId) with accountIdUpdate = SetTo (Some fixture.Data.entertainment5650Id) }
                return!
                    updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [ lineUpdate ] [] []
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (IngestionStageEntryLineCannotBeChanged (uuid, ReferencedByPayment)) ->
                             uuid = (debitLineId |> StageEntryLineId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Theory>]
    [<InlineData("field")>]
    [<InlineData("add")>]
    [<InlineData("remove")>]
    member _.``REQ-STG-6.6 a manual update on a Posted entry is rejected with a typed error, whether it edits a field, adds a line or removes one, and nothing is changed`` (change: string) =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let creditLineId =
                    entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit) |> StageEntryLine.stageEntryLineId
                let contextForPost = context |> Context.updateInitiationInstant
                do! post contextForPost
                let contextForUpdate = contextForPost |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                Assert.Equal(Posted, StageTestData.latestStatus before)
                let! description = "MARATHON PETRO, edited after posting" |> JournalEntryDescription.create
                let! extra = Money.fromDecimal 10.00M
                let attempt =
                    match change with
                    | "field" ->
                        updateStageEntry contextForUpdate
                            { (noChangeHeaderUpdates headerId) with descriptionUpdate = SetTo description } [] [] []
                    | "add" ->
                        updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) []
                            [ addition extra Debit (Some fixture.Data.entertainment5650Id)
                              addition extra Credit (Some fixture.Data.entertainment5650Id) ] []
                    | _ -> updateStageEntry contextForUpdate (noChangeHeaderUpdates headerId) [] [] [ creditLineId ]
                return!
                    attempt
                    |> expectRejection contextForUpdate headerId
                        (function
                         | AsError (IngestionPostedStageEntryCannotBeModified uuid) -> uuid = (headerId |> StageEntryHeaderId.value)
                         | _ -> false) before
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-STG-4.8 the manual update rejects setting an entry's status to Posted with a typed error, and nothing is changed`` () =
        runCommandRouteAndAutoRollback IngestUpdateStageEntry (fun context ->
            result {
                let! fullResult = StageTestData.runPipeline context
                let entry = fullResult.stagedEntries |> StageTestData.findByDescription "MARATHON PETRO 7218 ANYTOWN US"
                let headerId = entry |> stageEntryHeader |> StageEntryHeader.stageEntryHeaderId
                let debitLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Debit)
                let creditLine = entry |> seLines |> List.find (fun l -> l |> StageEntryLine.lineType = Credit)
                let contextForUpdate = context |> Context.updateInitiationInstant
                let! before = headerId |> fetchByStageEntryHeaderId contextForUpdate
                Assert.Equal(Classified, StageTestData.latestStatus before)
                return!
                    updateStageEntry contextForUpdate { (noChangeHeaderUpdates headerId) with statusUpdate = SetTo Posted } [] [] []
                    |> expectRejection contextForUpdate headerId
                        (function AsError (IngestionManualUpdateCannotSetStatus "Posted") -> true | _ -> false) before
            })
        |> railroadWrapper
