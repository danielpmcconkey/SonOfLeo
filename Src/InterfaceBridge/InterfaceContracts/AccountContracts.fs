module InterfaceBridge.InterfaceContracts.AccountContracts

open Model.Ledger.FiscalPeriods
open NodaTime
open System

// ****************************************
// RETURN
// ****************************************

type AccountReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
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

type AccountActivityDetailReturn = {  lineId: Guid // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
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

type AccountActivityReturn = {    accountCode: string // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
                                  accountName: string
                                  accountType: string
                                  accountSubtype: string option
                                  accountParentCode: string option
                                  accountExternalRef: string option
                                  activityDetail: AccountActivityDetailReturn option }
type AccountBalanceReturn = {   accountCode: string // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
                                totalCredits: decimal
                                totalDebits: decimal
                                netBalance: decimal }

// ****************************************
// CREATE
// ****************************************

type AccountCreateInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
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

type AccountFetchByCodeInput = { code: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type AccountFetchByParentCodeInput = { parentCode: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type AccountFetchByAccountTypeInput = { accountTypeSt: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type AccountFetchAllInput = { activeOnly: bool; } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type AccountActivitySortInput =
    | AccountCode
    | EntryDate
type AccountActivityFilterDateRangeInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    beginDate: LocalDate
    endInclusive: LocalDate
}

type AccountActivityTemporalFilterInput = // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    | PeriodKey of string
    | DateRange of AccountActivityFilterDateRangeInput

type AccountActivityFilterInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    accountCode: string option
    temporalFilter: AccountActivityTemporalFilterInput option
    source: string option
    accountType: string option
    accountSubtype: string option
    accountParentCode: string option
    journalEntryId: Guid option
    amount: decimal option
    description: string option
    unVoidedOnly: bool
}

type AccountActivityFetchInput = { filter: AccountActivityFilterInput; sort: AccountActivitySortInput option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2

type AccountBalanceFetchByAccountListInput = { codes: string list; asOf: LocalDate option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2

// ****************************************
// UPDATE
// ****************************************

type AccountDeactivationInput = { code: string; activeEnd: LocalDate } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type AccountUpdateNameInput = { code: string; newName: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type AccountUpdateExternalReferenceInput = { code: string; newReference: string option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    
    