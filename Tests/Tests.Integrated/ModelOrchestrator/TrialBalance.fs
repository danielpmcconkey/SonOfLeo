namespace Tests.Integrated.ModelOrchestrator

open DataAccessLayer.DbTransaction
open Logger.Audit
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Model.Ledger.Journaling.JournalEntryComponent
open ModelOrchestrator.TrialBalanceReport
open Tests.Helpers
open Tests.Helpers.Railroad
open Utilities
open Utilities.AppError
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type TrialBalanceTests(fixture: TestDataFixture) =

    let fetchTb () =
        let context = Context.Context.create NoTransaction FetchOnly
        let nextMonth = Calendar.today().PlusMonths(1)
        fetchTrialBalanceData context nextMonth

    [<Fact>]
    member _.``REQ-RPT-1.2 trial balance includes inactive accounts and accounts with no journal entry activity``() =
        let context = Context.Context.create NoTransaction FetchOnly
        let nextMonth = Calendar.today().PlusMonths(1)
        result {
            let! allAccounts = Account.fetchAll context false
            let! rows = fetchTrialBalanceData context nextMonth
            Assert.Equal(allAccounts |> List.length, rows |> List.length)
            let closedAccountCode = fixture.Data.closedAccount |> Account.code
            let closedRow = rows |> List.find(fun r -> r.accountCode = closedAccountCode)
            Assert.NotNull(closedRow)
            let noActivityCode =
                allAccounts
                |> List.find(fun a -> a |> Account.accountId = fixture.Data.assets1000Id)
                |> Account.code
            let noActivityRow = rows |> List.find(fun r -> r.accountCode = noActivityCode)
            Assert.NotNull(noActivityRow)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.4 leaf account row reflects only its own balance with no roll-up``() =
        let context = Context.Context.create NoTransaction FetchOnly
        let nextMonth = Calendar.today().PlusMonths(1)
        let leafId = fixture.Data.food5350Id
        result {
            let! balances = ModelOrchestrator.AccountBalance.fetchByAccountIdList context (Some [leafId]) None
            let expectedDebits = (balances |> List.head).totalDebits |> Money.amount
            let expectedCredits = (balances |> List.head).totalCredits |> Money.amount
            let! rows = fetchTrialBalanceData context nextMonth
            let leafCode =
                fixture.Data.accounts
                |> List.find(fun a -> a |> Account.accountId = leafId)
                |> Account.code
            let leafRow = rows |> List.find(fun r -> r.accountCode = leafCode)
            Assert.Equal(expectedDebits, leafRow.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, leafRow.totalCredits |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.5 parent account row includes its own values plus recursive child roll-up``() =
        let context = Context.Context.create NoTransaction FetchOnly
        let nextMonth = Calendar.today().PlusMonths(1)
        let parentId = fixture.Data.expenses5000Id
        result {
            let! allAccounts = Account.fetchAll context false
            let childIds =
                allAccounts
                |> List.filter(fun a -> a |> Account.parentId = Some parentId)
                |> List.map Account.accountId
            let allIds = parentId :: childIds
            let! balances = ModelOrchestrator.AccountBalance.fetchByAccountIdList context (Some allIds) None
            let expectedDebits =
                balances |> List.sumBy(fun b -> b.totalDebits |> Money.amount)
            let expectedCredits =
                balances |> List.sumBy(fun b -> b.totalCredits |> Money.amount)
            let! rows = fetchTrialBalanceData context nextMonth
            let parentCode =
                allAccounts
                |> List.find(fun a -> a |> Account.accountId = parentId)
                |> Account.code
            let parentRow = rows |> List.find(fun r -> r.accountCode = parentCode)
            Assert.Equal(expectedDebits, parentRow.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, parentRow.totalCredits |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.6 result list is sorted by account code``() =
        result {
            let! rows = fetchTb()
            let codes = rows |> List.map(fun r -> r.accountCode |> AccountCode.value)
            let sorted = codes |> List.sort
            Assert.Equal<string list>(sorted, codes)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.7 top-level accounts have generation 0 and children increment by 1 per level``() =
        let context = Context.Context.create NoTransaction FetchOnly
        let nextMonth = Calendar.today().PlusMonths(1)
        result {
            let! allAccounts = Account.fetchAll context false
            let! rows = fetchTrialBalanceData context nextMonth
            let topLevelCodes =
                allAccounts
                |> List.filter(fun a -> a |> Account.parentId |> Option.isNone)
                |> List.map Account.code
            topLevelCodes |> List.iter(fun code ->
                let row = rows |> List.find(fun r -> r.accountCode = code)
                Assert.Equal(0, row.generation))
            let childAccounts =
                allAccounts
                |> List.filter(fun a -> a |> Account.parentId |> Option.isSome)
            childAccounts |> List.iter(fun child ->
                let childCode = child |> Account.code
                let childRow = rows |> List.find(fun r -> r.accountCode = childCode)
                Assert.Equal(1, childRow.generation))
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.9 entries dated after the as-of date are excluded``() =
        let context = Context.Context.create NoTransaction FetchOnly
        let today = Calendar.today()
        let asOfDate = today.PlusDays(-2)
        let expenseId = fixture.Data.temporalExpense5700Id
        result {
            let! balancesCutoff =
                ModelOrchestrator.AccountBalance.fetchByAccountIdList context (Some [expenseId]) (Some asOfDate)
            let expectedDebits = (balancesCutoff |> List.head).totalDebits |> Money.amount
            let expectedCredits = (balancesCutoff |> List.head).totalCredits |> Money.amount
            let! rows = fetchTrialBalanceData context asOfDate
            let expenseCode =
                fixture.Data.accounts
                |> List.find(fun a -> a |> Account.accountId = expenseId)
                |> Account.code
            let row = rows |> List.find(fun r -> r.accountCode = expenseCode)
            Assert.Equal(expectedDebits, row.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, row.totalCredits |> Money.amount)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-RPT-1.11 account with no qualifying activity appears with zero credits debits and net``() =
        result {
            let! rows = fetchTb()
            let noActivityCode =
                fixture.Data.accounts
                |> List.find(fun a -> a |> Account.accountId = fixture.Data.retirement3030Id)
                |> Account.code
            let row = rows |> List.find(fun r -> r.accountCode = noActivityCode)
            Assert.Equal(0M, row.totalDebits |> Money.amount)
            Assert.Equal(0M, row.totalCredits |> Money.amount)
            Assert.Equal(0M, row.netBalance |> Money.amount)
            return ()
        }
        |> railroadWrapper
