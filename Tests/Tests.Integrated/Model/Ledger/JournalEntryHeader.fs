namespace Tests.Integrated.Model.Ledger

open DataAccessLayer.DbTransaction
open Logger.Audit
open Utilities.AppError
open Xunit
open Tests.Helpers
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent


[<Collection("SharedTestData")>]
type JournalEntryHeaderTests(fixture: TestDataFixture) =
    [<Fact>]
    member _.``REQ-JE-3.2 REQ-JE-2.7 fetchById returns a header whose entry date is in a closed fiscal period must succeed``
        ()
        =
        // The read path must not re-run the creation-time period-is-open rule
        // (REQ-JE-2.7). Closing a period is the normal end of its lifecycle;
        // historical entries stay readable.
        let context = Context.create NoTransaction FetchOnly
        let result = fixture.Data.jeInClosedPeriodId |> JournalEntryHeader.fetchById context
        match result with
        | Ok h ->
            Assert.Equal(fixture.Data.jeInClosedPeriodId, h |> JournalEntryHeader.journalEntryHeaderId)
            Assert.Equal(
                "Fixture JE in closed period",
                h |> JournalEntryHeader.description |> JournalEntryDescription.value
            )
        | Error e -> Assert.Fail $"Fetching a JE header in a closed period failed: {AppError.toMessage e}"
