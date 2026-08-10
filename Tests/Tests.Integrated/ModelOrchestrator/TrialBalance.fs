namespace Tests.Integrated.ModelOrchestrator

open Context.Context
open DataAccessLayer.DbTransaction
open Logger.Audit
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.JournalEntries.JournalEntry
open ModelOrchestrator.TrialBalanceReport
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type TrialBalanceTests(fixture: TestDataFixture) =

    let nextMonth = Calendar.today().PlusMonths(1)
    let context = create NoTransaction FetchOnly
    let prefetchedTb = fetchTrialBalanceData context nextMonth

    let unvoidedLines =
        fixture.Data.journalEntries
        |> List.filter(fun je ->
            je |> header |> JournalEntryHeader.voidedAt |> Option.isNone)
        |> List.collect lines

    let sumLinesForAccount accountId lineType =
        unvoidedLines
        |> List.filter(fun l ->
            l |> JournalEntryLine.accountId = accountId
            && l |> JournalEntryLine.lineType = lineType)
        |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)

    [<Fact>]
    member _.``REQ-RPT-1.2 trial balance includes inactive accounts and accounts with no journal entry activity``() =
        let expectedCount = fixture.Data.accounts |> List.length
        let expectedClosedAccountCode = fixture.Data.closedAccount |> Account.code
        let expectedNoActivityCode =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = fixture.Data.retirement3030Id)
            |> Account.code
        result {
            let! rows = prefetchedTb
            Assert.Equal(expectedCount, rows |> List.length)
            let closedRow = rows |> List.tryFind(fun r -> r.accountCode = expectedClosedAccountCode)
            Assert.True(closedRow |> Option.isSome, "Inactive account missing from trial balance")
            let noActivityRow = rows |> List.find(fun r -> r.accountCode = expectedNoActivityCode)
            Assert.Equal(0M, noActivityRow.totalDebits |> Money.amount)
            Assert.Equal(0M, noActivityRow.totalCredits |> Money.amount)
            Assert.Equal(0M, noActivityRow.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.4 leaf account row reflects only its own balance with no roll-up``() =
        let leafId = fixture.Data.food5350Id
        let leafAccount = fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = leafId)
        let leafCode = leafAccount |> Account.code
        let expectedDebits = sumLinesForAccount leafId Debit
        let expectedCredits = sumLinesForAccount leafId Credit
        let expectedNet = expectedDebits - expectedCredits
        result {
            let! rows = prefetchedTb
            let leafRow = rows |> List.find(fun r -> r.accountCode = leafCode)
            Assert.Equal(expectedDebits, leafRow.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, leafRow.totalCredits |> Money.amount)
            Assert.Equal(expectedNet, leafRow.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.5 parent account row includes its own values plus recursive child roll-up``() =
        let parentId = fixture.Data.expenses5000Id
        let parentAccount = fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = parentId)
        let parentCode = parentAccount |> Account.code
        let rec isDescendantOf targetParentId accountId =
            match fixture.Data.accounts |> List.tryFind(fun a -> a |> Account.accountId = accountId) with
            | None -> false
            | Some acct ->
                match acct |> Account.parentId with
                | None -> false
                | Some pid -> pid = targetParentId || isDescendantOf targetParentId pid
        let descendantIds =
            fixture.Data.accounts
            |> List.filter(fun a -> isDescendantOf parentId (a |> Account.accountId))
            |> List.map Account.accountId
        let allIds = parentId :: descendantIds
        let expectedDebits = allIds |> List.sumBy(fun id -> sumLinesForAccount id Debit)
        let expectedCredits = allIds |> List.sumBy(fun id -> sumLinesForAccount id Credit)
        let expectedNet = expectedDebits - expectedCredits
        result {
            let! rows = prefetchedTb
            let parentRow = rows |> List.find(fun r -> r.accountCode = parentCode)
            Assert.Equal(expectedDebits, parentRow.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, parentRow.totalCredits |> Money.amount)
            Assert.Equal(expectedNet, parentRow.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.6 result list is sorted by account code``() =
        // todo: this rules is wrong and this test needs to be revisited. The parent child hierarchy is primary. Account code is secondary
        result {
            let! rows = prefetchedTb
            let codes = rows |> List.map(fun r -> r.accountCode |> AccountCode.value)
            let sorted = codes |> List.sort
            Assert.Equal<string list>(sorted, codes)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.7 top-level accounts have generation 0 and children increment by 1 per level``() =
        result {
            let! rows = prefetchedTb
            let findGen code = (rows |> List.find(fun r -> r.accountCode |> AccountCode.value = code)).generation
            Assert.Equal(0, findGen "F-5000")
            Assert.Equal(1, findGen "F-5300")
            Assert.Equal(2, findGen "F-5310")
            Assert.Equal(3, findGen "F-5311")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.9 entries dated after the as-of date are excluded``() =
        let today = Calendar.today()
        let asOfDate = today.PlusDays(-2)
        let expenseId = fixture.Data.temporalExpense5700Id
        let expenseCode =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = expenseId)
            |> Account.code
        let linesBeforeCutoff =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                let h = je |> header
                h |> JournalEntryHeader.voidedAt |> Option.isNone
                && h |> JournalEntryHeader.entryDate |> EntryDate.entryDate <= asOfDate)
            |> List.collect lines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = expenseId)
        let expectedDebits =
            linesBeforeCutoff
            |> List.filter(fun l -> l |> JournalEntryLine.lineType = Debit)
            |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
        let expectedCredits =
            linesBeforeCutoff
            |> List.filter(fun l -> l |> JournalEntryLine.lineType = Credit)
            |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
        Assert.True(linesBeforeCutoff |> List.length > 0, "No lines before cutoff — test is vacuous")
        let cutoffContext = create NoTransaction FetchOnly
        result {
            let! rows = fetchTrialBalanceData cutoffContext asOfDate
            let row = rows |> List.find(fun r -> r.accountCode = expenseCode)
            Assert.Equal(expectedDebits, row.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, row.totalCredits |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.11 account with no qualifying activity appears with zero credits debits and net``() =
        let noActivityCode =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = fixture.Data.retirement3030Id)
            |> Account.code
        result {
            let! rows = prefetchedTb
            let row = rows |> List.find(fun r -> r.accountCode = noActivityCode)
            Assert.Equal(0M, row.totalDebits |> Money.amount)
            Assert.Equal(0M, row.totalCredits |> Money.amount)
            Assert.Equal(0M, row.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper
