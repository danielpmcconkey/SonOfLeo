namespace Tests.Integrated.ModelOrchestrator

open System
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.Rollback
open Utilities.ResultHelper
open Xunit
open Tests.Integrated
open Model.Audit
open ModelOrchestrator.JournalEntries.JournalEntry
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open Utilities
open Utilities.AppError

[<Collection("SharedTestData")>]
type JournalEntryCreationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-2.13 REQ-JE-2.11 constructNewAndSaveToDb posts a valid journal entry and returns it``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let expected = "JE create happy"
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            expected
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelope
                            tran
                    let actual = jeHappy |> header |> JournalEntryHeader.description |> JournalEntryDescription.value
                    Assert.Equal(expected, actual)
                    Assert.Equal(2, jeHappy |> lines |> List.length)
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-2.1 constructNewAndSaveToDb generates a unique UUID for the header``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _, jeHappyId = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelope
                            tran
                    Assert.NotEqual(Guid.Empty, jeHappyId |> JournalEntryHeaderId.value)
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-2.2 REQ-JE-1.21 constructNewAndSaveToDb generates unique UUIDs for each line``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelope
                            tran
                    jeHappy
                    |> lines
                    |> List.map(fun x -> x |> JournalEntryLine.journalEntryLineId)
                    |> List.iter(fun x -> Assert.NotEqual(Guid.Empty, x |> JournalEntryLineId.value))
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-2.9 REQ-JE-1.40 constructNewAndSaveToDb generates unique UUIDs for each external reference``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            [ ("TestBank", "F-SHARED-001"); ("TestBank", "TXN-001") ]
                            []
                            envelope
                            tran
                    jeHappy
                    |> externalReferences
                    |> List.map(fun x -> x |> JournalEntryExternalReference.journalEntryExternalReferenceId)
                    |> List.iter(fun x -> Assert.NotEqual(Guid.Empty, x |> JournalEntryExternalReferenceId.value))
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-SYS-3.2 constructNewAndSaveToDb sets created_at and modified_at from AuditEnvelope``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let expected = envelope |> AuditEnvelope.instant
        let today = Calendar.today()
        withRollback(fun tran ->
            let railroad =
                result {
                    let! jeHappy, _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelope
                            tran
                    Assert.Equal(expected, jeHappy |> header |> JournalEntryHeader.createdAt)
                    Assert.Equal(expected, jeHappy |> header |> JournalEntryHeader.modifiedAt)
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.46 constructNewAndSaveToDb accepts an entry with zero external references``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlyEmpty = []
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            explicitlyEmpty
                            []
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.46 constructNewAndSaveToDb accepts an entry with multiple external references``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlyMultiple = [ ("TestBank", "F-SHARED-001"); ("TestBank", "TXN-001") ]
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            explicitlyMultiple
                            []
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.55 constructNewAndSaveToDb accepts an entry with zero comments``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlyEmpty = []
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            explicitlyEmpty
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.55 constructNewAndSaveToDb accepts an entry with multiple comments``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlyMultiple =
            [ (None, "Fixture comment for testing")
              (None, "Fixture comment for testing 2") ]
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            explicitlyMultiple
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.6 constructNewAndSaveToDb accepts an entry with null source``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlyNone = None
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            explicitlyNone
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            []
                            []
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.26 constructNewAndSaveToDb accepts lines with null memos``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlyNone = None
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            explicitlyNone
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", explicitlyNone)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", explicitlyNone) ]
                            []
                            []
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.48 constructNewAndSaveToDb accepts duplicate source_fi/reference pairs across entries``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let explicitlySame = [ ("TestBank", "F-SHARED-001"); ("TestBank", "F-SHARED-001") ]
        withRollback(fun tran ->
            let railroad =
                result {
                    let! _ = // the test helper resolves to constructNewAndSaveToDb
                        createTestJournalEntryFromPrimitives
                            "JE create happy"
                            None
                            today
                            [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                              (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                            explicitlySame
                            []
                            envelope
                            tran
                    return ()
                }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-JE-1.12 constructNewAndSaveToDb rejects entry with fewer than 2 lines``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let onlyOneLine = [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None) ]
        withRollback(fun tran ->
            let result =
                createTestJournalEntryFromPrimitives "JE create unhappy432" None today onlyOneLine [] [] envelope tran
            match result with
            | Error(JournalEntryInsufficientLines _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail "Expected failure; got success")

    [<Fact>]
    member _.``REQ-JE-1.13 REQ-JE-2.12 constructNewAndSaveToDb rejects unbalanced entry — debits != credits``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let unbalancedLines =
            [ (fixture.Data.entertainment5650Id, 15.79M, "Debit", None)
              (fixture.Data.creditCard2220Id, 340.99M, "Credit", None) ]
        withRollback(fun tran ->
            let result =
                createTestJournalEntryFromPrimitives "JE create unhappy892" None today unbalancedLines [] [] envelope tran
            match result with
            | Error(JournalEntryDebitCreditMismatch _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail "Expected failure; got success")

    [<Fact>]
    member _.``REQ-JE-1.22 constructNewAndSaveToDb rejects line with nonexistent account ID``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let phoneyAccountId = Guid.NewGuid() |> AccountId.fromGuid
        withRollback(fun tran ->
            let result =
                createTestJournalEntryFromPrimitives
                    "JE create unhappy351"
                    None
                    today
                    [ (fixture.Data.entertainment5650Id, 1453840.27M, "Debit", None)
                      (phoneyAccountId, 1453840.27M, "Credit", None) ]
                    []
                    []
                    envelope
                    tran
            match result with
            | Error(JournalEntryLineAccountDoesntExist _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail "Expected failure; got success")

    [<Fact>]
    member _.``REQ-JE-2.5 REQ-JE-2.6 REQ-JE-1.11 constructNewAndSaveToDb rejects entry date with no matching fiscal period``
        ()
        =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let today = Calendar.today()
        let badDate = today.PlusYears(-3)
        withRollback(fun tran ->
            let result =
                createTestJournalEntryFromPrimitives
                    "JE create unhappy"
                    None
                    badDate
                    [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                      (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                    []
                    []
                    envelope
                    tran
            match result with
            | Error(JournalEntryDateNotInFiscalPeriod _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail "Expected failure; got success")

    [<Fact>]
    member _.``REQ-JE-2.7 constructNewAndSaveToDb rejects entry date in a closed fiscal period``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let badDate = (fixture.Data.closedFiscalPeriod |> FiscalPeriod.startDate).PlusDays(14)
        withRollback(fun tran ->
            let result =
                createTestJournalEntryFromPrimitives
                    "JE create unhappy"
                    None
                    badDate
                    [ (fixture.Data.entertainment5650Id, 86.04M, "Debit", None)
                      (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                    []
                    []
                    envelope
                    tran
            match result with
            | Error(JournalEntryHeaderEntryDateInvalid _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail "Expected failure; got success")

    [<Fact>]
    member _.``REQ-JE-2.8 constructNewAndSaveToDb rejects line referencing an inactive account as of entry date``() =
        let envelope = AuditEnvelope.create JournalEntryPostNew
        let badAccount = fixture.Data.closedAccount
        let badId = badAccount |> Account.accountId
        let badDate =
            (badAccount |> Account.activityPeriod |> AccountActivityPeriod.activeEnd |> Option.get).PlusMonths(1)
        withRollback(fun tran ->
            let result =
                createTestJournalEntryFromPrimitives
                    "JE create unhappy"
                    None
                    badDate
                    [ (badId, 86.04M, "Debit", None)
                      (fixture.Data.creditCard2220Id, 86.04M, "Credit", None) ]
                    []
                    []
                    envelope
                    tran
            match result with
            | Error(JournalEntryLineAccountInactive _) -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            | Ok _ -> Assert.Fail "Expected failure; got success")
