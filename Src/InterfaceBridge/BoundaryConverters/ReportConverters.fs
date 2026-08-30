module InterfaceBridge.BoundaryConverters.ReportConverters

open InterfaceBridge.InterfaceContracts.ReportsContracts
open Model
open Model.Ledger.AccountComponent
open ModelOrchestrator.TrialBalanceReport

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
