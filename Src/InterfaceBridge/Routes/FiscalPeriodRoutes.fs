module InterfaceBridge.Routes.FiscalPeriodRoutes

open InterfaceBridge.InterfaceContracts.FiscalPeriodContracts
open InterfaceBridge.BoundaryConverters.FiscalPeriodFieldConverters
open InterfaceBridge.Json
open Logger.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open InterfaceBridge.CommandRoute
open Utilities.ResultHelper
open ModelOrchestrator.FiscalPeriodCreation
open DataAccessLayer.DbTransaction
open Context

let private create payload _ =
    let context = Context.create NoTransaction FiscalPeriodCreate
    result {
        let! input = Json.fromJson<FiscalPeriodCreateInput> payload
        let! fiscalPeriodKey = input.periodKey |> FiscalPeriodKey.fromString
        let! model = constructNewAndSaveToDb context fiscalPeriodKey
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    }

let private fetch payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FiscalPeriodFetchByKeyInput> payload
        let! id = input.periodKey |> ``convert FiscalPeriodKeyString to FiscalPeriodId`` context
        let! model = id |> fetchById context
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    }

let private fetchAll payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<FiscalPeriodFetchAllInput> payload
        let! models = fetchAll context input.openOnly
        let returnVal = models |> List.map ``convert FiscalPeriod to FiscalPeriodReturn``
        return! Json.toJson<FiscalPeriodReturn list> returnVal
    }

let private close payload _ =
    let context = Context.create NoTransaction FiscalPeriodClose
    result {
        let! input = Json.fromJson<FiscalPeriodCloseInput> payload
        let! id = input.periodKey |> ``convert FiscalPeriodKeyString to FiscalPeriodId`` context
        let! model = id |> closeFiscalPeriod context
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    }

let private reopen payload _ =
    let context = Context.create NoTransaction FiscalPeriodReopen
    result {
        let! input = Json.fromJson<FiscalPeriodReopenInput> payload
        let! id = input.periodKey |> ``convert FiscalPeriodKeyString to FiscalPeriodId`` context
        let! model = id |> reopenFiscalPeriod context
        let returnVal = ``convert FiscalPeriod to FiscalPeriodReturn`` model
        return! Json.toJson<FiscalPeriodReturn> returnVal
    }

let fiscalPeriodDomainCommandRoutes =
    [
      // create
      { domain = "FiscalPeriod"
        verb = "Create"
        description = "Create a new fiscal period and insert it into the database."
        inputContract = typeof<FiscalPeriodCreateInput>.Name
        outputContract = typeof<FiscalPeriodReturn>.Name
        handler = create }
      // read
      { domain = "FiscalPeriod"
        verb = "FetchByKey"
        description = "Retrieve a specific fiscal period from the database."
        inputContract = typeof<FiscalPeriodFetchByKeyInput>.Name
        outputContract = typeof<FiscalPeriodReturn>.Name
        handler = fetch }
      { domain = "FiscalPeriod"
        verb = "FetchAll"
        description =
          "Retrieve all fiscal periods from the database with a flag to denote whether the caller only wants open periods."
        inputContract = typeof<FiscalPeriodFetchAllInput>.Name
        outputContract = typeof<FiscalPeriodReturn list>.Name
        handler = fetchAll }
      // update
      { domain = "FiscalPeriod"
        verb = "Close"
        description = "Closes an existing open fiscal period."
        inputContract = typeof<FiscalPeriodCloseInput>.Name
        outputContract = typeof<FiscalPeriodReturn>.Name
        handler = close }
      { domain = "FiscalPeriod"
        verb = "Reopen"
        description = "Reopens an existing closed fiscal period."
        inputContract = typeof<FiscalPeriodReopenInput>.Name
        outputContract = typeof<FiscalPeriodReturn>.Name
        handler = reopen } ]
