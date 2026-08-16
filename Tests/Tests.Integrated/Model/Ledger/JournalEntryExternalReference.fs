namespace Tests.Integrated.Model.Ledger

open System

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Xunit
open Tests.Helpers
open Model.Ledger.Journaling
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryExternalReferenceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-4.9 updateFiAndReferenceText updates FI and value on existing reference``() =
        let expectedFi = "UpdatedBank"
        let expectedRef = "UPD-001"
        let fiUpdate = expectedFi |> createFiUpdateFromString
        let refUpdate = expectedRef |> createReferenceTextUpdateFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    fiUpdate
                    refUpdate
                    fixture.Data.jeWithRefExtRefId
            match result with
            | Ok r ->
                let actualFi =
                    r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
                let actualRef = r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                Assert.Equal(expectedFi, actualFi)
                Assert.Equal(expectedRef, actualRef)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.9 REQ-SYS-3.3 updateFiAndReferenceText updates modified_at timestamp``() =
        let fiUpdate = "TimestampBank" |> createFiUpdateFromString
        let refUpdate = "TS-001" |> createReferenceTextUpdateFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let expectedInstant = context |> Context.getInitiationInstant
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    fiUpdate
                    refUpdate
                    fixture.Data.jeWithRefExtRefId
            match result with
            | Ok r ->
                Assert.Equal(expectedInstant, r |> JournalEntryExternalReference.modifiedAt)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.10 constructNewAndSaveToDb appends a reference to an existing entry``() =
        let expected1 = "NewBank"
        let expected2 = "NEW-001"
        let fiAdd = expected1 |> createJournalRefFinancialInstitutionFromString
        let refAdd = expected2 |> createJournalExternalReferenceTextFromString
        runCommandRouteAndAutoRollback JournalEntryAddExternalReference (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    fiAdd
                    refAdd
            match result with
            | Ok r ->
                Assert.Equal(fixture.Data.basicJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
                let actual1 =
                    r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
                let actual2 = r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                Assert.Equal(expected1, actual1)
                Assert.Equal(expected2, actual2)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.10 constructNewAndSaveToDb generates a unique UUID for the new reference``() =
        let fiAdd = "UuidBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "UUID-001" |> createJournalExternalReferenceTextFromString
        runCommandRouteAndAutoRollback JournalEntryAddExternalReference (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    fiAdd
                    refAdd
            match result with
            | Ok r ->
                Assert.NotEqual(
                    Guid.Empty,
                    r
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    |> JournalEntryExternalReferenceId.value
                )
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.10 appending a reference is permitted on a voided entry``() =
        let fiAdd = "VoidedBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "VOID-001" |> createJournalExternalReferenceTextFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.voidedJeId
                    fiAdd
                    refAdd
            match result with
            | Ok r ->
                Assert.Equal(fixture.Data.voidedJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.9 updateFiAndReferenceText is permitted on a voided entry``() =
        let expectedFi = "UpdatedVoidedBank"
        let expectedRef = "UPD-VOIDED-001"
        let fiUpdate = expectedFi |> createFiUpdateFromString
        let refUpdate = expectedRef |> createReferenceTextUpdateFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    fiUpdate
                    refUpdate
                    fixture.Data.voidedJeExtRefId
            match result with
            | Ok r ->
                let actualFi =
                    r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
                let actualRef = r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                Assert.Equal(expectedFi, actualFi)
                Assert.Equal(expectedRef, actualRef)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.9 updateFiAndReferenceText is permitted when fiscal period is closed``() =
        let expectedFi = "UpdatedClosedPeriodBank"
        let expectedRef = "UPD-CLOSED-001"
        let fiUpdate = expectedFi |> createFiUpdateFromString
        let refUpdate = expectedRef |> createReferenceTextUpdateFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    fiUpdate
                    refUpdate
                    fixture.Data.jeInClosedPeriodExtRefId
            match result with
            | Ok r ->
                let actualFi =
                    r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
                let actualRef = r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                Assert.Equal(expectedFi, actualFi)
                Assert.Equal(expectedRef, actualRef)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.10 appending a reference is permitted when fiscal period is closed``() =
        let fiAdd = "ClosedPeriodBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "CLOSED-001" |> createJournalExternalReferenceTextFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.jeInClosedPeriodId
                    fiAdd
                    refAdd
            match result with
            | Ok r ->
                Assert.Equal(fixture.Data.jeInClosedPeriodId, r |> JournalEntryExternalReference.journalEntryHeaderId)
                Ok()
            | Error e -> Error e)
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-5.1 external reference round-trips through persistence with all fields intact``() =
        let fiAdd = "FidelityBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "FID-RT-001" |> createJournalExternalReferenceTextFromString
        runCommandRouteAndAutoRollback AccountCreate (fun context ->
            let createResult =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    context
                    fixture.Data.basicJeId
                    fiAdd
                    refAdd
            match createResult with
            | Error e -> Error e
            | Ok created ->
                // fetch inside the same contextsaction — the create is never committed
                let fetchResult =
                    created
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    |> JournalEntryExternalReference.fetchById context
                match fetchResult with
                | Error e -> Error(TestingError $"Fetch after creation failed: {e}")
                | Ok fetched ->
                    Assert.Equal(
                        created |> JournalEntryExternalReference.journalEntryExternalReferenceId,
                        fetched |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    )
                    Assert.Equal(
                        created |> JournalEntryExternalReference.journalEntryHeaderId,
                        fetched |> JournalEntryExternalReference.journalEntryHeaderId
                    )
                    Assert.Equal(
                        created
                        |> JournalEntryExternalReference.financialInstitution
                        |> JournalRefFinancialInstitution.value,
                        fetched
                        |> JournalEntryExternalReference.financialInstitution
                        |> JournalRefFinancialInstitution.value
                    )
                    Assert.Equal(
                        created |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value,
                        fetched |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                    )
                    Assert.Equal(
                        created |> JournalEntryExternalReference.createdAt,
                        fetched |> JournalEntryExternalReference.createdAt
                    )
                    Assert.Equal(
                        created |> JournalEntryExternalReference.modifiedAt,
                        fetched |> JournalEntryExternalReference.modifiedAt
                    )
                    Ok())
        |> railroadWrapper
