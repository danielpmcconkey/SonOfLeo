namespace Tests.Integrated.Model.Ledger

open System
open Xunit
open Tests.Integrated
open Model.Audit
open Model.Ledger.Journaling
open Utilities
//
// [<Collection("SharedTestData")>]
// type JournalEntryExternalReferenceTests(fixture: TestDataFixture) =
//
//     // =============================================================================
//     // Update external reference
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-4.9 updateFiAndReferenceText updates FI and value on existing reference`` () =
//         let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let result = JournalEntryExternalReference.updateFiAndReferenceText envelope
//                              fixture.Data.jeWithRefExtRefId "UpdatedBank" "UPD-001" (Some transaction)
//             match result with
//             | Ok r ->
//                 Assert.Equal("UpdatedBank", r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
//                 Assert.Equal("UPD-001", r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-JE-4.9 REQ-SYS-3.3 updateFiAndReferenceText updates modified_at timestamp`` () =
//         let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
//         let expectedInstant = AuditEnvelope.instant envelope
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let result = JournalEntryExternalReference.updateFiAndReferenceText envelope
//                              fixture.Data.jeWithRefExtRefId "TimestampBank" "TS-001" (Some transaction)
//             match result with
//             | Ok r -> Assert.Equal(expectedInstant, r |> JournalEntryExternalReference.modifiedAt)
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-JE-4.9 updateFiAndReferenceText rejects invalid FI — empty string`` () =
//         // wiring test only — component-level rejection coverage lives in
//         // Tests.Isolated (REQ-JE-1.42, REQ-JE-1.44)
//         let envelope = AuditEnvelope.create JournalEntryUpdateExternalReference
//         let result = JournalEntryExternalReference.updateFiAndReferenceText envelope
//                          fixture.Data.jeWithRefExtRefId "" "VALID-001" None
//         Assert.True(Result.isError result)
//
//     // =============================================================================
//     // Add external reference to existing entry
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-4.10 constructNewAndSaveToDb appends a reference to an existing entry`` () =
//         let envelope = AuditEnvelope.create JournalEntryAddExternalReference
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let result = JournalEntryExternalReference.constructNewAndSaveToDb
//                              fixture.Data.basicJeId "NewBank" "NEW-001" envelope (Some transaction)
//             match result with
//             | Ok r ->
//                 Assert.Equal(fixture.Data.basicJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
//                 Assert.Equal("NewBank", r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
//                 Assert.Equal("NEW-001", r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-JE-4.10 constructNewAndSaveToDb generates a unique UUID for the new reference`` () =
//         let envelope = AuditEnvelope.create JournalEntryAddExternalReference
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let result = JournalEntryExternalReference.constructNewAndSaveToDb
//                              fixture.Data.basicJeId "UuidBank" "UUID-001" envelope (Some transaction)
//             match result with
//             | Ok r -> Assert.NotEqual(Guid.Empty, r |> JournalEntryExternalReference.journalEntryExternalReferenceId)
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     [<Fact>]
//     member _.``REQ-JE-4.10 appending a reference is permitted on a voided entry`` () =
//         let envelope = AuditEnvelope.create JournalEntryAddExternalReference
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let result = JournalEntryExternalReference.constructNewAndSaveToDb
//                              fixture.Data.voidedJeId "VoidedBank" "VOID-001" envelope (Some transaction)
//             match result with
//             | Ok r -> Assert.Equal(fixture.Data.voidedJeId, r |> JournalEntryExternalReference.journalEntryHeaderId)
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
//
//     // =============================================================================
//     // Persistence fidelity
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-SYS-5.1 external reference round-trips through persistence with all fields intact`` () =
//         let envelope = AuditEnvelope.create JournalEntryAddExternalReference
//         let transaction = DAL.createDbTransaction() |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
//
//         try
//             let createResult = JournalEntryExternalReference.constructNewAndSaveToDb
//                                    fixture.Data.basicJeId "FidelityBank" "FID-RT-001" envelope (Some transaction)
//             match createResult with
//             | Error e -> Assert.Fail (AppError.toMessage e)
//             | Ok created ->
//                 // fetch inside the same transaction — the create is never committed
//                 let fetchResult = created |> JournalEntryExternalReference.journalEntryExternalReferenceId |> JournalEntryExternalReference.fetchById (Some transaction)
//                 match fetchResult with
//                 | Error e -> Assert.Fail $"Fetch after creation failed: {e}"
//                 | Ok fetched ->
//                     Assert.Equal(created |> JournalEntryExternalReference.journalEntryExternalReferenceId, fetched |> JournalEntryExternalReference.journalEntryExternalReferenceId)
//                     Assert.Equal(created |> JournalEntryExternalReference.journalEntryHeaderId, fetched |> JournalEntryExternalReference.journalEntryHeaderId)
//                     Assert.Equal(
//                         created |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value,
//                         fetched |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value)
//                     Assert.Equal(
//                         created |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value,
//                         fetched |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value)
//                     Assert.Equal(created |> JournalEntryExternalReference.createdAt, fetched |> JournalEntryExternalReference.createdAt)
//                     Assert.Equal(created |> JournalEntryExternalReference.modifiedAt, fetched |> JournalEntryExternalReference.modifiedAt)
//         finally
//             DAL.rollbackDbTransactionAndDisposeConnection transaction |> ignore
