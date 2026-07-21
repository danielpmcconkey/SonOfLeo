namespace Tests.Integrated.InterfaceBridge.AccountRoutes

open InterfaceBridge.Json.Json
open Model
open Model.Ledger.Accounts.AccountComponent
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.InterfaceBridge._routeResolver
open Utilities
open Utilities.ResultHelper
open Xunit
open Tests.Integrated._Cleanup
open InterfaceBridge.InterfaceContracts.AccountContracts
open Utilities.AppError

 [<Collection("SharedTestData")>]
type AccountRouteTests(fixture: TestDataFixture) =

    // =============================================================================
    // Create
    // =============================================================================
    
    [<Fact>]
    member _.``REQ-AC-2.21 Account Create happy path`` () =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let railroad = result {
                let accountInput = createAccountInput genericAccountCodeString
                let! payload = accountInput |> toJson<AccountCreateInput>
                let! resultPayload = routeUiCommandForTesting "Account" "Create" [] payload
                let! accountReturn = fromJson<AccountReturn> resultPayload
                let! cleanUpId = accountReturn.code |> LookupCache.accountCodeToId.fetch
                accountIdToCleanup <- (cleanUpId |> AccountId.fromGuid |> Some)
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            cleanUpAccountId accountIdToCleanup |> ignore

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Create fails with invalid parent code`` () =
        let mutable accountIdToCleanup: AccountId option = None
        try
            let badAccountCode = Some "BullS**t"
            let railroad = result {
                let accountInput =
                    { code = genericAccountCodeString; name = genericAccountNameString; accountTypeSt = genericAccountTypeString
                      activeBegin =  genericAccountActiveBegin; activeEnd = genericAccountActiveEnd; subType = genericAccountSubtype
                      parentCode = badAccountCode; reference = genericAccountReference }
                let! payload = accountInput |> toJson<AccountCreateInput>
                do! match routeUiCommandForTesting "Account" "Create" [] payload with
                    | Ok resultPayload ->
                        result {
                            let! accountReturn = fromJson<AccountReturn> resultPayload
                            let! cleanUpId = accountReturn.code |> LookupCache.accountCodeToId.fetch
                            accountIdToCleanup <- (cleanUpId |> AccountId.fromGuid |> Some)
                            return! Error(TestingError "Expected failure; returned success.") }
                    | Error (AccountParentCodeInvalid _) -> Ok ()
                    | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
                return () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            cleanUpAccountId accountIdToCleanup |> ignore

    // =============================================================================
    // Read
    // =============================================================================

    [<Fact>]
    member _.``REQ-AC-3.4 Account FetchByCode happy path`` () =
        let payload = { code = "F-1270" } |> toJson<AccountFetchByCodeInput> |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))
        match routeUiCommandForTesting "Account" "FetchByCode" [] payload with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-3.10 Account FetchByParentCode happy path`` () =
        let railroad = result {
            let! payload = { parentCode = "F-1000" } |> toJson<AccountFetchByParentCodeInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchByParentCode" [] payload
            let! fetchedChildren = fromJson<AccountReturn list> returnPayload
            Assert.Equal(3, fetchedChildren |> List.length)
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account FetchByParentCode fails with invalid code`` () =
        let badAccountCode = "HorseS**t"
        let railroad = result {
            let! payload = { parentCode = badAccountCode } |> toJson<AccountFetchByParentCodeInput>
            do! match routeUiCommandForTesting "Account" "FetchByParentCode" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (InterfaceBridgeConversionFailure _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
            return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-3.6 Account FetchByAccountType happy path`` () =
        let explicitType = "Revenue"
        let railroad = result {
            let! payload = { accountTypeSt = explicitType } |> toJson<AccountFetchByAccountTypeInput>
            let! returnPayload = routeUiCommandForTesting "Account" "FetchByAccountType" [] payload 
            let! fetchedAccounts = fromJson<AccountReturn list> returnPayload
            fetchedAccounts
            |> List.forall (fun x -> x.accountTypeSt = explicitType)
            |> Assert.True
            Assert.True(fetchedAccounts |> List.length >= 2)
            return () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-3.7 Account FetchAll happy path`` () =
        let railroad = result {
           let! payload = { activeOnly = false } |> toJson<AccountFetchAllInput>
           let! returnPayload = routeUiCommandForTesting "Account" "FetchAll" [] payload
           let! fetchedAccounts = fromJson<AccountReturn list> returnPayload
           Assert.True(fetchedAccounts |> List.length >= 14)
           return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

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
              let! accountId = createTestAccountFromCodeString genericAccountCodeString
              idToCleanUp_1 <- Some accountId
              let! payload = { code = genericAccountCodeString; activeEnd = Some endDate } |> toJson<AccountDeactivationInput>
              let! returnPayload = routeUiCommandForTesting "Account" "Deactivate" [] payload
              let! accountReturn = returnPayload |> fromJson<AccountReturn>
              Assert.Equal(Some endDate, accountReturn.activeEnd)
              return ()
           }
           match railroad with
           | Ok _ -> ()
           | Error e -> Assert.Fail (AppError.toMessage e)
        finally
           match cleanUpAccountId idToCleanUp_1 with
           | Ok () -> ()
           | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account Deactivate fails with invalid code`` () =
        let badAccountCode = "BatS**t"
        let now = Calendar.today()
        let activeEnd = now.PlusDays(-1)
        let railroad = result {
           let! payload = { code = badAccountCode; activeEnd = Some activeEnd } |> toJson<AccountDeactivationInput>
           do! match routeUiCommandForTesting "Account" "Deactivate" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (InterfaceBridgeConversionFailure _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
           return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-4.8 Account UpdateName happy path`` () =
        let code = "AC-4.8"
        let newName = "He's got the monkeys, let's see the monkeys"
        let mutable idToCleanUp_1 = None
        try
           let railroad = result {
              let! accountId = createTestAccountFromCodeString code
              idToCleanUp_1 <- Some accountId
              let! payload = { code = code; newName = newName } |> toJson<AccountUpdateNameInput>
              let! returnPayload = routeUiCommandForTesting "Account" "UpdateName" [] payload
              let! accountReturn = returnPayload |> fromJson<AccountReturn>
              Assert.Equal(newName, accountReturn.name)
              return ()
           }
           match railroad with
           | Ok _ -> ()
           | Error e -> Assert.Fail (AppError.toMessage e)
        finally
           match cleanUpAccountId idToCleanUp_1 with
           | Ok () -> ()
           | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account UpdateName fails with invalid code`` () =
        let badAccountCode = "ApeS**t"
        let newName = "I picked the wrong day to quit sniffing glue"
        let railroad = result {
           let! payload = { code = badAccountCode; newName = newName } |> toJson<AccountUpdateNameInput>
           do! match routeUiCommandForTesting "Account" "UpdateName" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (InterfaceBridgeConversionFailure _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
           return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-AC-4.9 Account UpdateExternalReference happy path`` () =
        let code = "AC-4.9"
        let newReference = Some "Genuflect, show some respect"
        let mutable idToCleanUp_1 = None
        try
           let railroad = result {
              let! accountId = createTestAccountFromCodeString code
              idToCleanUp_1 <- Some accountId
              let! payload = { code = code; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
              let! returnPayload = routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload
              let! accountReturn = returnPayload |> fromJson<AccountReturn>
              Assert.Equal(newReference, accountReturn.reference)
              return ()
           }
           match railroad with
           | Ok _ -> ()
           | Error e -> Assert.Fail (AppError.toMessage e)
        finally
           match cleanUpAccountId idToCleanUp_1 with
           | Ok () -> ()
           | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-NGUI-1.5 Account UpdateExternalReference fails with invalid code`` () =
        let badAccountCode = "DogS**t"
        let newReference = Some "I'm not bad; I'm just drawn that way"
        let railroad = result {
           let! payload = { code = badAccountCode; newReference = newReference } |> toJson<AccountUpdateExternalReferenceInput>
           do! match routeUiCommandForTesting "Account" "UpdateExternalReference" [] payload with
                | Ok _ -> Error(TestingError "Expected failure; returned success.")
                | Error (InterfaceBridgeConversionFailure _) -> Ok ()
                | Error e -> Error(TestingError $"Wrong error type: {AppError.toMessage e}")
           return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    // =============================================================================
    // FetchActivity route
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.9 FetchActivity route returns enriched activity for an account`` () =
        let expected = 4
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
           let! returnPayload = routeUiCommandForTesting "Account" "FetchActivity" [] payload
           let! returned = fromJson<AccountActivityReturn list> returnPayload
           let actual = returned |> List.length
           Assert.Equal(expected, actual)
           return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    // =============================================================================
    // FetchBalances route
    // =============================================================================

    [<Fact>]
    member _.``REQ-JE-3.6 FetchBalances route returns balances for given account codes`` () =
        let railroad = result {
           let input : AccountBalanceFetchByAccountListInput = { codes = ["F-2210"; "F-5350"]; asOf = None }
           let! payload = input |> toJson<AccountBalanceFetchByAccountListInput>
           let! returnPayload = routeUiCommandForTesting "Account" "FetchBalances" [] payload
           let! returned = fromJson<AccountBalanceReturn list> returnPayload
           Assert.Equal(2, returned |> List.length)
           return ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)
