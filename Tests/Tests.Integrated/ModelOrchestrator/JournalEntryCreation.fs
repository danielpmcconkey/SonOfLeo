namespace Tests.Integrated.ModelOrchestrator

open System

open InterfaceBridge.CommandRoute
open Logger.Audit
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Utilities.ResultHelper
open Xunit
open Tests.Helpers
open ModelOrchestrator.JournalEntries.JournalEntry
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryCreationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.11 constructNewAndSaveToDb posts a valid journal entry and returns it``() =
        let expected = "JE create happy"
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
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
                Assert.Equal(2, jeHappy |> lines |> List.length)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.1 constructNewAndSaveToDb generates a unique UUID for the header``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _, jeHappyId = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                Assert.NotEqual(Guid.Empty, jeHappyId |> JournalEntryHeaderId.value)
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.2 REQ-JE-1.21 constructNewAndSaveToDb generates unique UUIDs for each line``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                jeHappy
                |> lines
                |> List.map(fun x -> x |> JournalEntryLine.journalEntryLineId)
                |> List.iter(fun x -> Assert.NotEqual(Guid.Empty, x |> JournalEntryLineId.value))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.9 REQ-JE-1.40 constructNewAndSaveToDb generates unique UUIDs for each external reference``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        [ ("TestBank", "F-SHARED-001"); ("TestBank", "TXN-001") ]
                        []
                jeHappy
                |> externalReferences
                |> List.map(fun x -> x |> JournalEntryExternalReference.journalEntryExternalReferenceId)
                |> List.iter(fun x -> Assert.NotEqual(Guid.Empty, x |> JournalEntryExternalReferenceId.value))
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-SYS-3.2 constructNewAndSaveToDb sets created_at and modified_at from AuditEnvelope``() =
        let today = Calendar.today()
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let expected = context |> Context.getInitiationInstant
            result {
                let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
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
    member _.``REQ-JE-1.46 constructNewAndSaveToDb accepts an entry with zero external references``() =
        let today = Calendar.today()
        let explicitlyEmpty = []
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        explicitlyEmpty
                        []
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.46 constructNewAndSaveToDb accepts an entry with multiple external references``() =
        let today = Calendar.today()
        let explicitlyMultiple = [ ("TestBank", "F-SHARED-001"); ("TestBank", "TXN-001") ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        explicitlyMultiple
                        []
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.55 constructNewAndSaveToDb accepts an entry with zero comments``() =
        let today = Calendar.today()
        let explicitlyEmpty = []
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        explicitlyEmpty
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.55 constructNewAndSaveToDb accepts an entry with multiple comments``() =
        let today = Calendar.today()
        let explicitlyMultiple =
            [ (None, "Fixture comment for testing")
              (None, "Fixture comment for testing 2") ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        explicitlyMultiple
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.6 constructNewAndSaveToDb accepts an entry with null source``() =
        let today = Calendar.today()
        let explicitlyNone = None
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        explicitlyNone
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        []
                        []
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.26 constructNewAndSaveToDb accepts lines with null memos``() =
        let today = Calendar.today()
        let explicitlyNone = None
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _ = // the test helper resolves to constructNewAndSaveToDb
                    createTestJournalEntryFromPrimitives
                        context
                        "JE create happy"
                        explicitlyNone
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", explicitlyNone)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", explicitlyNone) ]
                        []
                        []
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.48 constructNewAndSaveToDb accepts duplicate source_fi/reference pairs``() =
        let today = Calendar.today()
        let sameRef = ("TestBank", "F-SHARED-001")
        let explicitlySame = [ sameRef; sameRef ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                // test that you can do it in one single entry 
                let! _ = 
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-1.48 1"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                        explicitlySame
                        []
                
                // now test that you can do it across different entities
                let! _ =
                    createTestJournalEntryFromPrimitives
                        context
                        "REQ-JE-1.48 2"
                        None
                        today
                        [ (fixture.Data.entertainment5650Id, 286.04M, "Debit", None)
                          (fixture.Data.creditCard2220Id, 286.04M, "Credit", None) ]
                        [sameRef]
                        []
                return ()
            })
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.12 constructNewAndSaveToDb rejects entry with fewer than 2 lines``() =
        let today = Calendar.today()
        let onlyOneLine = [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None) ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives context "JE create unhappy432" None today onlyOneLine [] []
            match result with
            | Error(JournalEntryInsufficientLines _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.13 REQ-JE-2.12 constructNewAndSaveToDb rejects unbalanced entry — debits != credits``() =
        let today = Calendar.today()
        let unbalancedLines =
            [ (fixture.Data.entertainment5650Id, 15.79M, "Debit", None)
              (fixture.Data.creditCard2220Id, 340.99M, "Credit", None) ]
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            let result =
                createTestJournalEntryFromPrimitives context "JE create unhappy892" None today unbalancedLines [] []
            match result with
            | Error(JournalEntryDebitCreditMismatch _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-1.22 constructNewAndSaveToDb rejects line with nonexistent account ID``() =
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
            | Error(JournalEntryLineAccountDoesntExist _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Theory>]
    [<InlineData("0.00")>]
    [<InlineData("-5.00")>]
    member _.``REQ-JE-1.24 constructNewAndSaveToDb rejects line whose amount is not positive``(amount: string) =
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
            | Error(JournalEntryLineNonPositiveAmount _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.5 REQ-JE-2.6 REQ-JE-1.11 constructNewAndSaveToDb rejects entry date w/ no matching fiscal period``() =
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
            | Error(JournalEntryDateNotInFiscalPeriod _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.7 constructNewAndSaveToDb rejects entry date in a closed fiscal period``() =
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
            | Error(JournalEntryHeaderEntryDateInvalid _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-2.8 constructNewAndSaveToDb rejects line referencing an inactive account as of entry date``() =
        let badAccount = fixture.Data.closedAccount
        let badId = badAccount |> Account.accountId
        let badDate =
            (badAccount |> Account.activityPeriod |> AccountActivityPeriod.activeEnd |> Option.get).PlusMonths(1)
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
            | Error(JournalEntryLineAccountInactive _) -> Ok()
            | Error e -> Error(TestingError $"Wrong error. {AppError.toMessage e}")
            | Ok _ -> Error(TestingError $"Expected failure; succeeded"))
        |> railroadWrapper
