module Ui.InterfaceBridge.BoundaryConverters.ReportConverters

open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.CrossDomainOrchestration.TrialBalanceReport
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

let ``convert [TrialBalanceRowFlattened] to [TrialBalanceReturnRow]``
    (flattenedRow: TrialBalanceRowFlattened)
    : TrialBalanceReturnRow = {
        accountCode = flattenedRow.accountCode |> AccountCode.value
        accountName = flattenedRow.accountName |> AccountName.value
        generation = flattenedRow.generation
        totalCredits = flattenedRow.totalCredits |> Money.amount
        totalDebits = flattenedRow.totalDebits |> Money.amount
        netBalance = flattenedRow.netBalance |> Money.amount
    }
    
let ``convert [TrialBalanceRowFlattened list] to [TrialBalanceReturnRow list]``
    (flattenedRows: TrialBalanceRowFlattened list)
    : TrialBalanceReturnRow list =
    flattenedRows |> List.map ``convert [TrialBalanceRowFlattened] to [TrialBalanceReturnRow]``
