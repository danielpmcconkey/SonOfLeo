module InterfaceBridge.InterfaceContracts.SharedContracts

open NodaTime


type FilterDateRangeInput =
    {
      beginDate: LocalDate
      endInclusive: LocalDate }

type TemporalFilterInput =
    | PeriodKey of string
    | DateRange of FilterDateRangeInput
