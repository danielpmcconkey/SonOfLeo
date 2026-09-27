module Ui.InterfaceBridge.BoundaryConverters.BalanceSheetIntegrityConverters

open Business.FinancialServices
open Business.CrossDomainOrchestration.BalanceSheetIntegrity
open Ui.InterfaceBridge.InterfaceContracts.BalanceSheetIntegrityContracts

let ``convert [BalanceSheetIntegrity] to [BalanceSheetIntegrityReturnRow]``
    (integrity: BalanceSheetIntegrity)
    : BalanceSheetIntegrityReturnRow =
    { asOf = integrity.asOf
      totalDebits = integrity.totalDebits |> Money.amount
      totalCredits = integrity.totalCredits |> Money.amount
      debitsEqualCredits = integrity.debitsEqualCredits
      assets = integrity.assets |> Money.amount
      liabilities = integrity.liabilities |> Money.amount
      equity = integrity.equity |> Money.amount
      revenue = integrity.revenue |> Money.amount
      expenses = integrity.expenses |> Money.amount
      netIncome = integrity.netIncome |> Money.amount
      residual = integrity.residual |> Money.amount }
