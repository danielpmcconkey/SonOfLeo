module Tests.Integrated.InterfaceBridge.FiscalPeriodRoutes

open InterfaceBridge.Json.Json
open Model
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.InterfaceBridge._routeResolver
open Utilities.AppError
open Utilities.ResultHelper
open Xunit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Tests.Integrated
open Tests.Integrated._Cleanup
open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts

[<Collection("SharedTestData")>]
type FiscalPeriodRouteTests(fixture: TestDataFixture) =

    static let createFiscalPeriodInputPayload keyToUse =
        { FiscalPeriodInput.periodKey = keyToUse }
        |> toJson<FiscalPeriodInput> |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

    static let createFiscalPeriodFetchAllInputPayload openOnly =
        { openOnly = openOnly }
        |> toJson<FiscalPeriodFetchAllInput> |> Result.defaultWith (fun e -> failwith (AppError.toMessage e))

     // =============================================================================
     // Create
     // =============================================================================

    [<Fact>]
    member _.``REQ-FP-2.4 FiscalPeriod Create happy path`` () =
        let expected = "1993-06"
        let payload = createFiscalPeriodInputPayload expected

        let mutable keyToCleanUp = None
        try
            let railroad = result {
                let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "Create" [] payload
                let! fp = fromJson<FiscalPeriodReturn> resultPayload
                let returnedKey = fp.periodKey
                let! uuid = returnedKey |> LookupCache.fiscalPeriodKeyToId.fetch
                let id = uuid |> FiscalPeriodId.fromGuid
                keyToCleanUp <- Some returnedKey
                Assert.Equal(expected, returnedKey)
                let! fetched = id |> fetchById None
                Assert.Equal(expected, FiscalPeriodKey.value (periodKey fetched))
                () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

     // =============================================================================
     // Read
     // =============================================================================

    [<Fact>]
    member _.``REQ-FP-3.2 FiscalPeriod FetchByKey happy path`` () =
        let railroad = result {
            let! existingPeriod = fetchById None (fixture.Data.openFiscalPeriodIds |> List.head)
            let existingKey = existingPeriod |> periodKey |> FiscalPeriodKey.value
            let payload = createFiscalPeriodInputPayload existingKey
            let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "FetchByKey" [] payload
            let! returned = fromJson<FiscalPeriodReturn> resultPayload
            Assert.Equal(existingKey, returned.periodKey)
            () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-FP-3.4 FiscalPeriod FetchAll happy path`` () =
        let payload = createFiscalPeriodFetchAllInputPayload false

        let railroad = result {
            let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "FetchAll" [] payload
            let! returned = fromJson<FiscalPeriodReturn list> resultPayload
            Assert.True(returned |> List.length >= 9)
            () }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail (AppError.toMessage e)

     // =============================================================================
     // Update
     // =============================================================================

    [<Fact>]
    member _.``REQ-FP-4.1 FiscalPeriod Close happy path`` () =
        let expected = "1992-05"
        let payload = createFiscalPeriodInputPayload expected

        let mutable keyToCleanUp = None
        try
            let railroad = result {
                let! created = createTestFiscalPeriodFromPrimitives None expected
                let keyString = periodKey created |> FiscalPeriodKey.value
                keyToCleanUp <- Some keyString
                let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "Close" [] payload
                let! returned = fromJson<FiscalPeriodReturn> resultPayload
                let returnedKey = returned.periodKey
                Assert.Equal(expected, returnedKey)
                Assert.False(returned.isOpen)
                () }
            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)

    [<Fact>]
    member _.``REQ-FP-4.2 FiscalPeriod Reopen happy path`` () =
        let expected = "2048-11"
        let payload = createFiscalPeriodInputPayload expected

        let mutable keyToCleanUp = None
        try
            let railroad = result {
                let! created = createTestFiscalPeriodFromPrimitives None expected
                let id = created |> fiscalPeriodId
                let keyString = periodKey created |> FiscalPeriodKey.value
                keyToCleanUp <- Some keyString

                let! closed = closeFiscalPeriod id genericAuditEnvelope None
                Assert.False(isOpen closed)

                let! resultPayload = routeUiCommandForTesting "FiscalPeriod" "Reopen" [] payload
                let! returned = fromJson<FiscalPeriodReturn> resultPayload
                let returnedKey = returned.periodKey
                Assert.Equal(expected, returnedKey)
                Assert.True(returned.isOpen)
                ()
            }

            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail (AppError.toMessage e)
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok () -> ()
            | Error e -> failwith (AppError.toMessage e)


