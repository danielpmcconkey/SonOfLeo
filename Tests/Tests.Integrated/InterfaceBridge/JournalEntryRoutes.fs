module Tests.Integrated.InterfaceBridge.JournalEntryRoutes

open System
open InterfaceBridge.InterfaceContracts.JournalContracts
open InterfaceBridge.Json.Json
open Utilities.DAL
open Xunit
open Tests.Integrated
open Tests.Integrated._Cleanup
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Utilities
//
// [<Collection("SharedTestData")>]
// type JournalEntryRouteTests(fixture: TestDataFixture) =
//
//     // =============================================================================
//     // PostNew route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-2.13 REQ-JE-2.3 PostNew route creates a journal entry and returns it as JSON`` () =
//         let today = Calendar.today()
//         let input : JournalEntryInput =
//             { header = { description = "CLI PostNew test"; source = Some "CliTest"; entryDate = today }
//               lines =
//                 [ { accountCode = "F-2210"; amount = 50.00M; lineType = "Debit"; memo = None }
//                   { accountCode = "F-5350"; amount = 50.00M; lineType = "Credit"; memo = None } ]
//               externalReferences = []
//               comments = [] }
//         let mutable idToCleanUp = None
//         try
//             let railroad = result {
//                 let! payload = input |> toJson<JournalEntryInput>
//                 let code, stdout, e = runCli ["JournalEntry"; "PostNew"] payload
//                 do! if code <> 0 then Error $"PostNew returned non-zero: {e}" else Ok ()
//                 let! returned = fromJson<JournalEntryReturn> stdout
//                 idToCleanUp <- Some returned.header.id
//                 Assert.Equal("CLI PostNew test", returned.header.description)
//                 Assert.Equal(2, returned.lines |> List.length)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             match cleanUpJournalEntryId idToCleanUp with
//             | Ok () -> ()
//             | Error e -> failwith e
//
//     [<Fact>]
//     member _.``REQ-JE-2.12 PostNew route returns exit code 1 and error on invalid input`` () =
//         let today = Calendar.today()
//         let input : JournalEntryInput =
//             { header = { description = "CLI invalid test"; source = None; entryDate = today }
//               lines =
//                 [ { accountCode = "F-2210"; amount = 100.00M; lineType = "Debit"; memo = None }
//                   { accountCode = "F-5350"; amount = 75.00M; lineType = "Credit"; memo = None } ]
//               externalReferences = []
//               comments = [] }
//         let railroad = result {
//             let! payload = input |> toJson<JournalEntryInput>
//             let code, _, _ = runCli ["JournalEntry"; "PostNew"] payload
//             Assert.Equal(1, code)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-2.4 PostNew fails with invalid account code`` () =
//         let today = Calendar.today()
//         let expected = "Provided Account Code of Rumpelstiltskin didn't match any recorded Accounts in the database."
//         let input : JournalEntryInput =
//             { header = { description = "CLI PostNew test"; source = Some "CliTest"; entryDate = today }
//               lines =
//                 [ { accountCode = "Rumpelstiltskin"; amount = 50.00M; lineType = "Debit"; memo = None }
//                   { accountCode = "F-5350"; amount = 50.00M; lineType = "Credit"; memo = None } ]
//               externalReferences = []
//               comments = [] }
//         let railroad = result {
//             let! payload = input |> toJson<JournalEntryInput>
//             let code, _, e = runCli ["JournalEntry"; "PostNew"] payload
//             Assert.Equal(1, code)
//             Assert.Contains(expected, e)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // FetchById route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-3.2 FetchById route returns the correct entry as JSON`` () =
//         let railroad = result {
//             let! payload = { JournalEntryFetchByIdInput.id = fixture.Data.basicJeId } |> toJson<JournalEntryFetchByIdInput>
//             let code, stdout, e = runCli ["JournalEntry"; "FetchById"] payload
//             do! if code <> 0 then Error $"FetchById returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryReturn> stdout
//             Assert.Equal(fixture.Data.basicJeId, returned.header.id)
//             Assert.Equal("Fixture basic JE", returned.header.description)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-3.2 FetchById route returns exit code 1 for nonexistent ID`` () =
//         let railroad = result {
//             let! payload = { JournalEntryFetchByIdInput.id = Guid.NewGuid() } |> toJson<JournalEntryFetchByIdInput>
//             let code, _, _ = runCli ["JournalEntry"; "FetchById"] payload
//             Assert.Equal(1, code)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // FetchByPeriod route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-3.3 FetchByPeriod route returns entries for a given period key`` () =
//         let today = Calendar.today()
//         let monthF = today.Month.ToString("D2")
//         let periodKey = $"{today.Year}-{monthF}"
//         let railroad = result {
//             let! payload = { JournalEntryFetchByPeriodInput.periodKey = periodKey } |> toJson<JournalEntryFetchByPeriodInput>
//             let code, stdout, e = runCli ["JournalEntry"; "FetchByPeriod"] payload
//             do! if code <> 0 then Error $"FetchByPeriod returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryReturn list> stdout
//             Assert.True(returned |> List.length >= 1)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // FetchByExternalReference route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-3.5 FetchByExternalReference route returns matching entries`` () =
//         let railroad = result {
//             let! payload = { fi = Some "TestBank"; reference = Some "TXN-001" } |> toJson<JournalEntryFetchByExternalReferenceInput>
//             let code, stdout, e = runCli ["JournalEntry"; "FetchByExternalReference"] payload
//             do! if code <> 0 then Error $"FetchByExternalReference returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryReturn list> stdout
//             Assert.True(returned |> List.length >= 1)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-3.8 FetchByExternalReference route with FI only returns matching entries`` () =
//         let railroad = result {
//             let! payload = { fi = Some "TestBank"; reference = None } |> toJson<JournalEntryFetchByExternalReferenceInput>
//             let code, stdout, e = runCli ["JournalEntry"; "FetchByExternalReference"] payload
//             do! if code <> 0 then Error $"FetchByExternalReference (FI only) returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryReturn list> stdout
//             Assert.True(returned |> List.length >= 1)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // FetchByDateRange route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-3.7 FetchByDateRange route returns entries within date range`` () =
//         let today = Calendar.today()
//         let railroad = result {
//             let! payload = { beginDate = today; endDateInclusive = today } |> toJson<JournalEntryFetchByDateRangeInput>
//             let code, stdout, e = runCli ["JournalEntry"; "FetchByDateRange"] payload
//             do! if code <> 0 then Error $"FetchByDateRange returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryReturn list> stdout
//             Assert.True(returned |> List.length >= 1)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // Void route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-4.3 Void route voids an entry and returns it with void marker`` () =
//         // consumable fixture victim — the CLI commits, and its voided end-state
//         // is by design
//         let railroad = result {
//             let voidInput : JournalEntryVoidInput =
//                 { id = fixture.Data.cliVoidVictimId
//                   reason = { secondaryJournalEntryId = None; commentText = "CLI void reason" } }
//             let! voidPayload = voidInput |> toJson<JournalEntryVoidInput>
//             let voidCode, voidStdout, voidErr = runCli ["JournalEntry"; "Void"] voidPayload
//             do! if voidCode <> 0 then Error $"Void returned non-zero: {voidErr}" else Ok ()
//             let! voided = fromJson<JournalEntryReturn> voidStdout
//             Assert.True(voided.header.voidedAt |> Option.isSome)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-4.6 Void route returns exit code 1 for already-voided entry`` () =
//         let railroad = result {
//             let voidInput : JournalEntryVoidInput =
//                 { id = fixture.Data.voidedJeId
//                   reason = { secondaryJournalEntryId = None; commentText = "Should fail" } }
//             let! payload = voidInput |> toJson<JournalEntryVoidInput>
//             let code, _, _ = runCli ["JournalEntry"; "Void"] payload
//             Assert.Equal(1, code)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // UpdateExternalReference route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-4.9 UpdateExternalReference route updates FI and value`` () =
//         let railroad = result {
//             // consumable fixture victim — this update commits, and the fixture's
//             // TestBank/TXN-001 ref must survive for the fetchByReference tests
//             let updateInput : JournalEntryUpdateExternalReferenceInput =
//                 { id = fixture.Data.cliUpdateVictimExtRefId; fi = "CliUpdatedBank"; reference = "CLI-UPD-001" }
//             let! payload = updateInput |> toJson<JournalEntryUpdateExternalReferenceInput>
//             let code, stdout, e = runCli ["JournalEntry"; "UpdateExternalReference"] payload
//             do! if code <> 0 then Error $"UpdateExternalReference returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryExternalReferenceReturn> stdout
//             Assert.Equal("CliUpdatedBank", returned.financialInstitution)
//             Assert.Equal("CLI-UPD-001", returned.referenceText)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     // =============================================================================
//     // AddExternalReference route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-4.10 AddExternalReference route appends a reference to an existing entry`` () =
//         let mutable idToCleanUp = None
//         try
//             let railroad = result {
//                 let addInput : JournalEntryAddExternalReferenceInput =
//                     { journalEntryId = fixture.Data.basicJeId
//                       reference = { financialInstitution = "CliAddBank"; referenceText = "CLI-ADD-001" } }
//                 let! payload = addInput |> toJson<JournalEntryAddExternalReferenceInput>
//                 let code, stdout, e = runCli ["JournalEntry"; "AddExternalReference"] payload
//                 do! if code <> 0 then Error $"AddExternalReference returned non-zero: {e}" else Ok ()
//                 let! returned = fromJson<JournalEntryExternalReferenceReturn> stdout
//                 idToCleanUp <- Some returned.id
//                 Assert.Equal("CliAddBank", returned.financialInstitution)
//                 Assert.Equal("CLI-ADD-001", returned.referenceText)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             match cleanUpJournalEntryExtReferenceId idToCleanUp with
//             | Ok () -> ()
//             | Error e -> failwith e
//
//     // =============================================================================
//     // AddComment route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-5.1 AddComment route attaches a comment to an entry`` () =
//         let mutable idToCleanUp = None
//         try
//             let railroad = result {
//                 let addInput : JournalEntryAddCommentInput =
//                     { journalEntryId = fixture.Data.basicJeId
//                       comment = { secondaryJournalEntryId = None; commentText = "CLI added comment" } }
//                 let! payload = addInput |> toJson<JournalEntryAddCommentInput>
//                 let code, stdout, e = runCli ["JournalEntry"; "AddComment"] payload
//                 do! if code <> 0 then Error $"AddComment returned non-zero: {e}" else Ok ()
//                 let! returned = fromJson<JournalEntryCommentReturn> stdout
//                 idToCleanUp <- Some returned.id
//                 Assert.Equal("CLI added comment", returned.commentText)
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             match cleanUpJournalEntryCommentId idToCleanUp with
//             | Ok () -> ()
//             | Error e -> failwith e
//
//     // =============================================================================
//     // UpdateComment route
//     // =============================================================================
//
//     [<Fact>]
//     member _.``REQ-JE-5.3 UpdateComment route amends comment text`` () =
//         // consumable fixture victim — this update commits; fixtureCommentId must
//         // stay pristine for the model-level update tests
//         let railroad = result {
//             let updateInput : JournalEntryUpdateCommentInput =
//                 { id = fixture.Data.cliUpdateVictimCommentId
//                   secondaryJournalEntryId = NoChange
//                   commentText = SetTo "CLI updated comment text" }
//             let! payload = updateInput |> toJson<JournalEntryUpdateCommentInput>
//             let code, stdout, e = runCli ["JournalEntry"; "UpdateComment"] payload
//             do! if code <> 0 then Error $"UpdateComment returned non-zero: {e}" else Ok ()
//             let! returned = fromJson<JournalEntryCommentReturn> stdout
//             Assert.Equal("CLI updated comment text", returned.commentText)
//             return ()
//         }
//         match railroad with
//         | Ok _ -> ()
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//
//
//     [<Fact>]
//     member _.``REQ-JE-5.3 updateComment rejects empty text`` () =
//         // wiring test only — component-level rejection coverage lives in
//         // Tests.Isolated (REQ-JE-1.54)
//         
//         let mutable idToCleanUp = None
//         let expected = "CommentText cannot be empty"
//         try
//             let railroad = result {
//                 let updateInput : JournalEntryUpdateCommentInput =
//                     { id = fixture.Data.cliUpdateVictimCommentId
//                       secondaryJournalEntryId = NoChange
//                       commentText = SetTo "" }
//                 let! payload = updateInput |> toJson<JournalEntryUpdateCommentInput>
//                 let code, _, e = runCli ["JournalEntry"; "UpdateComment"] payload
//                 do! if code = 0 then Error "UpdateComment returned a success code when it shouldn't have" else Ok ()
//                 
//                 Assert.Equal(expected, e.Trim())
//                 return ()
//             }
//             match railroad with
//             | Ok _ -> ()
//             | Error e -> Assert.Fail (AppError.toMessage e)
//         finally
//             match cleanUpJournalEntryCommentId idToCleanUp with
//             | Ok () -> ()
//             | Error e -> failwith e
//
