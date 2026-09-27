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
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.SadPath
open App.Utility.IAppError
open App.Utility.Result
open Tests.Helpers.TestError
open App.Utility.FieldUpdate
open Xunit
open Business.FinancialServices.Ledger.LedgerError

[<Collection("SharedTestData")>]
type JournalEntryExternalReferenceOrchestrationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-4.9 updateFiAndReferenceText rejects no-op when both fields are NoChange``() =
        let referenceId = fixture.Data.jeWithRefExtRefId
        runCommandRouteAndAutoRollback JournalEntryUpdateExternalReference (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    NoChange
                    NoChange
                    referenceId
            isCorrectErrorEmpty result JournalEntryReferenceUpdateNoOp None)
        |> railroadWrapper

    // =========================================================================
    // Plan defects 8–17 (2026-09-27)
    // =========================================================================

    [<Fact>]
    member _.``REQ-SYS-6.2 updating a journal entry external reference by an ID no reference holds fails with a typed not-found error naming the kind of record and the ID`` () =
        let missingId = JournalEntryComponent.JournalEntryExternalReferenceId.create ()
        runCommandRouteAndAutoRollback JournalEntryUpdateExternalReference (fun context ->
            result {
                let! text = "REQ-SYS-6.2-NO-SUCH-REF" |> JournalEntryComponent.JournalExternalReferenceText.create
                return!
                    match JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText context NoChange (SetTo text) missingId with
                    | Error (AsError (JournalEntryExternalReferenceIdDoesntExist uuid)) ->
                        Assert.Equal(missingId |> JournalEntryComponent.JournalEntryExternalReferenceId.value, uuid)
                        Ok ()
                    | Error e -> Error (TestingError $"Wrong error: {e.DomainName}.{e.CaseName}: {e.ToMessage()}")
                    | Ok _ -> Error (TestingError "Expected failure; got success")
            })
        |> railroadWrapper
