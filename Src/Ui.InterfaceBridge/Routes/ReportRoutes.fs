module Ui.InterfaceBridge.Routes.ReportRoutes

open App.DataAccessLayer.DbTransaction
open Ui.InterfaceBridge.BoundaryConverters.ReportConverters
open Ui.InterfaceBridge.CommandRoute
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open App.Utility.Json
open Ui.InterfaceBridge.ReportWriters
open App.Operation.Audit
open Business.FinancialServices.TrialBalanceReport
open App.Utility.Result
open App.Session

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
