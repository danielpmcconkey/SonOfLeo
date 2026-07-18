module InterfaceBridge.InterfaceContracts.JournalContracts

open System
open NodaTime
open Utilities.FieldUpdate

type JournalEntryLineInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    accountCode: string
    amount: decimal
    lineType: string
    memo: string option }

type JournalEntryHeaderInput= { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    description: string
    source: string option
    entryDate: LocalDate }

type JournalEntryExternalReferenceInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    financialInstitution: string 
    referenceText: string }

type JournalEntryCommentInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    secondaryJournalEntryId: Guid option
    commentText: string }

type JournalEntryInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    header: JournalEntryHeaderInput
    lines: JournalEntryLineInput list
    externalReferences: JournalEntryExternalReferenceInput list
    comments: JournalEntryCommentInput list }

type JournalEntryLineReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    id: Guid
    accountCode: string
    amount: decimal
    lineType: string
    memo: string option
    createdAt: Instant
    modifiedAt: Instant }

type JournalEntryHeaderReturn= { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    id: Guid
    description: string
    source: string option
    entryDate: LocalDate
    voidedAt: Instant option
    createdAt: Instant
    modifiedAt: Instant }

type JournalEntryExternalReferenceReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    id: Guid
    financialInstitution: string 
    referenceText: string
    createdAt: Instant
    modifiedAt: Instant }

type JournalEntryCommentReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    id: Guid
    primaryJournalEntryId: Guid
    secondaryJournalEntryId: Guid option
    commentText: string
    createdAt: Instant
    modifiedAt: Instant }

type JournalEntryReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    header: JournalEntryHeaderReturn
    lines: JournalEntryLineReturn list
    externalReferences: JournalEntryExternalReferenceReturn list
    comments: JournalEntryCommentReturn list }

type JournalEntryFetchByIdInput = { id: Guid } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryFetchByPeriodInput = { periodKey: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryFetchLinesByAccountInput = { accountCode: string; nonVoidedOnly: bool } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryFetchByExternalReferenceInput = { fi: string option; reference: string option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryVoidInput = { id: Guid; reason: JournalEntryCommentInput } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryUpdateExternalReferenceInput = { id: Guid; fi: string option; reference: string option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryAddExternalReferenceInput = { journalEntryId: Guid; reference: JournalEntryExternalReferenceInput } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryAddCommentInput = { journalEntryId: Guid; comment: JournalEntryCommentInput } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryUpdateCommentInput = { id: Guid; secondaryJournalEntryId: FieldUpdate<Guid option>; commentText: FieldUpdate<string> } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
type JournalEntryFetchByDateRangeInput = { beginDate: LocalDate; endDateInclusive: LocalDate } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2


