module Ui.InterfaceBridge.Routes.ReportRoutes

open App.Utility
open App.Utility.Result
open App.Utility.Json
open App.Operation.CoreAuditableAction
open App.DataAccessLayer.DbTransaction
open App.Session
open Business.CrossDomainOrchestration.TrialBalanceReport
open Business.CrossDomainOrchestration.BalanceSheetIntegrity
open Business.CrossDomainOrchestration.PeriodActivity
open Business.CrossDomainOrchestration.Reconciliation
open Business.CrossDomainOrchestration.PrePostingReview
open Business.CrossDomainOrchestration.NetWorth
open Business.CrossDomainOrchestration.InvestmentWealthHistory
open Ui.InterfaceBridge.ReportWriters
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts
open Ui.InterfaceBridge.BoundaryConverters.ReportConverters
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
                trialBalanceData |> TrialBalanceWriter.write outputPathInput (context |> Context.getInitiationInstant) input.asOf.asOf
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
                let initiationInstant = context |> Context.getInitiationInstant
                let runDate = initiationInstant |> Calendar.dateFromInstant
                entries |> PrePostingReviewWriter.write outputPathInput initiationInstant runDate
        return! prePostingReviewReturn |> Json.toJson<PrePostingReviewReturn>
    }

let private reconciliation payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<ReconciliationInput> payload
        let! requests = input |> ``convert [ReconciliationInput] to [(AccountId * Money * LocalDate) list]`` context
        let! rows = requests |> reconcile context
        return!
            rows
            |> List.map ``convert [ReconciliationRow] to [ReconciliationReturnRow]``
            |> Json.toJson<ReconciliationReturnRow list>
    }

let private balanceSheetIntegrity payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<BalanceSheetIntegrityInput> payload
        let! integrity = computeBalanceSheetIntegrity context input.asOf.asOf
        let! (integrityReturn: BalanceSheetIntegrityReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly ->
                Ok (BalanceSheetIntegrityReturn.DataOnly (integrity |> ``convert [BalanceSheetIntegrity] to [BalanceSheetIntegrityReturnRow]``))
            | OutputSpecifier.Report outputPathInput -> integrity |> BalanceSheetIntegrityWriter.write outputPathInput (context |> Context.getInitiationInstant)
        return! integrityReturn |> Json.toJson<BalanceSheetIntegrityReturn>
    }

let private periodActivity payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<PeriodActivityInput> payload
        let! accounts = fetchPeriodActivity context input.beginDate input.endDate
        let! (periodActivityReturn: PeriodActivityReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly ->
                Ok (PeriodActivityReturn.DataOnly (accounts |> List.map ``convert [PeriodActivityAccount] to [PeriodActivityAccountReturnRow]``))
            | OutputSpecifier.Report outputPathInput ->
                accounts |> PeriodActivityWriter.write outputPathInput (context |> Context.getInitiationInstant) input.beginDate input.endDate
        return! periodActivityReturn |> Json.toJson<PeriodActivityReturn>
    }

let private netWorth payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<NetWorthInput> payload
        let! computed = computeNetWorth context input.asOf.asOf
        let! (netWorthReturn: NetWorthReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly -> Ok(NetWorthReturn.DataOnly(computed |> ``convert [NetWorth] to [NetWorthReturnRow]``))
            | OutputSpecifier.Report outputPathInput ->
                computed |> NetWorthWriter.write outputPathInput (context |> Context.getInitiationInstant)
        return! netWorthReturn |> Json.toJson<NetWorthReturn>
    }

let private investmentWealthHistory payload _ =
    let context = Context.create NoTransaction FetchOnly
    result {
        let! input = Json.fromJson<InvestmentWealthHistoryInput> payload
        let! grouping = input.grouping |> WealthGrouping.fromString
        let! points = computeInvestmentWealthHistory context input.beginDate input.endDate grouping
        let! (historyReturn: InvestmentWealthHistoryReturn) =
            match input.reportOutput with
            | OutputSpecifier.DataOnly ->
                Ok(InvestmentWealthHistoryReturn.DataOnly(points |> List.map ``convert [WealthPoint] to [WealthPointReturnRow]``))
            | OutputSpecifier.Report outputPathInput ->
                points
                |> InvestmentWealthHistoryWriter.write
                    outputPathInput (context |> Context.getInitiationInstant) input.beginDate input.endDate grouping
        return! historyReturn |> Json.toJson<InvestmentWealthHistoryReturn>
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
        { name = "Reconciliation"
          description = "Data only. For each (account code, external balance, as-of date) row, returns the account's name, its ledger net balance as of that row's date by the trial balance rules, and the delta (external minus ledger). External balances are given in the account's normal-balance direction. An unknown or repeated account code is an error; a non-zero delta is not."
          inputContract = typeof<ReconciliationInput>.Name
          outputContract = typeof<ReconciliationReturnRow list>.Name
          handler = reconciliation }
        { name = "BalanceSheetIntegrity"
          description = "As of a date: total debits and total credits across every non-voided journal entry line dated on or before it, and whether they are equal; the net balance of each account type (Asset, Liability, Equity, Revenue, Expense) in its normal-balance direction; net income (revenue minus expenses); and the residual, assets minus (liabilities plus equity plus net income). Unequal totals or a non-zero residual are returned, not raised. If data only, returns those figures; if Report, writes them and returns the full file path."
          inputContract = typeof<BalanceSheetIntegrityInput>.Name
          outputContract = typeof<BalanceSheetIntegrityReturn>.Name
          handler = balanceSheetIntegrity }
        { name = "PeriodActivity"
          description = "The spending view. For a begin and end date (inclusive), every Revenue and Expense account with non-voided activity in the range, in trial balance order: code, name, net total for the range in the account's normal-balance direction, and each contributing line (entry date, journal entry ID, description, line type, amount, memo) ordered by entry date then journal entry ID. If data only, returns the accounts; if Report, writes them and returns the full file path; date interpolation appends -begin_end."
          inputContract = typeof<PeriodActivityInput>.Name
          outputContract = typeof<PeriodActivityReturn>.Name
          handler = periodActivity }
        { name = "NetWorth"
          description = "As of a date inside a fiscal period: every Asset account not linked to an Investment Account or a Property at its own ledger balance, every Investment Account at the market value of its latest snapshot on or before the date, every Property owned on the date at its value with its mortgage accounts and equity, and every other Liability account; then the totals, net worth, investable wealth (net worth less the primary residence's equity), and investments totalled by tax treatment and by account group. If data only, returns those figures; if Report, writes them and returns the full file path."
          inputContract = typeof<NetWorthInput>.Name
          outputContract = typeof<NetWorthReturn>.Name
          handler = netWorth }
        { name = "InvestmentWealthHistory"
          description = "For every month-end from begin to end (inclusive), the market value of the holdings as of that date, totalled by the grouping given (Account, AccountGroup, TaxTreatment, Owners, or one of the seven dimensions) and in all; lines with no value in a dimension are totalled as unassigned. Not limited to fiscal periods. If data only, returns the points; if Report, writes a table of them and returns the full file path; date interpolation appends -begin_end."
          inputContract = typeof<InvestmentWealthHistoryInput>.Name
          outputContract = typeof<InvestmentWealthHistoryReturn>.Name
          handler = investmentWealthHistory }
    ]
