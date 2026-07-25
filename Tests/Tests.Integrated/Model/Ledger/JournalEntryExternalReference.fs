namespace Tests.Integrated.Model.Ledger

open System
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator
open Tests.Integrated.GenericTestProperties
open Xunit
open Tests.Integrated
open Model.Audit
open Model.Ledger.Journaling
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryExternalReferenceTests(fixture: TestDataFixture) =
    
    [<Fact>]
    member _.``REQ-JE-4.9 updateFiAndReferenceText updates FI and value on existing reference`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let fiUpdate = "UpdatedBank" |> createFiUpdateFromString
        let refUpdate = "UPD-001" |> createReferenceTextUpdateFromString
        try
            let result = JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText (Some transaction) envelope
                             fiUpdate refUpdate fixture.Data.jeWithRefExtRefId  
            match result with
            | Ok r ->
                Assert.Equal("UpdatedBank", r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
                Assert.Equal("UPD-001", r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
            
    [<Fact>]
    member _.``REQ-JE-4.9 REQ-SYS-3.3 updateFiAndReferenceText updates modified_at timestamp`` () =
        let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
        let expectedInstant = AuditEnvelope.instant envelope
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let fiUpdate = "TimestampBank" |> createFiUpdateFromString
        let refUpdate = "TS-001" |> createReferenceTextUpdateFromString
        try
            let result = JournalEntryExternalReferenceOrchestration.updateFiAndReferenceText (Some transaction) envelope
                             fiUpdate refUpdate fixture.Data.jeWithRefExtRefId  
            match result with
            | Ok r -> Assert.Equal(expectedInstant, r |> JournalEntryExternalReference.modifiedAt)
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
            
    [<Fact>]
    member _.``REQ-JE-4.10 constructNewAndSaveToDb appends a reference to an existing entry`` () =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let expected1 = "NewBank"
        let expected2 = "NEW-001"
        let fiAdd = expected1 |> createJournalRefFinancialInstitutionFromString
        let refAdd = expected2 |> createJournalExternalReferenceTextFromString
        try
            let result = JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb fixture.Data.basicJeId 
                             fiAdd refAdd envelope (Some transaction)
            match result with
            | Ok r ->
                Assert.Equal(fixture.Data.basicJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
                Assert.Equal(expected1, r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
                Assert.Equal(expected2, r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
            
    [<Fact>]
    member _.``REQ-JE-4.10 constructNewAndSaveToDb generates a unique UUID for the new reference`` () =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let fiAdd = "UuidBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "UUID-001" |> createJournalExternalReferenceTextFromString
        try
            let result = JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                             fixture.Data.basicJeId fiAdd refAdd envelope (Some transaction)
            match result with
            | Ok r -> Assert.NotEqual(Guid.Empty, r |> JournalEntryExternalReference.journalEntryExternalReferenceId |> JournalEntryExternalReferenceId.value)
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
            
    [<Fact>]
    member _.``REQ-JE-4.10 appending a reference is permitted on a voided entry`` () =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let fiAdd = "VoidedBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "VOID-001" |> createJournalExternalReferenceTextFromString
        try
            let result = JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                             fixture.Data.voidedJeId fiAdd refAdd envelope (Some transaction)
            match result with
            | Ok r -> Assert.Equal(fixture.Data.voidedJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
            
    [<Fact>]
    member _.``REQ-SYS-5.1 external reference round-trips through persistence with all fields intact`` () =
        let envelope = AuditEnvelope.create JournalEntryAddExternalReference
        let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let fiAdd = "FidelityBank" |> createJournalRefFinancialInstitutionFromString
        let refAdd = "FID-RT-001" |> createJournalExternalReferenceTextFromString
        try
            let createResult = JournalEntryExternalReferenceOrchestration.constructNewAndSaveToDb
                                   fixture.Data.basicJeId fiAdd refAdd envelope (Some transaction)
            match createResult with
            | Error e -> Assert.Fail (AppError.toMessage e)
            | Ok created ->
                // fetch inside the same transaction — the create is never committed
                let fetchResult = created |> JournalEntryExternalReference.journalEntryExternalReferenceId |> JournalEntryExternalReference.fetchById (Some transaction)
                match fetchResult with
                | Error e -> Assert.Fail $"Fetch after creation failed: {e}"
                | Ok fetched ->
                    Assert.Equal(created |> JournalEntryExternalReference.journalEntryExternalReferenceId, fetched |> JournalEntryExternalReference.journalEntryExternalReferenceId)
                    Assert.Equal(created |> JournalEntryExternalReference.journalEntryHeaderId, fetched |> JournalEntryExternalReference.journalEntryHeaderId)
                    Assert.Equal(
                        created |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value,
                        fetched |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
                    Assert.Equal(
                        created |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value,
                        fetched |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
                    Assert.Equal(created |> JournalEntryExternalReference.createdAt, fetched |> JournalEntryExternalReference.createdAt)
                    Assert.Equal(created |> JournalEntryExternalReference.modifiedAt, fetched |> JournalEntryExternalReference.modifiedAt)
        finally
            DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
