namespace Tests.Integrated.ModelOrchestrator

open System
open DataAccessLayer.DbTransaction
open Logger.Audit
open Model
open Model.Ledger
open Model.Ledger.JournalEntryComponent
open ModelOrchestrator.JournalEntries
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type JournalEntryLineOrchestrationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.4 fetchByAccountId returns every line posted to the account and no others``() =
        let accountId = fixture.Data.food5350Id
        let expectedLines =
            fixture.Data.journalEntries
            |> List.collect JournalEntry.lines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = accountId)
        let expectedIds =
            expectedLines
            |> List.map(JournalEntryLine.journalEntryLineId >> JournalEntryLineId.value)
            |> List.sort
        let expectedAmounts =
            expectedLines |> List.map(JournalEntryLine.amount >> Money.amount) |> List.sort
        let context = Context.create NoTransaction FetchOnly
        result {
            let! fetched = accountId |> JournalEntryLine.fetchByAccountId context false
            let actualIds =
                fetched
                |> List.map(JournalEntryLine.journalEntryLineId >> JournalEntryLineId.value)
                |> List.sort
            let actualAmounts =
                fetched |> List.map(JournalEntryLine.amount >> Money.amount) |> List.sort
            // an account with no fixture lines would make the two set comparisons vacuously true
            Assert.NotEmpty(expectedIds)
            Assert.Equal<Guid list>(expectedIds, actualIds)
            Assert.Equal<decimal list>(expectedAmounts, actualAmounts)
            return ()
        }
        |> railroadWrapper
