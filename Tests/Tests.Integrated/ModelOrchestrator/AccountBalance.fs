namespace Tests.Integrated.ModelOrchestrator

open Model
open Model.Ledger.Journaling.JournalEntryComponent
open Tests.Integrated.GenericTestProperties
open Utilities.AppError
open Xunit
open Tests.Integrated
open ModelOrchestrator.AccountBalance

[<Collection("SharedTestData")>]
type AccountBalanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList returns correct debit and credit totals``() =
        let id1 = fixture.Data.mortgage2210Id
        let id2 = fixture.Data.food5350Id
        let accountsList = [ id1; id2 ]
        let expectedDebits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType false id1 Debit
        let expectedCredits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType false id1 Credit
        let expectedDebits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType false id2 Debit
        let expectedCredits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType false id2 Credit
        let expectedBal1 = expectedCredits1 - expectedDebits1 // liability
        let expectedBal2 = expectedDebits2 - expectedCredits2 // expense
        let result = fetchByAccountIdList None accountsList None
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
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.6 REQ-JE-4.7 fetchByAccountIdList excludes voided entry amounts``() =
        let id1 = fixture.Data.creditCard2220Id
        let id2 = fixture.Data.entertainment5650Id
        let accountsList = [ id1; id2 ]
        let expectedDebits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType true id1 Debit
        let expectedCredits1 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType true id1 Credit
        let expectedDebits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType true id2 Debit
        let expectedCredits2 =
            fixture.Data.journalEntryLines |> sumJournalEntryLinesByAccountIdAndType true id2 Credit
        let expectedBal1 = expectedCredits1 - expectedDebits1 // liability
        let expectedBal2 = expectedDebits2 - expectedCredits2 // expense
        let result = fetchByAccountIdList None accountsList None
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
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList returns zero balances for account with no activity``() =
        let result = fetchByAccountIdList None [ fixture.Data.assets1000Id ] None
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
        let result = fetchByAccountIdList None [] None
        Assert.True(Result.isError result)

// todo: create a test on fetchByAccountIdList that checks that as-of works
