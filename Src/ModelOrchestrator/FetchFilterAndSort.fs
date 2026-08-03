module ModelOrchestrator.FetchFilters

open System
open Model
open Model.Ledger.Accounts.AccountComponent
open Model.Ledger.FiscalPeriods
open Model.Ledger.Journaling.JournalEntryComponent
open NodaTime

type FetchSort =
    | AccountCodeAsc
    | AccountCodeDesc
    | EntryDateAsc
    | EntryDateDesc
    | AmountAsc
    | AmountDesc

type FilterDateRange = { beginDate: LocalDate; endInclusive: LocalDate }

type TemporalFilter =
    | FiscalPeriodIdentifier of FiscalPeriodId
    | DateRange of FilterDateRange

type AccountActivityFilter =
    { accountId: AccountId option
      temporalFilter: TemporalFilter option
      source: JournalEntrySource option
      accountType: AccountType option
      accountSubtype: AccountSubtype option
      accountParentId: AccountId option
      journalEntryId: JournalEntryHeaderId option
      amount: Money option
      description: JournalEntryDescription option
      unVoidedOnly: bool }

type JournalEntryFetchFilter =
    { journalEntryHeaderId: JournalEntryHeaderId option
      source: JournalEntrySource option
      financialInstitution: JournalRefFinancialInstitution option
      referenceText: JournalExternalReferenceText option
      temporalFilter: TemporalFilter option
      unVoidedOnly: bool }
