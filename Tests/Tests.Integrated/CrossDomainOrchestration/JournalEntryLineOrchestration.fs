namespace Tests.Integrated.CrossDomainOrchestration

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.JournalEntryComponent
open Business.CrossDomainOrchestration.JournalEntryOrchestration
open Tests.Helpers
open Tests.Helpers.Railroad
open App.Utility.Result
open Xunit
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath

[<Collection("SharedTestData")>]
type JournalEntryLineOrchestrationTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.4 fetchByAccountId returns every line posted to the account and no others``() =
        let accountId = fixture.Data.food5350Id
        let expectedLines =
            fixture.Data.journalEntries
            |> List.collect JournalEntryOrchestration.jeLines
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
