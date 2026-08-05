module Tests.Integrated.InterfaceBridge.FiscalPeriodRoutes

open DataAccessLayer.DbTransaction
open InterfaceBridge.Json.Json
open Logger.Audit
open Model
open Tests.Helpers.EntityFunctions
open Tests.Helpers
open Tests.Helpers.Railroad
open Tests.Helpers.RouteResolver
open Utilities.AppError
open Tests.Helpers.SadPath
open Utilities.ResultHelper
open Xunit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Tests.Integrated
open Tests.Helpers.Cleanup
open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open Context.Context

[<Collection("SharedTestData")>]
type FiscalPeriodRouteTests(fixture: TestDataFixture) =

    static let createFiscalPeriodCreateInputPayload keyToUse =
        { FiscalPeriodCreateInput.periodKey = keyToUse }
        |> toJson<FiscalPeriodCreateInput>
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    static let createFiscalPeriodFetchByKeyInputPayload keyToUse =
        { FiscalPeriodFetchByKeyInput.periodKey = keyToUse }
        |> toJson<FiscalPeriodFetchByKeyInput>
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    static let createFiscalPeriodCloseInputPayload keyToUse =
        { FiscalPeriodCloseInput.periodKey = keyToUse }
        |> toJson<FiscalPeriodCloseInput>
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    static let createFiscalPeriodReopenInputPayload keyToUse =
        { FiscalPeriodReopenInput.periodKey = keyToUse }
        |> toJson<FiscalPeriodReopenInput>
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    static let createFiscalPeriodFetchAllInputPayload openOnly =
        { openOnly = openOnly }
        |> toJson<FiscalPeriodFetchAllInput>
        |> Result.defaultWith(fun e -> failwith(AppError.toMessage e))

    [<Fact>]
    member _.``REQ-FP-2.4 FiscalPeriod Create happy path``() =
        let expected = "1993-06"
        let payload = createFiscalPeriodCreateInputPayload expected
        let mutable keyToCleanUp = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "Create" [] payload
                let! fp = fromJson<FiscalPeriodReturn> resultPayload
                let returnedKey = fp.periodKey
                let! uuid = returnedKey |> LookupCache.fiscalPeriodKeyToId.fetch context
                let id = uuid |> FiscalPeriodId.fromGuid
                keyToCleanUp <- Some returnedKey
                Assert.Equal(expected, returnedKey)
                let! fetched = id |> fetchById context
                Assert.Equal(expected, FiscalPeriodKey.value(periodKey fetched))
                ()
            }
            |> railroadWrapper
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok() -> ()
            | Error e -> failwith(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-FP-3.2 FiscalPeriod FetchByKey happy path``() =
        let context = create NoTransaction FetchOnly
        result {
            let! existingPeriod = fetchById context (fixture.Data.openFiscalPeriodIds |> List.head)
            let existingKey = existingPeriod |> periodKey |> FiscalPeriodKey.value
            let payload = createFiscalPeriodFetchByKeyInputPayload existingKey
            let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "FetchByKey" [] payload
            let! returned = fromJson<FiscalPeriodReturn> resultPayload
            Assert.Equal(existingKey, returned.periodKey)
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-3.4 FiscalPeriod FetchAll happy path``() =
        let payload = createFiscalPeriodFetchAllInputPayload false
        result {
            let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "FetchAll" [] payload
            let! returned = fromJson<FiscalPeriodReturn list> resultPayload
            Assert.True(returned |> List.length >= 9)
            ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.1 FiscalPeriod Close happy path``() =
        let expected = "1992-05"
        let payload = createFiscalPeriodCloseInputPayload expected
        let mutable keyToCleanUp = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! created = createTestFiscalPeriodFromPrimitives context expected
                let keyString = periodKey created |> FiscalPeriodKey.value
                keyToCleanUp <- Some keyString
                let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "Close" [] payload
                let! returned = fromJson<FiscalPeriodReturn> resultPayload
                let returnedKey = returned.periodKey
                Assert.Equal(expected, returnedKey)
                Assert.False(returned.isOpen)
                ()
            }
            |> railroadWrapper
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok() -> ()
            | Error e -> failwith(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-FP-4.2 FiscalPeriod Reopen happy path``() =
        let expected = "2048-11"
        let payload = createFiscalPeriodReopenInputPayload expected
        let mutable keyToCleanUp = None
        try
            let context = create NoTransaction FetchOnly
            result {
                let! created = createTestFiscalPeriodFromPrimitives context expected
                let id = created |> fiscalPeriodId
                let keyString = periodKey created |> FiscalPeriodKey.value
                keyToCleanUp <- Some keyString
                let! closed = id |> closeFiscalPeriod context
                Assert.False(isOpen closed)
                let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "Reopen" [] payload
                let! returned = fromJson<FiscalPeriodReturn> resultPayload
                let returnedKey = returned.periodKey
                Assert.Equal(expected, returnedKey)
                Assert.True(returned.isOpen)
                ()
            }
            |> railroadWrapper
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok() -> ()
            | Error e -> failwith(AppError.toMessage e)

    [<Fact>]
    member _.``REQ-FP-3.2 FetchByKey rejects non-existent period key``() =
        let payload = createFiscalPeriodFetchByKeyInputPayload "1850-01"
        result {
            do!
                isCorrectError
                    (routeUiCommandForTesting "FiscalPeriod" "FetchByKey" [] payload)
                    FiscalPeriodNoPeriodMatchingKey
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.1 Close rejects non-existent period key``() =
        let payload = createFiscalPeriodCloseInputPayload "1850-01"
        result {
            do!
                isCorrectError
                    (routeUiCommandForTesting "FiscalPeriod" "Close" [] payload)
                    FiscalPeriodNoPeriodMatchingKey
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-4.2 Reopen rejects non-existent period key``() =
        let payload = createFiscalPeriodReopenInputPayload "1850-01"
        result {
            do!
                isCorrectError
                    (routeUiCommandForTesting "FiscalPeriod" "Reopen" [] payload)
                    FiscalPeriodNoPeriodMatchingKey
                    None
            return ()
        }
        |> railroadWrapper

    [<Fact>]
    member _.``REQ-FP-2.4 Fiscal Period Create rejects invalid period key string``() =
        let payload = createFiscalPeriodCreateInputPayload "abc"
        result {
            do!
                isCorrectError
                    (routeUiCommandForTesting "FiscalPeriod" "Create" [] payload)
                    FiscalPeriodInvalidKeyString
                    None
            return ()
        }
        |> railroadWrapper
