namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open Ui.InterfaceBridge.CommandRoute
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open Tests.Helpers
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Business.CrossDomainOrchestration.JournalEntryOrchestration.JournalEntryOrchestration
open Business.FinancialServices.Ledger.JournalEntryComponent
open App.Utility
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Business.FinancialServices.Ledger.LedgerError

[<Collection("SharedTestData")>]
type JournalEntryCreationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.11 constructNewAndPersist posts a valid journal entry and returns it``() =
        let expected = "JE create happy"
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! jeHappy, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        expected
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                let actual = jeHappy |> header |> JournalEntryHeader.description |> JournalEntryDescription.value
                Assert.Equal(expected, actual)
                Assert.Equal(2, jeHappy |> jeLines |> List.length)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.1 two entries posted in one transaction get header IDs distinct from each other and from every fixture entry's``() =
        let today = Calendar.today()
        let lines = [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None); (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _, firstId = createTestJournalEntryFromPrimitives context "JE header id 1" None today lines [] []
                let! _, secondId = createTestJournalEntryFromPrimitives context "JE header id 2" None today lines [] []
                Assert.NotEqual(firstId, secondId)
                let fixtureIds = fixture.Data.journalEntries |> List.map (header >> JournalEntryHeader.journalEntryHeaderId)
                Assert.DoesNotContain(firstId, fixtureIds)
                Assert.DoesNotContain(secondId, fixtureIds)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.2 REQ-JE-1.21 each line of a posted entry gets an ID distinct from its sibling's, from its header's and from every fixture line's``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, headerId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE line ids"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                let lineIds = je |> jeLines |> List.map (JournalEntryLine.journalEntryLineId >> JournalEntryLineId.value)
                Assert.Equal(2, lineIds |> List.distinct |> List.length)
                Assert.DoesNotContain(headerId |> JournalEntryHeaderId.value, lineIds)
                let fixtureLineIds = fixture.Data.journalEntryLines |> List.map (JournalEntryLine.journalEntryLineId >> JournalEntryLineId.value)
                Assert.Empty(Set.intersect (Set.ofList lineIds) (Set.ofList fixtureLineIds))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.9 REQ-JE-1.40 each external reference of a posted entry gets an ID distinct from its sibling's, from its entry's header and line IDs and from every fixture reference's``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, headerId =
                    createTestJournalEntryFromPrimitives
                        context
                        "JE reference ids"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        [ ("TestBank", "F-SHARED-001"); ("TestBank", "TXN-001") ]
                        []
                let referenceIds =
                    je |> externalReferences
                    |> List.map (JournalEntryExternalReference.journalEntryExternalReferenceId >> JournalEntryExternalReferenceId.value)
                Assert.Equal(2, referenceIds |> List.distinct |> List.length)
                let ownIds =
                    (headerId |> JournalEntryHeaderId.value)
                    :: (je |> jeLines |> List.map (JournalEntryLine.journalEntryLineId >> JournalEntryLineId.value))
                Assert.Empty(Set.intersect (Set.ofList referenceIds) (Set.ofList ownIds))
                let fixtureReferenceIds =
                    fixture.Data.journalEntryExternalReferences
                    |> List.map (JournalEntryExternalReference.journalEntryExternalReferenceId >> JournalEntryExternalReferenceId.value)
                Assert.Empty(Set.intersect (Set.ofList referenceIds) (Set.ofList fixtureReferenceIds))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.2 constructNewAndPersist sets created_at and modified_at from AuditEnvelope``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let expected = context |> Context.getInitiationInstant
            result {
                let! jeHappy, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                Assert.Equal(expected, jeHappy |> header |> JournalEntryHeader.createdAt)
                Assert.Equal(expected, jeHappy |> header |> JournalEntryHeader.modifiedAt)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.46 constructNewAndPersist accepts an entry with zero external references``() =
        let today = Calendar.today()
        let explicitlyEmpty = []
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        explicitlyEmpty
                        []
                Assert.Empty(je |> externalReferences)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.46 constructNewAndPersist accepts an entry with multiple external references``() =
        let today = Calendar.today()
        let explicitlyMultiple = [ ("TestBank", "F-SHARED-001"); ("TestBank", "TXN-001") ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        explicitlyMultiple
                        []
                let actualReferences =
                    je
                    |> externalReferences
                    |> List.map(fun r ->
                        (r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value),
                        (r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value))
                    |> List.sort
                Assert.Equal<(string * string) list>(explicitlyMultiple |> List.sort, actualReferences)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.55 constructNewAndPersist accepts an entry with zero comments``() =
        let today = Calendar.today()
        let explicitlyEmpty = []
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        explicitlyEmpty
                Assert.Empty(je |> comments)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.55 constructNewAndPersist accepts an entry with multiple comments``() =
        let today = Calendar.today()
        let explicitlyMultiple =
            [ (None, "Fixture comment for testing")
              (None, "Fixture comment for testing 2") ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        explicitlyMultiple
                let actualCommentTexts =
                    je
                    |> comments
                    |> List.map(fun c -> c |> JournalEntryComment.commentText |> CommentText.value)
                    |> List.sort
                Assert.Equal<string list>(explicitlyMultiple |> List.map snd |> List.sort, actualCommentTexts)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.6 constructNewAndPersist accepts an entry with null source``() =
        let today = Calendar.today()
        let explicitlyNone = None
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        explicitlyNone
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                Assert.Equal<JournalEntrySource option>(None, je |> header |> JournalEntryHeader.source)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.26 constructNewAndPersist accepts lines with null memos``() =
        let today = Calendar.today()
        let explicitlyNone = None
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! je, _ = // the test helper resolves to constructNewAndPersist
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        explicitlyNone
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", explicitlyNone)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", explicitlyNone) ]
                        []
                        []
                let actualMemos = je |> jeLines |> List.map JournalEntryLine.memo
                Assert.Equal(2, actualMemos |> List.length)
                Assert.Equal<JournalEntryLineMemo option list>([ None; None ], actualMemos)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.48 entries posted with a (source FI, reference) pair already used, on the same entry or on another, each keep their full reference list, duplicates included``() =
        let today = Calendar.today()
        let sameRef = ("TestBank", "F-SHARED-001")
        let explicitlySame = [ sameRef; sameRef ]
        let storedPairs context entryId =
            JournalEntryExternalReference.fetchByJournalEntryId context entryId
            |> Result.map (List.map (fun r ->
                r |> JournalEntryExternalReference.financialInstitution |> JournalRefFinancialInstitution.value,
                r |> JournalEntryExternalReference.referenceText |> JournalExternalReferenceText.value))
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                // the same pair twice on one entry
                let! _, firstId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-1.48 1"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        explicitlySame
                        []
                // and again on a different entry; the fixture already carries this pair too
                let! _, secondId =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-1.48 2"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 286.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 286.04M, "Credit", None) ]
                        [ sameRef ]
                        []
                let! firstPairs = storedPairs context firstId
                let! secondPairs = storedPairs context secondId
                Assert.Equal<(string * string) list>(explicitlySame, firstPairs)
                Assert.Equal<(string * string) list>([ sameRef ], secondPairs)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.12 constructNewAndPersist rejects entry with fewer than 2 lines``() =
        let today = Calendar.today()
        let onlyOneLine = [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None) ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives context "JE create unhappy432" None today onlyOneLine [] []
            match result with
            | Error (AsError (JournalEntryInsufficientLines _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.13 constructNewAndPersist rejects unbalanced entry — debits != credits``() =
        let today = Calendar.today()
        let unbalancedLines =
            [ (fixture.Data.entertainment5650Id, 15.79M, "Debit", None)
              (fixture.Data.creditCard2220Id, 340.99M, "Credit", None) ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives context "JE create unhappy892" None today unbalancedLines [] []
            match result with
            | Error (AsError (JournalEntryDebitCreditMismatch _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.22 constructNewAndPersist rejects line with nonexistent account ID``() =
        let today = Calendar.today()
        let phoneyAccountId = Guid.NewGuid() |> AccountId.fromGuid
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives
                    context
                    "JE create unhappy351"
                    None
                    today
                    [ (fixture.Data.entertainment5650Id, 1453840.27M, "Debit", None)
                      (phoneyAccountId, 1453840.27M, "Credit", None) ]
                    []
                    []
            match result with
            | Error (AsError (JournalEntryLineAccountDoesntExist _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Theory>]
    [<InlineData("0.00")>]
    [<InlineData("-5.00")>]
    member _.``REQ-JE-1.24 constructNewAndPersist rejects line whose amount is not positive``(amount: string) =
        let today = Calendar.today()
        let amountToUse = Decimal.Parse amount
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives
                    context
                    "JE create nonpositive line"
                    None
                    today
                    [ (fixture.Data.entertainment5650Id, amountToUse, "Debit", None)
                      (fixture.Data.moneyMarket1270Id, amountToUse, "Credit", None) ]
                    []
                    []
            match result with
            | Error (AsError (JournalEntryLineNonPositiveAmount _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.5 REQ-JE-2.6 REQ-JE-1.11 constructNewAndPersist rejects entry date w/ no matching fiscal period``() =
        let today = Calendar.today()
        let badDate = today.PlusYears(-3)
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives
                    context
                    "JE create unhappy"
                    None
                    badDate
                    [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                      (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                    []
                    []
            match result with
            | Error (AsError (JournalEntryDateNotInFiscalPeriod _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.7 constructNewAndPersist rejects entry date in a closed fiscal period``() =
        let badDate = (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives
                    context
                    "JE create unhappy"
                    None
                    badDate
                    [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                      (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                    []
                    []
            match result with
            | Error (AsError (JournalEntryHeaderEntryDateInvalid _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.8 constructNewAndPersist rejects line referencing an inactive account as of entry date``() =
        let badAccount = fixture.Data.closedAccount
        let badId = badAccount |> Account.accountId
        let badDate =
            (badAccount |> Account.activityPeriod |> ActivityPeriod.activeEnd |> Option.get).PlusMonths(1)
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives
                    context
                    "JE create unhappy"
                    None
                    badDate
                    [ (badId, 86.04M, "Debit", None)
                      (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                    []
                    []
            match result with
            | Error (AsError (JournalEntryLineAccountInactive _)) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {e.ToMessage()}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper
