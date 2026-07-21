module Tests.Integrated.InterfaceBridge.JournalEntryRoutes

open System
open InterfaceBridge.InterfaceContracts.JournalContracts
open InterfaceBridge.Json.Json
open Model.Audit
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.InterfaceBridge._routeResolver
open Utilities.AppError
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Xunit
open Tests.Integrated
open Tests.Integrated._Cleanup
open Utilities

[<Collection("SharedTestData")>]
type JournalEntryRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.3 PostNew route creates a journal entry and returns it as JSON`` () =
        let today = Calendar.today()
        let input : JournalEntryInput =
            { header = { description = "CLI PostNew test"; source = Some "CliTest"; entryDate = today }
              lines =
                [ { accountCode = "F-2210"; amount = 50.00M; lineType = "Debit"; memo = None }
                  { accountCode = "F-5350"; amount = 50.00M; lineType = "Credit"; memo = None } ]
              externalReferences = []
              comments = [] }
        let mutable idToCleanUp = None
        try
            let railroad = result {
                let! payload = input |> toJson<JournalEntryInput>
                let! resultPayload = routeUiCommandForTesting "JournalEntry" "PostNew" [] payload
                let! returned = fromJson<JournalEntryReturn> resultPayload
                idToCleanUp <- returned.header.id |> JournalEntryHeaderId.fromGuid |> Some
                Assert.Equal("CLI PostNew test", returned.header.description)
                Assert.Equal(2, returned.lines |> List.length)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-2.12 PostNew route fails when amounts don't add up`` () = // todo: move this to the orchestration tests
        let today = Calendar.today()
        let input : JournalEntryInput =
            { header = { description = "CLI invalid test"; source = None; entryDate = today }
              lines =
                [ { accountCode = "F-2210"; amount = 100.00M; lineType = "Debit"; memo = None }
                  { accountCode = "F-5350"; amount = 75.00M; lineType = "Credit"; memo = None } ]
              externalReferences = []
              comments = [] }
        let railroad = result {
            let! payload = input |> toJson<JournalEntryInput>
            do! match routeUiCommandForTesting "JournalEntry" "PostNew" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (JournalEntryDebitCreditMismatch _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-2.4 PostNew fails with invalid account code`` () =  // todo: move this to the orchestration tests
        let today = Calendar.today()
        let input : JournalEntryInput =
            { header = { description = "CLI PostNew test"; source = Some "CliTest"; entryDate = today }
              lines =
                [ { accountCode = "Rumpelstiltskin"; amount = 50.00M; lineType = "Debit"; memo = None }
                  { accountCode = "F-5350"; amount = 50.00M; lineType = "Credit"; memo = None } ]
              externalReferences = []
              comments = [] }
        let railroad = result {
            let! payload = input |> toJson<JournalEntryInput>
            do! match routeUiCommandForTesting "JournalEntry" "PostNew" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (InterfaceBridgeConversionFailure _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route returns the correct entry as JSON`` () =
        let expected = fixture.Data.basicJeId |> JournalEntryHeaderId.value
        let railroad = result {
            let! payload = { JournalEntryFetchByIdInput.id = expected } |> toJson<JournalEntryFetchByIdInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchById" [] payload
            let! returned = fromJson<JournalEntryReturn> returnPayload
            Assert.Equal(expected, returned.header.id)
            Assert.Equal("Basic journal entry", returned.header.description)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route returns exit code 1 for nonexistent ID`` () = // todo move this to orchestration tests
        let railroad = result {
            let! payload = { JournalEntryFetchByIdInput.id = Guid.NewGuid() } |> toJson<JournalEntryFetchByIdInput>
            do! match routeUiCommandForTesting "JournalEntry" "FetchById" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (DalResultantRowsDidntMatchExpectation _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.3 FetchByPeriod route returns entries for a given period key`` () =
        let expected = 5
        let today = Calendar.today()
        let monthF = today.Month.ToString("D2")
        let periodKey = $"{today.Year}-{monthF}"
        let railroad = result {
            let! payload = { JournalEntryFetchByPeriodInput.periodKey = periodKey } |> toJson<JournalEntryFetchByPeriodInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByPeriod" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.5 FetchByExternalReference route returns matching entries`` () =
        let expected = 1
        let railroad = result {
            let! payload = { fi = Some "TestBank"; reference = Some "TXN-001" } |> toJson<JournalEntryFetchByExternalReferenceInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByExternalReference" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.8 FetchByExternalReference route with FI only returns matching entries`` () =
        let expected = 1
        let railroad = result {
            let! payload = { fi = Some "TestBank"; reference = None } |> toJson<JournalEntryFetchByExternalReferenceInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByExternalReference" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.7 FetchByDateRange route returns entries within date range`` () =
        let expected = 3
        let today = Calendar.today()
        let railroad = result {
            let! payload = { beginDate = today; endDateInclusive = today } |> toJson<JournalEntryFetchByDateRangeInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByDateRange" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-4.3 Void route voids an entry and returns it with void marker`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! _, jeToVoidId =
                    createTestJournalEntryFromPrimitives "REQ-JE-4.3 JE to void" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                        (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToVoidId
                let voidInput : JournalEntryVoidInput =
                    { id = jeToVoidId |> JournalEntryHeaderId.value
                      reason = { secondaryJournalEntryId = None; commentText = "CLI void reason" } }
                let! payload = voidInput |> toJson<JournalEntryVoidInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "Void" [] payload
                let! voided = fromJson<JournalEntryReturn> returnPayload
                Assert.True(voided.header.voidedAt |> Option.isSome)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-4.6 Void route fails for already-voided entry`` () =
        let railroad = result {
            let voidInput : JournalEntryVoidInput =
                { id = fixture.Data.voidedJeId |> JournalEntryHeaderId.value
                  reason = { secondaryJournalEntryId = None; commentText = "Should fail" } }
            let! payload = voidInput |> toJson<JournalEntryVoidInput>
            do! match routeUiCommandForTesting "JournalEntry" "Void" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (JournalEntryVoidingNoOp _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-4.9 UpdateExternalReference route updates FI and value`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-4.9 JE to update ref" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [ ("TestBank", "TXN-001") ]
                        []
                        (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let refUuid = jeToUpdate
                              |> JournalEntry.externalReferences
                              |> List.head
                              |> JournalEntryExternalReference.journalEntryExternalReferenceId
                              |> JournalEntryExternalReferenceId.value
                let updateInput : JournalEntryUpdateExternalReferenceInput =
                    { id = refUuid; fi = Some "CliUpdatedBank"; reference = Some "CLI-UPD-001" }
                let! payload = updateInput |> toJson<JournalEntryUpdateExternalReferenceInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "UpdateExternalReference" [] payload
                let! returned = fromJson<JournalEntryExternalReferenceReturn> returnPayload
                Assert.Equal("CliUpdatedBank", returned.financialInstitution)
                Assert.Equal("CLI-UPD-001", returned.referenceText)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-4.10 AddExternalReference route appends a reference to an existing entry`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! _, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-4.10 JE to add reference" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                        (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let addInput : JournalEntryAddExternalReferenceInput =
                    { journalEntryId = jeToUpdateId |> JournalEntryHeaderId.value
                      reference = { financialInstitution = "CliAddBank"; referenceText = "CLI-ADD-001" } }
                let! payload = addInput |> toJson<JournalEntryAddExternalReferenceInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "AddExternalReference" [] payload
                let! returned = fromJson<JournalEntryExternalReferenceReturn> returnPayload
                Assert.Equal("CliAddBank", returned.financialInstitution)
                Assert.Equal("CLI-ADD-001", returned.referenceText)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-5.1 AddComment route attaches a comment to an entry`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! _, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-5.1 JE to add comment" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                        (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let addInput : JournalEntryAddCommentInput =
                    { journalEntryId = jeToUpdateId |> JournalEntryHeaderId.value
                      comment = { secondaryJournalEntryId = None; commentText = "CLI added comment" } }
                let! payload = addInput |> toJson<JournalEntryAddCommentInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "AddComment" [] payload
                let! returned = fromJson<JournalEntryCommentReturn> returnPayload
                Assert.Equal("CLI added comment", returned.commentText)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-5.3 UpdateComment route amends comment text`` () =
        let expected = "CLI updated comment text"
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-5.3 JE to update comment" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        [ (None, "Fixture comment for testing") ]
                        (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let commentUuid = jeToUpdate |> JournalEntry.comments |> List.head |> JournalEntryComment.journalEntryCommentId |> JournalEntryCommentId.value
                let updateInput : JournalEntryUpdateCommentInput =
                    { id = commentUuid
                      secondaryJournalEntryId = NoChange
                      commentText = SetTo expected }
                let! payload = updateInput |> toJson<JournalEntryUpdateCommentInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "UpdateComment" [] payload
                let! returned = fromJson<JournalEntryCommentReturn> returnPayload
                Assert.Equal(expected, returned.commentText)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)



    [<Fact>]
    member _.``REQ-JE-5.3 updateComment rejects empty text`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-5.3 JE to update comment" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        [ (None, "Fixture comment for testing") ]
                        (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let commentUuid = jeToUpdate |> JournalEntry.comments |> List.head |> JournalEntryComment.journalEntryCommentId |> JournalEntryCommentId.value
                
                let updateInput : JournalEntryUpdateCommentInput =
                    { id = commentUuid
                      secondaryJournalEntryId = NoChange
                      commentText = SetTo "" }
                let! payload = updateInput |> toJson<JournalEntryUpdateCommentInput>
                do! match routeUiCommandForTesting "JournalEntry" "UpdateComment" [] payload with
                    | Ok _ -> Error(TestingError "Expected failure; returned success.")
                    | Error (JournalEntryCommentIsEmpty _) -> Ok ()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)

