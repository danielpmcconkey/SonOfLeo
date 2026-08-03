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

type FiscalPeriodCreateInput = { periodKey: string }
type FiscalPeriodFetchByKeyInput = { periodKey: string }
type FiscalPeriodCloseInput = { periodKey: string }
type FiscalPeriodReopenInput = { periodKey: string }
type FiscalPeriodFetchAllInput = { openOnly: bool }
