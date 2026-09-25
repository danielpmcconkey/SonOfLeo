module Ui.InterfaceBridge.InterfaceContracts.JournalContracts

open System
open NodaTime
open App.Utility.FieldUpdate

type JournalEntryLineInput =
    {
      accountCode: string
      amount: decimal
      lineType: string
      memo: string option }

type JournalEntryHeaderInput =
    {
      description: string
      source: string option
      entryDate: LocalDate }

type JournalEntryExternalReferenceInput =
    {
      financialInstitution: string
      referenceText: string }

type JournalEntryCommentInput =
    {
      secondaryJournalEntryId: Guid option
      commentText: string }

type JournalEntryInput =
    {
      header: JournalEntryHeaderInput
      lines: JournalEntryLineInput list
      externalReferences: JournalEntryExternalReferenceInput list
      comments: JournalEntryCommentInput list }

type JournalEntryLineReturn =
    {
      id: Guid
      accountCode: string
      accountName: string
      amount: decimal
      lineType: string
      memo: string option
      createdAt: Instant
      modifiedAt: Instant }

type JournalEntryHeaderReturn =
    {
      id: Guid
      description: string
      source: string option
      entryDate: LocalDate
      voidedAt: Instant option
      createdAt: Instant
      modifiedAt: Instant }

type JournalEntryExternalReferenceReturn =
    {
      id: Guid
      financialInstitution: string
      referenceText: string
      createdAt: Instant
      modifiedAt: Instant }

type JournalEntryCommentReturn =
    {
      id: Guid
      primaryJournalEntryId: Guid
      secondaryJournalEntryId: Guid option
      commentText: string
      createdAt: Instant
      modifiedAt: Instant }

type JournalEntryReturn =
    {
      header: JournalEntryHeaderReturn
      lines: JournalEntryLineReturn list
      externalReferences: JournalEntryExternalReferenceReturn list
      comments: JournalEntryCommentReturn list }
    
/// TrialBalanceReturnRow is in the JE contracts because the concept of a trial balance is a function of account plus
/// journal entries. A trial balance *report* is a reporting function, but the data used to create that report is of the
/// JE domain
type TrialBalanceReturnRow =
    { accountCode: string
      accountName: string
      generation: int
      totalCredits: decimal
      totalDebits: decimal
      netBalance: decimal }




type JournalEntryFetchByIdInput = { id: Guid }
type JournalEntryFetchByPeriodInput = { periodKey: string }
type JournalEntryFetchLinesByAccountInput = { accountCode: string; nonVoidedOnly: bool }
type JournalEntryFetchByExternalReferenceInput = { fi: string option; reference: string option }
type JournalEntryVoidInput = { id: Guid; reason: JournalEntryCommentInput }
type JournalEntryUpdateExternalReferenceInput = { id: Guid; fi: string option; reference: string option }
type JournalEntryAddExternalReferenceInput = { journalEntryId: Guid; reference: JournalEntryExternalReferenceInput }
type JournalEntryAddCommentInput = { journalEntryId: Guid; comment: JournalEntryCommentInput }
type JournalEntryUpdateCommentInput =
    { id: Guid; secondaryJournalEntryId: FieldUpdate<Guid option>; commentText: FieldUpdate<string> }
type JournalEntryFetchByDateRangeInput = { beginDate: LocalDate; endDateInclusive: LocalDate }
