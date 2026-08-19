module InterfaceBridge.Routes.ReportRoutes

open DataAccessLayer.DbTransaction
open InterfaceBridge.BoundaryConverters.ReportConverters
open InterfaceBridge.CommandRoute
open InterfaceBridge.InterfaceContracts.ReportsContracts
open Utilities.Json
open InterfaceBridge.ReportWriters
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
        let! (trialBalanceReturn:TrialBalanceReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly -> Ok (TrialBalanceReturn.DataOnly trialBalanceRows) 
            | OutputSpecifier.Report outputPathInput ->
                trialBalanceData |> TrialBalanceWriter.write outputPathInput input.asOf.asOf
        return! trialBalanceReturn |> Json.toJson<TrialBalanceReturn>
    }
    
let reportingRoutes: ReportRoute list =
    [
        { name = "TrialBalance"
          description = "If data only, returns a sorted list of accounts, with their debits, credits, and net balances. Child debits, credits, and balances roll up to their parents. If Report, it creates a trial balance report and returns the full file path to it."
          inputContract = typeof<TrialBalanceInput>.Name
          outputContract = typeof<TrialBalanceReturn>.Name
          handler = trialBalance }
    ]
