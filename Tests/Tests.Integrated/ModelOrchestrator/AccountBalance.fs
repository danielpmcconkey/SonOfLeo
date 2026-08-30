namespace Tests.Integrated.ModelOrchestrator

open DataAccessLayer.DbTransaction
open InterfaceBridge.CommandRoute
open Logger.Audit
open Model
open Model.Ledger
open Model.Ledger.JournalEntryComponent
open ModelOrchestrator.JournalEntries.JournalEntry
open Tests.Helpers.EntityFunctions
open Tests.Helpers.Railroad
open Utilities.AppError
open Xunit
open Tests.Helpers
open Tests.Helpers.SadPath
open ModelOrchestrator.AccountBalance
open Utilities
open Utilities.ResultHelper


[<Collection("SharedTestData")>]
type AccountBalanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.6 REQ-RPT-1.10 fetchByAccountIdList returns correct debit and credit totals``() =
        let context = Context.create NoTransaction FetchOnly
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
        let result = fetchByAccountIdList context (Some accountsList) None
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
    member _.``REQ-JE-3.6 REQ-JE-4.7 REQ-RPT-1.8 fetchByAccountIdList excludes voided entry amounts``() =
        let context = Context.create NoTransaction FetchOnly
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
        let result = fetchByAccountIdList context (Some accountsList) None
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
        let context = Context.create NoTransaction FetchOnly
        let result = fetchByAccountIdList context (Some [fixture.Data.assets1000Id]) None
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
        let context = Context.create NoTransaction FetchOnly
        isCorrectErrorEmpty (fetchByAccountIdList context (Some []) None) AccountBalanceFetchInvalidArguments None
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.6.2 fetchByAccountIdList with asOf excludes entries after cutoff``() =
        let context = Context.create NoTransaction FetchOnly
        let today = Calendar.today()
        let asOfDate = today.PlusDays(-2)
        let expenseId = fixture.Data.temporalExpense5700Id
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
        let result = fetchByAccountIdList context (Some [expenseId]) (Some asOfDate)
        match result with
        | Ok balances ->
            Assert.Equal(1, balances |> List.length)
            let bal = balances |> List.head
            Assert.Equal(expectedDebits, bal.totalDebits |> Money.amount)
            Assert.Equal(expectedCredits, bal.totalCredits |> Money.amount)
            Assert.True(linesBeforeCutoff |> List.length > 0)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.6.2 fetchByAccountIdList with asOf before all entries returns zero balances``() =
        let context = Context.create NoTransaction FetchOnly
        let today = Calendar.today()
        let asOfDate = today.PlusDays(-4)
        let expenseId = fixture.Data.temporalExpense5700Id
        let result = fetchByAccountIdList context (Some [expenseId]) (Some asOfDate)
        match result with
        | Ok balances ->
            Assert.Equal(1, balances |> List.length)
            let bal = balances |> List.head
            Assert.Equal(0M, bal.totalDebits |> Money.amount)
            Assert.Equal(0M, bal.totalCredits |> Money.amount)
            Assert.Equal(0M, bal.netBalance |> Money.amount)
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-JE-3.6.1 net balance is positive in normal-balance orientation``() =
        let amount = 200.00M
        let zero = 0M
        runCommandRouteAndAutoRollback JournalEntryPostNew (fun context ->
            result {
                let! _, expenseId =
                    createTestAccountFromPrimitives
                        context "NB-EXP" "Normal Balance Expense" "Expense"
                        (Calendar.today().PlusYears(-1)) None (Some "OperatingExpense")
                        (Some fixture.Data.expenses5000Id) None
                let! _, revenueId =
                    createTestAccountFromPrimitives
                        context "NB-REV" "Normal Balance Revenue" "Revenue"
                        (Calendar.today().PlusYears(-1)) None (Some "OperatingRevenue")
                        (Some fixture.Data.revenue4000Id) None
                let! _ =
                    createTestJournalEntryFromPrimitives
                        context "Normal balance orientation test" None (Calendar.today())
                        [ (expenseId, amount, "Debit", None)
                          (revenueId, amount, "Credit", None) ]
                        [] []
                let! balances = fetchByAccountIdList context (Some [expenseId; revenueId]) None
                Assert.Equal(2, balances |> List.length)
                let expenseBal = balances |> List.find(fun b -> b.accountId = expenseId)
                let revenueBal = balances |> List.find(fun b -> b.accountId = revenueId)
                Assert.Equal(amount, expenseBal.totalDebits |> Money.amount)
                Assert.Equal(zero, expenseBal.totalCredits |> Money.amount)
                Assert.True(expenseBal.netBalance |> Money.amount > zero)
                Assert.Equal(zero, revenueBal.totalDebits |> Money.amount)
                Assert.Equal(amount, revenueBal.totalCredits |> Money.amount)
                Assert.True(revenueBal.netBalance |> Money.amount > zero)
                return ()
            })
        |> railroadWrapper
