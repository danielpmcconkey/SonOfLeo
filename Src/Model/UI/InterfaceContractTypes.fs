namespace Model.UI

open System
open NodaTime

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

    type JournalEntryLineInput = {
        accountCode: string
        amount: decimal
        lineType: string
        memo: string option }

    type JournalEntryHeaderInput= {
        description: string
        source: string option
        entryDate: LocalDate
        voidedAt: Instant option }

    type JournalEntryExternalReferenceInput = {
        financialInstitution: string 
        referenceText: string }

    type JournalEntryCommentInput = {
        secondaryJournalEntryId: Guid option
        commentText: string }

    type JournalEntryInput = {
        header: JournalEntryHeaderInput
        lines: JournalEntryLineInput list
        externalReferences: JournalEntryExternalReferenceInput list
        comments: JournalEntryCommentInput list }
    
    type JournalEntryLineReturn = {
        id: Guid
        accountCode: string
        amount: decimal
        lineType: string
        memo: string option
        createdAt: Instant
        modifiedAt: Instant }

    type JournalEntryHeaderReturn= {
        id: Guid
        description: string
        source: string option
        entryDate: LocalDate
        voidedAt: Instant option
        createdAt: Instant
        modifiedAt: Instant }

    type JournalEntryExternalReferenceReturn = {
        id: Guid
        financialInstitution: string 
        referenceText: string
        createdAt: Instant
        modifiedAt: Instant }

    type JournalEntryCommentReturn = {
        id: Guid
        secondaryJournalEntryId: Guid option
        commentText: string
        createdAt: Instant
        modifiedAt: Instant }

    type JournalEntryReturn = {
        header: JournalEntryHeaderReturn
        lines: JournalEntryLineReturn list
        externalReferences: JournalEntryExternalReferenceReturn list
        comments: JournalEntryCommentReturn list }

    type JournalEntryFetchByIdInput = { id: Guid }
    type JournalEntryFetchByPeriodInput = { periodKey: string }
    type JournalEntryFetchByAccountInput = { accountCode: string }
    type JournalEntryFetchByExternalReferenceInput = { fi: string; reference: string }
    type JournalEntryVoidInput = { id: Guid; reason: JournalEntryCommentInput }
    type JournalEntryUpdateExternalReferenceInput = { id: Guid; fi: string; reference: string }
    type JournalEntryAddExternalReferenceInput = { journalEntryId: Guid; reference: JournalEntryExternalReferenceInput }
    type JournalEntryAddCommentInput = { journalEntryId: Guid; reference: JournalEntryCommentInput }
    type JournalEntryAmendCommentInput = { id: Guid; secondaryJournalEntryId: Guid option; commentText: string }
    
    

