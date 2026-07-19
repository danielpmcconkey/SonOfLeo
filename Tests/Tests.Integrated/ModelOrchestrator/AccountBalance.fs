namespace Tests.Integrated.ModelOrchestrator

open Model
open Model.Ledger.Accounts.AccountComponent
open Xunit
open Tests.Integrated
open ModelOrchestrator.AccountBalance

// [<Collection("SharedTestData")>]
// type AccountBalanceTests(fixture: TestDataFixture) =
//
//     [<Fact>]
//     member _.``REQ-JE-3.6 fetchByAccountIdList returns correct debit and credit totals`` () =
//         let expected = fixture.Data.mortgage2210Id
//         let result = fetchByAccountIdList None [fixture.Data.mortgage2210Id] None
//         match result with
//         | Ok balances ->
//             Assert.Equal(1, balances |> List.length)
//             let bal = balances |> List.head
//             let actual = bal.accountId
//             Assert.Equal(expected, actual)
//             Assert.True(bal.totalDebits |> Money.amount > 0M)
//             Assert.Equal(0M, bal.totalCredits |> Money.amount)
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-3.6 fetchByAccountIdList excludes voided entry amounts`` () =
//     // todo: ``REQ-JE-3.6 fetchByAccountIdList excludes voided entry amounts`` fails when you run every test at once
//         let result = fetchByAccountIdList None [fixture.Data.entertainment5650Id] None
//         match result with
//         | Ok balances ->
//             let bal = balances |> List.head
//             let debitAmount = bal.totalDebits |> Money.amount
//             // entertainment5650 has 75 debit from the voided JE (excluded) plus
//             // 33 x 4 from void victims (included until those tests run).
//             // The voided JE's 75 must NOT be in the total.
//             let expected = 33M * 4M
//             Assert.Equal(expected, debitAmount)
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-3.6 fetchByAccountIdList returns zero balances for account with no activity`` () =
//         let result = fetchByAccountIdList None [fixture.Data.assets1000Id] None
//         match result with
//         | Ok balances ->
//             Assert.Equal(1, balances |> List.length)
//             let bal = balances |> List.head
//             Assert.Equal(0M, bal.totalDebits |> Money.amount)
//             Assert.Equal(0M, bal.totalCredits |> Money.amount)
//             Assert.Equal(0M, bal.netBalance |> Money.amount)
//         | Error e -> Assert.Fail (AppError.toMessage e)
//
//     [<Fact>]
//     member _.``REQ-JE-3.6 fetchByAccountIdList with empty list returns Error`` () =
//         let result = fetchByAccountIdList None [] None
//         Assert.True(Result.isError result)
