module Ui.InterfaceBridge.BoundaryConverters.ReconciliationConverters

open App.Utility.IAppError
open App.Utility.Result
open Business.FinancialServices
open Business.FinancialServices.Ledger.AccountComponent
open Business.CrossDomainOrchestration.Reconciliation
open Ui.InterfaceBridge.InterfaceContracts.ReconciliationContracts

let ``convert [ReconciliationInputRow] to [ReconciliationRequest]``
    (inputRow: ReconciliationInputRow)
    : Result<ReconciliationRequest, IAppError> =
    result {
        let! accountCode = inputRow.accountCode |> AccountCode.create
        let! externalBalance = inputRow.externalBalance |> Money.fromDecimal
        return { accountCode = accountCode; externalBalance = externalBalance; asOf = inputRow.asOf }
    }

let ``convert [ReconciliationInput] to [ReconciliationRequest list]``
    (input: ReconciliationInput)
    : Result<ReconciliationRequest list, IAppError> =
    input.rows
    |> List.map ``convert [ReconciliationInputRow] to [ReconciliationRequest]``
    |> convertListOfResultsToResultsList

let ``convert [ReconciliationRow] to [ReconciliationReturnRow]``
    (row: ReconciliationRow)
    : ReconciliationReturnRow =
    { accountCode = row.accountCode |> AccountCode.value
      accountName = row.accountName |> AccountName.value
      asOf = row.asOf
      externalBalance = row.externalBalance |> Money.amount
      ledgerBalance = row.ledgerBalance |> Money.amount
      delta = row.delta |> Money.amount }
