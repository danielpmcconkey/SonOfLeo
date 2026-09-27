module Ui.InterfaceBridge.InterfaceContracts.ReconciliationContracts

open NodaTime

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
