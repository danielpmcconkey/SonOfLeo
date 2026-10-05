module Ui.InterfaceBridge.InterfaceContracts.ReportsContracts

open NodaTime
open Ui.InterfaceBridge.InterfaceContracts.JournalContracts

// Declaration order matters here: F# infers an unannotated record literal's type from the last type declared with its
// labels. So a report's types that share labels with ReportAsOf or TrialBalanceReportInput come before them.

/// A journal entry touching a deactivated account, posted or voided after the account's active end.
type DeactivatedAccountEntryReturnRow = {
    journalEntryId: System.Guid
    entryDate: LocalDate
    description: string
    postedAt: NodaTime.Instant
    voidedAt: NodaTime.Instant option
}

type DeactivatedAccountWithBalanceReturnRow = {
    accountCode: string
    accountName: string
    activeEnd: LocalDate
    balance: decimal
    entriesAfterActiveEnd: DeactivatedAccountEntryReturnRow list
}

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
    // as of the date the report runs; empty when every deactivated account holds zero
    deactivatedAccountsWithBalance: DeactivatedAccountWithBalanceReturnRow list
}

/// One balance captured from an institution, in the account's normal-balance direction (a credit card balance owed is
/// positive). A clearing account is reconciled by supplying zero.
type ReconciliationInputRow = { accountCode: string; externalBalance: decimal; asOf: LocalDate }

type ReconciliationInput = { rows: ReconciliationInputRow list }

/// delta is externalBalance minus ledgerBalance.
type ReconciliationReturnRow =
    { accountCode: string
      accountName: string
      asOf: LocalDate
      externalBalance: decimal
      ledgerBalance: decimal
      delta: decimal }

type PeriodActivityLineReturnRow = {
    entryDate: LocalDate
    journalEntryId: System.Guid
    description: string
    lineType: string
    amount: decimal
    memo: string option
}

type PeriodActivityAccountReturnRow = {
    accountCode: string
    accountName: string
    // in the account's normal-balance direction
    netTotal: decimal
    lines: PeriodActivityLineReturnRow list
}

type ReportAsOf = { asOf: LocalDate }

type OutputPathInput = {
    baseDir: string
    // if true, the file is named fileName, a hyphen and the report's date in yyyy-MM-dd format, then .html; if false,
    // fileName then .html. The report's date is its as-of date unless its input says otherwise (period activity uses
    // its range). The writer appends .html either way, so fileName carries no extension.
    interpolateAsOf: bool
    fileName: string
}
type OutputPathReturn = { fullyQualifiedPath: string }
    
type OutputSpecifier =
    | DataOnly
    | Report of OutputPathInput
    
// begin and end are inclusive; with Report and interpolateAsOf, the file name gets -yyyy-MM-dd_yyyy-MM-dd (begin_end)
type PeriodActivityInput = { beginDate: LocalDate; endDate: LocalDate; reportOutput: OutputSpecifier }

// with Report and interpolateAsOf, the date appended to the file name is the as-of date. Declared ahead of the
// trial balance's input, which has the same fields, so that a bare record of those fields still reads as that one.
type NetWorthInput = { asOf: ReportAsOf; reportOutput: OutputSpecifier }

// with Report and interpolateAsOf, the date appended to the file name is the as-of date
type BalanceSheetIntegrityInput = { asOf: ReportAsOf; reportOutput: OutputSpecifier }

type TrialBalanceReportInput = { asOf: ReportAsOf; reportOutput: OutputSpecifier }

type BalanceSheetIntegrityReturn =
    | DataOnly of BalanceSheetIntegrityReturnRow
    | Report of OutputPathReturn

type PeriodActivityReturn =
    | DataOnly of PeriodActivityAccountReturnRow list
    | Report of OutputPathReturn

type TrialBalanceReportReturn = 
    | DataOnly of TrialBalanceReturnRow list
    | Report of OutputPathReturn

type PrePostingReviewInput = {
    // with Report and interpolateAsOf, the date appended to the file name is the date the report runs
    reportOutput: OutputSpecifier
}

type PrePostingPaymentReturnRow = {
    paymentId: System.Guid
    invoiceId: System.Guid
    paymentAmount: decimal
    invoiceDate: LocalDate
    dueDate: LocalDate
    invoiceAmount: decimal
    paymentState: string
    instanceDate: LocalDate
}

type PrePostingLineReturnRow = {
    stageEntryLineId: System.Guid
    lineType: string
    amount: decimal
    memo: string option
    accountCode: string
    accountName: string
    // the classification rule that assigned the account; None when the parser or the operator did
    ruleName: string option
    paymentAgreementName: string option
    masterAgreementName: string option
    payments: PrePostingPaymentReturnRow list
}

type PrePostingEntryReturnRow = {
    stageEntryHeaderId: System.Guid
    entryDate: LocalDate
    description: string
    sourceName: string
    fiReference: string
    status: string
    lines: PrePostingLineReturnRow list
}

type PrePostingReviewReturn =
    | DataOnly of PrePostingEntryReturnRow list
    | Report of OutputPathReturn


// every ledger account carries its name beside its code
type NetWorthLedgerAccountReturnRow = { code: string; name: string; balance: decimal }

type NetWorthInvestmentAccountReturnRow = {
    accountName: string
    owners: string list
    accountGroup: string
    taxTreatment: string
    snapshotDate: LocalDate
    provenance: string
    marketValue: decimal
    contributionBasis: decimal option
}

type NetWorthPropertyReturnRow = {
    propertyName: string
    propertyUse: string
    owners: string list
    value: decimal
    // the date of the Valuation the value came from; None when the value is the purchase basis
    valuationDate: LocalDate option
    valueIsPurchaseBasis: bool
    mortgageAccounts: NetWorthLedgerAccountReturnRow list
    equity: decimal
}

type NetWorthGroupTotalReturnRow = { group: string; marketValue: decimal }

type NetWorthReturnRow = {
    asOf: LocalDate
    assetAccounts: NetWorthLedgerAccountReturnRow list
    liabilityAccounts: NetWorthLedgerAccountReturnRow list
    investmentAccounts: NetWorthInvestmentAccountReturnRow list
    properties: NetWorthPropertyReturnRow list
    totalLedgerAssets: decimal
    totalInvestments: decimal
    totalPropertyValues: decimal
    totalLiabilities: decimal
    totalOwnedPropertyMortgages: decimal
    netWorth: decimal
    investableWealth: decimal
    investmentsByTaxTreatment: NetWorthGroupTotalReturnRow list
    investmentsByAccountGroup: NetWorthGroupTotalReturnRow list
}

type NetWorthReturn =
    | DataOnly of NetWorthReturnRow
    | Report of OutputPathReturn

// grouping is one of Account, AccountGroup, TaxTreatment, Owners, or a dimension name (InvestmentType, MarketCap,
// IndexType, Sector, Region, Objective, Benchmark); with Report and interpolateAsOf, the file name gets
// -yyyy-MM-dd_yyyy-MM-dd (begin_end)
type InvestmentWealthHistoryInput = {
    beginDate: LocalDate
    endDate: LocalDate
    grouping: string
    reportOutput: OutputSpecifier
}

type WealthGroupContract =
    | Named of string
    // an account's complete owner set
    | Owners of string list
    // lines whose Security has no value in the dimension grouped by
    | Unassigned

type WealthGroupTotalReturnRow = { group: WealthGroupContract; marketValue: decimal }

type WealthPointReturnRow = { monthEnd: LocalDate; totals: WealthGroupTotalReturnRow list; total: decimal }

type InvestmentWealthHistoryReturn =
    | DataOnly of WealthPointReturnRow list
    | Report of OutputPathReturn
