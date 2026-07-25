module InterfaceBridge.InterfaceContracts.FiscalPeriodContracts

open NodaTime

// return
type FiscalPeriodReturn =
    { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
      periodKey: string
      startDate: LocalDate
      endDate: LocalDate
      isOpen: bool
      createdAt: Instant
      modifiedAt: Instant }

/// FiscalPeriodInput is a multi-purpose interface contract, used for create, fetch by key, close, and reopen
type FiscalPeriodInput =
    { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
      periodKey: string }
type FiscalPeriodFetchAllInput = { openOnly: bool } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
