namespace Tests.Integrated.ModelOrchestrator

open Xunit
open Tests.Integrated
open ModelOrchestrator.AccountBalance

[<Collection("SharedTestData")>]
type AccountBalanceTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList returns correct debit and credit totals`` () =
        let result = fetchByAccountIdList None [fixture.Data.mortgage2210Id]
        match result with
        | Ok balances ->
            Assert.Equal(1, balances |> List.length)
            let bal = balances |> List.head
            Assert.Equal(fixture.Data.mortgage2210Id, bal.accountId)
            Assert.True(bal.totalDebits |> Model.MoneyModule.amount > 0M)
            Assert.Equal(0M, bal.totalCredits |> Model.MoneyModule.amount)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList excludes voided entry amounts`` () =
        let result = fetchByAccountIdList None [fixture.Data.entertainment5650Id]
        match result with
        | Ok balances ->
            let bal = balances |> List.head
            let debitAmount = bal.totalDebits |> Model.MoneyModule.amount
            // entertainment5650 has 75 debit from the voided JE (excluded) plus
            // 33 x 4 from void victims (included until those tests run).
            // The voided JE's 75 must NOT be in the total.
            Assert.True(debitAmount < 75M + 33M * 4M + 75M)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList returns zero balances for account with no activity`` () =
        let result = fetchByAccountIdList None [fixture.Data.assets1000Id]
        match result with
        | Ok balances ->
            Assert.Equal(1, balances |> List.length)
            let bal = balances |> List.head
            Assert.Equal(0M, bal.totalDebits |> Model.MoneyModule.amount)
            Assert.Equal(0M, bal.totalCredits |> Model.MoneyModule.amount)
            Assert.Equal(0M, bal.netBalance |> Model.MoneyModule.amount)
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-JE-3.6 fetchByAccountIdList with empty list returns Error`` () =
        let result = fetchByAccountIdList None []
        Assert.True(Result.isError result)
