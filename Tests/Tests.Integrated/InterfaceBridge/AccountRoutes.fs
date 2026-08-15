namespace Tests.Integrated.InterfaceBridge.AccountRoutes

open System
open DataAccessLayer.DbTransaction
open InterfaceBridge.InterfaceContracts.SharedContracts
open Utilities.Json.Json
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
open Tests.Helpers.SadPath
open Utilities
open Utilities.ResultHelper
open Xunit
open Tests.Helpers.Cleanup
open InterfaceBridge.InterfaceContracts.AccountContracts
open Utilities.AppError

open Model.Ledger.Journaling.JournalEntryComponent

[<Collection("SharedTestData")>]
type AccountRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-2.21 Account Create happy path``() =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let context = Context.create NoTransaction FetchOnly
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
            let context = Context.create NoTransaction FetchOnly
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
                    isCorrectError
                        (routeUiCommandForTesting "Account" "Create" [] payload)
                        AccountParentCodeInvalid
                        (Some "This may cause other tests to fail.")
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
                isCorrectError
                    (routeUiCommandForTesting "Account" "FetchByParentCode" [] payload)
                    AccountCodeDoesntMatchAccountId
                    None
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
            let context = Context.create NoTransaction FetchOnly
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
                isCorrectError
                    (routeUiCommandForTesting "Account" "Deactivate" [] payload)
                    AccountCodeDoesntMatchAccountId
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.8 Account UpdateName happy path``() =
        let code = "AC-4.8"
        let newName = "He's got the monkeys, let's see the monkeys"
        let mutable idToCleanUp_1 = None
        try
            let context = Context.create NoTransaction FetchOnly
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
                isCorrectError
                    (routeUiCommandForTesting "Account" "UpdateName" [] payload)
                    AccountCodeDoesntMatchAccountId
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.9 Account UpdateExternalReference happy path``() =
        let code = "AC-4.9"
        let newReference = Some "Genuflect, show some respect"
        let mutable idToCleanUp_1 = None
        try
            let context = Context.create NoTransaction FetchOnly
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
            do! isCorrectError
                    (routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload)
                    AccountCodeDoesntMatchAccountId
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-JE-3.9 FetchActivity happy path``() =
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
    member _.``REQ-JE-3.6 FetchBalances route happy path``() =
        result {
            let input: AccountBalanceFetchByAccountListInput = { codes = [ "F-2210"; "F-5350" ]; asOf = None }
            let! payload = input |> toJson<AccountBalanceFetchByAccountListInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchBalances" [] payload
            let! returned = fromJson<AccountBalanceReturn list> returnPayload
            Assert.Equal(2, returned |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("code", "", "AccountCodeIsEmpty")>]
    [<InlineData("code", "01234567890", "AccountCodeTooLong")>]
    [<InlineData("name", "", "AccountNameIsEmpty")>]
    [<InlineData("name",
                 "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789X",
                 "AccountNameTooLong")>]
    [<InlineData("accountTypeSt", "Fudge", "AccountTypeInvalid")>]
    [<InlineData("subType", "Fluffy", "AccountSubtypeInvalid")>]
    [<InlineData("parentCode", "", "AccountCodeIsEmpty")>]
    [<InlineData("parentCode", "01234567890", "AccountCodeTooLong")>]
    [<InlineData("reference", "", "AccountExternalReferenceIsEmpty")>]
    [<InlineData("reference",
                 "012345678901234567890123456789012345678901234567890",
                 "AccountExternalReferenceTooLong")>]
    member _.``REQ-AC-2.21 Account Create validates input as valid types``
        (field: string, value: string, expectedError: string)  =        
        let codeToUse = if field = "code" then value else genericAccountCodeString
        let nameToUse = if field = "name" then value else genericAccountNameString
        let typeToUse = if field = "accountTypeSt" then value else genericAccountTypeString
        let subTypeToUse = if field = "subType" then Some value else genericAccountSubtype
        let parentCodeToUse = if field = "parentCode" then Some value else genericAccountParentCode
        let referenceToUse = if field = "reference" then Some value else genericAccountReference
        let input: AccountCreateInput =
            { code = codeToUse
              name = nameToUse
              accountTypeSt = typeToUse
              activeBegin = genericAccountActiveBegin
              activeEnd = genericAccountActiveEnd
              subType = subTypeToUse
              parentCode = parentCodeToUse
              reference = referenceToUse }
        result {
            let! payload = input |> toJson<AccountCreateInput>
            do! isCorrectErrorString
                    (routeUiCommandForTesting "Account" "Create" [] payload)
                    expectedError
                    (Some "This may cause other tests to fail.")
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("codeEmpty", "AccountCodeIsEmpty")>]
    [<InlineData("codeTooLong", "AccountCodeTooLong")>]
    [<InlineData("alreadyInactive", "AccountAlreadyInactive")>]
    [<InlineData("proposedDateInvalid", "AccountDeactivationProposedDateIsInvalid")>]
    [<InlineData("activeChildren", "AccountActiveChildrenBeforeDeactivation")>]
    [<InlineData("nonZeroBalance", "AccountNonZeroBalanceBeforeDeactivation")>]
    member _.``REQ-AC-4.1 Deactivate validates input and state`` (scenario: string, expectedError: string) =
        let today = Calendar.today()
        let yesterday = today.PlusDays(-1)
        let codeToUse =
            match scenario with
            | "codeEmpty" -> ""
            | "codeTooLong" -> "01234567890"
            | "alreadyInactive" -> "F-1290"
            | "proposedDateInvalid" -> "F-3030"
            | "activeChildren" -> "F-5000"
            | "nonZeroBalance" -> "F-2210"
            | _ -> failwith $"Unknown scenario: {scenario}"
        let activeEndToUse =
            match scenario with
            | "proposedDateInvalid" -> Some(today.PlusYears(-2))
            | _ -> Some yesterday
        let input: AccountDeactivationInput = { code = codeToUse; activeEnd = activeEndToUse }
        result {
            let! payload = input |> toJson<AccountDeactivationInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "Deactivate" [] payload)
                    expectedError
                    (Some "This probably caused other tests to fail.")
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-4.6 Deactivate rejects when JEs dated after deactivation date``() =
        let today = Calendar.today()
        let yesterday = today.PlusDays(-1)
        let mutable accountIdToCleanUp: AccountId option = None
        let mutable jeIdToCleanUp: JournalEntryHeaderId option = None
        try
            let context = Context.create NoTransaction FetchOnly
            result {
                let! _, accountId =
                    createTestAccountFromPrimitives
                        context "AC-DJE" "Deactivation JE date test" "Expense"
                        (today.PlusYears(-1)) None (Some "OperatingExpense")
                        (Some fixture.Data.expenses5000Id) None
                accountIdToCleanUp <- Some accountId
                let! _, jeId =
                    createTestJournalEntryFromPrimitives
                        context "JE for deactivation date test" None today
                        [ (accountId, 50.00M, "Debit", None)
                          (accountId, 50.00M, "Credit", None) ]
                        [] []
                jeIdToCleanUp <- Some jeId
                let! payload =
                    { code = "AC-DJE"; activeEnd = Some yesterday } |> toJson<AccountDeactivationInput>
                do!
                    isCorrectError
                        (routeUiCommandForTesting "Account" "Deactivate" [] payload)
                        AccountDeactivationWithJournalEntriesDatedAfterDeactivationDate
                        (Some "This probably caused other tests to fail.")
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpJournalEntryId jeIdToCleanUp with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)
            match cleanUpAccountId accountIdToCleanUp with
            | Ok() -> ()
            | Error e -> Assert.Fail(AppError.toMessage e)



    [<Theory>]
    [<InlineData("accountCode", "", "AccountCodeIsEmpty")>]
    [<InlineData("accountCode", "aaaaaaaaaaaaa", "AccountCodeTooLong")>]
    [<InlineData("accountCode", "Z-9999", "AccountCodeDoesntMatchAccountId")>]
    [<InlineData("description", "", "JournalEntryDescriptionIsEmpty")>]
    [<InlineData("description",
                 "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789C0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789CM",
                 "JournalEntryDescriptionTooLong")>]
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
        (field: string, value: string, expectedError: string) =
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
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "FetchActivity" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("code", "", "AccountCodeIsEmpty")>]
    [<InlineData("code", "01234567890", "AccountCodeTooLong")>]
    [<InlineData("newName", "", "AccountNameIsEmpty")>]
    [<InlineData("newName",
                 "0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789X",
                 "AccountNameTooLong")>]
    member _.``REQ-AC-4.8 UpdateName validates input as valid types``
        (field: string, value: string, expectedError: string) =
        let codeToUse = if field = "code" then value else "F-1270"
        let nameToUse = if field = "newName" then value else "Valid name"
        let input: AccountUpdateNameInput = { code = codeToUse; newName = nameToUse }
        result {
            let! payload = input |> toJson<AccountUpdateNameInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "UpdateName" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("code", "", "AccountCodeIsEmpty")>]
    [<InlineData("code", "01234567890", "AccountCodeTooLong")>]
    [<InlineData("newReference", "", "AccountExternalReferenceIsEmpty")>]
    [<InlineData("newReference",
                 "012345678901234567890123456789012345678901234567890",
                 "AccountExternalReferenceTooLong")>]
    member _.``REQ-AC-4.9 UpdateExternalReference validates input as valid types``
        (field: string, value: string, expectedError: string) =
        let codeToUse = if field = "code" then value else "F-1270"
        let referenceToUse = if field = "newReference" then Some value else Some "Valid ref"
        let input: AccountUpdateExternalReferenceInput = { code = codeToUse; newReference = referenceToUse }
        result {
            let! payload = input |> toJson<AccountUpdateExternalReferenceInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("", "AccountCodeIsEmpty")>]
    [<InlineData("01234567890", "AccountCodeTooLong")>]
    [<InlineData("Z-9999", "AccountCodeDoesntMatchAccountId")>]
    member _.``REQ-AC-3.4 FetchByCode validates input as valid types``
        (code: string, expectedError: string) =
        result {
            let! payload = { AccountFetchByCodeInput.code = code } |> toJson<AccountFetchByCodeInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "FetchByCode" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("", "AccountCodeIsEmpty")>]
    [<InlineData("01234567890", "AccountCodeTooLong")>]
    member _.``REQ-AC-3.10 FetchByParentCode validates input as valid types``
        (parentCode: string, expectedError: string) =
        result {
            let! payload =
                { AccountFetchByParentCodeInput.parentCode = parentCode }
                |> toJson<AccountFetchByParentCodeInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "FetchByParentCode" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.6 FetchByAccountType rejects invalid type string``() =
        result {
            let! payload =
                { AccountFetchByAccountTypeInput.accountTypeSt = "Fudge" }
                |> toJson<AccountFetchByAccountTypeInput>
            do!
                isCorrectError
                    (routeUiCommandForTesting "Account" "FetchByAccountType" [] payload)
                    AccountTypeInvalid
                    None
            return ()
        }
        |> railroadWrapper

    [<Theory>]
    [<InlineData("codeEmpty", "AccountCodeIsEmpty")>]
    [<InlineData("codeTooLong", "AccountCodeTooLong")>]
    [<InlineData("codeInvalid", "AccountCodeDoesntMatchAccountId")>]
    [<InlineData("emptyList", "AccountBalanceFetchInvalidArguments")>]
    member _.``REQ-JE-3.6 FetchBalances validates input as valid types``
        (scenario: string, expectedError: string) =
        let codesToUse =
            match scenario with
            | "codeEmpty" -> [ "" ]
            | "codeTooLong" -> [ "01234567890" ]
            | "codeInvalid" -> [ "Z-9999" ]
            | "emptyList" -> []
            | _ -> failwith $"Unknown scenario: {scenario}"
        let input: AccountBalanceFetchByAccountListInput = { codes = codesToUse; asOf = None }
        result {
            let! payload = input |> toJson<AccountBalanceFetchByAccountListInput>
            do!
                isCorrectErrorString
                    (routeUiCommandForTesting "Account" "FetchBalances" [] payload)
                    expectedError
                    None
            return ()
        }
        |> railroadWrapper
