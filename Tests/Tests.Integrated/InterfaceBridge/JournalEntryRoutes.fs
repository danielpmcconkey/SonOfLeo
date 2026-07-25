module Tests.Integrated.InterfaceBridge.JournalEntryRoutes

open System
open InterfaceBridge.InterfaceContracts.JournalContracts
open InterfaceBridge.Json.Json
open Model.Audit
open Model.Ledger.FiscalPeriods
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
    member _.``REQ-JE-2.13 REQ-JE-2.3 PostNew route happy path`` () =
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
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)
    
    [<Theory>]
    [<InlineData ("description", "", "JournalEntryDescriptionIsEmpty")>]
    [<InlineData ("description", "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM", "JournalEntryDescriptionTooLong")>]
    [<InlineData ("source", "", "JournalEntrySourceIsEmpty")>]
    [<InlineData ("source", "01234567890123456789012345678901234567890123456789L", "JournalEntrySourceTooLong")>]
    [<InlineData ("accountCode", "", "AccountCodeIsEmpty")>]
    [<InlineData ("accountCode", "0123456789X", "AccountCodeTooLong")>]
    [<InlineData ("amount", "10.307", "MoneyFailedToConvertImproperPrecision")>]
    [<InlineData ("amount", "19999999999.99", "MoneyFailedToConvertExceededMax")>]
    [<InlineData ("amount", "-19999999999.99", "MoneyFailedToConvertBelowMin")>]
    [<InlineData ("lineType", "Blurgh", "JournalEntryLineTypeInvalid")>]
    [<InlineData ("memo", "", "JournalEntryLineMemoIsEmpty")>]
    [<InlineData ("memo", "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM", "JournalEntryLineMemoTooLong")>]
    [<InlineData ("fi", "", "JournalEntryExternalReferenceIsEmpty")>]
    [<InlineData ("fi", "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC", "JournalEntryExternalReferenceTooLong")>]
    [<InlineData ("refText", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData ("refText", "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC", "JournalEntryReferenceTextTooLong")>]
    [<InlineData ("commentText", "", "JournalEntryCommentIsEmpty")>]
    [<InlineData ("commentText", "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM", "JournalEntryCommentTooLong")>]
    member _.``REQ-JE-2.13 REQ-JE-2.4 PostNew validates input as valid types`` (field:string, value:string, error: string) =
        let today = Calendar.today()
        let descriptionToUse = if field = "description" then value else "JE primitives validation test"
        let sourceToUse = if field = "source" then Some value else None 
        let accountCodeToUse = if field = "accountCode" then value else "F-2210" 
        let amountToUse = if field = "amount" then Decimal.Parse(value) else 173.99M
        let lineTypeToUse = if field = "lineType" then value else "Debit"
        let memoToUse = if field = "memo" then Some value else None
        let fiToUse = if field = "fi" then value else "TestBank"
        let refTextToUse = if field = "refText" then value else "867-5309"
        let commentTextToUse = if field = "commentText" then value else "Fixture comment for testing"
        let input : JournalEntryInput =
            { header = { description = descriptionToUse; source = sourceToUse; entryDate = today }
              lines =
                [ { accountCode = accountCodeToUse; amount = amountToUse; lineType = lineTypeToUse; memo = memoToUse }
                  { accountCode = "F-5350"; amount = amountToUse; lineType = "Credit"; memo = None } ]
              externalReferences = [ { financialInstitution = fiToUse; referenceText = refTextToUse} ]
              comments = [ { secondaryJournalEntryId = None; commentText = commentTextToUse } ] }
        let mutable idToCleanUp = None
        try
            let railroad = result {
                let! payload = input |> toJson<JournalEntryInput>
                do! match routeUiCommandForTesting "JournalEntry" "PostNew" [] payload with
                    | Ok payloadToErase ->
                            let returnedResult = payloadToErase |> fromJson<JournalEntryReturn>
                            match returnedResult with
                            | Ok x ->
                                idToCleanUp <- x.header.id |> JournalEntryHeaderId.fromGuid |> Some
                                Error (TestingError "Expected failure; returned success. Record should be cleaned up.")
                            | Error e -> Error (TestingError $"Expected failure; returned success. Record clean up failed. {e}")
                    | Error e ->
                        if e.IsJournalEntryDescriptionIsEmpty && error = "JournalEntryDescriptionIsEmpty" then Ok()
                        elif e.IsJournalEntryDescriptionTooLong && error = "JournalEntryDescriptionTooLong" then Ok()
                        elif e.IsJournalEntrySourceIsEmpty && error = "JournalEntrySourceIsEmpty" then Ok()
                        elif e.IsJournalEntrySourceTooLong && error = "JournalEntrySourceTooLong" then Ok()
                        elif e.IsAccountCodeIsEmpty && error = "AccountCodeIsEmpty" then Ok()
                        elif e.IsAccountCodeTooLong && error = "AccountCodeTooLong" then Ok()
                        elif e.IsMoneyFailedToConvertImproperPrecision && error = "MoneyFailedToConvertImproperPrecision" then Ok()
                        elif e.IsMoneyFailedToConvertExceededMax && error = "MoneyFailedToConvertExceededMax" then Ok()
                        elif e.IsMoneyFailedToConvertBelowMin && error = "MoneyFailedToConvertBelowMin" then Ok()
                        elif e.IsJournalEntryLineTypeInvalid && error = "JournalEntryLineTypeInvalid" then Ok()
                        elif e.IsJournalEntryLineMemoIsEmpty && error = "JournalEntryLineMemoIsEmpty" then Ok()
                        elif e.IsJournalEntryLineMemoTooLong && error = "JournalEntryLineMemoTooLong" then Ok()
                        elif e.IsJournalEntryExternalReferenceIsEmpty && error = "JournalEntryExternalReferenceIsEmpty" then Ok()
                        elif e.IsJournalEntryExternalReferenceTooLong && error = "JournalEntryExternalReferenceTooLong" then Ok()
                        elif e.IsJournalEntryReferenceTextIsEmpty && error = "JournalEntryReferenceTextIsEmpty" then Ok()
                        elif e.IsJournalEntryReferenceTextTooLong && error = "JournalEntryReferenceTextTooLong" then Ok()
                        elif e.IsJournalEntryCommentIsEmpty && error = "JournalEntryCommentIsEmpty" then Ok()
                        elif e.IsJournalEntryCommentTooLong && error = "JournalEntryCommentTooLong" then Ok()
                        
                        else Error(TestingError $"Wrong error type. Expected {error}. {AppError.toMessage e}")
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route happy path`` () =
        let expected = fixture.Data.basicJeId |> JournalEntryHeaderId.value
        let railroad = result {
            let! payload = { JournalEntryFetchByIdInput.id = expected } |> toJson<JournalEntryFetchByIdInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchById" [] payload
            let! returned = fromJson<JournalEntryReturn> returnPayload
            Assert.Equal(expected, returned.header.id)
            Assert.Equal("Basic journal entry", returned.header.description)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-3.3 FetchByPeriod route happy path`` () =
        let today = Calendar.today()
        let monthF = today.Month.ToString("D2")
        let periodKey = $"{today.Year}-{monthF}"
        let period =
            fixture.Data.fiscalPeriods
            |> List.filter(fun x -> x |> FiscalPeriod.periodKey |> FiscalPeriodKey.value = periodKey)
            |> List.head
        let periodId = period |> FiscalPeriod.fiscalPeriodId
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun x ->
                x
                |> JournalEntry.header
                |> JournalEntryHeader.entryDate
                |> EntryDate.fiscalPeriodId = periodId)
            |> List.length
        let railroad = result {
            let! payload = { JournalEntryFetchByPeriodInput.periodKey = periodKey } |> toJson<JournalEntryFetchByPeriodInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByPeriod" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-3.5 FetchByExternalReference route happy path`` () =
        let fiStr = "TestBank"
        let refStr = "F-SHARED-001"
        let fiOptionStr = Some fiStr
        let refOptionStr = Some refStr
        let fi = JournalRefFinancialInstitution.create fiStr |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let exRef = JournalExternalReferenceText.create refStr |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        let expected =
            fixture.Data.journalEntryExternalReferences
            |> List.filter(fun jer ->
                jer |> JournalEntryExternalReference.financialInstitution = fi &&
                jer |> JournalEntryExternalReference.referenceText = exRef)
            |> List.length
        let railroad = result {
            let! payload = { fi = fiOptionStr; reference = refOptionStr } |> toJson<JournalEntryFetchByExternalReferenceInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByExternalReference" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-3.7 FetchByDateRange route happy path`` () =
        let today = Calendar.today()
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let entryDate = je |> JournalEntry.header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                entryDate >= today && entryDate <= today )
            |> List.length
        let railroad = result {
            let! payload = { beginDate = today; endDateInclusive = today } |> toJson<JournalEntryFetchByDateRangeInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByDateRange" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-4.3 Void route happy path`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! _, jeToVoidId =
                    createTestJournalEntryFromPrimitives "REQ-JE-4.3 JE to void" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [] [] (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToVoidId
                let voidInput : JournalEntryVoidInput =
                    { id = jeToVoidId |> JournalEntryHeaderId.value
                      reason = { secondaryJournalEntryId = None; commentText = "CLI void reason" } }
                let! payload = voidInput |> toJson<JournalEntryVoidInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "Void" [] payload
                let! voided = fromJson<JournalEntryReturn> returnPayload
                Assert.True(voided.header.voidedAt |> Option.isSome)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-4.9 UpdateExternalReference happy path`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-4.9 JE to update ref" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [ ("TestBank", "TXN-001") ]
                        [] (AuditEnvelope.create JournalEntryPostNew)
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
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-4.10 AddExternalReference route happy path`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! _, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-4.10 JE to add reference" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [] [] (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let addInput : JournalEntryAddExternalReferenceInput =
                    { journalEntryId = jeToUpdateId |> JournalEntryHeaderId.value
                      reference = { financialInstitution = "CliAddBank"; referenceText = "CLI-ADD-001" } }
                let! payload = addInput |> toJson<JournalEntryAddExternalReferenceInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "AddExternalReference" [] payload
                let! returned = fromJson<JournalEntryExternalReferenceReturn> returnPayload
                Assert.Equal("CliAddBank", returned.financialInstitution)
                Assert.Equal("CLI-ADD-001", returned.referenceText)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-5.1 AddComment route happy path`` () =
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! _, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-5.1 JE to add comment" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [] [] (AuditEnvelope.create JournalEntryPostNew)
                idToCleanUp_1 <- Some jeToUpdateId
                let addInput : JournalEntryAddCommentInput =
                    { journalEntryId = jeToUpdateId |> JournalEntryHeaderId.value
                      comment = { secondaryJournalEntryId = None; commentText = "CLI added comment" } }
                let! payload = addInput |> toJson<JournalEntryAddCommentInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "AddComment" [] payload
                let! returned = fromJson<JournalEntryCommentReturn> returnPayload
                Assert.Equal("CLI added comment", returned.commentText)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
    
    [<Fact>]
    member _.``REQ-JE-5.3 UpdateComment route happy path`` () =
        let expected = "CLI updated comment text"
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives "REQ-JE-5.3 JE to update comment" None (Calendar.today()) 
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [] [ (None, "Fixture comment for testing") ]
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
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
            
    [<Fact>]
    member _.``REQ-JE-5.3 updateComment rejects empty text`` () = // todo: refactor with multi-fail theory
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
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok () -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
