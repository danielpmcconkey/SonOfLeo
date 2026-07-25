namespace Tests.Integrated.SonOfLeoCli

open InterfaceBridge.InterfaceContracts.AccountContracts
open InterfaceBridge.Json.Json
open Tests.Integrated
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Utilities.AppError
open Utilities.ResultHelper
open Xunit

[<Collection("SharedTestData")>]
type ProgramTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-NGUI-1.3 System responds with a failure code when failing``() =
        let args = [ "Account"; "Create" ]
        let badPayload = "{}"
        let exitCode, _, _ = runCli args badPayload
        (exitCode = 1) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-1.3, REQ-NGUI-3.6 System responds with a success code when succeeding``() =
        let args = [ "Account"; "FetchAll" ]
        let payload =
            { activeOnly = true }
            |> toJson<AccountFetchAllInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let exitCode, _, _ = runCli args payload
        (exitCode = 0) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-1.3.1, REQ-NGUI-3.7 The stderr will comprise the error message``() =
        let code = "burp"
        let expectedError = AppError.toMessage(AccountCodeDoesntMatchAccountId code)
        let args = [ "Account"; "FetchByCode" ]
        let payload =
            { code = code }
            |> toJson<AccountFetchByCodeInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let _, _, e = runCli args payload
        Assert.Contains(expectedError, e)

    [<Fact>]
    member _.``REQ-NGUI-3.6 System responds with the payload via stdout upon success``() =
        let args = [ "Account"; "FetchByCode" ]
        let payload =
            { code = "F-1270" }
            |> toJson<AccountFetchByCodeInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let exitCode, p, _ = runCli args payload
        Assert.Equal(0, exitCode)
        let railroad =
            result {
                let! fetched = fromJson<AccountReturn> p
                Assert.Equal("Money Market", fetched.name)
                return ()
            }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-3.8 The domain argument is case sensitive``() =
        let args = [ "account"; "FetchAll" ]
        let payload =
            { activeOnly = true }
            |> toJson<AccountFetchAllInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let exitCode, _, _ = runCli args payload
        (exitCode = 1) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-3.8 The verb argument is case sensitive``() =
        let args = [ "Account"; "fetchAll" ]
        let payload =
            { activeOnly = true }
            |> toJson<AccountFetchAllInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let exitCode, _, _ = runCli args payload
        (exitCode = 1) |> Assert.True

    [<Fact>]
    member _.``REQ-NGUI-3.9 Incorrect routes must exit with an appropriate error``() =
        let expected = "Unknown command: Ropa Interior"
        let args = [ "Ropa"; "Interior" ]
        let payload =
            { activeOnly = true }
            |> toJson<AccountFetchAllInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        let exitCode, _, e = runCli args payload
        (exitCode = 1) |> Assert.True
        Assert.Equal(expected, e.Trim())
