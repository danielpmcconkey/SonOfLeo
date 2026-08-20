namespace Tests.Integrated.ModelOrchestrator

open InterfaceBridge.CommandRoute
open Logger.Audit
open ModelOrchestrator
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.SadPath
open Utilities.AppError
open Utilities.FieldUpdate
open Xunit

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
