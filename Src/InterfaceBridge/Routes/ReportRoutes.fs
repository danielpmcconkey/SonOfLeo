module InterfaceBridge.Routes.ReportRoutes

open System
open DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.ReportConverters
open InterfaceBridge.CommandRoute
open Context
open InterfaceBridge.InterfaceContracts.ReportsContracts
open InterfaceBridge.Json
open Logger.Audit
open ModelOrchestrator.TrialBalanceReport
open Utilities.ResultHelper



let private trialBalance payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<TrialBalanceInput> payload
        let! trialBalanceData = fetchTrialBalanceData context input.asOf.asOf
        let trialBalanceRows =
            trialBalanceData
            |> ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
        let (trialBalanceReturn:TrialBalanceReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly -> (TrialBalanceReturn.DataOnly trialBalanceRows) 
            | OutputSpecifier.Report outputPathInput -> 
                let dateInterpolation = if outputPathInput.interpolateAsOf then $"{input.asOf.asOf}" else ""
                let fullPath = $"{outputPathInput.baseDir}/{outputPathInput.fileName}{dateInterpolation}.html"
                let outputPathReturn = { fullyQualifiedPath = fullPath}
                raise (NotImplementedException "report generator not yet built.")
                (TrialBalanceReturn.Report outputPathReturn)
        return! trialBalanceReturn |> Json.toJson<TrialBalanceReturn>
    }
    
let reportingRoutes: ReportRoute list =
    [
        { name = "TrialBalance"
          description = "Create a new account and insert it into the database"
          inputContract = typeof<TrialBalanceInput>.Name
          outputContract = typeof<TrialBalanceReturn>.Name
          handler = trialBalance }
    ]
