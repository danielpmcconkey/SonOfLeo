namespace Tests.Integrated.InterfaceBridge.AccountRoutes

open App.Session
open App.DataAccessLayer
open Business.General
open Business.FinancialServices
open Business.FinancialServices.Ledger
open Business.CrossDomainOrchestration
open System
open App.DataAccessLayer.DbTransaction
open Ui.InterfaceBridge.InterfaceContracts.SharedContracts
open App.Utility.Json.Json
open App.Operation.AuditEnvelope
open Business.FinancialServices.Ledger.Account
open Business.FinancialServices.Ledger.AccountComponent
open Tests.Helpers.EntityFunctions
open Tests.Helpers
open Tests.Helpers.GenericTestProperties
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Tests.Helpers.SadPath
open App.Utility
open App.Utility.Result
open Xunit
open Tests.Helpers.Cleanup
open Ui.InterfaceBridge.InterfaceContracts.AccountContracts
open App.Utility.IAppError
open Tests.Helpers.TestError
open Business.FinancialServices.Ledger.JournalEntryComponent
open App.Operation.CoreAuditableAction
open Business.FinancialServices.Ledger.LedgerAuditableAction
open Business.FinancialServices.DataIngestion.DataIngestionAuditableAction
open Business.FinancialServices.Classification.ClassificationAuditableAction
open Business.FinancialServices.CashFlow.CashFlowAuditableAction
open Business.FinancialServices.Ledger.LedgerError
open Business.FinancialServices.BizFinServError
open Business.CrossDomainOrchestration.JournalEntryOrchestration

[<Collection("SharedTestData")>]
type AccountRouteTests(fixture: TestDataFixture) =

    [<Fact>]
    member _.``REQ-AC-2.21 Account Create happy path``() =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let context = Context.create NoTransaction FetchOnly
            let parentId = fixture.Data.assets1000Id
            let parentCode =
                fixture.Data.accounts
                |> List.find(fun a -> a |> Account.accountId = parentId)
                |> Account.code |> AccountCode.value
            let accountInput: AccountCreateInput =
                { code = "AC-2.21"
                  name = "Route-created cash account"
                  accountTypeSt = "Asset"
                  activeBegin = Calendar.today().PlusDays(-30)
                  activeEnd = Some(Calendar.today().PlusYears(2))
                  subType = Some "Cash"
                  parentCode = Some parentCode
                  reference = Some "route-ref-2.21" }
            result {
                let! payload = accountInput |> toJson<AccountCreateInput>
                let! resultPayload = routeUiCommandForTesting "Account" "Create" [] payload
                let! accountReturn = fromJson<AccountReturn> resultPayload
                let! cleanUpId = accountReturn.code |> Business.FinancialServices.Ledger.Account.codeToId.fetch (context |> Context.getDatabaseTransaction)
                accountIdToCleanup <- (cleanUpId |> AccountId.fromGuid |> Some)
                // read back from the database, apart from the route's return
                let! stored = cleanUpId |> AccountId.fromGuid |> Account.fetchById context
                Assert.Equal(accountInput.name, stored |> Account.accountName |> AccountName.value)
                Assert.Equal(AccountType.Asset, stored |> Account.accountType)
                Assert.Equal(Some AccountSubtype.Cash, stored |> Account.accountSubType)
                Assert.Equal(accountInput.activeBegin, stored |> Account.activityPeriod |> ActivityPeriod.activeBegin)
                Assert.Equal(accountInput.activeEnd, stored |> Account.activityPeriod |> ActivityPeriod.activeEnd)
                Assert.Equal(Some parentId, stored |> Account.parentId)
                Assert.Equal(accountInput.reference, stored |> Account.externalReference |> Option.map AccountExternalReference.value)
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId accountIdToCleanup with
            | Ok() -> ()
            | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Create fails with invalid parent code``() =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let badAccountCode = "BullS**t"
            result {
                let accountInput =
                    { code = genericAccountCodeString
                      name = genericAccountNameString
                      accountTypeSt = genericAccountTypeString
                      activeBegin = genericActiveBegin
                      activeEnd = genericActiveEnd
                      subType = genericAccountSubtype
                      parentCode = Some badAccountCode
                      reference = genericAccountReference }
                let! payload = accountInput |> toJson<AccountCreateInput>
                let () =
                    match routeUiCommandForTesting "Account" "Create" [] payload with
                    | Error (AsError (AccountParentCodeInvalid code)) -> Assert.Equal(badAccountCode, code)
                    | Error e -> Assert.Fail $"Wrong error. {e.ToMessage()}"
                    | Ok returnPayload ->
                        // record what the route wrongly created, so finally removes it, then fail
                        accountIdToCleanup <-
                            fromJson<AccountReturn> returnPayload
                            |> Result.bind (fun created ->
                                // read from the table, not the code cache, which may hold an earlier GenCode's ID
                                Account.fetchAll (Context.create NoTransaction FetchOnly) false
                                |> Result.map (List.tryFind (fun a -> a |> Account.code |> AccountCode.value = created.code)))
                            |> Result.toOption
                            |> Option.flatten
                            |> Option.map Account.accountId
                        Assert.Fail "Expected failure; got success"
                return ()
            }
            |> railroadWrapper
        finally
            match cleanUpAccountId accountIdToCleanup with
            | Ok() -> ()
            | Error e -> Assert.Fail(e.ToMessage())

    [<Fact>]
    member _.``REQ-AC-3.4 Account FetchByCode happy path``() =
        let expectedCode = "F-1270"
        let expectedAccount =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.code |> AccountCode.value = expectedCode)
        let expectedName = expectedAccount |> Account.accountName |> AccountName.value
        let expectedType = expectedAccount |> Account.accountType |> AccountType.toString
        let payload =
            { code = expectedCode }
            |> toJson<AccountFetchByCodeInput>
            |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
        result {
            let! resultPayload = routeUiCommandForTesting "Account" "FetchByCode" [] payload
            let! returned = fromJson<AccountReturn> resultPayload
            Assert.Equal(expectedCode, returned.code)
            Assert.Equal(expectedName, returned.name)
            Assert.Equal(expectedType, returned.accountTypeSt)
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.10 Account FetchByParentCode happy path``() =
        let parentId = fixture.Data.assets1000Id
        let parentAccount =
            fixture.Data.accounts |> List.filter(fun a -> a |> Account.accountId = parentId) |> List.head
        let parentCode = parentAccount |> Account.code |> AccountCode.value
        let expectedChildren =
            fixture.Data.accounts |> List.filter(fun a -> a |> Account.parentId = (Some parentId))
        let expectedCodes =
            expectedChildren |> List.map(fun a -> a |> Account.code |> AccountCode.value) |> List.sort
        result {
            let! payload = { parentCode = parentCode } |> toJson<AccountFetchByParentCodeInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchByParentCode" [] payload
            let! fetchedChildren = fromJson<AccountReturn list> returnPayload
            Assert.Equal(expectedChildren |> List.length, fetchedChildren |> List.length)
            (* The right number of the wrong accounts satisfies a count. Name them. *)
            Assert.Equal<string list>(expectedCodes, fetchedChildren |> List.map(fun a -> a.code) |> List.sort)
            Assert.All(fetchedChildren, fun child -> Assert.Equal(Some parentCode, child.parentCode))
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
        let expectedCodes =
            fixture.Data.accounts
            |> List.filter(fun a -> a |> Account.accountType = AccountType.Revenue)
            |> List.map(fun a -> a |> Account.code |> AccountCode.value)
        result {
            let! payload = { accountTypeSt = explicitType } |> toJson<AccountFetchByAccountTypeInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchByAccountType" [] payload
            let! fetchedAccounts = fromJson<AccountReturn list> returnPayload
            Assert.Equal<Set<string>>(expectedCodes |> Set.ofList, fetchedAccounts |> List.map _.code |> Set.ofList)
            Assert.Equal(expectedCodes |> List.length, fetchedAccounts |> List.length)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.7 Account FetchAll happy path``() =
        let expected = fixture.Data.totalAccounts
        let expectedCodes = fixture.Data.accounts |> List.map(fun a -> a |> Account.code |> AccountCode.value) |> Set.ofList
        result {
            let! payload = { activeOnly = false } |> toJson<AccountFetchAllInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchAll" [] payload
            let! fetchedAccounts = fromJson<AccountReturn list> returnPayload
            Assert.Equal<Set<string>>(expectedCodes, fetchedAccounts |> List.map _.code |> Set.ofList)
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
            | Error e -> Assert.Fail(e.ToMessage())

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
            | Error e -> Assert.Fail(e.ToMessage())

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
            | Error e -> Assert.Fail(e.ToMessage())

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
    member _.``REQ-AC-3.12 FetchActivity happy path``() =
        let accountId = fixture.Data.mortgage2210Id
        let code =
            fixture.Data.accounts
            |> List.find(fun a -> a |> Account.accountId = accountId)
            |> Account.code |> AccountCode.value
        let expectedLines =
            fixture.Data.journalEntryLines
            |> List.filter(fun l -> l |> JournalEntryLine.accountId = accountId)
        let expected = expectedLines |> List.length
        let expectedLineAmounts =
            expectedLines
            |> List.map(fun l -> l |> JournalEntryLine.journalEntryLineId |> JournalEntryLineId.value, l |> JournalEntryLine.amount |> Money.amount)
            |> Set.ofList
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
            Assert.Equal<Set<Guid * decimal>>(
                expectedLineAmounts,
                returned |> List.choose _.activityDetail |> List.map(fun d -> d.lineId, d.amount) |> Set.ofList)
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-AC-3.13 FetchBalances route returns each code's name, unvoided debit and credit totals and normal-balance net``() =
        let unvoidedLines =
            fixture.Data.journalEntries
            |> List.filter(fun je ->
                je |> JournalEntryOrchestration.header |> JournalEntryHeader.voidedAt |> Option.isNone)
            |> List.collect JournalEntryOrchestration.jeLines
        let expectedFor (accountId: AccountId) (debitNormal: bool) : AccountBalanceReturn =
            let account = fixture.Data.accounts |> List.find(fun a -> a |> Account.accountId = accountId)
            let sumOf lineType =
                unvoidedLines
                |> List.filter(fun l -> l |> JournalEntryLine.accountId = accountId && l |> JournalEntryLine.lineType = lineType)
                |> List.sumBy(fun l -> l |> JournalEntryLine.amount |> Money.amount)
            let debits = sumOf JournalEntryLineType.Debit
            let credits = sumOf JournalEntryLineType.Credit
            { accountCode = account |> Account.code |> AccountCode.value
              accountName = account |> Account.accountName |> AccountName.value
              totalDebits = debits
              totalCredits = credits
              netBalance = if debitNormal then debits - credits else credits - debits }
        // a credit-normal Liability and a debit-normal Expense
        let expected = [ expectedFor fixture.Data.mortgage2210Id false; expectedFor fixture.Data.food5350Id true ]
        result {
            let input: AccountBalanceFetchByAccountListInput = { codes = expected |> List.map _.accountCode; asOf = None }
            let! payload = input |> toJson<AccountBalanceFetchByAccountListInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchBalances" [] payload
            let! returned = fromJson<AccountBalanceReturn list> returnPayload
            Assert.All(expected, fun e -> Assert.NotEqual(0M, e.netBalance))
            Assert.Equal<AccountBalanceReturn list>(expected |> List.sortBy _.accountCode, returned |> List.sortBy _.accountCode)
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
              activeBegin = genericActiveBegin
              activeEnd = genericActiveEnd
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
            | Error e -> Assert.Fail(e.ToMessage())
            match cleanUpAccountId accountIdToCleanUp with
            | Ok() -> ()
            | Error e -> Assert.Fail(e.ToMessage())



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
    member _.``REQ-AC-3.12.1 FetchActivity validates all input as valid types``
        (field: string, value: string, expectedError: string) =
        let convertValueToTemporalFilter () : Result<TemporalFilterInput, IAppError> =
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
                                |> Result.defaultWith(fun (e: IAppError) -> failwith(e.ToMessage()))
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
    member _.``REQ-AC-3.11 REQ-AC-3.13.3 FetchBalances validates input as valid types``
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

