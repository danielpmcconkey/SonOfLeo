module Tests.Integrated.SonOfLeoCli.FiscalPeriodRoutes

open Model.Audit
open Xunit
open Utilities.ResultCE
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Model.UI.InterfaceContractTypes
open Model.UI.Json
open Tests.Integrated.GenericTestProperties
open Tests.Integrated.SonOfLeoCli.CliExecutor
open Tests.Integrated._Cleanup

let private createFiscalPeriodInputPayload keyToUse =
    { periodKey = keyToUse }
    |> toJson<FiscalPeriodInput> |> Result.defaultWith failwith

let private createFiscalPeriodFetchAllInputPayload openOnly =
    { openOnly = openOnly }
    |> toJson<FiscalPeriodFetchAllInput> |> Result.defaultWith failwith
    
    
    

/// createFiscalPeriodInDb is used to quickly stage records for testing RUD functions
let private createFiscalPeriodInDb (keyToUse: string) : Result<FiscalPeriod, string> =
    let envelope = AuditEnvelope.create FiscalPeriodCreate
    constructNewAndSaveToDb keyToUse envelope None


// =============================================================================
// Create
// =============================================================================

[<Fact>]
let ``REQ-FP-2.4 FiscalPeriod Create happy path`` () =
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
            keyToCleanUp <- Some returnedKey
            Assert.Equal(expected, returnedKey) // this validates that what came back was what we expected
            // now try to re-fetch it to make sure it made the full round-trip
            let! fetched = fetchByKey None expected
            Assert.Equal(expected, PeriodKey.value (periodKey fetched))
            ()
        }

        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodKey keyToCleanUp  with
        | Ok () -> ()
        | Error e -> failwith e

// =============================================================================
// Read
// =============================================================================

[<Fact>]
let ``REQ-FP-3.2 FiscalPeriod FetchByKey happy path`` () =
    let expected = "1997-03"
    let payload = createFiscalPeriodInputPayload expected
    let args = ["FiscalPeriod"; "FetchByKey"]
    
    let mutable keyToCleanUp = None
    try
        let railroad = result {
            let! created = createFiscalPeriodInDb expected
            let keyString = periodKey created |> PeriodKey.value
            keyToCleanUp <- Some keyString
            
            let returnCode, resultsPayload, e = runCli args payload
            do! if returnCode <> 0 then Error $"FiscalPeriod FetchByKey happy path returned a non-zero value: {e}" else Ok ()
            let! returned = fromJson<FiscalPeriodReturn> resultsPayload
            let returnedKey = returned.periodKey
            Assert.Equal(expected, returnedKey)
            ()
        }
        
        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodKey keyToCleanUp  with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-3.4 FiscalPeriod FetchAll happy path`` () =
    let explicitKey_1 = "2028-10"
    let explicitKey_2 = "2028-11"
    let explicitKey_3 = "2028-12"
    let explicitKey_4 = "2029-01"
    let payload = createFiscalPeriodFetchAllInputPayload false
    let args = ["FiscalPeriod"; "FetchAll"]
    let mutable keyToCleanUp_1 = None
    let mutable keyToCleanUp_2 = None
    let mutable keyToCleanUp_3 = None
    let mutable keyToCleanUp_4 = None
    try
        let railroad = result {
            let! fp_1 = constructNewAndSaveToDb explicitKey_1 genericAuditEnvelope None
            let key1 = fp_1 |> periodKey |> PeriodKey.value
            keyToCleanUp_1 <- Some key1
            
            let! fp_2 = constructNewAndSaveToDb explicitKey_2 genericAuditEnvelope None
            let key2 = fp_2 |> periodKey |> PeriodKey.value
            keyToCleanUp_2 <- Some key2
            
            let! fp_3 = constructNewAndSaveToDb explicitKey_3 genericAuditEnvelope None
            let key3 = fp_3 |> periodKey |> PeriodKey.value
            keyToCleanUp_3 <- Some key3
            
            let! fp_4 = constructNewAndSaveToDb explicitKey_4 genericAuditEnvelope None
            let key4 = fp_4 |> periodKey |> PeriodKey.value
            keyToCleanUp_4 <- Some key4
            
            let returnCode, resultsPayload, e = runCli args payload
            do! if returnCode <> 0 then Error $"FiscalPeriod FetchAll happy path returned a non-zero value: {e}" else Ok ()
            let! returned = fromJson<FiscalPeriodReturn list> resultsPayload

            Assert.Equal(4, returned |> List.length)
            ()
        }

        match railroad with
        | Ok _ -> ()
        | Error e -> Assert.Fail e
    finally
        match cleanUpFiscalPeriodKeysList [keyToCleanUp_1;keyToCleanUp_2;keyToCleanUp_3;keyToCleanUp_4;]  with
        | Ok () -> ()
        | Error e -> failwith e

// =============================================================================
// Update
// =============================================================================

[<Fact>]
let ``REQ-FP-4.1 FiscalPeriod Close happy path`` () =
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
        match cleanUpFiscalPeriodKey keyToCleanUp  with
        | Ok () -> ()
        | Error e -> failwith e

[<Fact>]
let ``REQ-FP-4.2 FiscalPeriod Reopen happy path`` () =
    let expected = "2048-11"
    let payload = createFiscalPeriodInputPayload expected
    let args = ["FiscalPeriod"; "Reopen"]

    let mutable keyToCleanUp = None
    try
        let railroad = result {
            let! created = createFiscalPeriodInDb expected
            let keyString = periodKey created |> PeriodKey.value
            keyToCleanUp <- Some keyString

            let! closed = closeFiscalPeriod expected genericAuditEnvelope None
            Assert.False(isOpen closed) // make sure it is, indeed, closed

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
        match cleanUpFiscalPeriodKey keyToCleanUp  with
        | Ok () -> ()
        | Error e -> failwith e
        
    