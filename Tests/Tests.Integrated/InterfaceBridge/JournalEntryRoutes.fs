module Tests.Integrated.InterfaceBridge.JournalEntryRoutes

open System
open DataAccessLayer.DbTransaction
open InterfaceBridge.InterfaceContracts.JournalContracts
open Utilities.Json.Json
open Logger.Audit
open Microsoft.FSharp.Reflection
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries.JournalEntry
open Tests.Helpers.EntityFunctions
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Utilities.AppError
open Tests.Helpers.SadPath
open Utilities.FieldUpdate
open Utilities.ResultHelper
open Xunit
open Tests.Helpers.Cleanup
open Utilities
open Context.Context

[<Collection("SharedTestData")>]
type JournalEntryRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.3 PostNew route happy path``() =
        let today = Calendar.today()
        let input: JournalEntryInput =
            { header = { description = "CLI PostNew test"; source = Some "CliTest"; entryDate = today }
              lines =
                [ { accountCode = "F-2210"; amount = 50.00M; lineType = "Debit"; memo = None }
                  { accountCode = "F-5350"; amount = 50.00M; lineType = "Credit"; memo = None } ]
              externalReferences = []
              comments = [] }
        let mutable idToCleanUp = None
        try
            result {
                let! payload = input |> toJson<JournalEntryInput>
                let! resultPayload = routeUiCommandForTesting "JournalEntry" "PostNew" [] payload
                let! returned = fromJson<JournalEntryReturn> resultPayload
                idToCleanUp <- returned.header.id |> JournalEntryHeaderId.fromGuid |> Some
                Assert.Equal("CLI PostNew test", returned.header.description)
                Assert.Equal(2, returned.lines |> List.length)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok() -> ()
            | Error e -> failwith(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.3 PostNew route unhappy path cleans up after itself``() =
        let expected = fixture.Data.journalEntries |> List.length
        let today = Calendar.today()
        let input: JournalEntryInput =
            { header = { description = "CLI PostNew test"; source = Some "CliTest"; entryDate = today }
              lines = [ { accountCode = "F-2210"; amount = 50.00M; lineType = "Debit"; memo = None } ] // only 1 line should fail and roll back
              externalReferences = []
              comments = [] }
        let mutable idToCleanUp = None
        try
            result {
                let! payload = input |> toJson<JournalEntryInput>
                match routeUiCommandForTesting "JournalEntry" "PostNew" [] payload with
                | Error(JournalEntryInsufficientLines _) ->
                    let railroad =
                        let context = create NoTransaction FetchOnly
                        result {
                            let absurdBegin = today.PlusYears(-7)
                            let absurdEnd = today.PlusYears(7)
                            let! newState = fetchByDateRange context absurdBegin absurdEnd
                            let newCount = newState |> List.length
                            Assert.Equal(expected, newCount)
                            return ()
                        }
                    match railroad with
                    | Ok _ -> ()
                    | Error e -> Assert.Fail(AppError.toMessage e)
                | Error e -> Assert.Fail $"Wrong error. {AppError.toMessage e}"
                | Ok _ -> // clean-up on aisle four
                    Assert.Fail "Expected failure; got success. You have data to clean up"
            }
            |> railroadWrapper
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok() -> ()
            | Error e -> failwith(AppError.toMessage e)

    [<Theory>]
    [<InlineData("description", "", "JournalEntryDescriptionIsEmpty")>]
    [<InlineData("description",
                 "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM",
                 "JournalEntryDescriptionTooLong")>]
    [<InlineData("source", "", "JournalEntrySourceIsEmpty")>]
    [<InlineData("source", "01234567890123456789012345678901234567890123456789L", "JournalEntrySourceTooLong")>]
    [<InlineData("accountCode", "", "AccountCodeIsEmpty")>]
    [<InlineData("accountCode", "0123456789X", "AccountCodeTooLong")>]
    [<InlineData("accountCode", "Z-9999", "AccountCodeDoesntMatchAccountId")>]
    [<InlineData("entryDate", "closedPeriod", "JournalEntryHeaderEntryDateInvalid")>]
    [<InlineData("amount", "10.307", "MoneyFailedToConvertImproperPrecision")>]
    [<InlineData("amount", "19999999999.99", "MoneyFailedToConvertExceededMax")>]
    [<InlineData("amount", "-19999999999.99", "MoneyFailedToConvertBelowMin")>]
    [<InlineData("lineType", "Blurgh", "JournalEntryLineTypeInvalid")>]
    [<InlineData("memo", "", "JournalEntryLineMemoIsEmpty")>]
    [<InlineData("memo",
                 "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM",
                 "JournalEntryLineMemoTooLong")>]
    [<InlineData("fi", "", "JournalRefFinancialInstitutionIsEmpty")>]
    [<InlineData("fi",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalRefFinancialInstitutionTooLong")>]
    [<InlineData("refText", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData("refText",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalEntryReferenceTextTooLong")>]
    [<InlineData("commentText", "", "JournalEntryCommentIsEmpty")>]
    [<InlineData("commentText",
                 "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM",
                 "JournalEntryCommentTooLong")>]
    member _.``REQ-JE-2.13 REQ-JE-2.4 PostNew validates input as valid types``
        (field: string, value: string, expectedError: string)
        =
        let today = Calendar.today()
        let descriptionToUse =
            if field = "description" then
                value
            else
                "JE primitives validation test"
        let sourceToUse = if field = "source" then Some value else None
        let accountCodeToUse = if field = "accountCode" then value else "F-2210"
        let amountToUse = if field = "amount" then Decimal.Parse(value) else 173.99M
        let lineTypeToUse = if field = "lineType" then value else "Debit"
        let memoToUse = if field = "memo" then Some value else None
        let fiToUse = if field = "fi" then value else "TestBank"
        let refTextToUse = if field = "refText" then value else "867-5309"
        let commentTextToUse =
            if field = "commentText" then
                value
            else
                "Fixture comment for testing"
        let entryDateToUse = if field = "entryDate" then today.PlusMonths(-5) else today
        let input: JournalEntryInput =
            { header = { description = descriptionToUse; source = sourceToUse; entryDate = entryDateToUse }
              lines =
                [ { accountCode = accountCodeToUse; amount = amountToUse; lineType = lineTypeToUse; memo = memoToUse }
                  { accountCode = "F-5350"; amount = amountToUse; lineType = "Credit"; memo = None } ]
              externalReferences = [ { financialInstitution = fiToUse; referenceText = refTextToUse } ]
              comments = [ { secondaryJournalEntryId = None; commentText = commentTextToUse } ] }
        let mutable idToCleanUp = None
        try
            result {
                let! payload = input |> toJson<JournalEntryInput>
                do!
                    isCorrectErrorString
                        (routeUiCommandForTesting "JournalEntry" "PostNew" [] payload)
                        expectedError
                        (Some "Record should be cleaned up.")
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpJournalEntryId idToCleanUp with
            | Ok() -> ()
            | Error e -> failwith(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.2 FetchById route happy path``() =
        let expected = fixture.Data.basicJeId |> JournalEntryHeaderId.value
        result {
            let! payload = { JournalEntryFetchByIdInput.id = expected } |> toJson<JournalEntryFetchByIdInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchById" [] payload
            let! returned = fromJson<JournalEntryReturn> returnPayload
            Assert.Equal(expected, returned.header.id)
            Assert.Equal("Basic journal entry", returned.header.description)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.3 FetchByPeriod route happy path``() =
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
            |> List.filter(fun x -> x |> header |> JournalEntryHeader.entryDate |> EntryDate.fiscalPeriodId = periodId)
            |> List.length
        result {
            let! payload =
                { JournalEntryFetchByPeriodInput.periodKey = periodKey } |> toJson<JournalEntryFetchByPeriodInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByPeriod" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.5 FetchByExternalReference route happy path``() =
        let fiStr = "TestBank"
        let refStr = "F-SHARED-001"
        let fiOptionStr = Some fiStr
        let refOptionStr = Some refStr
        let fi =
            JournalRefFinancialInstitution.create fiStr
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let exRef =
            JournalExternalReferenceText.create refStr
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let expected =
            fixture.Data.journalEntryExternalReferences
            |> List.filter(fun jer ->
                jer |> JournalEntryExternalReference.financialInstitution = fi
                && jer |> JournalEntryExternalReference.referenceText = exRef)
            |> List.length
        result {
            let! payload =
                { fi = fiOptionStr; reference = refOptionStr } |> toJson<JournalEntryFetchByExternalReferenceInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByExternalReference" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.7 FetchByDateRange route happy path``() =
        let today = Calendar.today()
        let expected =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let entryDate = je |> header |> JournalEntryHeader.entryDate |> EntryDate.entryDate
                entryDate >= today && entryDate <= today)
            |> List.length
        result {
            let! payload =
                { beginDate = today; endDateInclusive = today } |> toJson<JournalEntryFetchByDateRangeInput>
            let! returnPayload = routeUiCommandForTesting "JournalEntry" "FetchByDateRange" [] payload
            let! returned = fromJson<JournalEntryReturn list> returnPayload
            Assert.Equal(expected, returned |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.3 Void route happy path``() =
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! _, jeToVoidId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-4.3 JE to void"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                idToCleanUp_1 <- Some jeToVoidId
                let voidInput: JournalEntryVoidInput =
                    { id = jeToVoidId |> JournalEntryHeaderId.value
                      reason = { secondaryJournalEntryId = None; commentText = "CLI void reason" } }
                let! payload = voidInput |> toJson<JournalEntryVoidInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "Void" [] payload
                let! voided = fromJson<JournalEntryReturn> returnPayload
                Assert.True(voided.header.voidedAt |> Option.isSome)
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-4.9 UpdateExternalReference happy path``() =
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-4.9 JE to update ref"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [ ("TestBank", "TXN-001") ]
                        []
                idToCleanUp_1 <- Some jeToUpdateId
                let refUuid =
                    jeToUpdate
                    |> externalReferences
                    |> List.head
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    |> JournalEntryExternalReferenceId.value
                let updateInput: JournalEntryUpdateExternalReferenceInput =
                    { id = refUuid; fi = Some "CliUpdatedBank"; reference = Some "CLI-UPD-001" }
                let! payload = updateInput |> toJson<JournalEntryUpdateExternalReferenceInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "UpdateExternalReference" [] payload
                let! returned = fromJson<JournalEntryExternalReferenceReturn> returnPayload
                Assert.Equal("CliUpdatedBank", returned.financialInstitution)
                Assert.Equal("CLI-UPD-001", returned.referenceText)
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-4.10 AddExternalReference route happy path``() =
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! _, jeToUpdateId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-4.10 JE to add reference"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                idToCleanUp_1 <- Some jeToUpdateId
                let addInput: JournalEntryAddExternalReferenceInput =
                    { journalEntryId = jeToUpdateId |> JournalEntryHeaderId.value
                      reference = { financialInstitution = "CliAddBank"; referenceText = "CLI-ADD-001" } }
                let! payload = addInput |> toJson<JournalEntryAddExternalReferenceInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "AddExternalReference" [] payload
                let! returned = fromJson<JournalEntryExternalReferenceReturn> returnPayload
                Assert.Equal("CliAddBank", returned.financialInstitution)
                Assert.Equal("CLI-ADD-001", returned.referenceText)
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-5.1 AddComment route happy path``() =
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! _, jeToUpdateId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-5.1 JE to add comment"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        []
                idToCleanUp_1 <- Some jeToUpdateId
                let addInput: JournalEntryAddCommentInput =
                    { journalEntryId = jeToUpdateId |> JournalEntryHeaderId.value
                      comment = { secondaryJournalEntryId = None; commentText = "CLI added comment" } }
                let! payload = addInput |> toJson<JournalEntryAddCommentInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "AddComment" [] payload
                let! returned = fromJson<JournalEntryCommentReturn> returnPayload
                Assert.Equal("CLI added comment", returned.commentText)
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-5.3 UpdateComment route happy path``() =
        let expected = "CLI updated comment text"
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-5.3 JE to update comment"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        [ (None, "Fixture comment for testing") ]
                idToCleanUp_1 <- Some jeToUpdateId
                let commentUuid =
                    jeToUpdate
                    |> comments
                    |> List.head
                    |> JournalEntryComment.journalEntryCommentId
                    |> JournalEntryCommentId.value
                let updateInput: JournalEntryUpdateCommentInput =
                    { id = commentUuid; secondaryJournalEntryId = NoChange; commentText = SetTo expected }
                let! payload = updateInput |> toJson<JournalEntryUpdateCommentInput>
                let! returnPayload = routeUiCommandForTesting "JournalEntry" "UpdateComment" [] payload
                let! returned = fromJson<JournalEntryCommentReturn> returnPayload
                Assert.Equal(expected, returned.commentText)
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.7 FetchByDateRange rejects begin date after end date``() =
        let today = Calendar.today()
        let yesterday = today.PlusDays(-1)
        result {
            let! payload =
                { beginDate = today; endDateInclusive = yesterday } |> toJson<JournalEntryFetchByDateRangeInput>
            do!
                isCorrectError
                    (routeUiCommandForTesting "JournalEntry" "FetchByDateRange" [] payload)
                    JournalEntryFetchByDateRangeBeginAfterEnd
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-5.1 AddComment rejects non-existent secondary JE header ID``() =
        let primaryJeId = fixture.Data.basicJeId |> JournalEntryHeaderId.value
        let bogusSecondaryId = Guid.NewGuid()
        let input: JournalEntryAddCommentInput =
            { journalEntryId = primaryJeId
              comment = { secondaryJournalEntryId = Some bogusSecondaryId; commentText = "Test comment" } }
        result {
            let! payload = input |> toJson<JournalEntryAddCommentInput>
            do!
                isCorrectError
                    (routeUiCommandForTesting "JournalEntry" "AddComment" [] payload)
                    JournalEntryCommentSecondaryJeHeaderIdNotFound
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("commentEmpty", "JournalEntryCommentIsEmpty")>]
    [<InlineData("commentTooLong", "JournalEntryCommentTooLong")>]
    [<InlineData("sameIds", "JournalEntryCommentPrimaryAndSecondaryIdsAreSame")>]
    [<InlineData("bogusId", "JournalEntryHeaderIdDoesntExist")>]
    [<InlineData("bogusSecondary", "JournalEntryCommentSecondaryJeHeaderIdNotFound")>]
    [<InlineData("voidedJe", "JournalEntryVoidingNoOp")>]
    [<InlineData("closedPeriod", "JournalEntryVoidingFiscalPeriodIsClosed")>]
    member _.``REQ-JE-4.4 Void validates input and state`` (scenario: string, expectedError: string)=
        let jeIdToUse =
            match scenario with
            | "bogusId" -> Guid.NewGuid()
            | "voidedJe" -> fixture.Data.voidedJeId |> JournalEntryHeaderId.value
            | "closedPeriod" -> fixture.Data.jeInClosedPeriodId |> JournalEntryHeaderId.value
            | _ -> fixture.Data.basicJeId |> JournalEntryHeaderId.value
        let commentTextToUse =
            match scenario with
            | "commentEmpty" -> ""
            | "commentTooLong" -> String.replicate 201 "0123456789" + "X"
            | _ -> "Valid void reason"
        let secondaryIdToUse =
            match scenario with
            | "sameIds" -> Some jeIdToUse
            | "bogusSecondary" -> Some(Guid.NewGuid())
            | _ -> None
        let voidInput: JournalEntryVoidInput =
            { id = jeIdToUse
              reason = { secondaryJournalEntryId = secondaryIdToUse; commentText = commentTextToUse } }
        result {
            let! payload = voidInput |> toJson<JournalEntryVoidInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "JournalEntry" "Void" [] payload)
                    expectedError
                    (Some "This probably caused other tests to fail.")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-4.9 UpdateExternalReference rejects no-op update``() =
        let mutable idToCleanUp = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-4.9 JE for no-op ref update"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        [ ("TestBank", "TXN-NOOP") ]
                        []
                idToCleanUp <- Some jeToUpdateId
                let refUuid =
                    jeToUpdate
                    |> externalReferences
                    |> List.head
                    |> JournalEntryExternalReference.journalEntryExternalReferenceId
                    |> JournalEntryExternalReferenceId.value
                let updateInput: JournalEntryUpdateExternalReferenceInput =
                    { id = refUuid; fi = None; reference = None }
                let! payload = updateInput |> toJson<JournalEntryUpdateExternalReferenceInput>
                do!
                    isCorrectErrorEmpty
                        (routeUiCommandForTesting "JournalEntry" "UpdateExternalReference" [] payload)
                        JournalEntryReferenceUpdateNoOp
                        None
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Theory>]
    [<InlineData("commentEmpty", "JournalEntryCommentIsEmpty")>]
    [<InlineData("commentTooLong", "JournalEntryCommentTooLong")>]
    [<InlineData("sameIds", "JournalEntryCommentPrimaryAndSecondaryIdsAreSame")>]
    member _.``REQ-JE-5.3 UpdateComment validates input and state``
        (scenario: string, expectedError: string)
        =
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! jeToUpdate, jeToUpdateId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-5.3 JE to update comment"
                        None
                        (Calendar.today())
                        [ (fixture.Data.entertainment5650Id, 75.00M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 75.00M, "Credit", None) ]
                        []
                        [ (None, "Fixture comment for testing") ]
                idToCleanUp_1 <- Some jeToUpdateId
                let commentUuid =
                    jeToUpdate
                    |> comments
                    |> List.head
                    |> JournalEntryComment.journalEntryCommentId
                    |> JournalEntryCommentId.value
                let commentTextUpdate =
                    match scenario with
                    | "commentEmpty" -> SetTo ""
                    | "commentTooLong" -> SetTo(String.replicate 201 "0123456789" + "X")
                    | _ -> NoChange
                let secondaryIdUpdate =
                    match scenario with
                    | "sameIds" -> SetTo(Some(jeToUpdateId |> JournalEntryHeaderId.value))
                    | _ -> NoChange
                let updateInput: JournalEntryUpdateCommentInput =
                    { id = commentUuid; secondaryJournalEntryId = secondaryIdUpdate; commentText = commentTextUpdate }
                let! payload = updateInput |> toJson<JournalEntryUpdateCommentInput>
                do!
                    isCorrectErrorString
                        (routeUiCommandForTesting "JournalEntry" "UpdateComment" [] payload)
                        expectedError
                        None
                return ()
            }
            |> railroadWrapper
        finally
            match idToCleanUp_1 |> cleanUpJournalEntryId with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Theory>]
    [<InlineData("fi", "", "JournalRefFinancialInstitutionIsEmpty")>]
    [<InlineData("fi",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalRefFinancialInstitutionTooLong")>]
    [<InlineData("reference", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData("reference",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalEntryReferenceTextTooLong")>]
    [<InlineData("bothNull", "", "JournalEntryFetchByReferenceBothArgumentsNull")>]
    member _.``REQ-JE-3.5 FetchByExternalReference validates input as valid types``
        (field: string, value: string, expectedError: string) =
        let fiToUse =
            match field with
            | "fi" -> Some value
            | "bothNull" -> None
            | _ -> Some "TestBank"
        let referenceToUse =
            match field with
            | "reference" -> Some value
            | "bothNull" -> None
            | _ -> Some "TXN-001"
        let input: JournalEntryFetchByExternalReferenceInput =
            { fi = fiToUse; reference = referenceToUse }
        result {
            let! payload = input |> toJson<JournalEntryFetchByExternalReferenceInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "JournalEntry" "FetchByExternalReference" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("fi", "", "JournalRefFinancialInstitutionIsEmpty")>]
    [<InlineData("fi",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalRefFinancialInstitutionTooLong")>]
    [<InlineData("referenceText", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData("referenceText",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalEntryReferenceTextTooLong")>]
    [<InlineData("bogusId", "", "JournalEntryHeaderIdDoesntExist")>]
    member _.``REQ-JE-4.10 AddExternalReference validates input as valid types``
        (field: string, value: string, expectedError: string) =
        let jeIdToUse =
            if field = "bogusId" then Guid.NewGuid()
            else fixture.Data.basicJeId |> JournalEntryHeaderId.value
        let fiToUse = if field = "fi" then value else "TestBank"
        let refTextToUse = if field = "referenceText" then value else "TXN-VALID"
        let input: JournalEntryAddExternalReferenceInput =
            { journalEntryId = jeIdToUse
              reference = { financialInstitution = fiToUse; referenceText = refTextToUse } }
        result {
            let! payload = input |> toJson<JournalEntryAddExternalReferenceInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "JournalEntry" "AddExternalReference" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("", "AccountCodeIsEmpty")>]
    [<InlineData("01234567890", "AccountCodeTooLong")>]
    [<InlineData("Z-9999", "AccountCodeDoesntMatchAccountId")>]
    member _.``REQ-JE-3.4 FetchLinesByAccount validates input as valid types``
        (accountCode: string, expectedError: string) =
        let input: JournalEntryFetchLinesByAccountInput =
            { accountCode = accountCode; nonVoidedOnly = false }
        result {
            let! payload = input |> toJson<JournalEntryFetchLinesByAccountInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "JournalEntry" "FetchLinesByAccount" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("fi", "", "JournalRefFinancialInstitutionIsEmpty")>]
    [<InlineData("fi",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalRefFinancialInstitutionTooLong")>]
    [<InlineData("reference", "", "JournalEntryReferenceTextIsEmpty")>]
    [<InlineData("reference",
                 "01234567890123456789012345678901234567890123456789L01234567890123456789012345678901234567890123456789LC",
                 "JournalEntryReferenceTextTooLong")>]
    member _.``REQ-JE-4.9 UpdateExternalReference validates input as valid types``
        (field: string, value: string, expectedError: string) =
        let fiToUse = if field = "fi" then Some value else Some "TestBank"
        let referenceToUse = if field = "reference" then Some value else Some "TXN-001"
        let input: JournalEntryUpdateExternalReferenceInput =
            { id = Guid.NewGuid(); fi = fiToUse; reference = referenceToUse }
        result {
            let! payload = input |> toJson<JournalEntryUpdateExternalReferenceInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "JournalEntry" "UpdateExternalReference" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("commentEmpty", "JournalEntryCommentIsEmpty")>]
    [<InlineData("commentTooLong", "JournalEntryCommentTooLong")>]
    member _.``REQ-JE-5.1 AddComment validates input as valid types``
        (scenario: string, expectedError: string) =
        let commentTextToUse =
            match scenario with
            | "commentEmpty" -> ""
            | "commentTooLong" -> String.replicate 201 "0123456789" + "X"
            | _ -> failwith $"Unknown scenario: {scenario}"
        let input: JournalEntryAddCommentInput =
            { journalEntryId = fixture.Data.basicJeId |> JournalEntryHeaderId.value
              comment = { secondaryJournalEntryId = None; commentText = commentTextToUse } }
        result {
            let! payload = input |> toJson<JournalEntryAddCommentInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "JournalEntry" "AddComment" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper
