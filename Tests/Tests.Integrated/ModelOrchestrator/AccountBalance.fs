namespace Tests.Integrated.ModelOrchestrator

open DataAccessLayer.DbTransaction
open Logger.Audit
open Model
open Model.Ledger.Journaling.JournalEntryComponent
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Utilities.AppError
open Xunit
open Tests.Helpers
open ModelOrchestrator.AccountBalance
open Utilities
open Context.Context

[<Collection("SharedTestData")>]
type AccountBalanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList returns correct debit and credit totals``() =
        let context = create NoTransaction FetchOnly
        let id1 = fixture.Data.mortgage2210Id
        let id2 = fixture.Data.food5350Id
        let accountsList = [ id1; id2 ]
        let expectedDebits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context false id1 Debit
        let expectedCredits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context false id1 Credit
        let expectedDebits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context false id2 Debit
        let expectedCredits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context false id2 Credit
        let expectedBal1 = expectedCredits1 - expectedDebits1 // liability
        let expectedBal2 = expectedDebits2 - expectedCredits2 // expense
        let result = fetchByAccountIdList context accountsList None
        match result with
        | Ok balances ->
            // first check that we got the right number of rows
            Assert.Equal(accountsList |> List.length, balances |> List.length)
            // now check the values
            let row1 = balances |> List.filter(fun ab -> ab.accountId = id1) |> List.head
            let row2 = balances |> List.filter(fun ab -> ab.accountId = id2) |> List.head
            Assert.Equal(expectedCredits1, row1.totalCredits |> Money.amount)
            Assert.Equal(expectedCredits2, row2.totalCredits |> Money.amount)
            Assert.Equal(expectedDebits1, row1.totalDebits |> Money.amount)
            Assert.Equal(expectedDebits2, row2.totalDebits |> Money.amount)
            Assert.Equal(expectedBal1, row1.netBalance |> Money.amount)
            Assert.Equal(expectedBal2, row2.netBalance |> Money.amount)
            Ok()
        | Error e -> Error e
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.6 REQ-JE-4.7 fetchByAccountIdList excludes voided entry amounts``() =
        let context = create NoTransaction FetchOnly
        let id1 = fixture.Data.creditCard2220Id
        let id2 = fixture.Data.entertainment5650Id
        let accountsList = [ id1; id2 ]
        let expectedDebits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context true id1 Debit
        let expectedCredits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context true id1 Credit
        let expectedDebits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context true id2 Debit
        let expectedCredits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType context true id2 Credit
        let expectedBal1 = expectedCredits1 - expectedDebits1 // liability
        let expectedBal2 = expectedDebits2 - expectedCredits2 // expense
        let result = fetchByAccountIdList context accountsList None
        match result with
        | Ok balances ->
            // first check that we got the right number of rows
            Assert.Equal(accountsList |> List.length, balances |> List.length)
            // now check the values
            let row1 = balances |> List.filter(fun ab -> ab.accountId = id1) |> List.head
            let row2 = balances |> List.filter(fun ab -> ab.accountId = id2) |> List.head
            Assert.Equal(expectedCredits1, row1.totalCredits |> Money.amount)
            Assert.Equal(expectedCredits2, row2.totalCredits |> Money.amount)
            Assert.Equal(expectedDebits1, row1.totalDebits |> Money.amount)
            Assert.Equal(expectedDebits2, row2.totalDebits |> Money.amount)
            Assert.Equal(expectedBal1, row1.netBalance |> Money.amount)
            Assert.Equal(expectedBal2, row2.netBalance |> Money.amount)
            Ok()
        | Error e -> Error e
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList returns zero balances for account with no activity``() =
        let context = create NoTransaction FetchOnly
        let result = fetchByAccountIdList context [ fixture.Data.assets1000Id ] None
        match result with
        | Ok balances ->
            Assert.Equal(1, balances |> List.length)
            let bal = balances |> List.head
            Assert.Equal(0M, bal.totalDebits |> Money.amount)
            Assert.Equal(0M, bal.totalCredits |> Money.amount)
            Assert.Equal(0M, bal.netBalance |> Money.amount)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList with empty list returns Error``() =
        let context = create NoTransaction FetchOnly
        let result = fetchByAccountIdList context [] None
        Assert.True(Result.isError result) // todo: change this to a precise assertion on error type

    [<Fact>]
    member _.``REQ-JE-3.6.2 fetchByAccountIdList with asOf excludes entries after cutoff``() =
        let context = create NoTransaction FetchOnly
        let today = Calendar.today()
        let asOfDate = today.PlusDays(-2)
        let expenseId = fixture.Data.temporalExpense5700Id
        let result = fetchByAccountIdList context [ expenseId ] (Some asOfDate)
        match result with
        | Ok balances ->
            Assert.Equal(1, balances |> List.length)
            let bal = balances |> List.head
            Assert.Equal(137.42M, bal.totalDebits |> Money.amount)
            Assert.Equal(0M, bal.totalCredits |> Money.amount)
            Assert.Equal(137.42M, bal.netBalance |> Money.amount)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.6.1 net balance is positive in normal-balance orientation``() =
        let context = create NoTransaction FetchOnly
        let expenseId = fixture.Data.temporalExpense5700Id
        let revenueId = fixture.Data.temporalRevenue4500Id
        let result = fetchByAccountIdList context [ expenseId; revenueId ] None
        match result with
        | Ok balances ->
            Assert.Equal(2, balances |> List.length)
            let expenseBal = balances |> List.find(fun b -> b.accountId = expenseId)
            let revenueBal = balances |> List.find(fun b -> b.accountId = revenueId)
            Assert.Equal(287.09M, expenseBal.totalDebits |> Money.amount)
            Assert.Equal(0M, expenseBal.totalCredits |> Money.amount)
            Assert.True(expenseBal.netBalance |> Money.amount > 0M)
            Assert.Equal(0M, revenueBal.totalDebits |> Money.amount)
            Assert.Equal(287.09M, revenueBal.totalCredits |> Money.amount)
            Assert.True(revenueBal.netBalance |> Money.amount > 0M)
        | Error e -> Assert.Fail(AppError.toMessage e)
