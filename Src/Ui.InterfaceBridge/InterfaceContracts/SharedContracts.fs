module Ui.InterfaceBridge.InterfaceContracts.SharedContracts

open NodaTime

type FilterDateRangeInput =
    {
      beginDate: LocalDate
      endInclusive: LocalDate }

type TemporalFilterInput =
    | PeriodKey of string
    | DateRange of FilterDateRangeInput

// NoInput names the input contract of a route that takes none. It is never deserialized -- those handlers ignore the
// payload entirely -- so it exists to say so in the route table rather than to carry data.
type NoInput = NoInput
