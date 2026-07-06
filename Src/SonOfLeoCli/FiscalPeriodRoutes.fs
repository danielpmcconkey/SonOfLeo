module SonOfLeoCli.FiscalPeriodRoutes

open Model
open Model.Audit
open Model.Ledger.FiscalPeriods
open Model.Ledger.FiscalPeriods.FiscalPeriod
open Model.UI
open Utilities.ResultCE
open InterfaceContractTypes

let private convertModelToReturn fp : FiscalPeriodReturn = {
            periodKey = PeriodKey.value (periodKey fp)
            startDate = startDate fp
            endDate = endDate fp
            isOpen = isOpen fp
            createdAt = createdAt fp
            modifiedAt = modifiedAt fp
        }

let private create payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create FiscalPeriodCreate
        let! model = constructNewAndSaveToDb input.periodKey envelope None
        let returnVal  = convertModelToReturn model
        return! Json.toJson<FiscalPeriodReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private fetch payload _ = // REQ-FP-3.2
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! id =
            input.periodKey
            |> LookupCache.fiscalPeriodKeyToId.fetch
            |> Result.mapError(fun e -> $"Period key provided didn't match any recorded Fiscal Periods in the database. Further details: {e}") // REQ-NGUI-1.5
        let! model = id |> fetchById None 
        let returnVal  = convertModelToReturn model
        return! Json.toJson<FiscalPeriodReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private fetchAll payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodFetchAllInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let! models = fetchAll None input.openOnly
        let returnVal  = models |> List.map convertModelToReturn
        return! Json.toJson<FiscalPeriodReturn list> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private close payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create FiscalPeriodClose
        let! id =
            input.periodKey
            |> LookupCache.fiscalPeriodKeyToId.fetch
            |> Result.mapError(fun e -> $"Period key provided didn't match any recorded Fiscal Periods in the database. Further details: {e}") // REQ-NGUI-1.5
        let! model = closeFiscalPeriod id envelope None
        let returnVal  = convertModelToReturn model
        return! Json.toJson<FiscalPeriodReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }

let private reopen payload _ =
    result {
        let! input = Json.fromJson<FiscalPeriodInput> payload// REQ-NGUI-2.4, REQ-NGUI-3.5
        let envelope = AuditEnvelope.create FiscalPeriodReopen
        let! id =
            input.periodKey
            |> LookupCache.fiscalPeriodKeyToId.fetch
            |> Result.mapError(fun e -> $"Period key provided didn't match any recorded Fiscal Periods in the database. Further details: {e}") // REQ-NGUI-1.5
        let! model = reopenFiscalPeriod id envelope None
        let returnVal  = convertModelToReturn model
        return! Json.toJson<FiscalPeriodReturn> returnVal // REQ-NGUI-2.4, REQ-NGUI-3.5
    }
    
let fiscalPeriodDomainCommandRoutes = [
    // create
    { domain = "FiscalPeriod"; verb = "Create"; description = "Create a new fiscal period and insert it into the database."
      inputType = typeof<FiscalPeriodInput>.Name; outputType = typeof<FiscalPeriodReturn>.Name; handler =  create }
    // read
    { domain = "FiscalPeriod"; verb = "FetchByKey"; description = "Retrieve a specific fiscal period from the database."
      inputType = typeof<FiscalPeriodInput>.Name; outputType = typeof<FiscalPeriodReturn>.Name; handler =  fetch }    
    { domain = "FiscalPeriod"; verb = "FetchAll"; description = "Retrieve all fiscal periods from the database with a flag to denote whether the caller only wants open periods."
      inputType = typeof<FiscalPeriodFetchAllInput>.Name; outputType = typeof<FiscalPeriodReturn list>.Name; handler =  fetchAll }
    // update
    { domain = "FiscalPeriod"; verb = "Close"; description = "Closes an existing open fiscal period."
      inputType = typeof<FiscalPeriodInput>.Name; outputType = typeof<FiscalPeriodReturn>.Name; handler =  close }
    { domain = "FiscalPeriod"; verb = "Reopen"; description = "Reopens an existing closed fiscal period."
      inputType = typeof<FiscalPeriodInput>.Name; outputType = typeof<FiscalPeriodReturn>.Name; handler =  reopen }
]
