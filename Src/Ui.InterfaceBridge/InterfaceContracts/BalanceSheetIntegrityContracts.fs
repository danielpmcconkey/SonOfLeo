module Ui.InterfaceBridge.InterfaceContracts.BalanceSheetIntegrityContracts

open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

// with Report and interpolateAsOf, the date appended to the file name is the as-of date
type BalanceSheetIntegrityInput = { asOf: ReportAsOf; reportOutput: OutputSpecifier }

type BalanceSheetIntegrityReturnRow = {
    asOf: LocalDate
    totalDebits: decimal
    totalCredits: decimal
    debitsEqualCredits: bool
    // each account type's net balance in its normal-balance direction
    assets: decimal
    liabilities: decimal
    equity: decimal
    revenue: decimal
    expenses: decimal
    netIncome: decimal
    // assets minus (liabilities plus equity plus net income)
    residual: decimal
}

type BalanceSheetIntegrityReturn =
    | DataOnly of BalanceSheetIntegrityReturnRow
    | Report of OutputPathReturn
