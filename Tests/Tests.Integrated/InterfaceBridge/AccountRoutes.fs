namespace Tests.Integrated.InterfaceBridge.AccountRoutes

open System
open DataAccessLayer.DbTransaction
open InterfaceBridge.InterfaceContracts.SharedContracts
open InterfaceBridge.Json.Json
open Logger.Audit
open Model
open Model.Ledger.Accounts
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.Journaling
open Tests.Helpers.EntityFunctions
open Tests.Helpers
open Tests.Helpers.GenericTestProperties
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Utilities
open Utilities.ResultHelper
open Xunit
open Tests.Helpers.Cleanup
open InterfaceBridge.InterfaceContracts.AccountContracts
open Utilities.AppError
open Context.Context

[<Collection("SharedTestData")>]
type AccountRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-2.21 Account Create happy path``() =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let accountInput = createAccountInput "AC-2.21"
                let! payload = accountInput |> toJson<AccountCreateInput>
                let! resultPayload = routeUiCommandForTesting "Account" "Create" [] payload
                let! accountReturn = fromJson<AccountReturn> resultPayload
                let! cleanUpId = accountReturn.code |> LookupCache.accountCodeToId.fetch context
                accountIdToCleanup <- (cleanUpId |> AccountId.fromGuid |> Some)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId accountIdToCleanup with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Create fails with invalid parent code``() =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let badAccountCode = Some "BullS**t"
            let context = create NoTransaction FetchOnly
            result {
                let accountInput =
                    { code = genericAccountCodeString
                      name = genericAccountNameString
                      accountTypeSt = genericAccountTypeString
                      activeBegin = genericAccountActiveBegin
                      activeEnd = genericAccountActiveEnd
                      subType = genericAccountSubtype
                      parentCode = badAccountCode
                      reference = genericAccountReference }
                let! payload = accountInput |> toJson<AccountCreateInput>
                do!
                    match routeUiCommandForTesting "Account" "Create" [] payload with
                    | Ok resultPayload ->
                        result {
                            let! accountReturn = fromJson<AccountReturn> resultPayload
                            let! cleanUpId = accountReturn.code |> LookupCache.accountCodeToId.fetch context
                            accountIdToCleanup <- (cleanUpId |> AccountId.fromGuid |> Some)
                            return! Error(TestingError "Expected failure; returned success.")
                        }
                    | Error(AccountParentCodeInvalid _) -> Ok()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId accountIdToCleanup with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-3.4 Account FetchByCode happy path``() =
        let payload =
            { code = "F-1270" }
            |> toJson<AccountFetchByCodeInput>
            |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
        match routeUiCommandForTesting "Account" "FetchByCode" [] payload with
        | Ok _ -> ()
        | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-3.10 Account FetchByParentCode happy path``() =
        let parentId = fixture.Data.assets1000Id
        let parentAccount =
            fixture.Data.accounts |> List.filter(fun a -> a |> Account.accountId = parentId) |> List.head
        let parentCode = parentAccount |> Account.code |> AccountCode.value
        let expected =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.parentId = (Some parentId))
            |> List.length
        result {
            let! payload = { parentCode = parentCode } |> toJson<AccountFetchByParentCodeInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchByParentCode" [] payload
            let! fetchedChildren = fromJson<AccountReturn list> returnPayload
            Assert.Equal(expected, fetchedChildren |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account FetchByParentCode fails with invalid code``() =
        let badAccountCode = "HorseS**t"
        result {
            let! payload = { parentCode = badAccountCode } |> toJson<AccountFetchByParentCodeInput>
            do!
                match routeUiCommandForTesting "Account" "FetchByParentCode" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error(AccountCodeDoesntMatchAccountId _) -> Ok()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.6 Account FetchByAccountType happy path``() =
        let explicitType = "Revenue"
        let expected =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.accountType |> AccountType.toString = explicitType)
            |> List.length
        result {
            let! payload = { accountTypeSt = explicitType } |> toJson<AccountFetchByAccountTypeInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchByAccountType" [] payload
            let! fetchedAccounts = fromJson<AccountReturn list> returnPayload
            fetchedAccounts |> List.forall(fun x -> x.accountTypeSt = explicitType) |> Assert.True
            Assert.Equal(expected, fetchedAccounts |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.7 Account FetchAll happy path``() =
        let expected = fixture.Data.totalAccounts
        result {
            let! payload = { activeOnly = false } |> toJson<AccountFetchAllInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchAll" [] payload
            let! fetchedAccounts = fromJson<AccountReturn list> returnPayload
            Assert.Equal(expected, fetchedAccounts |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.1 Account Deactivate happy path``() =
        let now = Calendar.today()
        let endDate = now.PlusDays(-1)
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! _, accountId = genericAccountCodeString |> createTestAccountFromCodeString context
                idToCleanUp_1 <- Some accountId
                let! payload =
                    { code = genericAccountCodeString; activeEnd = Some endDate }
                    |> toJson<AccountDeactivationInput>
                let! returnPayload = routeUiCommandForTesting "Account" "Deactivate" [] payload
                let! accountReturn = returnPayload |> fromJson<AccountReturn>
                Assert.Equal(Some endDate, accountReturn.activeEnd)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId idToCleanUp_1 with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Deactivate fails with invalid code``() =
        let badAccountCode = "BatS**t"
        let now = Calendar.today()
        let activeEnd = now.PlusDays(-1)
        result {
            let! payload = { code = badAccountCode; activeEnd = Some activeEnd } |> toJson<AccountDeactivationInput>
            do!
                match routeUiCommandForTesting "Account" "Deactivate" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error(AccountCodeDoesntMatchAccountId _) -> Ok()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.8 Account UpdateName happy path``() =
        let code = "AC-4.8"
        let newName = "He's got the monkeys, let's see the monkeys"
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! _, accountId = code |> createTestAccountFromCodeString context
                idToCleanUp_1 <- Some accountId
                let! payload = { code = code; newName = newName } |> toJson<AccountUpdateNameInput>
                let! returnPayload = routeUiCommandForTesting "Account" "UpdateName" [] payload
                let! accountReturn = returnPayload |> fromJson<AccountReturn>
                Assert.Equal(newName, accountReturn.name)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId idToCleanUp_1 with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account UpdateName fails with invalid code``() =
        let badAccountCode = "ApeS**t"
        let newName = "I picked the wrong day to quit sniffing glue"
        result {
            let! payload = { code = badAccountCode; newName = newName } |> toJson<AccountUpdateNameInput>
            do!
                match routeUiCommandForTesting "Account" "UpdateName" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error(AccountCodeDoesntMatchAccountId _) -> Ok()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.9 Account UpdateExternalReference happy path``() =
        let code = "AC-4.9"
        let newReference = Some "Genuflect, show some respect"
        let mutable idToCleanUp_1 = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! _, accountId = code |> createTestAccountFromCodeString context
                idToCleanUp_1 <- Some accountId
                let! payload =
                    { code = code; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
                let! returnPayload = routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload
                let! accountReturn = returnPayload |> fromJson<AccountReturn>
                Assert.Equal(newReference, accountReturn.reference)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId idToCleanUp_1 with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account UpdateExternalReference fails with invalid code``() =
        let badAccountCode = "DogS**t"
        let newReference = Some "I'm not bad; I'm just drawn that way"
        result {
            let! payload =
                { code = badAccountCode; newReference = newReference }
                |> toJson<AccountUpdateExternalReferenceInput>
            do!
                match routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error(AccountCodeDoesntMatchAccountId _) -> Ok()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.9 FetchActivity happy path``() = // todo: ask claude to provide the correct REQ #
        let code = "F-2210"
        let account =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.code |> AccountCode.value = code)
            |> List.head
        let accountId = account |> Account.accountId
        let expected =
            fixture.Data.journalEntryLines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = accountId)
            |> List.length
        result {
            let input: AccountActivityFetchInput =
                { filter =
                    { accountCode = Some code
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
            let! returnPayload = routeUiCommandForTesting "Account" "FetchActivity" [] payload
            let! returned = fromJson<AccountActivityReturn list> returnPayload
            let actual = returned |> List.length
            Assert.Equal(expected, actual)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.6 FetchBalances route happy path``() = // todo: ask claude to provide the correct REQ #
        // note, we only check simple execution. We have more specific tests in the model orchestration tests.
        result {
            let input: AccountBalanceFetchByAccountListInput = { codes = [ "F-2210"; "F-5350" ]; asOf = None }
            let! payload = input |> toJson<AccountBalanceFetchByAccountListInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchBalances" [] payload
            let! returned = fromJson<AccountBalanceReturn list> returnPayload
            Assert.Equal(2, returned |> List.length)
            return ()
        }
        |> railroadWrapper

    // todo: create a test that checks that FetchBalances fails with improper codes

    // todo: create a test that checks that FetchBalances fails with improper asOf



    [<Theory>]
    [<InlineData("accountCode", "", "AccountCodeIsEmpty")>]
    [<InlineData("accountCode", "aaaaaaaaaaaaa", "AccountCodeTooLong")>]
    [<InlineData("temporalFilter", "periodKey: ", "FiscalPeriodInvalidKeyString")>]
    [<InlineData("temporalFilter", "periodKey:1974-03", "FiscalPeriodNoPeriodMatchingKey")>]
    [<InlineData("source", "", "JournalEntrySourceIsEmpty")>]
    [<InlineData("source", "012345678901234567890123456789012345678901234567890123456789", "JournalEntrySourceTooLong")>]
    [<InlineData("accountType", "Fudge", "AccountTypeInvalid")>]
    [<InlineData("accountSubtype", "Fluffy", "AccountSubtypeInvalid")>]
    [<InlineData("accountParentCode", "", "AccountParentCodeIsEmpty")>]
    [<InlineData("accountParentCode", "aaaaaaaaaaaaa", "AccountParentCodeTooLong")>]
    [<InlineData("accountParentCode", "9999", "AccountParentCodeInvalid")>]
    [<InlineData("amount", "10.307", "MoneyFailedToConvertImproperPrecision")>]
    [<InlineData("amount", "19999999999.99", "MoneyFailedToConvertExceededMax")>]
    [<InlineData("amount", "-19999999999.99", "MoneyFailedToConvertBelowMin")>]
    member _.``REQ-JE-3.9 FetchActivity validates all input as valid types``
        (field: string, value: string, error: string)
        = // todo: ask claude to provide the correct REQ #
        let convertValueToTemporalFilter () : Result<TemporalFilterInput, AppError> =
            match value.IndexOf(':') with
            | -1 -> Error(TestingError "bad inline data on temporal filter")
            | index ->
                let subField = value[0 .. (index - 1)]
                let valueToTest = value[index + 1 ..]
                match subField with
                | "periodKey" -> Ok(TemporalFilterInput.PeriodKey valueToTest)
                | "beginDate" -> Error(TestingError "it's impossible to send in a mal-formed LocalDate")
                | "endDate" -> Error(TestingError "it's impossible to send in a mal-formed LocalDate")
                | _ -> Error(TestingError "bad inline data on temporal filter")
        result {
            let input: AccountActivityFetchInput =
                { filter =
                    { accountCode = if field = "accountCode" then Some value else None
                      temporalFilter =
                        if field = "temporalFilter" then
                            Some(
                                convertValueToTemporalFilter()
                                |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))
                            )
                        else
                            None
                      source = if field = "source" then Some value else None
                      accountType = if field = "accountType" then Some value else None
                      accountSubtype = if field = "accountSubtype" then Some value else None
                      accountParentCode = if field = "accountParentCode" then Some value else None
                      journalEntryId =
                        if field = "journalEntryId" then
                            Some(Guid.Parse(value))
                        else
                            None
                      amount =
                        if field = "amount" then
                            Some(Decimal.Parse(value))
                        else
                            None
                      description = if field = "description" then Some value else None
                      unVoidedOnly = false }
                  sort = None }
            let! payload = input |> toJson<AccountActivityFetchInput>
            do!
                match routeUiCommandForTesting "Account" "FetchActivity" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error e ->
                    if e.IsAccountCodeIsEmpty && error = "AccountCodeIsEmpty" then Ok()
                    elif e.IsAccountCodeTooLong && error = "AccountCodeTooLong" then Ok()
                    elif e.IsAccountParentCodeIsEmpty && error = "AccountParentCodeIsEmpty" then Ok()
                    elif e.IsAccountParentCodeTooLong && error = "AccountParentCodeTooLong" then Ok()
                    elif e.IsFiscalPeriodInvalidKeyString && error = "FiscalPeriodInvalidKeyString" then Ok()
                    elif e.IsFiscalPeriodNoPeriodMatchingKey && error = "FiscalPeriodNoPeriodMatchingKey" then Ok()
                    elif e.IsJournalEntrySourceIsEmpty && error = "JournalEntrySourceIsEmpty" then Ok()
                    elif e.IsJournalEntrySourceTooLong && error = "JournalEntrySourceTooLong" then Ok()
                    elif e.IsAccountTypeInvalid && error = "AccountTypeInvalid" then Ok()
                    elif e.IsAccountSubtypeInvalid && error = "AccountSubtypeInvalid" then Ok()
                    elif e.IsAccountParentCodeInvalid && error = "AccountParentCodeInvalid" then Ok()
                    elif e.IsDalResultantRowsDidntMatchExpectation && error = "DalResultantRowsDidntMatchExpectation" then Ok()
                    elif e.IsMoneyFailedToConvertImproperPrecision && error = "MoneyFailedToConvertImproperPrecision" then Ok()
                    elif e.IsMoneyFailedToConvertExceededMax && error = "MoneyFailedToConvertExceededMax" then Ok()
                    elif e.IsMoneyFailedToConvertBelowMin && error = "MoneyFailedToConvertBelowMin" then Ok()
                    else Error(TestingError $"Wrong error type. Expected {error}. {AppError.toMessage e}")
            return ()
        }
        |> railroadWrapper
