namespace Model.UI

open System
open NodaTime
open Utilities.DAL

module InterfaceContractTypes =

    type CommandRoute = { // REQ-NGUI-1.1
        domain: string
        verb: string
        description: string
        inputType: string
        outputType: string
        handler: string -> string list -> Result<string, string> }  // REQ-NGUI-1.2
    
    
    // ****************************************
    // ACCOUNT DOMAIN
    // ****************************************
    
    // return
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
    
    // create
    type AccountCreateInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        code: string
        name: string
        accountTypeSt: string
        activeBegin: LocalDate
        activeEnd: LocalDate option
        subType: string option
        parentCode: string option
        reference: string option }
    // read
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
        | FiscalPeriodId of Guid
        | DateRange of AccountActivityFilterDateRangeInput

    type AccountActivityFilterInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        accountCode: string option
        temporalFilter: AccountActivityTemporalFilterInput option
        source: string option
        accountType: string option
        accountSubtype: string option
        accountParentCode: string option
        journalEntryId: Guid option
        unVoidedOnly: bool
    }
    
    type AccountActivityFetchInput = { filter: AccountActivityFilterInput; sort: AccountActivitySortInput option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    
    type AccountBalanceFetchByAccountListInput = { codes: string list } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    
    // update
    type AccountDeactivationInput = { code: string; activeEnd: LocalDate } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountUpdateNameInput = { code: string; newName: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type AccountUpdateExternalReferenceInput = { code: string; newReference: string option } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    
    
    // ****************************************
    // FISCAL PERIOD DOMAIN
    // ****************************************
    
    // return
    type FiscalPeriodReturn = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        periodKey: string
        startDate: LocalDate
        endDate: LocalDate
        isOpen: bool
        createdAt: Instant
        modifiedAt: Instant }
    
    /// FiscalPeriodInput is a multi-purpose interface contract, used for create, fetch by key, close, and reopen
    type FiscalPeriodInput = { // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
        periodKey: string }
    type FiscalPeriodFetchAllInput = { openOnly: bool; } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    
    
    // ****************************************
    // JOURNAL ENTRY DOMAIN
    // ****************************************

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
    type JournalEntryUpdateExternalReferenceInput = { id: Guid; fi: string; reference: string } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type JournalEntryAddExternalReferenceInput = { journalEntryId: Guid; reference: JournalEntryExternalReferenceInput } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type JournalEntryAddCommentInput = { journalEntryId: Guid; comment: JournalEntryCommentInput } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type JournalEntryUpdateCommentInput = { id: Guid; secondaryJournalEntryId: FieldUpdate<Guid option>; commentText: FieldUpdate<string> } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    type JournalEntryFetchByDateRangeInput = { beginDate: LocalDate; endDateInclusive: LocalDate } // REQ-NGUI-2.1, REQ-NGUI-2.1.1, REQ-NGUI-2.2
    

