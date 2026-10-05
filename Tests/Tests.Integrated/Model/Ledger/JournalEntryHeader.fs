namespace Tests.Integrated.Model.Ledger

open App.Session
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open App.DataAccessLayer.DbTransaction
open App.Operation.AuditEnvelope
open App.Utility.IAppError
open Tests.Helpers.TestError
open Tests.Helpers.SadPath
open Xunit
open Tests.Helpers
open Business.FinancialServices.Ledger.JournalEntryComponent
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction


[<Collection("SharedTestData")>]
type JournalEntryHeaderTests(fixture: TestDataFixture) =
    [<Fact>]
    member _.``REQ-JE-3.2 fetchById returns a header whose entry date is in a closed fiscal period must succeed``
        ()
        =
        // The read path must not re-run the creation-time rule that a posting's
        // period be open. Closing a period is the normal end of its lifecycle;
        // historical entries stay readable.
        let context = Context.create NoTransaction FetchOnly
        let expectedDescription =
            fixture.Data.journalEntries
            |> List.map JournalEntryOrchestration.JournalEntryOrchestration.header
            |> List.find (fun h -> h |> JournalEntryHeader.journalEntryHeaderId = fixture.Data.jeInClosedPeriodId)
            |> JournalEntryHeader.description
            |> JournalEntryDescription.value
        let result = fixture.Data.jeInClosedPeriodId |> JournalEntryHeader.fetchById context
        match result with
        | Ok h ->
            Assert.Equal(fixture.Data.jeInClosedPeriodId, h |> JournalEntryHeader.journalEntryHeaderId)
            Assert.Equal(
                expectedDescription,
                h |> JournalEntryHeader.description |> JournalEntryDescription.value
            )
        | Error e -> Assert.Fail $"Fetching a JE header in a closed period failed: {e.ToMessage()}"
