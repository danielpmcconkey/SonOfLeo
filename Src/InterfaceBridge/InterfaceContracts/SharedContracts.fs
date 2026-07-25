module InterfaceBridge.InterfaceContracts.SharedContracts

open NodaTime


type FilterDateRangeInput =
    { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
      beginDate: LocalDate
      endInclusive: LocalDate }

type TemporalFilterInput = // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    | PeriodKey of string
    | DateRange of FilterDateRangeInput
