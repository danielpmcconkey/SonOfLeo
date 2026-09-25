module Ui.InterfaceBridge.Routes.ReportRoutes

open App.Utility.Json
open App.Utility.Result
open App.DataAccessLayer.DbTransaction
open App.Operation.Audit
open App.Session
open Business.CrossDomainOrchestration.TrialBalanceReport
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.BoundaryConverters.ReportConverters
open Ui.InterfaceBridge.ReportWriters
open Ui.InterfaceBridge.CommandRoute

let private trialBalance payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<TrialBalanceReportInput> payload
        let! trialBalanceData = fetchTrialBalanceData context input.asOf.asOf
        let trialBalanceRows =
            trialBalanceData
            |> ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
        let! (trialBalanceReturn:TrialBalanceReportReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly -> Ok (TrialBalanceReportReturn.DataOnly trialBalanceRows) 
            | OutputSpecifier.Report outputPathInput ->
                trialBalanceData |> TrialBalanceWriter.write outputPathInput input.asOf.asOf
        return! trialBalanceReturn |> Json.toJson<TrialBalanceReportReturn>
    }
    
let reportingRoutes: ReportRoute list =
    [
        { name = "TrialBalance"
          description = "If data only, returns a sorted list of accounts, with their debits, credits, and net balances. Child debits, credits, and balances roll up to their parents. If Report, it creates a trial balance report and returns the full file path to it."
          inputContract = typeof<TrialBalanceReportInput>.Name
          outputContract = typeof<TrialBalanceReportReturn>.Name
          handler = trialBalance }
    ]
