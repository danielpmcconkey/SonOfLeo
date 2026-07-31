module InterfaceBridge.InterfaceContracts.FiscalPeriodContracts

open NodaTime

// return
type FiscalPeriodReturn =
    {
      periodKey: string
      startDate: LocalDate
      endDate: LocalDate
      isOpen: bool
      createdAt: Instant
      modifiedAt: Instant }

/// FiscalPeriodInput is a multi-purpose interface contract, used for create, fetch by key, close, and reopen
type FiscalPeriodInput =
    {
      periodKey: string }
type FiscalPeriodFetchAllInput = { openOnly: bool }
