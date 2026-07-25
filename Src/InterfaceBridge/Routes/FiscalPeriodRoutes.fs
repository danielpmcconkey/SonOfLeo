module InterfaceBridge.Routes.FiscalPeriodRoutes

open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters
open InterfaceBridge.Json
open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open InterfaceBridge.CommandRoute
open Utilities.ResultHelper
open ModelOrchestrator.FiscalPeriodCreation

let private create payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create FiscalPeriodCreate
        let! fiscalPeriodKey = input.periodKey |> FiscalPeriodKey.fromString
        let! model = constructNewAndSaveToDb fiscalPeriodKey envelope None
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private fetch payload _ = // REQ-FP-3.2
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! id = input.periodKey |> ``convert FiscalPeriodKeyString to FiscalPeriodId``
        let! model = id |> fetchById None
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private fetchAll payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodFetchAllInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let! models = fetchAll None input.openOnly
        let returnVal = models |> List.map ``convert FiscalPeriod to FiscalPeriodReturn``
        return! Json.toJson<FiscalPeriodReturn list> returnVal
    } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private close payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create FiscalPeriodClose
        let! id = input.periodKey |> ``convert FiscalPeriodKeyString to FiscalPeriodId``
        let! model = closeFiscalPeriod id envelope None
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    } // REQ-NGUI-2.4, REQ-NGUI-3.5

let private reopen payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload // REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create FiscalPeriodReopen
        let! id = input.periodKey |> ``convert FiscalPeriodKeyString to FiscalPeriodId``
        let! model = reopenFiscalPeriod id envelope None
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    } // REQ-NGUI-2.4, REQ-NGUI-3.5

let fiscalPeriodDomainCommandRoutes =
    [
      // create
      { domain = "FiscalPeriod"
        verb = "Create"
        description = "Create a new fiscal period and insert it into the database."
        inputType = typeof<FiscalPeriodInput>.Name
        outputType = typeof<FiscalPeriodReturn>.Name
        handler = create }
      // read
      { domain = "FiscalPeriod"
        verb = "FetchByKey"
        description = "Retrieve a specific fiscal period from the database."
        inputType = typeof<FiscalPeriodInput>.Name
        outputType = typeof<FiscalPeriodReturn>.Name
        handler = fetch }
      { domain = "FiscalPeriod"
        verb = "FetchAll"
        description =
          "Retrieve all fiscal periods from the database with a flag to denote whether the caller only wants open periods."
        inputType = typeof<FiscalPeriodFetchAllInput>.Name
        outputType = typeof<FiscalPeriodReturn list>.Name
        handler = fetchAll }
      // update
      { domain = "FiscalPeriod"
        verb = "Close"
        description = "Closes an existing open fiscal period."
        inputType = typeof<FiscalPeriodInput>.Name
        outputType = typeof<FiscalPeriodReturn>.Name
        handler = close }
      { domain = "FiscalPeriod"
        verb = "Reopen"
        description = "Reopens an existing closed fiscal period."
        inputType = typeof<FiscalPeriodInput>.Name
        outputType = typeof<FiscalPeriodReturn>.Name
        handler = reopen } ]
