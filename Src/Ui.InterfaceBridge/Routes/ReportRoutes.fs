module Ui.InterfaceBridge.Routes.ReportRoutes

open App.Utility.Json
open App.Utility.Result
open App.DataAccessLayer.DbTransaction
open App.Operation.CoreAuditableAction
open App.Session
open Business.CrossDomainOrchestration.TrialBalanceReport
open Business.CrossDomainOrchestration.PrePostingReview
open App.Utility
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
    
let private prePostingReview payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<PrePostingReviewInput> payload
        let! entries = fetchPrePostingReview context
        let! (prePostingReviewReturn: PrePostingReviewReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly ->
                Ok (PrePostingReviewReturn.DataOnly (entries |> ``convert [PrePostingEntry list] to [PrePostingEntryReturnRow list]``))
            | OutputSpecifier.Report outputPathInput ->
                entries |> PrePostingReviewWriter.write outputPathInput (Calendar.today())
        return! prePostingReviewReturn |> Json.toJson<PrePostingReviewReturn>
    }

let reportingRoutes: ReportRoute list =
    [
        { name = "TrialBalance"
          description = "If data only, returns a sorted list of accounts, with their debits, credits, and net balances. Child debits, credits, and balances roll up to their parents. If Report, it creates a trial balance report and returns the full file path to it."
          inputContract = typeof<TrialBalanceReportInput>.Name
          outputContract = typeof<TrialBalanceReportReturn>.Name
          handler = trialBalance }
        { name = "PrePostingReview"
          description = "Every staged entry the next batch post would post (status Classified or Reviewed), line by line: account, the classification rule that assigned it, and any linked payment agreement with the Payments, Invoices and Instances behind it. Run it after shadow post and reconciliation come back clean. A line with no account fails it. If data only, returns the entries; if Report, writes the review and returns the full file path to it."
          inputContract = typeof<PrePostingReviewInput>.Name
          outputContract = typeof<PrePostingReviewReturn>.Name
          handler = prePostingReview }
    ]
