namespace Tests.Integrated.Model.Ledger

open System
open Logger.Audit
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.InterfaceBridge._routeResolver
open Xunit
open Tests.Integrated
open Model.Ledger.Journaling
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryExternalReferenceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-4.9 updateFiAndReferenceText updates FI and value on existing reference``() =
        let fiUpdate = "UpdatedBank" |> createFiUpdateFromString
        let refUpdate = "UPD-001" |> createReferenceTextUpdateFromString
        runFuncAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    fiUpdate
                    refUpdate
                    fixture.Data.jeWithRefExtRefId
            match result with
            | Ok r ->
                Assert.Equal(
                    "UpdatedBank",
                    r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
                )
                Assert.Equal(
                    "UPD-001",
                    r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                )
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.9 REQ-SYS-3.3 updateFiAndReferenceText updates modified_at timestamp``() =
        let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
        let expectedInstant = AuditEnvelope.instant envelope
        let fiUpdate = "TimestampBank" |> createFiUpdateFromString
        let refUpdate = "TS-001" |> createReferenceTextUpdateFromString
        runFuncAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText
                    context
                    envelope
                    fiUpdate
                    refUpdate
                    fixture.Data.jeWithRefExtRefId
            match result with
            | Ok r -> Assert.Equal(expectedInstant, r |> JournalEntryExternalReference.modifiedAt)
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.10 constructNewAndSaveToDb appends a reference to an existing entry``() =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let expected1 = "NewBank"
        let expected2 = "NEW-001"
        let fiAdd = expected1 |> createJournalRefFinancialInstitutionFromString
        let refAdd = expected2 |> createJournalExternalReferenceTextFromString
        runFuncAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    fixture.Data.basicJeId
                    fiAdd
                    refAdd
                    envelope
                    context
            match result with
            | Ok r ->
                Assert.Equal(fixture.Data.basicJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
                Assert.Equal(
                    expected1,
                    r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value
                )
                Assert.Equal(
                    expected2,
                    r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value
                )
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.10 constructNewAndSaveToDb generates a unique UUID for the new reference``() =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let fiAdd = "UuidBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "UUID-001" |> createJournalExternalReferenceTextFromString
        runFuncAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    fixture.Data.basicJeId
                    fiAdd
                    refAdd
                    envelope
                    context
            match result with
            | Ok r ->
                Assert.NotEqual(
                    Guid.Empty,
                    r
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    |> JournalEntryExternalReferenceId.value
                )
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-4.10 appending a reference is permitted on a voided entry``() =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let fiAdd = "VoidedBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "VOID-001" |> createJournalExternalReferenceTextFromString
        runFuncAndAutoRollback AccountCreate (fun context ->
            let result =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    fixture.Data.voidedJeId
                    fiAdd
                    refAdd
                    envelope
                    context
            match result with
            | Ok r -> Assert.Equal(fixture.Data.voidedJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-SYS-5.1 external reference round-trips through persistence with all fields intact``() =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let fiAdd = "FidelityBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "FID-RT-001" |> createJournalExternalReferenceTextFromString
        runFuncAndAutoRollback AccountCreate (fun context ->
            let createResult =
                JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                    fixture.Data.basicJeId
                    fiAdd
                    refAdd
                    envelope
                    context
            match createResult with
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok created ->
                // fetch inside the same contextsaction — the create is never committed
                let fetchResult =
                    created
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    |> JournalEntryExternalReference.fetchById context
                match fetchResult with
                | Error e -> Assert.Fail $"Fetch after creation failed: {e}"
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
                    ))
