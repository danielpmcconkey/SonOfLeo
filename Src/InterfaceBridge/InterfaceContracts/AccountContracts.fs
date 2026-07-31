module InterfaceBridge.InterfaceContracts.AccountContracts

open InterfaceBridge.InterfaceContracts.SharedContracts
open ModelOrchestrator.FetchFilters
open NodaTime
open System

// ****************************************
// RETURN
// ****************************************

type AccountReturn =
    {
      code: string
      name: string
      accountTypeSt: string
      activeBegin: LocalDate
      activeEnd: LocalDate option
      subType: string option
      parentCode: string option
      reference: string option
      createdAt: Instant
      modifiedAt: Instant }

type AccountActivityDetailReturn =
    { lineId: Guid
      amount: decimal
      lineType: string
      lineMemo: string option
      lineCreatedAt: Instant
      lineModifiedAt: Instant
      journalEntryId: Guid
      entryDate: LocalDate
      journalEntryDescription: string
      journalEntrySource: string option
      journalEntryVoidedAt: Instant option }

type AccountActivityReturn =
    { accountCode: string
      accountName: string
      accountType: string
      accountSubtype: string option
      accountParentCode: string option
      accountExternalRef: string option
      activityDetail: AccountActivityDetailReturn option }
type AccountBalanceReturn =
    { accountCode: string
      totalCredits: decimal
      totalDebits: decimal
      netBalance: decimal }

// ****************************************
// CREATE
// ****************************************

type AccountCreateInput =
    {
      code: string
      name: string
      accountTypeSt: string
      activeBegin: LocalDate
      activeEnd: LocalDate option
      subType: string option
      parentCode: string option
      reference: string option }

// ****************************************
// READ
// ****************************************

type AccountFetchByCodeInput = { code: string }
type AccountFetchByParentCodeInput = { parentCode: string }
type AccountFetchByAccountTypeInput = { accountTypeSt: string }
type AccountFetchAllInput = { activeOnly: bool }

type AccountActivityFilterInput =
    {
      accountCode: string option
      temporalFilter: TemporalFilterInput option
      source: string option
      accountType: string option
      accountSubtype: string option
      accountParentCode: string option
      journalEntryId: Guid option
      amount: decimal option
      description: string option
      unVoidedOnly: bool }

type AccountActivityFetchInput = { filter: AccountActivityFilterInput; sort: FetchSort option }

type AccountBalanceFetchByAccountListInput = { codes: string list; asOf: LocalDate option }

// ****************************************
// UPDATE
// ****************************************

type AccountDeactivationInput = { code: string; activeEnd: LocalDate option }
type AccountUpdateNameInput = { code: string; newName: string }
type AccountUpdateExternalReferenceInput = { code: string; newReference: string option }
