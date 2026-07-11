namespace Tests.Integrated.SonOfLeoCli

open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.UI.InterfaceContractTypes
open Model.UI.Json
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Utilities
open Xunit
open Utilities.ResultCE
open Tests.Integrated._Cleanup

[<Collection("SharedTestData")>]
type AccountRouteTests(fixture: TestDataFixture) =

    static let createAccountInput codeToUse =
        { code = codeToUse; name = genericAccountNameString; accountTypeSt = genericAccountTypeString
          activeBegin =  genericAccountActiveBegin; activeEnd = genericAccountActiveEnd; subType = genericAccountSubtype
          parentCode = genericAccountParentCode; reference = genericAccountReference }

    static let createAccountInDb codeToUse =
        Account.constructNewAndSaveToDb codeToUse genericAccountNameString genericAccountTypeString
                        genericAccountActiveBegin genericAccountActiveEnd genericAccountSubtype genericAccountParentId
                        genericAccountReference genericAuditEnvelope None

    // =============================================================================
    // Create
    // =============================================================================

    [<Fact>]
    member _.``REQ-AC-2.21 Account Create happy path`` () =
        let accountInput = createAccountInput genericAccountCodeString
        let args = ["Account"; "Create"]
        let payload = accountInput |> toJson<AccountCreateInput> |> Result.defaultWith failwith
        let code, a, e = runCli args payload
        match code with
        | 0 ->
            let accountReturn:AccountReturn = fromJson<AccountReturn> a |> Result.defaultWith failwith
            let cleanUpId = accountReturn.code |> LookupCache.accountCodeToId.fetch |> Result.defaultWith failwith
            cleanUpAccountId (Some cleanUpId) |> Result.defaultWith failwith
        | _ ->
            Assert.Fail $"Create Account happy path returned a non-zero value: {e}"

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Create fails with invalid parent code`` () =
        let badAccountCode = Some "BullS**t"
        let expectedReturnCode = 1
        let expectedError = "Parent code provided didn't match any recorded Accounts in the database."
        let railroad = result {
            let accountInput =
                { code = genericAccountCodeString; name = genericAccountNameString; accountTypeSt = genericAccountTypeString
                  activeBegin =  genericAccountActiveBegin; activeEnd = genericAccountActiveEnd; subType = genericAccountSubtype
                  parentCode = badAccountCode; reference = genericAccountReference }
            let! payload = accountInput |> toJson<AccountCreateInput>
            let args = ["Account"; "Create"]
            let code, _, e = runCli args payload
            Assert.Equal(expectedReturnCode, code)
            Assert.Contains(expectedError, e)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    // =============================================================================
    // Read
    // =============================================================================

    [<Fact>]
    member _.``REQ-AC-3.4 Account FetchByCode happy path`` () =
        let args = ["Account"; "FetchByCode"]
        let payload = { code = "F-1270" } |> toJson<AccountFetchByCodeInput> |> Result.defaultWith failwith
        let code, _, e = runCli args payload
        match code with
        | 0 -> ()
        | _ -> Assert.Fail $"Account FetchByCode happy path returned a non-zero value: {e}"

    [<Fact>]
    member _.``REQ-AC-3.10 Account FetchByParentCode happy path`` () =
        let railroad = result {
            let! payload = { parentCode = "F-1000" } |> toJson<AccountFetchByParentCodeInput>
            let args = ["Account"; "FetchByParentCode"]
            let code, fetchedAccountRecords, e = runCli args payload

            do! if code <> 0
                then Error $"FetchByParentCode CLI returned {code}: {e}"
                else Ok ()

            let! fetchedChildren = fromJson<AccountReturn list> fetchedAccountRecords
            Assert.Equal(3, fetchedChildren |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account FetchByParentCode fails with invalid code`` () =
        let badAccountCode = "HorseS**t"
        let expectedReturnCode = 1
        let expectedError = "Parent code provided didn't match any recorded Accounts in the database."
        let railroad = result {
            let! payload = { parentCode = badAccountCode } |> toJson<AccountFetchByParentCodeInput>
            let args = ["Account"; "FetchByParentCode"]
            let code, _, e = runCli args payload
            Assert.Equal(expectedReturnCode, code)
            Assert.Contains(expectedError, e)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-AC-3.6 Account FetchByAccountType happy path`` () =
        let explicitType = "Revenue"
        let railroad = result {
            let! payload = { accountTypeSt = explicitType } |> toJson<AccountFetchByAccountTypeInput>
            let args = ["Account"; "FetchByAccountType"]
            let code, fetchedAccountRecords, e = runCli args payload

            do! if code <> 0
                then Error $"FetchByAccountType CLI returned {code}: {e}"
                else Ok ()

            let! fetchedAccounts = fromJson<AccountReturn list> fetchedAccountRecords

            fetchedAccounts
            |> List.forall (fun x -> x.accountTypeSt = explicitType)
            |> Assert.True

            Assert.True(fetchedAccounts |> List.length >= 2)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-AC-3.7 Account FetchAll happy path`` () =
        let railroad = result {
            let! payload = { activeOnly = false } |> toJson<AccountFetchAllInput>
            let args = ["Account"; "FetchAll"]
            let code, fetchedAccountRecords, e = runCli args payload

            do! if code <> 0
                then Error $"FetchAll CLI returned {code}: {e}"
                else Ok ()

            let! fetchedAccounts = fromJson<AccountReturn list> fetchedAccountRecords
            Assert.True(fetchedAccounts |> List.length >= 14)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    // =============================================================================
    // Update
    // =============================================================================

    [<Fact>]
    member _.``REQ-AC-4.1 Account Deactivate happy path`` () =
        let now = Calendar.today()
        let endDate = now.PlusDays(-1)
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! account = createAccountInDb genericAccountCodeString
                let accountId = Account.uniqueId account
                idToCleanUp_1 <- Some accountId
                let! payload = { code = genericAccountCodeString; activeEnd = endDate } |> toJson<AccountDeactivationInput>
                let args = ["Account"; "Deactivate"]
                let code, accountReturnString, e = runCli args payload
                do! if code = 1 then Error e else Ok ()
                let! accountReturn = accountReturnString |> fromJson<AccountReturn>
                Assert.Equal(Some endDate, accountReturn.activeEnd)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpAccountId idToCleanUp_1 with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Deactivate fails with invalid code`` () =
        let badAccountCode = "BatS**t"
        let now = Calendar.today()
        let activeEndInstant = now.PlusDays(-1)
        let expectedReturnCode = 1
        let expectedError = "Account code provided didn't match any recorded Accounts in the database."
        let railroad = result {
            let! payload = { code = badAccountCode; activeEnd = activeEndInstant } |> toJson<AccountDeactivationInput>
            let args = ["Account"; "Deactivate"]
            let code, _, e = runCli args payload
            Assert.Equal(expectedReturnCode, code)
            Assert.Contains(expectedError, e)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-AC-4.8 Account UpdateName happy path`` () =
        let newName = "He's got the monkeys, let's see the monkeys"
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! account = createAccountInDb genericAccountCodeString
                let accountId = Account.uniqueId account
                idToCleanUp_1 <- Some accountId
                let! payload = { code = genericAccountCodeString; newName = newName } |> toJson<AccountUpdateNameInput>
                let args = ["Account"; "UpdateName"]
                let code, accountReturnString, _ = runCli args payload
                Assert.Equal(0, code)
                let! accountReturn = accountReturnString |> fromJson<AccountReturn>
                Assert.Equal(newName, accountReturn.name)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpAccountId idToCleanUp_1 with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account UpdateName fails with invalid code`` () =
        let badAccountCode = "ApeS**t"
        let newName = "I picked the wrong day to quit sniffing glue"
        let expectedReturnCode = 1
        let expectedError = "Account code provided didn't match any recorded Accounts in the database."
        let railroad = result {
            let! payload = { code = badAccountCode; newName = newName } |> toJson<AccountUpdateNameInput>
            let args = ["Account"; "UpdateName"]
            let code, _, e = runCli args payload
            Assert.Equal(expectedReturnCode, code)
            Assert.Contains(expectedError, e)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-AC-4.9 Account UpdateExternalReference happy path`` () =
        let newReference = Some "Genuflect, show some respect"
        let mutable idToCleanUp_1 = None
        try
            let railroad = result {
                let! account = createAccountInDb genericAccountCodeString
                let accountId = Account.uniqueId account
                idToCleanUp_1 <- Some accountId
                let! payload = { code = genericAccountCodeString; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
                let args = ["Account"; "UpdateExternalReference"]
                let code, accountReturnString, _ = runCli args payload
                Assert.Equal(0, code)
                let! accountReturn = accountReturnString |> fromJson<AccountReturn>
                Assert.Equal(newReference, accountReturn.reference)
                return ()
            }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpAccountId idToCleanUp_1 with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account UpdateExternalReference fails with invalid code`` () =
        let badAccountCode = "DogS**t"
        let newReference = Some "I'm not bad; I'm just drawn that way"
        let expectedReturnCode = 1
        let expectedError = "Account code provided didn't match any recorded Accounts in the database"
        let railroad = result {
            let! payload = { code = badAccountCode; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
            let args = ["Account"; "UpdateExternalReference"]
            let code, _, e = runCli args payload
            Assert.Equal(expectedReturnCode, code)
            Assert.Contains(expectedError, e)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    // =============================================================================
    // FetchActivity route
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.9 FetchActivity route returns enriched activity for an account`` () =
        let railroad = result {
            let input : AccountActivityFetchInput = {
                filter = {
                    accountCode = Some "F-2210"
                    temporalFilter = None
                    source = None
                    accountType = None
                    accountSubtype = None
                    accountParentCode = None
                    journalEntryId = None
                    amount = None
                    description = None
                    unVoidedOnly = false }
                sort = None }
            let! payload = input |> toJson<AccountActivityFetchInput>
            let code, stdout, e = runCli ["Account"; "FetchActivity"] payload
            do! if code <> 0 then Error $"FetchActivity returned non-zero: {e}" else Ok ()
            let! returned = fromJson<AccountActivityReturn list> stdout
            Assert.True(returned |> List.length >= 1)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    // =============================================================================
    // FetchBalances route
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.6 FetchBalances route returns balances for given account codes`` () =
        let railroad = result {
            let input : AccountBalanceFetchByAccountListInput = { codes = ["F-2210"; "F-5350"]; asOf = None }
            let! payload = input |> toJson<AccountBalanceFetchByAccountListInput>
            let code, stdout, e = runCli ["Account"; "FetchBalances"] payload
            do! if code <> 0 then Error $"FetchBalances returned non-zero: {e}" else Ok ()
            let! returned = fromJson<AccountBalanceReturn list> stdout
            Assert.Equal(2, returned |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
