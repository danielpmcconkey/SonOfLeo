namespace Tests.Integrated.SonOfLeoCli

open Model
open Model.Audit
open Xunit
open Utilities.ResultCE
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Model.UI.InterfaceContractTypes
open Model.UI.Json
open Tests.Integrated
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Tests.Integrated._Cleanup

[<Collection("SharedTestData")>]
type FiscalPeriodRouteTests(fixture: TestDataFixture) =

    static let createFiscalPeriodInputPayload keyToUse =
        { FiscalPeriodInput.periodKey = keyToUse }
        |> toJson<FiscalPeriodInput> |> Result.defaultWith failwith

    static let createFiscalPeriodFetchAllInputPayload openOnly =
        { openOnly = openOnly }
        |> toJson<FiscalPeriodFetchAllInput> |> Result.defaultWith failwith

    static let createFiscalPeriodInDb (keyToUse: string) : Result<FiscalPeriod, string> =
        let envelope = AuditEnvelope.create FiscalPeriodCreate
        constructNewAndSaveToDb keyToUse envelope None

    // =============================================================================
    // Create
    // =============================================================================

    [<Fact>]
    member _.``REQ-FP-2.4 FiscalPeriod Create happy path`` () =
        let expected = "1993-06"
        let payload = createFiscalPeriodInputPayload expected
        let args = ["FiscalPeriod"; "Create"]

        let mutable keyToCleanUp = None
        try
            let railroad = result {
                let returnCode, resultsPayload, e = runCli args payload
                do! if returnCode <> 0 then Error $"FiscalPeriod Create happy path returned a non-zero value: {e}" else Ok ()
                let! fp = fromJson<FiscalPeriodReturn> resultsPayload
                let returnedKey = fp.periodKey
                let! id =
                    returnedKey
                    |> LookupCache.fiscalPeriodKeyToId.fetch
                    |> Result.mapError(fun e -> $"The returned Fiscal Period Key didn't match any records in the database. Further details: {e}")
                keyToCleanUp <- Some returnedKey
                Assert.Equal(expected, returnedKey)
                let! fetched = id |> fetchById None
                Assert.Equal(expected, PeriodKey.value (periodKey fetched))
                ()
            }

            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    // =============================================================================
    // Read
    // =============================================================================

    [<Fact>]
    member _.``REQ-FP-3.2 FiscalPeriod FetchByKey happy path`` () =
        let railroad = result {
            let! existingPeriod = fetchById None (fixture.Data.fiscalPeriodIds |> List.head)
            let existingKey = existingPeriod |> periodKey |> PeriodKey.value
            let payload = createFiscalPeriodInputPayload existingKey
            let args = ["FiscalPeriod"; "FetchByKey"]

            let returnCode, resultsPayload, e = runCli args payload
            do! if returnCode <> 0 then Error $"FiscalPeriod FetchByKey happy path returned a non-zero value: {e}" else Ok ()
            let! returned = fromJson<FiscalPeriodReturn> resultsPayload
            Assert.Equal(existingKey, returned.periodKey)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    [<Fact>]
    member _.``REQ-FP-3.4 FiscalPeriod FetchAll happy path`` () =
        let payload = createFiscalPeriodFetchAllInputPayload false
        let args = ["FiscalPeriod"; "FetchAll"]

        let railroad = result {
            let returnCode, resultsPayload, e = runCli args payload
            do! if returnCode <> 0 then Error $"FiscalPeriod FetchAll happy path returned a non-zero value: {e}" else Ok ()
            let! returned = fromJson<FiscalPeriodReturn list> resultsPayload
            Assert.True(returned |> List.length >= 9)
            ()
        }
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e

    // =============================================================================
    // Update
    // =============================================================================

    [<Fact>]
    member _.``REQ-FP-4.1 FiscalPeriod Close happy path`` () =
        let expected = "1992-05"
        let payload = createFiscalPeriodInputPayload expected
        let args = ["FiscalPeriod"; "Close"]

        let mutable keyToCleanUp = None
        try
            let railroad = result {
                let! created = createFiscalPeriodInDb expected
                let keyString = periodKey created |> PeriodKey.value
                keyToCleanUp <- Some keyString

                let returnCode, resultsPayload, e = runCli args payload
                do! if returnCode <> 0 then Error $"FiscalPeriod Close happy path returned a non-zero value: {e}" else Ok ()
                let! returned = fromJson<FiscalPeriodReturn> resultsPayload
                let returnedKey = returned.periodKey
                Assert.Equal(expected, returnedKey)
                Assert.False(returned.isOpen)
                ()
            }

            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e

    [<Fact>]
    member _.``REQ-FP-4.2 FiscalPeriod Reopen happy path`` () =
        let expected = "2048-11"
        let payload = createFiscalPeriodInputPayload expected
        let args = ["FiscalPeriod"; "Reopen"]

        let mutable keyToCleanUp = None
        try
            let railroad = result {
                let! created = createFiscalPeriodInDb expected
                let id = created |> FiscalPeriod.uniqueId
                let keyString = periodKey created |> PeriodKey.value
                keyToCleanUp <- Some keyString

                let! closed = closeFiscalPeriod id genericAuditEnvelope None
                Assert.False(isOpen closed)

                let returnCode, resultsPayload, e = runCli args payload
                do! if returnCode <> 0 then Error $"FiscalPeriod Reopen happy path returned a non-zero value: {e}" else Ok ()
                let! returned = fromJson<FiscalPeriodReturn> resultsPayload
                let returnedKey = returned.periodKey
                Assert.Equal(expected, returnedKey)
                Assert.True(returned.isOpen)
                ()
            }

            match railroad with
            | Ok _ -> ()
            | Error e -> Assert.Fail e
        finally
            match cleanUpFiscalPeriodKey keyToCleanUp with
            | Ok () -> ()
            | Error e -> failwith e
